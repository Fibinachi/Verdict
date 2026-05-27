using System.Windows;
using Verdict.Models;
using Verdict.ViewModels;
using Verdict.Services;

namespace Verdict.Views;

public partial class ModelSettingsWindow : Window
{
    public ModelSettingsViewModel ViewModel => (ModelSettingsViewModel)DataContext;

    public ModelSettingsWindow(ModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void AddModel_Click(object sender, RoutedEventArgs e)
    {
        var addDialog = new AddModelDialog { Owner = this };
        if (addDialog.ShowDialog() == true && addDialog.SelectedProvider != null)
        {
            var provider = addDialog.SelectedProvider;
            var newModel = new AIModelConfiguration 
            { 
                Provider = provider.ProviderName,
                FriendlyName = provider.DefaultFriendlyName
            };

            // Set default values from provider
            foreach (var field in provider.ConfigFields)
            {
                if (field.Key == "ModelId") newModel.ModelId = field.DefaultValue;
                else if (field.Key == "Endpoint") newModel.Endpoint = field.DefaultValue;
                else if (field.Key == "ApiKey") newModel.ApiKey = field.DefaultValue;
                else newModel.CustomSettings[field.Key] = field.DefaultValue;
            }

            var detailsDialog = new ModelDetailsDialog(newModel, provider) { Owner = this };
            if (detailsDialog.ShowDialog() == true)
            {
                ViewModel.Models.Add(newModel);
                ViewModel.SelectedModel = newModel;
            }
        }
    }

    private void ConfigureModel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedModel != null)
        {
            var provider = ProviderDiscoveryService.GetProviderByName(ViewModel.SelectedModel.Provider);
            if (provider != null)
            {
                var detailsDialog = new ModelDetailsDialog(ViewModel.SelectedModel, provider) { Owner = this };
                if (detailsDialog.ShowDialog() == true)
                {
                    // Persist immediately so test status and edits survive even if user cancels parent dialog
                    var settingsService = new SettingsService();
                    var currentDefaults = settingsService.GetDefaultCaseSettings();
                    // Replace or update the model in defaults
                    var existing = currentDefaults.AvailableModels
                        .FirstOrDefault(m => m.Provider == ViewModel.SelectedModel.Provider 
                                          && m.ModelId == ViewModel.SelectedModel.ModelId);
                    if (existing != null)
                    {
                        existing.FriendlyName = ViewModel.SelectedModel.FriendlyName;
                        existing.Status = ViewModel.SelectedModel.Status;
                        existing.ApiKey = ViewModel.SelectedModel.ApiKey;
                        existing.Endpoint = ViewModel.SelectedModel.Endpoint;
                        existing.ModelId = ViewModel.SelectedModel.ModelId;
                        existing.CustomSettings = ViewModel.SelectedModel.CustomSettings;
                    }
                    else if (!currentDefaults.AvailableModels.Any(m => 
                        m.Provider == ViewModel.SelectedModel.Provider && m.ModelId == ViewModel.SelectedModel.ModelId))
                    {
                        currentDefaults.AvailableModels.Add(ViewModel.SelectedModel);
                    }
                    currentDefaults.EnsureCollectionsInitialized();
                    settingsService.SaveDefaultCaseSettings(currentDefaults);
                }
            }
            else
            {
                MessageBox.Show("Provider module not found for this configuration.");
            }
        }
    }

    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to delete this model configuration?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            ViewModel.DeleteModel();
        }
    }

    private void RefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        // Add any new models from folder without clearing existing
        ViewModel.LoadFromFolder();
        MessageBox.Show("Models folder scanned - new models added.");
    }

    private void SaveToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedModel != null)
        {
            ViewModel.SaveSelectedToFolder();
            MessageBox.Show("Selected model saved to Models/ folder.");
        }
    }

    private void SetBiasDefaults_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.5;
        ViewModel.GenderBiasWeight = 0.5;
        ViewModel.EducationBiasWeight = 0.5;
        ViewModel.IncomeBiasWeight = 0.5;
        ViewModel.PoliticalBiasWeight = 0.5;
        ViewModel.EthnicityBiasWeight = 0.5;
        ViewModel.ReligionBiasWeight = 0.5;
    }

    private void ResetBiasDefaults_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.0;
        ViewModel.GenderBiasWeight = 0.0;
        ViewModel.EducationBiasWeight = 0.0;
        ViewModel.IncomeBiasWeight = 0.0;
        ViewModel.PoliticalBiasWeight = 0.0;
        ViewModel.EthnicityBiasWeight = 0.0;
        ViewModel.ReligionBiasWeight = 0.0;
    }

    private void OpenWeights_Click(object sender, RoutedEventArgs e)
    {
        var weightsWindow = new ModelWeightsWindow(ViewModel) { Owner = this };
        weightsWindow.ShowDialog();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
