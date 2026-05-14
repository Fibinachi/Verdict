using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class NvidiaProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "NVIDIA";
    public string Description => "NVIDIA NIM or API Catalog Endpoints";
    public string DefaultFriendlyName => "NVIDIA Nemotron";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model ID", DefaultValue = "nvidia/llama-3.1-nemotron-70b-instruct" },
        new ProviderField { Key = "ApiKey", Label = "API Key", IsPassword = true },
        new ProviderField { Key = "Endpoint", Label = "Endpoint URL", DefaultValue = "https://integrate.api.nvidia.com/v1" }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to NVIDIA API Catalog" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "https://integrate.api.nvidia.com/v1" : config.Endpoint;
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
            temperature = config.GetTemperature(0.5),
            top_p = 1,
            max_tokens = config.GetMaxTokens(1024)
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Exception($"NVIDIA API error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

        public Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        // Return a list of popular NVIDIA/NIM models
        var models = new List<string>
        {
            "nvidia/llama-3.1-nemotron-70b-instruct",
            "nvidia/llama-3.1-nemotron-51b-instruct",
            "meta/llama3-70b-instruct",
            "meta/llama3-8b-instruct",
            "microsoft/phi-3-mini-128k-instruct",
            "google/gemma-2b",
            "google/gemma-7b",
            "snowflake/arctic",
            "microsoft/dialo-125m",
            "bigscience/bloom-560m"
        };

        return Task.FromResult(models);
    }
}