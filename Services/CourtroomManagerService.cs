using System.Collections.ObjectModel;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Manages courtroom agent collections: initialization, slot occupancy,
/// and dynamic table slot management.
/// </summary>
public interface ICourtroomManagerService
{
    void InitializeCourtroom(
        ObservableCollection<Agent> judgeArea,
        ObservableCollection<Agent> jurors,
        ObservableCollection<Agent> defenseTeam,
        ObservableCollection<Agent> prosecutionTeam,
        ObservableCollection<Agent> gallery,
        CaseFile? caseData = null);

    void OccupySlot(
        Agent agent,
        ObservableCollection<Agent> defenseTeam,
        ObservableCollection<Agent> prosecutionTeam);

    void ResetAllAgents(IEnumerable<Agent> allAgents);

    Agent CreateEmptySlot(AgentRole role, string name = "+");
}

public class CourtroomManagerService : ICourtroomManagerService
{
    public void InitializeCourtroom(
        ObservableCollection<Agent> judgeArea,
        ObservableCollection<Agent> jurors,
        ObservableCollection<Agent> defenseTeam,
        ObservableCollection<Agent> prosecutionTeam,
        ObservableCollection<Agent> gallery,
        CaseFile? caseData = null)
    {
        judgeArea.Clear();
        jurors.Clear();
        defenseTeam.Clear();
        prosecutionTeam.Clear();
        gallery.Clear();

        // Bench: 1 Judge, 1 Reporter, 1 Witness box
        judgeArea.Add(Reporter());
        judgeArea.Add(Judge());
        judgeArea.Add(WitnessBox());

        // 12 Jurors in 3x4 formation (3 rows of 4 jurors each)
        // These are meant to be visible “juror cards”, so mark them occupied.
        for (int i = 1; i <= 12; i++)
            jurors.Add(new Agent { Role = AgentRole.Juror, Name = $"Juror {i}", IsOccupied = true });
        
        // Add 2 alternate jurors to gallery (to be displayed in 2x3 layout under gallery)
        gallery.Add(new Agent { Role = AgentRole.AlternateJuror, Name = "Alt Juror 1", IsOccupied = true });
        gallery.Add(new Agent { Role = AgentRole.AlternateJuror, Name = "Alt Juror 2", IsOccupied = true });


        // Counsel tables: load lawyer names from case data, or use defaults
        string defenseLawyerName = "Danny Defense";
        string plaintiffLawyerName = "Paul Plaintiff";
        string plaintiffClientName = "+ Plaintiff Client";
        string defenseClientName = "+ Defense Client";

        if (caseData != null)
        {
            if (!string.IsNullOrWhiteSpace(caseData.DefenseAttorney))
                defenseLawyerName = caseData.DefenseAttorney;
            if (!string.IsNullOrWhiteSpace(caseData.PlaintiffAttorney))
                plaintiffLawyerName = caseData.PlaintiffAttorney;
            if (caseData.Plaintiffs.Count > 0)
                plaintiffClientName = $"+ {caseData.Plaintiffs[0]}";
            if (caseData.Defendants.Count > 0)
                defenseClientName = $"+ {caseData.Defendants[0]}";
        }

        defenseTeam.Add(new Agent { Role = AgentRole.Lawyer, Name = defenseLawyerName });
        prosecutionTeam.Add(new Agent { Role = AgentRole.Lawyer, Name = plaintiffLawyerName });

        // Gallery: 4 litigation observers (default) + 2 client slots
        // Note: observers are connected to attorneys (can't talk to jury; estimates exposure/outcomes).
        var defenseAttorneyName = defenseLawyerName;
        var plaintiffAttorneyName = plaintiffLawyerName;

        // Stakeholder observers (config-driven symbolic players)
        // For criminal trials, we do NOT want insurance/coverage-style stakeholders.
        // Everything else can remain.
        var isCriminal = caseData?.Mode == CaseMode.Criminal;

        if (!isCriminal)
        {
            gallery.Add(new Agent
            {
                Role = AgentRole.InsuranceAdjuster,
                Name = "GEICO - Claims Adjuster",
                ObserverType = "InsuranceAdjuster",
                ConnectedAttorneyName = defenseAttorneyName,
                CanCommunicateWithJury = false,
                IsExposurePlanner = true
            });
        }

        gallery.Add(new Agent
        {
            Role = AgentRole.SupervisingProsecutor,
            Name = "Supervising Prosecutor",
            ObserverType = "SupervisingProsecutor",
            ConnectedAttorneyName = plaintiffAttorneyName,
            CanCommunicateWithJury = false,
            IsExposurePlanner = false
        });

        gallery.Add(new Agent
        {
            Role = AgentRole.LitigationManager,
            Name = "+ Litigation Manager",
            ObserverType = "LitigationManager",
            ConnectedAttorneyName = defenseAttorneyName,
            CanCommunicateWithJury = false,
            IsExposurePlanner = true
        });

        // Legacy generic observer slots kept for backward compatibility
        gallery.Add(new Agent
        {
            Role = AgentRole.Observer,
            Name = "Coverage Counsel Liaison",
            ObserverType = "ClaimsAnalyst",
            ConnectedAttorneyName = defenseAttorneyName,
            CanCommunicateWithJury = false,
            IsExposurePlanner = true
        });

        gallery.Add(new Agent
        {
            Role = AgentRole.Observer,
            Name = "+ Plaintiff-side Litigation Analyst",
            ObserverType = "ClaimsAnalyst",
            ConnectedAttorneyName = plaintiffAttorneyName,
            CanCommunicateWithJury = false,
            IsExposurePlanner = true
        });

        gallery.Add(new Agent
        {
            Role = AgentRole.Observer,
            Name = "+ Defense-side Litigation Analyst",
            ObserverType = "ClaimsAnalyst",
            ConnectedAttorneyName = defenseAttorneyName,
            CanCommunicateWithJury = false,
            IsExposurePlanner = true
        });

        gallery.Add(new Agent { Role = AgentRole.Client, Name = plaintiffClientName });
        gallery.Add(new Agent { Role = AgentRole.Client, Name = defenseClientName });
    }

    public void OccupySlot(
        Agent agent,
        ObservableCollection<Agent> defenseTeam,
        ObservableCollection<Agent> prosecutionTeam)
    {
        if (agent.IsOccupied) return;

        agent.IsOccupied = true;
        if (agent.Name.StartsWith("+"))
            agent.Name = $"Active {agent.Role}";

        // Ensure each counsel table has an available empty slot
        EnsureTableSlots(defenseTeam, AgentRole.Lawyer);
        EnsureTableSlots(prosecutionTeam, AgentRole.Lawyer);
    }

    public void ResetAllAgents(IEnumerable<Agent> allAgents)
    {
        foreach (var agent in allAgents)
        {
            var shouldHaveOpinion = agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror || agent.Role == AgentRole.Judge;
            if (shouldHaveOpinion)
                agent.VerdictLean = 0.5;
            agent.IsOccupied = false;
            agent.Memories.Clear();
        }
    }

    public Agent CreateEmptySlot(AgentRole role, string name = "+")
    {
        return new Agent { Role = role, Name = name };
    }

    // --- Private helpers ---

    private static void EnsureTableSlots(ObservableCollection<Agent> team, AgentRole role)
    {
        if (!team.Any(a => !a.IsOccupied && a.Role == role))
            team.Add(new Agent { Role = role, Name = "+" });
    }

    private static Agent Reporter() =>
        new() { Role = AgentRole.Reporter, Name = "Reporter", IsOccupied = true };

    private static Agent Judge() =>
        new() { Role = AgentRole.Judge, Name = "Judge Jones" };

    private static Agent WitnessBox() =>
        new() { Role = AgentRole.Witness, Name = "Witness Box" };
}
