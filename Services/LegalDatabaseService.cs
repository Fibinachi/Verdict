using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Service for accessing and querying the legal database.
/// </summary>
public class LegalDatabaseService
{
    private readonly string _databasePath;
    private List<LegalCitation> _citations = new();
    private bool _isLoaded = false;

    public LegalDatabaseService() : this("Resources/LegalDatabase.json")
    {
    }

    public LegalDatabaseService(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// <summary>
    /// Loads the legal database from JSON file.
    /// </summary>
    public void LoadDatabase()
    {
        if (_isLoaded) return;

        try
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _databasePath);
            if (!File.Exists(fullPath))
            {
                // Try relative to project directory
                fullPath = Path.Combine(Directory.GetCurrentDirectory(), _databasePath);
            }

            if (File.Exists(fullPath))
            {
                var json = File.ReadAllText(fullPath);
                var database = JsonSerializer.Deserialize<LegalDatabaseV2>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                if (database?.Citations != null)
                {
                    // Convert to LegalCitation format (use existing class)
                    _citations = database.Citations.Select(c => new LegalCitation
                    {
                        Code = c.Code,
                        Section = c.Section,
                        Description = c.Description,
                        Jurisdiction = c.Jurisdiction,
                        FavorsPlaintiff = c.FavorsPlaintiff
                    }).ToList();
                    _isLoaded = true;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading legal database: {ex.Message}");
        }

        // Load embedded database as fallback (sample citations)
        LoadEmbeddedCitations();
        _isLoaded = true;
    }

    /// <summary>
    /// Loads embedded sample citations as fallback.
    /// </summary>
    private void LoadEmbeddedCitations()
    {
        _citations = new List<LegalCitation>
        {
            new LegalCitation
            {
                Code = "IPC Section 302",
                Section = "302",
                Description = "Murder - Punishment with death or life imprisonment",
                Jurisdiction = "India",
                FavorsPlaintiff = true
            },
            new LegalCitation
            {
                Code = "IPC Section 304",
                Section = "304",
                Description = "Culpable Homicide Not Amounting to Murder",
                Jurisdiction = "India",
                FavorsPlaintiff = true
            },
            new LegalCitation
            {
                Code = "IPC Section 420",
                Section = "420",
                Description = "Cheating and Dishonestly Inducing Delivery of Property - Up to 7 years imprisonment",
                Jurisdiction = "India",
                FavorsPlaintiff = true
            },
            new LegalCitation
            {
                Code = "18 U.S.C. 1341",
                Section = "1341",
                Description = "Mail Fraud - Up to 20 years imprisonment",
                Jurisdiction = "United States Federal",
                FavorsPlaintiff = true
            },
            new LegalCitation
            {
                Code = "42 U.S.C. 1983",
                Section = "1983",
                Description = "Civil Action for Deprivation of Rights - Civil rights violations",
                Jurisdiction = "United States Federal",
                FavorsPlaintiff = true
            }
        };
    }

    /// <summary>
    /// Searches citations by keyword.
    /// </summary>
    public List<LegalCitation> SearchByKeyword(string keyword)
    {
        EnsureLoaded();
        
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<LegalCitation>();

        var lower = keyword.ToLower();
        return _citations
            .Where(c => c.Description != null && c.Description.ToLower().Contains(lower))
            .ToList();
    }

    /// <summary>
    /// Searches citations by category.
    /// </summary>
    public List<LegalCitation> SearchByCategory(string category)
    {
        EnsureLoaded();
        
        if (string.IsNullOrWhiteSpace(category))
            return _citations;

        return _citations
            .Where(c => c.Description != null && c.Description.Contains(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Searches citations by jurisdiction.
    /// </summary>
    public List<LegalCitation> SearchByJurisdiction(string jurisdiction)
    {
        EnsureLoaded();
        
        if (string.IsNullOrWhiteSpace(jurisdiction))
            return _citations;

        return _citations
            .Where(c => c.Jurisdiction != null && c.Jurisdiction.Equals(jurisdiction, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets a citation by Section code.
    /// </summary>
    public LegalCitation? GetBySection(string section)
    {
        EnsureLoaded();
        return _citations.FirstOrDefault(c => c.Section == section);
    }

    /// <summary>
    /// Gets all available citations.
    /// </summary>
    public List<LegalCitation> GetAll()
    {
        EnsureLoaded();
        return _citations;
    }

    /// <summary>
    /// Gets relevant legal citations for a given case description.
    /// </summary>
    public List<LegalCitation> GetRelevantCitations(string caseDescription)
    {
        EnsureLoaded();
        
        if (string.IsNullOrWhiteSpace(caseDescription))
            return new List<LegalCitation>();

        var keywords = caseDescription.ToLower().Split(new[] { ' ', ',', '.', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var results = new List<LegalCitation>();

        foreach (var citation in _citations)
        {
            int matchCount = 0;
            var descLower = (citation.Description ?? "").ToLower();
            foreach (var keyword in keywords.Take(20))
            {
                if (keyword.Length < 3) continue;
                if (descLower.Contains(keyword))
                    matchCount++;
            }
            
            if (matchCount > 0)
            {
                results.Add(new LegalCitation
                {
                    Code = citation.Code,
                    Section = citation.Section,
                    Description = $"{matchCount} matches: {citation.Description}",
                    Jurisdiction = citation.Jurisdiction,
                    FavorsPlaintiff = citation.FavorsPlaintiff
                });
            }
        }

        return results.Take(5).ToList();
    }

    /// <summary>
    /// Generates a prompt inclusion for legal citations.
    /// </summary>
    public string GenerateCitationContext(string caseDescription, int maxCitations = 3)
    {
        var citations = GetRelevantCitations(caseDescription);
        if (!citations.Any())
            return string.Empty;

        var context = "\n\n APPLICABLE LEGAL PRECEDENTS:\n";
        foreach (var citation in citations.Take(maxCitations))
        {
            context += $"\n- {citation.Code} {citation.Section}: {citation.Description}";
        }
        
        return context;
    }

    private void EnsureLoaded()
    {
        if (!_isLoaded)
            LoadDatabase();
    }
}

/// <summary>
/// Root object for JSON deserialization (V2 with extended fields).
/// </summary>
internal class LegalDatabaseV2
{
    public LegalDatabaseMetadataV2? Metadata { get; set; }
    public List<LegalCitationV2>? Citations { get; set; }
}

internal class LegalDatabaseMetadataV2
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LastUpdated { get; set; } = string.Empty;
}

/// <summary>
/// Extended citation for JSON deserialization.
/// </summary>
internal class LegalCitationV2
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public List<string> RelatedCitations { get; set; } = new();
    public bool FavorsPlaintiff { get; set; }
}
