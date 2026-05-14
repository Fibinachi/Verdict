using System;
using System.IO;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.Services;

public static class CharacterManager
{
    private static readonly string CharactersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Characters");

    public static string RepositoryPath => CharactersDirectory;

    static CharacterManager()
    {
        if (!Directory.Exists(CharactersDirectory))
        {
            Directory.CreateDirectory(CharactersDirectory);
        }
    }

    public static void SaveCharacter(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        string fileName = $"{agent.Name.Replace(" ", "_")}.vcs";
        string filePath = Path.Combine(CharactersDirectory, fileName);

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(agent, options);
        File.WriteAllText(filePath, json);
    }

    public static Agent? LoadCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return null;

        string fileName = $"{characterName.Replace(" ", "_")}.vcs";
        string filePath = Path.Combine(CharactersDirectory, fileName);

        if (!File.Exists(filePath)) return null;

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Agent>(json);
    }

    public static string[] GetAvailableCharacters()
    {
        if (!Directory.Exists(CharactersDirectory)) return Array.Empty<string>();
        
        var files = Directory.GetFiles(CharactersDirectory, "*.vcs");
        for (int i = 0; i < files.Length; i++)
        {
            files[i] = Path.GetFileNameWithoutExtension(files[i]).Replace("_", " ");
        }
        return files;
    }
}
