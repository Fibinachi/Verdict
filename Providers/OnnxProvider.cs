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

    public override string ProviderName => "HF ONNX (GenAI)";
    public override string Description => "Downloads ONNX-format models from HuggingFace for local inference via OnnxRuntimeGenAI. Requires genai_config.json.";
    public override string DefaultFriendlyName => "Local ONNX Model";

    public override List<ProviderField> ConfigFields => new()
    {
        new ProviderField
        {
            Key = "ModelId",
            Label = "HF ONNX Model ID",
            Description = "Recommended: 'llmware/llama-3.2-1b-instruct-onnx' (1.8GB) or 'onnx-community/Phi-4-mini-instruct-onnx' (2.5GB INT4). Also: local path to model directory.",
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

    /// <summary>
    /// ONNX-specific validation: uses ModelDownloadService.IsValidModelDirectory
    /// which checks for both .onnx files AND genai_config.json, plus validates
    /// that the .onnx files are self-contained or have their data files present.
    /// </summary>
    protected override bool IsModelDownloadComplete(string modelDir)
    {
        return ModelDownloadService.IsValidModelDirectory(modelDir);
    }

    protected override List<string> FindLocalModels(string modelsRoot)
    {
        var models = ModelDownloadService.ScanForModels(modelsRoot)
            .Select(m => $"[LOCAL] {m}")
            .ToList();

        // If no models found, add recommended models users can download
        if (models.Count == 0)
        {
            models.Add("--- Recommended ONNX GenAI Models (enter ID below) ---");
            models.Add("llmware/llama-3.2-1b-instruct-onnx (1.8 GB, fast)");
            models.Add("onnx-community/Phi-4-mini-instruct-onnx (2.5 GB INT4, strong)");
            models.Add("llmware/llama-3.2-3b-instruct-onnx (6 GB, balanced)");
            models.Add("onnx-community/DeepSeek-R1-Distill-Llama-8B-ONNX-DirectML-GenAI-INT4 (5 GB)");
            models.Add("onnx-community/Meta-Llama-3.1-8B-Instruct-ONNX-DirectML-GenAI-INT4 (5 GB)");
        }

        return models;
    }

    // Cache of model IDs known to have genai_config.json, to avoid repeated API calls
    private static readonly HashSet<string> _knownGenaiModels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _knownNonGenaiModels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether a HuggingFace model repo contains a genai_config.json file.
    /// Results are cached statically to avoid repeated API calls.
    /// </summary>
    private static async Task<bool> HasGenaiConfigAsync(string modelId)
    {
        if (_knownGenaiModels.Contains(modelId)) return true;
        if (_knownNonGenaiModels.Contains(modelId)) return false;

        try
        {
            var url = $"https://huggingface.co/api/models/{Uri.EscapeDataString(modelId)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("siblings", out var siblings))
            {
                foreach (var file in siblings.EnumerateArray())
                {
                    var rfilename = file.TryGetProperty("rfilename", out var rf) ? rf.GetString() : "";
                    if (rfilename == "genai_config.json" ||
                        rfilename.EndsWith("/genai_config.json", StringComparison.OrdinalIgnoreCase))
                    {
                        _knownGenaiModels.Add(modelId);
                        return true;
                    }
                }
            }
            _knownNonGenaiModels.Add(modelId);
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Overrides the base search to filter out HuggingFace models that lack genai_config.json,
    /// which is required for OnnxRuntimeGenAI compatibility.
    /// </summary>
    public override async Task<List<string>> GetAvailableModelsAsync(AIModelConfiguration config)
    {
        var models = await base.GetAvailableModelsAsync(config);

        // Filter remote entries (format: "name - modelId") — skip local [LOCAL] entries and separators
        var filtered = new List<string>();
        foreach (var entry in models)
        {
            // Keep local models, separators, and error messages
            if (entry.StartsWith("[LOCAL]") || entry.StartsWith("---") || entry.StartsWith("⚠") || entry.StartsWith("Error"))
            {
                filtered.Add(entry);
                continue;
            }

            // Extract modelId from "name - owner/modelId" format
            var lastSep = entry.LastIndexOf(" - ", StringComparison.Ordinal);
            if (lastSep < 0)
            {
                filtered.Add(entry); // Keep entries we can't parse
                continue;
            }

            var modelId = entry[(lastSep + 3)..].Trim();
            if (string.IsNullOrEmpty(modelId) || !modelId.Contains('/'))
            {
                filtered.Add(entry);
                continue;
            }

            // Check if this model has genai_config.json (required for ONNX GenAI)
            if (await HasGenaiConfigAsync(modelId))
                filtered.Add(entry);
            // else: silently skip — model lacks genai_config.json
        }

        return filtered;
    }

    protected override string FormatRemoteModelEntry(string modelId, string name, long downloads, long sizeOnDisk)
    {
        string sizeStr = sizeOnDisk > 0 ? $"({FormatSize(sizeOnDisk)})" : "";
        return $"{name} {sizeStr} - {modelId}";
    }

    protected override async Task DownloadModelFilesAsync(string modelId, string destinationDir, IProgress<DownloadProgressInfo>? progress, CancellationToken ct = default)
    {
        await _downloadService.DownloadModelAsync(modelId, destinationDir, info => progress?.Report(info), ct);
    }

    private async Task<string> ResolveModelPathAsync(AIModelConfiguration config, CancellationToken ct = default)
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
                // Download the model using the download service (resumes partial downloads)
                await _downloadService.DownloadModelAsync(modelId, modelDir, null, ct);
            }

            // Find the actual model subdirectory (handles nested onnx-community structure)
            var actualModelPath = ModelDownloadService.FindModelDirectory(modelDir);
            if (actualModelPath == null)
                throw new DirectoryNotFoundException($"No valid ONNX GenAI model found in: {modelDir}. The model may not be compatible with OnnxRuntimeGenAI. Try downloading a model from onnx-community or llmware on HuggingFace.");

            return actualModelPath;
        }

        // If it's a local path, resolve the actual model directory
        string resolvedPath = modelId;

        // If it's a file path, get the directory containing the model
        if (File.Exists(modelId))
        {
            resolvedPath = Path.GetDirectoryName(modelId) ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedPath))
                throw new DirectoryNotFoundException($"Cannot determine directory from file path: {modelId}");
        }

        // If it's a directory, try to find the actual model subdirectory within it
        if (Directory.Exists(resolvedPath))
        {
            var actualModelPath = ModelDownloadService.FindModelDirectory(resolvedPath);
            if (actualModelPath != null) return actualModelPath;

            throw new DirectoryNotFoundException(
                $"No valid ONNX GenAI model found in: {resolvedPath}. " +
                "A valid model directory must contain both a .onnx file and genai_config.json. " +
                "For onnx-community models, select the specific variant subdirectory (e.g., cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4). " +
                "Try downloading a compatible model like 'llmware/llama-3.2-1b-instruct-onnx' or 'onnx-community/Phi-4-mini-instruct-onnx'.");
        }

        throw new FileNotFoundException($"Model path not found: {modelId}. Enter a HuggingFace model ID (e.g., 'llmware/llama-3.2-1b-instruct-onnx') or a local directory containing ONNX model files.");
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
        string modelPath = await ResolveModelPathAsync(config, ct);

        if (!Directory.Exists(modelPath))
            throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");

        return await Task.Run(() =>
        {
            using var model = new Model(modelPath);
            using var tokenizer = new Tokenizer(model);
            using var generatorParams = new GeneratorParams(model);

            generatorParams.SetSearchOption("max_length", config.GetMaxTokens(2048));
            generatorParams.SetSearchOption("temperature", config.GetTemperature());

            // Build the prompt using the model's chat template
            string fullPrompt = BuildChatPrompt(modelPath, systemPrompt, userPrompt);

            using var generator = new Generator(model, generatorParams);
            var sequences = tokenizer.Encode(fullPrompt);
            generator.AppendTokenSequences(sequences);

            while (!generator.IsDone())
                generator.GenerateNextToken();

            var outputSequence = generator.GetSequence(0);
            var response = tokenizer.Decode(outputSequence);

            // Strip the input prompt from the response if the model echoes it
            if (!string.IsNullOrEmpty(response))
            {
                response = StripPromptFromResponse(fullPrompt, response);
            }

            return response?.Trim() ?? string.Empty;
        }, ct);
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
            string modelPath = await ResolveModelPathAsync(config, ct);

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
                    model = new
                    {
                        decoder = new
                        {
                            session_options = new
                            {
                                enable_cpu_mem_arena = false
                            }
                        }
                    }
                };
                var configJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(genaiConfigPath, configJson, ct);
            }

            // Try to load the model and generate a simple test response
            return await Task.Run(() =>
            {
                using var model = new Model(modelPath);
                Log("Model loaded successfully");

                using var tokenizer = new Tokenizer(model);
                Log("Tokenizer created successfully");

                using var generatorParams = new GeneratorParams(model);
                generatorParams.SetSearchOption("max_length", 50);
                generatorParams.SetSearchOption("temperature", 0.1);

                string testPrompt = BuildChatPrompt(modelPath, "", "Say exactly the word 'Connected' and nothing else.");

                using var generator = new Generator(model, generatorParams);
                var sequences = tokenizer.Encode(testPrompt);
                generator.AppendTokenSequences(sequences);

                while (!generator.IsDone())
                    generator.GenerateNextToken();

                var outputSequence = generator.GetSequence(0);
                var response = tokenizer.Decode(outputSequence);

                if (string.IsNullOrEmpty(response))
                    return "Error: Empty response from model";

                response = StripPromptFromResponse(testPrompt, response).Trim();
                Log($"Model response: '{response}'");

                if (response.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                    return $"Connected (Model: {onnxFiles[0].Split(Path.DirectorySeparatorChar).Last()})";

                return $"Model loaded but unexpected response: '{response}'";
            }, ct);
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

    /// <summary>
    /// Detects the model architecture from its config.json to use the correct chat template.
    /// </summary>
    private static string DetectModelArchitecture(string modelPath)
    {
        try
        {
            var configPath = Path.Combine(modelPath, "config.json");
            if (!File.Exists(configPath))
            {
                // Check parent directory
                configPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? modelPath, "config.json");
                if (!File.Exists(configPath)) return "generic";
            }

            var configJson = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            // Check model_type field
            if (root.TryGetProperty("model_type", out var modelType))
            {
                var type = modelType.GetString()?.ToLowerInvariant() ?? "";
                if (type.Contains("phi")) return "phi";
                if (type.Contains("llama")) return "llama3";
                if (type.Contains("mistral")) return "mistral";
                if (type.Contains("qwen")) return "qwen";
                if (type.Contains("gemma")) return "gemma";
            }

            // Check architectures field
            if (root.TryGetProperty("architectures", out var architectures) &&
                architectures.ValueKind == JsonValueKind.Array)
            {
                foreach (var arch in architectures.EnumerateArray())
                {
                    var archName = arch.GetString()?.ToLowerInvariant() ?? "";
                    if (archName.Contains("phi")) return "phi";
                    if (archName.Contains("llama")) return "llama3";
                    if (archName.Contains("mistral")) return "mistral";
                    if (archName.Contains("qwen")) return "qwen";
                    if (archName.Contains("gemma")) return "gemma";
                }
            }
        }
        catch { /* fall through to generic */ }

        return "generic";
    }

    /// <summary>
    /// Builds a chat prompt using the correct template for the detected model architecture.
    /// </summary>
    private static string BuildChatPrompt(string modelPath, string systemPrompt, string userPrompt)
    {
        var architecture = DetectModelArchitecture(modelPath);

        // Try to detect template from tokenizer_config.json first
        var template = LoadChatTemplate(modelPath);
        if (!string.IsNullOrEmpty(template))
        {
            return template
                .Replace("{system}", systemPrompt)
                .Replace("{user}", userPrompt);
        }

        // Fall back to architecture-specific templates
        bool hasSystem = !string.IsNullOrWhiteSpace(systemPrompt);

        return architecture switch
        {
            "phi" => hasSystem
                ? $"<|system|>{systemPrompt}<|end|>\n<|user|>{userPrompt}<|end|>\n<|assistant|>"
                : $"<|user|>{userPrompt}<|end|>\n<|assistant|>",

            "llama3" => hasSystem
                ? $"<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n{systemPrompt}<|eot_id|><|start_header_id|>user<|end_header_id|>\n{userPrompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n"
                : $"<|begin_of_text|><|start_header_id|>user<|end_header_id|>\n{userPrompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n",

            "mistral" => hasSystem
                ? $"<s>[INST] {systemPrompt}\n\n{userPrompt} [/INST]"
                : $"<s>[INST] {userPrompt} [/INST]",

            "gemma" => hasSystem
                ? $"<bos><start_of_turn>system\n{systemPrompt}<end_of_turn>\n<start_of_turn>user\n{userPrompt}<end_of_turn>\n<start_of_turn>model\n"
                : $"<bos><start_of_turn>user\n{userPrompt}<end_of_turn>\n<start_of_turn>model\n",

            // Generic: use the simple Phi-like format which works for many models
            _ => hasSystem
                ? $"<|system|>{systemPrompt}<|end|>\n<|user|>{userPrompt}<|end|>\n<|assistant|>"
                : $"<|user|>{userPrompt}<|end|>\n<|assistant|>",
        };
    }

    /// <summary>
    /// Attempts to load the chat_template from tokenizer_config.json.
    /// </summary>
    private static string? LoadChatTemplate(string modelPath)
    {
        try
        {
            var tokenizerConfigPath = Path.Combine(modelPath, "tokenizer_config.json");
            if (!File.Exists(tokenizerConfigPath))
            {
                // Check parent directory
                var parentDir = Path.GetDirectoryName(modelPath);
                if (parentDir != null)
                {
                    tokenizerConfigPath = Path.Combine(parentDir, "tokenizer_config.json");
                    if (!File.Exists(tokenizerConfigPath)) return null;
                }
                else return null;
            }

            var configJson = File.ReadAllText(tokenizerConfigPath);
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("chat_template", out var ct) &&
                ct.ValueKind == JsonValueKind.String)
            {
                var template = ct.GetString();
                if (!string.IsNullOrEmpty(template) && template.Contains("{user"))
                    return template;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OnnxProvider] Failed to read chat template from tokenizer_config.json: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Strips the input prompt prefix from the response if the model echoed it back.
    /// </summary>
    private static string StripPromptFromResponse(string prompt, string response)
    {
        if (string.IsNullOrEmpty(response)) return response;

        // Try to find where the assistant response starts
        string[] markers = [
            "<|assistant|>",
            "<|start_header_id|>assistant<|end_header_id|>",
            "[/INST]",
            "<start_of_turn>model\n",
            "assistant\n",
        ];

        foreach (var marker in markers)
        {
            int idx = response.LastIndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var afterMarker = response.Substring(idx + marker.Length).Trim();
                if (!string.IsNullOrEmpty(afterMarker))
                    return afterMarker;
            }
        }

        // If the response contains the prompt, try to remove it
        if (response.StartsWith(prompt.Trim(), StringComparison.Ordinal))
            return response.Substring(prompt.Trim().Length).Trim();

        return response;
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
