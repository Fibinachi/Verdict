using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Result of a single deliberation turn.
/// </summary>
public class DeliberationTurnResult
{
    public Agent SpeakingJuror { get; set; } = null!;
    public string Statement { get; set; } = string.Empty;
    public double Persuasiveness { get; set; }
    public double Direction { get; set; } // +1 = prosecution, -1 = defense, 0 = neutral
    public bool IsAmbivalent { get; set; }
    public List<(Agent Juror, double Shift)> InfluenceShifts { get; set; } = new();
    public int RoundNumber { get; set; }
    public bool UsedFallback { get; set; }
    public string? FallbackReason { get; set; }
}

/// <summary>
/// Manages jury deliberation: juror selection, LLM-powered deliberation statements,
/// conformity influence propagation, and edge-case handling.
/// </summary>
public interface IDeliberationService
{
    /// <summary>
    /// Selects the next juror to speak based on opinion strength, skipping those who already spoke this round.
    /// Returns null if all jurors have spoken or no jurors available.
    /// </summary>
    Agent? SelectNextSpeaker(IEnumerable<Agent> jurors, HashSet<Guid> spokenIds);

    /// <summary>
    /// Returns the jurors that should participate in deliberation.
    /// Alternates are excluded when a full panel of 12 seated jurors exists.
    /// </summary>
    IEnumerable<Agent> GetDeliberatingJurors(IEnumerable<Agent> jurors);

    /// <summary>
    /// Builds the deliberation prompt for a specific juror, incorporating their profile,
    /// evidence log, prior deliberation turns, and case context.
    /// </summary>
    string BuildDeliberationPrompt(Agent juror, CaseFile caseData, List<string> deliberationHistory);

    /// <summary>
    /// Executes a single deliberation turn: selects speaker, generates LLM statement,
    /// parses response, applies influence to all jurors.
    /// </summary>
    Task<DeliberationTurnResult> ExecuteTurnAsync(
        IEnumerable<Agent> jurors,
        HashSet<Guid> spokenIds,
        List<string> deliberationHistory,
        CaseFile caseData,
        IAgentInteractionService agentInteraction,
        int roundNumber);

    /// <summary>
    /// Applies conformity influence from a speaking juror's statement to all other jurors.
    /// Each juror's VerdictLean shifts based on the speaker's credibility, persuasiveness,
    /// and the listener's susceptibility (weaker opinions = more persuadable).
    /// </summary>
    List<(Agent Juror, double Shift)> ApplyConformityInfluence(
        IEnumerable<Agent> jurors,
        Agent speaker,
        double direction,
        double persuasiveness,
        string statement,
        int roundNumber);

    /// <summary>
    /// Parses a juror's deliberation statement for lean direction and persuasiveness.
    /// Handles ambivalence when VerdictLean is near 0.5 (within 0.07 threshold).
    /// </summary>
    (double Direction, double Persuasiveness, bool IsAmbivalent) ParseStatement(
        string statement, Agent juror);

    /// <summary>
    /// Extracts key points from a juror's trial events for use in deliberation arguments.
    /// Falls back to exhibit log, then to case evidence summaries if both are empty.
    /// </summary>
    List<string> ExtractKeyPoints(Agent juror, CaseFile caseData);

    /// <summary>
    /// Determines if consensus has been reached (all juror leans within 0.10 spread)
    /// or if max rounds have been exceeded.
    /// </summary>
    bool IsDeliberationComplete(IEnumerable<Agent> jurors, int currentRound, int maxRounds,
        out double spread, out bool isHung, CaseMode mode = CaseMode.Civil);
}

public class DeliberationService : IDeliberationService
{
    private const double AmbivalenceThreshold = 0.07; // From plans.md
    private const double ConsensusThreshold = 0.10;
    private const double BaseShiftFactor = 0.12; // Stronger peer influence

    public Agent? SelectNextSpeaker(IEnumerable<Agent> jurors, HashSet<Guid> spokenIds)
    {
        return GetDeliberatingJurors(jurors)
            .Where(j => j.CanVote && !spokenIds.Contains(j.AgentId))
            .OrderByDescending(j => Math.Abs(j.VerdictLean - 0.5))
            .FirstOrDefault();
    }

    public IEnumerable<Agent> GetDeliberatingJurors(IEnumerable<Agent> jurors)
    {
        var seatedJurors = jurors
            .Where(j => j.IsOccupied && j.Role == AgentRole.Juror)
            .ToList();

        if (seatedJurors.Count >= 12)
            return seatedJurors;

        var alternates = jurors
            .Where(j => j.IsOccupied && j.Role == AgentRole.AlternateJuror)
            .ToList();

        return seatedJurors.Concat(alternates);
    }

    public string BuildDeliberationPrompt(Agent juror, CaseFile caseData, List<string> deliberationHistory)
    {
        bool isCriminal = caseData.Mode == CaseMode.Criminal;
        string sideLabel = isCriminal ? "prosecution/guilty" : "plaintiff/liable";
        string oppLabel = isCriminal ? "defense/not guilty" : "defense/not liable";

        string verdictIssue = isCriminal
            ? string.Join("; ", caseData.Verdict?.Charges?.Select(c => c.Name) ?? new[] { "Charges" })
            : string.Join("; ", caseData.Verdict?.CausesOfAction?.Select(c => c.Name) ?? new[] { "Claims" });

        // Extract key points from trial events, fall back to case evidence summaries
        var keyPoints = ExtractKeyPoints(juror, caseData);
        string evidenceContext = keyPoints.Count > 0
            ? string.Join("\n", keyPoints.Take(12).Select((e, i) => $"  {i + 1}. {e}"))
            : "(no evidence reviewed)";

        string prevTurns = deliberationHistory.Count > 0
            ? string.Join("\n", deliberationHistory.TakeLast(6))
            : "(deliberation just starting)";

        // Determine which side the juror actually leans toward based on VerdictLean
        string currentSide = juror.VerdictLean >= 0.5 ? sideLabel : oppLabel;
        string jurorProfile = $"Juror: {juror.Name}\n" +
            $"- Age: {juror.Age}, Gender: {juror.Gender}, Race: {juror.Race}\n" +
            $"- Occupation: {juror.Occupation}, Education: {juror.EducationLevel}\n" +
            $"- Income: {juror.IncomeLevel}, Political: {juror.PoliticalAffiliation}\n" +
            $"- Religious: {juror.ReligiousAffiliation}, Marital: {juror.MaritalStatus}, Parental: {juror.ParentalStatus}\n" +
            $"- Current lean: {juror.VerdictLean:F2} (0.00 = strong defense, 1.00 = strong prosecution) — You ARE currently leaning toward {currentSide}.\n" +
            $"- Bias factor: {juror.Bias:F2}";

        string instructionsExcerpt = (caseData.Instructions?.Text?.Length ?? 0) > 0
            ? caseData.Instructions!.Text.Substring(0, Math.Min(600, caseData.Instructions.Text.Length))
            : "Standard jury instructions apply.";

        return $"You are a juror deliberating in a jury room. Stay in character based on your profile.\n\n" +
            $"{jurorProfile}\n\n" +
            $"CASE: {caseData.CaseName} ({(isCriminal ? "Criminal" : "Civil")})\n" +
            $"ISSUES: {verdictIssue}\n\n" +
            $"EVIDENCE REVIEWED:\n{evidenceContext}\n\n" +
            $"JURY INSTRUCTIONS:\n{instructionsExcerpt}\n\n" +
            $"PRIOR DELIBERATION:\n{prevTurns}\n\n" +
            $"IMPORTANT: Your lean value above indicates you are CURRENTLY leaning toward {currentSide}. " +
            $"Your statement must reflect this lean — do not contradict it. " +
            $"Give your honest assessment. Reference specific evidence from the EVIDENCE REVIEWED list above. " +
            $"Explain how your personal background (age, occupation, life experience) shapes how you interpret this evidence. " +
            $"Speak naturally as this juror, 3-5 sentences. Be specific about what evidence matters most to you and why.";
    }

    public async Task<DeliberationTurnResult> ExecuteTurnAsync(
        IEnumerable<Agent> jurors,
        HashSet<Guid> spokenIds,
        List<string> deliberationHistory,
        CaseFile caseData,
        IAgentInteractionService agentInteraction,
        int roundNumber)
    {
        var jurorList = GetDeliberatingJurors(jurors).Where(j => j.CanVote).ToList();
        if (jurorList.Count == 0)
            throw new InvalidOperationException("No jurors available for deliberation.");

        var speaker = SelectNextSpeaker(jurorList, spokenIds);
        if (speaker == null)
            throw new InvalidOperationException("All jurors have spoken this round.");

        spokenIds.Add(speaker.AgentId);

        // Build prompt and call LLM
        string prompt = BuildDeliberationPrompt(speaker, caseData, deliberationHistory);
        string llmResponse;

        // Try to match the agent's selected model by FriendlyName.
        // Fall back to the last model in the list (most recently configured),
        // then to the first model as a last resort.
        var defaultModel = caseData.AvailableModels.LastOrDefault()
                        ?? caseData.AvailableModels.FirstOrDefault();
        var jurorModel = caseData.AvailableModels.FirstOrDefault(m => m.FriendlyName == speaker.SelectedModel)
                      ?? defaultModel;

        string? fallbackReason = null;

        if (jurorModel != null)
        {
            try
            {
                var request = new StatementRequest
                {
                    Role = AgentRole.Juror,
                    Agent = speaker,
                    SpeakerName = speaker.Name,
                    Context = prompt,
                    CaseData = caseData,
                    Model = jurorModel
                };
                var result = await agentInteraction.GenerateStatementAsync(request);
                if (result.Success && result.Message != null)
                {
                    llmResponse = result.Message.Content;
                }
                else
                {
                    fallbackReason = result.Error ?? "LLM returned failure";
                    System.Diagnostics.Debug.WriteLine($"[Deliberation] LLM call failed for {speaker.Name}: {fallbackReason}");
                    llmResponse = GenerateFallbackStatement(speaker, caseData.Mode == CaseMode.Criminal);
                }
            }
            catch (Exception ex)
            {
                fallbackReason = $"{ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[Deliberation] LLM exception for {speaker.Name} (model={jurorModel?.FriendlyName}, provider={jurorModel?.Provider}): {ex.Message}");
                llmResponse = GenerateFallbackStatement(speaker, caseData.Mode == CaseMode.Criminal);
            }
        }
        else
        {
            fallbackReason = "No model configured (AvailableModels is empty)";
            llmResponse = GenerateFallbackStatement(speaker, caseData.Mode == CaseMode.Criminal);
        }

        // Parse the response
        var (direction, persuasiveness, isAmbivalent) = ParseStatement(llmResponse, speaker);

        // Apply conformity influence to other jurors
        var shifts = ApplyConformityInfluence(jurorList, speaker, direction, persuasiveness, llmResponse, roundNumber);

        // Update speaker's own lean (slight reinforcement of their stated position)
        if (direction > 0)
            speaker.VerdictLean = Math.Clamp(speaker.VerdictLean + 0.02, 0.0, 1.0);
        else if (direction < 0)
            speaker.VerdictLean = Math.Clamp(speaker.VerdictLean - 0.02, 0.0, 1.0);

        // Record in deliberation history
        deliberationHistory.Add($"{speaker.Name}: \"{llmResponse}\"");

        // Reinforce trial-event memories for all jurors when the speaker
        // references specific exhibits. This counteracts decay and keeps
        // evidence fresh during deliberation.
        foreach (var juror in jurorList)
        {
            juror.ReinforceRelatedMemories(llmResponse);
        }

        return new DeliberationTurnResult
        {
            SpeakingJuror = speaker,
            Statement = llmResponse,
            Persuasiveness = persuasiveness,
            Direction = direction,
            IsAmbivalent = isAmbivalent,
            InfluenceShifts = shifts,
            RoundNumber = roundNumber,
            UsedFallback = fallbackReason != null,
            FallbackReason = fallbackReason
        };
    }

    public List<(Agent Juror, double Shift)> ApplyConformityInfluence(
        IEnumerable<Agent> jurors,
        Agent speaker,
        double direction,
        double persuasiveness,
        string statement,
        int roundNumber)
    {
        var shifts = new List<(Agent, double)>();

        // Guard: no influence for neutral direction or invalid persuasiveness
        if (Math.Abs(direction) < 0.001 || persuasiveness <= 0 || double.IsNaN(persuasiveness))
            return shifts;

        // Clamp persuasiveness to valid range
        persuasiveness = Math.Clamp(persuasiveness, 0.0, 1.0);

        var listeners = jurors.Where(j => j.AgentId != speaker.AgentId && j.CanVote).ToList();
        if (listeners.Count == 0) return shifts;

        foreach (var juror in listeners)
        {
            // Susceptibility: weaker opinions are more persuadable.
            // Clamped to [0.1, 1.0] so even extreme jurors can be slightly influenced.
            double opinionStrength = Math.Abs(juror.VerdictLean - 0.5);
            double susceptibility = Math.Clamp(1.0 - opinionStrength, 0.1, 1.0);

            // Speaker credibility: based on age (experience), education, and specialized knowledge
            double speakerCredibility = 0.3 +
                (speaker.Age > 40 ? 0.2 : 0.0) +
                (speaker.EducationLevel?.Contains("Doctorate") == true || speaker.EducationLevel?.Contains("Master") == true ? 0.2 : 0.0) +
                (speaker.EducationLevel?.Contains("Bachelor") == true ? 0.1 : 0.0) +
                (speaker.SpecializedKnowledge?.Contains("Legal") == true ? 0.15 : 0.0);

            // Clamp credibility to [0.1, 1.0] to prevent extreme values
            speakerCredibility = Math.Clamp(speakerCredibility, 0.1, 1.0);

            // Calculate shift with validation against NaN/Infinity
            double shift = direction * persuasiveness * susceptibility * speakerCredibility * BaseShiftFactor;
            shift = Math.Clamp(shift, -0.25, 0.25); // Prevent any single shift from being too extreme

            // Apply shift (clamped)
            juror.VerdictLean = Math.Clamp(juror.VerdictLean + shift, 0.0, 1.0);
            juror.Bias = Math.Clamp(juror.Bias + shift * 0.3, -1.0, 1.0);

            // Record in juror's exhibit log for traceability
            string truncated = statement.Length <= 80 ? statement : statement.Substring(0, 77) + "...";
            juror.ExhibitLog.Add($"[Delib R{roundNumber}] Heard {speaker.Name}: \"{truncated}\" | Shift: {shift:+0.000;-0.000}");

            shifts.Add((juror, shift));
        }

        return shifts;
    }

    public (double Direction, double Persuasiveness, bool IsAmbivalent) ParseStatement(
        string statement, Agent juror)
    {
        string lower = statement.ToLower();

        // Direction comes from the juror's actual VerdictLean, NOT from keyword matching.
        // Keyword matching is unreliable: a juror might say "I'm leaning defense but..."
        // while their internal lean is actually prosecution. VerdictLean is the ground truth.
        // Edge case: exact 0.5 is truly neutral — direction = 0 (no influence either way).
        double direction;
        if (Math.Abs(juror.VerdictLean - 0.5) < 0.0001)
            direction = 0.0; // Perfectly neutral
        else
            direction = juror.VerdictLean >= 0.5 ? 1.0 : -1.0;

        // Detect persuasiveness from language cues
        double persuasiveness = 0.3; // base
        if (lower.Contains("strongly") || lower.Contains("clearly") || lower.Contains("definitely") ||
            lower.Contains("convinced") || lower.Contains("certain"))
            persuasiveness = 0.7;
        else if (lower.Contains("probably") || lower.Contains("likely") || lower.Contains("think") ||
                 lower.Contains("believe") || lower.Contains("seems"))
            persuasiveness = 0.5;
        else if (lower.Contains("maybe") || lower.Contains("unsure") || lower.Contains("question") ||
                 lower.Contains("uncertain") || lower.Contains("could go either way"))
            persuasiveness = 0.2;

        // Ambivalence check: when juror's lean is near 0.5 or language is non-committal
        bool isAmbivalent = Math.Abs(juror.VerdictLean - 0.5) < AmbivalenceThreshold
                            || persuasiveness <= 0.2;

        if (isAmbivalent)
        {
            persuasiveness = Math.Min(persuasiveness, 0.2); // Ambivalent jurors are less persuasive
        }

        return (direction, persuasiveness, isAmbivalent);
    }

    public List<string> ExtractKeyPoints(Agent juror, CaseFile caseData)
    {
        var keyPoints = new List<string>();

        // Try trial events first (richer context with LLM-formed memories)
        if (juror?.TrialEvents is { Count: > 0 } events)
        {
            foreach (var evt in events.OrderByDescending(e => e.Strength).Take(10))
            {
                if (evt == null || string.IsNullOrWhiteSpace(evt.Content))
                    continue;

                string point = evt.Content.Length <= 300
                    ? evt.Content
                    : evt.Content[..297] + "...";
                keyPoints.Add(point);
            }
        }

        // Fall back to exhibit log if trial events produced nothing
        if (keyPoints.Count == 0 && juror?.ExhibitLog is { Count: > 0 } log)
        {
            foreach (var entry in log.OrderByDescending(e => e?.Length ?? 0).Take(10))
            {
                if (!string.IsNullOrWhiteSpace(entry))
                    keyPoints.Add(entry.Length <= 300 ? entry : entry[..297] + "...");
            }
        }

        // Third fallback: use the case file evidence summaries directly
        // so jurors can reference evidence even without pre-processed TrialEvents
        if (keyPoints.Count == 0 && caseData?.Evidence is { Count: > 0 } evidence)
        {
            foreach (var doc in evidence)
            {
                string summary = doc.Summary?.Length > 250
                    ? doc.Summary[..247] + "..."
                    : doc.Summary ?? "(no summary)";
                keyPoints.Add($"[Exhibit {doc.ExhibitNumber}] {doc.FileName}: {summary}");
            }
        }

        // Final fallback: generic placeholder so the LLM doesn't hallucinate
        if (keyPoints.Count == 0)
        {
            keyPoints.Add("(No specific evidence has been reviewed yet. The juror is relying on general impressions from the trial proceedings.)");
        }

        return keyPoints;
    }

    public bool IsDeliberationComplete(
        IEnumerable<Agent> jurors, int currentRound, int maxRounds,
        out double spread, out bool isHung, CaseMode mode = CaseMode.Civil)
    {
        var voters = GetDeliberatingJurors(jurors).Where(j => j.CanVote).ToList();
        if (voters.Count == 0)
        {
            spread = 0;
            isHung = false;
            return true;
        }

        // Require at least 6 rounds before deliberation can conclude.
        // This ensures multiple jurors speak and influence has time to propagate.
        const int minRounds = 6;
        if (currentRound < minRounds)
        {
            spread = double.MaxValue;
            isHung = false;
            return false;
        }

        double maxLean = voters.Max(v => v.VerdictLean);
        double minLean = voters.Min(v => v.VerdictLean);
        spread = maxLean - minLean;

        bool isCriminal = mode == CaseMode.Criminal;
        int proCount = BurdenOfProof.CountProsecution(voters, isCriminal);
        int defCount = BurdenOfProof.CountDefense(voters, isCriminal);
        bool isUnanimousSide = (proCount == voters.Count || defCount == voters.Count);

        // In criminal cases, "all on same side" is NOT consensus if the spread is wide.
        // A jury with leans from 0.53 to 0.71 is NOT in agreement — they all have
        // reasonable doubt but at very different confidence levels.
        // Only treat unanimous-side as consensus when the spreads are genuinely tight.
        bool tightUnanimous = isUnanimousSide && spread <= 0.15;

        // True consensus: either tight spread OR unanimous-side with tight leans
        bool reachedConsensus = spread <= ConsensusThreshold || tightUnanimous;
        bool exceededRounds = currentRound >= maxRounds;
        isHung = exceededRounds && !reachedConsensus;

        return reachedConsensus || exceededRounds;
    }

    private static string GenerateFallbackStatement(Agent juror, bool isCriminal)
    {
        string side = juror.VerdictLean >= 0.5
            ? (isCriminal ? "prosecution" : "plaintiff")
            : "defense";
        string oppSide = juror.VerdictLean >= 0.5
            ? (isCriminal ? "defense" : "defense")
            : (isCriminal ? "prosecution" : "plaintiff");

        // Mix of confident, doubtful, questioning, and neutral statements
        string[] allTemplates = {
            // Confident statements (40%)
            $"Based on the evidence I've reviewed, I'm leaning toward the {side}. The key facts are persuasive.",
            $"After hearing the testimony, I think the {side} has the stronger case here.",
            $"I've been over the evidence carefully and I believe the {side} argument holds up.",
            // Questioning/doubtful statements (30%)
            $"I see points on both sides. The {side} evidence is compelling but I have questions about some of it.",
            $"I'm leaning toward the {side} but I want to hear what others think before I decide.",
            $"Some of the evidence seems contradictory. I'm not fully convinced either way yet.",
            // Counter-perspective statements (20%)
            $"I can see why someone would favor the {oppSide}. There are valid points on both sides.",
            $"Before I commit, I want to understand the {oppSide} argument better. What are others seeing?",
            // Neutral/open statements (10%)
            $"I'm still processing everything. Can someone walk through the timeline again?",
            $"The jury instructions set a high bar. I'm not sure the {side} has met it yet.",
        };

        // Pick based on juror's opinion strength - stronger opinions pick from confident pool
        double strength = Math.Abs(juror.VerdictLean - 0.5);
        int idx;
        if (strength > 0.2)
            idx = _random.Value.Next(0, 3); // Confident pool
        else if (strength > 0.1)
            idx = _random.Value.Next(3, 6); // Questioning pool
        else
            idx = _random.Value.Next(6, allTemplates.Length); // Counter/neutral pool

        return allTemplates[idx];
    }

    private static readonly Lazy<Random> _random = new(() => new Random());
}
