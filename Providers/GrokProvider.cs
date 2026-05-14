using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class GrokProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Grok";
    public string Description => "xAI Grok models (Grok-1, Grok-1.5, Grok-2) via X.AI API";
    public string DefaultFriendlyName => "Grok-2";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model ID", DefaultValue = "grok-2" },
        new ProviderField { Key = "ApiKey", Label = "API Key", IsPassword = true },
        new ProviderField { Key = "Endpoint", Label = "API Base URL", DefaultValue = "https://api.x.ai/v1" }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to Grok" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "https://api.x.ai/v1" : config.Endpoint;
        if (!endpoint.EndsWith("/chat/completions")) endpoint = endpoint.TrimEnd('/') + "/chat/completions";

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

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
            throw new System.Exception($"Grok API error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        var models = new List<string>
        {
            "grok-2",
            "grok-2-vision",
            "grok-1",
            "grok-1.5"
        };

        return Task.FromResult(models);
    }
}