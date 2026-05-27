using System;

namespace Verdict.Models;

/// <summary>
/// Per-agent bias dimension weights that control how strongly each demographic
/// or experiential factor influences the agent's opinions and verdict lean.
/// Null values mean "inherit the global default from ModelWeightsWindow."
/// Range 0.0–1.0.
/// Extracted from Agent.cs.
/// </summary>
public class BiasDimensionWeights : ObservableObject
{
    private double? _ageBiasWeight;
    private double? _genderBiasWeight;
    private double? _educationBiasWeight;
    private double? _incomeBiasWeight;
    private double? _politicalBiasWeight;
    private double? _ethnicityBiasWeight;
    private double? _religionBiasWeight;
    private double? _jurorExperienceWeight;
    private double? _legalKnowledgeWeight;
    private double? _professionalBackgroundWeight;
    private double? _communityTiesWeight;

    public double? AgeBiasWeight
    {
        get => _ageBiasWeight;
        set => SetProperty(ref _ageBiasWeight, ClampWeight(value));
    }

    public double? GenderBiasWeight
    {
        get => _genderBiasWeight;
        set => SetProperty(ref _genderBiasWeight, ClampWeight(value));
    }

    public double? EducationBiasWeight
    {
        get => _educationBiasWeight;
        set => SetProperty(ref _educationBiasWeight, ClampWeight(value));
    }

    public double? IncomeBiasWeight
    {
        get => _incomeBiasWeight;
        set => SetProperty(ref _incomeBiasWeight, ClampWeight(value));
    }

    public double? PoliticalBiasWeight
    {
        get => _politicalBiasWeight;
        set => SetProperty(ref _politicalBiasWeight, ClampWeight(value));
    }

    public double? EthnicityBiasWeight
    {
        get => _ethnicityBiasWeight;
        set => SetProperty(ref _ethnicityBiasWeight, ClampWeight(value));
    }

    public double? ReligionBiasWeight
    {
        get => _religionBiasWeight;
        set => SetProperty(ref _religionBiasWeight, ClampWeight(value));
    }

    public double? JurorExperienceWeight
    {
        get => _jurorExperienceWeight;
        set => SetProperty(ref _jurorExperienceWeight, ClampWeight(value));
    }

    public double? LegalKnowledgeWeight
    {
        get => _legalKnowledgeWeight;
        set => SetProperty(ref _legalKnowledgeWeight, ClampWeight(value));
    }

    public double? ProfessionalBackgroundWeight
    {
        get => _professionalBackgroundWeight;
        set => SetProperty(ref _professionalBackgroundWeight, ClampWeight(value));
    }

    public double? CommunityTiesWeight
    {
        get => _communityTiesWeight;
        set => SetProperty(ref _communityTiesWeight, ClampWeight(value));
    }

    /// <summary>
    /// Copies all bias weights from another instance.
    /// </summary>
    public void CopyFrom(BiasDimensionWeights source)
    {
        if (source == null) return;
        AgeBiasWeight = source.AgeBiasWeight;
        GenderBiasWeight = source.GenderBiasWeight;
        EducationBiasWeight = source.EducationBiasWeight;
        IncomeBiasWeight = source.IncomeBiasWeight;
        PoliticalBiasWeight = source.PoliticalBiasWeight;
        EthnicityBiasWeight = source.EthnicityBiasWeight;
        ReligionBiasWeight = source.ReligionBiasWeight;
        JurorExperienceWeight = source.JurorExperienceWeight;
        LegalKnowledgeWeight = source.LegalKnowledgeWeight;
        ProfessionalBackgroundWeight = source.ProfessionalBackgroundWeight;
        CommunityTiesWeight = source.CommunityTiesWeight;
    }

    /// <summary>
    /// Resets all weights to null (inherit global defaults).
    /// </summary>
    public void ResetAll()
    {
        AgeBiasWeight = null;
        GenderBiasWeight = null;
        EducationBiasWeight = null;
        IncomeBiasWeight = null;
        PoliticalBiasWeight = null;
        EthnicityBiasWeight = null;
        ReligionBiasWeight = null;
        JurorExperienceWeight = null;
        LegalKnowledgeWeight = null;
        ProfessionalBackgroundWeight = null;
        CommunityTiesWeight = null;
    }

    /// <summary>
    /// Sets all weights to a uniform value (0.0–1.0).
    /// </summary>
    public void SetAll(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        AgeBiasWeight = clamped;
        GenderBiasWeight = clamped;
        EducationBiasWeight = clamped;
        IncomeBiasWeight = clamped;
        PoliticalBiasWeight = clamped;
        EthnicityBiasWeight = clamped;
        ReligionBiasWeight = clamped;
        JurorExperienceWeight = clamped;
        LegalKnowledgeWeight = clamped;
        ProfessionalBackgroundWeight = clamped;
        CommunityTiesWeight = clamped;
    }

    private static double? ClampWeight(double? value)
        => value.HasValue ? Math.Clamp(value.Value, 0, 1) : null;
}
