using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.Services;

public sealed class ToyJurorLogicCoefsService
{
    private readonly ToyJurorLogicCoefsDto _loaded;

    public ToyJurorLogicCoefsService()
    {
        _loaded = LoadOrThrow();
    }

    public ToyJurorLogicCoefsDto GetCoefs() => _loaded;

    private static ToyJurorLogicCoefsDto LoadOrThrow()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDir, "Resources", "ToyJurorLogicCoefs.json"),
            Path.Combine(baseDir, "..", "..", "Resources", "ToyJurorLogicCoefs.json"),
            Path.Combine(baseDir, "..", "..", "..", "Resources", "ToyJurorLogicCoefs.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ToyJurorLogicCoefs.json")
        };

        string? jsonPath = null;
        foreach (var p in candidatePaths)
        {
            if (File.Exists(p)) { jsonPath = p; break; }
        }

        if (jsonPath == null)
            throw new FileNotFoundException("Could not find Resources/ToyJurorLogicCoefs.json", "ToyJurorLogicCoefs.json");

        var json = File.ReadAllText(jsonPath);

        var parsed = JsonSerializer.Deserialize<ToyJurorLogicCoefsDto>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed == null)
            throw new InvalidDataException("ToyJurorLogicCoefs.json could not be parsed.");

        parsed.NormalizeAndClamp();
        return parsed;
    }

    public sealed class ToyJurorLogicCoefsDto
    {
        public Dictionary<string, double> Alpha { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Eta { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Zeta { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Chi { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Rho { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public double Beta0 { get; set; }
        public double Beta1 { get; set; }
        public double Gamma1 { get; set; }
        public double Theta0 { get; set; }
        public double Theta1 { get; set; }

        internal void NormalizeAndClamp()
        {
            static void ClampDict(Dictionary<string, double> d)
            {
                if (d == null) return;
                var keys = new List<string>(d.Keys);
                foreach (var k in keys)
                {
                    d[k] = Math.Clamp(d[k], -10.0, 10.0);
                    // If you want to clamp to [0,2] for only factor weights, do it here.
                }
            }

            ClampDict(Alpha);
            ClampDict(Eta);
            ClampDict(Zeta);
            ClampDict(Chi);
            ClampDict(Rho);
        }
    }
}

