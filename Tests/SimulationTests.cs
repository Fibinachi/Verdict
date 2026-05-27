using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Tests;

public static class SimulationTests
{
    public static async Task RunDeliberationSimulations()
    {
        Console.WriteLine("=== DELIBERATION SIMULATION TESTS ===\n");

        var casePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "DefaultCases", "apple-river.jur");

        if (!File.Exists(casePath))
        {
            Console.WriteLine($"ERROR: Case file not found at {casePath}");
            return;
        }

        var json = File.ReadAllText(casePath);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var caseFile = JsonSerializer.Deserialize<CaseFile>(json, opts);
        if (caseFile == null) { Console.WriteLine("ERROR: Deserialization failed."); return; }

        Console.WriteLine($"Case: {caseFile.CaseName} ({caseFile.Mode})");
        Console.WriteLine($"Evidence items: {caseFile.Evidence?.Count ?? 0}");
        Console.WriteLine($"Charges: {caseFile.Verdict?.Charges?.Count ?? 0}\n");

        // Ensure the case has available models (DeepSeek by default)
        caseFile.EnsureCollectionsInitialized();

        // Try to load models from the source Models folder first (has API keys)
        var modelsFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Models");
        if (Directory.Exists(modelsFolder))
        {
            foreach (var jsonFile in Directory.GetFiles(modelsFolder, "*.json"))
            {
                try
                {
                    var modelJson = File.ReadAllText(jsonFile);
                    var model = JsonSerializer.Deserialize<AIModelConfiguration>(modelJson, opts);
                    if (model != null && !caseFile.AvailableModels.Any(m => m.FriendlyName == model.FriendlyName))
                        caseFile.AvailableModels.Add(model);
                }
                catch { /* skip malformed JSON */ }
            }
        }

        var providers = ProviderDiscoveryService.GetAvailableProviders();

        // Remove models whose providers are not active
        caseFile.AvailableModels.RemoveAll(m =>
            !providers.Any(p => p.ProviderName.Equals(m.Provider, StringComparison.OrdinalIgnoreCase)));

        // If no valid models remain, add DeepSeek default (reads DEEPSEEK_API_KEY env var)
        if (caseFile.AvailableModels.Count == 0)
        {
            caseFile.AddDefaultModels();
            bool hasKey = !string.IsNullOrWhiteSpace(caseFile.AvailableModels[0].ApiKey);
            Console.WriteLine($"DeepSeek model loaded. API key present: {(hasKey ? "YES (DEEPSEEK_API_KEY)" : "NO — set DEEPSEEK_API_KEY env var for live LLM tests")}");
        }

        var defaultModel = caseFile.AvailableModels.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.ApiKey))
                        ?? caseFile.AvailableModels.LastOrDefault()
                        ?? caseFile.AvailableModels.FirstOrDefault();

        Console.WriteLine($"Models available: {caseFile.AvailableModels.Count}");
        if (defaultModel != null)
        {
            bool hasApiKey = !string.IsNullOrWhiteSpace(defaultModel.ApiKey);
            Console.WriteLine($"Default model: {defaultModel.FriendlyName} ({defaultModel.Provider}) — API key: {(hasApiKey ? "YES" : "NO (will skip LLM calls)")}");
        }

        var demoService = new JuryDemographicsService();
        var delibService = new DeliberationService();
        var aiService = new AgentInteractionService(providers);

        for (int sim = 0; sim < 3; sim++)
        {
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"SIMULATION {sim + 1}");
            Console.WriteLine(new string('=', 60));

            // Generate a fresh jury
            var jurors = demoService.GenerateJuryPanel(12, 2, "St. Croix County", "Wisconsin");

            // Seed each juror with evidence and assign the default model
            foreach (var juror in jurors.Take(12))
            {
                // Assign the configured model to each juror
                if (defaultModel != null)
                    juror.SelectedModel = defaultModel.FriendlyName;

                foreach (var doc in caseFile.Evidence ?? new())
                {
                    string entry = $"Exhibit {doc.ExhibitNumber}: {doc.FileName} - {doc.Summary}";
                    if (!juror.ExhibitLog.Contains(entry))
                        juror.ExhibitLog.Add(entry);

                    // Also seed TrialEvents with rich context so ExtractKeyPoints finds evidence
                    string analysisContent = !string.IsNullOrWhiteSpace(doc.DetailedAnalysis)
                        ? doc.DetailedAnalysis
                        : doc.Summary;
                    string richContext = $"[Exhibit {doc.ExhibitNumber}: {doc.FileName}] {analysisContent}";
                    if (!juror.TrialEvents.Any(e => e.Content == richContext))
                        juror.RecordTrialEvent(richContext, juror.Bias * 0.1);
                }
            }

            // Display jury composition
            Console.WriteLine("\nJURY PANEL:");
            foreach (var j in jurors.Take(12))
            {
                Console.WriteLine($"  {j.Name,-22} {j.Age,2} {j.Gender,-6} {j.PoliticalAffiliation,-12} {j.EducationLevel,-18} L:{j.VerdictLean:F2}");
            }

            int avgAge = (int)jurors.Take(12).Average(j => j.Age);
            int cons = jurors.Count(j => j.PoliticalAffiliation == "Republican");
            int libs = jurors.Count(j => j.PoliticalAffiliation == "Democratic");
            int male = jurors.Count(j => j.Gender == "Male");
            int over50 = jurors.Count(j => j.Age >= 50);
            int parents = jurors.Count(j => j.ParentalStatus == "Has Children");
            Console.WriteLine($"  AvgAge:{avgAge} Male:{male} GOP:{cons} Dem:{libs} Over50:{over50} Parents:{parents}");

            // Run deliberation
            var spokenIds = new HashSet<Guid>();
            var history = new List<string>();
            bool completed = false;

            for (int round = 1; round <= Math.Min(caseFile.DeliberationRounds, 6); round++)
            {
                if (spokenIds.Count >= delibService.GetDeliberatingJurors(jurors).Count())
                    spokenIds.Clear();

                try
                {
                    var result = await delibService.ExecuteTurnAsync(
                        jurors, spokenIds, history, caseFile, aiService, round);

                    int pro = jurors.Count(j => j.VerdictLean > 0.5);
                    int def = jurors.Count(j => j.VerdictLean < 0.5);
                    string stmt = result.Statement.Length > 55
                        ? result.Statement[..52] + "..."
                        : result.Statement;
                    string lean = result.Direction > 0 ? "PROS" : (result.Direction < 0 ? "DEF" : "NEUT");

                    Console.WriteLine($"  R{round}: {result.SpeakingJuror.Name,-22} [{lean}] \"{stmt}\" | G:{pro} NG:{def}");

                    if (delibService.IsDeliberationComplete(jurors, round, caseFile.DeliberationRounds, out double spread, out bool isHung, caseFile.Mode))
                    {
                        Console.WriteLine($"  => Complete: spread={spread:F3} hung={isHung}");
                        completed = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  R{round}: ERROR - {ex.Message}");
                    break;
                }
            }

            if (!completed)
                Console.WriteLine($"  => Max rounds reached");

            // Final tally
            int fpro = jurors.Take(12).Count(j => j.VerdictLean > 0.5);
            int fdef = jurors.Take(12).Count(j => j.VerdictLean < 0.5);
            int fund = jurors.Take(12).Count(j => Math.Abs(j.VerdictLean - 0.5) < 0.01 && j.CanVote);
            double avgL = jurors.Take(12).Average(j => j.VerdictLean);

            string verdict = fpro > fdef ? "GUILTY" : (fdef > fpro ? "NOT GUILTY" : "HUNG");
            Console.WriteLine($"\n  VERDICT: {verdict} | G:{fpro} NG:{fdef}" + (fund > 0 ? $" U:{fund}" : "") + $" AvgLean:{avgL:F3}");

            Console.WriteLine("  Juror shifts:");
            foreach (var j in jurors.Take(12).OrderByDescending(j => j.VerdictLean))
            {
                string pos = j.VerdictLean > 0.55 ? "GUILTY" : (j.VerdictLean < 0.45 ? "NOT GUILTY" : "UNDECIDED");
                Console.WriteLine($"    {j.Name,-22} L:{j.VerdictLean:F3} B:{j.Bias:+0.00;-0.00} => {pos}");
            }
        }

        Console.WriteLine("\n=== ALL SIMULATIONS COMPLETE ===");
    }
}
