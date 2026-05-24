# System Patterns

## Architecture
Verdict follows a strict **MVVM (Model-View-ViewModel)** pattern with a **delegated service architecture**.

### Layer Structure
```
Views (XAML) → ViewModels → Services → Models
                              ↓
                          Providers (LLM)
```

### Key Pattern: Delegated Services
`MainViewModel` owns the agent collections and case state, but delegates all domain logic to dedicated injected services. This makes each domain concept independently testable.

## Service Architecture

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `CaseService` | `ICaseService` | Save/load .jur case files; generate PDF reports |
| `TranscriptService` | `ITranscriptService?` | DEVELOPMENTAL: Load/parse transcript files; LLM entity extraction (nullable dependency - not customer-facing) |
| `SettingsService` | `ISettingsService` | Default case settings persistence |
| `CourtroomManagerService` | `ICourtroomManagerService` | Courtroom initialization, slot occupancy, agent reset |
| `EvidenceAnalysisService` | `IEvidenceAnalysisService` | Evidence strength assessment, damage estimation, exposure calculation |
| `DebateService` | `IDebateService` | Stage advancement, influence calculation, event broadcasting |
| `JuryCalculationService` | `IJuryCalculationService` | Average lean, likely verdict string, opinion influence logic |
| `CaseEntityMapper` | `ICaseEntityMapper` | Maps extracted entities (charges/evidence/witnesses) onto CaseFile |
| `AgentAssignmentService` | `IAgentAssignmentService` | Case analysis and dynamic role assignment |
| `JuryDemographicsService` | `IJuryDemographicsService` | Generates jury panels based on county/state demographics |
| `CharacterManager` | — | Save/load .vcs character profiles (static class) |
| `LegalDatabaseService` | — | Query legal citations (IPC sections, U.S. Federal Law) |
| `ReportGenerationService` | `IReportGenerationService` | PDF report rendering with QuestPDF |
| `ProviderDiscoveryService` | — | Discovers installed LLM providers and their metadata (static class) |
| `AgentInteractionService` | `IAgentInteractionService` | Manages agent-to-agent conversations and deliberation |
| `Logger` | — | Static logging utility |

## Component Relationships

### Courtroom Layout
```
JudgeArea (ObservableCollection<Agent>):
  - Reporter (occupied by default)
  - Judge (empty slot)
  - Witness Box (empty slot)

Jurors (ObservableCollection<Agent>):
  - 12 Juror slots (3x4 formation)

DefenseTeam (ObservableCollection<Agent>):
  - Lawyer slots (dynamic, starts with 1)

ProsecutionTeam (ObservableCollection<Agent>):
  - Lawyer slots (dynamic, starts with 1)

Gallery (ObservableCollection<Agent>):
  - 2 Alternate Jurors
  - 4 Observers
  - 2 Client slots (Plaintiff Client, Defense Client)
```

### Data Flow
1. **Evidence Admission**: MainViewModel.AddEvidence → EvidenceAnalysisService.AssessStrength → EvidenceAnalysisService.CalculateExposure → JuryCalculationService.ApplyEvidenceInfluence
2. **Chat Input**: MainViewModel.ProcessChatInputLine → BroadcastEvent → TriggerAgentResponses (separated from transcript processing)
3. **Entity Extraction**: TranscriptService.ExtractEntitiesAsync → CaseEntityMapper.MapToCaseFile (DEVELOPMENTAL, requires ITranscriptService)
4. **Case Persistence**: CaseService.SaveCase/LoadCase (JSON serialization) + asset folder for agent prompts/memories
5. **Jury Generation**: JuryDemographicsService.GenerateJuryPanel → populates Jurors collection
6. **Role Assignment**: AgentAssignmentService.AnalyzeCase → ApplyAssignmentPlan → GenerateDefaultAgents

## Critical Implementation Paths

### Agent Core Metrics
- **Agent.VerdictLean**: Core metric (0.0=Defendant, 1.0=Prosecution/Plaintiff) - only updates if HasOpinion is true
- **Agent.Sentiment**: Emotional state (0.0-1.0) - Reporters remain neutral
- **Agent.Bias**: Inherent bias (-1.0 to 1.0) that amplifies opinion shifts
- **Agent.HasOpinion**: True for Juror, AlternateJuror, Judge; False for Reporter
- **Agent.CanVote**: True for Juror, AlternateJuror only
- **Agent.CanDeliberate**: True for Juror, AlternateJuror only

### Evidence Flow by TrialPhase
- **Discovery**: AssessStrength + mark IsDiscoveryComplete (no broadcast to agents)
- **Pretrial**: AssessStrength + CalculateExposure (settlement/reserve estimates)
- **Trial**: Add to Evidence collection + BroadcastEvent + ApplyEvidenceInfluence

### Debate Stage Progression
```
OpeningStatements → WitnessTestimony → CrossExamination → ClosingArguments → VerdictAnnouncement
```

### Phase Change Handling
- Moving to earlier phase resets appropriate data:
  - Trial → earlier: ResetTrialOpinions (clamp lean to 0.2-0.8, reset sentiment to 0.5)
  - Pretrial → Discovery: Reset settlement/reserve to 0

## Model Hierarchy

### ObservableObject (Base)
- Implements INotifyPropertyChanged
- Provides SetProperty<T> for derived classes
- Used by: Agent, CaseFile, MemoryEntry, AIModelConfiguration, ViewModelBase

### Agent Properties (30+ properties)
- Identity: Name, Gender, Age, Race, Occupation, EducationLevel, IncomeLevel, ZipCode
- Demographics: PoliticalAffiliation, ReligiousAffiliation, MaritalStatus, ParentalStatus
- Personality: Bias, Sentiment, VerdictLean, RiskPerception, SettlementAuthority
- Media: MediaConsumption, ConsumerSegment, Hobbies, ViewingCharacteristics
- Legal: SpecializedKnowledge, CurrentStatus, JudicialRulings, WrittenOpinions, JudicialTemperament
- System: SystemPrompt, Profile, SelectedModel, IsOccupied
- Collections: Memories, TrialEvents

### CaseFile Properties
- Identity: CaseName, CaseNumber, CourtName, Mode, Jurisdiction, JurisdictionSpecifics
- Phase: TrialPhase, CurrentDebateStage, JurorCount
- Financial: EstimatedSettlement, InsuranceReserve
- Collections: Agents, AvailableModels, Instructions, Verdict, Pleadings, Evidence, EventLog
- Prompts: ProsecutionPlaintiffPrompt, DefensePrompt, GeneralPrompt

## LLM Provider Architecture
All providers implement `ILLMProviderModule` interface:
- `ProviderName` - Display name
- `Description` - Provider description
- `DefaultFriendlyName` - Default model name
- `ConfigFields` - Configuration fields (API key, endpoint, model ID)
- `TestConnectionAsync` - Test connectivity
- `GenerateResponseAsync` - Generate LLM response
- `GetAvailableModelsAsync` - List available models

Providers follow OpenAI-compatible API pattern where possible. ProviderDiscoveryService uses reflection to find implementations.

## Test Architecture
- **TestHarness.cs**: Main test runner with Assert helper, runs all test suites
- **ModelTests.cs**: Tests for all 20 model classes (Agent, CaseFile, EvidenceDocument, etc.)
- **ServiceTests.cs**: Placeholder tests for all 15 services
- **ProviderTests.cs**: Placeholder tests for LLM providers
- **ViewModelTests.cs**: Placeholder tests for ViewModels
- **BaseTestClass.cs**: Abstract base with Assert helper and result aggregation
- **TestRunner.cs**: Alternative runner that aggregates results from all test suites
- **Tests.csproj**: Separate project referencing main Verdict.csproj
