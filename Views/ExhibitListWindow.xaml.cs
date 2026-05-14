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

        // Selection editing: double-click an exhibit to edit the Clerk Notes field.
        ExhibitListView.SelectionMode = System.Windows.Controls.SelectionMode.Single;
    }

    private void ExhibitListItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)

    {
        // Minimal implementation to satisfy the XAML handler.
        // This is intentionally kept simple: edit ClerkNotes field only.
        if (ExhibitListView.SelectedItem is not EvidenceDocument doc) return;

        var dialog = new Window
        {
            Owner = this,
            Title = $"Clerk Notes - Exhibit #{doc.ExhibitNumber}",
            Width = 650,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = doc.ClerkNotes ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(12)
        };

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
            dialog.DialogResult = true;
            dialog.Close();
        };

        var panel = new System.Windows.Controls.DockPanel();

        System.Windows.Controls.DockPanel.SetDock(save, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(save);
        panel.Children.Add(textBox);
        dialog.Content = panel;

        if (dialog.ShowDialog() == true)
        {
            doc.ClerkNotes = textBox.Text;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}

