# TODO: Disable debate stage / keep deliberation-only weighting

- [x] Lock `MainViewModel.CurrentDebateStage` to `CourtPhase.JuryDeliberation`
- [x] Remove transcript influence and any stage-triggered LLM responses
- [x] Disable court-phase UI handlers in `MainWindow.xaml.cs` (force deliberation)
- [x] Fix build error in `Views/ExhibitListWindow.xaml` — handler already exists and builds clean
- [x] `dotnet build` succeeds (577/577 tests passing)