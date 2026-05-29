# Verdict Juror Opinion Engine — Calibration & Validation Plan

**Version**: v0.61 | **Last Updated**: 2026-05-28
**Status**: Pre-calibration — research-grounded defaults, not empirically fitted.

## Planned Datasets

| Dataset | Source | Status |
|---------|--------|--------|
| RAND Civil Jury Study (2024) | RAND Corp. | Not acquired |
| NCSC State Court Data | National Center for State Courts | Not acquired |
| BJS Felony Defendants | Bureau of Justice Statistics | Not acquired |

Auxiliary: Mock-jury meta-analyses (Devine, Bornstein), GSS, ANES, Pew Religious Landscape.

## Evaluation Metrics

### Distributional Fit
- Mean conviction rate: criminal ~68% (BJS), civil plaintiff ~52% (RAND)
- Hung-jury rate: 2-8% criminal, 1-3% civil
- VerdictLean distribution: bimodal with middle trough
- Deliberation shift: mean 0.05-0.15

### Demographic Parity
- Race × conviction: no systematic disparity beyond documented effects
- Gender × verdict: match meta-analyses direction
- Education × influence: higher ed → higher w, not monotonically dominating
- Political × rigidity: strong partisans more rigid, not absolutely immune

### Sensitivity
- Coefficient sensitivity: ±20% → Δ conviction rate
- Seed stability: same inputs → consistent direction
- Jury size: 6 vs 12 → rate change within expected range
- Evidence ordering: primacy/recency within research ranges

## Methodology

Phase 1: Grid sweep ±50% at 5% increments
Phase 2: Nelder-Mead on most sensitive coefficients
Phase 3: Cross-validation on held-out case types

Loss: L = w₁·|μ_conv − target| + w₂·|μ_hung − target| + w₃·KL(dist) + w₄·Σ|parity_gap|

## Known Limitations

Structural: Single-pass deliberation (not iterative), no foreperson effects, no admissibility disputes, static traits, no emotional contagion.
Coefficient: US/Western-centric, mock-jury generalizability, publication bias.
Computational: Seed sensitivity, no fatigue/time effects.

## Reproducibility

Deterministic given: agent demographics, bias weights, evidence sequence, case mode, seed. Record these to reproduce any result.
