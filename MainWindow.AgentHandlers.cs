using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Verdict.Models;
using Verdict.Views;

namespace Verdict;

/// <summary>
/// Agent-related event handlers: add, edit, delete, generate jurors, chat, reports, context menus.
/// </summary>
public partial class MainWindow
{
    private void AddAgent_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            var agentToEdit = agent.IsOccupied ? agent : new Agent
            {
                Role = agent.Role,
                Name = agent.Name.StartsWith("+") ? "" : agent.Name
            };

            if (!agent.IsOccupied)
            {
                ApplyRoleDefaults(agentToEdit);
                var roleModel = ViewModels.MainViewModel.GetDefaultModelForRole(
                    agentToEdit.Role, agentToEdit.CommunicationTeam, _viewModel.CurrentCase);
                if (!string.IsNullOrWhiteSpace(roleModel))
                    agentToEdit.SelectedModel = roleModel;
            }

            var profileWindow = new AgentProfileWindow(agentToEdit, _viewModel.EffectiveModels) { Owner = this };

            if (profileWindow.ShowDialog() == true)
            {
                if (profileWindow.DataContext is AgentWithAvailableModels wrapper)
                {
                    if (!agent.IsOccupied)
                    {
                        agent.Name = string.IsNullOrWhiteSpace(wrapper.Name) ? $"Active {wrapper.Role}" : wrapper.Name;
                        agent.Gender = wrapper.Gender;
                        agent.Age = wrapper.Age;
                        agent.Race = wrapper.Race;
                        agent.Occupation = wrapper.Occupation;
                        agent.EducationLevel = wrapper.EducationLevel;
                        agent.IncomeLevel = wrapper.IncomeLevel;
                        agent.ZipCode = wrapper.ZipCode;
                        agent.PoliticalAffiliation = wrapper.PoliticalAffiliation;
                        agent.ReligiousAffiliation = wrapper.ReligiousAffiliation;
                        agent.SpecializedKnowledge = wrapper.SpecializedKnowledge;
                        agent.MaritalStatus = wrapper.MaritalStatus;
                        agent.ParentalStatus = wrapper.ParentalStatus;
                        agent.SettlementAuthority = wrapper.SettlementAuthority;
                        agent.RiskPerception = wrapper.RiskPerception;
                        agent.MediaConsumption = wrapper.MediaConsumption;
                        agent.ConsumerSegment = wrapper.ConsumerSegment;
                        agent.Hobbies = wrapper.Hobbies;
                        agent.ViewingCharacteristics = wrapper.ViewingCharacteristics;
                        agent.CurrentStatus = wrapper.CurrentStatus;
                        agent.SystemPrompt = wrapper.SystemPrompt;
                    }
                    agent.SelectedModel = wrapper.SelectedModel;
                }
                _viewModel.OccupySlot(agent);
            }
        }
        else
        {
            MessageBox.Show("Could not create agent profile. Invalid agent data.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Applies role-appropriate default values for new agents.
    /// </summary>
    private static void ApplyRoleDefaults(Agent agent)
    {
        switch (agent.Role)
        {
            case AgentRole.Judge:
                agent.CommunicationTeam = "Neutral";
                agent.OnlyAnswerWhenAsked = true;
                agent.Occupation = "Judge";
                agent.EducationLevel = "Juris Doctor (JD)";
                agent.Age = Math.Max(agent.Age, 40);
                agent.SpecializedKnowledge = "Criminal Law, Tort Law, Civil Law, Family Law";
                break;

            case AgentRole.Lawyer:
                agent.CommunicationTeam = "Neutral";
                agent.Age = Math.Max(agent.Age, 25);
                agent.Occupation = "Attorney";
                agent.EducationLevel = "Juris Doctor (JD)";
                agent.SpecializedKnowledge = "Criminal Law, Tort Law, Civil Law";
                break;

            case AgentRole.Witness:
                agent.Occupation = string.IsNullOrWhiteSpace(agent.Occupation) || agent.Occupation == "Unemployed"
                    ? "Professional" : agent.Occupation;
                break;

            case AgentRole.Reporter:
                agent.Occupation = "Court Reporter";
                agent.EducationLevel = "Associate Degree";
                break;
        }
    }

    private void GenerateLikelyJuror_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        var agent = menuItem?.Tag as Agent;
        if (agent == null) return;

        if (agent.Role != AgentRole.Juror && agent.Role != AgentRole.AlternateJuror)
        {
            MessageBox.Show("This feature is only available for juror seats.", "Invalid Operation",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string jurisdiction = _viewModel.CurrentCase.JurisdictionSpecifics ?? "Richland County, South Carolina";
        string[] parts = jurisdiction.Split(',');
        string county = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : "Richland County";
        string state = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : "South Carolina";

        var newJuror = _viewModel.GenerateSingleJuror(county, state);

        agent.Name = newJuror.Name;
        agent.Gender = newJuror.Gender;
        agent.Age = newJuror.Age;
        agent.Race = newJuror.Race;
        agent.Occupation = newJuror.Occupation;
        agent.EducationLevel = newJuror.EducationLevel;
        agent.IncomeLevel = newJuror.IncomeLevel;
        agent.ZipCode = newJuror.ZipCode;
        agent.PoliticalAffiliation = newJuror.PoliticalAffiliation;
        agent.ReligiousAffiliation = newJuror.ReligiousAffiliation;
        agent.SpecializedKnowledge = newJuror.SpecializedKnowledge;
        agent.MaritalStatus = newJuror.MaritalStatus;
        agent.ParentalStatus = newJuror.ParentalStatus;
        agent.SettlementAuthority = newJuror.SettlementAuthority;
        agent.RiskPerception = newJuror.RiskPerception;
        agent.MediaConsumption = newJuror.MediaConsumption;
        agent.ConsumerSegment = newJuror.ConsumerSegment;
        agent.Hobbies = newJuror.Hobbies;
        agent.ViewingCharacteristics = newJuror.ViewingCharacteristics;
        agent.CurrentStatus = newJuror.CurrentStatus;
        agent.SystemPrompt = newJuror.SystemPrompt;
        agent.Profile = newJuror.Profile;
        agent.Bias = newJuror.Bias;
        agent.VerdictLean = newJuror.VerdictLean;
        agent.Sentiment = newJuror.Sentiment;

        agent.TrialEvents.Clear();
        foreach (var mem in newJuror.TrialEvents)
            agent.TrialEvents.Add(mem);

        if (!agent.IsOccupied)
            _viewModel.OccupySlot(agent);

        _viewModel.TranscriptOutput = $"[JUROR GENERATED] New juror generated based on {county}, {state} demographics";
    }

    private void SaveAgent_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Agent saved to global casefile.");
    }

    private void ExportCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Agent agent && agent.IsOccupied)
        {
            Services.CharacterManager.SaveCharacter(agent);
            MessageBox.Show($"Character profile saved to {Services.CharacterManager.RepositoryPath}");
        }
    }

    private void DeleteAgent_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            agent.IsOccupied = false;
            agent.Name = "Unoccupied " + agent.Role;
        }
    }

    private void EditReporterProfile_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent == null) return;

        var profileWindow = new AgentProfileWindow(agent, _viewModel.EffectiveModels) { Owner = this };
        if (profileWindow.ShowDialog() == true)
        {
            if (profileWindow.DataContext is AgentWithAvailableModels wrapper)
            {
                agent.SelectedModel = wrapper.SelectedModel;
                agent.SystemPrompt = wrapper.SystemPrompt;
            }
        }
    }

    private void AgentSeat_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var border = sender as Border;
        if (border?.ContextMenu == null) return;

        var agent = border.DataContext as Agent;
        foreach (var item in border.ContextMenu.Items.OfType<MenuItem>())
        {
            if (item.Header.ToString() == "Chat")
                item.Visibility = (agent != null && agent.IsOccupied) ? Visibility.Visible : Visibility.Collapsed;
            else if (item.Header.ToString() == "Juror Report")
                item.Visibility = (agent != null && agent.IsOccupied &&
                    (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror))
                    ? Visibility.Visible : Visibility.Collapsed;
            else
                item.Visibility = Visibility.Visible;
        }
    }

    private void AddClientToLawyer_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent == null) return;

        var lawyers = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Lawyer && a.IsOccupied).ToList();
        if (!lawyers.Any())
        {
            MessageBox.Show("No lawyers are seated. Please add a lawyer first.", "No Lawyer Available",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var clientAgent = new Agent
        {
            Role = AgentRole.Client,
            Name = $"Client (of {agent.Name.Split(' ').LastOrDefault() ?? agent.Name})",
            IsOccupied = true
        };

        _viewModel.Gallery.Add(clientAgent);
        MessageBox.Show($"Client assigned to {agent.Name}. Client added to gallery area.",
            "Client Added", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AgentChat_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.Tag as Agent;
        if (agent == null || !agent.IsOccupied) return;

        var window = new AgentChatWindow(_viewModel, agent) { Owner = this };
        window.ShowDialog();
    }

    private void JurorReport_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.Tag as Agent;
        if (agent == null || !agent.IsOccupied) return;

        var reportWindow = new JurorReportWindow(agent, _viewModel.CurrentCase?.Mode ?? Models.CaseMode.Civil) { Owner = this };
        reportWindow.ShowDialog();
    }
}
