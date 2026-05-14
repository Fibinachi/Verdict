# Active Context

## Current Work Focus
Documenting the codebase and creating a comprehensive test harness that tests all functionality.

## Recent Changes
- All five LegalMind features have been fully integrated:
  1. Five-Stage Courtroom Debate ✅
  2. Legal Database Integration ✅
  3. PDF Case Report Generation ✅
  4. Enhanced Document Processing ✅
  5. Dynamic Role Assignment ✅

## Next Steps
1. Create comprehensive Memory Bank documentation
2. Expand the test harness to cover all services, models, providers, and viewmodels
3. Ensure all edge cases are tested
4. Verify the test harness runs successfully

## Active Decisions and Considerations
- The test harness currently tests: Models, TranscriptService, CaseService, CharacterManager, ProviderDiscovery, LLM Providers (logic only), ViewModels, Default Case Settings, and Jury Generation
- Need to add tests for: EvidenceAnalysisService, DebateService, JuryCalculationService, CaseEntityMapper, AgentAssignmentService, LegalDatabaseService, ReportGenerationService, AgentInteractionService, CourtroomManagerService, Logger, and all remaining model edge cases
- The test harness runs as a console application (Tests.csproj) referencing the main project

## Important Patterns and Preferences
- Tests use Console.WriteLine for progress and throw Exception on failure
- LLM response generation tests are skipped by default (require live API keys)
- Test methods follow naming convention: Test[Feature]
- All tests are called from RunTests() method

## Learnings and Project Insights
- The MainViewModel constructor has a complex dependency chain with 9 parameters
- Some services (CharacterManager, ProviderDiscoveryService, Logger) use static methods
- The CourtroomManagerService.InitializeCourtroom takes 5 ObservableCollection parameters
- Agent.CopyTo() is used for role assignment and slot population
- Evidence handling behavior changes based on TrialPhase (Discovery/Pretrial/Trial)
