using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Verdict.Tests;

public static class ReproOnnx
{
    public static void Run()
    {
        string modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "test-repro");
        Directory.CreateDirectory(modelPath);

        Console.WriteLine($"Creating dummy model at {modelPath}");

        // Create dummy config
        var genaiConfig = new
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
        File.WriteAllText(Path.Combine(modelPath, "genai_config.json"), JsonSerializer.Serialize(genaiConfig));
        File.WriteAllText(Path.Combine(modelPath, "model.onnx"), "");
        File.WriteAllText(Path.Combine(modelPath, "tokenizer.json"), "{}");
        File.WriteAllText(Path.Combine(modelPath, "tokenizer_config.json"), "{}");

        try
        {
            Console.WriteLine("Attempting to load model...");
            using var model = new Model(modelPath);
            Console.WriteLine("Model loaded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED to load model: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
