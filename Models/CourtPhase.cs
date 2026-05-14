namespace Verdict.Models;

/// <summary>
/// Phases of a courtroom trial simulation.
/// </summary>
public enum CourtPhase
{
    CaseGeneration,
    CourtOpening,
    LegalResearch,
    OpeningStatements,
    WitnessTestimony,
    CrossExamination,
    PartyStatements,
    ClosingArguments,
    JuryInstructions,
    JuryDeliberation,
    VerdictAnnouncement,
    CourtClosing
}
