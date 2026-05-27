using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Views;

public partial class AgentProfileWindow : Window
{
    private readonly JuryDemographicsService _demoService = new();
    private Agent? _targetAgent;

    public AgentProfileWindow(Agent agent, IEnumerable<AIModelConfiguration>? availableModels = null)
    {
        InitializeComponent();
        
        if (availableModels != null)
        {
            var wrapper = new AgentWithAvailableModels(agent, availableModels.ToList());
            DataContext = wrapper;
            _targetAgent = wrapper;
        }
        else
        {
            DataContext = agent;
            _targetAgent = agent;
        }

        // Listen for property changes to regenerate prompt/profile
        if (_targetAgent is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += OnAgentPropertyChanged;
        }

        // Generate initial prompt/profile if empty
        if (string.IsNullOrWhiteSpace(_targetAgent.SystemPrompt) || string.IsNullOrWhiteSpace(_targetAgent.Profile))
        {
            RegeneratePrompt(_targetAgent);
        }
    }

    private void OnAgentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_targetAgent == null) return;

        var promptRelevant = new HashSet<string>
        {
            nameof(Agent.Age), nameof(Agent.Gender), nameof(Agent.Race),
            nameof(Agent.Occupation), nameof(Agent.EducationLevel), nameof(Agent.IncomeLevel),
            nameof(Agent.MaritalStatus), nameof(Agent.ParentalStatus),
            nameof(Agent.ReligiousAffiliation), nameof(Agent.PoliticalAffiliation),
            nameof(Agent.Hobbies), nameof(Agent.ConsumerSegment),
            nameof(Agent.SpecializedKnowledge), nameof(Agent.MediaConsumption)
        };

        if (e.PropertyName != null && promptRelevant.Contains(e.PropertyName))
        {
            RegeneratePrompt(_targetAgent);
        }
    }

    private void RegeneratePrompt(Agent agent)
    {
        agent.Profile = BuildProfile(agent);
        agent.SystemPrompt = BuildPrompt(agent);
    }

    private static string BuildProfile(Agent agent)
    {
        string hobbiesText = string.IsNullOrWhiteSpace(agent.Hobbies) ? "Reading, Walking" : agent.Hobbies;
        string knowledgeText = string.IsNullOrWhiteSpace(agent.SpecializedKnowledge) ? "General public knowledge" : agent.SpecializedKnowledge;

        return $"A {agent.Age}-year-old {agent.Race} {agent.Gender}. " +
               $"Education: {agent.EducationLevel}. Occupation: {agent.Occupation}. " +
               $"Marital: {agent.MaritalStatus}. Parental: {agent.ParentalStatus}. " +
               $"Income: {agent.IncomeLevel}. Politics: {agent.PoliticalAffiliation}. " +
               $"Religion: {agent.ReligiousAffiliation}. Media: {agent.MediaConsumption}. " +
               $"Consumer: {agent.ConsumerSegment}. " +
               $"Hobbies: {hobbiesText}. Knowledge: {knowledgeText}.";
    }

    private static string BuildPrompt(Agent agent)
    {
        return $"You are a juror named {agent.Name}. {agent.Profile} " +
               $"Your hobbies include {agent.Hobbies}. " +
               $"As a {agent.Occupation}, you have specialized knowledge in {agent.SpecializedKnowledge}. " +
               "You are participating in a courtroom simulation. " +
               "Consider the evidence and arguments presented, and form an opinion based on your " +
               "background, experiences, and the facts of the case. " +
               "Your verdict lean should reflect your genuine assessment.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AddBiasFactor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is Agent agent)
            agent.BiasFactors.Add(new BiasFactor { Name = "New Factor", Weight = 0.0 });
    }

    private void ResetWeights_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Agent agent) return;
        agent.AgeBiasWeight = null;
        agent.GenderBiasWeight = null;
        agent.EducationBiasWeight = null;
        agent.IncomeBiasWeight = null;
        agent.PoliticalBiasWeight = null;
        agent.EthnicityBiasWeight = null;
        agent.ReligionBiasWeight = null;
        agent.JurorExperienceWeight = null;
        agent.LegalKnowledgeWeight = null;
        agent.ProfessionalBackgroundWeight = null;
        agent.CommunityTiesWeight = null;
    }

    private void ResetRolePrompt_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Agent agent) return;
        agent.RoleSystemPrompt = AgentInteractionService.GetDefaultSystemPrompt(agent.Role);
    }

    private void RemoveBiasFactor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element &&
            element.Tag is BiasFactor factor &&
            DataContext is Agent agent)
        {
            agent.BiasFactors.Remove(factor);
        }
    }
}

// Wrapper class to hold both the agent and available models
public class AgentWithAvailableModels : Agent
{
    public List<AIModelConfiguration> AvailableModels { get; }

    public AgentWithAvailableModels(Agent agent, List<AIModelConfiguration> availableModels)
    {
        // Copy all properties from the original agent
        Name = agent.Name;
        Role = agent.Role;
        Gender = agent.Gender;
        Age = agent.Age;
        Race = agent.Race;
        Occupation = agent.Occupation;
        EducationLevel = agent.EducationLevel;
        IncomeLevel = agent.IncomeLevel;
        ZipCode = agent.ZipCode;
        PoliticalAffiliation = agent.PoliticalAffiliation;
        ReligiousAffiliation = agent.ReligiousAffiliation;
        SpecializedKnowledge = agent.SpecializedKnowledge;
        MaritalStatus = agent.MaritalStatus;
        ParentalStatus = agent.ParentalStatus;
        SettlementAuthority = agent.SettlementAuthority;
        RiskPerception = agent.RiskPerception;
        MediaConsumption = agent.MediaConsumption;
        ConsumerSegment = agent.ConsumerSegment;
        Hobbies = agent.Hobbies;
        ViewingCharacteristics = agent.ViewingCharacteristics;
        CurrentStatus = agent.CurrentStatus;
        SystemPrompt = agent.SystemPrompt;
        Profile = agent.Profile;
        Bias = agent.Bias;
        VerdictLean = agent.VerdictLean;
        Sentiment = agent.Sentiment;
        IsOccupied = agent.IsOccupied;
        JudicialRulings = agent.JudicialRulings;
        WrittenOpinions = agent.WrittenOpinions;
        JudicialTemperament = agent.JudicialTemperament;
        SelectedModel = agent.SelectedModel;
        ModelTemperatureOverride = agent.ModelTemperatureOverride;
        ModelMaxTokensOverride = agent.ModelMaxTokensOverride;
        AgeBiasWeight = agent.AgeBiasWeight;
        GenderBiasWeight = agent.GenderBiasWeight;
        EducationBiasWeight = agent.EducationBiasWeight;
        IncomeBiasWeight = agent.IncomeBiasWeight;
        PoliticalBiasWeight = agent.PoliticalBiasWeight;
        EthnicityBiasWeight = agent.EthnicityBiasWeight;
        ReligionBiasWeight = agent.ReligionBiasWeight;
        JurorExperienceWeight = agent.JurorExperienceWeight;
        LegalKnowledgeWeight = agent.LegalKnowledgeWeight;
        ProfessionalBackgroundWeight = agent.ProfessionalBackgroundWeight;
        CommunityTiesWeight = agent.CommunityTiesWeight;
        RoleSystemPrompt = agent.RoleSystemPrompt;
        foreach (var entry in agent.ExhibitLog)
            ExhibitLog.Add(entry);
        foreach (var bf in agent.BiasFactors)
            BiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
        DocumentCount = agent.DocumentCount;
        
        // Copy collections
        foreach(var memory in agent.Memories)
        {
            Memories.Add(memory);
        }
        
        foreach(var trialEvent in agent.TrialEvents)
        {
            TrialEvents.Add(trialEvent);
        }
        
        AvailableModels = availableModels;
    }
}