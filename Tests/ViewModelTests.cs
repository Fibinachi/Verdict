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
        TestAgentChatWindow();
        TestUniversalChatWindow();
        TestChatMemoryPersistence();

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

        Assert(vm.WindowTitle == "VERDICT", "Default window title is VERDICT");
        Assert(vm.CurrentDebateStageDisplay == "Opening Statements", "Default debate stage display");
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
        Assert(vm.CurrentDebateStage == CourtPhase.WitnessTestimony, "AdvanceDebateStage advances to witness testimony");

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
        Assert(vm.CaseFile.AvailableModels.Count >= 3, "CaseFile.AddDefaultModels initializes available models");
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

    private static void TestAgentChatWindow()
    {
        Console.WriteLine("─── AgentChatWindow ───");

        var vm = new MainViewModel();
        var agent = new Agent
        {
            Name = "Test Juror",
            Role = AgentRole.Juror,
            SystemPrompt = "You are a thoughtful juror.",
            Profile = "A careful evaluator of evidence."
        };

        // Verify the window can be instantiated
        var window = new Views.AgentChatWindow(vm, agent);
        Assert(window != null, "AgentChatWindow can be instantiated");
        Assert(window.Title == "Chat with Test Juror", "Window title includes agent name");

        window.Close();
    }

    private static void TestUniversalChatWindow()
    {
        Console.WriteLine("─── UniversalChatWindow ───");

        var vm = new MainViewModel();
        var window = new Views.UniversalChatWindow(vm);
        Assert(window != null, "UniversalChatWindow can be instantiated");

        window.Close();
    }

    private static void TestChatMemoryPersistence()
    {
        Console.WriteLine("─── Chat Memory Persistence ───");

        var agent = new Agent { Name = "Test Agent", Role = AgentRole.Juror };

        // Simulate adding chat memories
        var userMemory = new MemoryEntry
        {
            Content = "You said: What do you think about the evidence?",
            Timestamp = DateTime.Now,
            Source = "Chat",
            Strength = 1.0
        };
        var agentMemory = new MemoryEntry
        {
            Content = "Test Agent responded: The evidence seems credible.",
            Timestamp = DateTime.Now,
            Source = "Chat",
            Strength = 1.0
        };
        agent.Memories.Add(userMemory);
        agent.Memories.Add(agentMemory);

        Assert(agent.Memories.Count == 2, "Chat memories are added to agent");
        Assert(agent.Memories.Any(m => m.Source == "Chat"), "Chat source is preserved");

        // Simulate wiping chat memories
        var chatMemories = agent.Memories.Where(m => m.Source == "Chat").ToList();
        foreach (var memory in chatMemories)
        {
            agent.Memories.Remove(memory);
        }

        Assert(agent.Memories.Count == 0, "Chat memories are removed after wipe");
    }
}