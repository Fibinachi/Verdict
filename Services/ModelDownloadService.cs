using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Verdict.Services
{
    /// <summary>
    /// Represents a downloadable ONNX model from HuggingFace.
    /// </summary>
    public class HuggingFaceModelInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long Downloads { get; set; }
        public long SizeOnDisk { get; set; }
        public string PipelineTag { get; set; } = string.Empty;
        public bool HasGenaiConfig { get; set; }
        public List<string> FileList { get; set; } = new();
    }

    /// <summary>
    /// Progress information for model downloads.
    /// </summary>
    public class DownloadProgressInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public int FilesCompleted { get; set; }
        public int TotalFiles { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public double ProgressPercent => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100 : 0;
    }

    /// <summary>
    /// Service for discovering and downloading ONNX models from HuggingFace.
    /// </summary>
    public class ModelDownloadService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Verdict/1.0" } }
        };

        private static readonly HttpClient _downloadClient = new()
        {
            Timeout = TimeSpan.FromHours(2),
            DefaultRequestHeaders = { { "User-Agent", "Verdict/1.0" } }
        };

        // Required files for OnnxRuntimeGenAI compatibility
        private static readonly string[] RequiredExtensions = { ".onnx", ".json", ".txt" };

        // Files that indicate a model is compatible with OnnxRuntimeGenAI
        private const string GenaiConfigFile = "genai_config.json";

        // Written only after a successful download+commit.
        // This prevents partial/incomplete downloads from being treated as usable.
        private const string DownloadCompleteMarkerFile = "download_complete.ok";

        // Prevent empty/partial ONNX files from being treated as valid.
        // (1KB is arbitrary but filters out the known "empty file" repro case.)
        private const long MinOnnxFileSizeBytes = 1024;

        /// <summary>
        /// Searches HuggingFace for ONNX models suitable for local LLM inference.
        /// </summary>
        public async Task<List<HuggingFaceModelInfo>> SearchModelsAsync(string? searchQuery = null, int limit = 30, CancellationToken ct = default)
        {
            var results = new List<HuggingFaceModelInfo>();
            
            try
            {
                // Search for ONNX models on HuggingFace
                var query = searchQuery ?? "onnx llm instruct genai";
                var searchUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&sort=downloads&direction=-1&limit={limit}";
                
                var response = await _httpClient.GetAsync(searchUrl, ct);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var modelId = element.TryGetProperty("modelId", out var mid) ? mid.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(modelId)) continue;
                    
                    var pipelineTag = element.TryGetProperty("pipeline_tag", out var pt) ? pt.GetString() ?? "" : "";
                    
                    // Filter for text-generation models
                    if (pipelineTag != "text-generation" && pipelineTag != "") continue;
                    
                    var downloads = element.TryGetProperty("downloads", out var dl) ? dl.GetInt64() : 0;
                    
                    var modelInfo = new HuggingFaceModelInfo
                    {
                        ModelId = modelId,
                        Name = modelId.Split('/').LastOrDefault() ?? modelId,
                        Description = element.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Downloads = downloads,
                        PipelineTag = pipelineTag,
                    };
                    
                    results.Add(modelInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching HuggingFace models: {ex.Message}");
            }
            
            return results;
        }

        /// <summary>
        /// Gets detailed information about a specific model, including its file list.
        /// </summary>
        public async Task<HuggingFaceModelInfo?> GetModelDetailsAsync(string modelId, CancellationToken ct = default)
        {
            try
            {
                var apiUrl = $"https://huggingface.co/api/models/{modelId}";
                var response = await _httpClient.GetAsync(apiUrl, ct);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                
                var modelInfo = new HuggingFaceModelInfo
                {
                    ModelId = modelId,
                    Name = modelId.Split('/').LastOrDefault() ?? modelId,
                    Description = doc.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    Downloads = doc.RootElement.TryGetProperty("downloads", out var dl) ? dl.GetInt64() : 0,
                    PipelineTag = doc.RootElement.TryGetProperty("pipeline_tag", out var pt) ? pt.GetString() ?? "" : "",
                };
                
                // Get file list from siblings
                PopulateModelFilesFromSiblings(modelInfo, doc.RootElement);

                
                // Estimate size
                if (doc.RootElement.TryGetProperty("sizeOnDisk", out var size))
                    modelInfo.SizeOnDisk = size.GetInt64();
                
                return modelInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting model details: {ex.Message}");
                return null;
            }
        }

        private void PopulateModelFilesFromSiblings(HuggingFaceModelInfo modelInfo, JsonElement root)
        {
            if (!root.TryGetProperty("siblings", out var siblings)) return;

            foreach (var sibling in siblings.EnumerateArray())
            {
                var filename = sibling.TryGetProperty("rfilename", out var rf) ? rf.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(filename)) continue;

                var ext = Path.GetExtension(filename).ToLowerInvariant();
                if (RequiredExtensions.Contains(ext) || filename.Equals(GenaiConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    modelInfo.FileList.Add(filename);
                    if (filename.Equals(GenaiConfigFile, StringComparison.OrdinalIgnoreCase))
                        modelInfo.HasGenaiConfig = true;
                }
            }
        }

        /// <summary>
        /// Downloads a model from HuggingFace to the specified directory.
        /// Reports progress via the provided callback.
        /// </summary>
        public async Task DownloadModelAsync(
            string modelId,
            string destinationDir,
            Action<DownloadProgressInfo>? onProgress = null,
            CancellationToken ct = default)
        {
            // Get model details first to know what files to download
            var modelInfo = await GetModelDetailsAsync(modelId, ct);
            if (modelInfo == null)
                throw new Exception($"Could not fetch model info for {modelId}");

            // If no file list, try a second details fetch (keeps behavior same)
            if (modelInfo.FileList.Count == 0)
            {
                var details = await GetModelDetailsAsync(modelId, ct);
                if (details != null)
                    modelInfo = details;
            }

            // If still no files, fetch siblings directly and populate
            if (modelInfo.FileList.Count == 0)
            {
                var apiUrl = $"https://huggingface.co/api/models/{modelId}";
                var response = await _httpClient.GetAsync(apiUrl, ct);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                PopulateModelFilesFromSiblings(modelInfo, doc.RootElement);
            }

            if (modelInfo.FileList.Count == 0)
                throw new Exception($"No downloadable files found for model {modelId}");

            // Ensure LFS files are included - many ONNX repos store .onnx in LFS
            var lfsFiles = new[] { "model.onnx", "model.safetensors", "model.onnx.data" };
            foreach (var lfsFile in lfsFiles)
            {
                if (!modelInfo.FileList.Contains(lfsFile))
                    modelInfo.FileList.Add(lfsFile);
            }

            var progress = new DownloadProgressInfo
            {
                ModelId = modelId,
                TotalFiles = modelInfo.FileList.Count
            };

            // Atomic-ish commit:
            // - download into a temp directory
            // - only after success: replace destinationDir contents + write marker
            var tempDir = destinationDir + ".tmp_" + Guid.NewGuid().ToString("N");
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
            Directory.CreateDirectory(tempDir);

            long totalBytesDownloaded = 0;
            var failedFiles = new List<string>();

            try
            {
                for (int i = 0; i < modelInfo.FileList.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var filename = modelInfo.FileList[i];
                    var fileUrl = $"https://huggingface.co/{modelId}/resolve/main/{filename}";
                    var destPath = Path.Combine(tempDir, filename);

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    progress.CurrentFile = filename;
                    progress.FilesCompleted = i;
                    onProgress?.Invoke(progress);

                    try
                    {
                        using var response = await _downloadClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1;

                        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                        await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                        var buffer = new byte[8192];
                        long fileBytesDownloaded = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                            fileBytesDownloaded += bytesRead;
                            totalBytesDownloaded += bytesRead;

                            progress.BytesDownloaded = totalBytesDownloaded;
                            progress.TotalBytes = totalBytes > 0
                                ? totalBytesDownloaded + (modelInfo.FileList.Count - i - 1) * totalBytes
                                : 0;
                            onProgress?.Invoke(progress);
                        }

                        await fileStream.FlushAsync(ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to download {filename}: {ex.Message}");
                        failedFiles.Add(filename);

                        if (File.Exists(destPath))
                        {
                            try { File.Delete(destPath); } catch { }
                        }
                    }
                }

                var criticalExtensions = new[] { ".onnx", ".safetensors" };
                var criticalFailed = failedFiles
                    .Where(f => criticalExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (criticalFailed.Count > 0)
                    throw new Exception($"Failed to download critical model files: {string.Join(", ", criticalFailed)}");

                // Ensure genai_config.json exists with required fields in the temp directory
                var genaiConfigPath = Path.Combine(tempDir, GenaiConfigFile);
                var needsUpdate = false;

                if (File.Exists(genaiConfigPath))
                {
                    try
                    {
                        var existingConfig = File.ReadAllText(genaiConfigPath);
                        using var doc = JsonDocument.Parse(existingConfig);
                        var root = doc.RootElement;

                        bool hasContextLength = root.TryGetProperty("context_length", out _) ||
                                               (root.TryGetProperty("model", out var modelEl) &&
                                                modelEl.TryGetProperty("context_length", out _));

                        if (!hasContextLength)
                            needsUpdate = true;
                    }
                    catch
                    {
                        needsUpdate = true;
                    }
                }
                else
                {
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    var defaultConfig = new
                    {
                        model = new
                        {
                            context_length = 2048,
                            decoder = new
                            {
                                session_options = new
                                {
                                    enable_cpu_mem_arena = false
                                }
                            }
                        }
                    };
                    var configJson = System.Text.Json.JsonSerializer.Serialize(
                        defaultConfig,
                        new JsonSerializerOptions { WriteIndented = true }
                    );
                    await File.WriteAllTextAsync(genaiConfigPath, configJson, ct);
                }

                // Validate tempDir looks usable before committing
                if (!IsValidModelDirectory(tempDir))
                    throw new Exception($"Downloaded files are not valid/complete for {modelId}.");

                // Commit:
                // - remove old destination
                // - move tempDir into destinationDir path
                if (Directory.Exists(destinationDir))
                {
                    try { Directory.Delete(destinationDir, true); } catch { /* ignore */ }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationDir)!);
                Directory.Move(tempDir, destinationDir);

                // Finally write marker (only after commit + validation)
                var markerPath = Path.Combine(destinationDir, DownloadCompleteMarkerFile);
                await File.WriteAllTextAsync(markerPath, DateTime.UtcNow.ToString("O"), ct);

                progress.FilesCompleted = modelInfo.FileList.Count;
                progress.CurrentFile = "Complete";
                onProgress?.Invoke(progress);
            }
            catch
            {
                // Cleanup temp dir
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Checks if a directory contains a valid ONNX model for OnnxRuntimeGenAI.
        /// </summary>
        public static bool IsValidModelDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return false;

            // Must have a completion marker to prevent partial/incomplete reuse.
            var markerPath = Path.Combine(directoryPath, DownloadCompleteMarkerFile);
            if (!File.Exists(markerPath))
                return false;

            // genai_config.json must exist and be parseable.
            // (OnnxProvider later may rewrite fields, but validation must be strict here.)
            var genaiConfigCandidates = new List<string>();

            var rootGenai = Path.Combine(directoryPath, GenaiConfigFile);
            if (File.Exists(rootGenai)) genaiConfigCandidates.Add(rootGenai);

            genaiConfigCandidates.AddRange(
                Directory.GetFiles(directoryPath, GenaiConfigFile, SearchOption.AllDirectories)
            );

            genaiConfigCandidates = genaiConfigCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (genaiConfigCandidates.Count == 0)
                return false;

            bool hasRequiredGenaiConfig = false;
            foreach (var candidate in genaiConfigCandidates)
            {
                try
                {
                    var existingConfig = File.ReadAllText(candidate);
                    if (string.IsNullOrWhiteSpace(existingConfig))
                        continue;

                    using var doc = JsonDocument.Parse(existingConfig);
                    var root = doc.RootElement;

                    bool hasContextLength = root.TryGetProperty("context_length", out _) ||
                                             (root.TryGetProperty("model", out var modelEl) &&
                                              modelEl.TryGetProperty("context_length", out _));

                    if (hasContextLength)
                    {
                        hasRequiredGenaiConfig = true;
                        break;
                    }
                }
                catch
                {
                    // try other candidates
                }
            }

            if (!hasRequiredGenaiConfig)
                return false;

            // At least one non-empty .onnx file
            var onnxFiles = Directory.GetFiles(directoryPath, "*.onnx", SearchOption.AllDirectories);
            if (onnxFiles.Length == 0)
                return false;

            var hasSaneOnnx = onnxFiles.Any(f =>
            {
                try
                {
                    var info = new FileInfo(f);
                    return info.Length >= MinOnnxFileSizeBytes;
                }
                catch
                {
                    return false;
                }
            });

            return hasSaneOnnx;
        }

        /// <summary>
        /// Scans a directory for subdirectories containing ONNX models.
        /// </summary>
        public static List<string> ScanForModels(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory)) return new List<string>();
            
            return Directory.GetDirectories(rootDirectory)
                .Where(dir => IsValidModelDirectory(dir))
                .Select(dir => new DirectoryInfo(dir).Name)
                .ToList();
        }
    }
}
