using System.Text.Json.Serialization;

namespace Verdict.Models;

[JsonConverter(typeof(AgentRoleJsonConverter))]
public enum AgentRole
{
    Judge,
    Witness,
    Reporter,
    Juror,
    AlternateJuror,
    Lawyer,
    Client,
    Observer
}
