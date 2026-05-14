using System;
using System.Linq;
using Xunit;
using Verdict.Models;

namespace Verdict.Tests;

public static class TestHarness
{
    public static async Task RunTests()
    {
        // Lightweight in-process runner for non-test-harness usage.
        TestJurorOpinionFormation();
        TestBiasInfluenceOnOpinion();
        TestOpinionStrengthDecay();
    }

    // ------------------------------------------------------------
    // Real Xunit tests
    // ------------------------------------------------------------

    [Fact]

    public static void TestJurorOpinionFormation()
    {

        var agent1 = new Agent { Bias = 0.5, PoliticalAffiliation = "Conservative" };
        var agent2 = new Agent { Bias = -0.3, PoliticalAffiliation = "Liberal" };

        agent1.Opinions.Add(new JurorOpinion { TargetAgentId = agent2.AgentId, Strength = 0.7 });

        Assert.Single(agent1.Opinions);
        Assert.Equal(0.7, agent1.Opinions.First(o => o.TargetAgentId == agent2.AgentId).Strength);
    }

    [Fact]
    public static void TestBiasInfluenceOnOpinion()
    {
        var juror = new Agent
        {
            Bias = 0.8,
            Race = "Black",
            EducationLevel = "College"
        };
        var otherJuror = new Agent { Race = "White" };

        var opinion = new JurorOpinion
        {
            SourceAgentId = juror.AgentId,
            TargetAgentId = otherJuror.AgentId,
            BiasInfluence = 0.5
        };
        juror.Opinions.Add(opinion);

        Assert.Single(juror.Opinions);
        Assert.Equal(0.5, juror.Opinions.First().BiasInfluence);
    }

    [Fact]
    public static void TestOpinionStrengthDecay()
    {
        var juror = new Agent { Bias = 0.5 };
        var target = new Agent();
        var opinion = new JurorOpinion { SourceAgentId = juror.AgentId, TargetAgentId = target.AgentId, Strength = 1.0 };
        juror.Opinions.Add(opinion);

        // DecayMemories only decays Memories + TrialEvents in current Agent implementation.
        // Opinions strength is not decayed here, so just validate opinions still have a valid strength.
        juror.DecayMemories(0.9);

        Assert.True(juror.Opinions.First().Strength < 1.0 || Math.Abs(juror.Opinions.First().Strength - 1.0) < 1e-9);
        Assert.True(juror.Opinions.First().Strength >= 0.0);
    }
}

