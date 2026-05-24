using System;
using System.ComponentModel;
using Verdict.Models;

namespace Verdict.Models
{
    /// <summary>
    /// Computed, UI-bound state for an InsuranceAdjuster.
    /// </summary>
    public sealed class InsuranceAdjusterState : ObservableObject
    {
        private double _expectedJuryVerdictProbability;
        private string _expectedJuryVerdictLabel = string.Empty;
        private double _expectedLiability; // expected dollar value if verdict favors plaintiff
        private double _adjusterReserve;   // which reserve we compare against
        private double _expectedSettlementOffer;
        private bool _shouldOfferSettlement;
        private string _offerReason = string.Empty;

        /// <summary>
        /// Probability that the jury reaches a plaintiff-favorable verdict (demographics-only heuristic).
        /// </summary>
        public double ExpectedJuryVerdictProbability
        {
            get => _expectedJuryVerdictProbability;
            set => SetProperty(ref _expectedJuryVerdictProbability, value);
        }

        /// <summary>
        /// Human-readable label (e.g. "Plaintiff-favorable" / "Defense-favorable" / "Split").
        /// </summary>
        public string ExpectedJuryVerdictLabel
        {
            get => _expectedJuryVerdictLabel;
            set => SetProperty(ref _expectedJuryVerdictLabel, value ?? string.Empty);
        }

        /// <summary>
        /// Expected dollar liability if plaintiff-favorable outcome occurs.
        /// </summary>
        public double ExpectedLiability
        {
            get => _expectedLiability;
            set => SetProperty(ref _expectedLiability, value);
        }

        /// <summary>
        /// Reserve threshold used for the adjuster (typically derived from case InsuranceReserve).
        /// </summary>
        public double AdjusterReserve
        {
            get => _adjusterReserve;
            set => SetProperty(ref _adjusterReserve, value);
        }

        /// <summary>
        /// Settlement offer computed when reserve is exceeded.
        /// </summary>
        public double ExpectedSettlementOffer
        {
            get => _expectedSettlementOffer;
            set => SetProperty(ref _expectedSettlementOffer, value);
        }

        public bool ShouldOfferSettlement
        {
            get => _shouldOfferSettlement;
            set => SetProperty(ref _shouldOfferSettlement, value);
        }

        /// <summary>
        /// Short reason displayed in the UI.
        /// </summary>
        public string OfferReason
        {
            get => _offerReason;
            set => SetProperty(ref _offerReason, value ?? string.Empty);
        }
    }
}

