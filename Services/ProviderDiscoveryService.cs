using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verdict.Models;

namespace Verdict.Services;

public class ProviderDiscoveryService
{
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
                    _providers.Add(provider);
                }
            }
            
            // In the future, scan a "Plugins" directory for external DLLs here
        }
        return _providers;
    }

    public static ILLMProviderModule? GetProviderByName(string name)
    {
        return GetAvailableProviders().FirstOrDefault(p =>
            p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
