using System.Collections.Generic;

namespace Verdict.Models
{
    /// <summary>
    /// Holds a mapping of predictor names to sensitivity multipliers in range [0.0, 2.0].
    /// </summary>
    public sealed class CaseTypeSensitivityMatrix
    {
public Dictionary<string, double> PredictorMultipliers { get; } = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);

        public static CaseTypeSensitivityMatrix CreateEmpty() => new();
    }
}

