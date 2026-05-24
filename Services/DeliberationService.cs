using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

public interface IDeliberationService
{
    void Start();
    Task<IReadOnlyList<DeliberationEntry>> DeliberateNextTurnAsync(IEnumerable<Agent> allAgents);
    Task<IReadOnlyList<DeliberationEntry>> RunFullDeliberationAsync(IEnumerable<Agent> allAgents);
    double ComputeJuryLiabilityAverage(IEnumerable<Agent> jurors);
    bool CheckConsensus(IEnumerable<Agent> jurors, CaseMode mode);
}

public sealed class DeliberationService : IDeliberationService
{
    private readonly HashSet<Guid> _spokenJurorIds = new();
    private int _round;
    private bool _initialized;

    private string _lastJurorSpeaker = string.Empty;
    private string _lastJurorKeyPoint = string.Empty;
    private string _lastJurorSide = string.Empty;


    public void Start()
    {
        _spokenJurorIds.Clear();
        _round = 0;
        _initialized = true;
    }

    public async Task<IReadOnlyList<DeliberationEntry>> DeliberateNextTurnAsync(IEnumerable<Agent> allAgents)
    {
        if (!_initialized) Start();

        // Regular jurors participate in deliberation.
        // Alternates only participate when they have been promoted into the Juror role
        // (i.e., after a juror has been removed).
        var jurors = allAgents
            .Where(a => a.IsOccupied && a.Role == AgentRole.Juror && a.CanVote)
            .ToList();


        if (!jurors.Any())
        {
            return new List<DeliberationEntry>
            {
                new DeliberationEntry
                {
                    Speaker = "Court",
                    Message = "No jurors seated for deliberation.",
                    Timestamp = DateTime.Now
                }
            };
        }

        var candidates = jurors
            .Where(j => !_spokenJurorIds.Contains(j.AgentId))
            .Select(j => new { Agent = j, Strength = Math.Abs(j.VerdictLean - 0.5) })
            .OrderByDescending(x => x.Strength)
            .ToList();

        if (!candidates.Any())
        {
            // reset another round
            _spokenJurorIds.Clear();
            candidates = jurors
                .Select(j => new { Agent = j, Strength = Math.Abs(j.VerdictLean - 0.5) })
                .OrderByDescending(x => x.Strength)
                .Take(3)
                .ToList();
        }

        var next = candidates.FirstOrDefault()?.Agent;
        if (next is null) return Array.Empty<DeliberationEntry>();

        _spokenJurorIds.Add(next.AgentId);
        _round++;

        string side = next.VerdictLean >= 0.5 ? "plaintiff" : "defense";
        double leanPercent = next.VerdictLean * 100;

        // If the juror is ambivalent (could go either way), they ask questions to resolve uncertainty.
        const double ambivalenceEps = 0.07; // ~7% of the scale around 0.5
        bool isAmbivalent = Math.Abs(next.VerdictLean - 0.5) <= ambivalenceEps;

        // Pull “evidence” and a “key point” from what this juror has already internalized.
        // (Best-effort: DeliberationService itself does not have a CaseFile reference.)
        // Juror self-reflection: if the juror has a memory, use it; otherwise fall back
        // to a neutral “refresh my memory” style line.
        string keyPoint = "";
        var lastEvent = next.TrialEvents.LastOrDefault();
        if (lastEvent != null)
        {
            keyPoint = (lastEvent.Content ?? string.Empty).Split('.').FirstOrDefault()?.Trim() ?? string.Empty;
        }


        // Prefer exhibit-backed recall if the reporter summaries have been recorded into ExhibitLog.
        var exhibitClues = next.ExhibitLog
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Take(2)
            .ToList();

        // Fallback: use the evidence strength + recent trial event snippet.
        string exhibitClause = exhibitClues.Any()
            ? string.Join(" | ", exhibitClues)
            : (keyPoint.Length > 0
                ? $"Evidence recall: {keyPoint}"
                : "Evidence recall: (no specific exhibit cached yet)");

        // “Argument to each other” requirement: explicitly address the previous juror's point.
        // If we're on the same side, we "agree and add"; if different, we "hear and counter".
        string addOrCounterClause = string.Empty;
        string? lastKeyPointQuoted = string.IsNullOrWhiteSpace(_lastJurorKeyPoint)
            ? null
            : $"'{_lastJurorKeyPoint}'";

        bool havePrev = !string.IsNullOrWhiteSpace(_lastJurorSpeaker) && !string.IsNullOrWhiteSpace(_lastJurorSide);

        if (havePrev && !string.IsNullOrWhiteSpace(lastKeyPointQuoted))
        {
            if (string.Equals(_lastJurorSide, side, StringComparison.OrdinalIgnoreCase))
            {
                // Agree and add
                addOrCounterClause =
                    $" I agree with {_lastJurorSpeaker} that {exhibitClause}, " +
                    $"and I think it strengthens the {side} side further.";
            }
            else
            {
                // Counter
                addOrCounterClause =
                    $" I hear {_lastJurorSpeaker}'s point ({lastKeyPointQuoted}), " +
                    $"but because {exhibitClause} seems to weigh more toward the {side} side, " +
                    $"I'm leaning that way.";
            }
        }
        else if (havePrev)
        {
            // We know the previous speaker/side but no key point; still address them.
            if (string.Equals(_lastJurorSide, side, StringComparison.OrdinalIgnoreCase))
            {
                addOrCounterClause =
                    $" I’m aligned with {_lastJurorSpeaker} on this, and I’m adding that {exhibitClause}.";
            }
            else
            {
                addOrCounterClause =
                    $" I understand {_lastJurorSpeaker}'s position, but {exhibitClause} still pushes me toward the {side} side.";
            }
        }
        else
        {
            // No previous juror to address yet
            addOrCounterClause = $" Based on {exhibitClause}, I'm leaning toward the {side} side.";
        }

        string line;

        if (isAmbivalent)
        {
            // Ask a question directed at the previous juror’s point.
            // Keep it neutral but targeted so deliberation feels interactive.
            string questionTarget = !string.IsNullOrWhiteSpace(_lastJurorSpeaker)
                ? _lastJurorSpeaker
                : "the other jurors";

            string questionBasis = !string.IsNullOrWhiteSpace(exhibitClause)
                ? exhibitClause
                : "the evidence presented";

            line =
                $"{next.Name}: I'm honestly torn right now—{questionTarget}, can you clarify how {questionBasis} leads you to the {side} side? " +
                $"What specific detail am I missing?";

            // Record the question so others can reference it on later turns.
            string questionMemory = $"[JUROR PERSUASION QUESTION] To {_lastJurorSpeaker}: Clarify how {questionBasis} supports {side}.";
            foreach (var juror in jurors.Where(j => j.AgentId != next.AgentId))
            {
                juror.RecordTrialEvent(questionMemory, juror.Bias * 0.02);
            }
        }
        else
        {
            line = $"{next.Name}: I'm leaning toward the {side} side ({leanPercent:F0}%)." +
                   addOrCounterClause;
        }

        // Store a short persuasion argument so other jurors can reference it next turn.
        // Skip persuasion-memory when the juror is asking a question instead of asserting.
        if (!isAmbivalent)
        {
            string persuasionArgument;
            if (string.Equals(_lastJurorSide, side, StringComparison.OrdinalIgnoreCase) && havePrev)
            {
                persuasionArgument = $"[JUROR PERSUASION ARGUMENT] {_lastJurorSpeaker} and I agree: {exhibitClause} -> strengthen {side}.";
            }
            else if (havePrev)
            {
                persuasionArgument = $"[JUROR PERSUASION ARGUMENT] Counter to {_lastJurorSpeaker}: {exhibitClause} weighs toward {side}.";
            }
            else
            {
                persuasionArgument = $"[JUROR PERSUASION ARGUMENT] Starting point: {exhibitClause} -> {side}.";
            }

            // Record persuasion into the other jurors' TrialEvents.
            foreach (var juror in jurors.Where(j => j.AgentId != next.AgentId))
            {
                juror.RecordTrialEvent(persuasionArgument, juror.Bias * 0.02);
            }
        }

        var entry = new DeliberationEntry
        {
            Speaker = next.Name,
            Message = line,
            Timestamp = DateTime.Now,
            IsJuror = true
        };
        
        // Update memory for the next juror so they can explicitly “remember” this point.
        _lastJurorSpeaker = next.Name;
        _lastJurorKeyPoint = !string.IsNullOrWhiteSpace(keyPoint)
            ? keyPoint
            : (exhibitClues.FirstOrDefault() ?? string.Empty);
        _lastJurorSide = side;

        // Conformity influence
        await ApplyDeliberationInfluenceAsync(next, jurors);

        return new List<DeliberationEntry> { entry };


    }

    public async Task<IReadOnlyList<DeliberationEntry>> RunFullDeliberationAsync(IEnumerable<Agent> allAgents)
    {
        Start();

        var entries = new List<DeliberationEntry>
        {
            new DeliberationEntry
            {
                Speaker = "Court",
                Message = "Deliberation begins. Jurors will now discuss the evidence.",
                Timestamp = DateTime.Now
            }
        };

        var jurors = allAgents
            .Where(a => a.IsOccupied && a.Role == AgentRole.Juror && a.CanVote)
            .ToList();


        if (!jurors.Any())
        {
            entries.Add(new DeliberationEntry
            {
                Speaker = "Court",
                Message = "No jurors seated. Cannot begin deliberation.",
                Timestamp = DateTime.Now
            });
            return entries;
        }

        int rounds = Math.Max(jurors.Count * 2, 6);
        for (int i = 0; i < rounds; i++)
        {
            var turnEntries = await DeliberateNextTurnAsync(allAgents);
            entries.AddRange(turnEntries);
            await Task.Delay(250);
        }

        // final verdict summary is computed by MainViewModel using JuryCalculationService,
        // so we only emit a placeholder message here.
        entries.Add(new DeliberationEntry
        {
            Speaker = "Court",
            Message = "Deliberation complete.",
            Timestamp = DateTime.Now
        });

        return entries;
    }

    public double ComputeJuryLiabilityAverage(IEnumerable<Agent> jurors) =>
        jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0.5;

    private static Task ApplyDeliberationInfluenceAsync(Agent speakingJuror, IEnumerable<Agent> allJurors)
    {
        double speakerLean = speakingJuror.VerdictLean;
        double influence = 0.05;

        foreach (var juror in allJurors.Where(j => j.AgentId != speakingJuror.AgentId))
        {
            if (juror.VerdictLean < 0.5)
            {
                juror.VerdictLean = Math.Max(0, juror.VerdictLean + influence);
            }
            else if (juror.VerdictLean > 0.5)
            {
                if (speakerLean < 0.5)
                    juror.VerdictLean = Math.Min(1, juror.VerdictLean - influence * 0.5);
            }
            else
            {
                juror.VerdictLean = Math.Max(0, Math.Min(1, juror.VerdictLean + (speakerLean - 0.5) * 0.1));
            }
        }

        return Task.CompletedTask;
    }

    public bool CheckConsensus(IEnumerable<Agent> jurors, CaseMode mode)
    {
        var active = jurors.Where(j => j.CanVote).ToList();
        if (!active.Any()) return false;

        if (mode == CaseMode.Criminal)
        {
            // All must be on one side
            return active.All(j => j.VerdictLean > 0.55) || active.All(j => j.VerdictLean < 0.45);
        }
        else
        {
            // 75% majority
            int threshold = (int)Math.Ceiling(active.Count * 0.75);
            return active.Count(j => j.VerdictLean > 0.55) >= threshold || active.Count(j => j.VerdictLean < 0.45) >= threshold;
        }
    }
}
