using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Views;

public partial class AgentProfileWindow : Window
{
    public AgentProfileWindow(Agent agent, IEnumerable<AIModelConfiguration>? availableModels = null)
    {
        InitializeComponent();
        
        // If available models are provided, create a wrapper that combines the agent with the available models
        if (availableModels != null)
        {
            var wrapper = new AgentWithAvailableModels(agent, availableModels.ToList());
            DataContext = wrapper;
        }
        else
        {
            DataContext = agent;
        }
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