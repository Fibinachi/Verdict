using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.Services;

public interface IAgentDemographicsGeneratorService
{
    /// <summary>
    /// Generates a statistically-average-ish stakeholder agent for a configurable role.
    /// The role is resolved from a config file (Resources/AgentDemographicsByRole.json).
    /// </summary>
    Agent GenerateStakeholderAgent(string roleName, int? seed = null);
}

public sealed class AgentDemographicsGeneratorService : IAgentDemographicsGeneratorService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;

    public AgentDemographicsGeneratorService(string? configPath = null)
    {
        _configPath = configPath ??
                      Path.Combine(Directory.GetCurrentDirectory(), "Resources", "AgentDemographicsByRole.json");
    }

    public Agent GenerateStakeholderAgent(string roleName, int? seed = null)
    {
        if (string.IsNullOrWhiteSpace(roleName)) throw new ArgumentException("roleName is required", nameof(roleName));

        var cfg = LoadConfig();
        if (!cfg.TryGetValue(roleName, out var roleCfg))
            throw new FileNotFoundException($"No role demographics found for '{roleName}' in {_configPath}");

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        int age = SampleFromAgeRanges(roleCfg.AgeRanges, rng);
        string gender = SampleWeighted(roleCfg.GenderWeights, rng);
        string race = SampleWeighted(roleCfg.RaceWeights, rng);
        string education = SampleWeighted(roleCfg.EducationWeights, rng);

        // Minimal occupation assignment: keep it symbolic/stakeholder-like.
        // If you want finer occupation mapping later, extend config.
        string occupation = roleName switch
        {
            "InsuranceAdjuster" => "Insurance Adjuster",
            "SupervisingProsecutor" => "Supervising Prosecutor",
            "LitigationManager" => "Litigation Manager",
            _ => roleName
        };

        // Create a deterministic-but-random-ish profile string.
        // Names are intentionally simple for now.
        string name = GenerateSimpleName(gender, race, rng);

        var agent = new Agent
        {
            IsOccupied = true,
            Role = InferBaseRole(roleName),
            Name = name,
            Age = age,
            Gender = gender,
            Race = race,
            EducationLevel = education,
            IncomeLevel = InferIncomeFromEducation(education),
            MaritalStatus = age < 25 ? "Single" : "Married",
            ParentalStatus = age < 28 ? "No Children" : "Has Children",
            ReligiousAffiliation = "Non-religious",
            PoliticalAffiliation = "Independent",
            MediaConsumption = "Mainstream News (Cable)",
            Occupation = occupation,
            ZipCode = GenerateZipPlaceholder(rng),
            Bias = rng.NextDouble() * 0.4 - 0.2,
            Sentiment = 0.5,
            VerdictLean = 0.5
        };

        agent.Profile = $"A {age}-year-old {race} {gender} serving as {roleName}. Education: {education}. Occupation: {occupation}.";

        // System prompt is optional; default agent prompts elsewhere will still work.
        agent.SystemPrompt = $"You are a stakeholder player: {roleName}. Maintain role-appropriate, decision-minded behavior.";

        return agent;
    }

    private Dictionary<string, RoleDemographicsConfig> LoadConfig()
    {
        if (!File.Exists(_configPath))
            throw new FileNotFoundException($"Missing demographics config: {_configPath}");

        var json = File.ReadAllText(_configPath);
        var cfg = JsonSerializer.Deserialize<Dictionary<string, RoleDemographicsConfig>>(json, _jsonOptions);
        return cfg ?? new Dictionary<string, RoleDemographicsConfig>(StringComparer.OrdinalIgnoreCase);
    }

    private static AgentRole InferBaseRole(string roleName)
    {
        // Map symbolic stakeholder role names to explicit AgentRole enum values.
        return roleName switch
        {
            "InsuranceAdjuster" => AgentRole.InsuranceAdjuster,
            "SupervisingProsecutor" => AgentRole.SupervisingProsecutor,
            "LitigationManager" => AgentRole.LitigationManager,
            _ => AgentRole.Observer
        };
    }

    private static int SampleFromAgeRanges(List<AgeRange> ranges, Random rng)
    {
        if (ranges is null || ranges.Count == 0) return rng.Next(25, 60);

        double total = ranges.Sum(r => r.Weight);
        double roll = rng.NextDouble() * total;
        double acc = 0;
        foreach (var r in ranges)
        {
            acc += r.Weight;
            if (roll <= acc)
                return rng.Next(r.Min, r.Max + 1);
        }
        var last = ranges[^1];
        return rng.Next(last.Min, last.Max + 1);
    }

    private static string SampleWeighted(List<WeightedString> weights, Random rng)
    {
        if (weights is null || weights.Count == 0) return "Unknown";
        double total = weights.Sum(w => w.Weight);
        double roll = rng.NextDouble() * total;
        double acc = 0;
        foreach (var w in weights)
        {
            acc += w.Weight;
            if (roll <= acc) return w.Value;
        }
        return weights[^1].Value;
    }

    private static string InferIncomeFromEducation(string education)
    {
        var e = (education ?? string.Empty).ToLowerInvariant();
        if (e.Contains("doctor")) return "Upper Class";
        if (e.Contains("master")) return "Upper Middle Class";
        if (e.Contains("bachelor")) return "Upper Middle Class";
        if (e.Contains("associate")) return "Middle Class";
        if (e.Contains("some college")) return "Working Class";
        if (e.Contains("high school")) return "Lower Class";
        return "Middle Class";
    }

    private static string GenerateSimpleName(string gender, string race, Random rng)
    {
        // Keep it simple and fast; you can later reuse JuryDemographicsService name logic.
        string first = gender switch
        {
            "Female" => rng.Next(0, 2) == 0 ? "Sarah" : "Jennifer",
            "Male" => rng.Next(0, 2) == 0 ? "Michael" : "James",
            _ => "Alex"
        };

        string last = rng.Next(0, 3) switch
        {
            0 => "Smith",
            1 => "Johnson",
            _ => "Williams"
        };

        // Light race flavor (purely cosmetic)
        if (race.Contains("Black", StringComparison.OrdinalIgnoreCase) && gender == "Male") last = "Carter";
        if (race.Contains("Hispanic", StringComparison.OrdinalIgnoreCase) && gender == "Female") last = "Gonzalez";

        return $"{first} {last}";
    }

    private static string GenerateZipPlaceholder(Random rng) => $"{rng.Next(29000, 29999)}";

    private sealed class RoleDemographicsConfig
    {
        public List<AgeRange> AgeRanges { get; set; } = new();
        public List<WeightedString> GenderWeights { get; set; } = new();
        public List<WeightedString> RaceWeights { get; set; } = new();
        public List<WeightedString> EducationWeights { get; set; } = new();
    }

    private sealed class AgeRange
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public double Weight { get; set; }
    }

    private sealed class WeightedString
    {
        public string Value { get; set; } = string.Empty;
        public double Weight { get; set; }
    }
}

