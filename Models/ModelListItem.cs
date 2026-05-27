namespace Verdict.Models;

/// <summary>
/// Structured model info for display in the model selection dialog.
/// </summary>
public class ModelListItem
{
    /// <summary>Human-readable display name (e.g., "Llama 3.2 1B Instruct ONNX").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The model identifier — HF repo ID or local filesystem path.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Where the model comes from: "Local", "HuggingFace", or separator header.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Size in bytes (0 if unknown).</summary>
    public long SizeBytes { get; set; }

    /// <summary>Human-readable size (e.g., "1.8 GB"). Empty if unknown.</summary>
    public string SizeFormatted { get; set; } = string.Empty;

    /// <summary>Backend/architecture hint (e.g., "CPU", "DirectML", "INT4", empty if unknown).</summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>True if this is a separator/header row, not a selectable model.</summary>
    public bool IsHeader { get; set; }
}
