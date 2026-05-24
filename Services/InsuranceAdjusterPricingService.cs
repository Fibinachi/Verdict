using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services
{
    public interface IInsuranceAdjusterPricingService
    {
        InsuranceAdjusterState ComputeState(
            IEnumerable<Agent> jurors,
            CaseFile caseFile,
            double reserveMultiplier = 1.0);
    }

    /// <summary>
    /// Demographics-only heuristic that estimates a plaintiff-favorable verdict probability.
    /// It must not use juror verdict lean/opinion (VerdictLean, TrialEvents, internalized evidence).
    /// </summary>
    public sealed class InsuranceAdjusterPricingService : IInsuranceAdjusterPricingService
    {
        public InsuranceAdjusterState ComputeState(
            IEnumerable<Agent> jurors,
            CaseFile caseFile,
            double reserveMultiplier = 1.0)
        {
            var jurorList = jurors?.Where(j => j != null && j.IsOccupied && j.CanVote).ToList()
                            ?? new List<Agent>();

            double reserve = (caseFile?.InsuranceReserve ?? 0) * reserveMultiplier;

            // Compute demographics-only expected plaintiff verdict probability.
            // Range: 0..1
            double pPlaintiff = ComputeDemographicsOnlyPlaintiffProbability(jurorList);

            // Map probability to expected liability (dollars) using case-level damages estimate.
            // We interpret caseFile.EstimatedSettlement as an empirical expected value already computed
            // from evidence (damages exposure). Here we only use its scale.
            double expectedLiability = caseFile != null ? caseFile.EstimatedSettlement : 0;

            // Heuristic: expected liability increases as plaintiff probability increases.
            // Keep stable scaling: if plaintiff is unlikely, expectedLiability trends toward 0.
            expectedLiability *= pPlaintiff;

            var state = new InsuranceAdjusterState
            {
                ExpectedJuryVerdictProbability = pPlaintiff,
                ExpectedJuryVerdictLabel = LabelFor(pPlaintiff),
                ExpectedLiability = expectedLiability,
                AdjusterReserve = reserve,
                ExpectedSettlementOffer = 0,
                ShouldOfferSettlement = false,
                OfferReason = string.Empty
            };

            // If expected liability exceeds reserve threshold, offer settlement.
            if (reserve > 0 && expectedLiability > reserve)
            {
                // Offer as a discounted fraction of expected liability, leaving margin.
                // More plaintiff probability => slightly more aggressive offer.
                double aggressiveness = 0.55 + (pPlaintiff * 0.35); // 0.55..0.90
                double offer = expectedLiability * aggressiveness;

                state.ShouldOfferSettlement = true;
                state.ExpectedSettlementOffer = offer;
                state.OfferReason = $"Expected plaintiff-favorable verdict probability ({pPlaintiff:P0}) implies expected liability above reserve.";
            }

            return state;
        }

        private static string LabelFor(double pPlaintiff)
        {
            if (pPlaintiff >= 0.75) return "Plaintiff-favorable (strong)";
            if (pPlaintiff >= 0.55) return "Plaintiff-favorable";
            if (pPlaintiff <= 0.25) return "Defense-favorable (strong)";
            if (pPlaintiff <= 0.45) return "Defense-favorable";
            return "Split";
        }

        private static double ComputeDemographicsOnlyPlaintiffProbability(List<Agent> jurors)
        {
            if (jurors == null || jurors.Count == 0) return 0.5;

            // Lightweight scoring: convert selected demographics into a standardized 0..1 influence.
            // IMPORTANT: does not reference VerdictLean, TrialEvents, Memories, Bias.

            double scoreSum = 0;
            foreach (var j in jurors)
            {
                double s = 0.5;

                // Age: very coarse.
                if (j.Age < 30) s += 0.03;
                else if (j.Age < 45) s += 0.01;
                else if (j.Age < 60) s -= 0.01;
                else s -= 0.03;

                // Education: treat higher education as slightly pro-plaintiff in this toy market.
                s += EducationShift(j.EducationLevel);

                // Political: independent baseline, republican slightly defense, democratic slightly plaintiff.
                s += PoliticalShift(j.PoliticalAffiliation);

                // Gender / race: small cosmetic shifts.
                s += GenderShift(j.Gender);
                s += RaceShift(j.Race);

                // Clamp per juror.
                s = Math.Clamp(s, 0.05, 0.95);
                scoreSum += s;
            }

            // Convert average score to probability.
            double p = scoreSum / jurors.Count;
            return Math.Clamp(p, 0.0, 1.0);
        }

        private static double EducationShift(string? education)
        {
            var e = (education ?? string.Empty).ToLowerInvariant();
            if (e.Contains("doctor") || e.Contains("phd")) return 0.05;
            if (e.Contains("master") || e.Contains("j.d") || e.Contains("jd")) return 0.035;
            if (e.Contains("bachelor")) return 0.02;
            if (e.Contains("some college")) return 0.01;
            if (e.Contains("high school")) return -0.01;
            return 0;
        }

        private static double PoliticalShift(string? affiliation)
        {
            var p = (affiliation ?? string.Empty).ToLowerInvariant();
            if (p.Contains("democrat")) return 0.04;
            if (p.Contains("republican")) return -0.04;
            if (p.Contains("libertarian")) return 0.01;
            return 0;
        }

        private static double GenderShift(string? gender)
        {
            var g = (gender ?? string.Empty).ToLowerInvariant();
            if (g.Contains("female")) return 0.01;
            if (g.Contains("male")) return -0.005;
            return 0;
        }

        private static double RaceShift(string? race)
        {
            var r = (race ?? string.Empty).ToLowerInvariant();
            if (r.Contains("black")) return 0.02;
            if (r.Contains("hispanic")) return 0.015;
            if (r.Contains("white")) return -0.01;
            return 0;
        }
    }
}

