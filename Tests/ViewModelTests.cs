using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.ViewModels;

namespace Verdict.Tests;

public class ViewModelTests : BaseTestClass
{
    public static async Task<(int passed, int failed, List<string> failures)> GetTestResults()
    {
        Console.WriteLine("\n=== VIEWMODEL TESTS ===");

        ResetCounters();
        
        TestMainViewModel();
        TestCaseSettingsViewModel();
        TestModelSettingsViewModel();

        return GetResults();
    }

    public static async Task RunAllTests()
    {
        var results = await GetTestResults();
        Console.WriteLine($"  VIEWMODEL TESTS RESULT: {results.passed} passed, {results.failed} failed");
    }

    private static void TestMainViewModel()
    {
        Console.WriteLine("\n─── MainViewModel ───");

        var vm = new MainViewModel();

        Assert(vm.WindowTitle.StartsWith("VERDICT"), "Default window title shows VERDICT prefix");
        Assert(vm.WindowTitle.Contains("v."), "Default window title contains 'v.'");
        Assert(vm.CurrentDebateStageDisplay == "Jury Deliberation", "Default debate stage is Jury Deliberation (stage UI disabled)");
        Assert(vm.Jurors.Count == 12, "Default courtroom has 12 jurors");
        Assert(vm.JudgeArea.Count == 3, "Default judge area has 3 slots");
        Assert(vm.DefenseTeam.Count == 1, "Default defense team has 1 lawyer");
        Assert(vm.ProsecutionTeam.Count == 1, "Default prosecution team has 1 lawyer");

        var updatedCase = new CaseFile
        {
            CaseName = "Alpha Case",
            TrialPhase = TrialPhase.Trial
        };
        updatedCase.Plaintiffs.Add("Alice");
        updatedCase.Defendants.Add("Bob");
        vm.CurrentCase = updatedCase;
        vm.UpdateWindowTitle();
        Assert(vm.WindowTitle == "VERDICT - Alice v. Bob - (TRIAL)", "UpdateWindowTitle formats title correctly");

        vm.AdvanceDebateStage();
        Assert(vm.CurrentDebateStage == CourtPhase.VerdictAnnouncement, "AdvanceDebateStage advances from JuryDeliberation to next phase");

        var juror = vm.GenerateSingleJuror("Richland County", "South Carolina");
        Assert(juror.Role == AgentRole.Juror, "GenerateSingleJuror returns a juror role");
        Assert(!string.IsNullOrWhiteSpace(juror.Name), "Generated juror has a name");

        vm.CurrentCase.Evidence.Add(new EvidenceDocument
        {
            FileName = "Exhibit1.txt",
            Summary = "A sample exhibit.",
            EstimatedDamages = 10000,
            EvidenceStrength = 0.75
        });
        vm.RefreshExhibits();
        Assert(vm.Exhibits.Count == 1, "RefreshExhibits syncs exhibits from the current case");

        var tempFile = Path.Combine(Path.GetTempPath(), $"verdict_test_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "Evidence content for testing.");
            var content = vm.ReadFileContent(tempFile);
            Assert(content == "Evidence content for testing.", "ReadFileContent returns text file contents");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }

        vm.CurrentCase.AvailableModels.Clear();
        var analysis = vm.GenerateDocumentAnalysisAsync("file.txt", "content", "summary").GetAwaiter().GetResult();
        Assert(!string.IsNullOrWhiteSpace(analysis.Analysis), "GenerateDocumentAnalysisAsync returns fallback analysis when no model configured");
    }

    private static void TestCaseSettingsViewModel()
    {
        Console.WriteLine("\n─── CaseSettingsViewModel ───");

        var caseFile = new CaseFile();
        var vm = new CaseSettingsViewModel(caseFile, true);

        Assert(vm.CaseFile != null, "CaseSettingsViewModel initializes CaseFile");
        Assert(vm.IsDefaultSettings, "IsDefaultSettings property preserves value");
        Assert(vm.CaseModes.Contains(CaseMode.Civil), "CaseModes includes Civil");
        Assert(vm.JurisdictionTypes.Contains(JurisdictionType.State), "JurisdictionTypes includes State");
        Assert(vm.TrialPhases.Contains(TrialPhase.Trial), "TrialPhases includes Trial");
        Assert(vm.CaseFile.AvailableModels.Count >= 1, "CaseFile.AddDefaultModels initializes at least 1 default model");
    }

    private static void TestModelSettingsViewModel()
    {
        Console.WriteLine("─── ModelSettingsViewModel ───");

        var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        Directory.CreateDirectory(folderPath);
        var tempModelPath = Path.Combine(folderPath, $"verdict_test_model_{Guid.NewGuid():N}.json");

        try
        {
            var modelConfig = new AIModelConfiguration
            {
                FriendlyName = "Test Folder Model",
                Provider = "OpenAI",
                ModelId = "verdict-test-model",
                Endpoint = "https://api.openai.com/v1",
                ApiKey = "sk-test"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(modelConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(tempModelPath, json);

            var vm = new ModelSettingsViewModel(new[]
            {
                new AIModelConfiguration { FriendlyName = "Existing Model", Provider = "OpenAI", ModelId = "existing-model" }
            });

            Assert(vm.Models.Any(m => m.ModelId == "verdict-test-model"), "LoadFromFolder imports models from the Models folder");

            vm.SelectedModel = vm.Models.First(m => m.ModelId == "verdict-test-model");
            vm.SaveSelectedToFolder();
            var savedPath = Path.Combine(folderPath, "verdict-test-model.json");
            Assert(File.Exists(savedPath), "SaveSelectedToFolder writes model file to Models folder");

            vm.DeleteModel();
            Assert(vm.SelectedModel == null, "DeleteModel clears selected model");
            Assert(!vm.Models.Any(m => m.ModelId == "verdict-test-model"), "DeleteModel removes the model from the Models collection");
            Assert(!File.Exists(savedPath), "DeleteModel removes the saved model file");
        }
        finally
        {
            if (File.Exists(tempModelPath))
                File.Delete(tempModelPath);
            var savedPath = Path.Combine(folderPath, "verdict-test-model.json");
            if (File.Exists(savedPath))
                File.Delete(savedPath);
        }
    }
}