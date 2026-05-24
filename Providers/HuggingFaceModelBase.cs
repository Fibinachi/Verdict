using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Providers;

public abstract class HuggingFaceModelBase : ILLMProviderModule
{
    protected static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Verdict/1.0" } }
    };

    protected static readonly HttpClient _downloadClient = new()
    {
        Timeout = TimeSpan.FromHours(2),
        DefaultRequestHeaders = { { "User-Agent", "Verdict/1.0" } }
    };

    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    public abstract string ProviderName { get; }
    public abstract string Description { get; }
    public abstract string DefaultFriendlyName { get; }
    public abstract List<ProviderField> ConfigFields { get; }

    protected abstract string CacheFileName { get; }
    protected abstract string DefaultModelsRoot { get; }
    protected abstract string[] SearchQueries { get; }
    protected abstract string[] ModelFilePatterns { get; }

    protected abstract List<string> FindLocalModels(string modelsRoot);
    protected abstract string FormatRemoteModelEntry(string modelId, string name, long downloads, long sizeOnDisk);
    protected abstract Task DownloadModelFilesAsync(string modelId, string destinationDir, IProgress<DownloadProgressInfo>? progress, CancellationToken ct = default);

    public abstract Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default);
    public abstract Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default);

    protected static string SanitizeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    protected static string ResolveModelsRoot(AIModelConfiguration config, string defaultRoot)
    {
        return string.IsNullOrWhiteSpace(config?.Endpoint) ? defaultRoot : config.Endpoint;
    }

    private string GetCacheFilePath(AIModelConfiguration config)
    {
        var root = ResolveModelsRoot(config, DefaultModelsRoot);
        return Path.Combine(root, CacheFileName);
    }

    private List<string> LoadCachedModelList(AIModelConfiguration config)
    {
        try
        {
            var path = GetCacheFilePath(config);
            if (!File.Exists(path)) return [];
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return [];

            var entry = JsonSerializer.Deserialize<ModelCacheEntry>(text);
            if (entry?.Models == null || entry.Models.Count == 0) return [];
            if (DateTime.UtcNow - entry.CachedAt > CacheExpiry) return [];

            return entry.Models;
        }
        catch
        {
            return [];
        }
    }

    private void SaveCachedModelList(AIModelConfiguration config, List<string> models)
    {
        var path = GetCacheFilePath(config);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmpPath = path + ".tmp";
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(new ModelCacheEntry
        {
            CachedAt = DateTime.UtcNow,
            Models = models
        }));
        File.Move(tmpPath, path, overwrite: true);
    }

    public async Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        var models = new List<string>();

        string modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);
        if (Directory.Exists(modelsRoot))
            models.AddRange(FindLocalModels(modelsRoot));

        var cachedModels = LoadCachedModelList(config);
        if (cachedModels.Count > 0)
        {
            models.AddRange(cachedModels);
            return models;
        }

        try
        {
            var seen = new HashSet<string>();
            var remoteModels = new List<string>();

            foreach (var query in SearchQueries)
            {
                var url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&sort=downloads&direction=-1&limit=20";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var modelId = el.TryGetProperty("modelId", out var mid) ? mid.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(modelId) || !seen.Add(modelId)) continue;

                    var pipelineTag = el.TryGetProperty("pipeline_tag", out var pt) ? pt.GetString() ?? "" : "";
                    if (pipelineTag != "text-generation" && pipelineTag != "") continue;

                    var downloads = el.TryGetProperty("downloads", out var dl) ? dl.GetInt64() : 0;
                    var name = modelId.Split('/').LastOrDefault() ?? modelId;

                    var entry = FormatRemoteModelEntry(modelId, name, downloads, 0);
                    remoteModels.Add(entry);
                    models.Add(entry);
                }
            }

            SaveCachedModelList(config, remoteModels);
        }
        catch (Exception ex)
        {
            var fallback = LoadCachedModelList(config);
            if (fallback.Count > 0)
            {
                models.Add("⚠ Using cached model list (offline). Delete cache to retry.");
                models.AddRange(fallback);
            }
            else
            {
                models.Add($"Error searching HuggingFace: {ex.Message}");
            }
        }

        return models;
    }

    public async Task DownloadModelAsync(AIModelConfiguration config, IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var modelId = config.ModelId.Trim();
        if (!modelId.Contains("/"))
            throw new ArgumentException("ModelId must be a HuggingFace repo ID.");

        var modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);
        var modelDir = Path.Combine(modelsRoot, SanitizeFolderName(modelId.Split('/').Last()));

        if (Directory.Exists(modelDir))
        {
            // Only treat models as downloaded if they pass the provider's "valid/complete" checks.
            // For ONNX providers this is enforced via download_complete.ok + strict genai_config validation.
            if (ModelDownloadService.IsValidModelDirectory(modelDir))
            {
                progress?.Report(new DownloadProgressInfo
                {
                    ModelId = modelId,
                    TotalFiles = 1,
                    FilesCompleted = 1,
                    TotalBytes = 1,
                    BytesDownloaded = 1,
                    CurrentFile = "Already downloaded"
                });
                return;
            }
        }

        await DownloadModelFilesAsync(modelId, modelDir, progress, ct);
    }

    private class ModelCacheEntry
    {
        public DateTime CachedAt { get; set; }
        public List<string> Models { get; set; } = [];
    }
}
