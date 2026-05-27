using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verdict.Models;

namespace Verdict.Services;

public class ProviderDiscoveryService
{
    /// <summary>
    /// Whitelist of active provider names. Only these providers are available at runtime.
    /// All other providers remain in the codebase for future development but are deactivated.
    /// Add provider names here to re-enable them.
    /// </summary>
    private static readonly HashSet<string> ActiveProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeepSeek",
        "Google Gemini",
        "ONNX",
        "Hugging Face",
        // "OpenAI",          // deactivated - re-add to enable
        // "Anthropic",       // deactivated - re-add to enable
        // "Grok",            // deactivated - re-add to enable
        // "Alibaba Cloud",   // deactivated - re-add to enable
        // "Ollama",          // deactivated - re-add to enable
        // "Hugging Face",    // deactivated - re-add to enable
        // "NVIDIA",          // deactivated - re-add to enable
        // "Intel",           // deactivated - re-add to enable
    };

    private static List<ILLMProviderModule>? _providers;

    public static List<ILLMProviderModule> GetAvailableProviders()
    {
        if (_providers == null)
        {
            _providers = new List<ILLMProviderModule>();
            
            // Scan current assembly for provider modules
            var interfaceType = typeof(ILLMProviderModule);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => interfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is ILLMProviderModule provider)
                {
                    // Only register active providers
                    if (ActiveProviders.Contains(provider.ProviderName))
                        _providers.Add(provider);
                }
            }
            
            // In the future, scan a "Plugins" directory for external DLLs here
        }
        return _providers;
    }

    public static ILLMProviderModule? GetProviderByName(string name)
    {
        return GetAvailableProviders().FirstOrDefault(p => p.ProviderName == name);
    }
}
