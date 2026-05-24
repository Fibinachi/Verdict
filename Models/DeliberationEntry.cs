using System;

namespace Verdict.Models;

/// <summary>
/// Represents a single entry in the jury deliberation log.
/// </summary>
public class DeliberationEntry
{
    public string Speaker { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool IsJuror { get; set; }
}