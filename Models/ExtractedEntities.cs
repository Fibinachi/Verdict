using System;
using System.Collections.Generic;

namespace Verdict.Models;

/// <summary>
/// Represents entities extracted from a transcript using LLM entity extraction.
/// </summary>
public class ExtractedEntities
{
    /// <summary>
    /// List of charges or claims identified in the transcript.
    /// </summary>
    public List<ExtractedCharge> Charges { get; set; } = new();

    /// <summary>
    /// List of evidence items mentioned in the transcript.
    /// </summary>
    public List<ExtractedEvidence> Evidence { get; set; } = new();

    /// <summary>
    /// List of witnesses mentioned or testifying in the transcript.
    /// </summary>
    public List<ExtractedWitness> Witnesses { get; set; } = new();

    /// <summary>
    /// Key legal issues or arguments identified.
    /// </summary>
    public List<string> LegalIssues { get; set; } = new();

    /// <summary>
    /// The cause of action or claims asserted.
    /// </summary>
    public string CauseOfAction { get; set; } = string.Empty;

    /// <summary>
    /// List of plaintiffs identified in the transcript.
    /// </summary>
    public List<string> Plaintiffs { get; set; } = new();

    /// <summary>
    /// List of defendants identified in the transcript.
    /// </summary>
    public List<string> Defendants { get; set; } = new();

    /// <summary>
    /// Plaintiff attorney(s) identified.
    /// </summary>
    public string PlaintiffAttorney { get; set; } = string.Empty;

    /// <summary>
    /// Defense attorney(s) identified.
    /// </summary>
    public string DefenseAttorney { get; set; } = string.Empty;

    /// <summary>
    /// Damages sought or mentioned.
    /// </summary>
    public string Damages { get; set; } = string.Empty;

    /// <summary>
    /// Summary of the case based on transcript analysis.
    /// </summary>
    public string CaseSummary { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when extraction was performed.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// A charge or criminal/civil claim extracted from transcript.
/// </summary>
public class ExtractedCharge
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Statute { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // e.g., "Felony", "Misdemeanor"
}

/// <summary>
/// Evidence mentioned in the transcript.
/// </summary>
public class ExtractedEvidence
{
    public string Type { get; set; } = string.Empty; // e.g., "Document", "Photo", "Video", "Testimony"
    public string Description { get; set; } = string.Empty;
    public string AdmittedBy { get; set; } = string.Empty; // Which party introduced it
    public double Strength { get; set; } = 0.5; // 0.0 to 1.0
}

/// <summary>
/// A witness mentioned or who testified.
/// </summary>
public class ExtractedWitness
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Plaintiff", "Defendant", "Expert", "Character"
    public string TestimonySummary { get; set; } = string.Empty;
    public bool Testified { get; set; }
}
