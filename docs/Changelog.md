# Changelog

## v0.70 — 2026-05-28

### Default Scenarios
- All 7 default case files now include 8 fully-themed agents (Judge, Reporter, Plaintiff/Prosecution Lawyer, Defense Lawyer, 4 themed Observers)
- Observers include case-appropriate Insurance Adjusters, Coverage Counsel, and Litigation Analysts
- Civil cases (NC accident) have real insurance adjuster personas; criminal cases have thematic equivalents

### ONNX Model Improvements
- HF search now filters to only models with `genai_config.json` (OnnxRuntimeGenAI requirement)
- External-data ONNX models supported (`.onnx_data` files checked alongside `.onnx`)
- Downloaded `llmware/llama-3.2-1b-instruct-onnx` (1.77 GB) to local Models directory

### UI Layout Overhaul
- Side columns reduced 360→240px for better proportionality
- Court record panel now uses proportional row height (was fixed 100px)
- Bench spacing increased 12→24px between Judge/Reporter/Witness seats
- Input prompt centered with MaxWidth=900 and word-wrap
- Court record header changes dynamically with trial phase
- Status bar now phase-aware: shows PHASE row, dynamic CONVICTION/LIABILITY label
- Window now has MinHeight/MinWidth constraints
- Overall margins tightened from 30px→20px for more usable space

### Bug Fixes
- Fixed ONNX local model detection test (5-byte stub → proper 50MB+ file via SetLength)
- Fixed `HasOnnxModelFiles` to recognize external-data `.onnx_data` files

### Documentation
- Updated `progress.md` with correct test counts (674 tests, 0 failures)
- Replaced stale "untested services" list with actual remaining work items

### Test Suite
- 674 tests passing, 0 failures, build clean

## v0.61 — 2026-05-28

### Mathematical Corrections
- Stage 3 rewrite: explicit if/else replaces malformed ternary
- Identity collapse fix: acquittal target fixed at τ−0.01 (not min(M,τ−0.01))
- Circular dependency removal: friction on g₂ not g₃
- Motivated reasoning: blended inside logistic (not post-hoc)
- Explicit ordering: target → g₃ → P → verdict_lean

### Engine Changes
- ComputeRigidityWithMotivatedTerm overload
- evidenceDirection parameter with sign(adjustedDeltaEvidence) fallback
- Only |PolId|>0.4 deploys motivated reasoning
- verdict_lean clamping: (0,1) → (0.05,0.95)
- Binary votes: Vᵢ = g₂ > threshold
- Asymmetric target: acquittal τ−0.01; conviction max(τ+0.01,M)

### Coefficients
α: pol_id 0.90→0.60, cons_relig 0.25→0.35, gender 0→0.25, ethnicity 0→0.35, nra flipped, bjw case-dependent
ζ: RWA 0.25→0.30, PolStrength 0.20→0.25
ρ: −30-40% globally; Logistic(0.6×x) scaling
χ: quadratic → softplus
9 new individual-difference traits

### Documentation
New: Architecture, AlgorithmicOverview, CalibrationAndValidation, APIReference, EvidenceModel, TraitModel, TraitGlossary, WeightMapping, CalibrationGuide, PipelineExample, Changelog
Updated: math.md

## v0.60 — 2026-05-27
- Probative value + media-type evidence weighting
- EvidenceProbativeService + JurorCredibilityService extraction
- BurdenOfProof: criminal 0.85, civil 0.50, reasonable doubt zone
- Research-backed default weights

## v0.50 — 2026-05-26
- Factored Agent into AgentDemographics, BiasDimensionWeights, AgentModelOverrides
- MemoryDecayService extraction
- 4 test suites

## v0.40 — 2026-05-15
- AgentChatWindow, JurorReportWindow, ExhibitListWindow
- Photo evidence, DetailedAnalysis, Stage menu, dynamic title bar

## v0.30 — 2026-05-09
- AgentChat, JurorReport, ExhibitList, Stage menu

## v0.20 — 2026-05-01
- Split Models.cs, ViewModelBase, 12 services, DeepSeek, ExtractedEntities
