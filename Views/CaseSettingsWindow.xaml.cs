using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Verdict.ViewModels;
using Verdict.Models;

namespace Verdict.Views
{
    /// <summary>
    /// Interaction logic for CaseSettingsWindow.xaml
    /// </summary>
    public partial class CaseSettingsWindow : Window
    {
        private CaseSettingsViewModel _viewModel;

        public CaseSettingsWindow(CaseSettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            this.Title = viewModel.IsDefaultSettings ? "Default Case Settings" : "Case Settings";
            
            // Store the original phase for comparison later
            Tag = viewModel.CaseFile.TrialPhase;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Check if the trial phase has changed and warn the user
            var currentPhase = _viewModel.CaseFile.TrialPhase;
            var originalPhase = (TrialPhase)Tag; // Retrieve original phase stored in Tag
            
            if (currentPhase != originalPhase)
            {
                var message = $"You have changed the trial phase from {originalPhase} to {currentPhase}. " +
                             "This may affect previously entered evidence and inputs. " +
                             "Are you sure you want to proceed? Note that changing to an earlier phase will allow you to modify inputs from that phase and earlier.";
                
                var result = MessageBox.Show(message, "Trial Phase Changed", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    DialogResult = false;
                    Close();
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}