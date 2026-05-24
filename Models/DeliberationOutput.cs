using System;
using System.Collections.Generic;

namespace Verdict.Models;

public class DeliberationOutput
{
    public string JurorName { get; set; } = string.Empty;
    
    public double UpdatedBias { get; set; } = 0.0;
    
    public string ReasoningChain { get; set; } = string.Empty;
    
    public double VerdictInclination { get; set; } = 0.5;
    
    public List<string> EvidenceConsidered { get; set; } = new List<string>();
    
    public double GroupPressure { get; set; } = 0.0;
    
    public double Sigma { get; set; } = 0.1;
    
    public static DeliberationOutput FromBiasUpdate(
        JurorState state, 
        BiasSurfaceDefinition surface, 
        BiasTerrain terrain,
        double evidenceA,
        double evidenceB,
        double groupPressure,
        List<string> evidenceConsidered)
    {
        var output = new DeliberationOutput
        {
            JurorName = state.Name,
            EvidenceConsidered = evidenceConsidered,
            GroupPressure = groupPressure,
            Sigma = state.Sigma
        };
        
        double interpolatedBias = terrain.GetInterpolatedBias(evidenceA, evidenceB, surface, groupPressure);
        output.UpdatedBias = interpolatedBias;
        
        output.VerdictInclination = (interpolatedBias + 1.0) / 2.0;
        
        var random = new Random(state.GetHashCode());
        double noise = random.NextDouble() * 2 - 1;
        output.UpdatedBias += noise * state.Sigma;
        output.UpdatedBias = Math.Clamp(output.UpdatedBias, -1.0, 1.0);
        
        output.VerdictInclination = (output.UpdatedBias + 1.0) / 2.0;
        
        output.ReasoningChain = GenerateReasoning(state, evidenceA, evidenceB, groupPressure, output.UpdatedBias);
        
        return output;
    }
    
    private static string GenerateReasoning(JurorState state, double eA, double eB, double groupPressure, double bias)
    {
        var reasoning = new System.Text.StringBuilder();
        
        reasoning.Append($"{state.Name} evaluates evidence: ");
        reasoning.Append($"pro-plaintiff weight={eA:F2}, defense weight={eB:F2}. ");
        reasoning.Append($"Group pressure={groupPressure:F2}. ");
        
        if (bias > 0.2)
            reasoning.Append("Leaning toward plaintiff/prosecution.");
        else if (bias < -0.2)
            reasoning.Append("Leaning toward defense.");
        else
            reasoning.Append("Remains undecided.");
        
        return reasoning.ToString();
    }
}