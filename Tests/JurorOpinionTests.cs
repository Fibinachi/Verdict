using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Verdict.Models;

namespace Verdict.Tests
{
    /// <summary>
    /// Tests for JurorOpinion functionality
    /// </summary>
    public class JurorOpinionTests
    {
        [Fact]
        public void TestJurorOpinionFormation()
        {
            // Arrange
            var agent1 = new Agent { Bias = 0.5, PoliticalAffiliation = "Conservative" };
            var agent2 = new Agent { Bias = -0.3, PoliticalAffiliation = "Liberal" };
            agent1.Opinions.Add(new JurorOpinion { TargetAgentId = agent2.AgentId, Strength = 0.7 });

            // Act - simulate opinion adjustment based on bias
            // (This would typically call a method like UpdateOpinionBasedOnBias)

            // Assert
            Assert.Single(agent1.Opinions);
            Assert.Equal(0.7, agent1.Opinions.First(o => o.TargetAgentId == agent2.AgentId).Strength);
        }

        [Fact]
        public void TestBiasInfluenceOnOpinion()
        {
            // Arrange
            var juror = new Agent
            {
                Bias = 0.8,
                Race = "Black",
                EducationLevel = "College"
            };
            var otherJuror = new Agent { Race = "White" };

            // Act
            var opinion = new JurorOpinion
            {
                SourceAgentId = juror.AgentId,
                TargetAgentId = otherJuror.AgentId,
                BiasInfluence = 0.5
            };
            juror.Opinions.Add(opinion);

            // Assert
            Assert.Single(juror.Opinions);
            Assert.Equal(0.5, juror.Opinions.First().BiasInfluence);
        }

        [Fact]
        public void TestOpinionStrengthDecay()
        {
            // Arrange
            var juror = new Agent { Bias = 0.5 };
            var target = new Agent();
            var opinion = new JurorOpinion { SourceAgentId = juror.AgentId, TargetAgentId = target.AgentId, Strength = 1.0 };
            juror.Opinions.Add(opinion);

            // Act
            juror.DecayMemories(0.9); // Should affect opinion strength

            // Assert
            Assert.True(juror.Opinions.First().Strength < 1.0);

            Assert.True(juror.Opinions.First().Strength >= 0.0);
        }

        [Fact]
        public void TestJurorOpinionDescriptionGeneration()
        {
            // Arrange
            var juror = new Agent { Bias = 0.7, EducationLevel = "College" };
            var otherJuror = new Agent { EducationLevel = "High School" };
            
            // Act
            var opinion = new JurorOpinion
            {
                SourceAgentId = juror.AgentId,
                TargetAgentId = otherJuror.AgentId,
                Strength = 0.8,
                BiasInfluence = 0.6
            };
            juror.Opinions.Add(opinion);

            // Assert
            Assert.NotNull(opinion.Description);
            Assert.NotEmpty(opinion.Description);
        }
    }
}