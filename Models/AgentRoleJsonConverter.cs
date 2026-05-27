using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Verdict.Models;

/// <summary>
/// Custom JSON converter for AgentRole that gracefully handles unknown enum values
/// by defaulting to Observer instead of throwing a JsonException.
/// This prevents crashes when loading case files saved with agent roles
/// not present in the current enum definition.
/// </summary>
public class AgentRoleJsonConverter : JsonConverter<AgentRole>
{
    private static readonly Dictionary<string, AgentRole> _knownValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Judge"] = AgentRole.Judge,
        ["Witness"] = AgentRole.Witness,
        ["Reporter"] = AgentRole.Reporter,
        ["Juror"] = AgentRole.Juror,
        ["AlternateJuror"] = AgentRole.AlternateJuror,
        ["Alternate Juror"] = AgentRole.AlternateJuror,
        ["Lawyer"] = AgentRole.Lawyer,
        ["Client"] = AgentRole.Client,
        ["Observer"] = AgentRole.Observer,
    };

    public override AgentRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (value != null && _knownValues.TryGetValue(value, out var role))
                return role;

            // Unknown role: default to Observer and log a diagnostic
            System.Diagnostics.Debug.WriteLine($"[AgentRoleJsonConverter] Unknown AgentRole value '{value}'. Defaulting to Observer.");
            return AgentRole.Observer;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            var intValue = reader.GetInt32();
            if (Enum.IsDefined(typeof(AgentRole), intValue))
                return (AgentRole)intValue;
            return AgentRole.Observer;
        }

        return AgentRole.Observer;
    }

    public override void Write(Utf8JsonWriter writer, AgentRole value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
