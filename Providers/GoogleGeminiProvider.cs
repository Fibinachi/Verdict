using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class GoogleGeminiProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Google Gemini";
    public string Description => "Google Gemini models - automatically loads available models based on your API key";
    public string DefaultFriendlyName => "Gemini 1.5 Pro";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model ID", DefaultValue = "gemini-1.5-pro" },
        new ProviderField { Key = "ApiKey", Label = "API Key", IsPassword = true }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to Google Gemini" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelId}:generateContent?key={config.ApiKey}";

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = config.GetTemperature(),
                maxOutputTokens = config.GetMaxTokens(1024)
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Exception($"Google Gemini API error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    public async Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        try
        {
            // Fetch actual models from the Google API
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={config.ApiKey}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new System.Exception($"Failed to fetch models from Google API: {content}");
            }

            using var doc = JsonDocument.Parse(content);
            var models = new List<string>();

            foreach (var modelElement in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                var modelId = modelElement.GetProperty("name").GetString();
                // Remove the "models/" prefix to get the actual model ID
                if (modelId?.StartsWith("models/") == true)
                {
                    modelId = modelId.Substring("models/".Length);
                }

                // Filter to only include generative models (not embeddings or other types)
                var supportedGenerationMethods = modelElement.GetProperty("supportedGenerationMethods").EnumerateArray();
                if (supportedGenerationMethods.Any(method => method.GetString() == "generateContent"))
                {
                    if (!string.IsNullOrEmpty(modelId)) // Check for null or empty before adding
                    {
                        models.Add(modelId);
                    }
                }
            }

            return models;
        }
        catch (System.Exception)
        {
            // Fallback to common models if API call fails
            return new List<string>
            {
                "gemini-1.5-pro",
                "gemini-1.5-flash",
                "gemini-1.0-pro"
            };
        }
    }
}