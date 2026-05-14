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

    private void ResetAllWeights_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to reset all weights to 0? This will remove all bias adjustments.",
                "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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

        ViewModel.JurorExperienceWeight = 0.9;
        ViewModel.LegalKnowledgeWeight = 0.9;
        ViewModel.ProfessionalBackgroundWeight = 0.9;
        ViewModel.CommunityTiesWeight = 0.0;
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

        ViewModel.JurorExperienceWeight = 0.4;
        ViewModel.LegalKnowledgeWeight = 0.4;
        ViewModel.ProfessionalBackgroundWeight = 0.4;
        ViewModel.CommunityTiesWeight = 0.7;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        // If the ViewModel exposes persistence/apply logic, prefer it.
        // Otherwise, treat Apply as closing the dialog successfully.
        DialogResult = true;
        Close();
    }
}

