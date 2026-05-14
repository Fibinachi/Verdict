using System;

namespace Verdict.Models;

public class EvidenceDocument
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime AdmittedAt { get; set; } = DateTime.Now;

    // Exhibit tracking for court record
    public int ExhibitNumber { get; set; }

    // Closing argument fields
    public string ClosingArgumentPlaintiff { get; set; } = string.Empty;
    public string ClosingArgumentDefense { get; set; } = string.Empty;
    public List<int> ReferencedExhibitsPlaintiff { get; set; } = new();
    public List<int> ReferencedExhibitsDefense { get; set; } = new();

    // Evidence strength (0.0 = weak, 1.0 = strong) - affects verdict lean
    public double EvidenceStrength { get; set; } = 0.5;

    // Estimated damages value for settlement calculations
    public double EstimatedDamages { get; set; }

    // Whether evidence has been assessed in discovery
    public bool IsDiscoveryComplete { get; set; }

    // Clerk notes for exhibit management
    public string? ClerkNotes { get; set; }

    // Media type classification: "Image", "Document", etc.
    public string? MediaType { get; set; }

    // Full detailed LLM analysis of the document (preserved separately from the summary)
    public string? DetailedAnalysis { get; set; }

    // The attorney who offered this exhibit into evidence
    public string OfferingAttorney { get; set; } = string.Empty;

    // Indexing for future usage
    public int PlaintiffSideExhibitNumber { get; set; }
    public int DefenseSideExhibitNumber { get; set; }

    // True if this exhibit is offered by the plaintiff side (civil plaintiff or prosecution)
    public bool IsOfferedByPlaintiffSide { get; set; }
}
