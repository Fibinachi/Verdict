using System.Windows;

namespace Verdict.Views;

public partial class ReportWindow : Window
{
    public ReportWindow(string reportText, string caseName)
    {
        InitializeComponent();
        ReportTextBox.Text = reportText;
        Title = $"Case Report — {caseName}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "txt",
            FileName = "Case_Report.txt"
        };
        if (dialog.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, ReportTextBox.Text);
            MessageBox.Show("Report saved successfully.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
