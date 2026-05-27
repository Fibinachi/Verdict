using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Centralized burden-of-proof thresholds for jury decision-making.
/// 
/// Criminal cases require proof "beyond a reasonable doubt" — a much higher standard
/// than civil cases which only require a "preponderance of the evidence" (>50%).
/// 
/// This class provides mode-aware thresholds so that a juror at 0.70 lean in a
/// criminal case is correctly treated as having REASONABLE DOUBT (functionally
/// NOT GUILTY), while the same 0.70 lean in a civil case is LIABLE.
/// </summary>
public static class BurdenOfProof
{
    /// <summary>
    /// The lean threshold above which a juror is considered to have voted
    /// guilty/liable. In criminal cases, this reflects "beyond a reasonable doubt."
    /// </summary>
    public const double CriminalConvictionThreshold = 0.85;
    public const double CivilConvictionThreshold = 0.50;

    /// <summary>
    /// The lean threshold below which a juror leans toward the defense/defendant.
    /// For criminal cases, anything between this and the conviction threshold
    /// is "reasonable doubt" — the juror thinks the defendant is probably guilty
    /// but the prosecution hasn't met its burden.
    /// </summary>
    public const double DefenseThreshold = 0.50;

    /// <summary>
    /// Returns the lean value above which a juror votes guilty/liable.
    /// </summary>
    public static double GetConvictionThreshold(bool isCriminal) =>
        isCriminal ? CriminalConvictionThreshold : CivilConvictionThreshold;

    /// <summary>
    /// Returns the lean value below which a juror leans defense.
    /// </summary>
    public static double GetDefenseThreshold(bool isCriminal) =>
        DefenseThreshold; // Same for both modes

    /// <summary>
    /// Returns the conviction threshold for a given case mode.
    /// </summary>
    public static double GetConvictionThreshold(CaseMode mode) =>
        mode == CaseMode.Criminal ? CriminalConvictionThreshold : CivilConvictionThreshold;

    /// <summary>
    /// Counts how many jurors would vote guilty/liable given the case mode.
    /// In criminal cases, only jurors above the CriminalConvictionThreshold count.
    /// </summary>
    public static int CountProsecution(IEnumerable<Agent> jurors, bool isCriminal)
    {
        double threshold = GetConvictionThreshold(isCriminal);
        return jurors.Count(j => j.VerdictLean > threshold);
    }

    /// <summary>
    /// Counts how many jurors would vote not-guilty/not-liable given the case mode.
    /// In criminal cases, this includes jurors in the "reasonable doubt" zone
    /// (between DefenseThreshold and CriminalConvictionThreshold).
    /// </summary>
    public static int CountDefense(IEnumerable<Agent> jurors, bool isCriminal)
    {
        double threshold = GetConvictionThreshold(isCriminal);
        return jurors.Count(j => j.VerdictLean <= threshold);
    }

    /// <summary>
    /// Returns the position label for a juror given their lean and the case mode.
    /// Criminal: "GUILTY" | "REASONABLE DOUBT" | "NOT GUILTY"
    /// Civil:    "LIABLE" | "UNDECIDED" | "NOT LIABLE"
    /// </summary>
    public static string GetPositionLabel(double lean, bool isCriminal)
    {
        double conviction = GetConvictionThreshold(isCriminal);

        if (lean > conviction)
            return isCriminal ? "GUILTY" : "LIABLE";

        if (lean < DefenseThreshold)
            return isCriminal ? "NOT GUILTY" : "NOT LIABLE";

        // In the zone between defense threshold and conviction threshold
        return isCriminal ? "REASONABLE DOUBT" : "UNDECIDED";
    }

    /// <summary>
    /// Returns a short label for the prosecution side.
    /// </summary>
    public static string ProsecutionLabel(bool isCriminal) =>
        isCriminal ? "Prosecution" : "Plaintiff";

    /// <summary>
    /// Returns a short label for the defense side.
    /// </summary>
    public static string DefenseLabel() => "Defense";
}
