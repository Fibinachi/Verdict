# Progress

## What Works
- **Models**: 24 model classes (20 core + 4 factored: AgentDemographics, BiasDimensionWeights, AgentModelOverrides, BurdenOfProof) with properties, INotifyPropertyChanged, serialization
- **Services**: 17 services implemented (added MemoryDecayService, BurdenOfProof)
- **Providers**: All 9 LLM providers implemented
- **ViewModels**: MainViewModel, CaseSettingsViewModel, ModelSettingsViewModel
- **Views**: All 10 XAML windows/user controls
- **Test Harness**: 674 tests passing — models, services, providers, viewmodels, MemoryDecayService, AgentDemographics, BiasDimensionWeights, AgentModelOverrides, EvidenceAnalysis, Debate, CourtroomManager, CaseEntityMapper, AgentAssignment, JuryDemographics, LegalDatabase, Logger, AgentInteraction, ToyJurorLogicEngine
- **Burden of Proof**: Mode-aware conviction thresholds (criminal >0.85, civil >0.50)
- **LegalMind Features**: All 5 phases fully integrated

## Recent Changes (May 2026)
- **2026-05-27**: Added `BurdenOfProof` service with mode-aware conviction thresholds. Criminal cases now require lean > 0.85 (beyond reasonable doubt) for guilty; jurors at 0.50–0.85 have "reasonable doubt" and vote not guilty. Civil cases unchanged at > 0.50 preponderance. Fixed hung-jury detection: unanimous-side vote now correctly returns a verdict regardless of lean spread. Fixed deliberation prompt to align LLM text with lean values.
- **2026-05-26**: Factored `Agent.cs` into composable sub-objects (`AgentDemographics`, `BiasDimensionWeights`, `AgentModelOverrides`). Extracted `MemoryDecayService` for progressive memory fuzzification. Added 4 new test suites (577 total tests, all passing).

## What's Left to Build
- **UCMJ Courts-Martial Venue** (deferred to later version — see TODO.md)
- **Calibration against real jury datasets** (data-acquisition gated — see docs/CalibrationAndValidation.md)
- **Dynamic counsel table scaling**: verify UI handles 2→6 occupants per table cleanly
- **Active speaker highlighting**: highlight agent whose turn it is to speak in courtroom UI

## Current Status
- **Codebase**: Fully functional with all features implemented
- **Documentation**: Updated docs/math.md with burden-of-proof section, AGENTS.md with refactoring log
- **Testing**: 674 tests passing in comprehensive test harness (1 known intermittent: ONNX local model detection)
- **Build**: Compiles successfully with `dotnet build`

## Known Issues
- Tests project is excluded from main project via Compile Remove directives in Verdict.csproj
- LLM response generation tests require live API keys (skipped by default)
- Some services use static methods which can complicate testing
- QuestPDF Community license limits commercial use