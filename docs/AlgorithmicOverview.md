# Verdict Juror Opinion Engine — Algorithmic Overview

**Version**: v0.75 | **Last Updated**: 2026-05-29
**Audience**: Legal professionals, researchers, reviewers — no formulas.

## What This Engine Does

Simulates how individual jurors form opinions during a trial: demographic profile → evidence exposure → jury deliberation → verdict lean (0.05 to 0.95).

## The Three Stages

### Stage 1: Who You Are (Static Bias → g₁)

Before evidence, every juror has pre-existing attitudes from life experience:
- Conservative juror → slight prosecution lean
- Prior negative justice system experience → defense lean
- "Bad things happen to bad people" believer → prosecution lean in violent crime

These combine into g₁ (0 to 1). Values above 0.5 lean prosecution; below 0.5 lean defense.

### Stage 2: What You Hear (Evidence → g₂)

Evidence moves jurors, moderated by:

**Rigidity (λ)**: Some jurors resist counterevidence. High authoritarianism, need for closure, or strong ideology → harder to move.

**Motivated Reasoning** (Kahan et al. 2012): When evidence conflicts with political identity, educated partisans become MORE resistant — a counterintuitive but well-documented effect. Only applies when |PolId| > 0.4.

**Temporal Weighting**: Early evidence gets primacy boost (first impressions); recent evidence gets recency boost (fresher memory). Middle-trial evidence has least impact.

After Stage 2, each juror has g₂.

### Stage 3: What Others Think (Deliberation → g₃, verdict_lean)

**Binary Voting**: Each juror casts a private vote. Criminal: 85%+ to convict ("beyond reasonable doubt"). Civil: >50% to find liable ("preponderance"). A juror at 70% in a criminal case votes NOT guilty — reasonable doubt.

**Group Consensus (M)**: Weighted by influence. Higher education, analytical thinking, narrative coherence → more weight.

**Directional Pull**: Majority favors conviction → pulled toward conviction boundary. Majority favors acquittal → pulled to threshold itself (fixed, absolute standard — reasonable doubt is not proportional to majority size).

**Threshold Friction**: Heightened resistance when peer pressure tries to push a juror across a legal boundary. A juror at 0.84 in criminal (just below 0.85) becomes temporarily harder to move.

**Conformity (φ)**: High need for closure, social media reliance → more conforming. High need for cognition, cognitive reflection → independent thinkers.

## Burden of Proof

| Aspect | Criminal | Civil |
|--------|----------|-------|
| Standard | Beyond reasonable doubt | Preponderance |
| Conviction threshold | 0.85 | 0.50 |
| Juror at 0.70 | NOT guilty (reasonable doubt) | LIABLE |

## Research Basis

Forresta (2025), Haney (1984), Kahan et al. (2012), Cacioppo & Petty (1982), Lerner (1980), Tyler (2006), Davis (1983), Kruglanski (1996), Frederick (2005), Crenshaw (1989), Pennington & Hastie (1992), Greenwald et al. Full citations: docs/JurorBiasResearch.md.

## Limitations

Does NOT: predict actual outcomes, model lawyers/judges/procedure, claim causation, or use calibrated coefficients. See docs/CalibrationAndValidation.md.
