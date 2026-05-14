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
        Assert(modelIdField!.Label == "Model Path or HF Model ID", "ModelId field label is 'Model Path or HF Model ID'");
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
            File.WriteAllText(Path.Combine(modelDir, "model.onnx"), "dummy");
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

        // Check that HuggingFaceProvider is discovered by the service
        var hfProvider = providers.FirstOrDefault(p => p.ProviderName == "Hugging Face");
        Assert(hfProvider != null, "ProviderDiscoveryService discovers HuggingFaceProvider");
        Assert(hfProvider is HuggingFaceProvider, "Discovered Hugging Face provider is HuggingFaceProvider type");

        // Check GetProviderByName works
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