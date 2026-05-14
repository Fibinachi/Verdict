using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Manages courtroom debate stages, transcript processing, and opinion influence.
/// </summary>
public interface IDebateService
{
    CourtPhase AdvanceStage(CourtPhase current);
    string StageDisplayName(CourtPhase stage);
    double CalculateInfluence(string speaker);
    void BroadcastEvent(
        CaseFile caseFile,
        IEnumerable<Agent> allAgents,
        string description,
        List<AgentRole> visibleTo,
        bool isSidebar = false);
}

public class DebateService : IDebateService
{
    public CourtPhase AdvanceStage(CourtPhase current)
    {
        return current switch
        {
            CourtPhase.OpeningStatements   => CourtPhase.WitnessTestimony,
            CourtPhase.WitnessTestimony    => CourtPhase.CrossExamination,
            CourtPhase.CrossExamination    => CourtPhase.ClosingArguments,
            CourtPhase.ClosingArguments    => CourtPhase.VerdictAnnouncement,
            CourtPhase.VerdictAnnouncement => CourtPhase.VerdictAnnouncement, // stay
            _ => current
        };
    }

    public string StageDisplayName(CourtPhase stage)
    {
        return stage switch
        {
            CourtPhase.OpeningStatements   => "Opening Statements",
            CourtPhase.WitnessTestimony    => "Witness Examination",
            CourtPhase.CrossExamination    => "Cross-Examination",
            CourtPhase.ClosingArguments    => "Closing Arguments",
            CourtPhase.VerdictAnnouncement => "Verdict Announcement",
            _ => stage.ToString()
        };
    }

    public double CalculateInfluence(string speaker)
    {
        if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff")) return 0.05;
        if (speaker.Contains("Defense")) return -0.05;
        return 0;
    }

    public void BroadcastEvent(
        CaseFile caseFile,
        IEnumerable<Agent> allAgents,
        string description,
        List<AgentRole> visibleTo,
        bool isSidebar = false)
    {
        var courtroomEvent = new CourtroomEvent
        {
            Description = description,
            VisibleTo = visibleTo,
            IsSidebar = isSidebar
        };
        caseFile.EventLog.Add(courtroomEvent);

        foreach (var agent in allAgents)
        {
            if (!visibleTo.Contains(agent.Role)) continue;

            agent.RecordTrialEvent(description, agent.Bias * 0.1);

            if (agent.Role != AgentRole.Reporter)
                agent.AnalyzeSentiment(description);

            // Heuristic status updates
            if (description.Contains("Evidence Admitted"))
                agent.CurrentStatus = "Recording Evidence";
            else if (description.Contains("Sidebar"))
                agent.CurrentStatus = "Standing By";
            else if (agent.Role == AgentRole.Juror)
                agent.CurrentStatus = "Listening to Testimony";
            else if (agent.Role == AgentRole.Reporter)
                agent.CurrentStatus = "Recording Proceedings";
        }
    }
}
