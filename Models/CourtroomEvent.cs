using System;
using System.Collections.Generic;

namespace Verdict.Models;

public class CourtroomEvent
{
    public string Description { get; set; } = string.Empty;
    public List<AgentRole> VisibleTo { get; set; } = new();
    public bool IsSidebar { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
