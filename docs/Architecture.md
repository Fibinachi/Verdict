# Verdict Juror Opinion Engine — Architecture

**Version**: v0.75 | **Last Updated**: 2026-05-29

The engine simulates juror opinion formation through three sequential stages. All code in Services/ToyJurorLogicEngine.cs.

## High-Level Pipeline

`
Stage 1: Static Bias → g₁      Stage 2: Evidence Drift → g₂     Stage 3: Deliberation → g₃, P
Demographics → initial lean     Evidence × rigidity → updated      Binary votes → consensus M
BJW case-type dependent          Primacy/Recency weighting         Directional target pull
Cross-interactions               Motivated reasoning (\|PolId\|>0.4)  Threshold friction
No logistic squashing            λ = logistic(η·traits)             P = logistic(θ₀+θ₁·g₃)
`

## Data Flow

Agent Demographics + BiasFactor Weights → ToyJurorTraits (50+ latent traits via coherent sampling, 2000 tries, 0.90 threshold) → Stage 1 (g₁ = clamp(β₀+β₁·b)) → Stage 2 (g₂ = g₁+γ₁·Δev·multiplier; multiplier = evidence conflicts? 1−λ : 1.0; temporal weight = primacy_anchor×recency_retention) → Stage 3 (Vᵢ = g₂ᵢ>τ; M = ΣwᵢVᵢ/Σwᵢ; threshold friction hᵢ = min(h_base+0.45exp(−15\|g₂−τ\|), 0.98); g₃ = clamp(g₂+(1−h)·φ·(target−g₂)); P = logistic(θ₀+θ₁·g₃); verdict_lean = clamp(0.05,0.95,P)) → agent.VerdictLean

## Deliberation (Single-Pass)

1. Binary vote: Vᵢ = g₂ᵢ > threshold (0.85 criminal, 0.50 civil)
2. Consensus: M = Σ(wᵢ·Vᵢ)/Σ(wᵢ)
3. Target: M≥0.50 → max(τ+0.01, M); else → τ−0.01 (fixed, absolute legal standard)
4. Friction: hᵢ = min(h_base + 0.45·exp(−15·\|g₂−τ\|), 0.98)
5. Conformity: g₃ = clamp(g₂ + (1−h)·φ·(target−g₂))
6. Output: P = logistic(θ₀+θ₁·g₃), verdict_lean = clamp(0.05, 0.95, P)

## Codebase Map

| Subsystem | File | Methods |
|-----------|------|---------|
| Core Engine | Services/ToyJurorLogicEngine.cs | ApplyCoherentDriftDeliberation, ComputeStaticBias, ComputeRigidity, ComputeHardness, ComputeInfluenceWeight, ComputeConformity |
| Traits | Services/ToyJurorLogicEngine.cs | GenerateCoherentTraits, SampleDemographics, SampleTraits, SampleReligion, SamplePolitics, SampleMedia, SampleEconomics, SampleEducationType, SampleBlueCollarDiy, SampleExperience, SampleMemberships, SampleIndividualDifferences |
| Evidence | Services/EvidenceProbativeService.cs | GetMediaImpactMultiplier, DetermineEvidenceCategory, AssessProbativeValue |
| Credibility | Services/JurorCredibilityService.cs | CalculatePerJurorCredibility, CalculateMediaTypeSensitivity |
| Burden | Services/BurdenOfProof.cs | GetConvictionThreshold, CountProsecution, CountDefense, GetPositionLabel |
| Memory | Services/MemoryDecayService.cs | DecayMemories, FuzzifyContent, RecordTrialEvent |
| Coefs | ToyJurorLogicEngine.BuildNeutralToyCoefs() | α/η/ζ/χ/ρ dictionaries |

## Key Design Decisions

- Single-pass deliberation (not iterative): deterministic per-seed, avoids convergence issues
- Binary votes for consensus, continuous for output: prevents feedback loops blurring legal standards
- Threshold friction on g₂ not g₃: removes circular dependency (v0.61 fix)
- Motivated reasoning inside logistic: prevents λ saturation (v0.61 fix)
- Asymmetric acquittal target: fixed at τ−0.01 regardless of majority; "beyond reasonable doubt" is absolute
