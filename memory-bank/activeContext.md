# Active Context

## Current Work Focus
Completed major architectural improvements: Agent factoring, burden-of-proof thresholds, and deliberation bug fixes. All 577 tests passing.

## Recent Changes (May 2026)
- **2026-05-27**: Added `BurdenOfProof` service — criminal cases now require lean > 0.85 for guilty (beyond reasonable doubt), jurors at 0.50–0.85 show "reasonable doubt" and vote not guilty. Fixed hung-jury detection: unanimous-side vote returns verdict. Fixed LLM prompt to align text with lean values. Updated all verdict counting, questionnaires, reports, and deliberation logic.
- **2026-05-26**: Factored `Agent.cs` into `AgentDemographics`, `BiasDimensionWeights`, `AgentModelOverrides`. Extracted `MemoryDecayService`. Added 4 test suites.

## Next Steps
1. Expand test coverage for remaining services (EvidenceAnalysis, Debate, ReportGeneration, etc.)
2. Consider calibrating burden-of-proof threshold against real jury data
3. Improve LLM deliberation prompt for better alignment with verdict lean values

## Important Patterns and Preferences
- `BurdenOfProof` is a static helper — no interface, used across ViewModels, Services, Tests
- `MemoryDecayService` is static — pure computation with no dependencies
- Agent sub-objects (`Demographics`, `BiasWeights`, `ModelOverrides`) all inherit `ObservableObject`
- All flat Agent properties delegate to sub-objects for XAML binding backward compatibility
- `CopyTo` uses each sub-object's `CopyFrom` for bulk transfer