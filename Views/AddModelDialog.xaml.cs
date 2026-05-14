using System.Collections.Generic;
using System.Windows;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Views;

public partial class AddModelDialog : Window
{
    public ILLMProviderModule? SelectedProvider { get; private set; }
    public List<ILLMProviderModule> AvailableProviders { get; }

    public AddModelDialog()
    {
        InitializeComponent();
        AvailableProviders = ProviderDiscoveryService.GetAvailableProviders();
        DataContext = this;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderList.SelectedItem is ILLMProviderModule provider)
        {
            SelectedProvider = provider;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please select a provider.");
        }
    }
}
