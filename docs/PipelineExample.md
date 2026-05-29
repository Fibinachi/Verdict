# Pipeline Example — Step-by-Step Walkthrough

**Version**: v0.75 | Complete worked example through all three stages.

## Setup
Criminal trial (armed robbery). Juror: Female, 45, White, Bachelor, Middle Class, Republican, Protestant.
Evidence: Security footage (Video, prosecution) at index 2 of 5 items.

## Stage 0: Generated Traits
RWA=7.2, SDO=5.8, Punitiveness=6.5, PolId=+0.27, ConservRelig=6.1, NFC=6.2, BJW=5.8, CRT=5.5, NeedClosure=5.4, Trust=6.0, DPQualified=1

## Stage 1: Static Bias → g₁

b = 0.60×0.27(pol) + 0.35×6.1(cons_relig) + 0.18×0.5×−1(news→defense) + 0.15×1(victim) + 0.20×5.8(bjw) + 0.25×1(dp) + 0.15×6.0(trust) + (−0.10)×5.0×−1(empathy) + 0.25×1.0(gender) + 0.35×0.5(ethnicity)
b = 0.162 + 2.135 − 0.090 + 0.150 + 1.160 + 0.250 + 0.900 + 0.500 + 0.250 + 0.175 = 5.592
g₁ = clamp(5.592, 0, 1) = 1.000 → Maximum prosecution lean

Key drivers: conserv_relig (+2.135) and BJW in violent crime (+1.160).

## Stage 2: Evidence Drift → g₂

Temporal: primacy=1.0 (not in first 20%), recency=exp(−0.05×2)=0.905, adjusted_delta=0.35×0.905=0.317

Rigidity: x = 0.55×7.2 + 0.45×5.8 + 0.25×6.5 + 0.25×6.1 + 0.25×6.0 + 0.12×5.4 − 0.15×5.5 + 0.10×5.8 + 0.08×4.0 + 0.12×5.0 + 0.15×6.0 + 0.12×5.0 + 0.08×6.0 = 14.523
λ = logistic(14.523) ≈ 1.000 → Near-total rigidity

Conflict: evidence_dir=+1, PolId=+0.27, conflicts = (+1)×(+0.27)>0? NO → aligned, no motivated reasoning
multiplier = 1.0 → g₂ = clamp(1.000 + 1.0×0.317×1.0) = 1.000

## Stage 3: Deliberation → VerdictLean

Binary: V = 1.000 > 0.85 = GUILTY

Influence: x = 0.25×16 + 0.05×1 + 0.20×6.2 + 0.12×5.5 + 0.02×45 + 0.03×0.5 + 0.08×6.0 + 0.04×5.0 + 0.3×5×0.3 = 8.195
w = 1.0 + 0.08×log(1+exp(8.195)) = 1.656

Hardness: x_zeta = 0.30×7.2 + 0.20×5.8 + 0.25×6.1 + 0.25×6.0 + 0.10×5.4 − 0.08×5.5 + 0.12×5.0 + 0.10×6.0 + 0.10×5.0 + 0.05×3 = 8.295
h_base = logistic(8.295) = 0.9998
friction = 0.45×exp(−15.0×|1.0−0.85|) = 0.45×exp(−2.25) = 0.047
h_i = min(0.9998+0.047, 0.98) = 0.98 [capped]

Consensus: 7 guilty + 4 not-guilty (weighted) → M≈0.62
Target = max(0.86, 0.62) = 0.86

Conformity: x_rho = 0.12×7.2 + 0.07×5.8 + 0.10×6.1 + 0.06×0.27 + 0.07×6.0 + 0.06×5.0 + 0.10×5.0 + 0.08×5.4 − 0.08×6.2 + 0.08×5.0 + 0.08×5.0 + 0.04×5.0 = 4.052
φ = logistic(0.6×4.052) = 0.919 → High conformity

But h=0.98 → only 2% of conformity applies:
g₃ = clamp(1.000 + 0.02×0.919×(−0.14)) = clamp(1.000 − 0.0026) = 0.997
P = logistic(5.0×0.997) = 0.993
verdict_lean = clamp(0.05, 0.95, 0.993) = 0.95

## Summary

| Stage | Value | Meaning |
|-------|-------|---------|
| g₁ | 1.000 | Maximum prosecution (religiosity+BJW) |
| λ | ≈1.000 | Near-total evidence immunity |
| g₂ | 1.000 | Unchanged (aligned, but even counterevidence futile) |
| V | 1.0 | GUILTY |
| h | 0.98 | Capped — essentially immovable |
| φ | 0.919 | Highly conforming (but only 2% applies) |
| g₃ | 0.997 | Barely moved |
| verdict_lean | 0.95 | Near-certain conviction |

## Counterfactual: Movable Juror

PolId=−0.15, RWA=3.5, SDO=4.0, CRT=8.0, NFC=8.5, NeedClosure=2.5:
λ≈0.25 (evidence-responsive), h≈0.30 (open), w≈2.8 (elite persuasive), φ≈0.35 (independent)

This juror is the "swing vote" — moved by evidence, persuasive, but not swayed by mere majority.
