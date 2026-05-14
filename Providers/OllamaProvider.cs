using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class OllamaProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Ollama";
    public string Description => "Local or remote Ollama instance";
    public string DefaultFriendlyName => "Ollama Llama 3";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model Name", DefaultValue = "llama3" },
        new ProviderField { Key = "Endpoint", Label = "Ollama URL", DefaultValue = "http://localhost:11434" }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to Ollama" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "http://localhost:11434" : config.Endpoint;
        if (!endpoint.EndsWith("/api/chat")) endpoint = endpoint.TrimEnd('/') + "/api/chat";

        var payload = new
        {
            model = config.ModelId,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            options = new
            {
                temperature = config.GetTemperature(),
                num_predict = config.GetMaxTokens()
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Exception($"Ollama error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public async Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "http://localhost:11434" : config.Endpoint;
        var modelsEndpoint = endpoint.TrimEnd('/') + "/api/tags";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, modelsEndpoint);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var models = new List<string>();
                var modelsArray = doc.RootElement.GetProperty("models");
                
                foreach (var model in modelsArray.EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        models.Add(name);
                    }
                }

                if (models.Count > 0)
                    return models;
            }
        }
        catch
        {
            // API failed, fall through to local directory scan
        }

        // Scan local Ollama models directory as fallback
        var localModels = ScanLocalOllamaModels();
        if (localModels.Count > 0)
            return localModels;

        // Last resort: return common models
        return new List<string>
        {
            "llama3",
            "mistral",
            "gemma:2b",
            "phi",
            "dolphin-mistral",
            "starling-lm",
            "llama2",
            "codellama",
            "nous-hermes2",
            "llava"
        };
    }

    private static List<string> ScanLocalOllamaModels()
    {
        var models = new List<string>();
        var ollamaDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ollama", "models", "manifests", "registry.ollama.ai", "library");

        if (!System.IO.Directory.Exists(ollamaDir))
            return models;

        try
        {
            foreach (var modelDir in System.IO.Directory.GetDirectories(ollamaDir))
            {
                var modelName = System.IO.Path.GetFileName(modelDir);
                if (string.IsNullOrEmpty(modelName)) continue;

                foreach (var tagFile in System.IO.Directory.GetFiles(modelDir))
                {
                    var tag = System.IO.Path.GetFileName(tagFile);
                    if (string.IsNullOrEmpty(tag)) continue;
                    models.Add($"{modelName}:{tag}");
                }
            }
        }
        catch
        {
            // Ignore directory access errors
        }

        return models;
    }
}