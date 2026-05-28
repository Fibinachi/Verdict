using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Verdict.Models;
using Verdict.ViewModels;
using Verdict.Views;

namespace Verdict;

/// <summary>
/// Settings and configuration event handlers: LLM config, model weights, case settings,
/// dialogs, reports, and application-level actions.
/// </summary>
public partial class MainWindow
{
    // ── Case settings dialogs ──

    private void CaseSettings_Click(object sender, RoutedEventArgs e)
    {
        var originalPhase = _viewModel.CurrentCase.TrialPhase;
        var viewModel = new CaseSettingsViewModel(_viewModel.CurrentCase);
        var window = new CaseSettingsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
        {
            _viewModel.ReinitializeCourtroom();

            if (_viewModel.CurrentCase.TrialPhase != originalPhase)
            {
                var message = $"Trial phase changed from {originalPhase} to {_viewModel.CurrentCase.TrialPhase}. " +
                    "You can now modify inputs from this phase and earlier phases. " +
                    "For example, if you changed from Trial to Discovery, you can now add or modify evidence. " +
                    "Remember to proceed through the phases in order for best results.";
                MessageBox.Show(message, "Phase Changed - Capabilities Updated",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _viewModel.UpdateWindowTitle();
            MessageBox.Show("Case settings updated.");
        }
    }

    private void DefaultCaseSettings_Click(object sender, RoutedEventArgs e)
    {
        CaseFile defaultCase;
        try
        {
            defaultCase = _viewModel.GetDefaultSettings();
            if (defaultCase == null)
                defaultCase = new CaseFile { CaseName = "New Case", Mode = CaseMode.Civil,
                    Jurisdiction = JurisdictionType.State, JurorCount = 12 };
            else
                defaultCase.EnsureCollectionsInitialized();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading default settings: {ex.Message}. Creating new default settings.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            defaultCase = new CaseFile { CaseName = "New Case", Mode = CaseMode.Civil,
                Jurisdiction = JurisdictionType.State, JurorCount = 12 };
            defaultCase.EnsureCollectionsInitialized();
        }

        var originalPhase = defaultCase.TrialPhase;
        var viewModel = new CaseSettingsViewModel(defaultCase, true);
        var window = new CaseSettingsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
        {
            if (defaultCase.TrialPhase != originalPhase)
            {
                MessageBox.Show(
                    $"Default trial phase changed from {originalPhase} to {defaultCase.TrialPhase}. " +
                    "This will be applied to new cases.", "Default Phase Changed",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            _viewModel.SaveDefaultSettings(defaultCase);
            MessageBox.Show("Default case settings saved.");
        }
    }

    private void CaseCustomModels_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new ModelSettingsViewModel(
            _viewModel.CurrentCase.AvailableModels,
            _viewModel.CurrentCase.GlobalTemperature, _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors,
            _viewModel.CurrentCase.DefaultJurorModel, _viewModel.CurrentCase.DefaultJudgeModel,
            _viewModel.CurrentCase.DefaultProsecutionModel, _viewModel.CurrentCase.DefaultDefenseModel,
            _viewModel.CurrentCase.DefaultWitnessModel, _viewModel.CurrentCase.DefaultReporterModel,
            _viewModel.CurrentCase.DefaultClientModel);
        var window = new ModelSettingsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
            ApplyModelSettings(viewModel, persistToDefaults: false);
    }

    private void LLMConfiguration_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new ModelSettingsViewModel(
            _viewModel.EffectiveModels,
            _viewModel.CurrentCase.GlobalTemperature, _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors,
            _viewModel.CurrentCase.DefaultJurorModel, _viewModel.CurrentCase.DefaultJudgeModel,
            _viewModel.CurrentCase.DefaultProsecutionModel, _viewModel.CurrentCase.DefaultDefenseModel,
            _viewModel.CurrentCase.DefaultWitnessModel, _viewModel.CurrentCase.DefaultReporterModel,
            _viewModel.CurrentCase.DefaultClientModel);
        var window = new ModelSettingsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
        {
            ApplyModelSettings(viewModel, persistToDefaults: true);

            // Auto-assign newly configured model to agents without one
            if (viewModel.Models.Count > 0)
            {
                var preferredModel = viewModel.Models.LastOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.ApiKey) && m.ApiKey.Trim() != "sk-")
                    ?? viewModel.Models.LastOrDefault();

                if (preferredModel != null)
                {
                    if (string.IsNullOrWhiteSpace(preferredModel.ApiKey) || preferredModel.ApiKey.Trim() == "sk-")
                    {
                        MessageBox.Show(
                            $"Model '{preferredModel.FriendlyName}' has no API key configured.\n" +
                            "LLM calls will fail until you configure a valid API key.\n\n" +
                            "Open LLM Configuration, select the model, and click 'Configure...' to set your key.",
                            "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    foreach (var agent in _viewModel.AllAgents.Where(a => a.IsOccupied))
                    {
                        if (string.IsNullOrWhiteSpace(agent.SelectedModel))
                            agent.SelectedModel = preferredModel.FriendlyName;
                    }
                }
            }

            // Persist to default settings
            var defaults = _viewModel.GetDefaultSettings();
            defaults.AvailableModels.Clear();
            defaults.AvailableModels.AddRange(viewModel.Models);
            defaults.GlobalTemperature = viewModel.GlobalTemperature;
            defaults.GlobalMaxTokens = viewModel.GlobalMaxTokens;
            defaults.DefaultJurorModel = viewModel.DefaultJurorModel;
            defaults.DefaultJudgeModel = viewModel.DefaultJudgeModel;
            defaults.DefaultProsecutionModel = viewModel.DefaultProsecutionModel;
            defaults.DefaultDefenseModel = viewModel.DefaultDefenseModel;
            defaults.DefaultWitnessModel = viewModel.DefaultWitnessModel;
            defaults.DefaultReporterModel = viewModel.DefaultReporterModel;
            defaults.DefaultClientModel = viewModel.DefaultClientModel;
            defaults.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.DefaultBiasFactors)
                defaults.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
            _viewModel.SaveDefaultSettings(defaults);

            MessageBox.Show("LLM Configuration updated and saved.");
        }
    }

    private void ModelWeights_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new ModelSettingsViewModel(
            _viewModel.CurrentCase.AvailableModels,
            _viewModel.CurrentCase.GlobalTemperature,
            _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors);

        var window = new ModelWeightsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
        {
            _viewModel.CurrentCase.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.GetCurrentBiasWeightFactors())
                _viewModel.CurrentCase.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });

            var defaults = _viewModel.GetDefaultSettings();
            defaults.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.GetCurrentBiasWeightFactors())
                defaults.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
            _viewModel.SaveDefaultSettings(defaults);

            MessageBox.Show("Model weights updated and saved.", "Model Weights",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Applies model settings from a ModelSettingsViewModel to the current case.
    /// When persistToDefaults is true, saves to default settings too.
    /// </summary>
    private void ApplyModelSettings(ModelSettingsViewModel viewModel, bool persistToDefaults)
    {
        if (viewModel.Models.Count > 0)
        {
            _viewModel.CurrentCase.AvailableModels.Clear();
            _viewModel.CurrentCase.AvailableModels.AddRange(viewModel.Models);
        }
        _viewModel.CurrentCase.GlobalTemperature = viewModel.GlobalTemperature;
        _viewModel.CurrentCase.GlobalMaxTokens = viewModel.GlobalMaxTokens;
        _viewModel.CurrentCase.DefaultJurorModel = viewModel.DefaultJurorModel;
        _viewModel.CurrentCase.DefaultJudgeModel = viewModel.DefaultJudgeModel;
        _viewModel.CurrentCase.DefaultProsecutionModel = viewModel.DefaultProsecutionModel;
        _viewModel.CurrentCase.DefaultDefenseModel = viewModel.DefaultDefenseModel;
        _viewModel.CurrentCase.DefaultWitnessModel = viewModel.DefaultWitnessModel;
        _viewModel.CurrentCase.DefaultReporterModel = viewModel.DefaultReporterModel;
        _viewModel.CurrentCase.DefaultClientModel = viewModel.DefaultClientModel;

        if (persistToDefaults)
        {
            _viewModel.CurrentCase.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.DefaultBiasFactors)
                _viewModel.CurrentCase.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
        }
    }

    // ── Jury generation ──

    private void GenerateJury_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GenerateJury();
        MessageBox.Show("Jury generated based on case jurisdiction demographics.");
    }

    private void GenerateAlternates_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GenerateAlternates(2);
        MessageBox.Show("Alternate jurors generated based on case jurisdiction demographics.");
    }

    // ── Agent interaction dialogs ──

    private void CounselDiscussion_Click(object sender, RoutedEventArgs e)
    {
        var window = new CounselDiscussionWindow(_viewModel) { Owner = this };
        window.ShowDialog();
    }

    private void ClientConsultation_Click(object sender, RoutedEventArgs e)
    {
        var clients = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Client && a.IsOccupied).ToList();
        var lawyers = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Lawyer && a.IsOccupied).ToList();

        if (!clients.Any() || !lawyers.Any())
        {
            MessageBox.Show("Need at least one seated Client and one seated Lawyer.");
            return;
        }

        var window = new ClientConversationWindow(_viewModel, clients.First(), lawyers.First()) { Owner = this };
        window.ShowDialog();
    }

    // ── Reports ──

    private void GenerateReport_Click(object sender, RoutedEventArgs e)
    {
        var reportText = _viewModel.GenerateTextReport();
        var reportWindow = new ReportWindow(reportText, _viewModel.CurrentCase.CaseName ?? "Case Report") { Owner = this };
        reportWindow.ShowDialog();
    }

    private void DynamicRoleAssignment_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will analyze the current case and automatically assign agents to recommended roles. Continue?",
            "Dynamic Role Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ApplyDynamicRoleAssignment();
            MessageBox.Show("Dynamic role assignment completed. Check the court record for details.",
                "Role Assignment", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ── Other menu handlers ──

    private void GenerateCase_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CaseSelectionDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedFilePath != null)
            {
                if (MessageBox.Show("Load this demo case? Unsaved changes will be lost.",
                    "Load Demo", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    _viewModel.LoadCase(dialog.SelectedFilePath);
            }
            else
            {
                if (MessageBox.Show("Start a new blank case? Unsaved changes will be lost.",
                    "New Case", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    _viewModel.NewCase();
            }
        }
    }

    private void LoadAgent_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Load Agent feature coming soon.");
    private void BuildAgent_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Build Agent feature coming soon.");
    private void DesignPrompts_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Design Prompts feature coming soon.");
    private void BatchUpdatePrompts_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Batch Update feature coming soon.");
    private void GeneralSettings_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Settings feature coming soon.");
    private void CourtroomLayoutSettings_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Layout Settings feature coming soon.");

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Verdict - Jury Verdict Modeling Simulation\nVersion 1.0", "About Verdict");
    }
}
