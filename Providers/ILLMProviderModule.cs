using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Returns structured model entries for display in the model selection dialog.
    /// Default implementation parses the legacy string list into ModelListItems.
    /// Providers that can supply richer metadata should override this.
    /// </summary>
    async Task<List<ModelListItem>> GetAvailableModelItemsAsync(AIModelConfiguration config)
    {
        var strings = await GetAvailableModelsAsync(config);
        var items = new List<ModelListItem>();

        foreach (var s in strings)
        {
            if (s.StartsWith("---") || s.StartsWith("⚠"))
            {
                items.Add(new ModelListItem { DisplayName = s, Source = "", IsHeader = true });
                continue;
            }

            var item = new ModelListItem { DisplayName = s, Source = "Remote" };

            // Try to parse "[LOCAL] name" format
            if (s.StartsWith("[LOCAL] "))
            {
                item.DisplayName = s["[LOCAL] ".Length..];
                item.ModelId = item.DisplayName;
                item.Source = "Local";

                // Try to get size from local model directory
                string modelsRoot = !string.IsNullOrWhiteSpace(config.Endpoint)
                    ? config.Endpoint
                    : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Verdict", "Models");
                string localDir = Path.Combine(modelsRoot, item.DisplayName);
                if (Directory.Exists(localDir))
                {
                    var onnxFiles = Directory.GetFiles(localDir, "*.onnx", SearchOption.TopDirectoryOnly);
                    var dataFiles = Directory.GetFiles(localDir, "*.data", SearchOption.TopDirectoryOnly);
                    long totalSize = onnxFiles.Sum(f => new FileInfo(f).Length)
                                   + dataFiles.Sum(f => new FileInfo(f).Length);
                    item.SizeBytes = totalSize;
                    item.SizeFormatted = FormatSize(totalSize);
                }
            }
            // Try to parse "Name (size) - repo-id" format
            else
            {
                var sep = " - ";
                var lastSep = s.LastIndexOf(sep, System.StringComparison.Ordinal);
                if (lastSep >= 0)
                {
                    item.ModelId = s[(lastSep + sep.Length)..];
                    item.DisplayName = s[..lastSep];

                    // Extract size from parentheses
                    var parenStart = item.DisplayName.LastIndexOf('(');
                    var parenEnd = item.DisplayName.LastIndexOf(')');
                    if (parenStart >= 0 && parenEnd > parenStart)
                    {
                        item.SizeFormatted = item.DisplayName[(parenStart + 1)..parenEnd];
                        item.DisplayName = item.DisplayName[..parenStart].Trim();
                    }
                }
                else
                {
                    item.ModelId = s;
                }
            }

            items.Add(item);
        }

        return items;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}