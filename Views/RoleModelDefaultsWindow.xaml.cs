using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Verdict.Models;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class RoleModelDefaultsWindow : Window
{
    public ModelSettingsViewModel ViewModel => (ModelSettingsViewModel)DataContext;

    public RoleModelDefaultsWindow(ModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Populate all combo boxes with the available models
        var models = ViewModel.Models.ToList();

        PopulateCombo(JudgeCombo, models, ViewModel.DefaultJudgeModel);
        PopulateCombo(ProsecutionCombo, models, ViewModel.DefaultProsecutionModel);
        PopulateCombo(DefenseCombo, models, ViewModel.DefaultDefenseModel);
        PopulateCombo(WitnessCombo, models, ViewModel.DefaultWitnessModel);
        PopulateCombo(ReporterCombo, models, ViewModel.DefaultReporterModel);
        PopulateCombo(ClientCombo, models, ViewModel.DefaultClientModel);
        PopulateCombo(JurorCombo, models, ViewModel.DefaultJurorModel);
    }

    private static void PopulateCombo(ComboBox combo, System.Collections.Generic.List<AIModelConfiguration> models, string defaultFriendlyName)
    {
        combo.ItemsSource = models;
        if (!string.IsNullOrEmpty(defaultFriendlyName))
        {
            var match = models.FirstOrDefault(m => m.FriendlyName == defaultFriendlyName);
            if (match != null)
                combo.SelectedItem = match;
        }
    }

    private void ApplyToExisting_Click(object sender, RoutedEventArgs e)
    {
        // Read the currently selected model for each role from the combo boxes
        string? judgeModel = (JudgeCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? prosecutionModel = (ProsecutionCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? defenseModel = (DefenseCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? witnessModel = (WitnessCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? reporterModel = (ReporterCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? clientModel = (ClientCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;
        string? jurorModel = (JurorCombo.SelectedItem as AIModelConfiguration)?.FriendlyName;

        // Find the parent MainWindow to access the current case's agents
        var mainWindow = Owner as MainWindow;
        if (mainWindow == null)
        {
            MessageBox.Show("Could not find the main window to update agents.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        int updatedCount = 0;
        var allAgents = mainWindow.GetAllAgents();
        foreach (var agent in allAgents)
        {
            string? roleModel = agent.Role switch
            {
                AgentRole.Judge => judgeModel,
                AgentRole.Lawyer => DetermineLawyerModel(agent, mainWindow, prosecutionModel, defenseModel),
                AgentRole.Witness => witnessModel,
                AgentRole.Reporter => reporterModel,
                AgentRole.Client => clientModel,
                AgentRole.Juror => jurorModel,
                AgentRole.AlternateJuror => jurorModel,
                _ => null
            };

            if (!string.IsNullOrEmpty(roleModel))
            {
                agent.SelectedModel = roleModel;
                updatedCount++;
            }
        }

        MessageBox.Show($"Updated {updatedCount} existing agents to use their role's default model.",
            "Agents Updated", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Determines which model a Lawyer agent should use by checking whether they are
    /// in the ProsecutionTeam or DefenseTeam collection on the MainWindow.
    /// </summary>
    private static string? DetermineLawyerModel(Agent agent, MainWindow mainWindow,
        string? prosecutionModel, string? defenseModel)
    {
        if (mainWindow.ProsecutionTeam.Contains(agent))
            return prosecutionModel;
        if (mainWindow.DefenseTeam.Contains(agent))
            return defenseModel;
        return null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Save the selected models back to the ViewModel
        ViewModel.DefaultJudgeModel = (JudgeCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultProsecutionModel = (ProsecutionCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultDefenseModel = (DefenseCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultWitnessModel = (WitnessCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultReporterModel = (ReporterCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultClientModel = (ClientCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;
        ViewModel.DefaultJurorModel = (JurorCombo.SelectedItem as AIModelConfiguration)?.FriendlyName ?? string.Empty;

        DialogResult = true;
        Close();
    }
}
