using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Service for saving and loading courtroom simulation case files.
/// </summary>
public interface ICaseService
{
    /// <summary>
    /// Saves the current case state and agent data to a file.
    /// </summary>
    void SaveCase(string filePath, CaseFile currentCase, IEnumerable<Agent> allAgents);

    /// <summary>
    /// Loads a case state from a file.
    /// </summary>
    CaseFile? LoadCase(string filePath);

    /// <summary>
    /// Generates a professional PDF case report for the given case.
    /// </summary>
    void GenerateReport(string filePath, CaseFile currentCase, IEnumerable<Agent> allAgents);
}

public class CaseService : ICaseService
{
    public void SaveCase(string filePath, CaseFile currentCase, IEnumerable<Agent> allAgents)
    {
        ArgumentNullException.ThrowIfNull(currentCase);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!filePath.EndsWith(".jur")) filePath += ".jur";

        currentCase.Agents.Clear();
        currentCase.Agents.AddRange(allAgents);

        string json = JsonSerializer.Serialize(currentCase, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        // Create assets folder
        string? directory = Path.GetDirectoryName(filePath);
        if (directory == null) return;

        string assetFolder = Path.Combine(directory, Path.GetFileNameWithoutExtension(filePath) + "_Assets");
        if (!Directory.Exists(assetFolder)) Directory.CreateDirectory(assetFolder);

        // Save Agent-specific prompts and memories to the assets folder
        string agentsFolder = Path.Combine(assetFolder, "Agents");
        if (!Directory.Exists(agentsFolder)) Directory.CreateDirectory(agentsFolder);

        foreach (var agent in allAgents)
        {
            if (agent.IsOccupied)
            {
                string agentDir = Path.Combine(agentsFolder, agent.Name.Replace(" ", "_"));
                if (!Directory.Exists(agentDir)) Directory.CreateDirectory(agentDir);

                File.WriteAllText(Path.Combine(agentDir, "system_prompt.txt"), agent.SystemPrompt);
                
                var memoryLines = agent.Memories.Select(m => $"{m.Timestamp:u} [{m.Strength:F2}] {m.Content}");
                File.WriteAllLines(Path.Combine(agentDir, "memories.txt"), memoryLines);
            }
        }
    }

    public CaseFile? LoadCase(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        string json = File.ReadAllText(filePath);
        var caseFile = JsonSerializer.Deserialize<CaseFile>(json);
        
        if (caseFile != null)
        {
            caseFile.LastSaved = File.GetLastWriteTime(filePath);
        }

        return caseFile;
    }

    /// <summary>
    /// Generates a professional PDF case report.
    /// </summary>
    public void GenerateReport(string filePath, CaseFile currentCase, IEnumerable<Agent> allAgents)
    {
        ArgumentNullException.ThrowIfNull(currentCase);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(allAgents);

        var reportService = new ReportGenerationService();
        reportService.GenerateReport(filePath, currentCase, allAgents);
    }
}
