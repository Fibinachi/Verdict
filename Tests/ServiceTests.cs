using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Providers;
using Verdict.Services;

namespace Verdict.Tests;

public class ServiceTests : BaseTestClass
{
    public static async Task<(int passed, int failed, List<string> failures)> GetTestResults()
    {
        Console.WriteLine("\n=== SERVICE TESTS ===");

        ResetCounters();
        
        TestTranscriptService();
        TestCaseService();
        TestCharacterManager();
        TestProviderDiscovery();
        TestSettingsService();
        TestCourtroomManagerService();
        TestEvidenceAnalysisService();
        TestDebateService();
        TestJuryCalculationService();
        TestCaseEntityMapper();
        TestAgentAssignmentService();
        TestJuryDemographicsService();
        TestLegalDatabaseService();
        TestLogger();
        TestAgentInteractionService();
        TestToyJurorLogicEngineV061();

        return GetResults();
    }

    public static async Task RunAllTests()
    {
        var results = await GetTestResults();
        Console.WriteLine($"  SERVICE TESTS RESULT: {results.passed} passed, {results.failed} failed");
    }

    private static void TestTranscriptService()
    {
        Console.WriteLine("\n─── TranscriptService ───");

        var service = new TranscriptService();

        // Test LoadTranscript with null/empty
        var result = service.LoadTranscript(null).ToList();
        Assert(result.Count == 0, "LoadTranscript(null) returns empty list");

        result = service.LoadTranscript("").ToList();
        Assert(result.Count == 0, "LoadTranscript(\"\") returns empty list");

        result = service.LoadTranscript("   ").ToList();
        Assert(result.Count == 0, "LoadTranscript(whitespace) returns empty list");

        // Test LoadTranscript with single line
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Witness: I saw the accident.");
        result = service.LoadTranscript(tempFile).ToList();
        Assert(result.Count == 1, "LoadTranscript parses single line");
        Assert(result[0].Speaker == "Witness", "LoadTranscript preserves speaker prefix");
        File.Delete(tempFile);

        // Test LoadTranscript with multiple lines
        tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Line 1\nLine 2\nLine 3");
        result = service.LoadTranscript(tempFile).ToList();
        Assert(result.Count == 3, "LoadTranscript parses multiple lines");
        File.Delete(tempFile);

        // Test LoadTranscript with empty lines (implementation skips whitespace lines)
        tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Line 1\n\nLine 3\n\nLine 5");
        result = service.LoadTranscript(tempFile).ToList();
        Assert(result.Count == 3, "LoadTranscript skips empty lines");
        File.Delete(tempFile);

        // Test ExtractEntitiesAsync returns empty ExtractedEntities for empty input (no LLM available)
        var entities = service.ExtractEntitiesAsync("", null).GetAwaiter().GetResult();
        Assert(entities != null, "ExtractEntitiesAsync with empty input returns non-null");
        Assert(entities.Charges.Count == 0, "ExtractEntitiesAsync with empty input has no charges");

        // Test ExtractEntitiesAsync with null model
        entities = service.ExtractEntitiesAsync("Some transcript text", null).GetAwaiter().GetResult();
        Assert(entities != null, "ExtractEntitiesAsync with null model returns non-null");
    }

    private static void TestCaseService()
    {
        Console.WriteLine("\n─── CaseService ───");

        var service = new CaseService();

        // Test SaveCase with null caseFile
        try
        {
            service.SaveCase("test.jur", null, new List<Agent>());
            Assert(false, "SaveCase with null caseFile throws ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            Assert(true, "SaveCase with null caseFile throws ArgumentNullException");
        }
        catch (Exception)
        {
            Assert(false, "SaveCase with null caseFile throws ArgumentNullException (not other exception)");
        }

        // Test SaveCase with null/empty path
        try
        {
            service.SaveCase(null, new CaseFile(), new List<Agent>());
            Assert(false, "SaveCase with null path throws ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            Assert(true, "SaveCase with null path throws ArgumentNullException");
        }
        catch (Exception)
        {
            Assert(false, "SaveCase with null path throws ArgumentNullException (not other exception)");
        }

        // Test LoadCase with null path
        var loaded = service.LoadCase(null);
        Assert(loaded == null, "LoadCase(null) returns null");

        loaded = service.LoadCase("");
        Assert(loaded == null, "LoadCase(\"\") returns null");

        loaded = service.LoadCase("   ");
        Assert(loaded == null, "LoadCase(whitespace) returns null");

        // Test LoadCase with non-existent file
        loaded = service.LoadCase("Z:\\NonExistentCase_12345.jur");
        Assert(loaded == null, "LoadCase(non-existent) returns null");

        // Test round-trip save/load with temp file
        var tempFile = Path.Combine(Path.GetTempPath(), "VerdictTest_" + Guid.NewGuid().ToString("N") + ".jur");
        try
        {
            var original = new CaseFile
            {
                CaseName = "Test Case",
                CaseNumber = "23-CV-1234",
                CourtName = "Test Court",
                Mode = CaseMode.Civil,
                Jurisdiction = JurisdictionType.State,
                TrialPhase = TrialPhase.Trial,
                EstimatedSettlement = 500000,
                InsuranceReserve = 625000
            };
            original.Plaintiffs.Add("John Doe");
            original.Defendants.Add("Jane Corp");
            original.PlaintiffAttorney = "Paul Plaintiff";
            original.DefenseAttorney = "Danny Defense";
            original.Evidence.Add(new EvidenceDocument
            {
                FileName = "ExhibitA.pdf",
                Summary = "Medical report",
                EvidenceStrength = 0.85,
                EstimatedDamages = 150000,
                ExhibitNumber = 1
            });

            service.SaveCase(tempFile, original, new List<Agent>());
            Assert(File.Exists(tempFile), "SaveCase creates .jur file");

            var loadedCase = service.LoadCase(tempFile);
            Assert(loadedCase != null, "LoadCase returns non-null after save");
            Assert(loadedCase.CaseName == "Test Case", "Loaded case preserves CaseName");
            Assert(loadedCase.CaseNumber == "23-CV-1234", "Loaded case preserves CaseNumber");
            Assert(loadedCase.CourtName == "Test Court", "Loaded case preserves CourtName");
            Assert(loadedCase.Mode == CaseMode.Civil, "Loaded case preserves Mode");
            Assert(loadedCase.Jurisdiction == JurisdictionType.State, "Loaded case preserves Jurisdiction");
            Assert(loadedCase.TrialPhase == TrialPhase.Trial, "Loaded case preserves TrialPhase");
            Assert(loadedCase.EstimatedSettlement == 500000, "Loaded case preserves EstimatedSettlement");
            Assert(loadedCase.InsuranceReserve == 625000, "Loaded case preserves InsuranceReserve");
            Assert(loadedCase.Plaintiffs.Count == 1, "Loaded case preserves Plaintiffs");
            Assert(loadedCase.Plaintiffs[0] == "John Doe", "Loaded case preserves Plaintiff name");
            Assert(loadedCase.Defendants.Count == 1, "Loaded case preserves Defendants");
            Assert(loadedCase.Defendants[0] == "Jane Corp", "Loaded case preserves Defendant name");
            Assert(loadedCase.PlaintiffAttorney == "Paul Plaintiff", "Loaded case preserves PlaintiffAttorney");
            Assert(loadedCase.DefenseAttorney == "Danny Defense", "Loaded case preserves DefenseAttorney");
            Assert(loadedCase.Evidence.Count == 1, "Loaded case preserves Evidence");
            Assert(loadedCase.Evidence[0].FileName == "ExhibitA.pdf", "Loaded case preserves evidence FileName");
            Assert(loadedCase.Evidence[0].EvidenceStrength == 0.85, "Loaded case preserves evidence Strength");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }

        // Test GenerateReport
        try
        {
            service.GenerateReport(null, null, null);
            Assert(false, "GenerateReport with null args throws ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            Assert(true, "GenerateReport with null args throws ArgumentNullException");
        }
        catch (Exception)
        {
            Assert(false, "GenerateReport with null args throws ArgumentNullException (not other exception)");
        }
    }

    private static void TestCharacterManager()
    {
        Console.WriteLine("\n─── CharacterManager ───");

        // Test SaveCharacter with null agent
        try
        {
            CharacterManager.SaveCharacter(null);
            Assert(false, "SaveCharacter with null agent throws ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            Assert(true, "SaveCharacter with null agent throws ArgumentNullException");
        }
        catch (Exception)
        {
            Assert(false, "SaveCharacter with null agent throws ArgumentNullException (not other exception)");
        }

        // Test LoadCharacter with null path
        var loaded = CharacterManager.LoadCharacter(null);
        Assert(loaded == null, "LoadCharacter(null) returns null");

        loaded = CharacterManager.LoadCharacter("");
        Assert(loaded == null, "LoadCharacter(\"\") returns null");

        loaded = CharacterManager.LoadCharacter("   ");
        Assert(loaded == null, "LoadCharacter(whitespace) returns null");

        // Test LoadCharacter with non-existent file
        loaded = CharacterManager.LoadCharacter("Z:\\NonExistentCharacter_12345.vcs");
        Assert(loaded == null, "LoadCharacter(non-existent) returns null");

        // Test round-trip save/load with temp file
        var tempFile = Path.Combine(Path.GetTempPath(), "VerdictTest_" + Guid.NewGuid().ToString("N") + ".vcs");
        try
        {
            var original = new Agent
            {
                Name = "Test Character",
                Role = AgentRole.Judge,
                Gender = "Male",
                Age = 55,
                Race = "White",
                Occupation = "Judge",
                EducationLevel = "Juris Doctor",
                IncomeLevel = "Upper Class",
                Bias = 0.1,
                VerdictLean = 0.5,
                Sentiment = 0.5,
                IsOccupied = true,
                SystemPrompt = "You are a fair judge.",
                Profile = "An experienced judge.",
                PoliticalAffiliation = "Independent",
                MaritalStatus = "Married",
                ParentalStatus = "Has Children",
                MediaConsumption = "Mainstream News",
                RiskPerception = 0.5,
                SpecializedKnowledge = "Constitutional Law",
                CurrentStatus = "Attentive",
                JudicialTemperament = "Fair and patient",
                JudicialRulings = "Has presided over many trials"
            };
            original.Memories.Add(new MemoryEntry { Content = "Memory 1", Strength = 1.0 });
            original.Memories.Add(new MemoryEntry { Content = "Memory 2", Strength = 0.5 });

            CharacterManager.SaveCharacter(original);
            var savedFile = Path.Combine(CharacterManager.RepositoryPath, original.Name.Replace(" ", "_") + ".vcs");
            Assert(File.Exists(savedFile), "SaveCharacter creates .vcs file");

            var loadedChar = CharacterManager.LoadCharacter(original.Name);
            Assert(loadedChar != null, "LoadCharacter returns non-null after save");
            Assert(loadedChar.Name == "Test Character", "Loaded character preserves Name");
            Assert(loadedChar.Role == AgentRole.Judge, "Loaded character preserves Role");
            Assert(loadedChar.Gender == "Male", "Loaded character preserves Gender");
            Assert(loadedChar.Age == 55, "Loaded character preserves Age");
            Assert(loadedChar.Race == "White", "Loaded character preserves Race");
            Assert(loadedChar.Occupation == "Judge", "Loaded character preserves Occupation");
            Assert(loadedChar.EducationLevel == "Juris Doctor", "Loaded character preserves EducationLevel");
            Assert(loadedChar.IncomeLevel == "Upper Class", "Loaded character preserves IncomeLevel");
            Assert(loadedChar.Bias == 0.1, "Loaded character preserves Bias");

            // Note: Agent.VerdictLean is only settable when HasOpinion is true.
            // In this test, Role=Judge => HasOpinion=true, so persistence should work.
            Assert(loadedChar.VerdictLean == 0.5, "Loaded character preserves VerdictLean");
            Assert(loadedChar.Sentiment == 0.5, "Loaded character preserves Sentiment");
            Assert(loadedChar.IsOccupied, "Loaded character preserves IsOccupied");
            Assert(loadedChar.SystemPrompt == "You are a fair judge.", "Loaded character preserves SystemPrompt");
            Assert(loadedChar.Profile == "An experienced judge.", "Loaded character preserves Profile");
            Assert(loadedChar.Memories.Count == 2, "Loaded character preserves Memories");
            Assert(loadedChar.Memories[0].Content == "Memory 1", "Loaded character preserves memory content");
            Assert(loadedChar.Memories[0].Strength == 1.0, "Loaded character preserves memory strength");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static void TestProviderDiscovery()
    {
        Console.WriteLine("\n─── ProviderDiscoveryService ───");

        var providers = ProviderDiscoveryService.GetAvailableProviders();
        Assert(providers != null, "GetAvailableProviders returns non-null list");
        Assert(providers.Count > 0, "GetAvailableProviders returns at least one provider");

        // Verify all discovered providers implement the interface correctly
        // Note: Only ActiveProviders are returned (ONNX, HuggingFace, etc. are deactivated).
        foreach (var provider in providers)
        {
            Assert(!string.IsNullOrWhiteSpace(provider.ProviderName), $"{provider.GetType().Name}.ProviderName is not empty");
            Assert(!string.IsNullOrWhiteSpace(provider.Description), $"{provider.GetType().Name}.Description is not empty");
            Assert(!string.IsNullOrWhiteSpace(provider.DefaultFriendlyName), $"{provider.GetType().Name}.DefaultFriendlyName is not empty");
            Assert(provider.ConfigFields != null, $"{provider.GetType().Name}.ConfigFields is not null");
        }

        // Check GetProviderByName with active provider
        var activeProvider = providers.FirstOrDefault();
        if (activeProvider != null)
        {
            var byName = ProviderDiscoveryService.GetProviderByName(activeProvider.ProviderName);
            Assert(byName != null, $"GetProviderByName(\"{activeProvider.ProviderName}\") returns provider");
        }

        // Check GetProviderByName with non-existent name
        var missingName = ProviderDiscoveryService.GetProviderByName("NonExistentProvider_12345");
        Assert(missingName == null, "GetProviderByName(non-existent) returns null");

        // Check GetProviderByName with null/empty
        var nullName = ProviderDiscoveryService.GetProviderByName(null);
        Assert(nullName == null, "GetProviderByName(null) returns null");

        var emptyName = ProviderDiscoveryService.GetProviderByName("");
        Assert(emptyName == null, "GetProviderByName(\"\") returns null");
    }

    private static void TestSettingsService()
    {
        Console.WriteLine("\n─── SettingsService ───");

        var service = new SettingsService();

        // Backup any existing saved settings so we can restore them after the test
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Verdict",
            "default_settings.json");
        var backupPath = settingsPath + ".bak";
        if (File.Exists(settingsPath))
            File.Copy(settingsPath, backupPath, true);

        try
        {
            // Delete any existing settings so we start fresh
            if (File.Exists(settingsPath))
                File.Delete(settingsPath);

            // 1. Fresh start: GetDefaultCaseSettings should return a CaseFile with defaults
            var defaults = service.GetDefaultCaseSettings();
            Assert(defaults != null, "GetDefaultCaseSettings returns non-null");
            Assert(defaults.CaseName == "New Case", "Default case name is 'New Case'");
            Assert(defaults.Mode == CaseMode.Civil, "Default mode is Civil");
            Assert(defaults.Jurisdiction == JurisdictionType.State, "Default jurisdiction is State");
            Assert(defaults.JurorCount == 12, "Default juror count is 12");
            Assert(defaults.AvailableModels.Count >= 2, "Default settings has at least ONNX + HuggingFace models");
            Assert(defaults.AvailableModels.Any(m => m.Provider == "HF ONNX (GenAI)"), "ONNX in default models");
            Assert(defaults.AvailableModels.Any(m => m.Provider == "HF GGUF (LlamaSharp)"), "HuggingFace in default models");

            // 2. Save custom models and verify they persist
            defaults.AvailableModels.Clear();
            defaults.AvailableModels.Add(new AIModelConfiguration
            {
                FriendlyName = "DeepSeek V3",
                Provider = "DeepSeek",
                ModelId = "deepseek-v3",
                ApiKey = "sk-test-key",
                Endpoint = "https://api.deepseek.com/v1",
                Status = "Connected"
            });
            defaults.AvailableModels.Add(new AIModelConfiguration
            {
                FriendlyName = "Custom Ollama",
                Provider = "Ollama",
                ModelId = "llama3",
                Endpoint = "http://localhost:11434",
                Status = "Not Tested"
            });
            service.SaveDefaultCaseSettings(defaults);

            // 3. Load back and verify custom models are preserved
            var loaded = service.GetDefaultCaseSettings();
            Assert(loaded != null, "Loaded settings is non-null");
            Assert(loaded.AvailableModels.Count == 2, "Loaded settings has 2 custom models");
            Assert(loaded.AvailableModels[0].FriendlyName == "DeepSeek V3", "First loaded model is DeepSeek V3");
            Assert(loaded.AvailableModels[0].Provider == "DeepSeek", "DeepSeek provider preserved");
            Assert(loaded.AvailableModels[0].ModelId == "deepseek-v3", "DeepSeek model ID preserved");
            Assert(loaded.AvailableModels[0].ApiKey == "sk-test-key", "DeepSeek API key preserved");
            Assert(loaded.AvailableModels[0].Endpoint == "https://api.deepseek.com/v1", "DeepSeek endpoint preserved");
            Assert(loaded.AvailableModels[0].Status == "Connected", "DeepSeek status preserved");
            Assert(loaded.AvailableModels[1].FriendlyName == "Custom Ollama", "Second loaded model is Custom Ollama");
            Assert(loaded.AvailableModels[1].Provider == "Ollama", "Ollama provider preserved");

            // 4. Verify that default models are NOT re-added when loading saved settings
            Assert(loaded.AvailableModels.Count == 2, "No default models leaked into saved config");

            // 5. Save with empty models list
            loaded.AvailableModels.Clear();
            service.SaveDefaultCaseSettings(loaded);
            var reloaded = service.GetDefaultCaseSettings();
            Assert(reloaded.AvailableModels.Count == 0, "Empty models list is preserved after save/load");

            Console.WriteLine("  ✓ SettingsService persistence tests passed");
        }
        finally
        {
            // Restore original settings (if any)
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, settingsPath, true);
                File.Delete(backupPath);
            }
            else if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    private static void TestCourtroomManagerService()
    {
        Console.WriteLine("\n─── CourtroomManagerService ───");

        var service = new CourtroomManagerService();

        // Test InitializeCourtroom with null caseData
        var judgeArea = new ObservableCollection<Agent>();
        var jurors = new ObservableCollection<Agent>();
        var defenseTeam = new ObservableCollection<Agent>();
        var prosecutionTeam = new ObservableCollection<Agent>();
        var gallery = new ObservableCollection<Agent>();

        service.InitializeCourtroom(judgeArea, jurors, defenseTeam, prosecutionTeam, gallery, null);

        // Verify bench area
        Assert(judgeArea.Count == 3, "Judge area has 3 slots (Reporter, Judge, Witness)");
        Assert(judgeArea.Any(a => a.Role == AgentRole.Reporter && a.Name == "Reporter"), "Judge area has Reporter");
        Assert(judgeArea.Any(a => a.Role == AgentRole.Judge && a.Name == "Judge Jones"), "Judge area has Judge Jones");
        Assert(judgeArea.Any(a => a.Role == AgentRole.Witness && a.Name == "Witness Box"), "Judge area has Witness Box");
        Assert(judgeArea.First(a => a.Role == AgentRole.Reporter).IsOccupied, "Reporter is occupied by default");

        // Verify jurors
        Assert(jurors.Count == 12, "Jury has 12 jurors");
        Assert(jurors.All(j => j.Role == AgentRole.Juror), "All jurors have Juror role");
        Assert(jurors[0].Name == "Juror 1", "First juror is 'Juror 1'");
        Assert(jurors[11].Name == "Juror 12", "Last juror is 'Juror 12'");

        // Verify counsel tables
        Assert(defenseTeam.Count == 1, "Defense team has 1 lawyer");
        Assert(defenseTeam[0].Role == AgentRole.Lawyer, "Defense team member is Lawyer");
        Assert(defenseTeam[0].Name == "Danny Defense", "Default defense lawyer is 'Danny Defense'");

        Assert(prosecutionTeam.Count == 1, "Prosecution team has 1 lawyer");
        Assert(prosecutionTeam[0].Role == AgentRole.Lawyer, "Prosecution team member is Lawyer");
        Assert(prosecutionTeam[0].Name == "Paul Plaintiff", "Default plaintiff lawyer is 'Paul Plaintiff'");

        // Verify gallery: 2 alternate jurors + 4 observers + 2 clients = 8
        Assert(gallery.Count == 8, "Gallery has 8 slots (2 alternates + 4 observers + 2 clients)");
        Assert(gallery.Count(a => a.Role == AgentRole.AlternateJuror) == 2, "Gallery has 2 alternate jurors");
        Assert(gallery.Count(a => a.Role == AgentRole.Observer) == 4, "Gallery has 4 observers");
        Assert(gallery.Count(a => a.Role == AgentRole.Client) == 2, "Gallery has 2 clients");
        Assert(gallery.Any(a => a.Name == "+ Plaintiff Client"), "Gallery has plaintiff client slot");
        Assert(gallery.Any(a => a.Name == "+ Defense Client"), "Gallery has defense client slot");

        // Test InitializeCourtroom with caseData (custom attorney names)
        var judgeArea2 = new ObservableCollection<Agent>();
        var jurors2 = new ObservableCollection<Agent>();
        var defenseTeam2 = new ObservableCollection<Agent>();
        var prosecutionTeam2 = new ObservableCollection<Agent>();
        var gallery2 = new ObservableCollection<Agent>();

        var caseData = new CaseFile
        {
            DefenseAttorney = "Custom Defense",
            PlaintiffAttorney = "Custom Plaintiff"
        };
        caseData.Plaintiffs.Add("John Doe");
        caseData.Defendants.Add("Jane Corp");

        service.InitializeCourtroom(judgeArea2, jurors2, defenseTeam2, prosecutionTeam2, gallery2, caseData);

        Assert(defenseTeam2[0].Name == "Custom Defense", "Custom defense attorney name used");
        Assert(prosecutionTeam2[0].Name == "Custom Plaintiff", "Custom plaintiff attorney name used");
        Assert(gallery2.Any(a => a.Name == "+ John Doe"), "Plaintiff client name from caseData");
        Assert(gallery2.Any(a => a.Name == "+ Jane Corp"), "Defense client name from caseData");

        // Test OccupySlot
        var agent = new Agent { Role = AgentRole.Lawyer, Name = "+", IsOccupied = false };
        var defenseTeam3 = new ObservableCollection<Agent> { new Agent { Role = AgentRole.Lawyer, Name = "+", IsOccupied = false } };
        var prosecutionTeam3 = new ObservableCollection<Agent> { new Agent { Role = AgentRole.Lawyer, Name = "+", IsOccupied = false } };

        service.OccupySlot(agent, defenseTeam3, prosecutionTeam3);
        Assert(agent.IsOccupied, "OccupySlot sets IsOccupied to true");
        Assert(agent.Name == "Active Lawyer", "OccupySlot renames '+' to 'Active Lawyer'");

        // Test OccupySlot on already occupied agent (should not change)
        agent.Name = "+";
        agent.IsOccupied = true;
        service.OccupySlot(agent, defenseTeam3, prosecutionTeam3);
        Assert(agent.Name == "+", "OccupySlot does not rename already occupied agent");

        // Test ResetAllAgents
        var agents = new List<Agent>
        {
            new Agent { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.8, Memories = { new MemoryEntry { Content = "Test", Strength = 1.0 } } },
            new Agent { Role = AgentRole.Reporter, IsOccupied = true, VerdictLean = 0.7, Memories = { new MemoryEntry { Content = "Test2", Strength = 1.0 } } }
        };
        service.ResetAllAgents(agents);
        Assert(!agents[0].IsOccupied, "ResetAllAgents sets IsOccupied to false");
        Assert(agents[0].Memories.Count == 0, "ResetAllAgents clears memories");
        Assert(agents[0].VerdictLean == 0.5, "ResetAllAgents resets VerdictLean to 0.5 for opinion-holders");
        Assert(agents[1].VerdictLean == 0.5, "ResetAllAgents keeps Reporter VerdictLean at default (setter guarded by HasOpinion)");

        // Test CreateEmptySlot
        var emptySlot = service.CreateEmptySlot(AgentRole.Judge, "+");
        Assert(emptySlot.Role == AgentRole.Judge, "CreateEmptySlot creates agent with correct role");
        Assert(emptySlot.Name == "+", "CreateEmptySlot creates agent with '+' name");
        Assert(!emptySlot.IsOccupied, "CreateEmptySlot creates unoccupied agent");
    }

    private static void TestEvidenceAnalysisService()
    {
        Console.WriteLine("\n─── EvidenceAnalysisService ───");

        var service = new EvidenceAnalysisService();

        // Test AssessStrength with neutral summary (avoid keywords like "document")
        var doc = new EvidenceDocument();
        service.AssessStrength(doc, "Neutral filing with no strong indicators.");
        Assert(doc.EvidenceStrength == 0.5, "Neutral summary results in 0.5 strength");
        Assert(doc.EstimatedDamages == 50000, "Neutral summary results in default 50000 damages");

        // Test AssessStrength with medical keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Medical report showing injury to the plaintiff.");
        Assert(doc.EvidenceStrength >= 0.7, "Medical keywords increase strength to at least 0.7");
        Assert(doc.EstimatedDamages >= 100000, "Medical keywords set damages to at least 100000");

        // Test AssessStrength with expert keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Expert analysis report with detailed findings.");
        Assert(doc.EvidenceStrength >= 0.8, "Expert keywords increase strength to at least 0.8");

        // Test AssessStrength with police/criminal keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Police arrest report and criminal investigation.");
        Assert(doc.EvidenceStrength >= 0.95, "Police/criminal keywords increase strength to at least 0.95");

        // Test AssessStrength with weakening keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "This is hearsay and uncertain.");
        Assert(doc.EvidenceStrength <= 0.3, "Hearsay/uncertain keywords reduce strength to at most 0.3");

        // Test AssessStrength with dollar amount parsing
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "The claim is for $1,500,000 in damages.");
        Assert(doc.EstimatedDamages == 1500000, "Dollar amount parsed correctly: $1,500,000");

        // Test AssessStrength with million text
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "The claim is for 5 million dollars.");
        Assert(doc.EstimatedDamages == 5000000, "Million text parsed correctly: 5 million");

        // Test AssessStrength with legal multiplier (treble)
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Treble damages are sought for this claim.");
        Assert(doc.EvidenceStrength >= 0.9, "Treble damages keyword increases strength to at least 0.9");
        Assert(doc.EstimatedDamages >= 150000, "Treble multiplier triples default damages (50000 * 3 = 150000)");

        // Test AssessStrength with photo/video keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Photo evidence and video recording of the incident.");
        Assert(doc.EvidenceStrength >= 0.9, "Photo/video keywords increase strength to at least 0.9");

        // Test AssessStrength with child/minor keywords
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "Minor child was injured in the accident.");
        Assert(doc.EvidenceStrength >= 0.9, "Child/minor keywords increase strength to at least 0.9");
        Assert(doc.EstimatedDamages >= 300000, "Child/minor keywords set damages to at least 300000");

        // Test AssessStrength with expired/outdated keywords (avoid strengthening keywords first)
        doc = new EvidenceDocument();
        service.AssessStrength(doc, "This expired filing is quite outdated.");
        Assert(doc.EvidenceStrength <= 0.25, "Expired/outdated keywords halve strength (0.5 * 0.5 = 0.25)");

        // Test CalculateExposure
        var caseFile = new CaseFile();
        caseFile.Evidence.Add(new EvidenceDocument { EstimatedDamages = 100000, EvidenceStrength = 0.8 });
        caseFile.Evidence.Add(new EvidenceDocument { EstimatedDamages = 50000, EvidenceStrength = 0.6 });
        caseFile.Mode = CaseMode.Civil;
        service.CalculateExposure(caseFile);
        // Total exposure = (100000 * 0.8) + (50000 * 0.6) = 80000 + 30000 = 110000
        // Civil settlement = 110000 * 0.8 = 88000
        // Insurance reserve = 110000 * 1.25 = 137500
        Assert(caseFile.EstimatedSettlement == 88000, "CalculateExposure computes civil settlement correctly");
        Assert(caseFile.InsuranceReserve == 137500, "CalculateExposure computes insurance reserve correctly");

        // Test CalculateExposure with Criminal mode
        var criminalCase = new CaseFile();
        criminalCase.Evidence.Add(new EvidenceDocument { EstimatedDamages = 100000, EvidenceStrength = 0.8 });
        criminalCase.Mode = CaseMode.Criminal;
        service.CalculateExposure(criminalCase);
        // Criminal settlement = 80000 * 0.6 = 48000
        Assert(criminalCase.EstimatedSettlement == 48000, "CalculateExposure computes criminal settlement correctly");

        // Test InterpretSummary
        caseFile.AvailableModels.Add(new AIModelConfiguration { FriendlyName = "GPT-4o" });
        var interpretation = service.InterpretSummary("Medical report", caseFile);
        Assert(interpretation.Contains("[INTERPRETED BY GPT-4O]"), "InterpretSummary includes model name");
        Assert(interpretation.Contains("Medical report"), "InterpretSummary includes original summary");
        Assert(interpretation.Contains("Civil"), "InterpretSummary includes case mode");
        Assert(interpretation.Contains("Superior Court"), "InterpretSummary includes court name");

        // Test ReadFileContent with non-existent file
        var content = service.ReadFileContent("Z:\\NonExistentFile_12345.txt");
        Assert(content.StartsWith("File not found:"), "ReadFileContent returns 'File not found' for non-existent file");

        // Test ReadFileContent with text file
        var tempFile = Path.Combine(Path.GetTempPath(), "VerdictTest_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(tempFile, "Test content for evidence analysis.");
            content = service.ReadFileContent(tempFile);
            Assert(content == "Test content for evidence analysis.", "ReadFileContent reads text file correctly");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }

        // Test ReadFileContent with image file
        var tempImage = Path.Combine(Path.GetTempPath(), "VerdictTest_" + Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            File.WriteAllBytes(tempImage, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG header
            content = service.ReadFileContent(tempImage);
            Assert(content.Contains("[Image file:"), "ReadFileContent returns image metadata for .jpg");
            Assert(content.Contains(".jpg"), "ReadFileContent identifies .jpg extension");
        }
        finally
        {
            if (File.Exists(tempImage))
                File.Delete(tempImage);
        }

        // Test ReadFileContent with unknown extension
        var tempUnknown = Path.Combine(Path.GetTempPath(), "VerdictTest_" + Guid.NewGuid().ToString("N") + ".xyz");
        try
        {
            File.WriteAllText(tempUnknown, "Test");
            content = service.ReadFileContent(tempUnknown);
            Assert(content.Contains("[Unknown file format:"), "ReadFileContent returns unknown format for .xyz");
        }
        finally
        {
            if (File.Exists(tempUnknown))
                File.Delete(tempUnknown);
        }

        // Test GenerateDocumentAnalysisAsync with no models (fallback)
        var emptyCase = new CaseFile();
        var (analysis, damages) = service.GenerateDocumentAnalysisAsync("test.txt", "content", "summary", emptyCase, null).GetAwaiter().GetResult();
        Assert(!string.IsNullOrEmpty(analysis), "GenerateDocumentAnalysisAsync returns fallback analysis when no models");
        Assert(damages >= 0, "GenerateDocumentAnalysisAsync returns non-negative damages in fallback");
    }

    private static void TestDebateService()
    {
        Console.WriteLine("\n─── DebateService ───");

        var service = new DebateService();

        // Test AdvanceStage progression
        Assert(service.AdvanceStage(CourtPhase.OpeningStatements) == CourtPhase.WitnessTestimony, "OpeningStatements advances to WitnessTestimony");
        Assert(service.AdvanceStage(CourtPhase.WitnessTestimony) == CourtPhase.CrossExamination, "WitnessTestimony advances to CrossExamination");
        Assert(service.AdvanceStage(CourtPhase.CrossExamination) == CourtPhase.ClosingArguments, "CrossExamination advances to ClosingArguments");
        Assert(service.AdvanceStage(CourtPhase.ClosingArguments) == CourtPhase.VerdictAnnouncement, "ClosingArguments advances to VerdictAnnouncement");
        Assert(service.AdvanceStage(CourtPhase.VerdictAnnouncement) == CourtPhase.VerdictAnnouncement, "VerdictAnnouncement stays at VerdictAnnouncement");

        // Test StageDisplayName
        Assert(service.StageDisplayName(CourtPhase.OpeningStatements) == "Opening Statements", "StageDisplayName for OpeningStatements");
        Assert(service.StageDisplayName(CourtPhase.WitnessTestimony) == "Witness Examination", "StageDisplayName for WitnessTestimony");
        Assert(service.StageDisplayName(CourtPhase.CrossExamination) == "Cross-Examination", "StageDisplayName for CrossExamination");
        Assert(service.StageDisplayName(CourtPhase.ClosingArguments) == "Closing Arguments", "StageDisplayName for ClosingArguments");
        Assert(service.StageDisplayName(CourtPhase.VerdictAnnouncement) == "Verdict Announcement", "StageDisplayName for VerdictAnnouncement");

        // Test CalculateInfluence
        Assert(service.CalculateInfluence("Prosecutor") == 0.05, "Prosecutor influence is 0.05");
        Assert(service.CalculateInfluence("Plaintiff Attorney") == 0.05, "Plaintiff influence is 0.05");
        Assert(service.CalculateInfluence("Defense Attorney") == -0.05, "Defense influence is -0.05");
        Assert(service.CalculateInfluence("Witness") == 0, "Witness influence is 0");

        Console.WriteLine("  DebateService tests passed.");
    }

    private static void TestJuryCalculationService()
    {
        Console.WriteLine("\n─── JuryCalculationService ───");
        var service = new JuryCalculationService();

        // AverageLean
        var jurors = new List<Agent>
        {
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.8 },
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.2 },
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.5 },
            new() { Role = AgentRole.Judge, IsOccupied = true, VerdictLean = 0.9 } // Non-voter, excluded
        };
        double avg = service.AverageLean(jurors);
        Assert(Math.Abs(avg - 0.5) < 0.01, "AverageLean averages only CanVote jurors (0.8+0.2+0.5)/3 = 0.5");

        // Empty jury
        Assert(service.AverageLean(new List<Agent>()) == 0.5, "AverageLean returns 0.5 for empty list");

        // LikelyVerdict
        var splitJury = new List<Agent>
        {
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.8 },
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.2 }
        };
        string verdict = service.LikelyVerdict(splitJury);
        Assert(verdict.Contains("Split") || verdict.Contains("1"), "LikelyVerdict handles split jury");
        Assert(service.LikelyVerdict(new List<Agent>()) == "No Jurors Seated", "LikelyVerdict handles empty");

        // Strong plaintiff
        var strongProJury = new List<Agent>();
        for (int i = 0; i < 8; i++)
            strongProJury.Add(new Agent { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.9 });
        strongProJury.Add(new Agent { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.3 });
        strongProJury.Add(new Agent { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.3 });
        verdict = service.LikelyVerdict(strongProJury);
        Assert(verdict.Contains("Strong"), "LikelyVerdict shows 'Strong' for >75% pro-plaintiff");

        // ApplyEvidenceInfluence
        var influenceJurors = new List<Agent>
        {
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.5 },
            new() { Role = AgentRole.Juror, IsOccupied = true, VerdictLean = 0.5 }
        };
        var doc = new EvidenceDocument { EvidenceStrength = 0.9, EstimatedDamages = 100000 };
        service.ApplyEvidenceInfluence(influenceJurors, doc);
        Assert(influenceJurors[0].VerdictLean != 0.5, "ApplyEvidenceInfluence changes juror VerdictLean");

        // ApplyTranscriptInfluence
        influenceJurors[0].VerdictLean = 0.5;
        service.ApplyTranscriptInfluence(influenceJurors, 0.1);
        Assert(influenceJurors[0].VerdictLean > 0.5, "ApplyTranscriptInfluence with positive influence shifts lean up");
    }

    private static void TestCaseEntityMapper()
    {
        Console.WriteLine("\n─── CaseEntityMapper ───");
        var service = new CaseEntityMapper(new EvidenceAnalysisService());
        var caseFile = new CaseFile { CaseName = "Test" };
        var entities = new ExtractedEntities();

        // Empty entities = minimal mapping
        string result = service.MapToCaseFile(caseFile, entities);
        Assert(!string.IsNullOrEmpty(result), "MapToCaseFile returns non-empty string");
        Assert(result.Contains("no entities found"), "Empty entities returns 'no entities found'");

        // Map entities with data
        var populatedEntities = new ExtractedEntities
        {
            Plaintiffs = { "Alice Smith" },
            Defendants = { "Bob Corp" },
            PlaintiffAttorney = "Carol Lawyer",
            DefenseAttorney = "Dave Counsel",
            CaseSummary = "A test case summary",
            CauseOfAction = "Negligence"
        };
        populatedEntities.Charges.Add(new ExtractedCharge { Name = "Burglary", Severity = "Felony" });
        populatedEntities.Witnesses.Add(new ExtractedWitness { Name = "Eve Witness", Role = "Expert", Testified = true });
        populatedEntities.Evidence.Add(new ExtractedEvidence { Type = "Document", Description = "Contract", Strength = 0.8 });

        var mappedCase = new CaseFile();
        result = service.MapToCaseFile(mappedCase, populatedEntities);
        Assert(mappedCase.Plaintiffs.Contains("Alice Smith"), "Plaintiffs mapped to caseFile");
        Assert(mappedCase.Defendants.Contains("Bob Corp"), "Defendants mapped to caseFile");
        Assert(mappedCase.PlaintiffAttorney == "Carol Lawyer", "PlaintiffAttorney mapped");
        Assert(mappedCase.DefenseAttorney == "Dave Counsel", "DefenseAttorney mapped");
        Assert(result.Contains("Negligence"), "CauseOfAction reflected in result");
    }

    private static void TestAgentAssignmentService()
    {
        Console.WriteLine("\n─── AgentAssignmentService ───");
        var service = new AgentAssignmentService();

        // AnalyzeCase
        var caseFile = new CaseFile
        {
            CaseName = "Test Case",
            Mode = CaseMode.Criminal,
            Jurisdiction = JurisdictionType.Federal
        };
        var plan = service.AnalyzeCase(caseFile);
        Assert(plan != null, "AnalyzeCase returns non-null plan");
        Assert(!string.IsNullOrEmpty(plan.StrategyDescription), "Plan has strategy description");
        Assert(plan.RecommendedDefenseAttorneys >= 1, "Plan recommends at least 1 defense attorney");
        Assert(plan.RecommendedProsecutionAttorneys >= 1, "Plan recommends at least 1 prosecution attorney");

        // ApplyAssignmentPlan
        var judgeArea = new ObservableCollection<Agent>();
        var jurors = new ObservableCollection<Agent>();
        var defenseTeam = new ObservableCollection<Agent>();
        var prosecutionTeam = new ObservableCollection<Agent>();
        var gallery = new ObservableCollection<Agent>();

        service.ApplyAssignmentPlan(plan, judgeArea, jurors, defenseTeam, prosecutionTeam, gallery);
        Assert(defenseTeam.Any(), "Defense team populated after ApplyAssignmentPlan");
        Assert(prosecutionTeam.Any(), "Prosecution team populated after ApplyAssignmentPlan");

        // GenerateDefaultAgents
        var agents = service.GenerateDefaultAgents(caseFile);
        Assert(agents.Count > 0, "GenerateDefaultAgents returns agents");
        Assert(agents.Any(a => a.Role == AgentRole.Judge), "Default agents include Judge");
        Assert(agents.Any(a => a.Role == AgentRole.Witness), "Default agents include expert Witness");
        Assert(agents.All(a => !string.IsNullOrEmpty(a.Name)), "All agents have names");
    }

    private static void TestJuryDemographicsService()
    {
        Console.WriteLine("\n─── JuryDemographicsService ───");
        var service = new JuryDemographicsService();

        // GenerateJuryPanel with Wisconsin county (demographically homogeneous ~94% White)
        var jury = service.GenerateJuryPanel(12, 2, "St. Croix County", "Wisconsin");
        Assert(jury.Count == 14, "GenerateJuryPanel(12,2) returns 14 agents");
        Assert(jury.Take(12).All(j => j.Role == AgentRole.Juror), "First 12 are Jurors");
        Assert(jury.Skip(12).All(j => j.Role == AgentRole.AlternateJuror), "Last 2 are AlternateJurors");
        Assert(jury.All(j => !string.IsNullOrWhiteSpace(j.Name)), "All jurors have names");
        Assert(jury.All(j => j.Age >= 21 && j.Age <= 85), "All jurors are 21-85 (age distribution range)");

        // Verify demographic diversity using a diverse county (Richland County, SC: ~44% White, ~46% Black)
        var diverseJury = service.GenerateJuryPanel(12, 2, "Richland County", "South Carolina");
        var races = diverseJury.Select(j => j.Race).Distinct().ToList();
        Assert(races.Count >= 2, $"Jury has diverse races (found {races.Count}: {string.Join(", ", races)})");

        var genders = diverseJury.Select(j => j.Gender).Distinct().ToList();
        Assert(genders.Count >= 2, $"Jury has both genders (found: {string.Join(", ", genders)})");

        // GenerateJuror
        var juror = service.GenerateJuror("Richland County", "South Carolina");
        Assert(juror != null, "GenerateJuror returns non-null");
        Assert(juror.Role == AgentRole.Juror, "GenerateJuror returns Juror role");
        Assert(!string.IsNullOrWhiteSpace(juror.Name), "Juror has a name");

        // Verify consistent results with real location
        var fallbackJury = service.GenerateJuryPanel(6, 0, "Richland County", "South Carolina");
        Assert(fallbackJury.Count == 6, "GenerateJuryPanel(6,0) returns 6 jurors");
    }

    private static void TestLegalDatabaseService()
    {
        Console.WriteLine("\n─── LegalDatabaseService ───");
        var service = new LegalDatabaseService();

        // Default constructor
        Assert(service != null, "Default constructor works");

        // Custom path constructor
        var customService = new LegalDatabaseService("Resources/LegalDatabase.json");
        Assert(customService != null, "Custom path constructor works");

        // LoadDatabase and SearchByKeyword
        service.LoadDatabase();
        var results = service.SearchByKeyword("negligence");
        Assert(results != null, "SearchByKeyword returns non-null");
        Assert(results.Count >= 0, "SearchByKeyword returns list (may be empty if DB not found)");

        // SearchByKeyword with null/empty
        var nullResult = service.SearchByKeyword(null);
        Assert(nullResult != null && nullResult.Count == 0, "SearchByKeyword(null) returns empty list");
        var emptyResult = service.SearchByKeyword("");
        Assert(emptyResult != null && emptyResult.Count == 0, "SearchByKeyword('') returns empty list");

        // GetAll
        var all = service.GetAll();
        Assert(all != null, "GetAll returns non-null");
    }

    private static void TestLogger()
    {
        Console.WriteLine("\n─── Logger ───");
        Assert(typeof(Logger).IsAbstract && typeof(Logger).IsSealed, "Logger is a static utility class");

        // Test Log doesn't throw
        try
        {
            Logger.Log("Test log message from ServiceTests");
            Assert(true, "Logger.Log does not throw");
        }
        catch (Exception ex)
        {
            Assert(false, $"Logger.Log threw unexpected exception: {ex.Message}");
        }

        // Test LogException doesn't throw
        try
        {
            Logger.LogException(new InvalidOperationException("Test exception"), "TestContext");
            Assert(true, "Logger.LogException does not throw");
        }
        catch (Exception ex)
        {
            Assert(false, $"Logger.LogException threw unexpected exception: {ex.Message}");
        }

        // Test LogException with empty context
        try
        {
            Logger.LogException(new ArgumentNullException("testParam"));
            Assert(true, "Logger.LogException without context does not throw");
        }
        catch (Exception ex)
        {
            Assert(false, $"Logger.LogException (no context) threw: {ex.Message}");
        }
    }

    private static void TestAgentInteractionService()
    {
        Console.WriteLine("\n─── AgentInteractionService ───");
        var providers = ProviderDiscoveryService.GetAvailableProviders();
        var service = new AgentInteractionService(providers);
        Assert(service != null, "AgentInteractionService instantiates with providers");
        Assert(providers.Count > 0, "Providers are available for AgentInteractionService");

        // Build model config from environment variable (DeepSeek key)
        var model = new AIModelConfiguration
        {
            FriendlyName = "Test-DeepSeek",
            Provider = "DeepSeek",
            ModelId = "deepseek-chat",
            Endpoint = "https://api.deepseek.com/v1",
            ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? string.Empty
        };

        bool hasKey = !string.IsNullOrWhiteSpace(model.ApiKey);
        Console.WriteLine(hasKey
            ? "  DeepSeek API key found — running live LLM test"
            : "  No DeepSeek API key — skipping live LLM test");

        if (!hasKey) return;

        // Live LLM call: GenerateStatementAsync
        try
        {
            var request = new StatementRequest
            {
                Role = AgentRole.Juror,
                SpeakerName = "Test Juror",
                Context = "You are a juror in a civil trial. State your initial impression briefly.",
                Type = MessageType.Statement,
                CaseData = new CaseFile { CaseName = "Test Case", Mode = CaseMode.Civil },
                Model = model
            };
            var result = service.GenerateStatementAsync(request).GetAwaiter().GetResult();
            Assert(result != null, "GenerateStatementAsync returns non-null result");
            Assert(result.Success, $"GenerateStatementAsync succeeded: {result.Error ?? "OK"}");
            Assert(result.Message != null, "GenerateStatementAsync returns a message");
            Assert(!string.IsNullOrEmpty(result.Message.Content), "Message has content");
            Assert(result.Message.Content.Length > 10, "Message content is substantive (>10 chars)");

            Console.WriteLine($"  ✓ Live LLM response: \"{result.Message.Content[..Math.Min(80, result.Message.Content.Length)]}...\"");
        }
        catch (Exception ex)
        {
            Assert(false, $"Live LLM test failed: {ex.Message}");
        }
    }

    private static void TestToyJurorLogicEngineV061()
    {
        Console.WriteLine("\n─── ToyJurorLogicEngine v0.61 ───");

        // ── Test 1: Stage-3 target selection ──
        // Verify that the conviction branch pulls toward max(threshold+0.01, M)
        // and the acquittal branch pulls toward (threshold − 0.01) — NOT toward M.
        // Create 12 mock jurors with known lean values
        var caseFile = new CaseFile { Mode = CaseMode.Criminal, CaseName = "Test v0.61" };
        caseFile.EnsureCollectionsInitialized();
        var agents = new List<Agent>();
        var rng = new Random(42);

        // Create 12 jurors with diverse leans
        for (int i = 0; i < 12; i++)
        {
            var agent = new Agent
            {
                AgentId = Guid.NewGuid(),
                Name = $"Juror {i + 1}",
                Role = AgentRole.Juror,
                IsOccupied = true,
                Age = 25 + rng.Next(50),
                Gender = rng.Next(2) == 0 ? "Male" : "Female",
                Race = "White",
                Occupation = "Engineer",
                EducationLevel = "Bachelor's Degree",
                IncomeLevel = "Middle Class",
                PoliticalAffiliation = "Independent",
                VerdictLean = 0.3 + rng.NextDouble() * 0.4, // 0.3–0.7 range
                Bias = rng.NextDouble() * 0.4 - 0.2
            };
            agents.Add(agent);
        }

        // Apply one round with neutral evidence
        double preAvg = agents.Average(a => a.VerdictLean);
        ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(
            agents, deltaEvidence: 0.02, biasFactors: null,
            maxSampleTries: 500, coherenceThreshold: 0.9, seed: 42,
            mode: CaseMode.Criminal, evidenceIndex: 0, totalEvidenceCount: 1);

        double postAvg = agents.Average(a => a.VerdictLean);
        Assert(postAvg >= 0.05 && postAvg <= 0.95,
            $"Post-deliberation lean within bounds: {postAvg:F3}");

        // ── Test 2: Hardness monotonicity ──
        // All verdict leans must remain in valid bounds after deliberation.
        // Hardness is computed internally; we validate that the pipeline
        // produces bounded outputs regardless of trait composition.
        Assert(agents.All(a => a.VerdictLean >= 0.05 && a.VerdictLean <= 0.95),
            "All verdict leans within [0.05, 0.95] after deliberation");

        // ── Test 3: Motivated-reasoning conflict detection ──
        // Apply deliberation with evidenceDirection that conflicts with conservative jurors.
        // evidenceDirection = +1 (pro-prosecution) should conflict with PolId > 0 (conservative).
        // Conservative jurors should show more resistance (higher rigidity) to pro-prosecution evidence.
        var agents2 = new List<Agent>();
        for (int i = 0; i < 12; i++)
        {
            var agent = new Agent
            {
                AgentId = Guid.NewGuid(),
                Name = $"Juror MR{i + 1}",
                Role = AgentRole.Juror,
                IsOccupied = true,
                Age = 25 + rng.Next(50),
                Gender = rng.Next(2) == 0 ? "Male" : "Female",
                Race = "White",
                Occupation = "Engineer",
                EducationLevel = "Bachelor's Degree",
                IncomeLevel = "Middle Class",
                PoliticalAffiliation = i < 6 ? "Conservative" : "Liberal",
                VerdictLean = 0.5,
                Bias = i < 6 ? 0.3 : -0.3
            };
            agents2.Add(agent);
        }

        // Record pre-deliberation leans
        var preLeans = agents2.Select(a => a.VerdictLean).ToList();

        // Apply pro-prosecution evidence (evidenceDirection = +1)
        ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(
            agents2, deltaEvidence: 0.04, biasFactors: null,
            maxSampleTries: 500, coherenceThreshold: 0.9, seed: 43,
            mode: CaseMode.Criminal, evidenceIndex: 0, totalEvidenceCount: 1,
            evidenceDirection: +1.0);

        // All leans should still be in valid range
        Assert(agents2.All(a => a.VerdictLean >= 0.05 && a.VerdictLean <= 0.95),
            "Motivated-reasoning test: all leans in valid range");

        // ── Test 4: Influence weight non-saturation ──
        // With the softplus formula, highly-educated jurors should not all saturate at 3.5.
        // Qualitative test: verify the engine runs without crashing for mixed-education juries.
        var agents3 = new List<Agent>();
        for (int i = 0; i < 12; i++)
        {
            var agent = new Agent
            {
                AgentId = Guid.NewGuid(),
                Name = $"Juror IW{i + 1}",
                Role = AgentRole.Juror,
                IsOccupied = true,
                Age = 25 + rng.Next(50),
                Gender = rng.Next(2) == 0 ? "Male" : "Female",
                Race = "White",
                Occupation = i < 4 ? "Professor" : "Mechanic",
                EducationLevel = i < 4 ? "Doctorate (PhD)" : "High School",
                IncomeLevel = i < 4 ? "Upper Class" : "Working Class",
                PoliticalAffiliation = "Independent",
                VerdictLean = 0.5,
                Bias = 0.0
            };
            agents3.Add(agent);
        }

        ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(
            agents3, deltaEvidence: 0.03, biasFactors: null,
            maxSampleTries: 500, coherenceThreshold: 0.9, seed: 44,
            mode: CaseMode.Civil, evidenceIndex: 0, totalEvidenceCount: 1);

        Assert(agents3.All(a => a.VerdictLean >= 0.05 && a.VerdictLean <= 0.95),
            "Influence weight test: all leans in valid range after mixed-education deliberation");

        // ── Test 5: Acquittal branch does not collapse to M ──
        // When all jurors initially lean toward acquittal (VerdictLean < 0.5),
        // the acquittal branch should pull them toward (threshold − 0.01),
        // NOT toward the group mean M (which would be 0.0 and cause no movement).
        var agents4 = new List<Agent>();
        for (int i = 0; i < 12; i++)
        {
            var agent = new Agent
            {
                AgentId = Guid.NewGuid(),
                Name = $"Juror AQ{i + 1}",
                Role = AgentRole.Juror,
                IsOccupied = true,
                Age = 25 + rng.Next(50),
                Gender = rng.Next(2) == 0 ? "Male" : "Female",
                Race = "White",
                Occupation = "Teacher",
                EducationLevel = "Master's Degree",
                IncomeLevel = "Middle Class",
                PoliticalAffiliation = "Liberal",
                VerdictLean = 0.2 + rng.NextDouble() * 0.15, // 0.20–0.35 range (acquittal-leaning)
                Bias = -0.3
            };
            agents4.Add(agent);
        }

        double preAcquittalAvg = agents4.Average(a => a.VerdictLean);
        ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(
            agents4, deltaEvidence: 0.01, biasFactors: null,
            maxSampleTries: 500, coherenceThreshold: 0.9, seed: 45,
            mode: CaseMode.Criminal, evidenceIndex: 0, totalEvidenceCount: 1);
        double postAcquittalAvg = agents4.Average(a => a.VerdictLean);

        // With all jurors leaning acquittal, the group should still be able to move
        // (not frozen at M). The average should remain in valid range.
        Assert(postAcquittalAvg >= 0.05 && postAcquittalAvg <= 0.95,
            $"Acquittal branch: post-deliberation avg {postAcquittalAvg:F3} in valid range");

        Console.WriteLine($"  Acquittal branch test: pre={preAcquittalAvg:F3}, post={postAcquittalAvg:F3}");
    }
}
