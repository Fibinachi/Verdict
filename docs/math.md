# Verdict Juror Opinion Engine — Mathematical Reference

This document provides a complete mathematical reference for the juror bias and opinion simulation engine. All formulas are implemented in `Services/ToyJurorLogicEngine.cs`.

---

## 1. Core Functions

### Logistic (Sigmoid) Function
```
logistic(x) = 1 / (1 + exp(-x))
```
Maps any real number to the range (0, 1). Used to convert linear scores into probabilities/beliefs.

### Clamping Functions
```
clamp01(x) = max(0, min(1, x))
clamp10(x) = max(0, min(10, x))
clamp11(x) = max(-1, min(1, x))
```

---

## 2. Master Pipeline: Three-Stage Verdict Formation

### Stage 1: Static Bias → Initial Belief (g₁)

```
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

```
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

```
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
```
implicit_bias ~ Uniform(-0.6, 0.6) × 1.2  [negative skew]
```
Simulates unconscious associations distinct from explicit attitudes. Accounts for ~15% of variance in juror decisions (Greenwald et al.).

### 3.3 Cross-Factor Interactions

**Race × Gender Compound Bias:**
```
if non-white AND female:  compound_bias = -0.15 × ethnicity_weight
if non-white AND male:    compound_bias = -0.10 × ethnicity_weight
else:                     compound_bias = 0
```
*Rationale: Compounded stereotyping of women of color in credibility assessments (Crenshaw 1989).*

**Education × Political Rigidity Interaction:**
```
edu_pol_rigidity = edu_weight × pol_weight × 0.3
```
*Rationale: Educated partisans show MORE motivated reasoning, not less (Kahan et al. 2012).*

**Age × Punitiveness (Curvilinear/U-Shaped):**
```
if age < 30:  age_punitive = (30 - age) × 0.04
if age > 60:  age_punitive = (age - 60) × 0.03
else:         age_punitive = 0
```
*Rationale: Young and old jurors show higher punitiveness; middle-aged most lenient (Steiner et al. 2001).*

**Gender × Religion Interaction (Rape Myth Acceptance):**
```
gender_religion_interaction = (gender_weight - 0.5) × (religion_weight - 0.5) × 0.6
```
*Rationale: Conservative gender views combined with religiosity increase myth acceptance (Burt & Albin 1981).*

**Religion × Political Conservatism Interaction:**
```
religion_pol_interaction = (religion_weight - 0.5) × (pol_weight - 0.5) × 0.4
```
*Rationale: Religious conservatism and political conservatism reinforce each other.*

### 3.4 Reliability-Weighted Civic Engagement
```
reliability_factor = edu_weight × 0.5 + legal_weight × 0.3 + experience_weight × 0.2
```
More educated and legally knowledgeable jurors are more likely to participate actively in deliberation.

---

## 4. Specific Trait Formulas

### RWA (Right-Wing Authoritarianism) — Range 0..10
```
RWA = clamp10(5 + bias × 3.2 × polW + age_factor + (ethnicityW - 0.5) × 0.6
    + edu_pol_rigidity + race_gender_compound × 0.5 + implicit_bias × 0.3
    + random(-1, 1) × 1.8)
```

### SDO (Social Dominance Orientation) — Range 0..10
```
SDO = clamp10(5 + (-bias) × 2.8 × polW + (incomeW - 0.5) × 1.2 + (genderW - 0.5) × 0.4
    + (eduW - 0.5) × (-0.2) + race_gender_compound × 0.3
    + random(-1, 1) × 1.8)
```

### Punitiveness — Range 0..10
```
Punitiveness = clamp10(5 + bias × 2.1 × polW + (religionW - 0.5) × 1.0
    + age_punitive_curve × 1.2 + implicit_bias × 0.4
    + random(-1, 1) × 1.8)
```

### Rape Myth Acceptance — Range 0..10
```
RapeMyth = clamp10(5 + bias × 1.6 × polW + (religionW - 0.5) × 1.2
    + (eduW - 0.5) × (-0.4) + gender_religion_interaction
    + race_gender_compound × 0.4 + random(-1, 1) × 1.8)
```

### Religiosity — Range 0..10
```
Religiosity = clamp10(5 + (religionW - 0.5) × 2.0 + (commStyleW - 0.5) × 0.5
    + implicit_bias × 0.2 + random(-1, 1) × 1.8)
```

### Conservative Religiosity — Range 0..10
```
ConservRelig = clamp10(5 + bias × 2.1 × religionW + (ethnicityW - 0.5) × 0.6
    + (religionW - 0.5) × (polW - 0.5) × 0.4 + random(-1, 1) × 1.8)
```

### Political Polarity — Range -1..1
```
PolId = clamp11(bias × polW + implicit_bias × 0.2)
```

### Job Security — Range 0..10
```
JobSecurity = clamp10(5 + (incomeW - 0.5) × 2 + (profBgW - 0.5) × 1.0
    + (legalW - 0.5) × 1.0 + implicit_bias × 0.2 + random(-1, 1) × 1.8)
```

### Volunteerism — Range 0..10
```
Volunteer = clamp10(4 + (communityW - 0.5) × 3 + (experienceW - 0.5) × 1.0
    + (commStyleW - 0.5) × 1.0 + reliability_factor × 0.8 + random(-1, 1) × 1.8)
```

### Activism — Range 0..10
```
Activism = clamp10(4 + (communityW - 0.5) × 2 + (polW - 0.5) × 1.8
    + (experienceW - 0.5) × 0.8 + reliability_factor × 0.6 + random(-1, 1) × 1.8)
```

### Online Partisanship — Range 0..10
```
OnlinePartisan = clamp10(5 + (commStyleW - 0.5) × 1.5 + (polW - 0.5) × 1.5
    + (experienceW - 0.5) × 0.5 + random(-1, 1) × 1.8)
```

---

## 5. Rigidity, Hardness, and Influence Weight

### Rigidity (λ) — How strongly a juror resists counterevidence
```
λ = logistic(η_intercept + η_RWA × RWA + η_SDO × SDO + ...)
Higher λ → less evidence penetration
```

### Hardness (h) — Resistance to conformity pressure
```
h = logistic(ζ_intercept + ζ_RWA × RWA + ζ_SDO × SDO + ...)
Higher h → less susceptible to jury consensus
```

### Influence Weight (w) — How much a juror sways peers
```
w = exp(χ_intercept + χ_edu × 12 + χ_elite × ElitePrivate + ...)
Higher w → greater persuasive power in deliberation
```

---

## 6. Research Sources

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

## 7. Notes for Future Calibration

1. **Coefficient fitness**: These coefficients are structurally grounded but numerically approximate. Future work should fit them against empirical verdict datasets.
2. **Interaction terms**: Currently pairwise; higher-order interactions (e.g., Race×Gender×Education) could be added.
3. **Dynamic weights**: Consider making weights context-dependent (e.g., crime type, case severity).
4. **Validation**: Compare simulated verdict distributions against real-world jury data for calibration.