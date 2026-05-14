using System;

namespace Verdict.Models;

public class MemoryEntry : ObservableObject
{
    private double _strength = 1.0;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsFromDocument { get; set; }
    public string Source { get; set; } = string.Empty;
    
    public double Strength
    {
        get => _strength;
        set => SetProperty(ref _strength, value);
    }
}
