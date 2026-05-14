using System.Collections.Generic;
using System.Windows;

namespace Verdict.Views;

/// <summary>
/// Dialog for selecting a model from a list of available models
/// </summary>
public partial class ModelSelectionDialog : Window
{
    public string SelectedModelId { get; private set; } = string.Empty;
    
    public ModelSelectionDialog(List<string> availableModels)
    {
        InitializeComponent();
        ModelList.ItemsSource = availableModels;
        Title = "Select Model";
    }
    
    private void SelectModel_Click(object sender, RoutedEventArgs e)
    {
        if (ModelList.SelectedItem is string selectedModel)
        {
            SelectedModelId = selectedModel;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please select a model.");
        }
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}