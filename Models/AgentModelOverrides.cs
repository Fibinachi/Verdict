using System;

namespace Verdict.Models;

/// <summary>
/// Per-agent model override settings.
/// Allows individual agents to use a different model, temperature, or token limit
/// than the global case defaults.
/// Extracted from Agent.cs.
/// </summary>
public class AgentModelOverrides : ObservableObject
{
    private string _selectedModel = string.Empty;
    private double? _modelTemperatureOverride;
    private int? _modelMaxTokensOverride;

    /// <summary>
    /// The selected model that this agent should use for AI interactions.
    /// If empty, the system will use the first available working model.
    /// </summary>
    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value ?? string.Empty);
    }

    /// <summary>
    /// Overrides the default model temperature for this agent (0.0-2.0).
    /// Null means use the global default from case settings.
    /// </summary>
    public double? ModelTemperatureOverride
    {
        get => _modelTemperatureOverride;
        set => SetProperty(ref _modelTemperatureOverride,
            value.HasValue ? Math.Clamp(value.Value, 0.0, 2.0) : null);
    }

    /// <summary>
    /// Overrides the default max tokens for this agent.
    /// Null means use the global default from case settings.
    /// </summary>
    public int? ModelMaxTokensOverride
    {
        get => _modelMaxTokensOverride;
        set => SetProperty(ref _modelMaxTokensOverride,
            value.HasValue ? Math.Max(64, value.Value) : null);
    }

    /// <summary>
    /// Copies all model overrides from another instance.
    /// </summary>
    public void CopyFrom(AgentModelOverrides source)
    {
        if (source == null) return;
        SelectedModel = source.SelectedModel;
        ModelTemperatureOverride = source.ModelTemperatureOverride;
        ModelMaxTokensOverride = source.ModelMaxTokensOverride;
    }

    /// <summary>
    /// Returns true if any override is set (non-default).
    /// </summary>
    public bool HasOverrides =>
        !string.IsNullOrWhiteSpace(SelectedModel) ||
        ModelTemperatureOverride.HasValue ||
        ModelMaxTokensOverride.HasValue;
}
