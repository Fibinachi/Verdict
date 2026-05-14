# Technical Context

## Technologies Used
- **.NET 9** with WPF (Windows Presentation Foundation)
- **C# 12+** with file-scoped namespaces, global usings
- **MVVM Pattern** with INotifyPropertyChanged via ObservableObject base class
- **JSON Serialization** (System.Text.Json) for case files and settings
- **QuestPDF** (2026.2.4) for PDF report generation
- **Microsoft.Extensions.Http** (10.0.7) for HTTP client infrastructure

## Development Setup
- **IDE**: Visual Studio Code
- **OS**: Windows 11
- **SDK**: .NET 9
- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Test**: `dotnet run --project Tests/Tests.csproj`
- **Clean**: `dotnet clean`

## Project Structure
```
Verdict/
├── Models/           # 20 entity classes
├── ViewModels/       # MainViewModel, CaseSettingsViewModel, ModelSettingsViewModel
├── Views/            # XAML windows and user controls (10 files)
├── Services/         # 15 service classes + interfaces
├── Providers/        # 9 LLM provider implementations
├── Resources/        # LegalDatabase.json
├── Tests/            # TestHarness.cs, Program.cs
├── memory-bank/      # Project documentation
├── specs/            # Specification documents
└── docs/             # Additional documentation
```

## Technical Constraints
- **WPF only runs on Windows** - no cross-platform support
- **LLM providers require API keys** for cloud-based models
- **QuestPDF Community license** - free for non-commercial use
- **Static HttpClient instances** in provider implementations
- **No database** - file-based persistence (.jur, .vcs, .json)

## Dependencies
- `Microsoft.Extensions.Http` 10.0.7
- `QuestPDF` 2026.2.4
- `System.Text.Json` 10.0.7

## Tool Usage Patterns
- **ProviderDiscoveryService** uses reflection to find ILLMProviderModule implementations
- **CharacterManager** uses static methods for .vcs file operations
- **Logger** uses static methods with file locking
- **LegalDatabaseService** loads from JSON file with embedded fallback
- **JuryDemographicsService** uses weighted random selection for demographic generation

## LLM Provider Architecture
All providers implement `ILLMProviderModule` interface:
- `ProviderName` - Display name
- `Description` - Provider description
- `DefaultFriendlyName` - Default model name
- `ConfigFields` - Configuration fields (API key, endpoint, model ID)
- `TestConnectionAsync` - Test connectivity
- `GenerateResponseAsync` - Generate LLM response
- `GetAvailableModelsAsync` - List available models

Providers follow OpenAI-compatible API pattern where possible.
