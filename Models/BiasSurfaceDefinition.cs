using System;
using System.Collections.Generic;

namespace Verdict.Models;

public class BiasSurfaceDefinition
{
    public string JurorName { get; set; } = string.Empty;
    
    public string FunctionString { get; set; } = "B(eA, eB) = tanh(beta0 + beta_a*eA + beta_b*eB + gamma*C)";
    
    public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
    
    public BiasSurfaceDefinition(JurorState state, double groupPressure = 0.0)
    {
        JurorName = state.Name;
        Parameters = new Dictionary<string, double>
        {
            ["beta0"] = state.Beta0,
            ["beta_a"] = state.BetaA,
            ["beta_b"] = state.BetaB,
            ["gamma"] = state.Gamma,
            ["sigma"] = state.Sigma,
            ["C"] = groupPressure
        };
        
        foreach (var kvp in state.ExtraParameters)
        {
            Parameters[kvp.Key] = kvp.Value;
        }
    }
    
    public double Evaluate(double eA, double eB, double C = 0.0)
    {
        double beta0 = Parameters["beta0"];
        double betaA = Parameters["beta_a"];
        double betaB = Parameters["beta_b"];
        double gamma = Parameters["gamma"];
        
        return Math.Tanh(beta0 + betaA * eA + betaB * eB + gamma * C);
    }
}