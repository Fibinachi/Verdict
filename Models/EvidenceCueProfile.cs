using System;
using System.Collections.Generic;
using System.Linq;

namespace Verdict.Models;

/// <summary>
/// Structured cues extracted from an EvidenceDocument (summary/details).
/// Used to drive predictor contributions without relying on fragile substring heuristics.
/// </summary>
public sealed class EvidenceCueProfile
{
    public EvidenceCueProfile()
    {
        Defendant = new CueSet();
        Victim = new CueSet();
    }

    public CueSet Defendant { get; }
    public CueSet Victim { get; }

    /// <summary>
    /// Confidence 0..1 that the extractor correctly identified defendant vs victim entities.
    /// </summary>
    public double EntityRoleConfidence { get; set; } = 0.5;

    public sealed class CueSet
    {
        // Politics: maps to a coarse band used by the toy model
        public string? PoliticalAffiliation { get; set; }

        public string? Race { get; set; }
        public string? Gender { get; set; }

        public int? AgeYears { get; set; }
        public string? EducationLevel { get; set; }

        public string? Religion { get; set; }

        /// <summary>
        /// Smooth confidence that each cue was extracted correctly.
        /// </summary>
        public Dictionary<string, double> CueConfidences { get; } = new(StringComparer.OrdinalIgnoreCase);

        public double Confidence(string cueName, double defaultValue = 0.5)
            => CueConfidences.TryGetValue(cueName, out var v) ? v : defaultValue;

        public bool HasAnyCues()
            => new[] { PoliticalAffiliation, Race, Gender, AgeYears?.ToString(), EducationLevel, Religion }
                .Any(s => !string.IsNullOrWhiteSpace(s));
    }
}

