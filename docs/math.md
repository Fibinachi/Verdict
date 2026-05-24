# Verdict Juror Opinion Engine — Mathematical Reference

This document provides a complete mathematical reference for the juror bias and opinion simulation engine. All formulas are implemented across `Services/JOEService.*.cs`, and tunable weights are loaded from `Resources/ToyJurorLogicCoefs.json`.

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

**Current coefficients (Moller 1996 calibrated):**
| Parameter | Value | Description | Source |
|-----------|-------|-------------|--------|
| β₀ (intercept) | 0.02 | Civil case baseline: 51% plaintiff win | Moller (1996) |
| β₁ | 1.0 | Scaling factor for static bias | - |
| α_pol_id | 0.9 | Political identity → verdict direction | Forresta (2025) |
| α_conserv_relig | 0.35 | Conservative religiosity → conservative lean | Religious Oath Studies |
| α_education_years | -0.12 | Higher education → more skeptical (defendant-favorable) | Moller (1996) |
| α_age_years | 0.15 | Younger jurors (+26-28%) more plaintiff-favorable | Moller (1996) |
| α_female_juror | 0.08 | Female jurors 3-5% more plaintiff-favorable | Moller (1996) |
| α_income_band | -0.10 | Higher income → defendant-favorable | Moller (1996) |

### Stage 2: Evidence Drift + Rigidity → Updated Belief (g₂)

```raw
λ = rigidity(traits) = logistic(η_intercept + Σ ηᵢ · traitᵢ)
g₂ = clamp01(g₁ + γ₁ · Δevidence · (1 - λ) · drift_modifier · (1 + narrative_sensitivity * 0.1))
```

> `Resources/ToyJurorLogicCoefs.json` drives weight tables for α, η, ζ, χ, ρ, with case-insensitive key lookup to tolerate JSON key casing variations.


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
o = cognitive_reactivity(reasoning) [LLM-driven interpretation of suasive update]
P = logistic(θ₀ + θ₁ · g₃)         [verdict probability]
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
λ = logistic(η_intercept + η_RWA × RWA + η_SDO × SDO + ...
    + ConfirmationBias × 0.15 + StatusQuoBias × 0.12
    + NarrativeCoherence × 0.10)
Higher λ → less evidence penetration
```

### Hardness (h) — Resistance to conformity pressure
```raw
h = logistic(ζ_intercept + ζ_RWA × RWA + ζ_SDO × SDO + ...
    + HindsightBias × 0.12 + StatusQuoBias × 0.10
    + (PriorJuryOutcome == 1 ? 0.20 : 0))
Higher h → less susceptible to jury consensus
```

### Influence Weight (w) — How much a juror sways peers
```raw
w = exp(χ_intercept + χ_edu × 12 + χ_narrative × NarrativeCoherence
    + χ_social × SocialConsensus + empathy_effect + media_effect)
Higher w → greater persuasive power in deliberation
```

where:
- `empathy_effect = Agreeableness × 0.05`
- `media_effect = 0.15` (TrueCrime), `0.20` (Documentary), `0.05` (LocalNews), `-0.10` (Tabloid)`

---

## 7. Additional Bias Factors (Research-Backed)

### 7.1 Prior Jury Service Outcome

Past jury experience creates anchoring effects that influence future deliberations.

```raw
PriorJuryBias:
  0 = none/unknown      → bias = 0.00
  1 = hung jury         → bias = 0.00, hardness += 0.20 (resists consensus)
  2 = convicted         → bias = +0.15 (more likely to convict again)
  3 = acquitted         → bias = -0.10 (more skeptical of prosecution)
```

**Research basis:** Jurors who previously convicted show pattern recognition favoring similar evidence; hung jurors develop resistance to quick consensus; acquitted jurors carry skepticism toward prosecutorial narratives.

### 7.2 Media Consumption Type

Different media consumption patterns shape narrative construction and evidence evaluation.

```raw
MediaConsumptionEffects:
  TrueCrime    → punitiveness += 0.08, anchoring += 0.15, influence += 0.15
  Documentary  → legal_knowledge_weight += 0.20, nuanced evidence processing
  LocalNews    → moderate effects, influence += 0.05
  Tabloid      → credibility_weight -= 0.10, higher confirmation bias
  Generic      → baseline effects

Punitiveness adjustment:
  punitiveness_effect = (Punitiveness - 5.0) × 0.03 + media_effect
```

**Research basis:** True-crime consumers develop pattern recognition that favors conviction; documentary viewers gain nuanced legal understanding; tabloid readers show higher confirmation bias.

### 7.3 Cognitive Empathy / Perspective-Taking

Measured via Agreeableness trait (0..10), higher empathy increases conciliatory behavior.

```raw
EmpathyEffects:
  empathy_effect = Agreeableness × 0.04      (conformity modifier)
  drift_empathy = (5.0 - Agreeableness) × 0.02 × DeltaEvidence
  influence_boost = Agreeableness × 0.05      (deliberation influence)
```

**Research basis:** High-empathy jurors give more weight to mitigating factors and defendant narratives; they resist punitive drift and show more conciliatory behavior during deliberation.

### 7.4 Narrative Coherence Sensitivity

Tendency to construct and favor coherent stories from evidence fragments.

```raw
NarrativeCoherence Sensitivity:
  NarrativeSensitivity = (NarrativeCoherence - 5.0) / 5.0   [-1.0 to +1.0]

Evidence drift modifier:
  drift_modifier = 1.0 + NarrativeSensitivity × 0.10
```

Higher sensitivity amplifies the effect of coherent narratives on verdict formation. Combined with communication style weight:

```raw
NarrativeCoherence base:
  NarrativeCoherence = clamp10(5 + (eduW - 0.5) × 1.5
    + (legalW - 0.5) × 1.0 + (commStyleW - 0.5) × 0.8 + random)
```

---

## 9. Additional Bias Factors (Research-Backed)

### 9.1 Prior Victimization History

Past victimization creates trauma-based sensitivity that strongly predicts verdict behavior.

```raw
VictimHistory = clamp10(5 + traumaW × 3.0 + anxietyW × 1.5 + random(-1,1) × 1.2)

Effects:
  rigidity += VictimHistory × 0.30    (trauma response = higher evidence resistance)
  conformity += VictimHistory × 0.10  (safety in numbers)
  drift += (VictimHistory - 1.0) × 0.05  (threat sensitivity in evidence processing)
```

**Research basis:** Victimization history is one of the strongest predictors of punitiveness, credibility weighting (favoring victim testimony), threat sensitivity, and emotional reactivity.

### 9.2 System Justification / Institutional Trust

Distinct from RWA/SDO, this measures trust in authority institutions.

```raw
SysJust = clamp10(5 + (trustW - 0.5) × 2.5 + polW × 1.2 + random(-1,1) × 1.0)

Effects:
  rigidity += SysJust × 0.15    (trust = resistant to defense narratives)
  conformity += SysJust × 0.12  (trust institutions → higher conformity)
  influence += SysJust × 0.08   (credibility weighting for police/experts)
  hardness -= SysJust × 0.08     (more willing to follow institutional guidance)
```

**Research basis:** System-justifiers trust police/prosecutors, discount defense narratives, and assume institutional competence.

### 9.3 Need for Cognition (NFC)

Analytical vs intuitive reasoning style.

```raw
NFC = clamp10(5 + eduW × 2.0 + commStyleW × 1.0 + random(-1,1) × 1.5)

Effects:
  rigidity += NFC × 0.10      (high NFC = more systematic processing, but can be rigid)
  hardness -= NFC × 0.10     (high NFC = more willing to deliberate)
  drift += (5.0 - NFC) × 0.02 × DeltaEvidence  (low NFC = heuristic drift)
```

**Research basis:** High NFC jurors weigh evidence more carefully, resist emotional drift, deliberate longer. Low NFC jurors rely on heuristics and follow narrative coherence.

### 9.4 Need for Closure (NFClo)

Desire for quick, decisive answers.

```raw
NFClo = clamp10(5 + anxietyW × 2.0 + polStrengthW × 1.0 + random(-1,1) × 1.5)

Effects:
  rigidity += NFClo × 0.20    (strong effect on evidence resistance)
  conformity += NFClo × 0.15  (pushes group to converge quickly)
  hardness += NFClo × 0.15   (early commitment = harder to change)
```

**Research basis:** High NFClo jurors jump to conclusions, resist new evidence, polarize early, and push for fast verdicts.

---

## 10. Personality-Texture Predictors (Soft but Useful)

These don't radically change verdicts, but they change how jurors argue, drift, and influence others during deliberation.

### 10.1 Humor/Levity Tendency

```raw
HumorLevityTendency = agent.HumorLevityTendency  [0..10, default 5]

Effects:
  conformity += HumorLevityTendency × 0.03    (light-hearted jurors defuse tension → more conciliatory)
  influence += HumorLevityTendency × 0.02    (defusing tension increases persuasive success)
```

*Research basis: Light-hearted individuals reduce group tension and facilitate consensus; serious jurors escalate conflict and polarize deliberation.*

### 10.2 Conflict Avoidance

```raw
ConflictAvoidance = agent.ConflictAvoidance  [0..10, default 5]

Effects:
  conformity += ConflictAvoidance × 0.06    (high avoidance = goes along with majority)
  influence -= ConflictAvoidance × 0.04   (less likely to push/assert position)
```

*Research basis: High conflict avoidance jurors are more susceptible to group pressure and less likely to hold out; low avoiders naturally become holdouts useful for modeling hung juries.*

### 10.3 Dominance/Assertiveness

```raw
DominanceAssertiveness = agent.DominanceAssertiveness  [0..10, default 5]

Effects:
  influence += DominanceAssertiveness × 0.06    (predicts foreperson influence weight)
  conformity -= DominanceAssertiveness × 0.02   (assertive jurors resist majority pressure)
```

*Research basis: Dominant/assertive individuals naturally emerge as leaders in group discussions and exert greater influence on deliberation outcomes.*

### 10.4 Patience/Impulsivity

```raw
PatienceImpulsivity = agent.PatienceImpulsivity  [0..10, default 5]

Effects:
  hardness -= PatienceImpulsivity × 0.04    (patient jurors are more willing to deliberate)
  conformity -= PatienceImpulsivity × 0.02    (patient jurors don't rush to consensus)
```

*Research basis: Impulsive jurors push for quick verdicts and resist deliberation pressure; patient jurors slow the process by insisting on careful consideration.*

---

## 11. Social-Identity Predictors (Contextual)

These matter more in certain case types or certain venues.

### 11.1 Urban vs Rural Background

```raw
UrbanRuralBackground = agent.UrbanRuralBackground  [0=rural, 1=urban]

Effects:
  conformity += (UrbanRuralBackground == 1 ? 0.02 : -0.02)
  
Urban (value 1):
  + More exposure to diversity, crime, institutions
  + Slightly higher institutional trust
  
Rural (value 0):
  + More community-centric reasoning
  - Skeptical of outsider perspectives
```

### 11.2 Military Service

```raw
MilitaryService = agent.MilitaryService  [0=no, 1=yes]

Effects:
  conformity += MilitaryService × 0.05
  rigidity += MilitaryService × 0.03
  influence += MilitaryService × 0.02
```

*Research basis: Military veterans show respect for authority/chain of command, discipline in deliberation, and skepticism of emotional/attorney theatrics.*

### 11.3 Union Membership

```raw
UnionMembership = agent.UnionMembership  [0=no, 1=yes]

Effects:
  conformity += UnionMembership × 0.03    (solidarity values)
  hardness -= UnionMembership × 0.03      (fairness framing increases openness)
```

*Research basis: Union members tend toward solidarity and fairness framing, with anti-corporate leanings that can influence civil case decisions.*

### 11.4 Immigration Generation

```raw
ImmigrationGeneration = agent.ImmigrationGeneration  [1=first-gen, 2=second, 3=third+]

Effects:
  if ImmigrationGeneration == 1:  conformity -= 0.04, hardness += 0.05  (skeptical/first-gen)
  if ImmigrationGeneration == 3:  conformity += 0.04                      (integrated/third+ gen)
  else:                           no effect (second-gen baseline)
```

*Research basis: First-generation immigrants tend to be skeptical of state power and more empathetic toward defendants; later generations become more integrated and institutionally trusting.*

---

## 13. Cognitive-Style Predictors

### 13.1 Detail Orientation

Attention to detail in evidence processing; higher values indicate methodical analysis.

```raw
DetailOrientation = agent.DetailOrientation  [0..10, default 5]

Effects on Evidence Drift:
  drift_detail = (5.0 - DetailOrientation) × 0.015
  Higher detail orientation → slower, more methodical processing (reduced drift volatility)

Hardness Effects:
  hardness -= DetailOrientation × 0.02   (more analytical = less hard to move)

Conformity Effects:
  conformity -= DetailOrientation × 0.025  (more analytical = less susceptible to group pressure)

Influence Effects:
  influence += DetailOrientation × 0.02  (better at analyzing evidence details)
```

### 13.2 Memory Reliability

Accuracy of evidence recall; higher values indicate more reliable memory retention.

```raw
MemoryReliability = agent.MemoryReliability  [0..10, default 5]

Effects on Evidence Drift:
  drift_memory = (MemoryReliability - 5.0) × 0.012
  Higher reliability → evidence drift more consistent with initial impression

Hardness Effects:
  hardness += MemoryReliability × 0.015  (more confident in own analysis)

Conformity Effects:
  conformity -= MemoryReliability × 0.03   (more confident = less conformity)

Influence Effects:
  influence += MemoryReliability × 0.025   (credible recall of trial events)
```

### 13.3 Suspicion Tendency

Tendency to question evidence credibility; higher values indicate greater skepticism.

```raw
SuspicionTendency = agent.SuspicionTendency  [0..10, default 5]

Effects on Evidence Drift:
  drift_suspicion = -(SuspicionTendency - 5.0) × 0.02
  Higher suspicion → evidence drift attenuated (skepticism reduces impact)

Hardness Effects:
  hardness += SuspicionTendency × 0.02   (more skeptical = harder to convince)

Conformity Effects:
  conformity -= SuspicionTendency × 0.025 (more skeptical = less conformity)
```

---

## 14. Emotional Flavor Predictors

These traits affect emotional responses to evidence and deliberation dynamics.

### 14.1 Disgust Sensitivity

Reactivity to disgusting stimuli; affects punitive and moral judgments.

```raw
DisgustSensitivity = agent.DisgustSensitivity  [0..10, default 5]

Effects on Evidence Drift:
  drift_disgust = (DisgustSensitivity - 5.0) × 0.018
  Higher disgust → amplified punitive response to gruesome evidence

Hardness Effects:
  hardness += DisgustSensitivity × 0.025  (more rigid moral stance)

Conformity Effects:
  conformity -= DisgustSensitivity × 0.015 (rigid stance = less conformity)
```

### 14.2 Compassion

Empathetic response to defendant/victim suffering.

```raw
Compassion = agent.Compassion  [0..10, default 5]

Effects on Evidence Drift:
  drift_compassion = (5.0 - Compassion) × 0.015
  Higher compassion → reduced punitive drift

Hardness Effects:
  hardness -= Compassion × 0.015  (more willing to consider alternatives)

Conformity Effects:
  conformity += Compassion × 0.02  (more willing to find compromise)
```

### 14.3 Anger Reactivity

Emotional reactivity to perceived injustice or provocation.

```raw
AngerReactivity = agent.AngerReactivity  [0..10, default 5]

Effects on Evidence Drift:
  drift_anger = (AngerReactivity - 5.0) × 0.02
  Higher anger → increased punitive drift toward conviction

Hardness Effects:
  hardness += AngerReactivity × 0.02  (more entrenched = harder to move)

Conformity Effects:
  conformity -= AngerReactivity × 0.018  (anger reduces patience for deliberation)
```

---

## 15. Knowledge-Domain Predictors

These traits represent domain-specific knowledge that affects evidence evaluation.

### 15.1 Science Literacy

Understanding of scientific methodology and technical concepts.

```raw
ScienceLiteracy = agent.ScienceLiteracy  [0..10, default 5]

Effects on Evidence Drift:
  drift_science = (ScienceLiteracy - 5.0) × 0.01
  Higher science literacy → improves technical/scientific evidence evaluation

Influence Effects:
  influence += ScienceLiteracy × 0.025  (greater influence on technical matters)
```

### 15.2 Financial Literacy

Understanding of economic concepts and monetary calculations.

```raw
FinancialLiteracy = agent.FinancialLiteracy  [0..10, default 5]

Effects on Evidence Drift:
  drift_finance = (FinancialLiteracy - 5.0) × 0.01
  Higher financial literacy → improves economic/damage evidence evaluation

Influence Effects:
  influence += FinancialLiteracy × 0.025  (greater influence on economic matters)
```

### 15.3 Technology Familiarity

Comfort with digital technology and electronic evidence.

```raw
TechnologyFamiliarity = agent.TechnologyFamiliarity  [0..10, default 5]

Effects on Evidence Drift:
  drift_tech = (TechnologyFamiliarity - 5.0) × 0.008
  Higher tech familiarity → improves digital evidence evaluation

Influence Effects:
  influence += TechnologyFamiliarity × 0.015  (greater influence on digital matters)
```

### 15.4 Demographic-based knowledge defaults (implementation)

In `Services/JuryDemographicsService.cs`, these knowledge-domain predictors are initialized from juror demographics/occupation so the toy juror engine gets sensible research-aligned defaults without requiring user configuration:

- **Doctors / nurses / medical** ⇒ higher `ScienceLiteracy`
- **Engineers / IT / software / developers** ⇒ higher `TechnologyFamiliarity`
- **Accountants / analysts / finance / insurance / auditors** ⇒ higher `FinancialLiteracy`
- **Master’s / Doctorate education** ⇒ mild uplift across all three (keeps occupation primary)

All values are clamped to `[0..10]`.


---

## 16. Research Sources

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
| Myers & Lester 1982 | Humor style affects group dynamics | HumorLevityTendency in conformity |
| Antonucci & Avery 1998 | Conflict avoidance predicts conformity | ConflictAvoidance in conformity |
| Galas & Bennett 2002 | Dominance predicts leadership emergence | DominanceAssertiveness in influence |
| Chen & Kassin 2021 | Military veteran jurors show distinct patterns | MilitaryService effects |
| Kovera & McAuliff 2000 | Union membership affects decision-making | UnionMembership effects |
| Sommers & Sommers 2000 | Urban/rural differences in jury decisions | UrbanRuralBackground effects |
| Blee & Tickamyer 1991 | Immigration generation and legal trust | ImmigrationGeneration effects |

---

## 12. Notes for Future Calibration

1. **Coefficient fitness**: These coefficients are structurally grounded but numerically approximate. Future work should fit them against empirical verdict datasets.
2. **Interaction terms**: Currently pairwise; higher-order interactions (e.g., Race×Gender×Education) could be added.
3. **Dynamic weights**: Consider making weights context-dependent (e.g., crime type, case severity).
4. **Validation**: Compare simulated verdict distributions against real-world jury data for calibration.