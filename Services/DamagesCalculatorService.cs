using Verdict.Models;

namespace Verdict.Services
{
    public enum DamageType
    {
        Physical,
        Emotional,
        Reputational,
        Financial,
        Health
    }

    public class DamagesCalculatorService
    {
        public static double CalculateDamages(DamageType type, double baseAmount, Agent agent)
        {
            switch (type)
            {
                case DamageType.Physical:
                    return CalculatePhysicalDamages(baseAmount, agent);
                case DamageType.Emotional:
                    return CalculateEmotionalDamages(baseAmount, agent);
                case DamageType.Reputational:
                    return CalculateReputationalDamages(baseAmount, agent);
                case DamageType.Financial:
                    return CalculateFinancialDamages(baseAmount, agent);
                case DamageType.Health:
                    return CalculateHealthDamages(baseAmount, agent);
                default:
                    return baseAmount;
            }
        }

        private static double CalculatePhysicalDamages(double baseAmount, Agent agent)
        {
            return baseAmount * 1.5;
        }

        private static double CalculateEmotionalDamages(double baseAmount, Agent agent)
        {
            return baseAmount * 2.0;
        }

        private static double CalculateReputationalDamages(double baseAmount, Agent agent)
        {
            return baseAmount;
        }

        private static double CalculateFinancialDamages(double baseAmount, Agent agent)
        {
            return baseAmount;
        }

        private static double CalculateHealthDamages(double baseAmount, Agent agent)
        {
            return baseAmount * 1.2;
        }
    }
}