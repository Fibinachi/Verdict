#!/usr/bin/env pwsh
# Quick setup script for default ONNX model
# Run this to create test model files for ONNX provider testing

param(
    [string]$Destination = ".\Models\test-model"
)

Write-Host "Setting up default ONNX model..."
Write-Host "Destination: $Destination"

# Kill any locked processes
taskkill /F /IM dotnet.exe /T 2>$null
taskkill /IM git.exe /T 2>$null
Start-Sleep -Seconds 2

# Create directory
$DestPath = Join-Path $PSScriptRoot $Destination
New-Item -ItemType Directory -Force -Path $DestPath | Out-Null

# Download genai_config.json
$genaiConfig = @"
{
  "model": {
    "context_length": 2048,
    "decoder": {
      "session_options": {
        "enable_cpu_mem_arena": false
      }
    }
  }
}
"@
Set-Content -Path "$DestPath\genai_config.json" -Value $genaiConfig

# Create a minimal model.onnx placeholder (actual model needs to be downloaded separately)
# This allows the model discovery to work
Set-Content -Path "$DestPath\model.onnx" -Value ""

# Create tokenizer files (required by OnnxRuntimeGenAI)
Set-Content -Path "$DestPath\tokenizer.json" -Value "{}"
Set-Content -Path "$DestPath\tokenizer_config.json" -Value '{"chat_template": ""}'
Set-Content -Path "$DestPath\special_tokens_map.json" -Value "{}"

Write-Host ""
Write-Host "Setup complete. Model directory: $DestPath"
Write-Host ""
Write-Host "NOTE: For actual ONNX inference, download a real model from HuggingFace:"
Write-Host "  Example: onnx-community/Llama-2-7b-Chat-ONNX"
Write-Host ""
Write-Host "Update the model path in Resources/default-case-settings.json"
Write-Host ""
Write-Host "Run 'dotnet run --project Tests\Tests.csproj' to test."