using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

public interface IJurorOpinionReportService
{
    (JurorState state, BiasSurfaceDefinition surface, BiasTerrain terrain) InitializeJuror(Agent agent, double groupPressure = 0.0);
    
    DeliberationOutput ProcessJuror(JurorState state, BiasSurfaceDefinition surface, BiasTerrain terrain, 
        double evidenceA, double evidenceB, double groupPressure, List<string> evidenceConsidered);
    
    List<DeliberationOutput> ProcessAllJurors(IEnumerable<Agent> jurors, double groupPressure = 0.0);
}

public class JurorOpinionReportService : IJurorOpinionReportService
{
    public (JurorState state, BiasSurfaceDefinition surface, BiasTerrain terrain) InitializeJuror(Agent agent, double groupPressure = 0.0)
    {
        var state = JurorState.FromAgent(agent);
        var surface = new BiasSurfaceDefinition(state, groupPressure);
        var terrain = new BiasTerrain(surface, groupPressure);
        
        return (state, surface, terrain);
    }
    
    public DeliberationOutput ProcessJuror(JurorState state, BiasSurfaceDefinition surface, BiasTerrain terrain,
        double evidenceA, double evidenceB, double groupPressure, List<string> evidenceConsidered)
    {
        return DeliberationOutput.FromBiasUpdate(state, surface, terrain, evidenceA, evidenceB, groupPressure, evidenceConsidered);
    }
    
    public List<DeliberationOutput> ProcessAllJurors(IEnumerable<Agent> jurors, double groupPressure = 0.0)
    {
        var results = new List<DeliberationOutput>();
        
        foreach (var juror in jurors.Where(j => j.IsOccupied && j.CanVote))
        {
            var (state, surface, terrain) = InitializeJuror(juror, groupPressure);
            
            double evidenceA = juror.TrialEvents.Count > 0 ? 0.5 : 0.0;
            double evidenceB = juror.TrialEvents.Count > 0 ? -0.5 : 0.0;
            
            var evidenceConsidered = juror.TrialEvents
                .Take(3)
                .Select(e => e.Content ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            var output = ProcessJuror(state, surface, terrain, evidenceA, evidenceB, groupPressure, evidenceConsidered);
            results.Add(output);
            
            juror.VerdictLean = output.VerdictInclination;
            juror.Bias = output.UpdatedBias;
        }
        
        return results;
    }
}