# Verdict - Courtroom Simulation

A WPF desktop application for modeling jury verdict outcomes using AI agents. The application simulates a courtroom environment where AI-powered agents (jurors, judge, lawyers, witnesses) interact and form opinions based on trial events, evidence, and testimony.

## Overview

Verdict is a .NET 9 WPF application designed for native Windows deployment. It uses an MVVM architecture and supports multiple LLM providers (OpenAI, Anthropic, Google Gemini, etc.) to power AI agent behavior in a courtroom simulation.

### Key Features

- **Dynamic Courtroom Layout**: Full courtroom with Judge, Witness Stand, Court Reporter, Jury Box (12 seats in 3x4 formation), Counsel Tables, and Gallery
- **AI Agent System**: Configurable AI agents with demographic backgrounds, biases, sentiment tracking, and verdict leanings
- **Transcript Processing**: Load and process court transcripts that influence agent opinions
- **Evidence Management**: Admit evidence documents that create trial memories and shift agent opinions
- **Photo Evidence**: Admit and describe photo evidence through the clerk, with image file support (.jpg, .png, .gif, .bmp)
- **Exhibit List**: List view of all evidence with exhibit numbers, summaries, and clerk descriptions
- **Agent Chat**: Right-click chat with any agent using their configured LLM, with conversation memory and fallback role-based responses
- **Memory Wipe**: Wipe chat memories per-agent from the chat window
- **Counsel Discussion**: Simulate a sidebar discussion between defense and prosecution lawyers, with judge ruling
- **Client Consultation**: Privileged attorney-client conversation window with settlement authority tracking
- **Juror Report**: Right-click sentiment and memory report for jurors
- **Model Weights**: Configure bias factor weights (age, gender, education, income, political, ethnicity, religion, experience, legal knowledge, professional background, community ties) via the Model Weights window
- **Trial Stage Menu (Experimental)**: Top-level Stage menu for trial phase (Discovery, Pretrial, Trial) and court phase selection. *Note: Current implementation is limited/placeholder and may not fully support all court phases.*
- **Dynamic Title Bar**: Title shows case name, parties, and trial stage (e.g., "VERDICT - Plaintiff v. Defendant - (TRIAL)").
- **Multi-Provider Support**: Connect to various LLM providers (OpenAI, Anthropic, Google Gemini, Ollama, DeepSeek, Grok, etc.)
- **Case File System**: Save/load simulations (.jur files) with full state preservation
- **Character Profiles**: Save reusable agent configurations as .vcs files
- **PDF Reports**: (Implemented via `ReportGenerationService`—see `Help/case-management.html` / Help pages for current workflow)

## Related Research (2025–2026)

The following works are referenced by the simulation’s multi-agent deliberation and diversity modeling approach:

- Devadiga, P., Shetty, O. J., & Agarwal, P. (2025). *SAMVAD: A Multi-Agent System for Simulating Judicial Deliberation Dynamics in India.* arXiv preprint arXiv:2509.03793. https://doi.org/10.48550/arxiv.2509.03793 (Cited by: 2)
- Grim, P., Singer, D. J., Bramson, A., Holman, B., Jung, J., & Berger, W. J. (2024). *The Epistemic Role of Diversity in Juries: An Agent-Based Model.* Journal of Artificial Societies and Social Simulation, 27(1), 12. https://doi.org/10.18564/jasss.5304 (Cited by: 8)
- Jiang, C., & Yang, X. (2024). *Agents on the Bench: Large Language Model Based Multi Agent Framework for Trustworthy Digital Justice.* arXiv preprint arXiv:2412.18697. https://doi.org/10.48550/arxiv.2412.18697 (Cited by: 14)
- Anonymous. (2026). *12 Angry AI Agents: Evaluating Multi-Agent LLM Decision-Making Through Cinematic Jury Deliberation.* arXiv preprint arXiv:2605.01986. https://doi.org/10.48550/arxiv.2605.01986 (Cited by: 1)


## Feature Roadmap

- **Full Court Phase Integration**: Moving beyond Deliberation-only logic to implement LLM-driven behaviors for Opening Statements and Witness Testimony.
- **Advanced Agent Management**: A dedicated repository for building, batch-updating, and loading agent profiles.
- **Legal Instruction Logic**: Dynamically adjusting juror "Rigidity" and "Hardness" based on specific judicial instructions.
- **Extended Testing**: Achieving 100% coverage across all 25 business logic services.

- **Dynamic Role Assignment**: Automatically assign agents to recommended roles based on case analysis
- **Logger**: File-based logging to `VerdictSimulation.log` in the application directory

## Architecture

```raw
Verdict/
├── Models/           # Data entities (Agent, CaseFile, EvidenceDocument, etc.)
├── ViewModels/       # MVVM ViewModels (MainViewModel, CaseSettingsViewModel, ModelSettingsViewModel)
├── Views/            # XAML Windows and UserControls
├── Services/         # Business logic — 25 dedicated services
├── Providers/        # LLM provider implementations
├── Resources/        # LegalDatabase.json
├── Help/             # HTML help pages
└── Tests/            # Console test harness
```

### Core Models

| Model | Purpose |
|-------|---------|
| `Agent` | Represents an individual in the courtroom (juror, judge, lawyer, etc.) with personality, memories, and verdict lean |
| `CaseFile` | Complete simulation state including agents, evidence, event log, and legal framework |
| `MemoryEntry` | A single memory or trial observation with timestamp and strength |
| `CourtroomEvent` | An event that occurred in the courtroom, visible to specific roles |
| `EvidenceDocument` | Evidence admitted into the case with summary and detailed LLM analysis |
| `AIModelConfiguration` | LLM provider configuration (API key, model ID, endpoint) |
| `JuryInstructions` | Legal instructions given to the jury |
| `VerdictForm` | The verdict form for recording jury decisions |
| `ExtractedEntities` | Entities extracted from transcripts (charges, witnesses, evidence) |
| `AgentInteraction` | Agent messages, legal citations, and interaction results |
| `Charge` / `CauseOfAction` / `Element` | Legal charge modeling |
| `BiasFactor` | Demographic bias factor with configurable weight |
| `EvidenceCueProfile` | Structured demographic and political cues extracted from evidence text |
| `CaseTypeSensitivityMatrix` | Case type-specific predictor multipliers for verdict drift calculation |
| `JurorOpinion` | Individual juror's verdict lean and confidence score |
| `DeliberationEntry` | A single statement or exchange made during jury deliberation |
| `InsuranceAdjusterState` | Estimated plaintiff verdict probability for insurance reserve evaluation |

### Agent Roles

```csharp
public enum AgentRole
{
    Judge,           // Presides over the trial
    Witness,         // Provides testimony
    Reporter,        // Records proceedings
    Juror,           // Decides the verdict (12 seats)
    AlternateJuror,  // Replacement juror (2 seats)
    Lawyer,          // Represents prosecution or defense
    Client,          // The plaintiff or defendant
    Observer         // Gallery observer
}
```

### Service Architecture

The application follows a **delegated service pattern** — `MainViewModel` owns the agent collections and case state, but delegates all domain logic to dedicated injected services.

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `CaseService` | `ICaseService` | Save/load .jur case files; generate PDF reports |
| `TranscriptService` | `ITranscriptService` | Load/parse transcript files; LLM entity extraction |
| `SettingsService` | `ISettingsService` | Default case settings persistence |
| `CourtroomManagerService` | `ICourtroomManagerService` | Courtroom initialization, slot occupancy, agent reset |
| `EvidenceAnalysisService` | `IEvidenceAnalysisService` | Evidence strength assessment, damage estimation, exposure calculation, LLM document analysis |
| `DebateService` | `IDebateService` | Stage advancement, influence calculation, event broadcasting |
| `JuryCalculationService` | `IJuryCalculationService` | Average lean, likely verdict string, opinion influence logic |
| `CaseEntityMapper` | `ICaseEntityMapper` | Maps extracted entities (charges/evidence/witnesses) onto CaseFile |
| `AgentAssignmentService` | `IAgentAssignmentService` | Case analysis, dynamic role assignment, default agent generation |
| `JuryDemographicsService` | `IJuryDemographicsService` | Generates jury panels based on county/state demographics |
| `AgentInteractionService` | `IAgentInteractionService` | Manages agent statements, examination, deliberation, court record internalization |
| `ChatOrchestratorService` | `IChatOrchestratorService` | Coordinates stage-appropriate chat interactions with agents; manages broadcasting |
| `EvidenceAdmissionService` | `IEvidenceAdmissionService` | Evidence file admission and testimony submission workflows |
| `DeliberationService` | `IDeliberationService` | Jury deliberation management and verdict calculation |
| `CharacterManager` | — | Save/load .vcs character profiles |
| `LegalDatabaseService` | — | Query legal citations (IPC sections, U.S. Federal Law) |
| `ReportGenerationService` | `IReportGenerationService` | PDF report rendering with QuestPDF |
| `ProviderDiscoveryService` | — | Discovers installed LLM providers and their metadata |
| `ModelDownloadService` | — | Downloads ONNX models from HuggingFace for local inference |
| `Logger` | — | Static file-based logger writing to `VerdictSimulation.log` |
| `CaseTypeSensitivityService` | — | Case type-specific juror sensitivity modeling for verdict drift |
| `AgentDemographicsGeneratorService` | `IAgentDemographicsGeneratorService` | Generates demographically-representative agents by role |
| `EvidenceCueExtractor` | — | Extracts demographic and political cues from evidence text |
| `InsuranceAdjusterPricingService` | `IInsuranceAdjusterPricingService` | Estimates plaintiff-favorable verdict probability for reserve estimation |
| `ConversationRestrictionPolicy` | — | Enforces courtroom communication rules based on agent roles and context |

### Views

| Window | Purpose |
|--------|---------|
| `MainWindow` | Primary courtroom UI |
| `AgentProfileWindow` | Edit agent demographics, personality, and model settings |
| `AgentChatWindow` | LLM-powered chat with any agent; memory persistence and wipe |
| `UniversalChatWindow` | Unified agent communication and testimony admission interface |
| `CaseSettingsWindow` | Edit case name, parties, jurisdiction, and legal framework |
| `ModelSettingsWindow` | Manage LLM provider configurations |
| `ModelWeightsWindow` | Configure bias factor weights for jury modeling |
| `JurorReportWindow` | Sentiment, verdict lean, and memory report for a juror |
| `JurorConversationWindow` | Targeted questioning of a juror |
| `CounselDiscussionWindow` | Sidebar discussion between defense and prosecution with judge ruling |
| `ClientConversationWindow` | Privileged attorney-client consultation |
| `ExhibitListWindow` | Grid view of all admitted evidence |
| `AddModelDialog` | Add a new LLM model configuration |
| `ModelSelectionDialog` | Select a model from available configurations |
| `ModelDetailsDialog` | View detailed model configuration |

### LLM Providers

| Provider | API Key Link | Models Supported |
|----------|-------------|-------------------|
| **OpenAI** | [platform.openai.com/api-keys](https://platform.openai.com/api-keys) | GPT-4o, GPT-4 Turbo, GPT-4 |
| **Anthropic** | [console.anthropic.com/api-keys](https://console.anthropic.com/api-keys) | Claude 3.5 Sonnet, Claude 3 Opus |
| **Google Gemini** | [aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey) | Gemini 1.5 Pro, Gemini 1.5 Flash |
| **Ollama** | [ollama.com](https://ollama.com/download) | Llama 3, Mistral, Phi3 (local) |
| **Hugging Face (GGUF)** | [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) | GGUF models via LLamaSharp |
| **ONNX** | [huggingface.co/models](https://huggingface.co/models?library=onnx) | ONNX models via Microsoft.ML.OnnxRuntime |
| **Alibaba (Qwen)** | [dashscope.console.aliyun.com](https://dashscope.console.aliyun.com) | Qwen 2, Qwen 1.5 |
| **NVIDIA NIM** | [build.nvidia.com](https://build.nvidia.com) | Mixtral, Llama 3 (via NIM) |
| **Intel Gaudi** | [vault.habana.ai](https://vault.habana.ai) | Gaudi-accelerated models |
| **DeepSeek** | [platform.deepseek.com](https://platform.deepseek.com) | DeepSeek-V3, DeepSeek-R1 |
| **Grok (xAI)** | [console.x.ai](https://console.x.ai) | Grok Beta |

#### Quick Start - Adding a Model

1. Click **"Add Model"** button in the Models panel
2. Select your provider from the dropdown
3. Enter your API key from the link above
4. Choose a model ID (e.g., `gpt-4o`, `claude-3-5-sonnet-20240620`)
5. Click **"Test Connection"** to verify
6. Click **"Save"** to add to your available models

## Test Harness

The project includes a comprehensive test harness in the `Tests/` directory — a console application that performs unit and integration tests on all core components.

### Architecture

| File | Type | Purpose |
|------|------|---------|
| `Program.cs` | Entry Point | Launches the test harness |
| `BaseTestClass.cs` | Base Class | Shared `Assert()` helper + pass/fail counters |
| `TestHarness.cs` | Legacy Runner | Original monolithic test runner (still functional) |
| `TestRunner.cs` | New Runner | Aggregates results from ModelTests + ServiceTests + ProviderTests + ViewModelTests |
| `ModelTests.cs` | Test Suite | 12 tests covering all 20 model classes |
| `ServiceTests.cs` | Test Suite | 15 tests covering all services (SettingsService has full round-trip tests) |
| `ProviderTests.cs` | Test Suite | 8 tests covering provider discovery + OnnxProvider + HuggingFaceProvider |
| `ViewModelTests.cs` | Test Suite | 3 placeholder tests for MainViewModel, CaseSettingsViewModel, ModelSettingsViewModel |

### Running Tests

```bash
dotnet run --project Tests/Tests.csproj
```

**Note**: LLM response generation tests are skipped by default as they require live API keys.

## Agent System

### Verdict Lean System

Each agent with voting capability (`HasOpinion`) tracks a `VerdictLean` value:
- `0.0` = Strongly favors Defendant
- `0.5` = Undecided
- `1.0` = Strongly favors Prosecution/Plaintiff

The lean updates based on transcript influence, admitted evidence, and peer influence during deliberation.

### Memory System

Agents maintain two types of memories:
- **Memories**: Background life experiences, identity traits, and chat/consultation exchanges (tagged by `Source`: `"Chat"`, `"CounselDiscussion"`, `"ClientConsultation"`)
- **TrialEvents**: Specific observations during the current trial

Both use `MemoryEntry` with `Content`, `Timestamp`, `Strength`, `Source`, and `IsFromDocument`.

### Sentiment Analysis

Heuristic sentiment analysis on trial content:
- Objection/Overruled → Negative shift
- Sustained → Positive shift
- Lied/False/Inconsistent → Significant negative shift
- Expert/DNA/Fact → Positive shift

### Bias System

Each agent has a `Bias` value (-1.0 to 1.0) that amplifies emotional responses to bias-confirming information and modifies verdict lean updates. Bias factor weights are configurable per demographic dimension via the Model Weights window.

## Data Formats

### Case Files (.jur)

JSON files storing complete simulation state:
```json
{
  "CaseName": "Sample Case",
  "CaseNumber": "23-CV-1234",
  "CourtName": "Superior Court",
  "Mode": "Civil",
  "Jurisdiction": "State",
  "JurorCount": 12,
  "Agents": [...],
  "Evidence": [...],
  "EventLog": [...],
  "Instructions": {...},
  "Verdict": {...}
}
```

### Character Profiles (.vcs)

Verdict Character Settings — reusable agent configurations stored in `/Characters` directory.

### Assets

Each simulation creates a sibling directory (`name_Assets/`) containing evidence documents and agent artifacts.

## Build and Development

### Prerequisites

- .NET 9 SDK
- Windows 11 (for WPF)

### Commands

```bash
dotnet build
dotnet run
dotnet run --project Tests/Tests.csproj
dotnet clean
```

## Courtroom Layout

```raw
+--------------------------------------------------+
|              GALLERY (4 obs + 2 clients)          |
|  +----+----+----+----+----+----+----+----+----+  |
|  |Alt1|Alt2|Obs1|Obs2|Obs3|Obs4|Plnt|DefC|Clie|  |
|  +----+----+----+----+----+----+----+----+----+  |
|                                                   |
|  +----------+                         +----------+|
|  | JURY BOX |   [WITNESS] [COURT      |          ||
|  | 3x4      |    STAND    REPORTER]   | OBSERVER ||
|  | Jurors   |                         |    6     ||
|  +----------+                         +----------+|
|                                                   |
|  [DEFENSE TABLE]      [PODIUM]     [PROSECUTION]  |
|  Lawyer + Client                  Lawyer + Client |
|                                                   |
|                   [ JUDGE ]                       |
+--------------------------------------------------+
```

### Seating Capacity

- **Judge Area**: 1 Judge, 1 Witness, 1 Reporter
- **Jury Box**: 12 Jurors (3x4 formation) + 2 Alternate Jurors
- **Gallery**: 4 Observers + 2 Client slots
- **Counsel Tables**: Defense (Left) and Prosecution/Plaintiff (Right)

## Court Phases

```csharp
public enum CourtPhase
{
    CaseGeneration,
    CourtOpening,
    LegalResearch,
    OpeningStatements,
    WitnessTestimony,
    CrossExamination,
    PartyStatements,
    ClosingArguments,
    JuryInstructions,
    JuryDeliberation,
    VerdictAnnouncement,
    CourtClosing
}
```

Use the **Stage** menu to switch between trial phases (Discovery, Pretrial, Trial) and select a court phase.

> **Experimental note (court phases):** The stage menu currently routes behavior in only limited/placeholder ways; parts of the pipeline (e.g., evidence admission and juror internalization) are driven by the active **trial phase** and the jury deliberation engine.



## License

This project is for demonstration and research purposes.
