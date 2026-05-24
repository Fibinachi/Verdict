using System.Windows;
using Verdict.ViewModels;
using Verdict.Models;

namespace Verdict.Views;

public partial class ExhibitListWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ExhibitListWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RefreshExhibits();

        // Selection editing: double-click an exhibit to edit the exhibit fields.
        // ExhibitListView is defined in XAML.
        this.ExhibitListView.SelectionMode = System.Windows.Controls.SelectionMode.Single;

    }

    private void ExhibitListItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ExhibitListView.SelectedItem is not EvidenceDocument doc) return;

        var dialog = new Window
        {
            Owner = this,
            Title = $"Exhibit Details - #{doc.ExhibitNumber}",
            Width = 950,
            Height = 720,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize
        };

        var fileNameBox = new System.Windows.Controls.TextBox
        {
            Text = doc.FileName ?? string.Empty,
            Margin = new Thickness(12, 10, 12, 4)
        };

        var offeringAttorneyBox = new System.Windows.Controls.TextBox
        {
            Text = doc.OfferingAttorney ?? string.Empty,
            Margin = new Thickness(12, 10, 12, 4)
        };

        var mediaTypeBox = new System.Windows.Controls.TextBox
        {
            Text = doc.MediaType ?? string.Empty,
            Margin = new Thickness(12, 10, 12, 4)
        };

        var evidenceStrengthBox = new System.Windows.Controls.TextBox
        {
            Text = doc.EvidenceStrength.ToString("0.##"),
            Margin = new Thickness(12, 10, 12, 4)
        };

        var estimatedDamagesBox = new System.Windows.Controls.TextBox
        {
            Text = doc.EstimatedDamages.ToString("0.##"),
            Margin = new Thickness(12, 10, 12, 4)
        };

        var plaintiffSideBox = new System.Windows.Controls.TextBox
        {
            Text = doc.PlaintiffSideExhibitNumber.ToString(),
            Margin = new Thickness(12, 10, 12, 4)
        };

        var defenseSideBox = new System.Windows.Controls.TextBox
        {
            Text = doc.DefenseSideExhibitNumber.ToString(),
            Margin = new Thickness(12, 10, 12, 4)
        };

        var isOfferedByPlaintiffCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Offered by Plaintiff side",
            IsChecked = doc.IsOfferedByPlaintiffSide,
            Margin = new Thickness(12, 10, 12, 4)
        };

        var isDiscoveryCompleteCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Discovery complete",
            IsChecked = doc.IsDiscoveryComplete,
            Margin = new Thickness(12, 10, 12, 4)
        };

        var summaryBox = new System.Windows.Controls.TextBox
        {
            Text = doc.Summary ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(12),
            MinHeight = 100
        };

        var detailsBox = new System.Windows.Controls.TextBox
        {
            Text = doc.DetailedAnalysis ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(12),
            MinHeight = 200
        };

        var notesBox = new System.Windows.Controls.TextBox
        {
            Text = doc.ClerkNotes ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(12),
            MinHeight = 120
        };

        var stack = new System.Windows.Controls.StackPanel();

        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"Exhibit #{doc.ExhibitNumber}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Margin = new Thickness(12, 12, 12, 6)
        });

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "File Name", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(fileNameBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Offering Attorney", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(offeringAttorneyBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Media Type", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(mediaTypeBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Evidence Strength (0..1)", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(evidenceStrengthBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Estimated Damages", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(estimatedDamagesBox);

        stack.Children.Add(isOfferedByPlaintiffCheck);
        stack.Children.Add(isDiscoveryCompleteCheck);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Plaintiff-side Exhibit #", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(plaintiffSideBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Defense-side Exhibit #", Margin = new Thickness(12, 10, 12, 2), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(defenseSideBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Summary", Margin = new Thickness(12, 10, 12, 4), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(summaryBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Detailed Analysis", Margin = new Thickness(12, 10, 12, 4), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(detailsBox);

        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Clerk Notes", Margin = new Thickness(12, 10, 12, 4), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(notesBox);

        var save = new System.Windows.Controls.Button
        {
            Content = "Save",
            Width = 100,
            Height = 30,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        save.Click += (_, _) =>
        {
            doc.FileName = fileNameBox.Text;
            doc.OfferingAttorney = offeringAttorneyBox.Text;
            doc.MediaType = string.IsNullOrWhiteSpace(mediaTypeBox.Text) ? null : mediaTypeBox.Text;

            if (double.TryParse(evidenceStrengthBox.Text, out var es))
                doc.EvidenceStrength = Math.Clamp(es, 0.0, 1.0);

            if (double.TryParse(estimatedDamagesBox.Text, out var ed))
                doc.EstimatedDamages = Math.Max(0.0, ed);

            doc.IsOfferedByPlaintiffSide = isOfferedByPlaintiffCheck.IsChecked == true;
            doc.IsDiscoveryComplete = isDiscoveryCompleteCheck.IsChecked == true;

            if (int.TryParse(plaintiffSideBox.Text, out var pi)) doc.PlaintiffSideExhibitNumber = pi;
            if (int.TryParse(defenseSideBox.Text, out var di)) doc.DefenseSideExhibitNumber = di;

            doc.Summary = summaryBox.Text;
            doc.DetailedAnalysis = detailsBox.Text;
            doc.ClerkNotes = notesBox.Text;

            dialog.DialogResult = true;
            dialog.Close();
        };

        var bottom = new System.Windows.Controls.DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(12, 12, 12, 8)
        };

        System.Windows.Controls.DockPanel.SetDock(save, System.Windows.Controls.Dock.Right);
        bottom.Children.Add(save);

        var root = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(bottom, System.Windows.Controls.Dock.Bottom);
        root.Children.Add(bottom);

        root.Children.Add(new System.Windows.Controls.ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        });

        dialog.Content = root;

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshExhibits();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}

