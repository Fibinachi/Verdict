# Bias Factor Weight Mapping

**Version**: v0.75 | Maps 12 user-facing weights to pipeline coefficients.

## The 12 Weights

| # | Name | Default |
|---|------|---------|
| 1 | Political Affiliation | 0.90 |
| 2 | Education Level | 0.80 |
| 3 | Legal Knowledge | 0.75 |
| 4 | Juror Experience | 0.70 |
| 5 | Community Ties | 0.65 |
| 6 | Ethnicity | 0.60 |
| 7 | Age | 0.55 |
| 8 | Professional Background | 0.55 |
| 9 | Income Level | 0.50 |
| 10 | Gender | 0.45 |
| 11 | Religion | 0.40 |
| 12 | Communication Style | 0.35 |

## Weight → Trait Generation

### Political Affiliation (0.90)
RWA: bias×3.2×PolW | SDO: −bias×2.8×PolW | Punitiveness: bias×2.1×PolW | RapeMyth: bias×1.6×PolW | ConservRelig: bias×2.1×PolW×religionW | PolId: bias×PolW | OnlinePartisan: (PolW−0.5)×1.5 | Activism: (PolW−0.5)×1.8 | ConfirmationBias: (PolW−0.5)×2.0

### Education Level (0.80)
SDO: (EduW−0.5)×−0.2 | RapeMyth: (EduW−0.5)×−0.4 | Anchoring resistance: (EduW−0.5)×−1.0 | Hindsight resistance: (EduW−0.5)×−0.8 | Narrative: (EduW−0.5)×1.5 | Reliability: ×0.5 | Motivated reasoning (Stage 2): eduW×polW×0.20

### Legal Knowledge (0.75)
JobSecurity: (legalW−0.5)×1.0 | Narrative: (legalW−0.5)×1.0 | Reliability: ×0.3

### Juror Experience (0.70)
Volunteer: (experienceW−0.5)×1.0 | Activism: (experienceW−0.5)×0.8 | Reliability: ×0.2

### Community Ties (0.65)
Volunteer: (communityW−0.5)×3 | Donate: (communityW−0.5)×2 | Activism: (communityW−0.5)×2 | SocialConsensus: (communityW−0.5)×2.0

### Ethnicity (0.60)
RWA: (ethnicityW−0.5)×0.6 | ConservRelig: (ethnicityW−0.5)×0.6 | Static bias α=0.35 conditional: PolId>0?−0.5:+0.5 | Compound: Non-white female −0.15×ethnicityW; male −0.10×ethnicityW

### Age (0.55)
RWA age factor: (ageW−0.5)×0.8 | Punitiveness curve: age<30 (30−age)×0.04; age>60 (age−60)×0.03 | Influence χ: 0.02×AgeYears

### Professional Background (0.55)
JobSecurity: (profBgW−0.5)×1.0

### Income Level (0.50)
SDO: (incomeW−0.5)×1.2 | JobSecurity: (incomeW−0.5)×2

### Gender (0.45)
SDO: (genderW−0.5)×0.4 | Static bias α=0.25 conditional: PolId>0?1.0:−0.5 | Gender×religion: (genderW−0.5)×(religionW−0.5)×0.6

### Religion (0.40)
Punitiveness: (ReligionW−0.5)×1.0 | RapeMyth: (ReligionW−0.5)×1.2 | Religiosity: (ReligionW−0.5)×2.0 | ConservRelig: bias×2.1×ReligionW | Religion×politics: (ReligionW−0.5)×(PolW−0.5)×0.4 | ConfirmationBias: (ReligionW−0.5)×1.5

### Communication Style (0.35)
Religiosity: (CommStyleW−0.5)×0.5 | Volunteer: ×1.0 | Donate: ×0.8 | OnlinePartisan: ×1.5 | Anchoring: ×1.5 | HindsightBias: ×2.0 | Narrative: ×0.8 | OutcomeBias: ×1.2 | SocialConsensus: ×1.0

## Interaction Effects

Gender×Religion: (genderW−0.5)×(religionW−0.5)×0.6 → RapeMyth
Religion×Politics: (religionW−0.5)×(polW−0.5)×0.4 → ConservRelig
Edu×Politics: eduW×polW×0.20 (Stage 2) → Motivated reasoning
Race×Gender: Compound penalty → Credibility

## Reliability Factor

reliability = eduW×0.5 + legalW×0.3 + experienceW×0.2
Boosts: Volunteer×0.8, Donate×0.5, Activism×0.6

## Research Defaults

Political 0.90: Forresta 2025 | Education 0.80: RAND 2024 | Legal 0.75: multiple | Experience 0.70: prior jury service | Community 0.65: social capital | Ethnicity 0.60: meta-analyses | Age 0.55: RAND 2024 | Professional 0.55: occupation effects | Income 0.50: RAND 2024 | Gender 0.45: RAND 2024 | Religion 0.40: Pozzulo 2024 | Communication 0.35: engagement effects

Full citations: docs/JurorBiasResearch.md
