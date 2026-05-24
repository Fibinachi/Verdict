using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Loads CaseTypeSensitivity.json and provides predictor multipliers per case type.
/// </summary>
public sealed class CaseTypeSensitivityService
{
    private readonly Dictionary<string, CaseTypeSensitivityMatrix> _matrices;
    private readonly CaseTypeSensitivityMatrix _fallback;

    public CaseTypeSensitivityService()
    {
        _matrices = LoadMatrices();
        _fallback = _matrices.TryGetValue("Other", out var m) ? m : CaseTypeSensitivityMatrix.CreateEmpty();
    }

    public CaseTypeSensitivityMatrix GetMatrixForCaseType(string caseType)
    {
        if (string.IsNullOrWhiteSpace(caseType)) return _fallback;

        return _matrices.TryGetValue(NormalizeKey(caseType), out var matrix)
            ? matrix
            : _fallback;
    }

    private static string NormalizeKey(string s)
        => s.Trim().Replace("-", string.Empty).Replace("_", string.Empty);

    private static Dictionary<string, CaseTypeSensitivityMatrix> LoadMatrices()
    {
        // Try content root first; if running tests, use AppContext.BaseDirectory.
        var baseDir = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDir, "Resources", "CaseTypeSensitivity.json"),
            Path.Combine(baseDir, "..", "..", "Resources", "CaseTypeSensitivity.json"),
            Path.Combine(baseDir, "..", "..", "..", "Resources", "CaseTypeSensitivity.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "CaseTypeSensitivity.json")
        };

        string? jsonPath = null;
        foreach (var p in candidatePaths)
        {
            if (File.Exists(p)) { jsonPath = p; break; }
        }

        if (jsonPath == null)
            throw new FileNotFoundException("Could not find Resources/CaseTypeSensitivity.json", "CaseTypeSensitivity.json");

        var json = File.ReadAllText(jsonPath);

        var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var matrices = new Dictionary<string, CaseTypeSensitivityMatrix>(StringComparer.OrdinalIgnoreCase);
        if (parsed == null) return matrices;

        foreach (var (caseType, predictorDict) in parsed)
        {
            var matrix = CaseTypeSensitivityMatrix.CreateEmpty();
            if (predictorDict != null)
            {
                foreach (var (predictorName, multiplier) in predictorDict)
                {
                    // clamp to [0,2]
                    var clamped = Math.Clamp(multiplier, 0.0, 2.0);
                    matrix.PredictorMultipliers[predictorName] = clamped;
                }
            }

            matrices[NormalizeKey(caseType)] = matrix;
        }

        return matrices;
    }
}

