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
        private static readonly string[] RequiredExtensions = { ".onnx", ".json", ".txt", ".data", ".onnx_data" };
        
        // Files that indicate a model is compatible with OnnxRuntimeGenAI
        private const string GenaiConfigFile = "genai_config.json";

        /// <summary>Min file size for a real .onnx model (not an LFS stub).</summary>
        private const long MinOnnxModelBytes = 50_000_000; // 50 MB — real ONNX models are always >50MB

        /// <summary>Manifest file recording completed downloads for resumability.</summary>
        private const string DownloadManifestFile = "_download_manifest.json";

        /// <summary>
        /// Detects if a file is a Git LFS pointer stub (small text file pointing to the real binary).
        /// LFS stubs start with "version https://git-lfs.github.com".
        /// </summary>
        private static bool IsLfsPointerStub(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > 1024) return false; // LFS stubs are always small (<1KB)
                var content = File.ReadAllText(filePath);
                return content.StartsWith("version https://git-lfs.github.com", StringComparison.Ordinal);
            }
            catch { return false; }
        }

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
            var lfsFiles = new[] { "model.onnx", "model.onnx_data" };
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

            // Load download manifest for resumability
            var manifestPath = Path.Combine(destinationDir, DownloadManifestFile);
            var completedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifestJson = await File.ReadAllTextAsync(manifestPath, ct);
                    var manifest = JsonSerializer.Deserialize<DownloadManifest>(manifestJson);
                    if (manifest?.ModelId == modelId && manifest.CompletedFiles != null)
                    {
                        foreach (var f in manifest.CompletedFiles)
                            completedFiles.Add(f);
                    }
                }
                catch { /* corrupted manifest — restart download */ }
            }

            for (int i = 0; i < modelInfo.FileList.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var filename = modelInfo.FileList[i];
                var fileUrl = $"https://huggingface.co/{modelId}/resolve/main/{filename}";
                var destPath = Path.Combine(destinationDir, filename);

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                // Resume: skip files already recorded as complete in the manifest.
                // Without manifest confirmation, even large files could be partial downloads.
                if (completedFiles.Contains(filename))
                {
                    // Validate that the existing file isn't an LFS pointer stub
                    if (File.Exists(destPath) && IsLfsPointerStub(destPath))
                    {
                        // LFS stub — delete and re-download
                        System.Diagnostics.Debug.WriteLine($"Existing file {filename} is LFS stub, re-downloading...");
                        try { File.Delete(destPath); } catch { }
                        completedFiles.Remove(filename);
                        // Fall through to normal download below
                    }
                    else
                    {
                        var existingSize = File.Exists(destPath) ? new FileInfo(destPath).Length : 0;
                        totalBytesDownloaded += existingSize;
                        progress.FilesCompleted = i + 1;
                        progress.BytesDownloaded = totalBytesDownloaded;
                        progress.CurrentFile = $"Resumed: {filename}";
                        onProgress?.Invoke(progress);
                        continue;
                    }
                }

                // Clean up any partial/stub file from a prior interrupted download
                if (File.Exists(destPath))
                {
                    try { File.Delete(destPath); } catch { }
                }

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
                    long lastProgressReport = 0;
                    const long progressInterval = 100_000; // Report progress every ~100KB
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                        fileBytesDownloaded += bytesRead;
                        totalBytesDownloaded += bytesRead;

                        // Throttle progress updates to avoid flooding UI thread
                        if (totalBytesDownloaded - lastProgressReport >= progressInterval || fileBytesDownloaded == totalBytes)
                        {
                            lastProgressReport = totalBytesDownloaded;
                            progress.BytesDownloaded = totalBytesDownloaded;
                            progress.TotalBytes = totalBytes > 0 ? totalBytesDownloaded + (modelInfo.FileList.Count - i - 1) * totalBytes : 0;
                            onProgress?.Invoke(progress);
                        }
                    }

                    // Final progress for this file
                    progress.BytesDownloaded = totalBytesDownloaded;
                    progress.TotalBytes = totalBytes > 0 ? totalBytesDownloaded + (modelInfo.FileList.Count - i - 1) * totalBytes : 0;
                    onProgress?.Invoke(progress);

                    await fileStream.FlushAsync(ct);

                    // Validate: if we got an LFS pointer stub instead of the real file,
                    // retry using the raw URL (which forces LFS resolution)
                    if (IsLfsPointerStub(destPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"LFS stub detected for {filename}, retrying with raw URL...");
                        try { File.Delete(destPath); } catch { }

                        // Retry with the raw content URL (bypasses LFS pointer caching)
                        var rawUrl = $"https://huggingface.co/{modelId}/raw/main/{filename}";
                        using var retryResponse = await _downloadClient.GetAsync(rawUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                        retryResponse.EnsureSuccessStatusCode();

                        await using var retryStream = await retryResponse.Content.ReadAsStreamAsync(ct);
                        await using var retryFileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                        await retryStream.CopyToAsync(retryFileStream, ct);
                        await retryFileStream.FlushAsync(ct);

                        // If it's STILL a stub, give up on this file
                        if (IsLfsPointerStub(destPath))
                        {
                            try { File.Delete(destPath); } catch { }
                            throw new Exception($"File {filename} could not be resolved from Git LFS. The model may require HuggingFace authentication.");
                        }
                    }

                    // Record this file as complete in the download manifest
                    completedFiles.Add(filename);
                    await SaveDownloadManifestAsync(manifestPath, modelId, completedFiles);
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

            var criticalExtensions = new[] { ".onnx", ".data", ".onnx_data" };
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
        /// Searches recursively because some models (e.g., onnx-community) nest
        /// the actual model files inside cpu_and_mobile/ or gpu/ subdirectories.
        /// Also validates that .onnx files are not LFS pointer stubs.
        /// </summary>
        public static bool IsValidModelDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return false;

            // If a manifest exists, trust it: only valid if all files are recorded complete
            var manifestPath = Path.Combine(directoryPath, DownloadManifestFile);
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifestJson = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<DownloadManifest>(manifestJson);
                    if (manifest?.CompletedFiles != null && manifest.CompletedFiles.Count > 0)
                    {
                        // Model is valid if the manifest exists and at least the critical files are present
                        return HasOnnxModelFilesInManifest(directoryPath, manifest);
                    }
                }
                catch { /* corrupted manifest — fall through to file-based check */ }
            }

            // No manifest: fall back to file-based validation (legacy/manual models)
            if (HasOnnxModelFiles(directoryPath)) return true;

            // Search one level deep for model subdirectories (cpu_and_mobile/*, gpu/*, etc.)
            foreach (var subDir in Directory.GetDirectories(directoryPath))
            {
                if (HasOnnxModelFiles(subDir)) return true;

                foreach (var nestedDir in Directory.GetDirectories(subDir))
                {
                    if (HasOnnxModelFiles(nestedDir)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the actual directory containing genai_config.json and model.onnx,
        /// handling nested structures from onnx-community model repositories.
        /// Returns the directory path, or null if not found.
        /// </summary>
        public static string? FindModelDirectory(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory)) return null;

            // Check top-level first
            if (HasOnnxModelFiles(baseDirectory)) return baseDirectory;

            // Search for nested model directories
            foreach (var subDir in Directory.GetDirectories(baseDirectory))
            {
                if (HasOnnxModelFiles(subDir)) return subDir;

                foreach (var nestedDir in Directory.GetDirectories(subDir))
                {
                    if (HasOnnxModelFiles(nestedDir)) return nestedDir;
                }
            }

            return null;
        }

        private static bool HasOnnxModelFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return false;

            var hasGenaiConfig = File.Exists(Path.Combine(directoryPath, GenaiConfigFile));

            if (!hasGenaiConfig) return false;

            // Check for self-contained .onnx files (model weights in the .onnx itself)
            var onnxFiles = Directory.GetFiles(directoryPath, "*.onnx", SearchOption.TopDirectoryOnly);
            if (onnxFiles.Any(f => !IsLfsPointerStub(f) && new FileInfo(f).Length >= MinOnnxModelBytes))
                return true;

            // Check for external-data format: small .onnx header + large .onnx_data file
            // (common with llmware/ models that split weights into a separate data file)
            var onnxDataFiles = Directory.GetFiles(directoryPath, "*.onnx_data", SearchOption.TopDirectoryOnly);
            return onnxDataFiles.Any(f => !IsLfsPointerStub(f) && new FileInfo(f).Length >= MinOnnxModelBytes);
        }

        /// <summary>
        /// Validates a model using the download manifest: checks that critical ONNX files
        /// listed in the manifest actually exist on disk with expected sizes.
        /// </summary>
        private static bool HasOnnxModelFilesInManifest(string directoryPath, DownloadManifest manifest)
        {
            if (!File.Exists(Path.Combine(directoryPath, GenaiConfigFile)))
                return false;

            var criticalExtensions = new[] { ".onnx", ".onnx_data" };
            foreach (var file in manifest.CompletedFiles)
            {
                if (criticalExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var path = Path.Combine(directoryPath, file);
                    if (File.Exists(path) && new FileInfo(path).Length >= MinOnnxModelBytes)
                        return true;
                }
            }
            return false;
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

        /// <summary>
        /// Persists the download manifest so interrupted downloads can resume.
        /// </summary>
        private static async Task SaveDownloadManifestAsync(string manifestPath, string modelId, HashSet<string> completedFiles)
        {
            var manifest = new DownloadManifest
            {
                ModelId = modelId,
                CompletedFiles = completedFiles.ToList(),
                LastUpdated = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(manifest);
            await File.WriteAllTextAsync(manifestPath, json);
        }

        /// <summary>
        /// Scans all known model directories for incomplete downloads that have a manifest
        /// but aren't yet valid complete models. Returns tuples of (modelId, directoryPath).
        /// </summary>
        public static List<IncompleteDownload> GetIncompleteDownloads()
        {
            var results = new List<IncompleteDownload>();

            // Scan both ONNX and GGUF model roots
            var modelRoots = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Verdict", "Models"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "models")
            };

            foreach (var root in modelRoots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var dir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(dir, DownloadManifestFile);
                    if (!File.Exists(manifestPath)) continue;

                    // Skip already-valid directories
                    if (IsValidModelDirectory(dir)) continue;

                    // Try to read the manifest to get the modelId
                    string? modelId = null;
                    int completedCount = 0;
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<DownloadManifest>(json);
                        modelId = manifest?.ModelId;
                        completedCount = manifest?.CompletedFiles?.Count ?? 0;
                    }
                    catch { /* corrupted manifest */ }

                    if (!string.IsNullOrWhiteSpace(modelId) && modelId.Contains("/"))
                    {
                        results.Add(new IncompleteDownload
                        {
                            ModelId = modelId,
                            DirectoryPath = dir,
                            CompletedFileCount = completedCount
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Resumes all incomplete downloads found across model directories.
        /// Fires and forgets — reports completion via the provided callback.
        /// </summary>
        public static async Task ResumeAllIncompleteDownloadsAsync(Action<string, bool>? onComplete = null)
        {
            var incomplete = GetIncompleteDownloads();
            if (incomplete.Count == 0) return;

            var service = new ModelDownloadService();

            foreach (var download in incomplete)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Auto-Resume] Resuming download: {download.ModelId} ({download.CompletedFileCount} files already done)");
                    await service.DownloadModelAsync(download.ModelId, download.DirectoryPath);
                    onComplete?.Invoke(download.ModelId, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Auto-Resume] Failed to resume {download.ModelId}: {ex.Message}");
                    onComplete?.Invoke(download.ModelId, false);
                }
            }
        }
    }

    /// <summary>
    /// Represents an interrupted/incomplete model download that can be resumed.
    /// </summary>
    public class IncompleteDownload
    {
        public string ModelId { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public int CompletedFileCount { get; set; }
    }

    /// <summary>
    /// Tracks which files have been fully downloaded for a given model,
    /// enabling interrupted downloads to resume without re-downloading completed files.
    /// </summary>
    internal class DownloadManifest
    {
        public string ModelId { get; set; } = string.Empty;
        public List<string> CompletedFiles { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}
