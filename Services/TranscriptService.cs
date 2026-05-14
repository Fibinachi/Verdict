using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Service for loading and parsing court transcript files.
/// </summary>
public interface ITranscriptService
{
    /// <summary>
    /// Reads a transcript file and yields speaker/content pairs.
    /// </summary>
    /// <param name="filePath">Path to the transcript text file.</param>
    IEnumerable<(string Speaker, string Content)> LoadTranscript(string filePath);

    /// <summary>
    /// Extracts structured entities from a transcript using LLM analysis.
    /// </summary>
    /// <param name="transcript">The full transcript text.</param>
    /// <param name="model">The AI model configuration to use.</param>
    /// <returns>Extracted entities from the transcript.</returns>
    Task<ExtractedEntities> ExtractEntitiesAsync(string transcript, AIModelConfiguration model);
}

public class TranscriptService : ITranscriptService
{
    /// <summary>
    /// System prompt for entity extraction.
    /// </summary>
    private const string EntityExtractionPrompt = @"You are a legal case analyst. Analyze the following courtroom transcript and extract structured information.

Extract the following entities from the transcript:
1. PARTIES: All plaintiffs and defendants named in the case
2. ATTORNEYS: Plaintiff/prosecution attorney(s) and defense attorney(s)
3. CHARGES: Any criminal charges or civil claims mentioned
4. EVIDENCE: Any documents, photos, videos, or physical evidence mentioned
5. WITNESSES: Any witnesses mentioned or who testified
6. LEGAL ISSUES: Key legal issues or arguments raised
7. CAUSE OF ACTION: The main cause of action or claims being asserted
8. DAMAGES: Any damages sought or mentioned
9. CASE SUMMARY: A brief summary of the case

Respond in JSON format matching this schema:
{
    ""plaintiffs"": [""Plaintiff Name 1"", ""Plaintiff Name 2""],
    ""defendants"": [""Defendant Name 1"", ""Defendant Name 2""],
    ""plaintiffAttorney"": """",
    ""defenseAttorney"": """",
    ""charges"": [{ ""name"": "", ""description"": "", ""statute"": "", ""severity"": """" }],
    ""evidence"": [{ ""type"": "", ""description"": "", ""admittedBy"": "", ""strength"": 0.5 }],
    ""witnesses"": [{ ""name"": "", ""role"": "", ""testimonySummary"": "", ""testified"": true/false }],
    ""legalIssues"": [""issue1"", ""issue2""],
    ""causeOfAction"": """",
    ""damages"": """",
    ""caseSummary"": """"
}

Only include entities that are actually mentioned in the transcript. Use empty arrays for missing lists.";

    public IEnumerable<(string Speaker, string Content)> LoadTranscript(string filePath)
    {
        if (!File.Exists(filePath)) yield break;
        
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Expected format: "Speaker: Content"
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                yield return (parts[0].Trim(), parts[1].Trim());
            }
            else
            {
                // Fallback for lines without a speaker prefix
                yield return (string.Empty, line.Trim());
            }
        }
    }

    public async Task<ExtractedEntities> ExtractEntitiesAsync(string transcript, AIModelConfiguration model)
    {
        if (string.IsNullOrWhiteSpace(transcript) || model == null)
            return new ExtractedEntities();

        // Get the provider module for LLM calls
        var provider = ProviderDiscoveryService.GetProviderByName(model.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException($"Provider '{model.Provider}' not found. Ensure it is configured.");
        }

        // Call the LLM to extract entities
        var llmResponse = await provider.GenerateResponseAsync(
            model,
            EntityExtractionPrompt,
            transcript
        );

        // Parse the JSON response
        return ParseExtractionResponse(llmResponse);
    }

    /// <summary>
    /// Parses the LLM JSON response into ExtractedEntities.
    /// </summary>
    private ExtractedEntities ParseExtractionResponse(string jsonResponse)
    {
        var entities = new ExtractedEntities();

        try
        {
            // Try to extract JSON from the response (in case there's extra text)
            var jsonMatch = Regex.Match(jsonResponse, @"\{[\s\S]*\}", RegexOptions.IgnoreCase);
            if (!jsonMatch.Success)
            {
                return entities;
            }

            using var doc = JsonDocument.Parse(jsonMatch.Value);
            var root = doc.RootElement;

            // Parse charges
            if (root.TryGetProperty("charges", out var charges))
            {
                foreach (var charge in charges.EnumerateArray())
                {
                    entities.Charges.Add(new ExtractedCharge
                    {
                        Name = charge.GetProperty("name").GetString() ?? string.Empty,
                        Description = charge.GetProperty("description").GetString() ?? string.Empty,
                        Statute = charge.GetProperty("statute").GetString() ?? string.Empty,
                        Severity = charge.GetProperty("severity").GetString() ?? string.Empty
                    });
                }
            }

            // Parse evidence
            if (root.TryGetProperty("evidence", out var evidence))
            {
                foreach (var ev in evidence.EnumerateArray())
                {
                    entities.Evidence.Add(new ExtractedEvidence
                    {
                        Type = ev.GetProperty("type").GetString() ?? string.Empty,
                        Description = ev.GetProperty("description").GetString() ?? string.Empty,
                        AdmittedBy = ev.GetProperty("admittedBy").GetString() ?? string.Empty,
                        Strength = ev.TryGetProperty("strength", out var strength) ? strength.GetDouble() : 0.5
                    });
                }
            }

            // Parse witnesses
            if (root.TryGetProperty("witnesses", out var witnesses))
            {
                foreach (var witness in witnesses.EnumerateArray())
                {
                    entities.Witnesses.Add(new ExtractedWitness
                    {
                        Name = witness.GetProperty("name").GetString() ?? string.Empty,
                        Role = witness.GetProperty("role").GetString() ?? string.Empty,
                        TestimonySummary = witness.GetProperty("testimonySummary").GetString() ?? string.Empty,
                        Testified = witness.TryGetProperty("testified", out var testified) && testified.GetBoolean()
                    });
                }
            }

            // Parse legal issues
            if (root.TryGetProperty("legalIssues", out var legalIssues))
            {
                foreach (var issue in legalIssues.EnumerateArray())
                {
                    entities.LegalIssues.Add(issue.GetString() ?? string.Empty);
                }
            }

            // Parse plaintiffs
            if (root.TryGetProperty("plaintiffs", out var plaintiffs))
            {
                foreach (var p in plaintiffs.EnumerateArray())
                    entities.Plaintiffs.Add(p.GetString() ?? string.Empty);
            }

            // Parse defendants
            if (root.TryGetProperty("defendants", out var defendants))
            {
                foreach (var d in defendants.EnumerateArray())
                    entities.Defendants.Add(d.GetString() ?? string.Empty);
            }

            // Parse simple fields
            entities.PlaintiffAttorney = root.TryGetProperty("plaintiffAttorney", out var pa) ? pa.GetString() ?? string.Empty : string.Empty;
            entities.DefenseAttorney = root.TryGetProperty("defenseAttorney", out var da) ? da.GetString() ?? string.Empty : string.Empty;
            entities.CauseOfAction = root.TryGetProperty("causeOfAction", out var coa) ? coa.GetString() ?? string.Empty : string.Empty;
            entities.Damages = root.TryGetProperty("damages", out var damages) ? damages.GetString() ?? string.Empty : string.Empty;
            entities.CaseSummary = root.TryGetProperty("caseSummary", out var summary) ? summary.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            // If parsing fails, return empty entities
        }

        return entities;
    }
}
