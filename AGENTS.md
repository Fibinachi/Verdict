# Repository Guidelines

## Project Structure & Module Organization
Verdict is a WPF application (.NET 9) designed for native Windows deployment. The architecture follows a strict MVVM pattern.

- **Models/**: Individual class files for all entities. **Agent is now a composed model** — see Agent Architecture below.
  - Core models: `Agent.cs`, `CaseFile.cs`, `EvidenceDocument.cs`, `MemoryEntry.cs`, `CourtroomEvent.cs`, `AIModelConfiguration.cs`, `JuryInstructions.cs`, `VerdictForm.cs`, `ExtractedEntities.cs`, `AgentInteraction.cs`, `Charge.cs`, `CauseOfAction.cs`, `Element.cs`, `LegalStandard.cs`, `CaseMode.cs`, `JurisdictionType.cs`, `TrialPhase.cs`, `CourtPhase.cs`, `AgentRole.cs`, `ObservableObject.cs`.
  - **Factored Agent sub-models**: `AgentDemographics.cs` (age, gender, race, occupation, education, income, etc.), `BiasDimensionWeights.cs` (11 per-category bias weight overrides), `AgentModelOverrides.cs` (per-agent model, temperature, token limit), `BiasFactor.cs`, `JurorOpinion.cs`.
- **ViewModels/**: Contains `MainViewModel.cs`, `CaseSettingsViewModel.cs`, `ModelSettingsViewModel.cs`, and `ViewModelBase.cs`.
- **Views/**: Contains XAML windows and user controls (`AgentProfileWindow.xaml`, `CaseSettingsWindow.xaml`, `ModelSettingsWindow.xaml`, `CounselDiscussionWindow.xaml`, `ClientConversationWindow.xaml`, `AddModelDialog.xaml`, `GoogleModelSelectionDialog.xaml`, `ModelDetailsDialog.xaml`).
- **Services/**: Contains 17 services for business logic. Key additions: `MemoryDecayService` (progressive memory fuzzification extracted from `Agent.cs`), `BurdenOfProof` (mode-aware conviction thresholds for criminal vs civil cases).
## Agent Architecture (Factored)
`Agent` uses composition with three sub-object models that each inherit `ObservableObject`:
- **`Agent.Demographics`** (`AgentDemographics`): Gender, Age, Race, Occupation, EducationLevel, IncomeLevel, MediaConsumption, ConsumerSegment, MaritalStatus, ParentalStatus, Hobbies, ReligiousAffiliation, PoliticalAffiliation, ZipCode, ViewingCharacteristics, CurrentStatus, SpecializedKnowledge. Has `CopyFrom()`, `ToProfileSummary()`.
- **`Agent.BiasWeights`** (`BiasDimensionWeights`): 11 nullable `double?` properties (AgeBiasWeight through CommunityTiesWeight). Null = inherit global default. Has `CopyFrom()`, `ResetAll()`, `SetAll(double)`.
- **`Agent.ModelOverrides`** (`AgentModelOverrides`): SelectedModel, ModelTemperatureOverride (clamped 0.0–2.0), ModelMaxTokensOverride (min 64). Has `CopyFrom()`, `HasOverrides`.
- **2026-05-15**: Added `AgentChatWindow` for LLM-powered conversation with any agent via right-click "Chat", with memory persistence tagged `Source = "Chat"` and memory wipe button. Added `JurorReportWindow` for right-click sentiment/verdict lean/memory report on juror seats. Added `ExhibitListWindow` accessible from Case menu (`_Exhibit List`) showing all evidence in a `ListView`. Added photo evidence support by extending file dialog filters to include image formats (`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`) with automatic `MediaType` classification (`"Image"` vs `"Document"`) in `EvidenceDocument`. Added `DetailedAnalysis` field to `EvidenceDocument` for full LLM analysis storage. Added `Stage` top-level menu with trial phase (Discovery/Pretrial/Trial) and court phase submenus. Added dynamic title bar via `WindowTitle` property on `MainViewModel` formatted as `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"`. Increased bench spacing (`Margin="12,0"`) on bench area `ContentPresenter` items. Removed redundant "Case loaded." message boxes. Fixed double LLM call in evidence analysis pipeline.
All original flat properties (e.g., `agent.Age`, `agent.SelectedModel`) remain available for XAML binding backward compatibility — they delegate reads/writes to the sub-objects. **`CopyTo`** uses each sub-object's `CopyFrom` for bulk transfer.

Memory decay/fuzzification logic is extracted to **`MemoryDecayService`** (static methods: `DecayMemories`, `FuzzifyContent`, `RecordTrialEvent`, `ReinforceMemory`, `ReinforceRelatedMemories`, `ApproximateNumber`, `RangeNumber`, `VagueNumber`, `RoundToSignificant`, `TryParseCleanNumber`). `Agent` methods delegate to this service.

## Refactoring Log
- **2026-05-27**: Added `BurdenOfProof` service with mode-aware conviction thresholds. Criminal cases now require lean > 0.85 (beyond reasonable doubt) for a guilty vote; jurors at 0.50–0.85 have "reasonable doubt" and vote not guilty. Civil cases unchanged at > 0.50 preponderance standard. Updated all verdict counting, juror position labeling, deliberation completion logic, jury questionnaires, and reports. Fixed hung-jury detection: unanimous-side vote (all jurors on same outcome) now correctly returns a verdict regardless of lean spread. Fixed deliberation prompt to explicitly tell LLM which side the juror's lean value represents, preventing text/lean contradictions.
- **2026-05-27 (evidence weighting)**: Added probative value and media-type-aware evidence weighting. Added `ProbativeValue`, `EvidenceCategory` (Document/Image/Video/Audio/Physical/Testimony), `WitnessCredibility`, `IsTestimonial`, and `ReliabilityScore` fields to `EvidenceDocument`. Refactored `EvidenceAnalysisService.AssessStrength` with media-type impact multipliers (Video 1.6×, Physical 1.5×, Image 1.4×, Audio 1.3×, Document/Testimony 1.0×) and new `AssessProbativeValue` method accounting for reliability, direct/circumstantial classification, hearsay, and witness credibility. Added `CalculatePerJurorCredibility` and `CalculateMediaTypeSensitivity` to `JuryCalculationService` — each juror now assesses witness credibility and responds to evidence media types through their own demographic biases (education, age, politics, race/gender, attentiveness). Extended `MainViewModel` file type detection from Image/Document to full Video/Audio support. Updated file dialog filters in `MainWindow.xaml.cs` to include video and audio formats. Updated all help documentation and `docs/math.md` with mathematical formulas for probative weighting, credibility perception, and media sensitivity.
- **2026-05-27 (service extraction)**: Extracted `EvidenceProbativeService` (static, 117 lines) from `EvidenceAnalysisService` (597→500 lines) containing `GetMediaImpactMultiplier`, `DetermineEvidenceCategory`, and `AssessProbativeValue`. Extracted `JurorCredibilityService` (static, 150 lines) from `JuryCalculationService` (376→237 lines) containing `CalculatePerJurorCredibility` and `CalculateMediaTypeSensitivity`. Both new services are pure static utility classes with no dependencies, while the originals retain thin forwarding methods for backward compatibility.
- **2026-05-27 (default weights fix)**: Fixed `ModelWeightsWindow` to use research-backed defaults. Renamed "Reset All Weights" (set to 0) to "Research Defaults" (green button, sets research values from `docs/JurorBiasResearch.md`). Added separate "Zero All" button for the old behavior. Added missing `CommunicationStyleWeight` to all preset buttons (Balanced, Expert Focus, Diverse Perspective) and to `SyncDefaultBiasFactors()`. Added `Professional Background` entry to research doc table (0.55 weight). `DefaultBiasFactors` collection now stays in sync with slider values via `SyncDefaultBiasFactors()`.
- **2026-05-26**: Factored `Agent.cs` (~900 lines) into composable sub-objects. Extracted `AgentDemographics`, `BiasDimensionWeights`, and `AgentModelOverrides` as separate `ObservableObject` models. Extracted memory decay/fuzzification logic into `MemoryDecayService` (static service). `Agent.CopyTo` simplified using `CopyFrom` on each sub-object. All flat XAML-binding properties retained as delegation properties for backward compatibility. Added 4 new test suites in `ModelTests.cs`: `TestAgentDemographicsModel`, `TestBiasDimensionWeightsModel`, `TestAgentModelOverridesModel`, `TestMemoryDecayService`.
- **2026-05-01**: Split `Models.cs` into individual files in the `Models/` directory. Moved `MainViewModel.cs` to the `ViewModels/` directory and refactored to inherit from `ViewModelBase`. Extracted business logic from `MainViewModel` into `CaseService` and `TranscriptService` in the `Services/` directory. Introduced `ObservableObject` and `ViewModelBase` to standardize `INotifyPropertyChanged` implementation. Refactored `Agent` and `MemoryEntry` to use `SetProperty` for cleaner property definitions. **MainWindow.xaml**: Dynamic abstracted overhead courtroom view using `ItemsControl` for dynamic seating.
- **Post-2026-05-01**: Added 12 additional services (`CourtroomManagerService`, `EvidenceAnalysisService`, `DebateService`, `JuryCalculationService`, `CaseEntityMapper`, `AgentAssignmentService`, `JuryDemographicsService`, `AgentInteractionService`, `LegalDatabaseService`, `ReportGenerationService`, `ProviderDiscoveryService`, `SettingsService`). Added `CaseSettingsViewModel` and `ModelSettingsViewModel`. Added `DeepSeekProvider`. Added `ExtractedEntities` model. Added `LegalDatabase.json` resource. Added `Logger` service.
- **2026-05-15**: Added `AgentChatWindow` for LLM-powered conversation with any agent via right-click "Chat", with memory persistence tagged `Source = "Chat"` and memory wipe button. Added `JurorReportWindow` for right-click sentiment/verdict lean/memory report on juror seats. Added `ExhibitListWindow` accessible from Case menu (`_Exhibit List`) showing all evidence in a `ListView`. Added photo evidence support by extending file dialog filters to include image formats (`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`) with automatic `MediaType` classification (`"Image"` vs `"Document"`) in `EvidenceDocument`. Added `DetailedAnalysis` field to `EvidenceDocument` for full LLM analysis storage. Added `Stage` top-level menu with trial phase (Discovery/Pretrial/Trial) and court phase submenus. Added dynamic title bar via `WindowTitle` property on `MainViewModel` formatted as `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"`. Increased bench spacing (`Margin="12,0"`) on bench area `ContentPresenter` items. Removed redundant "Case loaded." message boxes. Fixed double LLM call in evidence analysis pipeline.
- **2026-05-09**: Added `AgentChatWindow` for LLM-powered conversation with any agent, including memory persistence via `MemoryEntry` objects tagged with `Source = "Chat"`. Added `JurorReportWindow` for viewing juror sentiment, verdict lean, and memory report from juror seat context menus. Added `ExhibitListWindow` accessible from the Case menu (`_Exhibit List`) showing all evidence in a `ListView`. Added `_Stage` menu to the main menu bar with trial phase (Discovery/Pretrial/Trial) and court phase (Opening Statements, Witness Testimony, Cross Examination, Party Statements, Closing Arguments, Jury Deliberation) submenus. Increased bench spacing by setting `Margin="12,0"` on bench area `ContentPresenter` items for better visual separation of Judge, Reporter, and Witness seats. Added photo evidence support by extending the evidence file dialog filter to include image formats (`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`) with automatic media type classification (`"Image"` vs `"Document"`) in `EvidenceDocument`. Added dynamic title bar via `WindowTitle` property on `MainViewModel` that formats as `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"` and updates on case load and phase changes.

## Build, Test, and Development Commands
- **Preferred Shell**: PowerShell
- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Test**: `dotnet run --project Tests/Tests.csproj`
- **Clean**: `dotnet clean`
    - **ViewModels**: Courtroom initialization and transcript processing logic.
## Test Harness
The project includes a custom test harness located in the `Tests/` directory. This is a console application that performs integration and unit tests on core components.

- **TestHarness.cs**: Contains the test logic for Models, Services, Providers, and ViewModels.
- **Coverage**:
    - **Models**: Sentiment analysis, verdict lean, and trial events.
    - **Services**: Transcript parsing, case persistence (JSON), and character management.
    - **Providers**: LLM provider discovery and configuration metadata.
    - **ViewModels**: Courtroom initialization and transcript processing logic.
- **Note**: LLM response generation tests are skipped by default as they require live API keys.
- **Character Profiles (.vcs)**: Verdict Character Settings. JSON files storing reusable agent profiles (prompts, memories, settings) in the `/Characters` repository.
### Adding New Tests
When adding new features, update `TestHarness.cs` with corresponding test methods:
1. Create a new `Test[Feature]` method.
2. Add it to the `RunTests` method.
3. Use `Console.WriteLine` for progress and throw an `Exception` on failure.

## Data Formats & Storage
- **Simulations (.jur)**: JSON-based case files storing simulation state and agent configurations.
- **Character Profiles (.vcs)**: Verdict Character Settings. JSON files storing reusable agent profiles (prompts, memories, settings) in the `/Characters` repository.
- **Assets**: Every simulation (`name.jur`) creates a sibling directory (`name_Assets/`) to store evidence, documents, and agent artifacts.

## Seating Conventions
- **Jury Box**: 12 primary seats + 2 alternate seats.
- **Observer Box**: Maximum 6 seats for gallery observation (4 observers + 2 client slots).
- **Counsel Tables**: Prosecution/Plaintiff (Right) and Defense (Left) tables.
- **Bench Area**: 1 Judge, 1 Reporter, 1 Witness Box. Bench seats have `Margin="12,0"` spacing for better visual separation.
- **Interactive Points**: 
    - Left-click "+" to add/activate agent (opens Agent Profile window).
    - Right-click for context menu (Edit Profile, Generate Likely Juror, Chat, Juror Report, Save as Character .VCS, Save to Casefile, Delete).
    - Right-click "Chat" opens `AgentChatWindow` with LLM-powered conversation and memory persistence (tagged `Source = "Chat"`). "Wipe Memories" button clears only chat memories.
    - Right-click "Juror Report" on juror/alternate seats opens `JurorReportWindow` showing sentiment, verdict lean, and memory report.
    - Central Podium button focuses the input prompt.
    - Generate Jury button populates jury box with demographically-generated jurors.
    - Admit Evidence button opens file dialog for document submission (supports PDF, TXT, DOCX, RTF, JPG, JPEG, PNG, GIF, BMP).
    - Clerk seat (green) accepts document drops for evidence submission.
- **Stage Menu**: The `_Stage` menu in the main menu bar provides trial phase (Discovery/Pretrial/Trial) and court phase (Opening Statements, Witness Testimony, Cross Examination, Party Statements, Closing Arguments, Jury Deliberation) submenus to control the current case stage.
- **Exhibit List**: Accessible from the `Case` menu via `_Exhibit List`, opens `ExhibitListWindow` showing all evidence in a `ListView`.
- **Dynamic Title Bar**: The window title updates dynamically to `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"` based on case name and trial phase.
2. Update `docs/JurorBiasResearch.md` if the change affects bias factor defaults or research citations.
## Coding Style
- C# 12+ features (Global Usings, File-scoped Namespaces).
- XAML styles are defined in `Window.Resources` for consistency.
- All UI-bound properties must trigger `OnPropertyChanged`.

## Math & Simulation Documentation

The juror opinion engine uses a sophisticated coherence + drift + conformity model with research-backed parameters. When modifying the math in `ToyJurorLogicEngine.cs`, `JuryCalculationService.cs`, or `JuryDemographicsService.cs`:
- Trait generation formulas from agent demographics

1. Update `docs/math.md` with any changes to formulas, coefficients, or pipeline stages.
- Trait generation formulas from agent demographics

2. Update `docs/JurorBiasResearch.md` if the change affects bias factor defaults or research citations.- Cross-factor interaction terms and their rationale

3. Update the corresponding help page in `Help/` if user-facing documentation is affected.- Coefficient tables with research sources

- Complete formulas for all three pipeline stages (static bias → evidence drift → deliberation)
The math document (`docs/math.md`) contains: