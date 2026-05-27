using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Handles progressive memory decay and content fuzzification for agent memories.
/// Extracted from Agent.cs to separate concerns — this is pure computational logic
/// with no UI dependencies.
/// </summary>
public static class MemoryDecayService
{
    private static readonly Regex NumberPattern = new(
        @"\b\d{1,3}(?:,\d{3})*(?:\.\d+)?\b",
        RegexOptions.Compiled);

    private static readonly Regex ExhibitRefPattern = new(
        @"\b(?:Exhibit|Ex|Exh)\.?\s*#?\s*(\d+|[A-Z])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Decays the strength of all memories and fuzzifies their content over time.
    /// Exact details become approximate, then vague ranges, then generic impressions.
    /// Very weak memories (strength &lt; 0.05) are pruned entirely.
    /// </summary>
    /// <param name="memories">The background memories collection.</param>
    /// <param name="trialEvents">The trial event memories collection.</param>
    /// <param name="factor">The decay factor (default 0.95).</param>
    public static void DecayMemories(
        ObservableCollection<MemoryEntry> memories,
        ObservableCollection<MemoryEntry> trialEvents,
        double factor = 0.95)
    {
        PruneAndFuzzify(memories, factor);
        PruneAndFuzzify(trialEvents, factor);
    }

    private static void PruneAndFuzzify(ObservableCollection<MemoryEntry> entries, double factor)
    {
        var toRemove = new List<MemoryEntry>();

        foreach (var entry in entries)
        {
            entry.Strength *= factor;

            if (entry.Strength < 0.05)
            {
                toRemove.Add(entry);
                continue;
            }

            FuzzifyContent(entry);
        }

        foreach (var entry in toRemove)
            entries.Remove(entry);
    }

    /// <summary>
    /// Progressively reduces the precision of memory content as strength fades:
    /// 0.70+ → crisp and exact (no change)
    /// 0.40-0.70 → approximate, rounded ("about $2,500")
    /// 0.20-0.40 → vague range ("$2,000-$3,000")
    /// 0.05-0.20 → generic impression ("some amount of money")
    /// </summary>
    public static void FuzzifyContent(MemoryEntry memory)
    {
        var content = memory.Content;
        var strength = memory.Strength;

        if (strength >= 0.70) return; // Still crisp

        // Find all numbers (including dollar amounts, counts, etc.)
        var matches = NumberPattern.Matches(content);
        if (matches.Count == 0) return; // No numbers to fuzzify

        if (strength >= 0.40)
        {
            // Approximate: round to 1-2 significant figures
            content = NumberPattern.Replace(content, m => ApproximateNumber(m.Value));
        }
        else if (strength >= 0.20)
        {
            // Vague range: expand to a range
            content = NumberPattern.Replace(content, m => RangeNumber(m.Value));
        }
        else
        {
            // Very vague: replace with generic descriptions
            content = NumberPattern.Replace(content, m => VagueNumber(m.Value));
        }

        memory.Content = content;
    }

    /// <summary>
    /// Scans existing memories for content related to the new statement.
    /// If the statement references exhibits (e.g., "Exhibit 1", "Ex. A"),
    /// any existing memory mentioning the same exhibit gets a strength boost,
    /// effectively refreshing that memory against decay.
    /// </summary>
    public static void ReinforceRelatedMemories(
        ObservableCollection<MemoryEntry> memories,
        ObservableCollection<MemoryEntry> trialEvents,
        string newStatement)
    {
        var exhibitRefs = ExhibitRefPattern.Matches(newStatement)
            .Select(m => m.Value.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        if (exhibitRefs.Count == 0) return;

        foreach (var memory in trialEvents.Concat(memories))
        {
            if (exhibitRefs.Any(refText => memory.Content.Contains(refText, StringComparison.OrdinalIgnoreCase)))
            {
                memory.Strength = Math.Min(1.0, memory.Strength * 1.5);
            }
        }
    }

    /// <summary>
    /// Adds a new trial observation or reinforces an existing one.
    /// </summary>
    public static void RecordTrialEvent(
        ObservableCollection<MemoryEntry> trialEvents,
        string content,
        double biasInfluence)
    {
        var existing = trialEvents.FirstOrDefault(m => m.Content == content);
        if (existing != null)
        {
            existing.Strength *= (1.0 + biasInfluence);
        }
        else
        {
            trialEvents.Add(new MemoryEntry { Content = content, Strength = 1.0 + biasInfluence });
        }
    }

    /// <summary>
    /// Adds a new background memory or reinforces an existing one.
    /// </summary>
    public static void ReinforceMemory(
        ObservableCollection<MemoryEntry> memories,
        string content,
        double biasInfluence)
    {
        var existing = memories.FirstOrDefault(m => m.Content == content);
        if (existing != null)
        {
            existing.Strength *= (1.0 + biasInfluence);
        }
        else
        {
            memories.Add(new MemoryEntry { Content = content, Strength = 1.0 + biasInfluence });
        }
    }

    /// <summary>
    /// Rounds a number string to ~1-2 significant figures with an "about" prefix.
    /// E.g., "2,546" → "about 2,500"  |  "42" → "about 40"
    /// </summary>
    public static string ApproximateNumber(string numStr)
    {
        if (!TryParseCleanNumber(numStr, out double value))
            return numStr;

        if (value >= 1_000_000)
            return $"about {RoundToSignificant(value, 2):N0}";
        if (value >= 1_000)
            return $"about {RoundToSignificant(value, 2):N0}";
        if (value >= 100)
            return $"about {RoundToSignificant(value, 1):N0}";
        if (value >= 10)
            return $"about {RoundToSignificant(value, 1):N0}";

        return $"about {value:F0}";
    }

    /// <summary>
    /// Expands a number to a range. E.g., "2,546" → "2,000-3,000" | "42" → "40-50"
    /// </summary>
    public static string RangeNumber(string numStr)
    {
        if (!TryParseCleanNumber(numStr, out double value))
            return numStr;

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        double lower = Math.Floor(value / magnitude) * magnitude;
        double upper = lower + magnitude;

        // For small numbers, use tighter ranges
        if (value < 10) return $"{Math.Max(0, (int)value - 2)}-{(int)value + 2}";
        if (value < 100) return $"{RoundToSignificant(lower, 1):N0}-{RoundToSignificant(upper, 1):N0}";

        return $"{lower:N0}-{upper:N0}";
    }

    /// <summary>
    /// Replaces a number with a generic qualitative description.
    /// E.g., "2,546" → "some amount"  |  "42" → "a few"
    /// </summary>
    public static string VagueNumber(string numStr)
    {
        if (!TryParseCleanNumber(numStr, out double value))
            return numStr;

        if (value >= 1_000_000) return "a very large amount";
        if (value >= 100_000) return "a large amount";
        if (value >= 1_000) return "thousands";
        if (value >= 100) return "hundreds";
        if (value >= 10) return "dozens";
        if (value >= 2) return "a few";

        return "a single";
    }

    /// <summary>
    /// Strips commas from a number string and parses it.
    /// </summary>
    public static bool TryParseCleanNumber(string numStr, out double value)
    {
        return double.TryParse(numStr.Replace(",", ""), out value);
    }

    /// <summary>
    /// Rounds a number to the specified number of significant figures.
    /// </summary>
    public static double RoundToSignificant(double value, int significantDigits)
    {
        if (value == 0) return 0;
        double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(value))) - significantDigits + 1);
        return Math.Round(value / scale) * scale;
    }
}
