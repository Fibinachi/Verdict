# Verdict Juror Opinion Engine — Mathematical Reference

This document provides a complete mathematical reference for the juror bias and opinion simulation engine. All formulas are implemented in `Services/ToyJurorLogicEngine.cs`.

---

## 1. Core Functions

### Logistic (Sigmoid) Function
```raw
logistic(x) = 1 / (1 + exp(-x))
```
Maps any real number to the range (0, 1). Used to convert linear scores into probabilities/beliefs.

### Clamping Functions
```raw
clamp01(x) = max(0, min(1, x))
clamp10(x) = max(0, min(10, x))
clamp11(x) = max(-1, min(1, x))
```

---

## 2. Master Pipeline: Three-Stage Verdict Formation

### Stage 1: Static Bias → Initial Belief (g₁)

```raw
b = static_bias(traits) = intercept
g₁ = logistic(β₀ + β₁ · b)
```

**Current coefficients:**
| Parameter | Value | Description |
|-----------|-------|-------------|
| β₀ (intercept) | 0.02 | Slight pro-plaintiff baseline (civil case framing) |
| β₁ | 1.0 | Scaling factor for static bias |
| α_pol_id | 0.9 | Political identity → verdict direction |
| α_conserv_relig | 0.35 | Conservative religiosity → conservative lean |
| α_education_years | 0.10 | Higher education → slightly plaintiff-leaning |
| α_implicit_bias | 0.25 | Unconscious associations → initial lean |

### Stage 2: Evidence Drift + Rigidity → Updated Belief (g₂)

```raw
λ = rigidity(traits) = logistic(η_intercept + Σ ηᵢ · traitᵢ)
g₂ = clamp01(g₁ + γ₁ · Δevidence · (1 - λ))
```

**Current coefficients (η):**
| Factor | Weight | Interpretation |
|--------|--------|----------------|
| RWA | 0.55 | Right-wing authoritarianism strongly resists counterevidence |
| SDO | 0.45 | Social dominance orientation resists status-quo challenges |
| Punitiveness | 0.25 | Punitive mindset resists leniency evidence |
| Conserv_Relig | 0.25 | Religious conservatism increases rigidity |
| Pol_Strength | 0.25 | Strong political identity → more rigid |
| Primary_History | 0.10 | Prior political engagement |
| Online_Partisan | 0.08 | Online echo chamber exposure |
| Income_Band | 0.10 | Economic status affects openness |
| Job_Security | 0.06 | Job security → moderate rigidity |
| Home_Own | 0.05 | Homeownership → slight rigidity |

**γ₁ (evidence sensitivity):** 1.0

### Stage 3: Deliberation + Conformity → Final Verdict Lean

```raw
M = Σ(wᵢ · g₂ᵢ) / Σ(wᵢ)          [weighted jury consensus]
φ = conformity(traits)             [susceptibility to peer influence]
g₃ = g₂ᵢ + (1 - hᵢ) · φ · (M - g₂ᵢ)   [deliberation update]
P = logistic(θ₀ + θ₁ · g₃)         [verdict probability]
verdict_lean = clamp(0.2, 0.8, P)  [mapped to 0..1 range]
```

**Current coefficients (θ):**
| Parameter | Value | Description |
|-----------|-------|-------------|
| θ₀ | 0.0 | Threshold offset |
| θ₁ | 5.0 | Steepness of verdict mapping |

---

## 3. Trait Generation from Agent Demographics

### 3.1 Demographic → Trait Mapping (Research-Backed)

Each juror agent has observable demographics. These are mapped to the latent trait space using **bias-category weights** that represent the relative influence of each demographic dimension. Default weights are derived from meta-analyses of jury research (see `docs/JurorBiasResearch.md`).

#### Bias Factor Weights (Default Values)
| Factor | Weight | Source |
|--------|--------|--------|
| Political Affiliation | 0.90 | Strongest predictor (Forresta 2025) |
| Education Level | 0.80 | Analytical evaluation (RAND 2024) |
| Legal Knowledge | 0.75 | Expertise influences interpretation |
| Juror Experience | 0.70 | Deliberation participation |
| Community Ties | 0.65 | Social identity influences decisions |
| Ethnicity | 0.60 | Racial bias in verdicts |
| Age | 0.55 | Perspective and credibility assessment |
| Income Level | 0.50 | Economic perspective |
| Gender | 0.45 | Context-dependent influence |
| Religion | 0.40 | Case-type variation |
| Communication Style | 0.35 | Narrative influence |

### 3.2 Implicit Bias
```raw
implicit_bias ~ Uniform(-0.6, 0.6) × 1.2  [negative skew]
```
Simulates unconscious associations distinct from explicit attitudes. Accounts for ~15% of variance in juror decisions (Greenwald et al.).

### 3.3 Cross-Factor Interactions

**Race × Gender Compound Bias:**
```raw
if non-white AND female:  compound_bias = -0.15 × ethnicity_weight
if non-white AND male:    compound_bias = -0.10 × ethnicity_weight
else:                     compound_bias = 0
```
*Rationale: Compounded stereotyping of women of color in credibility assessments (Crenshaw 1989).*

**Education × Political Rigidity Interaction:**
```raw
edu_pol_rigidity = edu_weight × pol_weight × 0.3
```
*Rationale: Educated partisans show MORE motivated reasoning, not less (Kahan et al. 2012).*

**Age × Punitiveness (Curvilinear/U-Shaped):**
```raw
if age < 30:  age_punitive = (30 - age) × 0.04
if age > 60:  age_punitive = (age - 60) × 0.03
else:         age_punitive = 0
```
*Rationale: Young and old jurors show higher punitiveness; middle-aged most lenient (Steiner et al. 2001).*

**Gender × Religion Interaction (Rape Myth Acceptance):**
```raw
gender_religion_interaction = (gender_weight - 0.5) × (religion_weight - 0.5) × 0.6
```
*Rationale: Conservative gender views combined with religiosity increase myth acceptance (Burt & Albin 1981).*

**Religion × Political Conservatism Interaction:**
```raw
religion_pol_interaction = (religion_weight - 0.5) × (pol_weight - 0.5) × 0.4
```
*Rationale: Religious conservatism and political conservatism reinforce each other.*

### 3.4 Reliability-Weighted Civic Engagement
```raw
reliability_factor = edu_weight × 0.5 + legal_weight × 0.3 + experience_weight × 0.2
```
More educated and legally knowledgeable jurors are more likely to participate actively in deliberation.

---

## 4. Specific Trait Formulas

### RWA (Right-Wing Authoritarianism) — Range 0..10
```raw
RWA = clamp10(5 + bias × 3.2 × polW + age_factor + (ethnicityW - 0.5) × 0.6
    + edu_pol_rigidity + race_gender_compound × 0.5 + implicit_bias × 0.3
    + random(-1, 1) × 1.8)
```

### SDO (Social Dominance Orientation) — Range 0..10
```raw
SDO = clamp10(5 + (-bias) × 2.8 × polW + (incomeW - 0.5) × 1.2 + (genderW - 0.5) × 0.4
    + (eduW - 0.5) × (-0.2) + race_gender_compound × 0.3
    + random(-1, 1) × 1.8)
```

### Punitiveness — Range 0..10
```raw
Punitiveness = clamp10(5 + bias × 2.1 × polW + (religionW - 0.5) × 1.0
    + age_punitive_curve × 1.2 + implicit_bias × 0.4
    + random(-1, 1) × 1.8)
```

### Rape Myth Acceptance — Range 0..10
```raw
RapeMyth = clamp10(5 + bias × 1.6 × polW + (religionW - 0.5) × 1.2
    + (eduW - 0.5) × (-0.4) + gender_religion_interaction
    + race_gender_compound × 0.4 + random(-1, 1) × 1.8)
```

### Religiosity — Range 0..10
```raw
Religiosity = clamp10(5 + (religionW - 0.5) × 2.0 + (commStyleW - 0.5) × 0.5
    + implicit_bias × 0.2 + random(-1, 1) × 1.8)
```

### Conservative Religiosity — Range 0..10
```raw
ConservRelig = clamp10(5 + bias × 2.1 × religionW + (ethnicityW - 0.5) × 0.6
    + (religionW - 0.5) × (polW - 0.5) × 0.4 + random(-1, 1) × 1.8)
```

### Political Polarity — Range -1..1
```raw
PolId = clamp11(bias × polW + implicit_bias × 0.2)
```

### Job Security — Range 0..10
```raw
JobSecurity = clamp10(5 + (incomeW - 0.5) × 2 + (profBgW - 0.5) × 1.0
    + (legalW - 0.5) × 1.0 + implicit_bias × 0.2 + random(-1, 1) × 1.8)
```

### Volunteerism — Range 0..10
```raw
Volunteer = clamp10(4 + (communityW - 0.5) × 3 + (experienceW - 0.5) × 1.0
    + (commStyleW - 0.5) × 1.0 + reliability_factor × 0.8 + random(-1, 1) × 1.8)
```

### Activism — Range 0..10
```raw
Activism = clamp10(4 + (communityW - 0.5) × 2 + (polW - 0.5) × 1.8
    + (experienceW - 0.5) × 0.8 + reliability_factor × 0.6 + random(-1, 1) × 1.8)
```

### Online Partisanship — Range 0..10
```raw
OnlinePartisan = clamp10(5 + (commStyleW - 0.5) × 1.5 + (polW - 0.5) × 1.5
    + (experienceW - 0.5) × 0.5 + random(-1, 1) × 1.8)
```

---

## 5. Rigidity, Hardness, and Influence Weight

### Rigidity (λ) — How strongly a juror resists counterevidence
```raw
λ = logistic(η_intercept + η_RWA × RWA + η_SDO × SDO + ...)
Higher λ → less evidence penetration
```

### Hardness (h) — Resistance to conformity pressure
```raw
h = logistic(ζ_intercept + ζ_RWA × RWA + ζ_SDO × SDO + ...)
Higher h → less susceptible to jury consensus
```

### Influence Weight (w) — How much a juror sways peers
```raw
w = exp(χ_intercept + χ_edu × 12 + χ_elite × ElitePrivate + ...)
Higher w → greater persuasive power in deliberation
```

---

## 6. Evidence Probative Weighting

Before evidence enters the three-stage pipeline, it is weighted for probative value. This ensures that different types of evidence have appropriate impact, and that witness credibility is assessed through each juror's biases.

### 6.1 Evidence Category Impact Multipliers

Each piece of evidence is classified into a category that determines its raw persuasive impact multiplier:

| Category | Multiplier | Rationale |
|----------|-----------|-----------|
| Video | 1.6× | Highest impact — visual + auditory + temporal |
| Physical | 1.5× | Tangible objects feel "real" |
| Image | 1.4× | Visual evidence is compelling |
| Audio | 1.3× | Hearing a voice adds authenticity |
| Testimony | 1.0× (scaled) | Actual impact depends on witness credibility |
| Document | 1.0× | Baseline — requires reading and parsing |

### 6.2 Probative Value Calculation

Probative value (legal weight, distinct from raw impact) is calculated as:

```raw
probative_base = category_base[EvidenceCategory]
probative_base *= 0.5 + ReliabilityScore × 0.5       [chain of custody scaling]
if direct_evidence:  probative_base *= 1.2           [direct vs. circumstantial]
if circumstantial:   probative_base *= 0.85
if hearsay/rumor:    probative_base *= 0.5
if outdated:         probative_base *= 0.6
if unauthenticated:  probative_base *= 0.7

[For testimonial evidence:]
credibility_base = 0.5  (default)
  expert witness:    0.85
  eyewitness:        0.60
  law enforcement:   0.75
  character witness: 0.45
  [red flags reduce by 0.6-0.7]

probative_value = probative_base × (0.5 + credibility × 0.5)
ProbativeValue = clamp(0.05, 1.0, probative_value)
```

### 6.3 Per-Juror Credibility Perception

Each juror perceives witness credibility through their own biases:

```raw
per_juror_cred = WitnessCredibility
  + education_adjustment    [more educated → more skeptical: -0.03 to -0.08]
  + age_adjustment          [older → more trusting: +0.08; young → skeptical: -0.05]
  + political_adjustment    [Dem +0.03; Rep -0.02]
  + in_group_bias           [+0.03 for same-race witnesses]
  + gender_adjustment       [female jurors +0.02 attunement]
  + status_adjustment       [attentive +0.05; skeptical -0.12; sympathetic +0.10]
  + risk_adjustment         [risk-averse → more skeptical]
per_juror_cred = clamp(0.05, 0.95, per_juror_cred)
```

### 6.4 Per-Juror Media Type Sensitivity

Jurors respond differently to different evidence media based on their demographics:

```raw
media_sensitivity = 1.0
  [Age < 30:]  Video +0.15, Image +0.08, Document -0.05
  [Age > 55:]  Video -0.05, Document +0.10, Testimony +0.08
  [High education:]  Video -0.08, Document +0.05, Testimony -0.05
  [Heavy TV consumer:]  Video +0.10, Image +0.05
  [Status effects:]  bored → Document -0.10; focused → Testimony +0.10
media_sensitivity = clamp(0.7, 1.3, media_sensitivity)
```

### 6.5 Evidence Influence on Verdict Lean

The per-evidence update to each juror's VerdictLean combines all factors:

```raw
perceived_probative = ProbativeValue
if IsTestimonial:
    perceived_probative *= 0.5 + per_juror_cred × 0.5

total_weight = jurorWeight × backgroundWeight × media_sensitivity
ΔVerdictLean = perceived_probative × total_weight
```

Where `backgroundWeight` is the juror's demographic susceptibility to the evidence content (from `JuryCalculationService.CalculateBackgroundWeight`), and `jurorWeight` is the base influence rate (default 0.02 for jurors).

### 6.6 Considered Damages (Per-Juror)

Each juror arrives at a personal damages assessment:

```raw
evidence_damages = EstimatedDamages
  × biasFactor          [1.0 + Bias × 0.3, range 0.7-1.3]
  × backgroundFactor    [0.5 + backgroundWeight × 0.5]
  × probativeFactor     [0.5 + ProbativeValue × 0.5]
  × credibilityFactor   [if testimonial: 0.5 + per_juror_cred × 0.5]

ConsideredDamages = ConsideredDamages × 0.7 + evidence_damages × 0.3  [weighted blend]
```

---

## 7. Research Sources

| Study | Key Finding | Impact on Model |
|-------|-------------|-----------------|
| Forresta 2025 | Political identity is strongest predictor of verdict | PolW = 0.90, highest weight |
| RAND 2024 | Education correlates with analytical evaluation | EduW = 0.80, second highest |
| Kahan et al. 2012 | Educated partisans show MORE motivated reasoning | Edu×Pol interaction term |
| Sunstein 2003 | Like-minded groups polarize | Conformity amplification in deliberation |
| Mutz 2006 | Diverse panels deliberate more thoroughly | Diversity exposure bonus in Chi |
| Steiner et al. 2001 | Age punitiveness is U-shaped | AgePunitiveCurve in Punitiveness |
| Burt & Albin 1981 | Gender×Religion predicts rape myth acceptance | GenderReligionInteraction |
| Greenwald et al. | Implicit bias accounts for ~15% of variance | implicitBias latent variable |
| Crenshaw 1989 | Intersectional compound bias | RaceGenderCompound term |

---

## 8. Notes for Future Calibration

1. **Coefficient fitness**: These coefficients are structurally grounded but numerically approximate. Future work should fit them against empirical verdict datasets.
2. **Interaction terms**: Currently pairwise; higher-order interactions (e.g., Race×Gender×Education) could be added.
3. **Dynamic weights**: Consider making weights context-dependent (e.g., crime type, case severity).
4. **Validation**: Compare simulated verdict distributions against real-world jury data for calibration.
---

## 9. Burden-of-Proof Classification (Post-Pipeline)

After the three-stage pipeline produces each juror's `VerdictLean` (range 0.0–1.0), the value is classified into a verdict position based on the case mode. The mapping is handled by `Services/BurdenOfProof.cs`.

### Criminal Case Thresholds (Beyond Reasonable Doubt)

```raw
conviction_threshold = 0.85
defense_threshold = 0.50

Classification:
  VerdictLean > 0.85  →  GUILTY           (beyond reasonable doubt)
  0.50 ≤ VerdictLean ≤ 0.85  →  REASONABLE DOUBT  (functionally NOT GUILTY)
  VerdictLean < 0.50  →  NOT GUILTY       (leans defense)
```

In criminal cases, the prosecution must prove guilt "beyond a reasonable doubt." A juror who thinks the defendant is probably guilty (e.g., lean = 0.70) but has reasonable doubt must vote NOT GUILTY. The 0.85 threshold reflects this high standard — the juror must be at least 85% convinced to convict.

### Civil Case Thresholds (Preponderance of Evidence)

```raw
conviction_threshold = 0.50

Classification:
  VerdictLean > 0.50  →  LIABLE
  VerdictLean < 0.50  →  NOT LIABLE
```

In civil cases, the plaintiff need only prove their case by a "preponderance of the evidence" (more likely than not, >50%). The standard threshold of 0.50 applies.

### Verdict Counting

```raw
proCount  = count(jurors where VerdictLean > conviction_threshold)
defCount  = count(jurors where VerdictLean ≤ conviction_threshold)

verdict = proCount == total ? (criminal ? "GUILTY" : "LIABLE") :
          defCount == total ? (criminal ? "NOT GUILTY" : "NOT LIABLE") :
          spread ≤ 0.10      ? majority_side_verdict  :
          hung_jury
```

A unanimous side (all jurors on the same side of the conviction threshold) always returns a verdict, regardless of lean spread. This correctly handles cases where all 12 jurors lean prosecution but with varying conviction levels (e.g., 0.55–0.82 spread in a civil case = LIABLE verdict).

### Effect on Deliberation Completion

The `IsDeliberationComplete` method now considers two forms of consensus:
1. **Tight spread**: max(lean) - min(lean) ≤ 0.10 (everyone agrees closely)
2. **Unanimous side**: all jurors are on the same side of the conviction threshold

Either form allows deliberation to end before the maximum round count.