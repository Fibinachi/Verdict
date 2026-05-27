using System;
using System.Windows;
using Microsoft.Win32;

namespace Verdict.Views;

public partial class TrialReportWindow : Window
{
    public TrialReportWindow(string report, string caseName = "")
    {
        InitializeComponent();
        ReportTextBox.Text = report;
        CaseNameLabel.Text = string.IsNullOrWhiteSpace(caseName) ? "" : $"— {caseName}";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ReportTextBox.Text);
        MessageBox.Show("Report copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"Verdict_Report_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, ReportTextBox.Text);
            MessageBox.Show($"Report saved to:\n{dialog.FileName}", "Export Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
