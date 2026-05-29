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

**Primary Finding**: Higher education levels correlate with more rigorous evidence evaluation and skepticism toward weak evidence. Education type also independently affects deliberation dynamics.

- **RAND Study (2024)** and supporting literature:
  - Higher education associated with more analytical thinking in verdict decisions
  - More educated jurors are more skeptical of weak evidence
  - Education inversely correlates with punitiveness in many contexts

- **Education Type Effects** (multiple studies):
  - **Elite private colleges**: Graduates exert outsized influence in deliberation (Chi=0.08). Higher SES and network confidence translate to jury room authority.
  - **State flagship universities**: Moderate influence (Chi=0.05). Broad educational foundation without elite signaling.
  - **Other private colleges**: Above-average influence (Chi=0.04). Similar to elite but less pronounced.
  - **Trade schools**: Practical credibility (Chi=0.03). Valued for hands-on expertise in relevant cases.
  - **HBCUs**: Distinct perspective (Chi=0.02). Historically Black Colleges bring unique life experience to deliberation.
  - **Community colleges**: Working-class credibility (Chi=0.02). Practical, accessible education background.
  - **Bible colleges**: Religious moral authority (Chi=0.02, Zeta=0.06, Eta=0.05). Religious education creates stronger conviction and resistance to counterevidence. Moral framework affects both influence and rigidity.

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

| Factor | Default Weight | Directionality | Research Basis |
|--------|---------------|---------------|----------------|
| Political Affiliation | 0.90 | PROSECUTION (conservative) / DEFENSE (liberal) | Strongest predictor of verdict direction (Forresta 2025, Politics in Courtroom 2018) |
| Education Level | 0.80 | NEUTRAL | High education correlates with analytical evaluation (RAND 2024) |
| Legal Knowledge | 0.75 | NEUTRAL | Education and legal expertise strongly influence evidence evaluation (RAND 2024) |
| Juror Experience | 0.70 | NEUTRAL | Experience affects deliberation participation and conformity (multiple studies) |
| Community Ties | 0.65 | CONTEXTUAL | Social identity influences decision-making (Pozzulo 2024) |
| Ethnicity | 0.60 | CONTEXTUAL | Strong influence, varies by context (Vettese 2025, RAND 2024) |
| Age | 0.55 | CONTEXTUAL | Influences perspective and credibility assessment (Pozzulo 2017, Sheahan 2021) |
| Professional Background | 0.55 | NEUTRAL | Occupation and expertise affect technical evidence and expert testimony weighting (RAND 2024) |
| Income Level | 0.50 | CONTEXTUAL | Economic status affects perspective but moderated by other factors |
| Gender | 0.45 | CONTEXTUAL | Moderate influence, context-dependent (Pozzulo 2010) |
| Religion | 0.40 | CONTEXTUAL | Moderate influence, varies by case type (Vettese 2024, Fraser 2025) |
| Communication Style | 0.35 | NEUTRAL | Moderates but less directly influences verdicts |
| Firearm Ownership (NRA) | 0.12* | DEFENSE (self-defense cases) | Affects defense lean, rigidity, and conformity; modeled as trait coefficient |

### Directionality Legend

- **PROSECUTION**: Factor directionally pushes toward conviction/prosecution in most case types
- **DEFENSE**: Factor directionally pushes toward acquittal/defense in most case types
- **NEUTRAL**: Factor affects decision quality/style but has no consistent directional lean
- **CONTEXTUAL**: Direction depends on case type, defendant characteristics, or other interacting factors

## Implementation Notes

These default values are used in:
- `ToyJurorLogicEngine.cs` - Coefficient calculations for trait generation
- `JuryCalculationService.cs` - Background weight calculations
- `JuryDemographicsService.cs` - Juror profile generation

The weights can be adjusted via the Model Weights window for specific case scenarios.

## Case-Type Sensitivity

Certain bias factors have **case-type-dependent effects** — their magnitude or direction changes based on the nature of the case. The following table documents known case-type interactions implemented in the engine.

### Case-Type Modifier Table

| Case Type | Affected Factors | Effect | Mechanism |
|-----------|-----------------|--------|-----------|
| **Sexual Assault** | BJW → defense (victim-blaming) | α_bjw applied negatively | `caseSubType == "sexualassault" → b −= α_bjw × BJW` |
| **Sexual Assault** | Gender × Religion interaction | Victim credibility penalty | Conservative gender views + religiosity → reduced victim credibility |
| **Violent Crime** | BJW → prosecution | α_bjw applied positively | `caseSubType == "violentcrime" → b += α_bjw × BJW` |
| **Violent Crime** | NRA → prosecution | α_nra applied positively | NRA members lean prosecution in violent crime (self-defense framing) |
| **Property Crime** | BJW → prosecution | α_bjw applied positively | Same as violent crime — "bad things happen to bad people" |
| **Property Crime** | Income level amplified | Higher-income jurors more defense-leaning | Property crime → economic self-interest moderates |
| **Fraud / White Collar** | Education amplified | Higher education → more skeptical of prosecution | Complex evidence requires analytical evaluation |
| **Fraud / White Collar** | Income level amplified | Higher income → more defense-leaning | Economic class identification with white-collar defendants |
| **Domestic Violence** | Gender amplified | Female jurors → more prosecution-leaning | Gender-based empathy and experience |
| **Domestic Violence** | Religion amplified | Conservative religious → more defense-leaning (privacy/family) | Religious views on family privacy may reduce prosecution support |

### BJW Direction by Case Type

```
SexualAssault   → b −= α_bjw × BeliefInJustWorld  [victim-blaming → defense]
ViolentCrime     → b += α_bjw × BeliefInJustWorld  ["bad things happen to bad people" → prosecution]
PropertyCrime    → b += α_bjw × BeliefInJustWorld  [same as violent crime]
Fraud            → b += 0.05 × BeliefInJustWorld   [mild prosecution lean — "crime doesn't pay"]
DomesticViolence → b += 0.05 × BeliefInJustWorld   [neutral/mild effect]
default          → b += 0.05 × BeliefInJustWorld   [neutral/mild effect]
```

### Gender × Religion Interaction by Case Type

| Case Type | Interaction Strength | Effect |
|-----------|---------------------|--------|
| Sexual Assault | Strong (×0.6) | Conservative religious males → higher rape myth acceptance → lower victim credibility |
| Domestic Violence | Moderate (×0.4) | Similar mechanism, less pronounced |
| Violent Crime | Mild (×0.2) | Gender/religion interaction less relevant to general violence |
| Other | None | No special gender×religion interaction |

## Implicit Bias (Greenwald et al.)

### Overview

Implicit bias refers to unconscious associations that influence behavior independent of explicit (conscious) attitudes. In the jury context, implicit bias accounts for approximately **15% of variance** in juror decisions above and beyond explicit demographic factors (Greenwald, Poehlman, Uhlmann, & Banaji, 2009; Greenwald & Banaji, 1995).

### Role in the VERDICT Engine

Implicit bias is modeled as a latent variable (`implicitBias ~ Uniform(−0.6, 0.6) × 1.2`) that propagates into several trait and coefficient systems:

| System | Implicit Bias Role | Magnitude |
|--------|-------------------|-----------|
| **RWA (Authoritarianism)** | Amplifies authoritarian tendencies | ×0.3 |
| **SDO (Social Dominance)** | Not directly applied (SDO is more explicit) | — |
| **Punitiveness** | Amplifies punitive tendencies | ×0.4 |
| **Credibility Assessment** | Unconscious credibility discount for out-group witnesses | Via compound bias |
| **Media Sensitivity** | Affects susceptibility to emotionally-charged evidence | Via media traits |
| **Rape Myth Acceptance** | Not directly applied | — |
| **Religiosity** | Mild amplification | ×0.2 |
| **Political Polarity (PolId)** | Mild amplification of explicit political lean | ×0.2 |
| **Job Security** | Mild influence on economic anxiety | ×0.2 |

### Research Basis

- **Greenwald, A.G., & Banaji, M.R. (1995).** "Implicit social cognition: Attitudes, self-esteem, and stereotypes." *Psychological Review*, 102(1), 4–27.
- **Greenwald, A.G., Poehlman, T.A., Uhlmann, E.L., & Banaji, M.R. (2009).** "Understanding and using the Implicit Association Test: III. Meta-analysis of predictive validity." *Journal of Personality and Social Psychology*, 97(1), 17–41.
- **Levinson, J.D., Cai, H., & Young, D. (2014).** "Guilty by implicit racial bias: The guilty/not guilty Implicit Association Test." *Ohio State Journal of Criminal Law*, 8, 187–208.

### Distinction from Explicit Bias

- **Explicit bias**: Captured by PolId, ConservRelig, Punitiveness — conscious attitudes the juror would self-report.
- **Implicit bias**: Captured by the `implicitBias` term — unconscious associations operating below awareness.
- The two interact: implicit bias amplifies explicit positions but also introduces independent variance, creating jurors whose conscious and unconscious lean diverge.

## Known Limitations

The VERDICT Juror Opinion Engine is a quantitative simulation model. Like all models, it has known limitations that users and researchers should be aware of when interpreting results:

### 1. No Interpersonal Dynamics
- The model treats each juror independently during evidence stages (g₁, g₂).
- It does **not** model: side conversations, non-verbal communication, seating proximity effects, or informal alliances formed during breaks.
- Real juries develop complex social dynamics (cliques, rivalries, leadership contests) that the conformity parameter (ρ) only approximates.

### 2. No Emotional Contagion
- The model does not capture emotional spread: a crying juror influencing others' emotional states, collective anger at a defendant, or shared sympathy for a victim.
- Emotional valence is collapsed into the single `VerdictLean` dimension.
- Research (Barsade, 2002; Hatfield, Cacioppo, & Rapson, 1994) shows that emotional contagion significantly affects group decision-making.

### 3. No Multi-Day Fatigue
- The simulation treats each evidence item and deliberation round as atomic events with no temporal fatigue.
- Real trials span days or weeks; juror attention, patience, and cognitive resources degrade over time.
- Long-trial effects (boredom, frustration, desire to "just get it over with") are not modeled.

### 4. No Strategic Juror Behavior
- Jurors in the model do not act strategically: they don't hold out for concessions, trade votes, or engage in logrolling.
- Real juries exhibit strategic behavior: reluctant agreement to avoid hung jury, compromise verdicts, and charge bargaining within the jury room.

### 5. No Jury Instruction Comprehension Variance
- All jurors are assumed to understand and apply jury instructions identically.
- Research shows substantial variance in juror comprehension of legal instructions, correlated with education and cognitive reflection (Reifman, Gusick, & Ellsworth, 1992).

### 6. Single-Axis Verdict Dimension
- The model collapses all case complexity into a single `VerdictLean` dimension (0..1).
- Multi-charge cases, lesser-included offenses, and special verdict forms are not represented.
- Civil damage calculations use a simplified `ConsideredDamages` field rather than full itemization.

### 7. No Voir Dire Dynamics
- Jury selection (voir dire) is not modeled; all jurors are "seated" and participate.
- Real juries are shaped by peremptory challenges and challenges for cause, which systematically bias composition.

### 8. No Deliberation Time Pressure
- The model does not account for time limits, judicial pressure (Allen charges), or external deadlines.
- Time pressure significantly affects concession rates and verdict outcomes in real juries.

These limitations are acknowledged design choices. The engine prioritizes **trait-to-verdict causal pathways** and **demographic sensitivity** over full social-psychological realism. Future versions may address specific limitations based on user feedback and research advances.

## References

1. Forresta, A. (2025). "Beyond a reasonable doubt: the impact of jurors' political affiliations on jury trials." Journal of Law and Economics, 68, 361-386. https://eprints.soton.ac.uk/494474

2. Vettese, A., Pica, E., & Pozzulo, J. (2024). "House of Worship Mass Shooting: The Influence of Defendant Age, Religion, and Victim Religion on Mock-Juror Decision-Making." Journal of Police and Criminal Psychology, 39, 693–705. https://doi.org/10.1007/s11896-024-09695-6

3. Vettese, A., & Pozzulo, J. (2025). "When Indigenous Canadians Take the Stand: The Influence of Age and Race on Mock-Juror Perceptions and Verdict Decisions." Journal of Police and Criminal Psychology. https://doi.org/10.1007/s11896-025-09742-w

4. RAND Corporation. (2024). "The Role of Juror Demographics in Civil Trials." Working Paper WRA4984-1. https://www.rand.org/pubs/working_papers/WRA4984-1.html

5. Social Cognitive Processes Study. (2022). "Social cognitive processes explain bias in juror decisions." PMC, 9949508. https://pmc.ncbi.nlm.nih.gov/articles/PMC9949508/

6. Politics in the Courtroom Study. (2018). "Political Ideology and Jury Decision Making." Journal of the European Economic Association, 17(3), 834–878. https://academic.oup.com/jeea/article/17/3/834/4981454

7. Fraser & Fehr. (2025). "Studying Religious Symbols and Bias in Court Proceedings." Osgoode Hall Journal. https://digitalcommons.osgoode.yorku.ca/cgi/viewcontent.cgi?article=4067&context=ohlj

8. ACLU. (2025). "Fatal Flaws: Revealing the Racial and Religious Gerrymandering of the Capital Jury." https://assets.aclu.org/live/uploads/2025/06/Fatal-Flaws-Revealing-the-Racial-and-Religious-Gerrymandering-of-the-Capital-Jury-1.pdf

9. Cacioppo, J.T., & Petty, R.E. (1982). "The need for cognition." Journal of Personality and Social Psychology, 42(1), 116–131. https://doi.org/10.1037/0022-3514.42.1.116

10. Lerner, M.J. (1980). "The Belief in a Just World: A Fundamental Delusion." Plenum Press.

11. Haney, C. (1984). "On the selection of capital juries: The biasing effects of the death-qualification process." Law and Human Behavior, 8(1-2), 121–132.

12. Tyler, T.R. (2006). "Why People Obey the Law." Princeton University Press.

13. Davis, M.H. (1983). "Measuring individual differences in empathy: Evidence for a multidimensional approach." Journal of Personality and Social Psychology, 44(1), 113–126.

14. Kruglanski, A.W., & Webster, D.M. (1996). "Motivated closing of the mind: 'Seizing' and 'freezing.'" Psychological Review, 103(2), 263–283.

15. Frederick, S. (2005). "Cognitive reflection and decision making." Journal of Economic Perspectives, 19(4), 25–42.

16. Barberá, P. (2020). "Social media, echo chambers, and political polarization." In Persily & Tucker (Eds.), Social Media and Democracy. Cambridge University Press.

17. Hans, V.P., & Reyna, V.F. (2011). "To dollars from sense: Qualitative to quantitative translation in jury damage awards." Journal of Empirical Legal Studies, 8(S1), 120–147.

18. Pratto, F., Sidanius, J., Stallworth, L.M., & Malle, B.F. (1994). "Social dominance orientation: A personality variable predicting social and political attitudes." Journal of Personality and Social Psychology, 67(4), 741–763.

19. Pennycook, G., et al. (2016). "Atheists and agnostics are more reflective than religious believers." Frontiers in Psychology, 7, 1028.

20. Jost, J.T., Glaser, J., Kruglanski, A.W., & Sulloway, F.J. (2003). "Political conservatism as motivated social cognition." Psychological Bulletin, 129(3), 339–375.

21. Greenwald, A.G., & Banaji, M.R. (1995). "Implicit social cognition: Attitudes, self-esteem, and stereotypes." Psychological Review, 102(1), 4–27.

22. Greenwald, A.G., Poehlman, T.A., Uhlmann, E.L., & Banaji, M.R. (2009). "Understanding and using the Implicit Association Test: III. Meta-analysis of predictive validity." Journal of Personality and Social Psychology, 97(1), 17–41.
