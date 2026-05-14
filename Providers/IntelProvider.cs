using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class IntelProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Intel";
    public string Description => "Intel Gaudi or OpenVINO Optimized Endpoints (OpenAI Compatible)";
    public string DefaultFriendlyName => "Intel Gaudi Model";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model ID", DefaultValue = "intel/neural-chat-7b-v3-3" },
        new ProviderField { Key = "Endpoint", Label = "Endpoint URL", DefaultValue = "http://localhost:8080/v1" },
        new ProviderField { Key = "ApiKey", Label = "API Key (if required)", IsPassword = true }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to Intel AI Endpoint" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "http://localhost:8080/v1" : config.Endpoint;
        if (!endpoint.EndsWith("/chat/completions")) endpoint = endpoint.TrimEnd('/') + "/chat/completions";

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        var payload = new
        {
            model = config.ModelId,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = config.GetTemperature(),
            max_tokens = config.GetMaxTokens()
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Exception($"Intel API error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

        public Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        // For Intel provider, return some common open-source models that might be deployed on Intel hardware
        var models = new List<string>
        {
            "intel/neural-chat-7b-v3-3",
            "Intel/LaBSE",
            "distilbert-base-uncased",
            "sentence-transformers/all-MiniLM-L6-v2",
            "microsoft/DialoGPT-medium",
            "dbmdz/bert-large-cased-finetuned-conll03-english"
        };

        return Task.FromResult(models);
    }
}