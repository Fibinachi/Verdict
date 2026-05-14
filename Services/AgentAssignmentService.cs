using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Service for dynamically analyzing a case and assigning agents to appropriate roles.
/// Phase 5 of the LegalMind integration.
/// </summary>
public interface IAgentAssignmentService
{
    /// <summary>
    /// Analyzes the case and produces recommended agent role assignments.
    /// </summary>
    AgentAssignmentPlan AnalyzeCase(CaseFile caseFile);

    /// <summary>
    /// Applies an assignment plan to the courtroom's agent collections.
    /// </summary>
    void ApplyAssignmentPlan(AgentAssignmentPlan plan, 
        ICollection<Agent> judgeArea,
        ICollection<Agent> jurors, 
        ICollection<Agent> defenseTeam,
        ICollection<Agent> prosecutionTeam,
        ICollection<Agent> gallery);

    /// <summary>
    /// Generates a default set of agent profiles based on case type.
    /// </summary>
    List<Agent> GenerateDefaultAgents(CaseFile caseFile);
}

/// <summary>
/// Contains the recommended role assignments for a case.
/// </summary>
public class AgentAssignmentPlan
{
    /// <summary>
    /// Description of the recommended case strategy.
    /// </summary>
    public string StrategyDescription { get; set; } = string.Empty;

    /// <summary>
    /// Recommended number of defense attorneys.
    /// </summary>
    public int RecommendedDefenseAttorneys { get; set; } = 1;

    /// <summary>
    /// Recommended number of prosecution/plaintiff attorneys.
    /// </summary>
    public int RecommendedProsecutionAttorneys { get; set; } = 1;

    /// <summary>
    /// Types of expert witnesses recommended.
    /// </summary>
    public List<string> RecommendedExperts { get; set; } = new();

    /// <summary>
    /// Suggested jury instructions focus areas.
    /// </summary>
    public List<string> JuryFocusAreas { get; set; } = new();

    /// <summary>
    /// Key evidence themes to emphasize.
    /// </summary>
    public List<string> EvidenceThemes { get; set; } = new();

    /// <summary>
    /// The likely legal standard that applies.
    /// </summary>
    public LegalStandard ApplicableStandard { get; set; } = LegalStandard.PreponderanceOfEvidence;

    /// <summary>
    /// Overall case complexity rating (1-10).
    /// </summary>
    public int ComplexityRating { get; set; } = 5;
}

public class AgentAssignmentService : IAgentAssignmentService
{
    public AgentAssignmentPlan AnalyzeCase(CaseFile caseFile)
    {
        var plan = new AgentAssignmentPlan();

        // Analyze case mode
        if (caseFile.Mode == CaseMode.Criminal)
        {
            plan.ApplicableStandard = LegalStandard.BeyondReasonableDoubt;
            plan.RecommendedDefenseAttorneys = 2; // Criminal defense often requires more resources
            plan.RecommendedProsecutionAttorneys = 2;
            plan.RecommendedExperts.Add("Forensic Expert");
            plan.RecommendedExperts.Add("Character Witness");
            plan.JuryFocusAreas.Add("Reasonable Doubt");
            plan.JuryFocusAreas.Add("Burden of Proof");
            plan.ComplexityRating = 8;
            plan.StrategyDescription = "Criminal case requiring beyond reasonable doubt standard. " +
                                       "Focus on evidence chain of custody and witness credibility.";
        }
        else
        {
            plan.ApplicableStandard = LegalStandard.PreponderanceOfEvidence;
            plan.RecommendedDefenseAttorneys = 1;
            plan.RecommendedProsecutionAttorneys = 1;

            // Analyze evidence for civil case type
            var evidenceSummaries = string.Join(" ", caseFile.Evidence.Select(e => e.Summary ?? "").ToList()).ToLower();
            
            if (evidenceSummaries.Contains("medical") || evidenceSummaries.Contains("injury") || evidenceSummaries.Contains("hospital"))
            {
                plan.RecommendedExperts.Add("Medical Expert");
                plan.JuryFocusAreas.Add("Medical Causation");
                plan.JuryFocusAreas.Add("Damages Assessment");
                plan.ComplexityRating = 7;
                plan.StrategyDescription = "Personal injury/medical case. Expert testimony on causation and damages is critical.";
            }
            else if (evidenceSummaries.Contains("contract") || evidenceSummaries.Contains("agreement"))
            {
                plan.RecommendedExperts.Add("Contract Law Expert");
                plan.JuryFocusAreas.Add("Contract Interpretation");
                plan.JuryFocusAreas.Add("Good Faith Dealing");
                plan.ComplexityRating = 6;
                plan.StrategyDescription = "Contract dispute. Focus on the written terms and parties' intent.";
            }
            else if (evidenceSummaries.Contains("employment") || evidenceSummaries.Contains("discrimination"))
            {
                plan.RecommendedExperts.Add("HR/Employment Expert");
                plan.RecommendedExperts.Add("Damages Expert");
                plan.JuryFocusAreas.Add("Workplace Standards");
                plan.JuryFocusAreas.Add("Discrimination Law");
                plan.ComplexityRating = 7;
                plan.StrategyDescription = "Employment case. Focus on workplace policies and discriminatory intent.";
            }
            else if (evidenceSummaries.Contains("property") || evidenceSummaries.Contains("real estate"))
            {
                plan.RecommendedExperts.Add("Real Estate Appraiser");
                plan.JuryFocusAreas.Add("Property Valuation");
                plan.JuryFocusAreas.Add("Property Rights");
                plan.ComplexityRating = 5;
                plan.StrategyDescription = "Property dispute. Focus on ownership documentation and valuation.";
            }
            else if (evidenceSummaries.Contains("insurance") || evidenceSummaries.Contains("bad faith"))
            {
                plan.RecommendedExperts.Add("Insurance Industry Expert");
                plan.JuryFocusAreas.Add("Insurance Bad Faith");
                plan.JuryFocusAreas.Add("Policy Interpretation");
                plan.ComplexityRating = 6;
                plan.StrategyDescription = "Insurance case. Focus on policy terms and claims handling practices.";
            }
            else
            {
                plan.RecommendedExperts.Add("General Liability Expert");
                plan.JuryFocusAreas.Add("Standard of Care");
                plan.JuryFocusAreas.Add("Causation");
                plan.ComplexityRating = 5;
                plan.StrategyDescription = "General civil case. Focus on preponderance of evidence standard.";
            }
        }

        // Analyze jurisdiction
        if (caseFile.Jurisdiction == JurisdictionType.Federal)
        {
            plan.ComplexityRating = Math.Min(10, plan.ComplexityRating + 2);
            plan.StrategyDescription += " Federal jurisdiction adds procedural complexity.";
        }

        // Analyze evidence volume
        if (caseFile.Evidence.Count > 10)
        {
            plan.ComplexityRating = Math.Min(10, plan.ComplexityRating + 1);
            plan.RecommendedExperts.Add("Evidence/Records Custodian");
            plan.StrategyDescription += " High volume of evidence requires careful organization.";
        }

        // Generate evidence themes from evidence
        plan.EvidenceThemes = caseFile.Evidence
            .Select(e => e.Summary?.Split(' ').Take(5).Aggregate((a, b) => a + " " + b) ?? "Unknown")
            .Take(3)
            .ToList();

        return plan;
    }

    public void ApplyAssignmentPlan(AgentAssignmentPlan plan,
        ICollection<Agent> judgeArea,
        ICollection<Agent> jurors,
        ICollection<Agent> defenseTeam,
        ICollection<Agent> prosecutionTeam,
        ICollection<Agent> gallery)
    {
        // Ensure defense team has enough lawyers
        var defenseLawyers = defenseTeam.Where(a => a.Role == AgentRole.Lawyer).ToList();
        int defenseSlotsToAdd = plan.RecommendedDefenseAttorneys - defenseLawyers.Count(l => !l.IsOccupied);
        for (int i = 0; i < defenseSlotsToAdd; i++)
        {
            defenseTeam.Add(new Agent { Role = AgentRole.Lawyer, Name = "+" });
        }

        // Ensure prosecution team has enough lawyers
        var prosecutionLawyers = prosecutionTeam.Where(a => a.Role == AgentRole.Lawyer).ToList();
        int prosecutionSlotsToAdd = plan.RecommendedProsecutionAttorneys - prosecutionLawyers.Count(l => !l.IsOccupied);
        for (int i = 0; i < prosecutionSlotsToAdd; i++)
        {
            prosecutionTeam.Add(new Agent { Role = AgentRole.Lawyer, Name = "+" });
        }
    }

    public List<Agent> GenerateDefaultAgents(CaseFile caseFile)
    {
        var agents = new List<Agent>();
        var plan = AnalyzeCase(caseFile);

        // Generate expert witness profiles based on recommended experts
        foreach (var expertType in plan.RecommendedExperts)
        {
            var expert = new Agent
            {
                Name = $"Dr. {GenerateExpertLastName()}",
                Role = AgentRole.Witness,
                IsOccupied = true,
                Occupation = expertType,
                SpecializedKnowledge = expertType,
                EducationLevel = "Doctorate",
                IncomeLevel = "Upper Class",
                Bias = 0.0,
                VerdictLean = 0.5,
                Sentiment = 0.5
            };

            expert.Profile = $"A qualified {expertType} with extensive experience and credentials. " +
                            $"Holds advanced degrees and has testified in numerous cases.";
            expert.SystemPrompt = $"You are a {expertType} serving as an expert witness. " +
                                 "Provide clear, authoritative testimony on matters within your expertise. " +
                                 "Be objective and base your opinions on established facts and methodologies.";

            agents.Add(expert);
        }

        // Generate a judge profile if needed
        var judge = new Agent
        {
            Name = $"Honorable {GenerateJudgeLastName()}",
            Role = AgentRole.Judge,
            IsOccupied = true,
            Occupation = "Judge",
            EducationLevel = "Juris Doctor",
            IncomeLevel = "Upper Class",
            JudicialTemperament = "Fair and patient, but maintains strict courtroom decorum.",
            JudicialRulings = "Has presided over numerous civil and criminal trials.",
            Bias = 0.0,
            VerdictLean = 0.5,
            Sentiment = 0.5,
            Profile = "An experienced judge known for fair rulings and thorough legal analysis."
        };
        judge.SystemPrompt = "You are the presiding judge. Maintain courtroom order, rule on objections, " +
                            "and ensure a fair trial. Be authoritative but impartial.";
        agents.Add(judge);

        return agents;
    }

    private string GenerateExpertLastName()
    {
        var lastNames = new[] { "Mitchell", "Harper", "Bennett", "Crawford", "Fletcher", 
                                "Marshall", "Preston", "Whitaker", "Ellison", "Bradford" };
        return lastNames[new Random().Next(lastNames.Length)];
    }

    private string GenerateJudgeLastName()
    {
        var lastNames = new[] { "Roberts", "Anderson", "Thompson", "Carter", "Mitchell", 
                                "Harrison", "Wells", "Knight", "Sullivan", "Davidson" };
        return lastNames[new Random().Next(lastNames.Length)];
    }
}
