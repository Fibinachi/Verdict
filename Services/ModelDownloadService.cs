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

            // Create destination directory
            Directory.CreateDirectory(destinationDir);

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

            long totalBytesDownloaded = 0;
            var failedFiles = new List<string>();

            for (int i = 0; i < modelInfo.FileList.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filename = modelInfo.FileList[i];
                var fileUrl = $"https://huggingface.co/{modelId}/resolve/main/{filename}";
                var destPath = Path.Combine(destinationDir, filename);

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
                        progress.TotalBytes = totalBytes > 0 ? totalBytesDownloaded + (modelInfo.FileList.Count - i - 1) * totalBytes : 0;
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
            var criticalFailed = failedFiles.Where(f => criticalExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();
            if (criticalFailed.Count > 0)
                throw new Exception($"Failed to download critical model files: {string.Join(", ", criticalFailed)}");

            var genaiConfigPath = Path.Combine(destinationDir, GenaiConfigFile);
            if (!File.Exists(genaiConfigPath))
            {
                var defaultConfig = new
                {
                    model_type = "onnx",
                    architectures = new[] { "OnnxModel" },
                    max_position_embeddings = 2048
                };
                var configJson = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(genaiConfigPath, configJson, ct);
            }

            progress.FilesCompleted = modelInfo.FileList.Count;
            progress.CurrentFile = "Complete";
            onProgress?.Invoke(progress);
        }

        /// <summary>
        /// Checks if a directory contains a valid ONNX model for OnnxRuntimeGenAI.
        /// </summary>
        public static bool IsValidModelDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return false;
            
            var hasOnnxFile = Directory.GetFiles(directoryPath, "*.onnx", SearchOption.TopDirectoryOnly).Length > 0;
            var hasGenaiConfig = File.Exists(Path.Combine(directoryPath, GenaiConfigFile));
            
            return hasOnnxFile && hasGenaiConfig;
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
