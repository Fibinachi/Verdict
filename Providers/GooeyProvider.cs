using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class GooeyProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Gooey AI";
    public string Description => "Gooey AI Lipsync and animation services";
    public string DefaultFriendlyName => "Gooey AI Lipsync";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ApiKey", Label = "API Key", IsPassword = true },
        new ProviderField { Key = "Endpoint", Label = "API Base URL (Optional)", DefaultValue = "https://api.gooey.ai/v2" }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "https://api.gooey.ai/v2" : config.Endpoint;
            endpoint = endpoint.TrimEnd('/') + "/Lipsync/";

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            var payload = new
            {
                text = "Test connection",
                voice = "peter"
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: {response.StatusCode} - {content}";
            }

            return "Success: Connected to Gooey AI";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        throw new System.NotSupportedException("Gooey AI provider only supports Lipsync functionality, not text generation.");
    }

    public async Task<LipsyncResponse> GenerateLipsyncAsync(AIModelConfiguration config, string text, string voice = "peter", string animation = "neutral", float speed = 1.0f)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "https://api.gooey.ai/v2" : config.Endpoint;
        endpoint = endpoint.TrimEnd('/') + "/Lipsync/";

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var payload = new
        {
            text = text,
            voice = voice,
            animation = animation,
            speed = speed
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Exception($"Gooey AI Lipsync error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var result = doc.RootElement.GetProperty("result");
        
        return new LipsyncResponse
        {
            Url = result.GetProperty("url").GetString() ?? "",
            VideoUrl = result.GetProperty("video_url").GetString() ?? "",
            Duration = result.GetProperty("duration").GetSingle(),
            CharacterCount = result.GetProperty("character_count").GetInt32()
        };
    }

    public Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        // Gooey AI doesn't have traditional models, return service types
        var models = new List<string>
        {
            "Lipsync Service",
            "Animation Service"
        };

        return Task.FromResult(models);
    }
}

public class LipsyncResponse
{
    public string Url { get; set; } = "";
    public string VideoUrl { get; set; } = "";
    public float Duration { get; set; }
    public int CharacterCount { get; set; }
}