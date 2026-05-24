using System;
using System.Windows;

namespace Verdict.Views;

public partial class TextEvidenceDialog : Window
{
    public string EvidenceText => EvidenceTextBox.Text;
    public string EvidenceFactorsText => EvidenceFactorsBox.Text;
    public string ReporterQuestionsText => ReporterQuestionsBox.Text;
    public string OfferingAttorney => OfferingAttorneyBox.Text;
    public bool OfferedByPlaintiff => OfferedByPlaintiffCheck.IsChecked == true;

    public TextEvidenceDialog()
    {
        InitializeComponent();
    }

    public TextEvidenceDialog(string offeringAttorney, bool offeredByPlaintiff)
        : this()
    {
        OfferingAttorneyBox.Text = offeringAttorney ?? string.Empty;
        OfferedByPlaintiffCheck.IsChecked = offeredByPlaintiff;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EvidenceText))
        {
            MessageBox.Show("Evidence text cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}

