using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Tests;

public class JurorOpinionReportTests : BaseTestClass
{
    public static void RunTests()
    {
        Console.WriteLine("\n=== JUROR OPINION REPORT TESTS ===");
        ResetCounters();
        
        TestJurorStateCreation();
        TestBiasSurfaceDefinition();
        TestBiasTerrain();
        TestDeliberationOutput();
        TestJurorOpinionReportService();
        
        var results = GetResults();
        Console.WriteLine($"  JUROR OPINION REPORT TESTS RESULT: {results.passed} passed, {results.failed} failed");
    }
    
    private static void TestJurorStateCreation()
    {
        Console.WriteLine("\n─── JurorState Creation ───");
        
        var agent = new Agent 
        { 
            Name = "Test Juror", 
            Bias = 0.2, 
            DetailOrientation = 7.0,
            Compassion = 8.0,
            ConflictAvoidance = 6.0,
            MemoryReliability = 8.5
        };
        
        var state = JurorState.FromAgent(agent);
        
        Assert(state.Name == "Test Juror", "JurorState.Name matches agent name");
        Assert(Math.Abs(state.Beta0 - 0.2) < 0.01, "JurorState.Beta0 matches agent bias");
        Assert(state.BetaA > -1, "JurorState.BetaA is computed");
        Assert(state.BetaB > -1, "JurorState.BetaB is computed");
        Assert(state.Gamma >= 0, "JurorState.Gamma is non-negative");
        Assert(state.Sigma > 0, "JurorState.Sigma is positive");
    }
    
    private static void TestBiasSurfaceDefinition()
    {
        Console.WriteLine("\n─── BiasSurfaceDefinition ───");
        
        var state = new JurorState
        {
            Name = "Test Juror",
            Beta0 = 0.0,
            BetaA = 0.1,
            BetaB = -0.1,
            Gamma = 0.2,
            Sigma = 0.1
        };
        
        var surface = new BiasSurfaceDefinition(state, 0.0);
        
        Assert(surface.JurorName == "Test Juror", "Surface JurorName matches");
        Assert(surface.Parameters.ContainsKey("beta0"), "Parameters contains beta0");
        Assert(surface.Parameters["beta0"] == 0.0, "beta0 parameter value correct");
        Assert(surface.FunctionString.Contains("tanh"), "Function string contains tanh");
        
        double result = surface.Evaluate(0, 0, 0);
        Assert(Math.Abs(result) < 1.0, "Evaluate returns value in valid range");
        
        result = surface.Evaluate(1, 1, 0);
        Assert(Math.Abs(result) <= 1.0, "Evaluate with inputs returns value in tanh range");
    }
    
    private static void TestBiasTerrain()
    {
        Console.WriteLine("\n─── BiasTerrain ───");
        
        var state = new JurorState
        {
            Name = "Test Juror",
            Beta0 = 0.0,
            BetaA = 0.1,
            BetaB = 0.1,
            Gamma = 0.1,
            Sigma = 0.1
        };
        
        var surface = new BiasSurfaceDefinition(state, 0.0);
        var terrain = new BiasTerrain(surface, 0.0, 80);
        
        Assert(terrain.GridEA.Length == 80, "GridEA has 80 points");
        Assert(terrain.GridEB.Length == 80, "GridEB has 80 points");
        Assert(terrain.GridZ.GetLength(0) == 80, "GridZ is 80x80");
        Assert(terrain.GridEA[0] == -3.0, "GridEA starts at -3");
        Assert(terrain.GridEA[79] == 3.0, "GridEA ends at +3");
        
        double interpolated = terrain.GetInterpolatedBias(0, 0, surface, 0);
        Assert(Math.Abs(interpolated) <= 1.0, "Interpolated bias is in valid range");
    }
    
    private static void TestDeliberationOutput()
    {
        Console.WriteLine("\n─── DeliberationOutput ───");
        
        var state = new JurorState
        {
            Name = "Test Juror",
            Beta0 = 0.2,
            BetaA = 0.1,
            BetaB = 0.1,
            Gamma = 0.1,
            Sigma = 0.1
        };
        
        var surface = new BiasSurfaceDefinition(state, 0.0);
        var terrain = new BiasTerrain(surface, 0.0);
        
        var evidence = new List<string> { "Evidence A", "Evidence B" };
        var output = DeliberationOutput.FromBiasUpdate(state, surface, terrain, 0.5, -0.3, 0.0, evidence);
        
        Assert(output.JurorName == "Test Juror", "Output JurorName matches");
        Assert(output.VerdictInclination >= 0.0 && output.VerdictInclination <= 1.0, "VerdictInclination is normalized");
        Assert(!string.IsNullOrEmpty(output.ReasoningChain), "ReasoningChain is generated");
        Assert(output.EvidenceConsidered.Count == 2, "EvidenceConsidered contains all items");
    }
    
    private static void TestJurorOpinionReportService()
    {
        Console.WriteLine("\n─── JurorOpinionReportService ───");
        
        var service = new JurorOpinionReportService();
        
        var jurors = new List<Agent>
        {
            new Agent { Name = "Juror 1", IsOccupied = true, Role = AgentRole.Juror, Bias = 0.3, VerdictLean = 0.6 },
            new Agent { Name = "Juror 2", IsOccupied = true, Role = AgentRole.Juror, Bias = -0.2, VerdictLean = 0.4 }
        };
        
        var results = service.ProcessAllJurors(jurors, 0.1);
        
        Assert(results.Count == 2, "Service processes all jurors");
        Assert(results[0].JurorName == "Juror 1", "First juror processed");
        Assert(results[1].JurorName == "Juror 2", "Second juror processed");
        
        Assert(jurors[0].VerdictLean >= 0 && jurors[0].VerdictLean <= 1, "Juror 1 VerdictLean updated");
        Assert(jurors[1].VerdictLean >= 0 && jurors[1].VerdictLean <= 1, "Juror 2 VerdictLean updated");
    }
}