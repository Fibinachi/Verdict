using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Extracts coarse demographic/political cues for defendant and victim from the text of an EvidenceDocument.
/// This replaces brittle substring-only heuristics by using regex patterns + synonym buckets
/// and returns a confidence-weighted EvidenceCueProfile.
/// </summary>
public sealed class EvidenceCueExtractor
{
    private static readonly Regex SentencesSplit = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);


    // Lightweight synonym buckets. Add/extend over time without touching math/pipeline code.
    private static readonly Dictionary<string, string[]> PoliticalSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Democratic"] = new[] { "democrat", "democratic" },
        ["Republican"] = new[] { "republican", "republic" },
        ["Libertarian"] = new[] { "libertarian" },
        ["Independent"] = new[] { "independent", "unaffiliated" }
    };

    private static readonly Dictionary<string, string[]> RaceSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["White"] = new[] { "white" },
        ["Black"] = new[] { "black", "african american" },
        ["Hispanic/Latino"] = new[] { "hispanic", "latino", "latina" },
        ["Asian"] = new[] { "asian" },
        ["Native American"] = new[] { "native american", "indigenous" },
        ["Middle Eastern"] = new[] { "middle eastern", "arab" },
        ["Multiracial"] = new[] { "multiracial" }
    };

    private static readonly Dictionary<string, string[]> GenderSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Male"] = new[] { "male", "man", "he", "his" },
        ["Female"] = new[] { "female", "woman", "she", "her" },
        ["Non-binary/Other"] = new[] { "non-binary", "they", "their" }
    };

    private static readonly Dictionary<string, string[]> ReligionSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Catholic"] = new[] { "catholic" },
        ["Protestant"] = new[] { "protestant" },
        ["Jewish"] = new[] { "jewish", "judaism" },
        ["Muslim"] = new[] { "muslim", "islam" },
        ["Non-religious"] = new[] { "non-religious", "atheist", "agnostic" },
        ["Other Christian"] = new[] { "christian" }
    };

    private static readonly Dictionary<string, string[]> EducationSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["High School"] = new[] { "high school" },
        ["Some College"] = new[] { "some college", "college" },
        ["Associate Degree"] = new[] { "associate" },
        ["Bachelor's Degree"] = new[] { "bachelor", "b.a", "bachelor's" },
        ["Master's Degree"] = new[] { "master", "m.a", "m.s", "master's" },
        ["Doctorate"] = new[] { "doctorate", "phd", "ph.d", "ph.d" }
    };


        // Age patterns like: "aged 34", "age 17", "34-year-old"
    private static readonly Regex AgeRegex = new(@"(?:(?:aged|age)\s*(?<age>\d{1,3})|(?<age2>\d{1,3})\s*-\s*year\s*-\s*old)", RegexOptions.IgnoreCase | RegexOptions.Compiled);


    public EvidenceCueProfile Extract(string? summaryOrText)
    {
        var text = summaryOrText ?? string.Empty;
        var profile = new EvidenceCueProfile();

        if (string.IsNullOrWhiteSpace(text))
            return profile;

        var lower = text.ToLowerInvariant();

        // Heuristic entity-role confidence: if text mentions defendant/victim explicitly.
        double roleConf = 0.5;
        if (ContainsAny(lower, "defendant")) roleConf += 0.2;
        if (ContainsAny(lower, "victim")) roleConf += 0.2;
        if (ContainsAny(lower, "accuser")) roleConf += 0.05;
        profile.EntityRoleConfidence = Math.Clamp(roleConf, 0.0, 1.0);

        // Partition into (best-effort) sentences; assign cues based on nearest role keywords.
        var sentences = SentencesSplit.Split(text);
        if (sentences.Length == 0) sentences = new[] { text };

        foreach (var s in sentences)
        {
            var sl = s.ToLowerInvariant();

            bool likelyDef = ContainsAny(sl, "defendant") || ContainsAny(sl, "suspect") || ContainsAny(sl, "accused");
            bool likelyVict = ContainsAny(sl, "victim") || ContainsAny(sl, "accuser") || ContainsAny(sl, "survivor");

            // If roles are not explicit, we conservatively treat the first entity mention as defendant.
            if (!likelyDef && !likelyVict)
            {
                // If summary contains “alleged” and “injuries” etc; still uncertain.
                likelyDef = string.IsNullOrWhiteSpace(profile.Defendant.PoliticalAffiliation) && !string.IsNullOrWhiteSpace(sl);
                likelyVict = !likelyDef && string.IsNullOrWhiteSpace(profile.Victim.PoliticalAffiliation);
            }

            if (likelyDef) ApplyCues(s, profile.Defendant);
            if (likelyVict) ApplyCues(s, profile.Victim);
        }

        // If neither side got anything, attempt whole-text extraction.
        if (!profile.Defendant.HasAnyCues() && !profile.Victim.HasAnyCues())
        {
            ApplyCues(text, profile.Defendant);
        }

        return profile;
    }

    private static void ApplyCues(string text, EvidenceCueProfile.CueSet cueSet)
    {
        var lower = text.ToLowerInvariant();

        // Political
        foreach (var (key, syns) in PoliticalSynonyms)
        {
            if (SynContainsAny(lower, syns))
            {
                cueSet.PoliticalAffiliation = key;
                cueSet.CueConfidences["PoliticalAffiliation"] = 0.7;
                break;
            }
        }

        // Race
        foreach (var (key, syns) in RaceSynonyms)
        {
            if (SynContainsAny(lower, syns))
            {
                cueSet.Race = key;
                cueSet.CueConfidences["Race"] = 0.7;
                break;
            }
        }

        // Gender
        foreach (var (key, syns) in GenderSynonyms)
        {
            if (SynContainsAny(lower, syns))
            {
                cueSet.Gender = key;
                cueSet.CueConfidences["Gender"] = 0.6;
                break;
            }
        }

        // Age
        var m = AgeRegex.Match(text);
        if (m.Success)
        {
            var ageStr = m.Groups["age"].Success ? m.Groups["age"].Value : m.Groups["age2"].Value;
            if (int.TryParse(ageStr, out var age) && age >= 0 && age <= 120)
            {
                cueSet.AgeYears = age;
                cueSet.CueConfidences["Age"] = 0.65;
            }
        }

        // Education
        foreach (var (key, syns) in EducationSynonyms)
        {
            if (SynContainsAny(lower, syns))
            {
                cueSet.EducationLevel = key;
                cueSet.CueConfidences["EducationLevel"] = 0.65;
                break;
            }
        }

        // Religion
        foreach (var (key, syns) in ReligionSynonyms)
        {
            if (SynContainsAny(lower, syns))
            {
                cueSet.Religion = key;
                cueSet.CueConfidences["Religion"] = 0.65;
                break;
            }
        }
    }

    private static bool ContainsAny(string lower, string needle)
        => lower.Contains(needle.ToLowerInvariant());

    private static bool ContainsAny(string lower, string[] needles)
    {
        foreach (var n in needles)
            if (lower.Contains(n.ToLowerInvariant())) return true;
        return false;
    }

    private static bool SynContainsAny(string lower, string[] synonyms)
    {
        foreach (var s in synonyms)
        {
            if (lower.Contains(s.ToLowerInvariant())) return true;
        }
        return false;
    }
}

