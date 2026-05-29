# Calibration Guide

**Version**: v0.75 | Practical guide to tuning coefficients.

## Quick Reference

### Alpha (α) — Static Bias
pol_id=0.60 | conserv_relig=0.35 | gender=0.25 | ethnicity=0.35 | nra=0.12 | news_lean=0.18 | prior_victim=0.15 | prior_system=−0.12 | bjw=0.20 | death_penalty=0.25 | trust=0.15 | empathy=−0.10

### Eta (η) — Rigidity
RWA=0.55 | SDO=0.45 | punitiveness=0.25 | conserv_relig=0.25 | pol_strength=0.25 | need_closure=0.12 | cognitive_reflection=−0.15 | bjw=0.10

### Zeta (ζ) — Hardness
RWA=0.30 | SDO=0.20 | conserv_relig=0.25 | pol_strength=0.25 | need_closure=0.10 | cognitive_reflection=−0.08

### Chi (χ) — Influence
education_years=0.25 | nfc=0.20 | cognitive_reflection=0.12 | elite_private=0.08 | age=0.02

### Rho (ρ) — Conformity
RWA=0.12 | SDO=0.07 | conserv_relig=0.10 | pol_id=0.06 | religiosity=0.10 | need_closure=0.08 | nfc=−0.08 | nra=−0.04

## Common Scenarios

**Hung juries too rare**: Reduce all ρ 20-30%, reduce conformity scaling to Logistic(0.4×x), increase ζ
**Hung juries too common**: Increase ρ 20%, scaling to 0.8, decrease ζ
**Politics dominates**: Reduce α_pol_id, increase α_conserv_relig/α_empathy/α_trust, reduce η_pol_strength
**Evidence weak**: Increase γ₁ to 1.5-2.0, reduce η globally 20%, reduce friction 0.45→0.30
**No deliberation movement**: Increase ρ 30%, reduce ζ 20%, reduce friction 0.45→0.30

## Safe Bounds

α: −1..+1 (>1.5 saturates g₁) | η: 0..1 (>1 λ=1 no evidence) | ζ: 0..0.5 (>0.5 h>0.95) | χ: 0..0.5 (>0.5 elite at cap) | ρ: −0.2..0.3 (>0.5 φ saturates) | β₁: 0.5..2 (>3 loses variance) | γ₁: 0.5..2 (>3 evidence overwhelms) | θ₁: 2..8 (>10 all at extremes) | Friction: 0.1..0.8 | Criminal τ: 0.80-0.90 (<0.75 violates law)

## Known Sensitivities

1. θ₁ (5.0): ±1.0 dramatically alters distribution. Tune in 0.5 steps.
2. Conformity scaling (0.6): 0.6→0.8 ≈ doubles hung-jury rate.
3. Friction (0.45): At 0.65, near-threshold jurors nearly immovable.
4. Motivated reasoning (0.20): At 0.30, educated partisans immune to counterevidence.
5. α_pol_id (0.60): ±0.15 shifts conviction 3-5pp.

## Testing

1. Change ONE coefficient
2. dotnet run --project Tests/Tests.csproj
3. Load DefaultCases/twelve-mildly-bothered-agents.jur
4. Generate jury, record baseline
5. Apply change in BuildNeutralToyCoefs()
6. Rebuild, re-run same case same seed
7. Compare: mean conviction, hung-jury rate, deliberation shift

## Checklist
[ ] Conviction rate within 5pp of target (68% criminal, 52% civil)
[ ] Hung-jury 2-8% criminal, 1-3% civil
[ ] VerdictLean bimodal
[ ] Deliberation shift 0.05-0.15
[ ] No coefficient at bound
[ ] Same seed = same output
[ ] Different seeds plausible
[ ] Extremes produce extremes
[ ] Zero evidence stays near g₁
