using System;
using System.Linq;
using System.Windows;

namespace Verdict;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--test"))
        {
            // Test functionality is disabled until Tests project is integrated
            MessageBox.Show("Tests are not currently integrated with the main project.", 
                "Test Mode", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
