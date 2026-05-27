using System;

namespace Verdict.Models
{
    /// <summary>
    /// A single snapshot of a juror's verdict lean at a point in time,
    /// used to track opinion evolution through the trial and deliberation.
    /// </summary>
    public class OpinionSnapshot
    {
        /// <summary>Round number (trial phase uses 0, deliberation uses round #)</summary>
        public int Round { get; set; }

        /// <summary>Phase label (e.g., "Discovery", "Pretrial", "Opening", "Round 3")</summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>Verdict lean at this point (0=defense, 1=prosecution)</summary>
        public double VerdictLean { get; set; }

        /// <summary>What caused the change (e.g., "Evidence: Exhibit 3", "Deliberation: Jennifer Ward")</summary>
        public string Cause { get; set; } = string.Empty;

        /// <summary>Timestamp for ordering</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
