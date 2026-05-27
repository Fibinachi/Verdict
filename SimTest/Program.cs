using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Verdict.Models;
using Verdict.Services;

var casePath = @"DefaultCases\apple-river.jur";
var json = File.ReadAllText(casePath);
var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
var caseFile = JsonSerializer.Deserialize<CaseFile>(json, opts)!;

// Ensure models are available and wired with DeepSeek key
caseFile.EnsureCollectionsInitialized();
caseFile.AddDefaultModels();
bool hasKey = !string.IsNullOrWhiteSpace(caseFile.AvailableModels[0].ApiKey);
Console.WriteLine($"DeepSeek API key: {(hasKey ? "YES (DEEPSEEK_API_KEY set)" : "NO — set DEEPSEEK_API_KEY env var for live LLM")}");

Console.WriteLine($"Loaded: {caseFile.CaseName} ({caseFile.Mode})");
Console.WriteLine($"Evidence: {caseFile.Evidence?.Count}, Charges: {caseFile.Verdict?.Charges?.Count}\n");

var demo = new JuryDemographicsService();
var delib = new DeliberationService();
var providers = ProviderDiscoveryService.GetAvailableProviders();
var ai = new AgentInteractionService(providers);

for (int sim = 0; sim < 3; sim++)
{
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"SIMULATION {sim + 1}");
    Console.WriteLine(new string('=', 60));
    
    var jurors = demo.GenerateJuryPanel(12, 2, "St. Croix County", "Wisconsin");
    
    foreach (var j in jurors.Take(12))
        foreach (var d in caseFile.Evidence!)
            if (!j.ExhibitLog.Contains($"Exhibit {d.ExhibitNumber}: {d.FileName}"))
                j.ExhibitLog.Add($"Exhibit {d.ExhibitNumber}: {d.FileName} - {d.Summary}");

    Console.WriteLine("\nJURY:");
    foreach (var j in jurors.Take(12))
        Console.WriteLine($"  {j.Name,-22} {j.Age,2} {j.Gender,-6} {j.PoliticalAffiliation,-12} {j.EducationLevel,-18} L:{j.VerdictLean:F2}");

    int cons = jurors.Count(j => j.PoliticalAffiliation == "Republican");
    int libs = jurors.Count(j => j.PoliticalAffiliation == "Democratic");
    int over50 = jurors.Count(j => j.Age >= 50);
    int parents = jurors.Count(j => j.ParentalStatus == "Has Children");
    Console.WriteLine($"  AvgAge:{jurors.Take(12).Average(j=>j.Age):F0} GOP:{cons} Dem:{libs} Over50:{over50} Parents:{parents}");

    var spoken = new HashSet<Guid>();
    var hist = new List<string>();
    for (int r = 1; r <= 6; r++)
    {
        if (spoken.Count >= jurors.Count(j => j.CanVote)) spoken.Clear();
        try
        {
            var result = await delib.ExecuteTurnAsync(jurors, spoken, hist, caseFile, ai, r);
            int pro = jurors.Count(j => j.VerdictLean > 0.5);
            int def = jurors.Count(j => j.VerdictLean < 0.5);
            string stmt = result.Statement.Length > 55 ? result.Statement[..52] + "..." : result.Statement;
            Console.WriteLine($"  R{r}: {result.SpeakingJuror.Name,-22} [{result.Direction:+0;-0}] \"{stmt}\" | G:{pro} NG:{def}");
            if (delib.IsDeliberationComplete(jurors, r, caseFile.DeliberationRounds, out _, out _, caseFile.Mode)) break;
        }
        catch (Exception ex) { Console.WriteLine($"  R{r}: ERR - {ex.Message}"); break; }
    }

    int fpro = jurors.Take(12).Count(j => j.VerdictLean > 0.5);
    int fdef = jurors.Take(12).Count(j => j.VerdictLean < 0.5);
    double avgL = jurors.Take(12).Average(j => j.VerdictLean);
    string verdict = fpro > fdef ? "GUILTY" : (fdef > fpro ? "NOT GUILTY" : "HUNG");
    Console.WriteLine($"\n  VERDICT: {verdict} | G:{fpro} NG:{fdef} AvgLean:{avgL:F3}");
    Console.WriteLine("  Final positions:");
    foreach (var j in jurors.Take(12).OrderByDescending(j => j.VerdictLean))
    {
        string pos = j.VerdictLean > 0.85 ? "GUILTY" :
                     j.VerdictLean > 0.50 ? "REASONABLE DOUBT" : "NOT GUILTY";
        Console.WriteLine($"    {j.Name,-22} L:{j.VerdictLean:F3} {pos}");
    }
}

Console.WriteLine("\nDONE - All simulations complete.");

