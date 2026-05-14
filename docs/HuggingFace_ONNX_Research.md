# HuggingFace ONNX Models & Download APIs for .NET

## 1. Popular ONNX LLM Models on HuggingFace

### Recommended Model IDs (for Microsoft.ML.OnnxRuntimeGenAI)

These models include `genai_config.json` and are compatible with `OnnxRuntimeGenAI`:

| Model ID | Parameters | Size (approx) | Notes |
|----------|-----------|---------------|-------|
| `llmware/llama-3.2-1b-instruct-onnx` | 1B | ~1.8 GB | Small, fast, good for testing |
| `llmware/llama-3.2-3b-instruct-onnx` | 3B | ~6 GB | Good balance of speed/quality |
| `llmware/llama-3.1-instruct-onnx` | 8B | ~16 GB | Full quality, needs more RAM |
| `llmware/mistral-7b-instruct-v0.3-onnx` | 7B | ~14 GB | Strong alternative to Llama |
| `llmware/qwen2.5-1.5b-instruct-onnx-qnn` | 1.5B | ~3 GB | QNN quantized (4-bit) |
| `llmware/qwen2.5-7b-instruct-onnx-qnn` | 7B | ~14 GB | QNN quantized (4-bit) |
| `llmware/phi-3-mini-4k-instruct-onnx-qnn` | 3.8B | ~8 GB | Phi-3 QNN quantized |
| `llmware/phi-3.5-mini-instruct-onnx-qnn` | 3.8B | ~8 GB | Phi-3.5 QNN quantized |
| `onnx-community/Llama-3.2-1B-Instruct-GENAI-ONNX` | 1B | ~1.8 GB | Alternative GENAI format |
| `onnx-community/Llama-3.2-3B-Instruct-GENAI-ONNX` | 3B | ~6 GB | Alternative GENAI format |
| `onnx-community/Meta-Llama-3.1-8B-Instruct-ONNX-DirectML-GenAI-INT4` | 8B | ~5 GB | INT4 quantized for DirectML |
| `onnx-community/DeepSeek-R1-Distill-Llama-8B-ONNX-DirectML-GenAI-INT4` | 8B | ~5 GB | DeepSeek distilled, INT4 |
| `onnx-community/gemma-2-9b-it-ONNX-DirectML-GenAI-INT4` | 9B | ~5.5 GB | Google Gemma, INT4 |

### Key Model Families
- **llmware/** - Well-maintained ONNX models with genai_config.json, OpenVINO optimized
- **onnx-community/** - Community ONNX exports, often with DirectML GenAI INT4 quantization
- **microsoft/Phi-3** variants - Official Microsoft ONNX exports

## 2. File Structure of an ONNX Model

A typical ONNX model directory for OnnxRuntimeGenAI contains:

```
model_directory/
├── model.onnx              # The main ONNX model file (can be 1-15+ GB)
├── model.onnx.data         # External data for weights (if model > 2GB)
├── genai_config.json       # REQUIRED for OnnxRuntimeGenAI - model config
├── config.json             # HuggingFace model configuration
├── tokenizer.json          # Tokenizer data (required)
├── tokenizer_config.json   # Tokenizer configuration
├── special_tokens_map.json # Special token mappings
└── hash_record_sha256.json # Integrity hashes (optional)
```

**Important**: `genai_config.json` is the key file that OnnxRuntimeGenAI looks for. Without it, the `Model(path)` constructor will fail.

### Example genai_config.json content:
```json
{
  "model": {
    "decoder": {
      "session_options": {
        "enable_cpu_mem_arena": false
      }
    }
  }
}
```

## 3. HuggingFace Download Mechanisms for .NET

### Option A: Direct HTTP Download (Already Implemented)

The existing `OnnxProvider.cs` already implements this approach:

**List files in a model repo:**
```
GET https://huggingface.co/api/models/{repo_id}
```
Response includes a `siblings` array with `rfilename` for each file.

**Download a specific file:**
```
GET https://huggingface.co/{repo_id}/resolve/main/{filename}
```
Example: `https://huggingface.co/llmware/llama-3.2-1b-instruct-onnx/resolve/main/model.onnx`

**Search for models:**
```
GET https://huggingface.co/api/models?search=onnx+llm+instruct&sort=downloads&direction=-1&limit=30
```

### Option B: ElBruno.HuggingFace.Downloader NuGet Package (Recommended Enhancement)

**Package**: `ElBruno.HuggingFace.Downloader` (v1.1.0, MIT license)
- 6,641+ downloads
- Targets .NET 8.0+ (compatible with .NET 9)
- Specifically designed for downloading ONNX models from HuggingFace
- Features: progress reporting, caching, authentication, atomic writes, DI support

**Usage:**
```csharp
using ElBruno.HuggingFace;

var downloader = new HuggingFaceDownloader();
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "llmware/llama-3.2-1b-instruct-onnx",
    LocalDirectory = "./models/llama-3.2-1b",
    RequiredFiles = ["model.onnx", "model.onnx.data", "genai_config.json", 
                     "config.json", "tokenizer.json", "tokenizer_config.json"],
    OptionalFiles = ["special_tokens_map.json"],
    Progress = progress // Progress<DownloadProgress>
});
```

**Install:** `dotnet add package ElBruno.HuggingFace.Downloader`

### Option C: HuggingfaceHub NuGet Package

**Package**: `HuggingfaceHub` (v0.1.3, 17,324 downloads)
- Older, less maintained (last updated April 2024)
- Simpler API for downloading models
- No progress reporting

### Option D: HuggingFace NuGet Package (OpenAPI SDK)

**Package**: `HuggingFace` (v0.4.2, 334,270 downloads)
- Generated C# SDK from HuggingFace OpenAPI spec
- Covers the full HuggingFace Hub API (list models, get model info, etc.)
- Does NOT handle file downloading directly
- Useful for model discovery/browsing

### Option E: SemanticKernel.Connectors.HuggingFace

**Package**: `Microsoft.SemanticKernel.Connectors.HuggingFace` (v1.75.0-preview, 408,743 downloads)
- Microsoft's Semantic Kernel connector for HuggingFace Inference API
- For remote inference (API calls), NOT for local model downloading
- Not suitable for this use case

## 4. HuggingFace API Endpoints Summary

| Purpose | Endpoint | Method |
|---------|----------|--------|
| Search models | `https://huggingface.co/api/models?search={query}&sort=downloads&direction=-1&limit={n}` | GET |
| Get model details + file list | `https://huggingface.co/api/models/{repo_id}` | GET |
| Download file | `https://huggingface.co/{repo_id}/resolve/main/{filename}` | GET |
| Download file (specific branch) | `https://huggingface.co/{repo_id}/resolve/{branch}/{filename}` | GET |
| OpenAPI spec | `https://huggingface.co/.well-known/openapi.json` | GET |
| Inference API (chat) | `https://router.huggingface.co/v1/chat/completions` | POST |

**Authentication**: Public models don't require auth. For gated/private models, set `Authorization: Bearer hf_...` header.

## 5. Model Size Estimates

| Model Size | FP16 ONNX | INT4 Quantized |
|-----------|-----------|---------------|
| 0.5B params | ~1 GB | ~0.3 GB |
| 1B params | ~2 GB | ~0.6 GB |
| 1.5B params | ~3 GB | ~0.9 GB |
| 3B params | ~6 GB | ~1.8 GB |
| 7B params | ~14 GB | ~4 GB |
| 8B params | ~16 GB | ~5 GB |
| 9B params | ~18 GB | ~5.5 GB |

**Note**: Models >2GB use `model.onnx` + `model.onnx.data` (external data format).

## 6. Recommended Approach for Verdict

### Current State (Already Working)
The existing `OnnxProvider.cs` already implements:
- Model search via `https://huggingface.co/api/models?search=onnx+llm+instruct`
- File listing via `https://huggingface.co/api/models/{repoId}`
- File download via `https://huggingface.co/{repoId}/resolve/main/{filename}`
- Required files: `model.onnx`, `model.onnx.data`, `genai_config.json`, `config.json`, `tokenizer.json`, `tokenizer_config.json`, `special_tokens_map.json`

### Potential Improvements
1. **Add `ElBruno.HuggingFace.Downloader` package** for progress reporting, atomic writes, and better error handling
2. **Improve search query** to also find `onnx-community/` models and DirectML GenAI INT4 variants
3. **Add parallel downloads** for multiple files (model.onnx.data can be large)
4. **Add resume support** for interrupted downloads
5. **Add HF_TOKEN support** for gated models
6. **Show download progress** in the UI during model download

### Search Query Enhancement
Current: `search=onnx+llm+instruct`
Better: `search=onnx+llm+instruct+genai&sort=downloads&direction=-1&limit=30`
Or search for DirectML variants: `search=onnx+DirectML+GenAI+INT4&sort=downloads&direction=-1&limit=20`

## 7. Existing .NET Projects Using HuggingFace Model Downloads

| Project | Approach |
|---------|----------|
| **LLamaSharp** | Custom HTTP download with progress, manages own model cache |
| **SemanticKernel** | Uses HuggingFace Inference API (remote), not local downloads |
| **ElBruno.LocalLLMs** | Uses `ElBruno.HuggingFace.Downloader` for ONNX model downloads |
| **ElBruno.LocalEmbeddings** | Uses `ElBruno.HuggingFace.Downloader` for embedding model downloads |
| **Verdict (current)** | Custom HTTP download via `OnnxProvider.cs` |

## 8. Key Takeaways

1. **Best small model for testing**: `llmware/llama-3.2-1b-instruct-onnx` (~1.8 GB)
2. **Best production small model**: `llmware/llama-3.2-3b-instruct-onnx` (~6 GB)
3. **Best INT4 quantized**: `onnx-community/Meta-Llama-3.1-8B-Instruct-ONNX-DirectML-GenAI-INT4` (~5 GB)
4. **The existing implementation works** but could benefit from `ElBruno.HuggingFace.Downloader` for progress reporting
5. **No API key needed** for public models - direct HTTP download works
6. **genai_config.json is mandatory** for OnnxRuntimeGenAI - without it the model won't load
