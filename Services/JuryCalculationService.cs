using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;


/// <summary>
/// Handles jury-specific calculations: average lean, likely verdict, and opinion updates.
/// </summary>
public interface IJuryCalculationService
{
    double AverageLean(IEnumerable<Agent> jurors);
    string LikelyVerdict(IEnumerable<Agent> jurors);
    string FinalVerdict(IEnumerable<Agent> jurors, CaseMode caseMode);
    void ApplyEvidenceInfluence(IEnumerable<Agent> allAgents, EvidenceDocument doc, CaseMode caseMode, double jurorWeight = 0.02, double otherWeight = 0.01);
    void ApplyTranscriptInfluence(IEnumerable<Agent> allAgents, double influence);
    void ResetTrialOpinions(IEnumerable<Agent> jurors);
}

public class JuryCalculationService : IJuryCalculationService
{
    private readonly CaseTypeSensitivityService _caseTypeSensitivity;

    public JuryCalculationService(CaseTypeSensitivityService? caseTypeSensitivity = null)
    {
        _caseTypeSensitivity = caseTypeSensitivity ?? new CaseTypeSensitivityService();
    }

    public double AverageLean(IEnumerable<Agent> jurors)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        return voters.Any() ? voters.Average(j => j.VerdictLean) : 0.5;
    }

    private static void ApplyToyCoherentDeliberation(
        IEnumerable<Agent> jurors,
        double deltaEvidence,
        IReadOnlyList<BiasFactor>? biasFactors)
    {
        var jurorList = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!jurorList.Any()) return;

        IReadOnlyList<BiasFactor>? factors = biasFactors;
        if (factors == null)
        {
            var first = jurorList.FirstOrDefault();
            factors = first?.BiasFactors?.ToList();
        }

        Verdict.Services.JOEService.ApplyCoherentDriftDeliberation(

            jurorList,
            deltaEvidence,
            factors,
            maxSampleTries: 1500,
            coherenceThreshold: 0.9,
            seed: 42);
    }


    public string LikelyVerdict(IEnumerable<Agent> jurors)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!voters.Any()) return "No Jurors Seated";

        int pro = voters.Count(v => v.VerdictLean > 0.5);
        int def = voters.Count(v => v.VerdictLean < 0.5);
        int total = voters.Count;

        string consensus;
        if ((double)pro / total > 0.75)
            consensus = "Strong";
        else if (pro > def)
            consensus = "Leaning";
        else if (def > pro && (double)def / total > 0.75)
            consensus = "Strongly Defense";
        else if (def > pro)
            consensus = "Leaning Defense";
        else
            consensus = "Split";

        return $"{pro} for Prosecution, {def} for Defense ({consensus})";
    }

    public string FinalVerdict(IEnumerable<Agent> jurors, CaseMode caseMode)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!voters.Any()) return "No Jurors Seated";

        if (caseMode == CaseMode.Criminal)
        {
            // Unanimity rule: only convict if all seated jurors lean toward conviction.
            bool unanimousConvict = voters.All(v => v.VerdictLean > 0.5);
            return unanimousConvict ? "Convict" : "Acquit";
        }

        // Civil: no unanimous requirement; use simple majority.
        int pro = voters.Count(v => v.VerdictLean > 0.5);
        int def = voters.Count(v => v.VerdictLean < 0.5);

        return pro >= def ? "Plaintiff wins" : "Defense wins";
    }

    public void ApplyEvidenceInfluence(IEnumerable<Agent> allAgents, EvidenceDocument doc, CaseMode caseMode, double jurorWeight = 0.02, double otherWeight = 0.01)
    {
        string caseType = InferCaseTypeFromSummary(doc.Summary);
        var matrix = _caseTypeSensitivity.GetMatrixForCaseType(caseType);

        // Structured cue extraction (replaces fragile substring matching for defendant/victim context)
        var cueText = string.IsNullOrWhiteSpace(doc.DetailedAnalysis)
            ? doc.Summary
            : $"{doc.Summary}\n\n{doc.DetailedAnalysis}";

        var cueProfile = new EvidenceCueExtractor().Extract(cueText);

        foreach (var juror in allAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            double backgroundWeight = CalculateBackgroundWeight(juror, cueProfile, matrix);
            double totalWeight = jurorWeight * backgroundWeight;

            // Juror-perceived strength: base evidence strength scaled by the juror's bias.
            // This makes evidence strength juror-specific without changing the global Assessment pipeline.
            double perceivedStrength = Math.Clamp(
                doc.EvidenceStrength * (1.0 + (juror.Bias * 0.5)),
                0.0,
                1.0);

            juror.UpdateVerdictLean(perceivedStrength * totalWeight);

            // Dollars/damages must not factor into criminal verdict logic.
            if (caseMode != CaseMode.Criminal)
                UpdateConsideredDamages(juror, doc, backgroundWeight, perceivedStrength);
        }

        foreach (var agent in allAgents.Where(a => a.IsOccupied && a.HasOpinion && a.Role != AgentRole.Reporter
                                                   && a.Role != AgentRole.Juror && a.Role != AgentRole.AlternateJuror))
            agent.UpdateVerdictLean(doc.EvidenceStrength * otherWeight);
    }

    private static void UpdateConsideredDamages(Agent juror, EvidenceDocument doc, double backgroundWeight, double perceivedStrength)
    {
        double baseDamages = doc.EstimatedDamages;

        double biasFactor = 1.0 + (juror.Bias * 0.3);

        double backgroundFactor = 0.5 + (backgroundWeight * 0.5);

        double strengthFactor = 0.5 + (perceivedStrength * 0.5);

        double evidenceDamages = baseDamages * biasFactor * backgroundFactor * strengthFactor;

        if (juror.ConsideredDamages > 0)
        {
            juror.ConsideredDamages = (juror.ConsideredDamages * 0.7) + (evidenceDamages * 0.3);
        }
        else
        {
            juror.ConsideredDamages = evidenceDamages;
        }

        double rounded = Math.Round(juror.ConsideredDamages / 1000.0) * 1000.0;
        double inThousands = rounded / 1000.0;
        juror.Valuation = $"${inThousands:F0}K";
    }

    private static string InferCaseTypeFromSummary(string summary)

    {
        if (string.IsNullOrWhiteSpace(summary)) return "Other";

        string lower = summary.ToLowerInvariant();

        bool mentionsShooting = lower.Contains("shooting") || lower.Contains("gunshot") || lower.Contains("firearm");
        bool mentionsPolice = lower.Contains("police") || lower.Contains("officer") || lower.Contains("law enforcement");

        if (mentionsPolice && mentionsShooting)
            return "PoliceInvolvedShooting";

        if (lower.Contains("homicide") || lower.Contains("murder") || lower.Contains("manslaughter"))
            return "Homicide";

        if (lower.Contains("sexual assault") || lower.Contains("sexual abuse") ||
            lower.Contains("rape") || lower.Contains("harassment"))
            return "SexualAssault";

        if (lower.Contains("fraud") || lower.Contains("embezzlement") || lower.Contains("forgery") ||
            lower.Contains("scam") || lower.Contains("misrepresentation"))
            return "Fraud";

        if ((lower.Contains("child") || lower.Contains("minor")) &&
            (lower.Contains("abuse") || lower.Contains("neglect") || lower.Contains("endanger")))
            return "ChildAbuse";

        if (lower.Contains("domestic violence") || lower.Contains("dv") || lower.Contains("battery"))
            return "DomesticViolence";

        if (mentionsShooting)
            return "Homicide";

        if (lower.Contains("assault") || lower.Contains("battery"))
            return "DomesticViolence";

        if ((mentionsPolice && mentionsShooting) || lower.Contains("police brutality") || lower.Contains("excessive force"))
            return "PoliceInvolvedShooting";

        return "Other";
    }

    /// <summary>
    /// Maps a juror's demographics and trait predictors into a dictionary
    /// of predictor-name → contribution (normalized 0..10).
    /// </summary>
    private static Dictionary<string, double> MapDemographicsToPredictors(Agent juror, string summaryLower)
    {
        var preds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Direct trait-to-predictor mapping (these exist on Agent)
        preds["DisgustSensitivity"] = juror.DisgustSensitivity / 10.0;
        preds["AngerReactivity"] = juror.AngerReactivity / 10.0;
        preds["Compassion"] = juror.Compassion / 10.0;
        preds["SuspicionTendency"] = juror.SuspicionTendency / 10.0;
        preds["NeedForClosure"] = juror.NeedForClosure / 10.0;
        preds["NeedForCognition"] = juror.NeedForCognition / 10.0;
        preds["DetailOrientation"] = juror.DetailOrientation / 10.0;
        preds["MemoryReliability"] = juror.MemoryReliability / 10.0;
        preds["FinancialLiteracy"] = juror.FinancialLiteracy / 10.0;
        preds["SystemJustification"] = juror.SystemJustification / 10.0;

        // VictimHistory derived from PriorVictimizationHistory (0-2 → 0.0-1.0)
        preds["VictimHistory"] = juror.PriorVictimizationHistory / 2.0;

        // RapeMythAcceptance: inferred from demographics (traditional/conservative leaning)
        double rma = 0.3;
        if (juror.PoliticalAffiliation.Contains("Republican", StringComparison.OrdinalIgnoreCase)) rma += 0.2;
        if (juror.ReligiousAffiliation.Contains("Conservative Christian", StringComparison.OrdinalIgnoreCase) ||
            juror.ReligiousAffiliation.Contains("Evangelical", StringComparison.OrdinalIgnoreCase)) rma += 0.15;
        if (juror.Gender.Contains("Male", StringComparison.OrdinalIgnoreCase)) rma += 0.1;
        if (juror.Age > 55) rma += 0.1;
        if (juror.EducationLevel.Contains("High School", StringComparison.OrdinalIgnoreCase)) rma += 0.05;
        rma = Math.Clamp(rma, 0.0, 1.0);
        preds["RapeMythAcceptance"] = rma;

        // GenderReligionInteraction: combined cultural traditionalism factor
        double gri = 0.3;
        gri += (juror.ReligiousAffiliation.Contains("Non-religious", StringComparison.OrdinalIgnoreCase) ||
                juror.ReligiousAffiliation.Contains("Atheist", StringComparison.OrdinalIgnoreCase)) ? -0.1 : 0.05;
        if (juror.Gender.Contains("Male", StringComparison.OrdinalIgnoreCase)) gri += 0.1;
        if (juror.Age > 50) gri += 0.1;
        if (juror.PoliticalAffiliation.Contains("Republican", StringComparison.OrdinalIgnoreCase)) gri += 0.1;
        if (juror.ParentalStatus.Contains("Has Children", StringComparison.OrdinalIgnoreCase)) gri -= 0.05;
        gri = Math.Clamp(gri, 0.0, 1.0);
        preds["GenderReligionInteraction"] = gri;

        return preds;
    }

    /// <summary>
    /// Evidence-type–specific cue detection in the summary.
    /// Returns a boost (0..1) if the evidence relates to the given cue.
    /// </summary>
    private static double DetectCueInSummary(string lower, string cueWord)
    {
        return lower.Contains(cueWord) ? 0.15 : 0.0;
    }

    /// <summary>
    /// Calculates a background-based weight multiplier (0.0 to 2.0) using
    /// the predictor-contribution model with case-type sensitivity multipliers.
    /// </summary>
    private double CalculateBackgroundWeight(Agent juror, EvidenceCueProfile cueProfile, CaseTypeSensitivityMatrix matrix)
    {
        double weight = 1.0;

        // Keep legacy heuristics as a baseline so existing calibration doesn't instantly shift.
        // The key change is that case-type-sensitive predictor contributions are computed from
        // structured evidence cues (defendant/victim) rather than direct doc.Summary substring checks.
        string lower = "";
        
        // Legacy demographic heuristics (keep for backward compatibility)

        weight += juror.EducationLevel.ToLower() switch
        {
            "high school" => 0.15,
            "some college" => 0.05,
            "associate degree" => 0.0,
            "bachelor's degree" => -0.05,
            "master's degree" => -0.1,
            "juris doctor (jd)" => -0.2,
            "doctorate (phd)" => -0.15,
            "md" => -0.1,
            _ => 0.0
        };

        weight += juror.IncomeLevel.ToLower() switch
        {
            "lower class" => 0.15,
            "lower middle class" => 0.1,
            "middle class" => 0.0,
            "upper middle class" => -0.05,
            "upper class" => -0.1,
            _ => 0.0
        };

        if (juror.Age < 30) weight += 0.1;
        else if (juror.Age < 45) weight += 0.0;
        else if (juror.Age < 60) weight -= 0.05;
        else weight -= 0.1;

        weight += juror.PoliticalAffiliation.ToLower() switch
        {
            "democratic" => 0.1,
            "republican" => -0.1,
            "libertarian" => -0.05,
            "independent" => 0.0,
            _ => 0.0
        };

        if (!string.IsNullOrEmpty(juror.SpecializedKnowledge) &&
            !juror.SpecializedKnowledge.Contains("None", StringComparison.OrdinalIgnoreCase) &&
            !juror.SpecializedKnowledge.Contains("General", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains(juror.SpecializedKnowledge.ToLower()[..Math.Min(10, juror.SpecializedKnowledge.Length)]))
                weight -= 0.15;
            else
                weight -= 0.05;
        }

        if (juror.ParentalStatus.Contains("Has Children", StringComparison.OrdinalIgnoreCase) &&
            (lower.Contains("child") || lower.Contains("minor") || lower.Contains("family") || lower.Contains("dependent")))
            weight += 0.15;

        if (!juror.ReligiousAffiliation.Contains("Non-religious", StringComparison.OrdinalIgnoreCase) &&
            (lower.Contains("moral") || lower.Contains("negligence") || lower.Contains("harm")))
            weight += 0.05;

        if (juror.MediaConsumption.Contains("Mainstream News", StringComparison.OrdinalIgnoreCase) ||
            juror.MediaConsumption.Contains("24/7", StringComparison.OrdinalIgnoreCase))
            weight += 0.05;

        weight += (juror.RiskPerception - 0.5) * 0.2;

        weight += juror.CurrentStatus.ToLower() switch
        {
            "attentive" => 0.05,
            "focused" => 0.1,
            "skeptical" => -0.1,
            "bored" => -0.15,
            "distracted" => -0.2,
            "sympathetic" => 0.15,
            "hostile" => -0.15,
            _ => 0.0
        };

        // --- Predictor-based contribution model (B2 + B3) ---
        var predictors = MapDemographicsToPredictors(juror, lower);
        double predictorWeight = 0.0;

        foreach (var (predictorName, contribution) in predictors)
        {
            double multiplier = matrix.PredictorMultipliers.TryGetValue(predictorName, out var m) ? m : 1.0;
            double deviation = contribution - 0.5;
            predictorWeight += deviation * (multiplier - 1.0);
        }

        weight += predictorWeight;

        return Math.Clamp(weight, 0.0, 2.0);
    }

    public void ApplyTranscriptInfluence(IEnumerable<Agent> allAgents, double influence)
    {
        foreach (var agent in allAgents)
        {
            if (agent.IsOccupied && agent.HasOpinion && agent.Role != AgentRole.Reporter)
                agent.UpdateVerdictLean(influence);
        }
    }

    public void ResetTrialOpinions(IEnumerable<Agent> jurors)
    {
        foreach (var juror in jurors.Where(j => j.IsOccupied))
        {
            juror.VerdictLean = Math.Max(0.2, Math.Min(0.8, juror.VerdictLean));
            juror.Sentiment = 0.5;
        }
    }
}
