using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntimeGenAI;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Providers;

public class OnnxProvider : HuggingFaceModelBase
{
    private readonly ModelDownloadService _downloadService;

    public OnnxProvider()
    {
        _downloadService = new ModelDownloadService();
    }

    public OnnxProvider(ModelDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public override string ProviderName => "ONNX";
    public override string Description => "Run LLM models locally using ONNX Runtime. Search and download models from HuggingFace, or use local ONNX model files.";
    public override string DefaultFriendlyName => "Local ONNX Model";

    public override List<ProviderField> ConfigFields => new()
    {
        new ProviderField
        {
            Key = "ModelId",
            Label = "Model Path or HF Model ID",
            Description = "e.g., llmware/llama-3.2-1b-instruct-onnx or C:\\Models\\my-model",
            DefaultValue = ""
        },
        new ProviderField
        {
            Key = "Endpoint",
            Label = "Models Root Directory",
            Description = "Directory where downloaded models are stored",
            DefaultValue = ""
        }
    };

    protected override string CacheFileName => "onnx_model_cache.json";
    protected override string DefaultModelsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Verdict", "Models");
    protected override string[] SearchQueries => ["onnx instruct"];
    protected override string[] ModelFilePatterns => ["*.onnx", "*.json"];

    protected override List<string> FindLocalModels(string modelsRoot)
    {
        return ModelDownloadService.ScanForModels(modelsRoot)
            .Select(m => $"[LOCAL] {m}")
            .ToList();
    }

    protected override string FormatRemoteModelEntry(string modelId, string name, long downloads, long sizeOnDisk)
    {
        string sizeStr = sizeOnDisk > 0 ? $"({FormatSize(sizeOnDisk)})" : "";
        return $"{name} {sizeStr} - {modelId}";
    }

    protected override async Task DownloadModelFilesAsync(string modelId, string destinationDir, IProgress<DownloadProgressInfo>? progress, CancellationToken ct = default)
    {
        await _downloadService.DownloadModelAsync(modelId, destinationDir, info => progress?.Report(info));
    }

    private async Task<string> ResolveModelPathAsync(AIModelConfiguration config)
    {
        string modelId = config.ModelId.Trim();

        // If it's a HuggingFace model ID (contains "/"), download it first
        if (modelId.Contains("/"))
        {
            string modelsRoot = ResolveModelsRoot(config, DefaultModelsRoot);
            string modelDir = Path.Combine(modelsRoot, SanitizeFolderName(modelId.Split('/').Last()));

            // Check if model directory already exists and is valid
            if (!ModelDownloadService.IsValidModelDirectory(modelDir))
            {
                // Download the model using the download service
                await _downloadService.DownloadModelAsync(modelId, modelDir, null);
            }

            return modelDir;
        }

        // If it's a local path, use it directly
        if (File.Exists(modelId))
        {
            // If it's a file path, get the directory containing the model
            string modelDir = Path.GetDirectoryName(modelId);
            if (string.IsNullOrEmpty(modelDir))
                return modelId;

            // Ensure the directory exists
            if (!Directory.Exists(modelDir))
                throw new DirectoryNotFoundException($"Model directory not found: {modelDir}");

            return modelDir;
        }

        // If it's a directory path, use it directly
        if (Directory.Exists(modelId))
        {
            return modelId;
        }

        throw new FileNotFoundException($"Model path not found: {modelId}");
    }

    public override async Task<string> GenerateResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.ModelId))
            throw new ArgumentException("ModelId is required. Set it to a local model path or a HuggingFace model ID.");

        try
        {
            return await GenerateOnnxResponseAsync(config, systemPrompt, userPrompt, ct);
        }
        catch (DllNotFoundException ex)
        {
            return $"Error: ONNX Runtime native library not found ({ex.Message}). Ensure Microsoft.ML.OnnxRuntimeGenAI.DirectML or CPU package is installed.";
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("OnnxRuntime"))
        {
            return $"Error: ONNX Runtime binary not found ({ex.Message}). Ensure the native runtime package is installed.";
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    private async Task<string> GenerateOnnxResponseAsync(AIModelConfiguration config, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        string modelPath = await ResolveModelPathAsync(config);

        if (!Directory.Exists(modelPath))
            throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");

        using var model = new Model(modelPath);
        using var tokenizer = new Tokenizer(model);
        using var generatorParams = new GeneratorParams(model);

        generatorParams.SetSearchOption("max_length", config.GetMaxTokens(2048));
        generatorParams.SetSearchOption("temperature", config.GetTemperature());

        string fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? $"<|user|>{userPrompt}<|assistant|>"
            : $"<|system|>{systemPrompt}<|user|>{userPrompt}<|assistant|>";

        using var generator = new Generator(model, generatorParams);
        var sequences = tokenizer.Encode(fullPrompt);
        generator.AppendTokenSequences(sequences);

        while (!generator.IsDone())
            generator.GenerateNextToken();

        var outputSequence = generator.GetSequence(0);
        var response = tokenizer.Decode(outputSequence);

        return response?.Trim() ?? string.Empty;
    }

    public override async Task<string> TestConnectionAsync(AIModelConfiguration config, CancellationToken ct = default)
    {
        if (config == null) return "Error: Configuration is null.";
        if (string.IsNullOrWhiteSpace(config.ModelId))
            return "Error: Model path is required. Enter a local path or HuggingFace model ID.";

        try
        {
            return await TestOnnxConnectionAsync(config, ct);
        }
        catch (DllNotFoundException ex)
        {
            return $"Error: ONNX Runtime native library not found ({ex.Message}). Ensure Microsoft.ML.OnnxRuntimeGenAI.DirectML or CPU package is installed.";
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("OnnxRuntime"))
        {
            return $"Error: ONNX Runtime binary not found ({ex.Message}). Ensure the native runtime package is installed.";
        }
        catch (Exception ex)
        {
            return $"Error testing connection: {ex.Message}";
        }
    }

    private async Task<string> TestOnnxConnectionAsync(AIModelConfiguration config, CancellationToken ct)
    {
        try
        {
            string modelPath = await ResolveModelPathAsync(config);

            if (!Directory.Exists(modelPath))
                return $"Error: Model directory not found: {modelPath}";

            Log($"Testing ONNX model at: {modelPath}");

            // Check for required files
            var onnxFiles = Directory.GetFiles(modelPath, "*.onnx", SearchOption.TopDirectoryOnly);
            if (onnxFiles.Length == 0)
                return $"Error: No .onnx files found in model directory: {modelPath}";

            var genaiConfigPath = Path.Combine(modelPath, "genai_config.json");
            if (!File.Exists(genaiConfigPath))
            {
                Log("No genai_config.json found, creating default one");
                var defaultConfig = new
                {
                    model_type = "onnx",
                    architectures = new[] { "OnnxModel" },
                    max_position_embeddings = 2048
                };
                var configJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(genaiConfigPath, configJson, ct);
            }

            // Try to load the model
            Log("Attempting to load ONNX model...");
            using var model = new Model(modelPath);
            Log("Model loaded successfully");

            // Try to create tokenizer
            Log("Attempting to create tokenizer...");
            using var tokenizer = new Tokenizer(model);
            Log("Tokenizer created successfully");

            // Try to generate a response
            Log("Attempting to generate response...");
            using var generatorParams = new GeneratorParams(model);
            generatorParams.SetSearchOption("max_length", 50);
            generatorParams.SetSearchOption("temperature", 0.1);

            using var generator = new Generator(model, generatorParams);
            var sequences = tokenizer.Encode("Say exactly the word 'Connected' and nothing else.");
            generator.AppendTokenSequences(sequences);

            while (!generator.IsDone())
                generator.GenerateNextToken();

            var outputSequence = generator.GetSequence(0);
            var response = tokenizer.Decode(outputSequence);

            if (string.IsNullOrEmpty(response))
                return "Error: Empty response from model";

            response = response.Trim();
            Log($"Model response: '{response}'");

            if (response.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                return "Connected";
            else
                return $"Model loaded but unexpected response: '{response}'";
        }
        catch (DllNotFoundException ex)
        {
            return $"Error: ONNX Runtime native library not found ({ex.Message}). Ensure Microsoft.ML.OnnxRuntimeGenAI.DirectML or CPU package is installed.";
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("OnnxRuntime"))
        {
            return $"Error: ONNX Runtime binary not found ({ex.Message}). Ensure the native runtime package is installed.";
        }
        catch (Exception ex)
        {
            Log($"Error testing ONNX connection: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            return $"Error testing connection: {ex.Message}";
        }
    }

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[ONNX Test] {message}");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
