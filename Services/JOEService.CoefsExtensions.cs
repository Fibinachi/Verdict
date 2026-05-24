using System.Collections.Generic;
using Verdict.Models;

namespace Verdict.Services;

public static partial class JOEService
{
    private sealed class ToyCoefs
    {
        // Use case-insensitive lookup so JSON coefficient keys are robust across capitalization differences.
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        public Dictionary<string, double> Alpha { get; } = new(KeyComparer);
        public Dictionary<string, double> Eta { get; } = new(KeyComparer);
        public Dictionary<string, double> Zeta { get; } = new(KeyComparer);
        public Dictionary<string, double> Chi { get; } = new(KeyComparer);
        public Dictionary<string, double> Rho { get; } = new(KeyComparer);

        public double Beta0 { get; set; } = 0.0;
        public double Beta1 { get; set; } = 1.0;
        public double Gamma1 { get; set; } = 1.0;
        public double Theta0 { get; set; } = 0.0;
        public double Theta1 { get; set; } = 5.0;

        public double StaticBiasIntercept => Alpha.TryGetValue("intercept", out var v) ? v : 0.0;
    }

    private static ToyCoefs BuildNeutralToyCoefs()
    {
        var coefs = new ToyCoefs
        {
            Beta0 = 0.0,
            Beta1 = 1.0,
            Gamma1 = 1.0,
            Theta0 = 0.0,
            Theta1 = 5.0
        };
        return coefs;
    }

    private static ToyCoefs BuildCoefsFromDto(ToyJurorLogicCoefsService.ToyJurorLogicCoefsDto dto)
    {
        var coefs = new ToyCoefs
        {
            Beta0 = dto.Beta0,
            Beta1 = dto.Beta1,
            Gamma1 = dto.Gamma1,
            Theta0 = dto.Theta0,
            Theta1 = dto.Theta1
        };

        if (dto.Alpha != null)
            foreach (var kv in dto.Alpha)
                coefs.Alpha[kv.Key] = kv.Value;

        if (dto.Eta != null)
            foreach (var kv in dto.Eta)
                coefs.Eta[kv.Key] = kv.Value;

        if (dto.Zeta != null)
            foreach (var kv in dto.Zeta)
                coefs.Zeta[kv.Key] = kv.Value;

        if (dto.Chi != null)
            foreach (var kv in dto.Chi)
                coefs.Chi[kv.Key] = kv.Value;

        if (dto.Rho != null)
            foreach (var kv in dto.Rho)
                coefs.Rho[kv.Key] = kv.Value;

        return coefs;
    }
}

