# Repository Guidelines

## Project Structure & Module Organization
Verdict is a WPF application (.NET 9) designed for native Windows deployment. The architecture follows a strict MVVM pattern.

- **Models/**: Individual class files for all entities (`Agent.cs`, `CaseFile.cs`, `EvidenceDocument.cs`, `MemoryEntry.cs`, `CourtroomEvent.cs`, `AIModelConfiguration.cs`, `JuryInstructions.cs`, `VerdictForm.cs`, `ExtractedEntities.cs`, `AgentInteraction.cs`, `Charge.cs`, `CauseOfAction.cs`, `Element.cs`, `LegalStandard.cs`, `CaseMode.cs`, `JurisdictionType.cs`, `TrialPhase.cs`, `CourtPhase.cs`, `AgentRole.cs`, `ObservableObject.cs`).
- **ViewModels/**: Contains `MainViewModel.cs`, `CaseSettingsViewModel.cs`, `ModelSettingsViewModel.cs`, and `ViewModelBase.cs`.
- **Views/**: Contains XAML windows and user controls (`AgentProfileWindow.xaml`, `CaseSettingsWindow.xaml`, `ModelSettingsWindow.xaml`, `CounselDiscussionWindow.xaml`, `ClientConversationWindow.xaml`, `AddModelDialog.xaml`, `GoogleModelSelectionDialog.xaml`, `ModelDetailsDialog.xaml`, `UniversalChatWindow.xaml`, `JurorReportWindow.xaml`, `ExhibitListWindow.xaml`, `JurorConversationWindow.xaml`).
- **Services/**: Contains 25 services for business logic (see Service Architecture table in README.md).
- **Providers/**: Contains LLM provider implementations (`OpenAIProvider.cs`, `AnthropicProvider.cs`, `GoogleGeminiProvider.cs`, `OllamaProvider.cs`, `HuggingFaceProvider.cs`, `AlibabaProvider.cs`, `NvidiaProvider.cs`, `IntelProvider.cs`, `DeepSeekProvider.cs`).

## Refactoring Log
- **2026-05-01**: Split `Models.cs` into individual files in the `Models/` directory. Moved `MainViewModel.cs` to the `ViewModels/` directory and refactored to inherit from `ViewModelBase`. Extracted business logic from `MainViewModel` into `CaseService` and `TranscriptService` in the `Services/` directory. Introduced `ObservableObject` and `ViewModelBase` to standardize `INotifyPropertyChanged` implementation. Refactored `Agent` and `MemoryEntry` to use `SetProperty` for cleaner property definitions. **MainWindow.xaml**: Dynamic abstracted overhead courtroom view using `ItemsControl` for dynamic seating.
- **Post-2026-05-01**: Added 12 additional services (`CourtroomManagerService`, `EvidenceAnalysisService`, `DebateService`, `JuryCalculationService`, `CaseEntityMapper`, `AgentAssignmentService`, `JuryDemographicsService`, `AgentInteractionService`, `LegalDatabaseService`, `ReportGenerationService`, `ProviderDiscoveryService`, `SettingsService`). Added `CaseSettingsViewModel` and `ModelSettingsViewModel`. Added `DeepSeekProvider`. Added `ExtractedEntities` model. Added `LegalDatabase.json` resource. Added `Logger` service.
- **2026-05-15**: Added `AgentChatWindow` for LLM-powered conversation with any agent via right-click "Chat", with memory persistence tagged `Source = "Chat"` and memory wipe button. Added `JurorReportWindow` for right-click sentiment/verdict lean/memory report on juror seats. Added `ExhibitListWindow` accessible from Case menu (`_Exhibit List`) showing all evidence in a `ListView`. Added photo evidence support by extending file dialog filters to include image formats (`.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`) with automatic `MediaType` classification (`"Image"` vs `"Document"`) in `EvidenceDocument`. Added `DetailedAnalysis` field to `EvidenceDocument` for full LLM analysis storage. Added `Stage` top-level menu with trial phase (Discovery/Pretrial/Trial) and court phase submenus. Added dynamic title bar via `WindowTitle` property on `MainViewModel` formatted as `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"`. Increased bench spacing (`Margin="12,0"`) on bench area `ContentPresenter` items. Removed redundant "Case loaded." message boxes. Fixed double LLM call in evidence analysis pipeline.
- **Post-2026-05-15**: Added `ChatOrchestratorService` to coordinate stage-appropriate chat interactions. Added `EvidenceAdmissionService` for testimony and evidence file admission workflows. Added `DeliberationService` for jury deliberation management. Added `CaseTypeSensitivityService` for case type-specific juror sensitivity modeling. Added `AgentDemographicsGeneratorService` for role-based agent demographic generation. Added `EvidenceCueExtractor` for demographic cue extraction from evidence text. Added `InsuranceAdjusterPricingService` for reserve estimation. Added `ConversationRestrictionPolicy` for courtroom communication rules. Added `UniversalChatWindow` for unified agent communication and testimony admission interface.

## Build, Test, and Development Commands
- **Preferred Shell**: PowerShell
- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Test**: `dotnet run --project Tests/Tests.csproj`
- **Clean**: `dotnet clean`

## Test Harness
The project includes a custom test harness located in the `Tests/` directory. This is a console application that performs integration and unit tests on core components.

- **TestHarness.cs**: Contains the test logic for Models, Services, Providers, and ViewModels.
- **Coverage**:
    - **Models**: Sentiment analysis, verdict lean, and trial events.
    - **Services**: Transcript parsing, case persistence (JSON), and character management.
    - **Providers**: LLM provider discovery and configuration metadata.
    - **ViewModels**: Courtroom initialization and transcript processing logic.
- **Note**: LLM response generation tests are skipped by default as they require live API keys.

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
    - **Stage Menu**: The `_Stage` menu in the main menu bar provides trial phase (Discovery/Pretrial/Trial) selection. Court phase menus (Opening Statements, Witness Testimony, Cross Examination, Party Statements, Closing Arguments, Jury Deliberation) are currently **developmental placeholders** - only Jury Deliberation is an active stage for user interaction. The other court phases require additional LLM-driven agent behavior implementation before they become functional.

- **Exhibit List**: Accessible from the `Case` menu via `_Exhibit List`, opens `ExhibitListWindow` showing all evidence in a `ListView`.
- **Dynamic Title Bar**: The window title updates dynamically to `"VERDICT - Plaintiff v. Defendant - (CASE STAGE)"` based on case name and trial phase.

## Coding Style
- C# 12+ features (Global Usings, File-scoped Namespaces).
- XAML styles are defined in `Window.Resources` for consistency.
- All UI-bound properties must trigger `OnPropertyChanged`.

## Math & Simulation Documentation
The juror opinion engine uses a sophisticated coherence + drift + conformity model with research-backed parameters. When modifying the math in `Services/JOEService.*.cs`, adjusting tunable weights in `Resources/ToyJurorLogicCoefs.json`, or updating `JuryCalculationService.cs` or `JuryDemographicsService.cs`:
1. Update `docs/math.md` with any changes to formulas, coefficients, or pipeline stages.
2. Update `docs/JurorBiasResearch.md` if the change affects bias factor defaults or research citations.
3. Update the corresponding help page in `Help/` if user-facing documentation is affected.

The math document (`docs/math.md`) contains:
- Complete formulas for all three pipeline stages (static bias → evidence drift → deliberation)
- Coefficient tables with research sources
- Cross-factor interaction terms and their rationale
- Trait generation formulas from agent demographics
