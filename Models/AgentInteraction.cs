using System;
using System.Collections.Generic;

namespace Verdict.Models;

/// <summary>
/// Represents a single message in the agent interaction system.
/// </summary>
public class AgentMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Speaker { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public MessageType Type { get; set; } = MessageType.Statement;
    public string? ReplyToId { get; set; }
    public List<string> LegalCitations { get; set; } = new();
    public double EmotionalTone { get; set; } = 0.5; // 0.0 = negative, 1.0 = positive
    public bool IsFromAI { get; set; } = true;
}

/// <summary>
/// Type of agent message in the courtroom.
/// </summary>
public enum MessageType
{
    Statement,
    Question,
    Objection,
    Response,
    Ruling,
    Testimony,
    Deliberation,
    Verdict
}

/// <summary>
/// Represents the configuration for a courtroom role.
/// </summary>
public class RoleConfiguration
{
    public AgentRole Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public int MaxResponseWords { get; set; } = 40;
    public bool CanDeliberate { get; set; }
    public bool CanRule { get; set; }
    public bool CanTestify { get; set; }
    public List<AgentRole> RespondsTo { get; set; } = new();
}

/// <summary>
/// Configuration for agent interactions during a phase.
/// </summary>
public class PhaseConfiguration
{
    public CourtPhase Phase { get; set; }
    public List<RoleConfiguration> Roles { get; set; } = new();
    public bool RequiresLegalResearch { get; set; }
    public bool RequiresDeliberation { get; set; }
    public int MinParticipants { get; set; } = 2;
}

/// <summary>
/// Represents a legal citation or precedent reference.
/// </summary>
public class LegalCitation
{
    public string Code { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public bool FavorsPlaintiff { get; set; }
}

/// <summary>
/// Represents the result of an agent interaction.
/// </summary>
public class InteractionResult
{
    public bool Success { get; set; }
    public AgentMessage? Message { get; set; }
    public string? Error { get; set; }
    public List<LegalCitation> Citations { get; set; } = new();
    public double SentimentShift { get; set; }
}

/// <summary>
/// Request for generating a courtroom statement.
/// </summary>
public class StatementRequest
{
    public AgentRole Role { get; set; }
    public Agent? Agent { get; set; }
    public string SpeakerName { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string? ReplyToMessage { get; set; }
    public MessageType Type { get; set; } = MessageType.Statement;
    public CaseFile? CaseData { get; set; }
    public List<AgentMessage>? Transcript { get; set; }
    public AIModelConfiguration? Model { get; set; }
}

/// <summary>
/// Request for conducting cross-examination.
/// </summary>
public class ExaminationRequest
{
    public AgentRole ExaminerRole { get; set; }
    public string ExaminerName { get; set; } = string.Empty;
    public AgentRole WitnessRole { get; set; }
    public string WitnessName { get; set; } = string.Empty;
    public string WitnessBackground { get; set; } = string.Empty;
    public string? ExpectedTestimony { get; set; }
    public bool IsCross { get; set; }
    public CaseFile? CaseData { get; set; }
    public List<AgentMessage>? Transcript { get; set; }
    public AIModelConfiguration? Model { get; set; }
}

/// <summary>
/// Request for jury deliberation.
/// </summary>
public class DeliberationRequest
{
    public List<Agent> Jurors { get; set; } = new();
    public List<AgentMessage> Transcript { get; set; } = new();
    public CaseFile? CaseData { get; set; }
    public AIModelConfiguration? Model { get; set; }
}

/// <summary>
/// Result of jury deliberation.
/// </summary>
public class DeliberationResult
{
    public string Verdict { get; set; } = string.Empty; // "Plaintiff" or "Defendant"
    public double Confidence { get; set; } // 0.0 to 1.0
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, double> IndividualVotes { get; set; } = new();
}

/// <summary>
/// Case data structure for agent context.
/// </summary>
public class AgentCaseData
{
    public string CaseNumber { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public string PlaintiffClaim { get; set; } = string.Empty;
    public string DefendantDefense { get; set; } = string.Empty;
    public string CaseOverview { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
    public List<string> LegalIssues { get; set; } = new();
    public string DamagesSought { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
}
