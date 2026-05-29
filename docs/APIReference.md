# Verdict Juror Opinion Engine — API Reference

**Version**: v0.75 | **Last Updated**: 2026-05-29

## Core Engine

### ToyJurorLogicEngine.ApplyCoherentDriftDeliberation

```csharp
public static void ApplyCoherentDriftDeliberation(
    IReadOnlyList<Agent> jurors, double deltaEvidence,
    IReadOnlyList<BiasFactor>? biasFactors = null,
    int maxSampleTries = 2000, double coherenceThreshold = 0.9,
    int? seed = null, CaseMode mode = CaseMode.Civil,
    int evidenceIndex = 0, int totalEvidenceCount = 1,
    string caseSubType = "", double evidenceDirection = 0.0)
```

| Param | Type | Range | Description |
|-------|------|-------|-------------|
| jurors | IReadOnlyList<Agent> | Non-null | Only IsOccupied && CanVote processed |
| deltaEvidence | double | −1..+1 | + = prosecution, − = defense |
| biasFactors | IReadOnlyList<BiasFactor>? | Null or 12 weights | Category bias weights |
| maxSampleTries | int | ≥1 (rec 2000) | Max coherent trait attempts |
| coherenceThreshold | double | 0–1 (rec 0.9) | Min coherence score |
| seed | int? | Any or null | Reproducibility |
| mode | CaseMode | Civil/Criminal | Conviction threshold |
| evidenceIndex | int | 0-based | Primacy/recency position |
| totalEvidenceCount | int | ≥1 | Total evidence items |
| caseSubType | string | SexualAssault/ViolentCrime/PropertyCrime | BJW direction |
| evidenceDirection | double | −1/0/+1 | Explicit; 0 = infer from delta |

**Side Effects**: Sets agent.VerdictLean (0.05–0.95) and agent.Sentiment (0.5).

## BurdenOfProof

| Method | Input | Returns |
|--------|-------|---------|
| GetConvictionThreshold(CaseMode) | Criminal | 0.85 |
| GetConvictionThreshold(CaseMode) | Civil | 0.50 |
| GetPositionLabel(lean, isCriminal) | lean>τ | GUILTY/LIABLE |
| GetPositionLabel(lean, isCriminal) | lean<0.50 | NOT GUILTY/NOT LIABLE |
| GetPositionLabel(lean, isCriminal) | 0.50..τ | REASONABLE DOUBT/UNDECIDED |
| CountProsecution(jurors, isCriminal) | — | Count above threshold |
| CountDefense(jurors, isCriminal) | — | Count at/below threshold |

## EvidenceProbativeService

| Method | Returns |
|--------|---------|
| GetMediaImpactMultiplier(category) | Video 1.6, Physical 1.5, Image 1.4, Audio 1.3, Testimony/Document 1.0 |
| DetermineEvidenceCategory(mediaType, summary, content) | One of Video/Audio/Image/Physical/Testimony/Document |
| AssessProbativeValue(doc, summary, content) | Sets doc.ProbativeValue (0.05–1.0) |

Probative formula: category base → reliability scale → direct ×1.2/circ ×0.85 → hearsay ×0.5/outdated ×0.6/unauth ×0.7 → testimonial credibility scale.

## JurorCredibilityService

| Method | Returns | Range |
|--------|---------|-------|
| CalculatePerJurorCredibility(juror, doc) | Perceived witness credibility | 0.05–0.95 |
| CalculateMediaTypeSensitivity(juror, category) | Sensitivity multiplier | ~0.7–1.3 |

Credibility adjustments: Education (HS +0.05 → JD −0.08), Age (>55 +0.08, <25 −0.05), Politics (Dem +0.03, Rep −0.02), Gender (F +0.02), Intersectional (WOC witnesses −0.06×ethnicityW), Status (Attentive +0.05, Skeptical −0.12), Risk ((risk−0.5)×−0.15). Returns 0.5 for non-testimonial.

## MemoryDecayService

DecayMemories(agent) — Progressive fuzzification of MemoryEntry objects.
RecordTrialEvent(agent, description, source) — New MemoryEntry with source tag.
ReinforceMemory(agent, keyword) — Strengthen matching memories.

## Coefficient Dictionaries (BuildNeutralToyCoefs)

| Dict | Purpose | Key Values |
|------|---------|------------|
| Alpha | Static bias | pol_id=0.60, conserv_relig=0.35, gender=0.25, ethnicity=0.35, nra=0.12, news_lean=0.18, prior_victim=0.15, prior_system=−0.12, bjw=0.20, death_penalty=0.25, trust=0.15, empathy=−0.10 |
| Eta | Rigidity | RWA=0.55, SDO=0.45, punitiveness=0.25, conserv_relig=0.25, pol_strength=0.25, need_closure=0.12, cognitive_reflection=−0.15, bjw=0.10 |
| Zeta | Hardness | RWA=0.30, SDO=0.20, conserv_relig=0.25, pol_strength=0.25, need_closure=0.10, cognitive_reflection=−0.08 |
| Chi | Influence | education_years=0.25, nfc=0.20, cognitive_reflection=0.12, elite_private=0.08, age=0.02 |
| Rho | Conformity | RWA=0.12, SDO=0.07, conserv_relig=0.10, pol_id=0.06, religiosity=0.10, need_closure=0.08, nfc=−0.08, nra=−0.04 |

Scalars: Beta0=0, Beta1=1, Gamma1=1, Theta0=0, Theta1=5.

## Expected Ranges

| Variable | Min | Max |
|----------|-----|-----|
| VerdictLean | 0.05 | 0.95 |
| g₁, g₂, g₃ | 0.0 | 1.0 |
| b | ~−3.0 | ~+3.0 |
| λ, φ | 0.0 | 1.0 |
| h | 0.0 | 0.98 |
| w | 1.0 | 3.5 |
| M | 0.0 | 1.0 |
| friction | ~0.0 | 0.45 |
