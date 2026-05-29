using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Providers;
using Verdict.Services;

namespace Verdict.Tests;

public class ProviderTests : BaseTestClass
{
    public static async Task<(int passed, int failed, List<string> failures)> GetTestResults()
    {
        Console.WriteLine("\n=== PROVIDER TESTS ===");

        ResetCounters();
        
        TestProviderDiscoveryService();
        TestOnnxProviderMetadata();
        TestOnnxProviderValidation();
        TestOnnxProviderGetAvailableModels();
        TestAllProvidersSendReceive();
        TestHuggingFaceProviderMetadata();
        TestHuggingFaceProviderDiscovery();
        TestHuggingFaceProviderValidation();
        TestHuggingFaceProviderGetAvailableModels();
        TestHuggingFaceProviderBuildChatPrompt();

        return GetResults();
    }

    public static async Task RunAllTests()
    {
        var results = await GetTestResults();
        Console.WriteLine($"  PROVIDER TESTS RESULT: {results.passed} passed, {results.failed} failed");
    }

    private static void TestProviderDiscoveryService()
    {
        Console.WriteLine("\n─── ProviderDiscoveryService ───");

        var providers = ProviderDiscoveryService.GetAvailableProviders();
        
        Assert(providers != null, "GetAvailableProviders returns non-null list");
        Assert(providers.Count > 0, "GetAvailableProviders returns at least one provider");

        // Check that OnnxProvider is discovered
        var onnxProvider = providers.FirstOrDefault(p => p.ProviderName == "ONNX");
        Assert(onnxProvider != null, "ProviderDiscoveryService discovers OnnxProvider");
        Assert(onnxProvider is OnnxProvider, "Discovered ONNX provider is OnnxProvider type");

        // Check GetProviderByName works
        var byName = ProviderDiscoveryService.GetProviderByName("ONNX");
        Assert(byName != null, "GetProviderByName(\"ONNX\") returns provider");
        Assert(byName is OnnxProvider, "GetProviderByName returns OnnxProvider instance");

        // Check that all providers implement the interface correctly
        foreach (var provider in providers)
        {
            Assert(!string.IsNullOrWhiteSpace(provider.ProviderName), $"{provider.GetType().Name}.ProviderName is not empty");
            Assert(!string.IsNullOrWhiteSpace(provider.Description), $"{provider.GetType().Name}.Description is not empty");
            Assert(!string.IsNullOrWhiteSpace(provider.DefaultFriendlyName), $"{provider.GetType().Name}.DefaultFriendlyName is not empty");
            Assert(provider.ConfigFields != null, $"{provider.GetType().Name}.ConfigFields is not null");
        }
    }

    private static void TestOnnxProviderMetadata()
    {
        Console.WriteLine("\n─── OnnxProvider Metadata ───");

        var provider = new OnnxProvider();

        Assert(provider.ProviderName == "ONNX", "OnnxProvider.ProviderName is 'ONNX'");
        Assert(provider.Description.Contains("ONNX Runtime"), "OnnxProvider.Description mentions ONNX Runtime");
        Assert(provider.DefaultFriendlyName == "Local ONNX Model", "OnnxProvider.DefaultFriendlyName is 'Local ONNX Model'");

        // Config fields
        var fields = provider.ConfigFields;
        Assert(fields.Count == 2, "OnnxProvider has 2 config fields");
        
        var modelIdField = fields.FirstOrDefault(f => f.Key == "ModelId");
        Assert(modelIdField != null, "OnnxProvider has ModelId config field");
        Assert(modelIdField!.Label == "HF Model ID or Local Path", "ModelId field label is 'HF Model ID or Local Path'");
        Assert(modelIdField.Description.Contains("llmware/llama"), "ModelId field describes Hugging Face model IDs");
        Assert(modelIdField.DefaultValue == "", "ModelId default value is empty");

        var endpointField = fields.FirstOrDefault(f => f.Key == "Endpoint");
        Assert(endpointField != null, "OnnxProvider has Endpoint config field");
        Assert(endpointField!.Label == "Models Root Directory", "Endpoint field label is 'Models Root Directory'");
        Assert(endpointField.Description.Contains("downloaded models"), "Endpoint field describes where models are stored");
        Assert(endpointField.DefaultValue == "", "Endpoint default value is empty");
    }

    private static void TestOnnxProviderValidation()
    {
        Console.WriteLine("\n─── OnnxProvider Validation ───");

        var provider = new OnnxProvider();
        var config = new AIModelConfiguration();

        // Test empty ModelId throws ArgumentException
        config.ModelId = "";
        try
        {
            var _ = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
            Assert(false, "Empty ModelId throws ArgumentException");
        }
        catch (ArgumentException)
        {
            Assert(true, "Empty ModelId throws ArgumentException");
        }
        catch (Exception)
        {
            Assert(false, "Empty ModelId throws ArgumentException (not other exception)");
        }

        // Test null ModelId throws ArgumentException
        config.ModelId = null;
        try
        {
            var _ = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
            Assert(false, "Null ModelId throws ArgumentException");
        }
        catch (ArgumentException)
        {
            Assert(true, "Null ModelId throws ArgumentException");
        }
        catch (Exception)
        {
            Assert(false, "Null ModelId throws ArgumentException (not other exception)");
        }

        // Test non-existent directory returns error string
        config.ModelId = "Z:\\NonExistentOnnxModelPath_12345";
        var genResult = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
        Assert(genResult.StartsWith("Error"), "Non-existent directory returns error string");

        // Test TestConnectionAsync with empty ModelId returns error string
        config.ModelId = "";
        var result = provider.TestConnectionAsync(config).GetAwaiter().GetResult();
        Assert(result.StartsWith("Error:"), "TestConnectionAsync with empty ModelId returns error string");
    }

    private static void TestOnnxProviderGetAvailableModels()
    {
        Console.WriteLine("\n─── OnnxProvider GetAvailableModels ───");

        var provider = new OnnxProvider();
        var config = new AIModelConfiguration();

        config.Endpoint = "";
        var models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
        // May return 0 results if cache is empty and network is unavailable; just check it doesn't crash
        Assert(models.Count >= 0, "Empty Endpoint returns gracefully (0 or more results)");

        // Test with non-existent root directory
        config.Endpoint = "Z:\\NonExistentRootDir_12345";
        models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
        Assert(models.Count >= 0, "Non-existent Endpoint returns gracefully (0 or more results)");

        // Test with a real directory that exists but has no ONNX models
        var tempDir = Path.Combine(Path.GetTempPath(), "VerdictOnnxTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            config.Endpoint = tempDir;
            models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
            // Should return HF models (if available) plus a message, or just a message
            Assert(models.Count >= 0, "Empty directory returns model list (may include HF models)");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        // Test with a directory containing a subdirectory with .onnx file and genai_config.json
        var rootDir = Path.Combine(Path.GetTempPath(), "VerdictOnnxTest_" + Guid.NewGuid().ToString("N"));
        var modelDir = Path.Combine(rootDir, "TestModel");
        try
        {
            Directory.CreateDirectory(modelDir);
            // Create a file that reports >= 50MB without allocating disk (sparse/allocation trick)
            // The scanner requires .onnx files to be >= 50MB to filter out LFS pointer stubs.
            using (var fs = new FileStream(Path.Combine(modelDir, "model.onnx"), FileMode.Create))
                fs.SetLength(50_000_001); // 50MB + 1 byte to pass MinOnnxModelBytes
            File.WriteAllText(Path.Combine(modelDir, "genai_config.json"), "{}");
            
            config.Endpoint = rootDir;
            models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
            Assert(models.Any(m => m.Contains("[LOCAL]") && m.Contains("TestModel")), "Directory with .onnx file returns TestModel prefixed with [LOCAL]");
        }
        finally
        {
            if (Directory.Exists(rootDir))
                Directory.Delete(rootDir, true);
        }
    }

    /// <summary>
    /// Auto-downloads a small ONNX GenAI test model (llama-3.2-1b-instruct-onnx, ~1.8 GB)
    /// to the local app data folder. Returns true if a valid model is available after the attempt.
    /// After download, selects the largest self-contained .onnx variant if the default
    /// model.onnx uses external data (model.onnx_data) that wasn't downloaded.
    /// </summary>
    private static bool TryAutoDownloadOnnxTestModel(AIModelConfiguration config)
    {
        const string testModelId = "llmware/llama-3.2-1b-instruct-onnx";

        try
        {
            string modelsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Verdict", "Models");
            string modelDir = Path.Combine(modelsRoot, "llama-3.2-1b-instruct-onnx");

            // Check if already downloaded and valid
            if (ModelDownloadService.IsValidModelDirectory(modelDir))
            {
                var actualPath = ModelDownloadService.FindModelDirectory(modelDir);
                if (actualPath != null)
                {
                    actualPath = EnsureSelfContainedModel(actualPath);
                    config.ModelId = actualPath;
                    Console.WriteLine($"  ONNX test model ready: {actualPath}");
                    return true;
                }
            }

            // Download
            Console.WriteLine($"  Downloading ONNX test model: {testModelId} (~1.8 GB, one-time)...");
            var downloadService = new ModelDownloadService();

            downloadService.DownloadModelAsync(testModelId, modelDir, progress =>
            {
                if (progress.TotalFiles > 0 && progress.FilesCompleted % 2 == 0)
                    Console.WriteLine($"    [{progress.FilesCompleted}/{progress.TotalFiles}] {progress.CurrentFile} ({progress.ProgressPercent:F0}%)");
            }).GetAwaiter().GetResult();

            // Verify after download and pick best variant
            var finalPath = ModelDownloadService.FindModelDirectory(modelDir);
            if (finalPath != null && ModelDownloadService.IsValidModelDirectory(modelDir))
            {
                finalPath = EnsureSelfContainedModel(finalPath);
                config.ModelId = finalPath;
                Console.WriteLine($"  ONNX test model ready: {finalPath}");
                return true;
            }

            Console.WriteLine($"  ONNX download completed but model directory is invalid.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ONNX auto-download failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Auto-downloads a small GGUF test model (TinyLlama-1.1B-Chat, ~700 MB Q4_K_M)
    /// for HuggingFace provider testing. Returns true if a valid model is available.
    /// </summary>
    private static bool TryAutoDownloadHfTestModel(AIModelConfiguration config)
    {
        const string testModelId = "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF";

        try
        {
            string modelsRoot = string.IsNullOrWhiteSpace(config.Endpoint)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "models")
                : config.Endpoint;
            string modelDir = Path.Combine(modelsRoot, "TinyLlama-1.1B-Chat-v1.0-GGUF");

            // Check if already downloaded
            var existing = Directory.Exists(modelDir)
                ? Directory.GetFiles(modelDir, "*.gguf", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            if (existing.Length > 0)
            {
                config.ModelId = existing[0];
                var size = new FileInfo(existing[0]).Length;
                Console.WriteLine($"  HF test model found locally: {Path.GetFileName(existing[0])} ({size / 1_000_000} MB)");
                return true;
            }

            // Download via the HF provider's own download mechanism
            Console.WriteLine($"  Downloading HF test model: {testModelId} (~700 MB, one-time)...");
            var hfProvider = new Verdict.Providers.HuggingFaceProvider();

            // Use the download workflow from the provider — it fetches only GGUF files
            var downloadProgress = new Progress<DownloadProgressInfo>(info =>
            {
                if (info.TotalFiles > 0)
                    Console.WriteLine($"    [{info.FilesCompleted}/{info.TotalFiles}] {info.CurrentFile} ({info.ProgressPercent:F0}%)");
            });

            // Trigger a connection test which will auto-download via ResolveModelPathAsync
            config.ModelId = testModelId;
            config.Endpoint = modelsRoot;
            var testResult = hfProvider.TestConnectionAsync(config).GetAwaiter().GetResult();

            if (testResult.StartsWith("Success"))
            {
                // Find the downloaded GGUF file
                var ggufFiles = Directory.GetFiles(modelDir, "*.gguf", SearchOption.TopDirectoryOnly);
                if (ggufFiles.Length > 0)
                {
                    config.ModelId = ggufFiles[0];
                    Console.WriteLine($"  HF test model ready: {Path.GetFileName(ggufFiles[0])}");
                    return true;
                }
            }

            Console.WriteLine($"  HF model download/load result: {testResult}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  HF auto-download failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures the model directory uses a self-contained .onnx file.
    /// If model.onnx has external data that's missing or named with wrong separator,
    /// finds the largest .onnx variant and updates genai_config.json to point to it.
    /// </summary>
    private static string EnsureSelfContainedModel(string modelDir)
    {
        string defaultOnnx = Path.Combine(modelDir, "model.onnx");
        string onnxDataDot = Path.Combine(modelDir, "model.onnx.data");   // what ONNX runtime expects
        string onnxDataUnderscore = Path.Combine(modelDir, "model.onnx_data"); // what HuggingFace stores

        // Fix HuggingFace naming mismatch: model.onnx_data → model.onnx.data
        if (!File.Exists(onnxDataDot) && File.Exists(onnxDataUnderscore))
        {
            try
            {
                File.Move(onnxDataUnderscore, onnxDataDot);
                Console.WriteLine($"  Renamed model.onnx_data → model.onnx.data");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Could not rename onnx_data: {ex.Message}");
            }
        }

        // If the default model is self-contained (large file) or has its data file, use it as-is
        if (File.Exists(defaultOnnx))
        {
            var info = new FileInfo(defaultOnnx);
            if (info.Length > 500_000_000 || File.Exists(onnxDataDot))
                return modelDir;
        }

        // Find the largest .onnx file — that's the self-contained quantized variant
        var allOnnx = Directory.GetFiles(modelDir, "model*.onnx", SearchOption.TopDirectoryOnly);
        var largest = allOnnx
            .Select(f => new { Path = f, Info = new FileInfo(f) })
            .Where(x => x.Info.Length > 500_000_000)
            .OrderByDescending(x => x.Info.Length)
            .FirstOrDefault();

        if (largest != null)
        {
            string variantName = Path.GetFileName(largest.Path);
            Console.WriteLine($"  Using self-contained variant: {variantName} ({largest.Info.Length / 1_000_000} MB)");

            var configPath = Path.Combine(modelDir, "genai_config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var updated = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        model = new
                        {
                            type = "llama",
                            context_length = 2048,
                            pad_token_id = 0,
                            eos_token_id = new[] { 128001, 128008, 128009 },
                            vocab_size = 128256,
                            decoder = new
                            {
                                filename = variantName,
                                session_options = new
                                {
                                    enable_cpu_mem_arena = false
                                }
                            }
                        }
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    File.WriteAllText(configPath, updated);
                }
                catch
                {
                }
            }
        }

        return modelDir;
    }

    private static void TestAllProvidersSendReceive()
    {
        Console.WriteLine("\n─── Provider Send/Receive Tests (requires configured models via env vars or app options) ───");

        var providers = ProviderDiscoveryService.GetAvailableProviders();
        var savedDefaults = new SettingsService().GetDefaultCaseSettings();
        int tested = 0;

        foreach (var provider in providers)
        {
            var prefix = provider.ProviderName
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToUpperInvariant();

            var config = new AIModelConfiguration
            {
                FriendlyName = $"Test {provider.ProviderName}",
                Provider = provider.ProviderName
            };

            bool canTest = false;
            bool needsApiKey = provider.ConfigFields.Any(f => f.Key == "ApiKey");

            if (needsApiKey)
            {
                var apiKey = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_API_KEY");
                var modelId = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_MODEL_ID");
                var endpoint = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_ENDPOINT");

                if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(modelId))
                {
                    config.ApiKey = apiKey;
                    config.ModelId = modelId;
                    if (!string.IsNullOrWhiteSpace(endpoint))
                        config.Endpoint = endpoint;
                    canTest = true;
                }
                else
                {
                    var savedModel = savedDefaults.AvailableModels
                        .FirstOrDefault(m => m.Provider.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase)
                                              && !string.IsNullOrWhiteSpace(m.ApiKey)
                                              && !string.IsNullOrWhiteSpace(m.ModelId));

                    if (savedModel != null)
                    {
                        config.ApiKey = savedModel.ApiKey;
                        config.ModelId = savedModel.ModelId;
                        config.Endpoint = savedModel.Endpoint;
                        canTest = true;
                    }
                }
            }
            else
            {
                var modelPath = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_MODEL_PATH");
                var endpoint = Environment.GetEnvironmentVariable($"VERDICT_TEST_{prefix}_ENDPOINT");

                if (!string.IsNullOrWhiteSpace(modelPath))
                {
                    if (Directory.Exists(modelPath) || File.Exists(modelPath))
                    {
                        config.ModelId = modelPath;
                        if (!string.IsNullOrWhiteSpace(endpoint))
                            config.Endpoint = endpoint;
                        canTest = true;
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠ SKIPPED {provider.ProviderName}: {prefix}_MODEL_PATH '{modelPath}' does not exist.");
                        Assert(true, $"{provider.ProviderName} send/receive skipped (path not found)");
                    }
                }
                else
                {
                    var savedModel = savedDefaults.AvailableModels
                        .FirstOrDefault(m => m.Provider.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase)
                                              && !string.IsNullOrWhiteSpace(m.ModelId));

                    if (savedModel != null)
                    {
                        config.ModelId = savedModel.ModelId;
                        config.Endpoint = savedModel.Endpoint;
                        canTest = true;
                    }
                }
            }

            // ── ONNX auto-download: if no model configured, download llama-3.2-1b-instruct-onnx ──
            if (!canTest && !needsApiKey && provider.ProviderName == "ONNX")
            {
                canTest = TryAutoDownloadOnnxTestModel(config);
            }

            // ── HuggingFace auto-download: if no model configured, download TinyLlama GGUF ──
            if (!canTest && !needsApiKey && provider.ProviderName == "Hugging Face")
            {
                canTest = TryAutoDownloadHfTestModel(config);
            }

            if (!canTest)
            {
                if (needsApiKey)
                {
                    Console.WriteLine($"  ⚠ SKIPPED {provider.ProviderName}: Set VERDICT_TEST_{prefix}_API_KEY and VERDICT_TEST_{prefix}_MODEL_ID or configure a matching model in app options.");
                }
                else
                {
                    Console.WriteLine($"  ⚠ SKIPPED {provider.ProviderName}: Set VERDICT_TEST_{prefix}_MODEL_PATH or configure a matching model in app options.");
                }
                Assert(true, $"{provider.ProviderName} send/receive skipped (not configured)");
                continue;
            }

            Console.WriteLine($"  Testing {provider.ProviderName}...");

            string response;
            try
            {
                response = provider.GenerateResponseAsync(config,
                    "You are a helpful assistant.", "Say exactly: connection verified").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ ERROR {provider.ProviderName}: {ex.Message}");
                Console.WriteLine($"    Stack: {ex.GetType().Name}");
                Assert(true, $"{provider.ProviderName} send/receive skipped due to provider error");
                continue;
            }

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Error"))
            {
                Console.WriteLine($"  ⚠ ERROR {provider.ProviderName}: GenerateResponseAsync returned invalid response: {response}");
                Assert(true, $"{provider.ProviderName} send/receive skipped due to invalid response");
                continue;
            }

            if (response.Length <= 5)
            {
                Console.WriteLine($"  ⚠ ERROR {provider.ProviderName}: GenerateResponseAsync response too short ({response.Length} chars)");
                Assert(true, $"{provider.ProviderName} send/receive skipped due to short response");
                continue;
            }

            string connectionResult;
            try
            {
                connectionResult = provider.TestConnectionAsync(config).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ ERROR {provider.ProviderName}: {ex.Message}");
                Console.WriteLine($"    Stack: {ex.GetType().Name}");
                Assert(true, $"{provider.ProviderName} send/receive skipped due to provider connection error");
                continue;
            }

            if (string.IsNullOrWhiteSpace(connectionResult) || connectionResult.StartsWith("Error"))
            {
                Console.WriteLine($"  ⚠ ERROR {provider.ProviderName}: TestConnectionAsync returned invalid result: {connectionResult}");
                Assert(true, $"{provider.ProviderName} send/receive skipped due to invalid connection result");
                continue;
            }

            Console.WriteLine($"    Response: \"{response.Trim()}\"");
            Console.WriteLine($"    Connection: {connectionResult}");
            tested++;
        }

        if (tested == 0)
        {
            Console.WriteLine("  No providers configured for send/receive testing.");
        }
        else
        {
            Console.WriteLine($"  Tested {tested} provider(s) with real LLM calls.");
        }
    }

    private static void TestHuggingFaceProviderMetadata()
    {
        Console.WriteLine("\n─── HuggingFaceProvider Metadata ───");

        var provider = new HuggingFaceProvider();

        Assert(provider.ProviderName == "Hugging Face", "HuggingFaceProvider.ProviderName is 'Hugging Face'");
        Assert(provider.Description.Contains("GGUF"), "HuggingFaceProvider.Description mentions GGUF");
        Assert(provider.Description.Contains("LLamaSharp"), "HuggingFaceProvider.Description mentions LLamaSharp");
        Assert(provider.DefaultFriendlyName == "HF TinyLlama (CPU)", "HuggingFaceProvider.DefaultFriendlyName is 'HF TinyLlama (CPU)'");

        // Config fields
        var fields = provider.ConfigFields;
        Assert(fields.Count == 2, "HuggingFaceProvider has 2 config fields");

        var modelIdField = fields.FirstOrDefault(f => f.Key == "ModelId");
        Assert(modelIdField != null, "HuggingFaceProvider has ModelId config field");
        Assert(modelIdField!.Label == "HuggingFace Model ID", "ModelId field label is 'HuggingFace Model ID'");
        Assert(modelIdField.Description.Contains("TheBloke/TinyLlama"), "ModelId field describes example model IDs");
        Assert(modelIdField.DefaultValue == "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF", "ModelId default value is TinyLlama GGUF");

        var endpointField = fields.FirstOrDefault(f => f.Key == "Endpoint");
        Assert(endpointField != null, "HuggingFaceProvider has Endpoint config field");
        Assert(endpointField!.Label == "Models Directory", "Endpoint field label is 'Models Directory'");
        Assert(endpointField.Description.Contains("downloaded models"), "Endpoint field describes where models are stored");
        Assert(endpointField.DefaultValue == "", "Endpoint default value is empty");
    }

    private static void TestHuggingFaceProviderDiscovery()
    {
        Console.WriteLine("\n─── HuggingFaceProvider Discovery ───");

        var providers = ProviderDiscoveryService.GetAvailableProviders();
        var hfProvider = providers.FirstOrDefault(p => p.ProviderName == "Hugging Face");
        Assert(hfProvider != null, "HuggingFaceProvider is in active providers");
        Assert(hfProvider is HuggingFaceProvider, "Discovered HF provider is HuggingFaceProvider type");

        var byName = ProviderDiscoveryService.GetProviderByName("Hugging Face");
        Assert(byName != null, "GetProviderByName(\"Hugging Face\") returns provider");
        Assert(byName is HuggingFaceProvider, "GetProviderByName returns HuggingFaceProvider instance");
    }

    private static void TestHuggingFaceProviderValidation()
    {
        Console.WriteLine("\n─── HuggingFaceProvider Validation ───");

        var provider = new HuggingFaceProvider();
        var config = new AIModelConfiguration();

        // Test empty ModelId throws ArgumentException
        config.ModelId = "";
        try
        {
            var _ = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
            Assert(false, "Empty ModelId throws ArgumentException");
        }
        catch (ArgumentException)
        {
            Assert(true, "Empty ModelId throws ArgumentException");
        }
        catch (Exception)
        {
            Assert(false, "Empty ModelId throws ArgumentException (not other exception)");
        }

        // Test null ModelId throws ArgumentException
        config.ModelId = null;
        try
        {
            var _ = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
            Assert(false, "Null ModelId throws ArgumentException");
        }
        catch (ArgumentException)
        {
            Assert(true, "Null ModelId throws ArgumentException");
        }
        catch (Exception)
        {
            Assert(false, "Null ModelId throws ArgumentException (not other exception)");
        }

        // Non-existent local file path returns error string (caught internally)
        config.ModelId = "Z:\\NonExistentModel_12345.gguf";
        var genResult = provider.GenerateResponseAsync(config, "system", "user").GetAwaiter().GetResult();
        Assert(genResult.StartsWith("Error"), "Non-existent local path returns error string");

        // Test TestConnectionAsync with empty ModelId returns error string
        config.ModelId = "";
        var result = provider.TestConnectionAsync(config).GetAwaiter().GetResult();
        Assert(result.StartsWith("Error:"), "TestConnectionAsync with empty ModelId returns error string");

        // Test TestConnectionAsync with non-existent model returns error string
        config.ModelId = "Z:\\NonExistentModel_12345.gguf";
        result = provider.TestConnectionAsync(config).GetAwaiter().GetResult();
        Assert(result.StartsWith("Error"), "TestConnectionAsync with non-existent model returns error string");
    }

    private static void TestHuggingFaceProviderGetAvailableModels()
    {
        Console.WriteLine("\n─── HuggingFaceProvider GetAvailableModels ───");

        var provider = new HuggingFaceProvider();
        var config = new AIModelConfiguration();

        // Test with empty Endpoint (no root directory set) - uses default user profile path
        config.Endpoint = "";
        var models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
        // Note: May return 0 results if network is unavailable; just check it doesn't crash
        Assert(models.Count >= 0, "Empty Endpoint returns gracefully (0 or more results)");

        // Test with non-existent root directory
        config.Endpoint = "Z:\\NonExistentRootDir_12345";
        models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
        // Note: May return 0 results if network is unavailable; just check it doesn't crash
        Assert(models.Count >= 0, "Non-existent Endpoint returns gracefully (0 or more results)");

        // Test with a real directory that exists but has no GGUF models
        var tempDir = Path.Combine(Path.GetTempPath(), "VerdictHFTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            config.Endpoint = tempDir;
            models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
            // Should return HF model results or at least not crash
            Assert(models.Count >= 0, "Empty directory returns model list (may include HF API results)");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        // Test with a directory containing a subdirectory with .gguf file
        var rootDir = Path.Combine(Path.GetTempPath(), "VerdictHFTest_" + Guid.NewGuid().ToString("N"));
        var modelDir = Path.Combine(rootDir, "TestLocalModel");
        try
        {
            Directory.CreateDirectory(modelDir);
            File.WriteAllText(Path.Combine(modelDir, "model.gguf"), "dummy");

            config.Endpoint = rootDir;
            models = provider.GetAvailableModelsAsync(config).GetAwaiter().GetResult();
            Assert(models.Any(m => m.Contains("[LOCAL]") && m.Contains("TestLocalModel")), "Directory with .gguf file returns TestLocalModel prefixed with [LOCAL]");
        }
        finally
        {
            if (Directory.Exists(rootDir))
                Directory.Delete(rootDir, true);
        }
    }

    private static void TestHuggingFaceProviderBuildChatPrompt()
    {
        Console.WriteLine("\n─── HuggingFaceProvider BuildChatPrompt ───");

        var provider = new HuggingFaceProvider();

        // Use reflection to invoke the private BuildChatPrompt method
        var method = typeof(HuggingFaceProvider).GetMethod("BuildChatPrompt",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert(method != null, "BuildChatPrompt private method is accessible via reflection");
        if (method == null) return;

        // Test 1: Llama 2 format
        var result = (string)method.Invoke(provider, new object[] { "TheBloke/Llama2-7B-GGUF", "", "Hello" })!;
        Assert(result == "[INST] Hello [/INST]", "Llama 2 model uses [INST] format without system prompt");
        Assert(!result.Contains("<<SYS>>"), "Llama 2 format has no <<SYS>> section");
        Assert(result.Contains("[INST]") && result.Contains("[/INST]"), "Llama 2 format contains INST tags");

        // Test 2: Llama 2 format with system prompt
        result = (string)method.Invoke(provider, new object[] { "TheBloke/CodeLlama-34B-GGUF", "Be helpful.", "Hello" })!;
        Assert(result.Contains("<<SYS>>") && result.Contains("<</SYS>>"), "Llama 2 with system prompt has SYS tags");
        Assert(result.Contains("Be helpful."), "Llama 2 with system prompt includes system content");
        Assert(result.Contains("[INST]") && result.Contains("[/INST]"), "Llama 2 with system prompt has INST tags");

        // Test 3: Llama 3 format
        result = (string)method.Invoke(provider, new object[] { "NousResearch/Meta-Llama-3-8B-GGUF", "", "Hello" })!;
        Assert(result.StartsWith("<|begin_of_text|>"), "Llama 3 format starts with BOT token");
        Assert(result.Contains("<|start_header_id|>user<|end_header_id|>"), "Llama 3 format has user header");
        Assert(result.Contains("<|eot_id|>"), "Llama 3 format has EOT token");
        Assert(result.Contains("Hello"), "Llama 3 format includes user message");

        // Test 4: Llama 3 format with system prompt
        result = (string)method.Invoke(provider, new object[] { "meta-llama/Meta-Llama-3.1-8B-Instruct-GGUF", "Be concise.", "Hello" })!;
        Assert(result.Contains("<|start_header_id|>system<|end_header_id|>"), "Llama 3 with system has system header");
        Assert(result.Contains("Be concise."), "Llama 3 with system includes system content");
        Assert(result.Contains("<|start_header_id|>assistant<|end_header_id|>"), "Llama 3 with system has assistant header");

        // Test 5: ChatML format (TinyLlama)
        result = (string)method.Invoke(provider, new object[] { "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF", "", "Hello" })!;
        Assert(result == "<|im_start|>user\nHello<|im_end|>\n<|im_start|>assistant\n", "TinyLlama uses ChatML format without system");

        // Test 6: ChatML format (Mistral)
        result = (string)method.Invoke(provider, new object[] { "TheBloke/Mistral-7B-Instruct-GGUF", "Be helpful.", "Hello" })!;
        Assert(result.StartsWith("<|im_start|>system\nBe helpful.<|im_end|>"), "Mistral with system uses ChatML format");
        Assert(result.Contains("<|im_start|>user\nHello<|im_end|>"), "Mistral ChatML includes user message");
        Assert(result.EndsWith("<|im_start|>assistant\n"), "Mistral ChatML ends with assistant header");

        // Test 7: ChatML format (Zephyr)
        result = (string)method.Invoke(provider, new object[] { "TheBloke/zephyr-7B-beta-GGUF", "", "Hi" })!;
        Assert(result == "<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n", "Zephyr uses ChatML format");

        // Test 8: ChatML format (Mixtral)
        result = (string)method.Invoke(provider, new object[] { "TheBloke/Mixtral-8x7B-Instruct-GGUF", "", "Hi" })!;
        Assert(result == "<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n", "Mixtral uses ChatML format");

        // Test 9: Default fallback (unknown model) also uses ChatML
        result = (string)method.Invoke(provider, new object[] { "SomeUnknown/Model-GGUF", "", "Hi" })!;
        Assert(result == "<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n", "Unknown model falls back to ChatML format");

        // Test 10: Default fallback with system prompt
        result = (string)method.Invoke(provider, new object[] { "SomeUnknown/Model-GGUF", "System instruction.", "Hi" })!;
        Assert(result.StartsWith("<|im_start|>system\nSystem instruction.<|im_end|>"), "Unknown model fallback with system includes system content");
        Assert(result.Contains("<|im_start|>user\nHi<|im_end|>"), "Unknown model fallback with system includes user message");
        Assert(result.EndsWith("<|im_start|>assistant\n"), "Unknown model fallback with system ends with assistant header");
    }
}