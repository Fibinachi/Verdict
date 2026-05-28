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

    protected List<string> LoadCachedModelList(AIModelConfiguration config)
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

    protected void SaveCachedModelList(AIModelConfiguration config, List<string> models)
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
        var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(modelsRoot))
        {
            models.AddRange(FindLocalModels(modelsRoot));
            foreach (var dir in Directory.GetDirectories(modelsRoot))
                localNames.Add(new DirectoryInfo(dir).Name);
        }

        var cachedModels = LoadCachedModelList(config);
        if (cachedModels.Count > 0)
        {
            // Filter out cached entries for models already downloaded locally
            var prunedCache = cachedModels.Where(entry =>
            {
                var lastSep = entry.LastIndexOf(" - ", StringComparison.Ordinal);
                if (lastSep < 0) return true;
                var modelId = entry[(lastSep + 3)..];
                var folderName = SanitizeFolderName(modelId.Split('/').Last());
                // Remove if already downloaded locally
                if (localNames.Contains(folderName)) return false;
                // Remove if there's a broken/ghost download
                var potentialDir = Path.Combine(modelsRoot, folderName);
                if (Directory.Exists(potentialDir) && !ModelDownloadService.IsValidModelDirectory(potentialDir))
                    return false; // Broken download — don't list it
                return true;
            }).ToList();

            if (prunedCache.Count != cachedModels.Count)
                SaveCachedModelList(config, prunedCache);

            models.AddRange(prunedCache);
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
                    // Allow text-generation models, untagged models, and ONNX-related models
                    if (!string.IsNullOrEmpty(pipelineTag) &&
                        !pipelineTag.StartsWith("text-generation", StringComparison.OrdinalIgnoreCase) &&
                        !pipelineTag.Contains("onnx", StringComparison.OrdinalIgnoreCase))
                        continue;

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

    /// <summary>
    /// Returns structured model entries with size, source, and architecture metadata.
    /// Overrides the legacy string-based list with directory-scanning for local models.
    /// </summary>
    public async Task<List<ModelListItem>> GetAvailableModelItemsAsync(AIModelConfiguration config)
    {
        var items = new List<ModelListItem>();
        string modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);

        // ── Local models with metadata ──
        if (Directory.Exists(modelsRoot))
        {
            foreach (var dir in Directory.GetDirectories(modelsRoot))
            {
                if (!ModelDownloadService.IsValidModelDirectory(dir)) continue;

                var dirInfo = new DirectoryInfo(dir);
                var onnxFiles = Directory.GetFiles(dir, "*.onnx", SearchOption.TopDirectoryOnly);
                var dataFiles = Directory.GetFiles(dir, "*.data", SearchOption.TopDirectoryOnly);
                long totalSize = onnxFiles.Sum(f => new FileInfo(f).Length)
                               + dataFiles.Sum(f => new FileInfo(f).Length);

                // Detect architecture hints from file names
                string arch = "";
                var modelVariants = onnxFiles.Select(f => Path.GetFileName(f).ToLowerInvariant()).ToArray();
                if (modelVariants.Any(f => f.Contains("int4") || f.Contains("q4"))) arch = "INT4";
                else if (modelVariants.Any(f => f.Contains("int8") || f.Contains("q8"))) arch = "INT8";
                else if (modelVariants.Any(f => f.Contains("fp16"))) arch = "FP16";
                else if (modelVariants.Any(f => f.Contains("bnb4"))) arch = "BNB4";

                items.Add(new ModelListItem
                {
                    DisplayName = dirInfo.Name,
                    ModelId = dirInfo.FullName,
                    Source = "Local",
                    SizeBytes = totalSize,
                    SizeFormatted = FormatItemSize(totalSize),
                    Architecture = arch
                });
            }
        }

        if (items.Count == 0)
        {
            items.Add(new ModelListItem { DisplayName = "Recommended ONNX GenAI Models (enter ID below)", IsHeader = true });
        }

        // ── Cached remote models ──
        var cached = LoadCachedModelList(config);
        if (cached.Count > 0)
        {
            // Track which local folder names are already present so we don't show duplicates
            var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(modelsRoot))
            {
                foreach (var dir in Directory.GetDirectories(modelsRoot))
                    localNames.Add(new DirectoryInfo(dir).Name);
            }

            var validCachedEntries = new List<string>();
            foreach (var entry in cached)
            {
                var sep = " - ";
                var lastSep = entry.LastIndexOf(sep, StringComparison.Ordinal);
                if (lastSep < 0)
                {
                    validCachedEntries.Add(entry);
                    continue;
                }

                var modelId = entry[(lastSep + sep.Length)..];
                var display = entry[..lastSep];

                // Skip cached entries for models that are already downloaded locally
                var folderName = SanitizeFolderName(modelId.Split('/').Last());
                if (localNames.Contains(folderName))
                    continue;

                // Check for partial/ghost download — a folder exists but isn't valid
                var potentialLocalDir = Path.Combine(modelsRoot, folderName);
                bool isGhostDownload = Directory.Exists(potentialLocalDir)
                    && !ModelDownloadService.IsValidModelDirectory(potentialLocalDir);

                string size = "";
                var parenStart = display.LastIndexOf('(');
                var parenEnd = display.LastIndexOf(')');
                if (parenStart >= 0 && parenEnd > parenStart)
                {
                    var parenContent = display[(parenStart + 1)..parenEnd];
                    if (LooksLikeFileSize(parenContent))
                    {
                        size = parenContent;
                        display = display[..parenStart].Trim();
                    }
                }

                items.Add(new ModelListItem
                {
                    DisplayName = isGhostDownload ? $"⚠ {display} (broken download)" : display,
                    ModelId = modelId,
                    Source = isGhostDownload ? "Broken" : "HuggingFace",
                    SizeFormatted = size,
                    IsHeader = false
                });
                validCachedEntries.Add(entry);
            }

            // Prune stale cache entries (keep only what we displayed)
            if (validCachedEntries.Count != cached.Count)
                SaveCachedModelList(config, validCachedEntries);

            return items;
        }

        // ── Fetch from HuggingFace API ──
        try
        {
            var seen = new HashSet<string>();
            var remoteEntries = new List<string>();

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
                    if (!string.IsNullOrEmpty(pipelineTag) &&
                        !pipelineTag.StartsWith("text-generation", StringComparison.OrdinalIgnoreCase) &&
                        !pipelineTag.Contains("onnx", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = modelId.Split('/').LastOrDefault() ?? modelId;
                    var remoteEntry = FormatRemoteModelEntry(modelId, name, 0, 0);
                    remoteEntries.Add(remoteEntry);

                    items.Add(new ModelListItem
                    {
                        DisplayName = name,
                        ModelId = modelId,
                        Source = "HuggingFace",
                        SizeFormatted = "" // HF API doesn't provide size in search results
                    });
                }
            }

            SaveCachedModelList(config, remoteEntries);
        }
        catch
        {
            // Fall back to cached list if available
            var fallback = LoadCachedModelList(config);
            if (fallback.Count > 0 && !items.Any(i => i.Source == "HuggingFace"))
            {
                items.Add(new ModelListItem { DisplayName = "⚠ Cached (offline)", IsHeader = true });
            }
        }

        return items;
    }

    /// <summary>Returns true if text looks like a file size ("1.8 GB"), not "15K downloads".</summary>
    private static bool LooksLikeFileSize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToUpperInvariant();
        return t.EndsWith("GB") || t.EndsWith("MB") || t.EndsWith("KB") || t.EndsWith("B")
            || t.EndsWith("TB") || t.Contains("GB ") || t.Contains("MB ") || t.Contains("KB ");
    }

    private static string FormatItemSize(long bytes)
    {
        if (bytes <= 0) return "";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    public async Task DownloadModelAsync(AIModelConfiguration config, IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var modelId = config.ModelId.Trim();
        if (!modelId.Contains("/"))
            throw new ArgumentException("ModelId must be a HuggingFace repo ID.");

        var modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);
        var modelDir = Path.Combine(modelsRoot, SanitizeFolderName(modelId.Split('/').Last()));

        // Check if model is already fully downloaded (not just partial)
        if (IsModelDownloadComplete(modelDir))
        {
            progress?.Report(new DownloadProgressInfo { ModelId = modelId, TotalFiles = 1, FilesCompleted = 1, TotalBytes = 1, BytesDownloaded = 1, CurrentFile = "Already downloaded" });
            return;
        }

        // Partial download may exist — let per-file resume logic handle it
        // (stub files and tiny files are deleted and re-downloaded; substantial files are kept)

        await DownloadModelFilesAsync(modelId, modelDir, progress, ct);
    }

    /// <summary>
    /// Validates that a model directory has all required files and isn't a partial download.
    /// For ONNX: checks .onnx files are substantial (>50MB, not LFS stubs).
    /// For GGUF: checks .gguf files exist and are substantial (>50MB).
    /// For other: checks files match patterns and are >100KB.
    /// </summary>
    protected virtual bool IsModelDownloadComplete(string modelDir)
    {
        if (!Directory.Exists(modelDir)) return false;

        foreach (var pattern in ModelFilePatterns)
        {
            var files = Directory.GetFiles(modelDir, pattern, SearchOption.AllDirectories);
            if (files.Length == 0) return false;

            // Check that at least one matching file is substantial.
            // For .onnx/.gguf files, require >50MB (real models are always large).
            // For other files (.json, etc.), require >100KB to skip tiny stubs.
            long minSize = (pattern == "*.onnx" || pattern == "*.gguf") ? 50_000_000 : 100_000;

            // Also check for LFS pointer stubs (small text files that point to missing LFS objects)
            if (files.Any(f => {
                long len = new FileInfo(f).Length;
                if (len < minSize) return false;
                // Check for LFS pointer stub
                if (len < 1024 && pattern == "*.onnx")
                {
                    try { return !File.ReadAllText(f).StartsWith("version https://git-lfs.github.com", StringComparison.Ordinal); }
                    catch { return false; }
                }
                return true;
            }))
                return true;
        }

        return false;
    }

    private class ModelCacheEntry
    {
        public DateTime CachedAt { get; set; }
        public List<string> Models { get; set; } = [];
    }
}
