using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Providers;

public class AlibabaProvider : ILLMProviderModule
{
    private static readonly HttpClient _httpClient = new();

    public string ProviderName => "Alibaba Cloud";
    public string Description => "Alibaba DashScope (Tongyi Qianwen) Models";
    public string DefaultFriendlyName => "Qwen 2.5";

    public List<ProviderField> ConfigFields => new()
    {
        new ProviderField { Key = "ModelId", Label = "Model ID", DefaultValue = "qwen-plus" },
        new ProviderField { Key = "ApiKey", Label = "API Key", IsPassword = true },
        new ProviderField { Key = "Endpoint", Label = "Endpoint URL", DefaultValue = "https://dashscope.aliyuncs.com/compatible-mode/v1" }
    };

    public async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        try
        {
            var response = await GenerateResponseAsync(config, "You are a connectivity tester.", "Respond with 'Connected'");
            return response.Contains("Connected") ? "Success: Connected to Alibaba DashScope" : $"Error: Unexpected response: {response}";
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var endpoint = string.IsNullOrEmpty(config.Endpoint) ? "https://dashscope.aliyuncs.com/compatible-mode/v1" : config.Endpoint;
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
            throw new System.Exception($"Alibaba API error ({response.StatusCode}): {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

        public Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        // Return a list of popular Alibaba/Qwen models
        var models = new List<string>
        {
            "qwen-turbo",
            "qwen-plus",
            "qwen-max",
            "qwen-7b-chat",
            "qwen-14b-chat",
            "qwen-72b-chat",
            "qwen2-7b-instruct",
            "qwen2-72b-instruct"
        };

        return Task.FromResult(models);
    }
}