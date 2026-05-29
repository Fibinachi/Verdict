# Verdict Evidence Model

**Version**: v0.61 | **Last Updated**: 2026-05-28

Evidence flows through a multi-layer weighting pipeline before reaching the three-stage engine.

## Evidence Categories & Raw Impact

| Category | Multiplier | Rationale |
|----------|-----------|-----------|
| Video | 1.6× | Visual + auditory + temporal |
| Physical | 1.5× | Tangible objects feel real |
| Image | 1.4× | Visual evidence compelling |
| Audio | 1.3× | Voice adds authenticity |
| Testimony | 1.0× (base) | Depends on witness credibility |
| Document | 1.0× | Requires reading and parsing |

Category detection: keywords for Testimony/Physical → MediaType mapping fallback.

## Probative Value

```
probative = category_base[EvidenceCategory]
probative *= 0.5 + ReliabilityScore × 0.5
if direct: ×1.2; if circumstantial: ×0.85
if hearsay: ×0.5; if outdated: ×0.6; if unauthenticated: ×0.7
if IsTestimonial: probative *= 0.5 + credibility × 0.5
ProbativeValue = clamp(0.05, 1.0, probative)
```

Category bases: Physical 0.80, Video 0.75, Audio 0.70, Image 0.65, Testimony 0.60, Document 0.60.

Witness credibility baselines: Expert/forensic/ME 0.85, Law enforcement 0.75, Eyewitness 0.60, Default 0.50, Character 0.45.

## Per-Juror Credibility Perception

| Factor | Direction |
|--------|-----------|
| Education | HS +0.05 → JD −0.08 |
| Age | >55 +0.08; <25 −0.05 |
| Politics | Dem +0.03; Rep −0.02 |
| Gender | Female +0.02 |
| Intersectional | WOC witnesses −0.06×ethnicityW |
| Status | Attentive +0.05, Skeptical −0.12, Sympathetic +0.10 |
| Risk | (risk−0.5) × −0.15 |

Returns 0.5 for non-testimonial. Final: clamp(0.05, 0.95).

## Media Type Sensitivity

| Evidence | Age <30 | Age >55 | Graduate Edu |
|----------|---------|---------|-------------|
| Video | +0.15 | −0.05 | −0.08 |
| Image | +0.08 | −0.03 | −0.04 |
| Document | −0.05 | +0.10 | +0.05 |
| Testimony | — | +0.08 | −0.05 |

Heavy TV consumers: video +0.10, image +0.05.

## ΔVerdictLean Pipeline

1. Raw deltaEvidence (−1..+1)
2. Temporal: primacy × recency
3. Direction → conflict → rigidity multiplier
4. Stage 2: g₂ = g₁ + γ₁×Δ×multiplier
5. Stage 3: g₃ = g₂ + (1−h)×φ×(target−g₂)
6. Final: P = logistic(θ₀+θ₁×g₃), clamp(0.05,0.95)

## Consolidated Reference

| Type | Raw | Probative | Sensitivity |
|------|-----|-----------|-------------|
| Video | 1.6× | Direct 1.2×/Circ 0.85× | Educ(−), Age(−y,+o) |
| Physical | 1.5× | Chain-of-custody | Educ(−), Risk(+) |
| Image | 1.4× | Authentication | Age(−y,+o), Media(+) |
| Audio | 1.3× | Authentication | Age(+), Attentive(+) |
| Testimony | 1.0×→cred | Credibility pipeline | Most juror-variable |
| Document | 1.0× | Authentication | Educ(+), smallest variance |
| Hearsay | 0.5× | ×0.5 penalty | Same as parent |
| Circumstantial | 0.85× | ×0.85 | Educ(+) |
| Outdated | 0.6× | ×0.6 | Age(+) |
| Unauthenticated | 0.7× | ×0.7 | LegalKnowledge(+) |

## Code

Services/EvidenceProbativeService.cs: GetMediaImpactMultiplier, DetermineEvidenceCategory, AssessProbativeValue
Services/JurorCredibilityService.cs: CalculatePerJurorCredibility, CalculateMediaTypeSensitivity
Services/ToyJurorLogicEngine.cs: ApplyCoherentDriftDeliberation (integration)
