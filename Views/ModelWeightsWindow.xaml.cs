using System.Windows;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class ModelWeightsWindow : Window
{
    public ModelSettingsViewModel ViewModel => (ModelSettingsViewModel)DataContext;

    public ModelWeightsWindow(ModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    // The following click handlers are referenced by ModelWeightsWindow.xaml.

    /// <summary>
    /// Resets all weight sliders to research-backed defaults documented in docs/JurorBiasResearch.md.
    /// These values represent the relative strength of each factor's influence on juror perception
    /// based on meta-analyses of mock jury studies, RAND civil jury data, and published research.
    /// </summary>
    private void ResearchDefaults_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.55;
        ViewModel.GenderBiasWeight = 0.45;
        ViewModel.EducationBiasWeight = 0.80;
        ViewModel.IncomeBiasWeight = 0.50;
        ViewModel.PoliticalBiasWeight = 0.90;
        ViewModel.EthnicityBiasWeight = 0.60;
        ViewModel.ReligionBiasWeight = 0.40;
        ViewModel.JurorExperienceWeight = 0.70;
        ViewModel.LegalKnowledgeWeight = 0.75;
        ViewModel.ProfessionalBackgroundWeight = 0.55;
        ViewModel.CommunityTiesWeight = 0.65;
        ViewModel.CommunicationStyleWeight = 0.35;

        // Also sync the DefaultBiasFactors collection so case saves/loads reflect research values
        SyncDefaultBiasFactors();
    }

    /// <summary>
    /// Sets all weights to zero — effectively disabling demographic bias influence.
    /// Useful for testing the engine with only explicit evidence influence.
    /// </summary>
    private void ZeroAllWeights_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Set all weights to zero? This removes all demographic bias influences.",
                "Confirm Zero All", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ViewModel.AgeBiasWeight = 0.0;
            ViewModel.GenderBiasWeight = 0.0;
            ViewModel.EducationBiasWeight = 0.0;
            ViewModel.IncomeBiasWeight = 0.0;
            ViewModel.PoliticalBiasWeight = 0.0;
            ViewModel.EthnicityBiasWeight = 0.0;
            ViewModel.ReligionBiasWeight = 0.0;
            ViewModel.JurorExperienceWeight = 0.0;
            ViewModel.LegalKnowledgeWeight = 0.0;
            ViewModel.ProfessionalBackgroundWeight = 0.0;
            ViewModel.CommunityTiesWeight = 0.0;
            ViewModel.CommunicationStyleWeight = 0.0;
            SyncDefaultBiasFactors();
        }
    }

    private void BalancedWeights_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.5;
        ViewModel.GenderBiasWeight = 0.5;
        ViewModel.EducationBiasWeight = 0.5;
        ViewModel.IncomeBiasWeight = 0.5;
        ViewModel.PoliticalBiasWeight = 0.5;
        ViewModel.EthnicityBiasWeight = 0.5;
        ViewModel.ReligionBiasWeight = 0.5;
        ViewModel.JurorExperienceWeight = 0.5;
        ViewModel.LegalKnowledgeWeight = 0.5;
        ViewModel.ProfessionalBackgroundWeight = 0.5;
        ViewModel.CommunityTiesWeight = 0.5;
        ViewModel.CommunicationStyleWeight = 0.5;
        SyncDefaultBiasFactors();
    }

    private void ExpertFocus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.0;
        ViewModel.GenderBiasWeight = 0.0;
        ViewModel.EducationBiasWeight = 0.0;
        ViewModel.IncomeBiasWeight = 0.0;
        ViewModel.PoliticalBiasWeight = 0.0;
        ViewModel.EthnicityBiasWeight = 0.0;
        ViewModel.ReligionBiasWeight = 0.0;
        ViewModel.CommunicationStyleWeight = 0.0;

        ViewModel.JurorExperienceWeight = 0.9;
        ViewModel.LegalKnowledgeWeight = 0.9;
        ViewModel.ProfessionalBackgroundWeight = 0.9;
        ViewModel.CommunityTiesWeight = 0.0;
        SyncDefaultBiasFactors();
    }

    private void DiversePerspective_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AgeBiasWeight = 0.7;
        ViewModel.GenderBiasWeight = 0.7;
        ViewModel.EducationBiasWeight = 0.7;
        ViewModel.IncomeBiasWeight = 0.7;
        ViewModel.PoliticalBiasWeight = 0.7;
        ViewModel.EthnicityBiasWeight = 0.7;
        ViewModel.ReligionBiasWeight = 0.7;
        ViewModel.CommunicationStyleWeight = 0.7;

        ViewModel.JurorExperienceWeight = 0.4;
        ViewModel.LegalKnowledgeWeight = 0.4;
        ViewModel.ProfessionalBackgroundWeight = 0.4;
        ViewModel.CommunityTiesWeight = 0.7;
        SyncDefaultBiasFactors();
    }

    /// <summary>
    /// Synchronizes the DefaultBiasFactors collection with the current slider values.
    /// Ensures that saved case files and default-case settings reflect what the
    /// user selected in the weight configuration window.
    /// </summary>
    private void SyncDefaultBiasFactors()
    {
        ViewModel.DefaultBiasFactors.Clear();
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Age", Weight = ViewModel.AgeBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Gender", Weight = ViewModel.GenderBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Education Level", Weight = ViewModel.EducationBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Income Level", Weight = ViewModel.IncomeBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Political Affiliation", Weight = ViewModel.PoliticalBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Ethnicity", Weight = ViewModel.EthnicityBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Religion", Weight = ViewModel.ReligionBiasWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Juror Experience", Weight = ViewModel.JurorExperienceWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Legal Knowledge", Weight = ViewModel.LegalKnowledgeWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Professional Background", Weight = ViewModel.ProfessionalBackgroundWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Community Ties", Weight = ViewModel.CommunityTiesWeight });
        ViewModel.DefaultBiasFactors.Add(new Models.BiasFactor { Name = "Communication Style", Weight = ViewModel.CommunicationStyleWeight });
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        // If the ViewModel exposes persistence/apply logic, prefer it.
        // Otherwise, treat Apply as closing the dialog successfully.
        DialogResult = true;
        Close();
    }
}

