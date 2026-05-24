using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

public static partial class JOEService
{
    private static double Logistic(double x) => 1.0 / (1.0 + Math.Exp(-x));
    private static double Clamp01(double x) => Math.Max(0.0, Math.Min(1.0, x));

    public static void ApplyBiasParameterPipeline(IEnumerable<Agent> jurors, double deltaEvidence)
    {
        var jurorList = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (jurorList.Count == 0) return;

        var jurorStates = jurorList.Select(AgentToJurorState).ToList();
        var jurorStatesReadOnly = jurorStates.AsReadOnly();

        var jurorG2s = new double[jurorList.Count];
        var jurorHs = new double[jurorList.Count];
        var jurorWs = new double[jurorList.Count];

        for (int i = 0; i < jurorList.Count; i++)
        {
            var state = jurorStates[i];
            jurorG2s[i] = ComputeG2FromBiasParameters(state, deltaEvidence);
            jurorHs[i] = state.Gamma;
            jurorWs[i] = Math.Exp(state.BetaA * 2.0 + state.BetaB * 2.0 + 1.0);
        }

        double wSum = jurorWs.Sum();
        if (wSum <= 0) wSum = 1.0;

        double M = 0.0;
        for (int i = 0; i < jurorList.Count; i++)
        {
            if (jurorWs[i] <= 0) continue;
            M += (jurorWs[i] / wSum) * jurorG2s[i];
        }

        for (int i = 0; i < jurorList.Count; i++)
        {
            var agent = jurorList[i];
            var state = jurorStates[i];

            double phi = state.Sigma * 2.0;
            double g3 = Clamp01(jurorG2s[i] + (1.0 - jurorHs[i]) * phi * (M - jurorG2s[i]));
            double p = Logistic(5.0 * g3);
            double verdictLean = 0.2 + 0.8 * p;

            agent.VerdictLean = verdictLean;
            agent.Sentiment = 0.5;
        }
    }

    public static JurorState AgentToJurorState(Agent agent)
    {
        return JurorState.FromAgent(agent);
    }

    private static double ComputeG2FromBiasParameters(JurorState state, double deltaEvidence)
    {
        double g1 = Logistic(state.Beta0);
        double rigidity = state.Gamma;
        double driftModifier = 1.0 + (state.BetaA * 0.1) + (state.BetaB * 0.15);
        double narrativeSensitivity = state.BetaB;
        return Clamp01(g1 + 1.0 * deltaEvidence * (1.0 - rigidity) * driftModifier * (1.0 + narrativeSensitivity * 0.1));
    }

    public static void ApplyCoherentDriftDeliberation(
        IReadOnlyList<Agent> jurors,
        double deltaEvidence,
        IReadOnlyList<BiasFactor>? biasFactors = null,
        int maxSampleTries = 2000,
        double coherenceThreshold = 0.9,
        int? seed = null)
    {
        if (jurors == null || jurors.Count == 0) return;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        ToyCoefs coefs;
        try
        {
            var coefsService = new ToyJurorLogicCoefsService();
            coefs = BuildCoefsFromDto(coefsService.GetCoefs());
        }
        catch
        {
            coefs = BuildNeutralToyCoefs();
        }

        var biasLookup = (biasFactors ?? Array.Empty<BiasFactor>())
            .GroupBy(bf => bf.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Weight, StringComparer.OrdinalIgnoreCase);

        var g2s = new double[jurors.Count];
        var hs = new double[jurors.Count];
        var ws = new double[jurors.Count];
        var traitsByIndex = new ToyJurorTraits?[jurors.Count];

        for (int i = 0; i < jurors.Count; i++)
        {
            var agent = jurors[i];
            if (!agent.IsOccupied || !agent.CanVote)
            {
                traitsByIndex[i] = null;
                g2s[i] = 0.5;
                hs[i] = 0.0;
                ws[i] = 0.0;
                continue;
            }

            int perAgentSeed = seed.HasValue ? unchecked(seed.Value + i * 997) : rng.Next(int.MinValue, int.MaxValue);
            var arng = new Random(perAgentSeed);
            var traits = GenerateCoherentTraits(arng, biasLookup, agent, maxSampleTries, coherenceThreshold);
            traitsByIndex[i] = traits;

            double b = ComputeStaticBias(traits, coefs);
            double g1 = Logistic(coefs.Beta0 + coefs.Beta1 * b);

            double lam = ComputeRigidity(traits, coefs);
            double h = ComputeHardness(traits, coefs);

            double g2 = ComputeEvidenceDriftG2(traits, g1, deltaEvidence, coefs);
            double w = ComputeInfluenceWeight(traits, g2, coefs);

            g2s[i] = g2;
            hs[i] = h;
            ws[i] = w;
        }

        double wSum = ws.Sum();
        if (wSum <= 0) wSum = 1.0;

        double M = 0.0;
        for (int i = 0; i < jurors.Count; i++)
        {
            if (ws[i] <= 0) continue;
            M += (ws[i] / wSum) * g2s[i];
        }

        for (int i = 0; i < jurors.Count; i++)
        {
            var agent = jurors[i];
            if (!agent.IsOccupied || !agent.CanVote) continue;

            var traits = traitsByIndex[i];
            if (traits == null) continue;

            double phi = ComputeConformity(traits, coefs);
            double g3 = Clamp01(g2s[i] + (1.0 - hs[i]) * phi * (M - g2s[i]));
            double p = Logistic(coefs.Theta0 + coefs.Theta1 * g3);
            double verdictLean = 0.1 + 0.8 * p;

            agent.VerdictLean = verdictLean;
            agent.Sentiment = 0.5;
        }
    }
}
