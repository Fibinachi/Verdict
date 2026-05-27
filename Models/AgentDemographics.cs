using System;

namespace Verdict.Models;

/// <summary>
/// Encapsulates all demographic and background characteristics of an agent.
/// Extracted from Agent.cs to promote composition over a monolithic model.
/// </summary>
public class AgentDemographics : ObservableObject
{
    private string _gender = "Unknown";
    private int _age = 18;
    private string _race = "Unknown";
    private string _occupation = "Unemployed";
    private string _educationLevel = "High School";
    private string _incomeLevel = "Middle Class";
    private string _mediaConsumption = "Mainstream News";
    private string _consumerSegment = "General";
    private string _maritalStatus = "Single";
    private string _parentalStatus = "No Children";
    private string _hobbies = string.Empty;
    private string _religiousAffiliation = "Non-religious";
    private string _politicalAffiliation = "Independent";
    private string _zipCode = "00000";
    private string _viewingCharacteristics = string.Empty;
    private string _currentStatus = "Attentive";
    private string _specializedKnowledge = "None (General Public)";

    public string Gender
    {
        get => _gender;
        set => SetProperty(ref _gender, value);
    }

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public string Race
    {
        get => _race;
        set => SetProperty(ref _race, value);
    }

    public string Occupation
    {
        get => _occupation;
        set => SetProperty(ref _occupation, value);
    }

    public string EducationLevel
    {
        get => _educationLevel;
        set => SetProperty(ref _educationLevel, value);
    }

    public string IncomeLevel
    {
        get => _incomeLevel;
        set => SetProperty(ref _incomeLevel, value);
    }

    public string MediaConsumption
    {
        get => _mediaConsumption;
        set => SetProperty(ref _mediaConsumption, value);
    }

    public string ConsumerSegment
    {
        get => _consumerSegment;
        set => SetProperty(ref _consumerSegment, value);
    }

    public string MaritalStatus
    {
        get => _maritalStatus;
        set => SetProperty(ref _maritalStatus, value);
    }

    public string ParentalStatus
    {
        get => _parentalStatus;
        set => SetProperty(ref _parentalStatus, value);
    }

    public string Hobbies
    {
        get => _hobbies;
        set => SetProperty(ref _hobbies, value);
    }

    public string ReligiousAffiliation
    {
        get => _religiousAffiliation;
        set => SetProperty(ref _religiousAffiliation, value);
    }

    public string PoliticalAffiliation
    {
        get => _politicalAffiliation;
        set => SetProperty(ref _politicalAffiliation, value);
    }

    public string ZipCode
    {
        get => _zipCode;
        set => SetProperty(ref _zipCode, value);
    }

    public string ViewingCharacteristics
    {
        get => _viewingCharacteristics;
        set => SetProperty(ref _viewingCharacteristics, value);
    }

    /// <summary>
    /// Current mental/physical state during the trial (e.g. Bored, Focused, Skeptical).
    /// </summary>
    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    /// <summary>
    /// Specialized training or expertise (e.g., Medical, Engineering, Legal). 
    /// Defaults to "None (General Public)" for average understanding.
    /// </summary>
    public string SpecializedKnowledge
    {
        get => _specializedKnowledge;
        set => SetProperty(ref _specializedKnowledge, value);
    }

    /// <summary>
    /// Copies all demographic values from another demographics instance.
    /// </summary>
    public void CopyFrom(AgentDemographics source)
    {
        if (source == null) return;
        Gender = source.Gender;
        Age = source.Age;
        Race = source.Race;
        Occupation = source.Occupation;
        EducationLevel = source.EducationLevel;
        IncomeLevel = source.IncomeLevel;
        MediaConsumption = source.MediaConsumption;
        ConsumerSegment = source.ConsumerSegment;
        MaritalStatus = source.MaritalStatus;
        ParentalStatus = source.ParentalStatus;
        Hobbies = source.Hobbies;
        ReligiousAffiliation = source.ReligiousAffiliation;
        PoliticalAffiliation = source.PoliticalAffiliation;
        ZipCode = source.ZipCode;
        ViewingCharacteristics = source.ViewingCharacteristics;
        CurrentStatus = source.CurrentStatus;
        SpecializedKnowledge = source.SpecializedKnowledge;
    }

    /// <summary>
    /// Returns a multi-line summary of the demographics for use in LLM system prompts.
    /// </summary>
    public string ToProfileSummary()
    {
        return $"- Age: {Age}, Gender: {Gender}, Race: {Race}\n" +
               $"- Occupation: {Occupation}, Education: {EducationLevel}\n" +
               $"- Income: {IncomeLevel}, Political: {PoliticalAffiliation}\n" +
               $"- Religious: {ReligiousAffiliation}, Marital: {MaritalStatus}, Parental: {ParentalStatus}";
    }
}
