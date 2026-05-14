using System.Collections.Generic;

namespace Verdict.Models;

public class CauseOfAction
{
    public string Name { get; set; } = string.Empty;
    public List<Element> Elements { get; set; } = new();
    public bool IsLiable { get; set; }
}
