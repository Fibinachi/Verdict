using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Providers;

public class HuggingFaceProvider : HuggingFaceModelBase
{
    public override string ProviderName => "Hugging Face";
    public override string Description => "Download and run GGUF models from HuggingFace locally using LLamaSharp. No API key needed.";
    public override string DefaultFriendlyName => "HF TinyLlama (CPU)";

    public override List<ProviderField> ConfigFields => new()
    {
        new ProviderField
        {
            Key = "ModelId",
            Label = "HuggingFace Model ID",
            Description = "e.g., TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF",
            DefaultValue = "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF"
        },
        new ProviderField
        {
            Key = "Endpoint",
            Label = "Models Directory",
            Description = "Directory where downloaded models are stored (default: C:\\Users\\<user>\\models)",
            DefaultValue = ""
        }
    };

    protected override string CacheFileName => "huggingface_model_cache.json";
    protected override string DefaultModelsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "models");
    protected override string[] SearchQueries => ["gguf", "GGUF", "llm gguf"];
    protected override string[] ModelFilePatterns => ["*.gguf"];

    protected override List<string> FindLocalModels(string modelsRoot)
    {
        var results = new List<string>();
        foreach (var dir in Directory.GetDirectories(modelsRoot))
        {
            var ggufFiles = Directory.GetFiles(dir, "*.gguf", SearchOption.TopDirectoryOnly);
            if (ggufFiles.Length > 0)
            {
                var dirName = Path.GetFileName(dir);
                var fileSize = new FileInfo(ggufFiles[0]).Length;
                results.Add($"[LOCAL] {dirName} ({FormatSize(fileSize)})");
            }
        }
        return results;
    }

    protected override string FormatRemoteModelEntry(string modelId, string name, long downloads, long sizeOnDisk)
    {
        string downloadsStr = downloads > 0 ? $"({FormatNumber(downloads)} downloads)" : "";
        return $"{name} {downloadsStr} - {modelId}";
    }

    protected override async Task DownloadModelFilesAsync(string modelId, string destinationDir, IProgress<DownloadProgressInfo>? progress, CancellationToken ct = default)
    {
        var apiUrl = $"https://huggingface.co/api/models/{modelId}";
        var response = await _httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var ggufFiles = new List<string>();
        long totalSize = 0;

        if (doc.RootElement.TryGetProperty("siblings", out var siblings))
        {
            foreach (var sibling in siblings.EnumerateArray())
            {
                var filename = sibling.TryGetProperty("rfilename", out var rf) ? rf.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(filename)) continue;

                if (filename.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                {
                    ggufFiles.Add(filename);
                    if (sibling.TryGetProperty("size", out var size))
                        totalSize += size.GetInt64();
                }
            }
        }

        if (ggufFiles.Count == 0)
            throw new Exception($"No GGUF files found for model {modelId}. Make sure the repository contains GGUF format files.");

        Directory.CreateDirectory(destinationDir);

        var progressInfo = new DownloadProgressInfo
        {
            ModelId = modelId,
            TotalFiles = ggufFiles.Count,
            TotalBytes = totalSize
        };

        long totalBytesDownloaded = 0;

        for (int i = 0; i < ggufFiles.Count; i++)
        {
            var filename = ggufFiles[i];
            var fileUrl = $"https://huggingface.co/{modelId}/resolve/main/{filename}";
            var destPath = Path.Combine(destinationDir, filename);

            progressInfo.CurrentFile = filename;
            progressInfo.FilesCompleted = i;
            progress?.Report(progressInfo);

            try
            {
                using var fileResponse = await _downloadClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                fileResponse.EnsureSuccessStatusCode();

                await using var contentStream = await fileResponse.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long fileBytesDownloaded = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    fileBytesDownloaded += bytesRead;
                    totalBytesDownloaded += bytesRead;

                    progressInfo.BytesDownloaded = totalBytesDownloaded;
                    progressInfo.TotalBytes = totalSize > 0 ? totalSize : totalBytesDownloaded;
                    progress?.Report(progressInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading {filename}: {ex.Message}");
                throw new Exception($"Failed to download {filename}: {ex.Message}");
            }
        }

        progressInfo.FilesCompleted = ggufFiles.Count;
        progressInfo.CurrentFile = "Complete";
        progress?.Report(progressInfo);
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {suffixes[order]}";
    }

    private static string FormatNumber(long number)
    {
        if (number >= 1_000_000_000) return $"{number / 1_000_000_000.0:F1}B";
        if (number >= 1_000_000) return $"{number / 1_000_000.0:F1}M";
        if (number >= 1_000) return $"{number / 1_000.0:F1}K";
        return number.ToString();
    }

    /// <summary>
    /// Builds a chat prompt using the appropriate template format for the given model.
    /// The template is inferred from the model name since GGUF metadata isn't available
    /// without first loading the model into memory.
    /// </summary>
    private string BuildChatPrompt(string modelId, string systemPrompt, string userPrompt)
    {
        string modelName = modelId.ToLowerInvariant();

        // Llama 2 / CodeLlama format: [INST] {prompt} [/INST]
        if (modelName.Contains("llama2") || modelName.Contains("codellama"))
        {
            return string.IsNullOrWhiteSpace(systemPrompt)
                ? $"[INST] {userPrompt} [/INST]"
                : $"<<SYS>>\n{systemPrompt}\n<</SYS>>\n\n[INST] {userPrompt} [/INST]";
        }

        // Llama 3 format: <|begin_of_text|><|start_header_id|>user<|end_header_id|>\n{prompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n
        if (modelName.Contains("llama3") || modelName.Contains("llama-3"))
        {
            var prompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? $"<|begin_of_text|><|start_header_id|>user<|end_header_id|>\n{userPrompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n"
                : $"<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n{systemPrompt}<|eot_id|><|start_header_id|>user<|end_header_id|>\n{userPrompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n";
            return prompt;
        }

        // TinyLlama, Zephyr, Mistral, Mixtral (ChatML format): <|im_start|>user\n{prompt}<|im_end|>\n<|im_start|>assistant\n
        if (modelName.Contains("tinyllama") || modelName.Contains("zephyr") || modelName.Contains("mistral") || modelName.Contains("mixtral"))
        {
            var prompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? $"<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n"
                : $"<|im_start|>system\n{systemPrompt}<|im_end|>\n<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n";
            return prompt;
        }

        // Default fallback: ChatML format (works with most modern instruction-tuned models)
        var defaultPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? $"<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n"
            : $"<|im_start|>system\n{systemPrompt}<|im_end|>\n<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n";
        return defaultPrompt;
    }

    private async Task<string> ResolveModelPathAsync(AIModelConfiguration config, IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string modelId = config.ModelId.Trim();

        if (modelId.Contains("/"))
        {
            string modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);
            string repoName = modelId.Split('/').Last();
            string modelDir = Path.Combine(modelsRoot, SanitizeFolderName(repoName));

            if (Directory.Exists(modelDir))
            {
                var ggufFiles = Directory.GetFiles(modelDir, "*.gguf", SearchOption.TopDirectoryOnly);
                if (ggufFiles.Length > 0)
                    return ggufFiles[0];
            }

            await DownloadModelFilesAsync(modelId, modelDir, progress, ct);

            var downloadedFiles = Directory.GetFiles(modelDir, "*.gguf", SearchOption.TopDirectoryOnly);
            if (downloadedFiles.Length == 0)
                throw new Exception($"No GGUF files were downloaded for model {modelId}");

            return downloadedFiles[0];
        }

        if (File.Exists(modelId))
            return modelId;

        if (Directory.Exists(modelId))
        {
            var ggufFiles = Directory.GetFiles(modelId, "*.gguf", SearchOption.TopDirectoryOnly);
            if (ggufFiles.Length > 0)
                return ggufFiles[0];
        }

        throw new FileNotFoundException($"Model not found: {modelId}. Specify a HuggingFace repo ID (e.g., TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF) or a local path to a GGUF file.");
    }

    public override async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.ModelId))
            throw new ArgumentException("ModelId is required. Enter a HuggingFace repo ID (e.g., TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF).");

        try
        {
            string modelPath = await ResolveModelPathAsync(config, ct: ct);

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"GGUF model file not found: {modelPath}");

            var modelParams = new ModelParams(modelPath)
            {
                ContextSize = (uint)config.GetMaxTokens(2048),
                GpuLayerCount = 0,
                Threads = Environment.ProcessorCount
            };

            using var model = LLamaWeights.LoadFromFile(modelParams);
            using var context = model.CreateContext(modelParams);
            var executor = new InteractiveExecutor(context);

            // Determine the chat template format based on the model name.
            // Since we can't easily query the GGUF model's built-in chat template at runtime
            // without loading the model first, we infer the format from common model naming conventions.
            // This covers the most widely-used GGUF chat formats.
            string fullPrompt = BuildChatPrompt(config.ModelId, systemPrompt, userPrompt);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = config.GetMaxTokens(1024),
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = (float)config.GetTemperature(0.7f)
                },
                AntiPrompts = new List<string> { "[INST]", "<<SYS>>", "<|im_end|>", "<|eot_id|>", "User:", "</s>" }
            };

            var response = new System.Text.StringBuilder();
            await foreach (var text in executor.InferAsync(fullPrompt, inferenceParams, ct))
            {
                response.Append(text);
            }

            return response.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    public override async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        if (config == null) return "Error: Configuration is null.";
        if (string.IsNullOrWhiteSpace(config.ModelId))
            return "Error: Model ID is required. Enter a HuggingFace repo ID.";

        try
        {
            string modelPath = await ResolveModelPathAsync(config, ct: ct);

            if (!File.Exists(modelPath))
                return $"Error: Model file not found: {modelPath}";

            var modelParams = new ModelParams(modelPath)
            {
                ContextSize = (uint)config.GetMaxTokens(512),
                GpuLayerCount = 0
            };

            using var model = LLamaWeights.LoadFromFile(modelParams);
            using var context = model.CreateContext(modelParams);

            return "Success: Model loaded successfully.";
        }
        catch (Exception ex)
        {
            return $"Error testing connection: {ex.Message}";
        }
    }
}
