# Default Cases (File menu) - Implementation Checklist

- [ ] Create `Services/BuiltInCaseScenarios.cs` with 3 scenario builders:
  - [ ] Rebranded O.J. Trial (e.g., criminal)
  - [ ] Rebranded Sacco & Vanzetti (e.g., criminal)
  - [ ] Rebranded Exxon Trial (e.g., civil)
- [ ] Each scenario must preload:
  - [ ] CaseFile party/attorney fields + jurisdiction + instructions/charges/causes where applicable
  - [ ] Evidence via `AddTestimonyEvidence`-style data (text-only) so no external files are required
- [ ] Update `Views/MainWindow.xaml` to add `_File -> _Default Cases` submenu.
- [ ] Update `Views/MainWindow.xaml.cs` with click handlers that:
  - [ ] Call `_viewModel.NewCase()`
  - [ ] Apply the selected scenario case file fields
  - [ ] Admit all scenario evidence/testimony
  - [ ] Generate jury
  - [ ] Refresh exhibits + update window title
- [ ] Build + run tests (`dotnet build`, `dotnet test`).

