using System.Collections.Generic;

namespace Verdict.Models;

public class VerdictForm
{
    public List<Charge> Charges { get; set; } = new(); // For Criminal
    public List<CauseOfAction> CausesOfAction { get; set; } = new(); // For Civil
    public decimal DamagesAwarded { get; set; } // For Civil
}
