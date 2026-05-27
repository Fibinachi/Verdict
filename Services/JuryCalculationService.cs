using Verdict.Models;

namespace Verdict.Services;


/// <summary>
/// Handles jury-specific calculations: average lean, likely verdict, and opinion updates.
/// </summary>
public interface IJuryCalculationService
{
    string LikelyVerdict(IEnumerable<Agent> jurors, CaseMode mode = CaseMode.Civil);

    double AverageLean(IEnumerable<Agent> jurors);
    string LikelyVerdict(IEnumerable<Agent> jurors);
    void ApplyEvidenceInfluence(IEnumerable<Agent> allAgents, EvidenceDocument doc, double jurorWeight = 0.02, double otherWeight = 0.01);
    void ApplyTranscriptInfluence(IEnumerable<Agent> allAgents, double influence);
    void ResetTrialOpinions(IEnumerable<Agent> jurors);
}

public class JuryCalculationService : IJuryCalculationService
{
    public double AverageLean(IEnumerable<Agent> jurors)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        return voters.Any() ? voters.Average(j => j.VerdictLean) : 0.5;
    }

    /// <summary>
    /// Applies the toy coherence+drift+conformity engine to the provided jurors.
    /// This replaces the simple evidence drift with a structured deliberation-style update.
    /// </summary>
    private static void ApplyToyCoherentDeliberation(
        IEnumerable<Agent> jurors,
        double deltaEvidence,
        IReadOnlyList<BiasFactor>? biasFactors)
    {
        var jurorList = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!jurorList.Any()) return;

        // Use the first juror's bias factors as a global proxy if no explicit list is provided.
        IReadOnlyList<BiasFactor>? factors = biasFactors;
        if (factors == null)
        {
            var first = jurorList.FirstOrDefault();
            factors = first?.BiasFactors?.ToList();
        }

        Verdict.Services.ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(

            jurorList,
            deltaEvidence,
            factors,
            maxSampleTries: 1500,
            coherenceThreshold: 0.9,
            seed: 42);
    }


    public string LikelyVerdict(IEnumerable<Agent> jurors)
        => LikelyVerdict(jurors, CaseMode.Civil);

    public string LikelyVerdict(IEnumerable<Agent> jurors, CaseMode mode = CaseMode.Civil)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!voters.Any()) return "No Jurors Seated";

        int pro = voters.Count(v => v.VerdictLean > 0.5);
        int def = voters.Count(v => v.VerdictLean < 0.5);
        int total = voters.Count;

        // Mode-aware labels: criminal uses Prosecution/Defense, civil uses Plaintiff/Defense
        bool isCriminal = mode == CaseMode.Criminal;
        string proLabel = isCriminal ? "Pro-Prosecution" : "Pro-Plaintiff";
        string defLabel = isCriminal ? "Pro-Defense" : "Pro-Defense";
        string strongProLabel = isCriminal ? "Strongly Pro-Prosecution" : "Strong Pro-Plaintiff";
        string leaningProLabel = isCriminal ? "Leaning Prosecution" : "Leaning Plaintiff";
        string strongDefLabel = "Strongly Defense";
        string leaningDefLabel = "Leaning Defense";

        string consensus;
        if ((double)pro / total > 0.75)
            consensus = strongProLabel;
        else if (pro > def)
            consensus = leaningProLabel;
        else if (def > pro && (double)def / total > 0.75)
            consensus = strongDefLabel;
        else if (def > pro)
            consensus = leaningDefLabel;
        else
            consensus = "Split";

        return $"{pro} {proLabel}, {def} {defLabel} ({consensus})";
    }

    public void ApplyEvidenceInfluence(IEnumerable<Agent> allAgents, EvidenceDocument doc, double jurorWeight = 0.02, double otherWeight = 0.01)
    {
        foreach (var juror in allAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            // Calculate a per-juror weight based on their background demographics.
            // Each juror interprets evidence differently depending on who they are.
            double backgroundWeight = CalculateBackgroundWeight(juror, doc);

            // Each juror assesses witness credibility through their own biases
            double perJurorCredibility = JurorCredibilityService.CalculatePerJurorCredibility(juror, doc);

            // Probative value is adjusted by the juror's perception of credibility
            double perceivedProbative = doc.ProbativeValue;
            if (doc.IsTestimonial)
            {
                // Blend objective credibility with juror's biased perception
                perceivedProbative *= 0.5 + perJurorCredibility * 0.5;
            }

            // Media-type sensitivity: some jurors are more swayed by certain evidence types
            double mediaSensitivity = JurorCredibilityService.CalculateMediaTypeSensitivity(juror, doc.EvidenceCategory);

            // Total weight combines background, credibility perception, and media sensitivity
            double totalWeight = jurorWeight * backgroundWeight * mediaSensitivity;
            juror.UpdateVerdictLean(perceivedProbative * totalWeight);

            // Update the juror's considered damages based on the evidence
            // Each juror arrives at a different amount based on their background
            UpdateConsideredDamages(juror, doc, backgroundWeight, perJurorCredibility);
        }

        foreach (var agent in allAgents.Where(a => a.IsOccupied && a.HasOpinion && a.Role != AgentRole.Reporter
                                                    && a.Role != AgentRole.Juror && a.Role != AgentRole.AlternateJuror))
            agent.UpdateVerdictLean(doc.ProbativeValue * otherWeight);
    }

    /// <summary>
    /// Updates a juror's personal considered damages amount based on new evidence.
    /// Each juror arrives at a different number based on their background, bias,
    /// the evidence strength, and their perception of witness credibility.
    /// </summary>
    private static void UpdateConsideredDamages(Agent juror, EvidenceDocument doc, double backgroundWeight, double perJurorCredibility)
    {
        // Base damages from the evidence document
        double baseDamages = doc.EstimatedDamages;

        // Apply the juror's personal bias (negative bias = defense-leaning = lower damages)
        double biasFactor = 1.0 + (juror.Bias * 0.3); // Bias ranges -1 to 1, so factor ranges 0.7 to 1.3

        // Apply background weight (more susceptible jurors award more)
        double backgroundFactor = 0.5 + (backgroundWeight * 0.5); // Maps 0-2 weight to 0.5-1.5 factor

        // Apply probative value (jurors weigh evidence they find probative more heavily)
        double probativeFactor = 0.5 + (doc.ProbativeValue * 0.5); // Maps 0-1 probative to 0.5-1.0 factor

        // For testimonial evidence, credibility perception affects damages
        double credibilityFactor = 1.0;
        if (doc.IsTestimonial)
        {
            credibilityFactor = 0.5 + perJurorCredibility * 0.5;
        }

        // Calculate the juror's considered damages for this piece of evidence
        double evidenceDamages = baseDamages * biasFactor * backgroundFactor * probativeFactor * credibilityFactor;

        // Blend with existing considered damages (weighted average, new evidence has 30% weight)
        if (juror.ConsideredDamages > 0)
        {
            juror.ConsideredDamages = (juror.ConsideredDamages * 0.7) + (evidenceDamages * 0.3);
        }
        else
        {
            juror.ConsideredDamages = evidenceDamages;
        }

        // Update the Valuation string for display
        // Round to nearest thousand and display as $XK
        double rounded = Math.Round(juror.ConsideredDamages / 1000.0) * 1000.0;
        double inThousands = rounded / 1000.0;
        juror.Valuation = $"${inThousands:F0}K";
    }

    /// <summary>
    /// Calculates a background-based weight multiplier (0.0 to 2.0) for how strongly
    /// a juror's demographics influence their reaction to evidence.
    /// 1.0 = average influence, &gt;1.0 = more susceptible, &lt;1.0 = more resistant.
    /// Default adjustments are based on juror bias research documented in docs/JurorBiasResearch.md
    /// </summary>
    private static double CalculateBackgroundWeight(Agent juror, EvidenceDocument doc)
    {
        double weight = 1.0;
        string lower = doc.Summary.ToLower();

        // --- Education level: more educated jurors are more skeptical of weak evidence ---
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

        // --- Income level: higher income jurors may be more defense-oriented in civil cases ---
        weight += juror.IncomeLevel.ToLower() switch
        {
            "lower class" => 0.15,
            "lower middle class" => 0.1,
            "middle class" => 0.0,
            "upper middle class" => -0.05,
            "upper class" => -0.1,
            _ => 0.0
        };

        // --- Age: younger jurors may be more sympathetic to plaintiffs, older more skeptical ---
        if (juror.Age < 30) weight += 0.1;
        else if (juror.Age < 45) weight += 0.0;
        else if (juror.Age < 60) weight -= 0.05;
        else weight -= 0.1;

        // --- Political affiliation ---
        weight += juror.PoliticalAffiliation.ToLower() switch
        {
            "democratic" => 0.1,
            "republican" => -0.1,
            "libertarian" => -0.05,
            "independent" => 0.0,
            _ => 0.0
        };

        // --- Specialized knowledge: experts are harder to sway ---
        if (!string.IsNullOrEmpty(juror.SpecializedKnowledge) &&
            !juror.SpecializedKnowledge.Contains("None", StringComparison.OrdinalIgnoreCase) &&
            !juror.SpecializedKnowledge.Contains("General", StringComparison.OrdinalIgnoreCase))
        {
            // If the evidence relates to their expertise, they're more discerning
            if (lower.Contains(juror.SpecializedKnowledge.ToLower()[..Math.Min(10, juror.SpecializedKnowledge.Length)]))
                weight -= 0.15;
            else
                weight -= 0.05;
        }

        // --- Parental status: parents may be more sympathetic in cases involving children ---
        if (juror.ParentalStatus.Contains("Has Children", StringComparison.OrdinalIgnoreCase) &&
            (lower.Contains("child") || lower.Contains("minor") || lower.Contains("family") || lower.Contains("dependent")))
            weight += 0.15;

        // --- Religious affiliation: can affect views on certain evidence types ---
        if (!juror.ReligiousAffiliation.Contains("Non-religious", StringComparison.OrdinalIgnoreCase) &&
            (lower.Contains("moral") || lower.Contains("negligence") || lower.Contains("harm")))
            weight += 0.05;

        // --- Media consumption: heavy news consumers may have stronger preconceptions ---
        if (juror.MediaConsumption.Contains("Mainstream News", StringComparison.OrdinalIgnoreCase) ||
            juror.MediaConsumption.Contains("24/7", StringComparison.OrdinalIgnoreCase))
            weight += 0.05;

        // --- Risk perception: risk-averse jurors lean defense, risk-tolerant lean plaintiff ---
        weight += (juror.RiskPerception - 0.5) * 0.2;

        // --- Current status: attentive jurors weigh evidence more carefully ---
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

        // Clamp the final weight to a reasonable range
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
