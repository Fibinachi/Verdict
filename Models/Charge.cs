using System.Collections.Generic;

namespace Verdict.Models;

public class Charge
{
    public string Name { get; set; } = string.Empty;
    public List<Element> Elements { get; set; } = new();
    public bool IsGuilty { get; set; }
}
