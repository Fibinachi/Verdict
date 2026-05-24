namespace Verdict.Models;

public enum AgentRole
{
    Judge,
    Witness,
    Reporter,
    Juror,
    AlternateJuror,
    Lawyer,
    Client,

    // Stakeholder/observer roles (configurable symbolic players)
    InsuranceAdjuster,
    SupervisingProsecutor,
    LitigationManager,

    Observer
}
