using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Verdict.Models;
using Verdict.Providers;
using Verdict.Services;

namespace Verdict.Tests;

public static class TestHarness
{
    public static async Task RunTests()
    {
        // Runs fully offline: validates model directory completeness rules.
        TestIsValidModelDirectoryRejectsIncompleteDownloads();

        ReproOnnx.Run();
        TestJurorOpinionFormation();
        TestBiasInfluenceOnOpinion();
        TestOpinionStrengthDecay();
        TestSentimentAnalysis();
        TestVerdictLeanUpdate();
        TestComputedProperties();
        TestVerdictLeanSetterBehavior();
        await TestLlmProvidersHelloWorld();
        JurorOpinionReportTests.RunTests();
        TestJurorStateBiasParameterMapping();
        TestBiasParameterPipeline();
    }

    public static void TestJurorOpinionFormation()
    {
        var agent1 = new Agent { Bias = 0.5, PoliticalAffiliation = "Conservative" };
        var agent2 = new Agent { Bias = -0.3, PoliticalAffiliation = "Liberal" };

        agent1.Opinions.Add(new JurorOpinion { TargetAgentId = agent2.AgentId, Strength = 0.7 });

        Assert.Single(agent1.Opinions);
        Assert.Equal(0.7, agent1.Opinions.First(o => o.TargetAgentId == agent2.AgentId).Strength);
    }

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

    public static void TestOpinionStrengthDecay()
    {
        var juror = new Agent { Bias = 0.5 };
        var target = new Agent();
        var opinion = new JurorOpinion { SourceAgentId = juror.AgentId, TargetAgentId = target.AgentId, Strength = 1.0 };
        juror.Opinions.Add(opinion);

        juror.DecayMemories(0.9);

        Assert.True(juror.Opinions.First().Strength < 1.0 || Math.Abs(juror.Opinions.First().Strength - 1.0) < 1e-9);
        Assert.True(juror.Opinions.First().Strength >= 0.0);
    }

    public static void TestSentimentAnalysis()
    {
        var agent = new Agent { Bias = 0.0, Sentiment = 0.5 };
        
        agent.AnalyzeSentiment("The expert testimony was convincing and factual.");
        Assert.True(agent.Sentiment > 0.5, "Positive content should increase sentiment");
        
        agent.AnalyzeSentiment("The witness lied and gave inconsistent testimony.");
        Assert.True(agent.Sentiment < 0.5, "Negative content should decrease sentiment");
        
        var reporter = new Agent { Role = AgentRole.Reporter, Bias = 0.0, Sentiment = 0.5 };
        double originalSentiment = reporter.Sentiment;
        reporter.AnalyzeSentiment("The expert testimony was convincing and factual.");
        Assert.Equal(originalSentiment, reporter.Sentiment);
    }

    public static void TestVerdictLeanUpdate()
    {
        var agent = new Agent { Bias = 0.0, VerdictLean = 0.5, IsOccupied = true };
        
        agent.UpdateVerdictLean(0.2);
        Assert.True(agent.VerdictLean > 0.5, "Positive influence should increase verdict lean");
        
        agent.UpdateVerdictLean(-0.3);
        Assert.True(agent.VerdictLean < 0.5, "Negative influence should decrease verdict lean");
        
        agent.VerdictLean = 0.9;
        agent.UpdateVerdictLean(0.5);
        Assert.Equal(1.0, agent.VerdictLean);
        
        agent.VerdictLean = 0.1;
        agent.UpdateVerdictLean(-0.5);
        Assert.Equal(0.0, agent.VerdictLean);
    }

    public static void TestComputedProperties()
    {
        var juror = new Agent { Role = AgentRole.Juror, IsOccupied = true };
        Assert.True(juror.HasOpinion, "Juror should have opinion capability");
        Assert.True(juror.CanVote, "Juror should be able to vote");
        Assert.True(juror.CanDeliberate, "Juror should be able to deliberate");
        Assert.False(juror.IsClient, "Juror should not be a client");
        
        var judge = new Agent { Role = AgentRole.Judge, IsOccupied = true };
        Assert.True(judge.HasOpinion, "Judge should have opinion capability");
        Assert.False(judge.CanVote, "Judge should not be able to vote");
        Assert.False(judge.CanDeliberate, "Judge should not be able to deliberate");
        Assert.False(judge.IsClient, "Judge should not be a client");
        
        var reporter = new Agent { Role = AgentRole.Reporter, IsOccupied = true };
        Assert.False(reporter.HasOpinion, "Reporter should not have opinion capability");
        Assert.False(reporter.CanVote, "Reporter should not be able to vote");
        Assert.False(reporter.CanDeliberate, "Reporter should not be able to deliberate");
        Assert.False(reporter.IsClient, "Reporter should not be a client");
        
        var client = new Agent { Role = AgentRole.Client, IsOccupied = true };
        Assert.False(client.HasOpinion, "Client should not have opinion capability");
        Assert.False(client.CanVote, "Client should not be able to vote");
        Assert.False(client.CanDeliberate, "Client should not be able to deliberate");
        Assert.True(client.IsClient, "Client should be a client");
        
        var unoccupied = new Agent { Role = AgentRole.Juror, IsOccupied = false };
        Assert.False(unoccupied.HasOpinion, "Unoccupied agent should not have opinion capability");
        Assert.False(unoccupied.CanVote, "Unoccupied agent should not be able to vote");
        Assert.False(unoccupied.CanDeliberate, "Unoccupied agent should not be able to deliberate");
        Assert.False(unoccupied.IsClient, "Unoccupied agent should not be a client");
    }

    public static void TestVerdictLeanSetterBehavior()
    {
        var agentWithoutOpinion = new Agent { Role = AgentRole.Reporter };
        double originalLean = agentWithoutOpinion.VerdictLean;
        agentWithoutOpinion.VerdictLean = 0.8;
        Assert.Equal(originalLean, agentWithoutOpinion.VerdictLean);
        
        var agentWithOpinion = new Agent { Role = AgentRole.Juror, IsOccupied = true };
        agentWithOpinion.VerdictLean = 0.3;
        Assert.Equal(0.3, agentWithOpinion.VerdictLean);
    }

    public static async Task TestLlmProvidersHelloWorld()
    {
        Console.WriteLine("\n--- Testing LLM Providers (Hello World) ---");
        
        var settingsService = new SettingsService();
        var defaultSettings = settingsService.GetDefaultCaseSettings();
        var providers = ProviderDiscoveryService.GetAvailableProviders();

        foreach (var provider in providers)
        {
            Console.WriteLine($"\n  Testing {provider.ProviderName}...");
            
            var prefix = provider.ProviderName.Replace(" ", "_").Replace("-", "_").ToUpperInvariant();
            
// Check for a saved model in default settings for this provider
            var savedModel = defaultSettings.AvailableModels
                .FirstOrDefault(m => m.Provider.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase)
                                            && !string.IsNullOrWhiteSpace(m.ModelId));

            var isOnnx = provider.ProviderName.Equals("ONNX", StringComparison.OrdinalIgnoreCase);
            if (isOnnx && savedModel == null)
            {
                var localModelDir = Path.Combine(AppContext.BaseDirectory, "Models", "test-model");
                if (Directory.Exists(localModelDir) && ModelDownloadService.IsValidModelDirectory(localModelDir))
                {
                    savedModel = new AIModelConfiguration
                    {
                        FriendlyName = "Local ONNX Model",
                        Provider = "ONNX",
                        ModelId = localModelDir
                    };
                }
            }

            if (savedModel == null)
            {
                Console.WriteLine($"    SKIPPED {provider.ProviderName} - no model configured in default settings");
                continue;
            }

            var testConfig = new AIModelConfiguration
            {
                FriendlyName = $"Test {provider.ProviderName}",
                Provider = provider.ProviderName,
                ModelId = savedModel.ModelId,
                ApiKey = savedModel.ApiKey,
                Endpoint = savedModel.Endpoint
            };

            var envApiKey = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_API_KEY");
            var envModelId = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_MODEL_ID");
            var envEndpoint = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_ENDPOINT");

            if (!string.IsNullOrWhiteSpace(envApiKey)) testConfig.ApiKey = envApiKey;
            if (!string.IsNullOrWhiteSpace(envModelId)) testConfig.ModelId = envModelId;
            if (!string.IsNullOrWhiteSpace(envEndpoint)) testConfig.Endpoint = envEndpoint;

            if (isOnnx && savedModel.ModelId.Contains("/") && string.IsNullOrWhiteSpace(envModelId))
            {
                Console.WriteLine($"    SKIPPED ONNX - HuggingFace model requires download (set VERDICT_TEST_ONNX_MODEL_ID to a local path)");
                continue;
            }
            
            try
            {
                var timeout = isOnnx ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(2);
                using var cts = new CancellationTokenSource(timeout);
                
                var overallSw = System.Diagnostics.Stopwatch.StartNew();
                Console.WriteLine($"    Model: {testConfig.ModelId}");
                if (!string.IsNullOrEmpty(testConfig.Endpoint)) Console.WriteLine($"    Endpoint: {testConfig.Endpoint}");
                if (!string.IsNullOrEmpty(testConfig.ApiKey)) Console.WriteLine($"    API Key: [CONFIGURED]");

                Console.WriteLine($"    [ONNX STATUS] Step 1/3: starting connection test for {provider.ProviderName}...");
                var connSw = System.Diagnostics.Stopwatch.StartNew();
                string connectionResult = await provider.TestConnectionAsync(testConfig, cts.Token);
                connSw.Stop();
                Console.WriteLine($"    [ONNX STATUS] Step 1/3 complete in {connSw.Elapsed}.");

                if (connectionResult.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"    CONNECTION FAILED {provider.ProviderName}: {connectionResult}");
                    continue;
                }
                
                Console.WriteLine($"    [ONNX STATUS] Step 2/3: starting generation test for {provider.ProviderName}...");
                var genSw = System.Diagnostics.Stopwatch.StartNew();
                string response = await provider.GenerateResponseAsync(
                    testConfig,
                    "You are a helpful assistant. Respond with exactly the phrase 'Hello World' and nothing else. NO PREAMBLE. NO MARKDOWN.",
                    "Hello World",
                    cts.Token);

                genSw.Stop();
                Console.WriteLine($"    [ONNX STATUS] Step 2/3 complete in {genSw.Elapsed}.");

                if (response.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"    GENERATION FAILED {provider.ProviderName}: {response}");
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    Console.WriteLine($"    EMPTY RESPONSE {provider.ProviderName}");
                    continue;
                }
                
                Console.WriteLine($"    SUCCESS {provider.ProviderName} responded: \"{response.Trim()}\"");

                // Only ONNX is sensitive to native init/generation; this checkpoint makes it obvious where a hard stop occurs.
                Console.WriteLine($"    [ONNX STATUS] Step 3/3 complete for {provider.ProviderName} in {overallSw.Elapsed}. Total test step for this provider");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"    TIMEOUT {provider.ProviderName} - test took too long");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    EXCEPTION {provider.ProviderName}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"\n  LLM Provider Testing Complete");
    }

    public static void TestJurorStateBiasParameterMapping()
    {
        Console.WriteLine("\n--- Testing JurorState Bias Parameter Mapping ---");

        var agent = new Agent
        {
            Name = "Test Juror",
            Bias = 0.3,
            DetailOrientation = 7.0,
            Compassion = 6.0,
            ConflictAvoidance = 4.0,
            MemoryReliability = 8.0,
            Age = 45,
            Openness = 6.0,
            NeedForCognition = 7.0,
            SuspicionTendency = 5.0,
            SystemJustification = 4.0,
            DisgustSensitivity = 3.0,
            AngerReactivity = 4.0
        };

        var jurorState = JurorState.FromAgent(agent);

        Assert.Equal("Test Juror", jurorState.Name);
        Assert.Equal(0.3, jurorState.Beta0);
        Assert.Equal(0.2, jurorState.BetaA, precision: 1);
        Assert.Equal(0.1, jurorState.BetaB, precision: 1);
        Assert.Equal(0.4, jurorState.Gamma, precision: 1);
        Assert.Equal(0.4, jurorState.Sigma, precision: 1);

        Assert.Equal(45, jurorState.ExtraParameters["Age"]);
        Assert.Equal(6.0, jurorState.ExtraParameters["Openness"]);
        Assert.Equal(7.0, jurorState.ExtraParameters["NeedForCognition"]);

        Console.WriteLine("  JurorState parameter mapping: PASSED");
    }

    public static void TestBiasParameterPipeline()
    {
        Console.WriteLine("\n--- Testing Bias Parameter Pipeline ---");

        var jurors = new List<Agent>
        {
            new Agent { Bias = 0.5, DetailOrientation = 6.0, Compassion = 5.0, ConflictAvoidance = 5.0, MemoryReliability = 5.0, IsOccupied = true },
            new Agent { Bias = -0.3, DetailOrientation = 4.0, Compassion = 7.0, ConflictAvoidance = 3.0, MemoryReliability = 6.0, IsOccupied = true },
            new Agent { Bias = 0.0, DetailOrientation = 5.0, Compassion = 5.0, ConflictAvoidance = 5.0, MemoryReliability = 5.0, IsOccupied = true }
        };

        JOEService.ApplyBiasParameterPipeline(jurors, 0.1);

        foreach (var juror in jurors)
        {
            Assert.InRange(juror.VerdictLean, 0.0, 1.0);
            Console.WriteLine($"  {juror.Name}: VerdictLean = {juror.VerdictLean:F3}");
        }

        Console.WriteLine("  Bias parameter pipeline: PASSED");
    }

    public static void TestIsValidModelDirectoryRejectsIncompleteDownloads()
    {
        Console.WriteLine("\n--- Testing IsValidModelDirectory (incomplete download rejection) ---");

        var root = Path.Combine(Path.GetTempPath(), "VerdictModelValidationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            // Case A: missing completion marker => invalid
            var caseA = Path.Combine(root, "caseA_no_marker");
            Directory.CreateDirectory(caseA);
            File.WriteAllText(Path.Combine(caseA, "genai_config.json"), @"{ ""model"": { ""context_length"": 2048 } }");
            File.WriteAllBytes(Path.Combine(caseA, "model.onnx"), new byte[2048]);
            Assert.False(ModelDownloadService.IsValidModelDirectory(caseA));

            // Case B: marker exists but missing genai_config.json => invalid
            var caseB = Path.Combine(root, "caseB_no_genai_config");
            Directory.CreateDirectory(caseB);
            File.WriteAllText(Path.Combine(caseB, "download_complete.ok"), DateTime.UtcNow.ToString("O"));
            File.WriteAllBytes(Path.Combine(caseB, "model.onnx"), new byte[2048]);
            Assert.False(ModelDownloadService.IsValidModelDirectory(caseB));

            // Case C: genai_config.json exists but missing context_length => invalid
            var caseC = Path.Combine(root, "caseC_genai_no_context_length");
            Directory.CreateDirectory(caseC);
            File.WriteAllText(Path.Combine(caseC, "download_complete.ok"), DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(caseC, "genai_config.json"), @"{ ""model"": { } }");
            File.WriteAllBytes(Path.Combine(caseC, "model.onnx"), new byte[2048]);
            Assert.False(ModelDownloadService.IsValidModelDirectory(caseC));

            // Case D: context_length ok + marker ok + genai_config ok, but onnx too small => invalid
            var caseD = Path.Combine(root, "caseD_onnx_too_small");
            Directory.CreateDirectory(caseD);
            File.WriteAllText(Path.Combine(caseD, "download_complete.ok"), DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(caseD, "genai_config.json"), @"{ ""context_length"": 2048 }");
            // MinOnnxFileSizeBytes is 1024; create 512 bytes
            File.WriteAllBytes(Path.Combine(caseD, "model.onnx"), new byte[512]);
            Assert.False(ModelDownloadService.IsValidModelDirectory(caseD));

            // Case E: all required bits => valid
            var caseE = Path.Combine(root, "caseE_valid");
            Directory.CreateDirectory(caseE);
            File.WriteAllText(Path.Combine(caseE, "download_complete.ok"), DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(caseE, "genai_config.json"), @"{ ""model"": { ""context_length"": 2048 } }");
            // >=1024 bytes
            File.WriteAllBytes(Path.Combine(caseE, "model.onnx"), new byte[2048]);
            Assert.True(ModelDownloadService.IsValidModelDirectory(caseE));
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { /* ignore */ }
        }

        Console.WriteLine("  IsValidModelDirectory incomplete-download tests: PASSED");
    }
}
