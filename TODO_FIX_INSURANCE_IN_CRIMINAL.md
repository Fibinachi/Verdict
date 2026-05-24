# Fix: No “insurance guys” in criminal trial mode

- [x] Update `Services/CourtroomManagerService.cs` so `AgentRole.InsuranceAdjuster` (and related insurance/stakeholder observer slots) are not added when `caseData.Mode == CaseMode.Criminal`.
- [x] Update `ViewModels/MainViewModel.cs` so `UpdateInsuranceAdjusterState()` returns/sets null (and doesn’t compute offers/reserve UI) when `CurrentCase.Mode == CaseMode.Criminal`.

- [ ] (Optional) Update `EvidenceAnalysisService.CalculateExposure()` so it doesn’t inflate `InsuranceReserve` for criminal cases, if that value is only meaningful in civil.
- [ ] Build / run tests (`dotnet build`, test project if needed) and confirm criminal cases no longer show InsuranceAdjuster.
