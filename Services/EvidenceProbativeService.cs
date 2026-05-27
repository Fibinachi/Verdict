using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Handles evidence probative value assessment, media-type impact multipliers,
/// and evidence category classification. Extracted from EvidenceAnalysisService
/// to keep files focused and manageable.
/// </summary>
public static class EvidenceProbativeService
{
    /// <summary>
    /// Returns the impact multiplier for a given evidence category based on research
    /// on how different media types affect juror perception.
    /// Videos are most impactful ("seeing is believing"), followed by images,
    /// audio recordings, then documents. Testimony impact varies by credibility.
    /// </summary>
    public static double GetMediaImpactMultiplier(string evidenceCategory)
    {
        return evidenceCategory switch
        {
            "Video" => 1.6,      // Highest impact — visual + auditory + temporal
            "Image" => 1.4,      // High impact — visual evidence is compelling
            "Audio" => 1.3,      // Moderate-high — hearing a voice adds authenticity
            "Physical" => 1.5,   // High impact — tangible objects feel "real"
            "Testimony" => 1.0,  // Base — actual impact depends on witness credibility
            "Document" => 1.0,   // Baseline — documents require reading and parsing
            _ => 1.0
        };
    }

    /// <summary>
    /// Determines the evidence category from media type and content analysis.
    /// More granular than MediaType — distinguishes testimony, physical evidence, etc.
    /// </summary>
    public static string DetermineEvidenceCategory(string? mediaType, string summary, string fileContent)
    {
        string combined = $"{summary} {fileContent}".ToLower();

        // Testimony indicators
        if (combined.Contains("witness statement") || combined.Contains("deposition") ||
            combined.Contains("affidavit") || combined.Contains("sworn testimony") ||
            combined.Contains("under oath") || combined.Contains("witness interview"))
            return "Testimony";

        // Physical evidence indicators
        if (combined.Contains("physical evidence") || combined.Contains("weapon") ||
            combined.Contains("fingerprint") || combined.Contains("dna sample") ||
            combined.Contains("blood sample") || combined.Contains("forensic"))
            return "Physical";

        // Use MediaType for audiovisual classification
        return mediaType switch
        {
            "Video" => "Video",
            "Audio" => "Audio",
            "Image" => "Image",
            "Document" => "Document",
            _ => "Document"
        };
    }

    /// <summary>
    /// Assesses the probative value of evidence based on its category, reliability,
    /// relevance indicators, and whether it's direct or circumstantial.
    /// Probative value is the legal weight — distinct from raw persuasive impact.
    /// </summary>
    public static void AssessProbativeValue(EvidenceDocument doc, string summary, string fileContent)
    {
        string lower = $"{summary} {fileContent}".ToLower();
        double probative = 0.5; // Start neutral

        // --- Evidence type base probative value ---
        probative = doc.EvidenceCategory switch
        {
            "Video" => 0.75,      // Video can be highly probative if authentic
            "Image" => 0.65,      // Photos are probative but can be misleading
            "Audio" => 0.70,      // Audio recordings carry weight
            "Physical" => 0.80,   // Physical evidence is highly probative
            "Testimony" => 0.60,  // Testimony base; modified by credibility below
            "Document" => 0.60,   // Documents are solid but abstract
            _ => 0.50
        };

        // --- Reliability adjustment (chain of custody, authenticity) ---
        probative *= 0.5 + doc.ReliabilityScore * 0.5; // Scale by reliability

        // --- Direct vs circumstantial evidence ---
        if (lower.Contains("direct evidence") || lower.Contains("eyewitness") ||
            lower.Contains("saw it") || lower.Contains("firsthand") || lower.Contains("directly"))
            probative = Math.Min(1.0, probative * 1.2); // Direct evidence carries more weight

        if (lower.Contains("circumstantial") || lower.Contains("indirect"))
            probative *= 0.85; // Circumstantial is weaker legally

        // --- Hearsay and reliability red flags ---
        if (lower.Contains("hearsay") || lower.Contains("rumor") || lower.Contains("uncertain"))
            probative *= 0.5;
        if (lower.Contains("expired") || lower.Contains("old") || lower.Contains("outdated"))
            probative *= 0.6;
        if (lower.Contains("unauthenticated") || lower.Contains("unverified"))
            probative *= 0.7;

        // --- Witness credibility for testimony ---
        if (doc.IsTestimonial)
        {
            // Base credibility assessment from keywords
            double credBase = 0.5;
            if (lower.Contains("expert witness") || lower.Contains("forensic expert") ||
                lower.Contains("medical examiner"))
                credBase = 0.85;
            else if (lower.Contains("eyewitness") || lower.Contains("eye witness"))
                credBase = 0.6; // Eyewitness testimony is notoriously unreliable
            else if (lower.Contains("character witness"))
                credBase = 0.45;
            else if (lower.Contains("police officer") || lower.Contains("law enforcement"))
                credBase = 0.75;

            // Red flags for credibility
            if (lower.Contains("inconsistent") || lower.Contains("contradict") ||
                lower.Contains("prior conviction") || lower.Contains("perjured"))
                credBase *= 0.6;
            if (lower.Contains("bias") || lower.Contains("motive to lie") ||
                lower.Contains("interested party"))
                credBase *= 0.7;

            doc.WitnessCredibility = Math.Clamp(credBase, 0.1, 1.0);
            probative *= 0.5 + doc.WitnessCredibility * 0.5; // Credibility scales probative value
        }

        doc.ProbativeValue = Math.Clamp(probative, 0.05, 1.0);
    }
}
