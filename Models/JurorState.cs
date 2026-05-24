using System;
using System.Collections.Generic;

namespace Verdict.Models;

public class JurorState
{
    public string Name { get; set; } = "New Juror";
    
    public double Beta0 { get; set; } = 0.0;
    
    public double BetaA { get; set; } = 0.0;
    
    public double BetaB { get; set; } = 0.0;
    
    public double Gamma { get; set; } = 0.0;
    
    public double Sigma { get; set; } = 0.1;
    
    public Dictionary<string, double> ExtraParameters { get; set; } = new Dictionary<string, double>();
    
    public static JurorState FromAgent(Agent agent)
    {
        return new JurorState
        {
            Name = agent.Name,
            Beta0 = agent.Bias,
            BetaA = agent.DetailOrientation / 10.0 - 0.5,
            BetaB = agent.Compassion / 10.0 - 0.5,
            Gamma = agent.ConflictAvoidance / 10.0,
            Sigma = agent.MemoryReliability / 20.0,
            ExtraParameters = new Dictionary<string, double>
            {
                ["Age"] = agent.Age,
                ["Openness"] = agent.Openness,
                ["NeedForCognition"] = agent.NeedForCognition,
                ["SuspicionTendency"] = agent.SuspicionTendency,
                ["SystemJustification"] = agent.SystemJustification,
                ["DisgustSensitivity"] = agent.DisgustSensitivity,
                ["AngerReactivity"] = agent.AngerReactivity
            }
        };
    }
}