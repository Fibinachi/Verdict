using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Verdict.Models;

public class ProviderField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPassword { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
}

public interface ILLMProviderModule
{
    string ProviderName { get; }
    string Description { get; }
    string DefaultFriendlyName { get; }
    List<ProviderField> ConfigFields { get; }
    Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default);
    Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config);
}