using System;
using Verdict.Models;
using Verdict.Providers;

var provider = new OnnxProvider();
var config = new AIModelConfiguration 
{ 
    ModelId = @"C:\Users\charlesp\Verdict\Models\test-onnx-model"
};

Console.WriteLine("Testing ONNX connection...");
Console.WriteLine($"ModelId: '{config.ModelId}'");
Console.WriteLine($"Trimmed: '{config.ModelId.Trim()}'");

var result = provider.TestConnectionAsync(config).GetAwaiter().GetResult();
Console.WriteLine($"Result: {result}");
