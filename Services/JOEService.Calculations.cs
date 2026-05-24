using System;
using System.Collections.Generic;
using Verdict.Models;

namespace Verdict.Services;

public static partial class JOEService
{
    private static double ComputeStaticBias(ToyJurorTraits j, ToyCoefs coefs)
    {
        double priorJuryBias = 0.0;
        if (j.PriorJuryOutcome == 2) priorJuryBias = 0.15;
        else if (j.PriorJuryOutcome == 3) priorJuryBias = -0.10;

        return coefs.Alpha.TryGetValue("intercept", out var v) ? v + priorJuryBias : priorJuryBias;
    }

    private static double ComputeRigidity(ToyJurorTraits j, ToyCoefs coefs)
    {
        double x = coefs.Eta.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
        x += coefs.Eta.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
        x += coefs.Eta.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
        x += coefs.Eta.TryGetValue("punitiveness", out var pW) ? pW * j.Punitiveness : 0.0;
        x += coefs.Eta.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
        x += coefs.Eta.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
        x += coefs.Eta.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
        x += coefs.Eta.TryGetValue("primary_history", out var phW) ? phW * j.PrimaryHistory : 0.0;
        x += coefs.Eta.TryGetValue("online_partisan", out var opW) ? opW * j.OnlinePartisan : 0.0;
        x += coefs.Eta.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
        x += coefs.Eta.TryGetValue("home_own", out var hoW) ? hoW * j.HomeOwn : 0.0;
        x += coefs.Eta.TryGetValue("income_band", out var ibW) ? ibW * j.IncomeBand : 0.0;
        x += coefs.Eta.TryGetValue("job_security", out var jsW) ? jsW * j.JobSecurity : 0.0;
        x += coefs.Eta.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
        x += coefs.Eta.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
        x += coefs.Eta.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
        x += coefs.Eta.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
        x += j.ConfirmationBias * 0.15;
        x += j.StatusQuoBias * 0.12;
        x += j.NarrativeCoherence * 0.10;
        x += j.PriorVictimization * 0.3;
        x += j.SystemJustification * 0.15;
        x += j.NeedForClosure * 0.20;

        return Logistic(x);
    }

    private static double ComputeHardness(ToyJurorTraits j, ToyCoefs coefs)
    {
        double x = coefs.Zeta.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
        x += coefs.Zeta.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
        x += coefs.Zeta.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
        x += coefs.Zeta.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
        x += coefs.Zeta.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
        x += coefs.Zeta.TryGetValue("primary_history", out var phW) ? phW * j.PrimaryHistory : 0.0;
        x += coefs.Zeta.TryGetValue("activism", out var aW) ? aW * j.Activism : 0.0;
        x += coefs.Zeta.TryGetValue("online_partisan", out var opW) ? opW * j.OnlinePartisan : 0.0;
        x += coefs.Zeta.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
        x += coefs.Zeta.TryGetValue("home_own", out var hoW) ? hoW * j.HomeOwn : 0.0;
        x += coefs.Zeta.TryGetValue("income_band", out var ibW) ? ibW * j.IncomeBand : 0.0;
        x += coefs.Zeta.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
        x += coefs.Zeta.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
        x += coefs.Zeta.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
        x += coefs.Zeta.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
        x += coefs.Zeta.TryGetValue("education_years", out var eduW) ? eduW * 12.0 : 0.0;
        x += j.HindsightBias * 0.12;
        x += j.NarrativeCoherence * 0.03;
        x += j.StatusQuoBias * 0.10;
        if (j.PriorJuryOutcome == 1) x += 0.20;
        x -= j.SystemJustification * 0.08;
        x -= j.NeedForCognition * 0.10;
        x += j.NeedForClosure * 0.15;
        x -= j.PatienceImpulsivity * 0.04;
        x -= j.UnionMembership * 0.03;
        if (j.ImmigrationGeneration == 1) x += 0.05;
        x += j.DisgustSensitivity * 0.025;
        x += j.AngerReactivity * 0.02;
        x -= j.Compassion * 0.015;
        x -= j.DetailOrientation * 0.02;
        x += j.MemoryReliability * 0.015;
        x += j.SuspicionTendency * 0.02;

        return Logistic(x);
    }

    private static double ComputeInfluenceWeight(ToyJurorTraits j, double G2, ToyCoefs coefs)
    {
        double x = coefs.Chi.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
        x += coefs.Chi.TryGetValue("education_years", out var eduW) ? eduW * 12.0 : 0.0;
        x += coefs.Chi.TryGetValue("elite_private", out var epW) ? epW * j.ElitePrivate : 0.0;
        x += coefs.Chi.TryGetValue("state_flagship", out var sfW) ? sfW * j.StateFlagship : 0.0;
        x += coefs.Chi.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
        x += coefs.Chi.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
        x += coefs.Chi.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
        x += coefs.Chi.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
        x += coefs.Chi.TryGetValue("age", out var ageW) ? ageW * j.AgeYears : 0.0;
        x += coefs.Chi.TryGetValue("certainty", out var certW) ? certW * Math.Abs(G2 - 0.5) : 0.0;
        x += coefs.Chi.TryGetValue("narrative_coherence", out var ncW) ? ncW * j.NarrativeCoherence : 0.0;
        x += coefs.Chi.TryGetValue("social_consensus", out var scW) ? scW * j.SocialConsensus : 0.0;
        x += coefs.Chi.TryGetValue("anchoring", out var anW) ? anW * j.Anchoring * 0.3 : 0.0;
        x += coefs.Chi.TryGetValue("hindsight", out var hbW2) ? hbW2 * j.HindsightBias * -0.2 : 0.0;

        if (j.MediaConsumptionType == "TrueCrime") x += 0.15;
        else if (j.MediaConsumptionType == "Documentary") x += 0.20;
        else if (j.MediaConsumptionType == "LocalNews") x += 0.05;
        else if (j.MediaConsumptionType == "Tabloid") x -= 0.10;

        x += j.Agreeableness * 0.05;
        x += j.DominanceAssertiveness * 0.06;
        x -= j.ConflictAvoidance * 0.04;
        x += j.ScienceLiteracy * 0.025;
        x += j.FinancialLiteracy * 0.025;
        x += j.TechnologyFamiliarity * 0.015;
        x += j.DetailOrientation * 0.02;
        x += j.MemoryReliability * 0.025;
        x = Math.Clamp(x, -20.0, 60.0);

        double w = Math.Exp(x);
        return Math.Max(w, 1e-12);
    }

    private static double ComputeConformity(ToyJurorTraits j, ToyCoefs coefs)
    {
        double x = coefs.Rho.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
        x += coefs.Rho.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
        x += coefs.Rho.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
        x += coefs.Rho.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
        x += coefs.Rho.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
        x += coefs.Rho.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
        x += coefs.Rho.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
        x += coefs.Rho.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
        x += coefs.Rho.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
        x += j.SocialConsensus * 0.08;
        x += j.Anchoring * 0.04;
        x += j.OutcomeBias * 0.05;
        x += j.OnlinePartisan * 0.02;
        x += j.Agreeableness * 0.04;
        x += j.PriorVictimization * 0.10;
        x += j.SystemJustification * 0.12;
        x += j.NeedForClosure * 0.15;
        if (j.PriorJuryOutcome == 1) x -= 0.25;
        x += j.HumorLevityTendency * 0.03;
        x += j.ConflictAvoidance * 0.06;
        if (j.UrbanRuralBackground == 1) x += 0.02;
        else x -= 0.02;
        x += j.MilitaryService * 0.05;
        x += j.UnionMembership * 0.03;
        if (j.ImmigrationGeneration == 1) x -= 0.04;
        else if (j.ImmigrationGeneration == 3) x += 0.04;
        x -= j.DetailOrientation * 0.025;
        x -= j.MemoryReliability * 0.03;
        x -= j.SuspicionTendency * 0.025;
        x -= j.DisgustSensitivity * 0.015;
        x += j.Compassion * 0.02;
        x -= j.AngerReactivity * 0.018;
        return x;
    }

    private static double ComputeNarrativeCoherenceSensitivity(ToyJurorTraits j)
    {
        return (j.NarrativeCoherence - 5.0) / 5.0;
    }

    private static double ComputeEvidenceDriftModifier(ToyJurorTraits traits)
    {
        double modifier = 1.0;
        modifier += (traits.Punitiveness - 5.0) * 0.03;
        modifier += traits.MediaConsumptionType == "TrueCrime" ? 0.08 : 0.0;
        modifier += traits.MediaConsumptionType == "Documentary" ? -0.05 : 0.0;
        modifier += traits.MediaConsumptionType == "Tabloid" ? 0.04 : 0.0;
        modifier += (5.0 - traits.Agreeableness) * 0.02;
        modifier += (traits.PriorVictimization - 1.0) * 0.05;
        modifier += (5.0 - traits.NeedForCognition) * 0.02;
        modifier += (traits.SystemJustification - 5.0) * 0.015;
        modifier += (5.0 - traits.DetailOrientation) * 0.015;
        modifier += (traits.MemoryReliability - 5.0) * 0.012;
        modifier += -(traits.SuspicionTendency - 5.0) * 0.02;
        modifier += (traits.DisgustSensitivity - 5.0) * 0.018;
        modifier += (5.0 - traits.Compassion) * 0.015;
        modifier += (traits.AngerReactivity - 5.0) * 0.02;
        modifier += (traits.ScienceLiteracy - 5.0) * 0.01;
        modifier += (traits.FinancialLiteracy - 5.0) * 0.01;
        modifier += (traits.TechnologyFamiliarity - 5.0) * 0.008;
        return modifier;
    }

    private static double ComputeEvidenceDriftG2(ToyJurorTraits traits, double g1, double deltaEvidence, ToyCoefs coefs)
    {
        double narrativeSensitivity = ComputeNarrativeCoherenceSensitivity(traits);
        double driftModifier = ComputeEvidenceDriftModifier(traits);
        return Clamp01(g1 + coefs.Gamma1 * deltaEvidence * (1.0 - ComputeRigidity(traits, coefs)) * driftModifier * (1.0 + narrativeSensitivity * 0.1));
    }
}
