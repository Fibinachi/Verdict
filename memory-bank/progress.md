# Progress

## What Works
- **Models**: 24 model classes (20 core + 4 factored: AgentDemographics, BiasDimensionWeights, AgentModelOverrides, BurdenOfProof) with properties, INotifyPropertyChanged, serialization
- **Services**: 17 services implemented (added MemoryDecayService, BurdenOfProof)
- **Providers**: All 9 LLM providers implemented
- **ViewModels**: MainViewModel, CaseSettingsViewModel, ModelSettingsViewModel
- **Views**: All 10 XAML windows/user controls
- **Test Harness**: 577 tests passing — models, services, providers, viewmodels, MemoryDecayService, AgentDemographics, BiasDimensionWeights, AgentModelOverrides
- **Burden of Proof**: Mode-aware conviction thresholds (criminal >0.85, civil >0.50)
- **LegalMind Features**: All 5 phases fully integrated

## Recent Changes (May 2026)
- **2026-05-27**: Added `BurdenOfProof` service with mode-aware conviction thresholds. Criminal cases now require lean > 0.85 (beyond reasonable doubt) for guilty; jurors at 0.50–0.85 have "reasonable doubt" and vote not guilty. Civil cases unchanged at > 0.50 preponderance. Fixed hung-jury detection: unanimous-side vote now correctly returns a verdict regardless of lean spread. Fixed deliberation prompt to align LLM text with lean values.
- **2026-05-26**: Factored `Agent.cs` into composable sub-objects (`AgentDemographics`, `BiasDimensionWeights`, `AgentModelOverrides`). Extracted `MemoryDecayService` for progressive memory fuzzification. Added 4 new test suites (577 total tests, all passing).

## What's Left to Build
- **Comprehensive Test Coverage**: Need tests for:
  - EvidenceAnalysisService (strength assessment, exposure calculation)
  - DebateService (stage advancement, influence calculation, event broadcasting)
  - CaseEntityMapper (entity mapping to CaseFile)
  - AgentAssignmentService (case analysis, role assignment, default agent generation)
  - LegalDatabaseService (citation search, relevance scoring)
  - ReportGenerationService (PDF generation)
  - AgentInteractionService (statement generation, examination, deliberation)
  - CourtroomManagerService (initialization, slot occupancy, reset)
  - Logger (logging functionality)
  - All model edge cases (null handling, boundary conditions)
  - Provider edge cases (connection failures, invalid configs)
  - ViewModel edge cases (null case files, empty collections)

## Current Status
- **Codebase**: Fully functional with all features implemented
- **Documentation**: Updated docs/math.md with burden-of-proof section, AGENTS.md with refactoring log
- **Testing**: 577 tests passing in comprehensive test harness
- **Build**: Compiles successfully with `dotnet build`

## Known Issues
- Tests project is excluded from main project via Compile Remove directives in Verdict.csproj
- LLM response generation tests require live API keys (skipped by default)
- Some services use static methods which can complicate testing
- QuestPDF Community license limits commercial use