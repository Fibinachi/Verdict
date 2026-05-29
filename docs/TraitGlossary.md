# Trait Glossary

**Version**: v0.61 | Complete glossary of all latent traits.

## Core Psychological (0–10)

**RWA** — Altemeyer 1981. Rigidity η=0.55, Hardness ζ=0.30, Conformity ρ=0.12. High = submits to authority, conventional, resists counterevidence.
**SDO** — Sidanius & Pratto 1999. Rigidity η=0.45, Hardness ζ=0.20, Conformity ρ=0.07. High = prefers hierarchy, resists status-quo challenges.
**Punitiveness** — Steiner 2001. Rigidity η=0.25. High = desires harsh punishment. U-shaped with age.
**RapeMyth** — Burt & Albin 1981. Coherence only. Endorses victim stereotypes. Higher with conservative gender+religiosity.
**Religiosity** — Rigidity η=0.12, Conformity ρ=0.10. General religious commitment.
**ConservRelig** — Static bias α=0.35, Rigidity η=0.25, Hardness ζ=0.25, Conformity ρ=0.10. Doctrinal literalism.

## Political

**PolId** (−1..+1) — Static bias α=0.60, Conformity ρ=0.06. Triggers motivated reasoning when |PolId|>0.4. Strongest directional driver.
**PolStrength** (0–10) — Rigidity η=0.25, Hardness ζ=0.25, Conformity ρ=0.07.
**PrimaryHistory** (0–10) — Rigidity η=0.10, Hardness ζ=0.10. Prior political engagement.
**Activism** (0–10) — Hardness ζ=0.08. Political/social activism.
**OnlinePartisan** (0–10) — Rigidity η=0.08, Hardness ζ=0.08. Echo-chamber exposure.

## Civic (0–10)

**Volunteer** — communityW×3, experience, reliability×0.8. Civic volunteering.
**Donate** — communityW×2, legalW×0.5, reliability×0.5. Charitable/political donations.

## Media

**TvCrime** (0–20) — Rigidity η=0.08. "CSI effect" proxy.
**TvCableNews** (0–20) — Cluster assignment.
**NewsLean** (−1..+1) — Static bias α=0.18 (×−1). Fox +1, MSNBC −1.
**NewsIntensity** (0–30) — High/low information distinction.
**SmUse** (0–8) — Influences SmNewsReliance.
**SmPolar** (0–10) — Conformity ρ=0.06. Social media echo chamber.

## Economic

**IncomeBand** (1–5) — Rigidity η=0.10, Hardness ζ=0.05.
**JobSecurity** (0–10) — Rigidity η=0.06.
**HomeOwn** (0/1) — Rigidity η=0.05.
**Union** (0/1) — Coherence only.

## Education Type (0/1)

ElitePrivate χ=0.08; StateFlagship χ=0.05; OtherPrivate χ=0.04; CommunityCollege χ=0.02 ζ=0.02; TradeSchool χ=0.03 ζ=0.03; Hbcu χ=0.02 ζ=0.03; BibleCollege χ=0.02 ζ=0.06.

## Identity

BlueCollar (0/1) ζ=0.04 ρ=0.02; Diyer (0–10) ζ=0.02 ρ=0.01; ToolOwnership (0–10); MakerIdentity (0–10); SierraClub (0/1); Nra (0/1) α=0.12 η=0.10 ρ=−0.04; OilExecutive (0/1).

## Experience (0–2)

PriorVictimization — α=0.15. 0=none, 1=property, 2=violent.
PriorSystemContact — α=−0.12. 0=none, 1=minor, 2=significant.

## Cognitive Biases (0–10)

Anchoring: Hardness×0.12, Conformity×0.04, Influence×0.3. Chapman & Bornstein 1996.
HindsightBias: Hardness×0.12, Influence×−0.2. Fischhoff 1975.
ConfirmationBias: Rigidity×0.15. Nickerson 1998.
NarrativeCoherence: Rigidity×0.08, Hardness×0.10, Influence direct. Pennington & Hastie 1992.
OutcomeBias: Conformity×0.05. Baron & Hershey 1988.
SocialConsensus: Conformity×0.08, Influence direct. Sunstein 2003.
StatusQuoBias: Rigidity×0.12, Hardness×0.10. Samuelson & Zeckhauser 1988.

## Individual Differences

NeedForCognition (0–10) χ=0.20 ρ=−0.08. Cacioppo & Petty 1982.
BeliefInJustWorld (0–10) α=0.20 η=0.10. Lerner 1980.
DeathPenaltyQualified (0/1) α=0.25. Haney 1984.
TrustInInstitutions (0–10) α=0.15. Tyler 2006.
TraitEmpathy (0–10) α=−0.10. Davis 1983.
NeedForClosure (0–10) η=0.12 ζ=0.10 ρ=0.08. Kruglanski 1996.
CognitiveReflection (0–10) η=−0.15 ζ=−0.08 χ=0.12. Frederick 2005.
SmNewsReliance (0–10) ρ=0.08. Barbera 2020.
PersonalInjuryHistory (0/1). Hans & Reyna 2011.

Full citations: docs/JurorBiasResearch.md. Full formulas: docs/TraitModel.md.
