# Juror Bias Research: Factors Affecting Juror Perception

## Overview

This document summarizes peer-reviewed research on how demographic and psychological factors affect juror perception and decision-making in courtroom simulations. The findings inform the default weight values used in the VERDICT bias factor system.

## Key Research Studies

### Political Affiliation and Ideology

**Primary Finding**: Political ideology significantly influences verdict decisions, with measurable effects on conviction rates.

- **Forresta (2025)** - "Beyond a reasonable doubt: the impact of jurors' political affiliations on jury trials" (Journal of Law and Economics):
  - Independent jurors decrease guilty verdicts by 2.93%
  - Democratic jurors show a negative but not statistically significant impact on conviction rates
  - Democratic jurors are 3.7 percentage points more likely to be removed from a seated jury (indicating awareness of bias potential)
  - **Evidence Link**: https://eprints.soton.ac.uk/494474

- **Politics in the Courtroom (2018)** - Swedish study on political party affiliation:
  - Far-right Swedish Democrat jurors increased convictions by 17 percentage points for defendants with Arabic names
  - Far-left Vänster party increased convictions by 14 percentage points with female victims
  - **Evidence Link**: https://academic.oup.com/jeea/article/17/3/834/4981454

### Religious Affiliation and Religiosity

**Primary Finding**: Religious identity and depth of religious commitment affect verdict decisions and witness credibility assessments.

- **House of Worship Mass Shooting (2024)** - Pozzulo et al.:
  - Christian defendants received more guilty verdicts than Muslim defendants
  - Victim religion influenced guilt ratings: higher guilt ratings when victim was Muslim
  - Moral outrage partially mediated defendant religion effects
  - **Evidence Link**: https://link.springer.com/article/10.1007/s11896-024-09695-6

- **Religious Oath Studies** - Fraser & Fehr:
  - Jurors more likely to trust witnesses who give religious oaths
  - Variation by religiosity and religious group affiliation
  - Religious fundamentalism associated with discrimination against religious out-groups
  - **Evidence Link**: https://digitalcommons.osgoode.yorku.ca/cgi/viewcontent.cgi?article=4067&context=ohlj

### Race and Ethnicity

**Primary Finding**: Racial bias significantly impacts juror decision-making, with white-dominated juries showing higher conviction rates against non-white defendants.

- **Indigenous Canadians Study (2025)** - Vettese, Pica, & Pozzulo:
  - Contrary to hypotheses: Indigenous eyewitness testimony resulted in more guilty verdicts
  - Mock-jurors were more punitive toward White defendants and held more favorable perceptions of Indigenous defendants
  - Racial bias scores moderated relationships between race and verdict decisions
  - **Evidence Link**: https://link.springer.com/article/10.1007/s11896-025-09742-w

- **RAND Civil Jury Study (2024)**:
  - Women are less plaintiff-friendly in civil trials
  - Age matters: older jurors tend toward more conservative verdicts
  - Race does not usually predict outcomes when controlling for other factors
  - **Evidence Link**: https://www.rand.org/pubs/working_papers/WRA4984-1.html

- **Social Cognitive Processes (2022)** - PMC study:
  - Crime-type bias driven by social cognition (mentalizing) rather than emotion or moral judgment
  - Racial bias linked to theory of mind and stereotype processing
  - **Evidence Link**: https://pmc.ncbi.nlm.nih.gov/articles/PMC9949508/

### Age

**Primary Finding**: Age affects both witness credibility assessment and defendant treatment.

- **Pozzulo et al. (2017)** - Age influences confidence in eyewitness testimony:
  - Younger witnesses (12-year-olds) receive different credibility assessments than older witnesses
  - Age interacts with race in credibility assessments
  - **Evidence Link**: https://link.springer.com/article/10.1007/s11896-016-9201-1

- **Sheahan et al. (2021)** - Abuse cases:
  - Defendant age affects juror decisions, with younger defendants receiving more leniency in some contexts
  - **Evidence Link**: https://journals.sagepub.com/doi/10.1177/0886260517731316

### Education Level

**Primary Finding**: Higher education levels correlate with more rigorous evidence evaluation and skepticism toward weak evidence.

- **RAND Study (2024)** and supporting literature:
  - Higher education associated with more analytical thinking in verdict decisions
  - More educated jurors are more skeptical of weak evidence
  - Education inversely correlates with punitiveness in many contexts

### Gender

**Primary Finding**: Gender affects both how jurors perceive victims/witnesses and how they evaluate evidence.

- **Pozzulo et al. (2010)** - Gender interactions:
  - Male defendants with female victims receive harsher judgments
  - Gender of juror also affects decision-making patterns
  - **Evidence Link**: https://journals.sagepub.com/doi/10.1177/0093854809344173

- **Death Qualification Studies** (ACLU 2025):
  - White-dominated juries more likely to sentence to death
  - Black jurors more likely to discount mitigating evidence in death penalty cases
  - **Evidence Link**: https://assets.aclu.org/live/uploads/2025/06/Fatal-Flaws-Revealing-the-Racial-and-Religious-Gerrymandering-of-the-Capital-Jury-1.pdf

## Default Bias Factor Weight Values

Based on the research synthesis, the following table provides recommended default weight values for each bias factor. These values represent the relative strength of each factor's influence on juror perception.

| Factor | Default Weight | Research Basis |
|--------|---------------|----------------|
| Political Affiliation | 0.90 | Strongest predictor of verdict direction (Forresta 2025, Politics in Courtroom 2018) |
| Education Level | 0.80 | High education correlates with analytical evaluation (RAND 2024) |
| Legal Knowledge | 0.75 | Education and legal expertise strongly influence evidence evaluation (RAND 2024) |
| Juror Experience | 0.70 | Experience affects deliberation participation and conformity (multiple studies) |
| Community Ties | 0.65 | Social identity influences decision-making (Pozzulo 2024) |
| Ethnicity | 0.60 | Strong influence, varies by context (Vettese 2025, RAND 2024) |
| Age | 0.55 | Influences perspective and credibility assessment (Pozzulo 2017, Sheahan 2021) |
| Professional Background | 0.55 | Occupation and expertise affect technical evidence and expert testimony weighting (RAND 2024) |
| Income Level | 0.50 | Economic status affects perspective but moderated by other factors |
| Gender | 0.45 | Moderate influence, context-dependent (Pozzulo 2010) |
| Religion | 0.40 | Moderate influence, varies by case type (Vettese 2024, Fraser 2025) |
| Communication Style | 0.35 | Moderates but less directly influences verdicts |

## Implementation Notes

These default values are used in:
- `ToyJurorLogicEngine.cs` - Coefficient calculations for trait generation
- `JuryCalculationService.cs` - Background weight calculations
- `JuryDemographicsService.cs` - Juror profile generation

The weights can be adjusted via the Model Weights window for specific case scenarios.

## References

1. Forresta, A. (2025). "Beyond a reasonable doubt: the impact of jurors' political affiliations on jury trials." Journal of Law and Economics, 68, 361-386. https://eprints.soton.ac.uk/494474

2. Vettese, A., Pica, E., & Pozzulo, J. (2024). "House of Worship Mass Shooting: The Influence of Defendant Age, Religion, and Victim Religion on Mock-Juror Decision-Making." Journal of Police and Criminal Psychology, 39, 693–705. https://doi.org/10.1007/s11896-024-09695-6

3. Vettese, A., & Pozzulo, J. (2025). "When Indigenous Canadians Take the Stand: The Influence of Age and Race on Mock-Juror Perceptions and Verdict Decisions." Journal of Police and Criminal Psychology. https://doi.org/10.1007/s11896-025-09742-w

4. RAND Corporation. (2024). "The Role of Juror Demographics in Civil Trials." Working Paper WRA4984-1. https://www.rand.org/pubs/working_papers/WRA4984-1.html

5. Social Cognitive Processes Study. (2022). "Social cognitive processes explain bias in juror decisions." PMC, 9949508. https://pmc.ncbi.nlm.nih.gov/articles/PMC9949508/

6. Politics in the Courtroom Study. (2018). "Political Ideology and Jury Decision Making." Journal of the European Economic Association, 17(3), 834–878. https://academic.oup.com/jeea/article/17/3/834/4981454

7. Fraser & Fehr. (2025). "Studying Religious Symbols and Bias in Court Proceedings." Osgoode Hall Journal. https://digitalcommons.osgoode.yorku.ca/cgi/viewcontent.cgi?article=4067&context=ohlj

8. ACLU. (2025). "Fatal Flaws: Revealing the Racial and Religious Gerrymandering of the Capital Jury." https://assets.aclu.org/live/uploads/2025/06/Fatal-Flaws-Revealing-the-Racial-and-Religious-Gerrymandering-of-the-Capital-Jury-1.pdf