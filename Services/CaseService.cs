using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        string json = JsonSerializer.Serialize(currentCase, new JsonSerializerOptions { 
            WriteIndented = true, 
            Converters = { new JsonStringEnumConverter() } 
        });
        File.WriteAllText(filePath, json);

        // Create assets folder next to the .jur so it can be kept with the case file.
        // IMPORTANT: the folder must be persisted in a way that will travel with the .jur,
        // i.e., the folder name is based on the .jur file name.
        string? directory = Path.GetDirectoryName(filePath);
        if (directory == null) return;

        string assetFolder = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(filePath) + "_Assets");

        if (!Directory.Exists(assetFolder)) Directory.CreateDirectory(assetFolder);

        // Save Agent-specific prompts and memories to the assets folder
        string agentsFolder = Path.Combine(assetFolder, "Agents");
        if (!Directory.Exists(agentsFolder)) Directory.CreateDirectory(agentsFolder);

        foreach (var agent in allAgents)
        {
            if (!agent.IsOccupied) continue;

            string agentDir = Path.Combine(agentsFolder, agent.Name.Replace(" ", "_"));
            if (!Directory.Exists(agentDir)) Directory.CreateDirectory(agentDir);

            File.WriteAllText(Path.Combine(agentDir, "system_prompt.txt"), agent.SystemPrompt);

            var memoryLines = agent.Memories
                .Select(m => $"{m.Timestamp:u} [{m.Strength:F2}] {m.Content}");
            File.WriteAllLines(Path.Combine(agentDir, "memories.txt"), memoryLines);
        }

        // Persist the asset folder location inside the .jur so it is discoverable when loading.
        // (We do not change the folder structure, we just record it.)
        currentCase.LastSaved = File.GetLastWriteTime(filePath);
    }

    public CaseFile? LoadCase(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (!File.Exists(filePath)) return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        string json = File.ReadAllText(filePath);
        var caseFile = JsonSerializer.Deserialize<CaseFile>(json, options);

        if (caseFile == null) return null;

        caseFile.LastSaved = File.GetLastWriteTime(filePath);

        // Re-attach persisted agent memories/prompts from the saved assets folder.
        // This keeps the case behavior consistent after moving/copying the .jur + its _Assets folder.
        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        string assetFolder = Path.Combine(directory, Path.GetFileNameWithoutExtension(filePath) + "_Assets");
        string agentsFolder = Path.Combine(assetFolder, "Agents");

        if (Directory.Exists(agentsFolder))
        {
            foreach (var agent in caseFile.Agents)
            {
                if (agent == null || !agent.IsOccupied) continue;

                string agentDir = Path.Combine(agentsFolder, agent.Name.Replace(" ", "_"));
                string systemPromptPath = Path.Combine(agentDir, "system_prompt.txt");
                string memoriesPath = Path.Combine(agentDir, "memories.txt");

                if (File.Exists(systemPromptPath))
                {
                    agent.SystemPrompt = File.ReadAllText(systemPromptPath);
                }

                if (File.Exists(memoriesPath))
                {
                    var lines = File.ReadAllLines(memoriesPath);
                    agent.Memories.Clear();

                    foreach (var line in lines)
                    {
                        // Format: {timestamp:u} [{strength:F2}] {content}
                        // We tolerate parse failures and keep content as-is.
                        try
                        {
                            int strengthStart = line.IndexOf("[");
                            int strengthEnd = line.IndexOf("]");
                            if (strengthStart >= 0 && strengthEnd > strengthStart)
                            {
                                string strengthStr = line.Substring(strengthStart + 1, strengthEnd - strengthStart - 1);
                                double strength = double.TryParse(strengthStr, out var s) ? s : 0.5;

                                // timestamp is up to the first space after the timestamp portion
                                string timestampStr = line.Substring(0, strengthStart).Trim();
                                DateTime ts = DateTime.TryParse(timestampStr, out var t) ? t : DateTime.MinValue;

                                // content begins after the closing '] ' (or just after ']')
                                int contentStart = strengthEnd + 1;
                                if (contentStart < line.Length && line[contentStart] == ' ') contentStart++;
                                string content = contentStart <= line.Length ? line.Substring(contentStart) : string.Empty;

                                agent.Memories.Add(new MemoryEntry { Timestamp = ts, Strength = strength, Content = content });
                            }
                            else
                            {
                                agent.Memories.Add(new MemoryEntry { Timestamp = DateTime.MinValue, Strength = 0.5, Content = line });
                            }
                        }
                        catch
                        {
                            agent.Memories.Add(new MemoryEntry { Timestamp = DateTime.MinValue, Strength = 0.5, Content = line });
                        }
                    }
                }
            }
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
