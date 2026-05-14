# TODO: Disable debate stage / keep deliberation-only weighting

- [x] Lock `MainViewModel.CurrentDebateStage` to `CourtPhase.JuryDeliberation`
- [x] Remove transcript influence and any stage-triggered LLM responses
- [x] Disable court-phase UI handlers in `MainWindow.xaml.cs` (force deliberation)
- [ ] Fix build error in `Views/ExhibitListWindow.xaml` by implementing missing `ExhibitListItem_MouseDoubleClick` handler
- [ ] Re-run `dotnet build`

