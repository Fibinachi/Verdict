using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Verdict.Models;

namespace Verdict.Services;

public interface ISettingsService
{
    CaseFile GetDefaultCaseSettings();
    void SaveDefaultCaseSettings(CaseFile caseFile);
    List<string> GetRecentFiles();
    void AddRecentFile(string filePath);
    void RemoveRecentFile(string filePath);
    void ClearRecentFiles();
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verdict",
        "default_settings.json");

    private static readonly string RecentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verdict",
        "recent_files.json");

    private const int MaxRecentFiles = 10;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _jsonWriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CaseFile GetDefaultCaseSettings()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                var defaults = JsonSerializer.Deserialize<CaseFile>(json, _jsonOptions);
                if (defaults != null) return defaults;
            }
            catch
            {
                // Fallback to hardcoded defaults on error
            }
        }

        var fallback = new CaseFile 
        { 
            CaseName = "New Case",
            Mode = CaseMode.Civil,
            Jurisdiction = JurisdictionType.State,
            JurorCount = 12
        };
        fallback.AddDefaultModels();
        return fallback;
    }

    public void SaveDefaultCaseSettings(CaseFile caseFile)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(caseFile, _jsonWriteOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public List<string> GetRecentFiles()
    {
        if (File.Exists(RecentFilesPath))
        {
            try
            {
                string json = File.ReadAllText(RecentFilesPath);
                var files = JsonSerializer.Deserialize<List<string>>(json);
                if (files != null) return files;
            }
            catch
            {
            }
        }
        return new List<string>();
    }

    public void AddRecentFile(string filePath)
    {
        var files = GetRecentFiles();
        files.Remove(filePath);
        files.Insert(0, filePath);
        if (files.Count > MaxRecentFiles)
            files.RemoveRange(MaxRecentFiles, files.Count - MaxRecentFiles);

        string? directory = Path.GetDirectoryName(RecentFilesPath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(RecentFilesPath, json);
    }

    public void ClearRecentFiles()
    {
        if (File.Exists(RecentFilesPath))
            File.Delete(RecentFilesPath);
    }

    public void RemoveRecentFile(string filePath)
    {
        var files = GetRecentFiles();
        files.Remove(filePath);

        string? directory = Path.GetDirectoryName(RecentFilesPath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(RecentFilesPath, json);
    }
}
