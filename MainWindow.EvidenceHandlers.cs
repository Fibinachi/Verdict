using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Verdict.Models;
using Verdict.Views;

namespace Verdict;

/// <summary>
/// Evidence-related event handlers: document intake, clerk seat drop, exhibit list, re-evaluation.
/// </summary>
public partial class MainWindow
{
    private string PickAttorney()
    {
        var lawyers = _viewModel.DefenseTeam.Concat(_viewModel.ProsecutionTeam)
            .Where(a => a.IsOccupied && a.Role == AgentRole.Lawyer)
            .ToList();

        if (lawyers.Count == 0) return string.Empty;
        if (lawyers.Count == 1) return lawyers[0].Name;

        var dialog = new Window
        {
            Title = "Select Offering Attorney",
            Width = 320, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize
        };
        var combo = new ComboBox
        {
            Margin = new Thickness(12, 12, 12, 8),
            ItemsSource = lawyers.Select(l => l.Name).ToList(),
            SelectedIndex = 0
        };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 12, 12) };
        ok.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
        var panel = new StackPanel();
        panel.Children.Add(combo);
        panel.Children.Add(ok);
        dialog.Content = panel;
        return dialog.ShowDialog() == true ? combo.SelectedItem?.ToString() ?? string.Empty : string.Empty;
    }

    private async void AddEvidence_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Evidence (*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg)|*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg|Documents (*.pdf;*.txt;*.docx;*.rtf)|*.pdf;*.txt;*.docx;*.rtf|Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Video (*.mp4;*.mov;*.avi;*.wmv;*.mkv)|*.mp4;*.mov;*.avi;*.wmv;*.mkv|Audio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All Files (*.*)|*.*"
        };
        if (openFileDialog.ShowDialog() == true)
        {
            string attorney = PickAttorney();
            await _viewModel.AddEvidence(
                Path.GetFileName(openFileDialog.FileName),
                openFileDialog.FileName,
                "Admitted Document",
                null,
                attorney
            );
            MessageBox.Show("Document admitted to court record and memory broadcast.");
        }
    }

    private async void ClerkSeat_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Evidence (*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg)|*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg|Documents (*.pdf;*.txt;*.docx;*.rtf)|*.pdf;*.txt;*.docx;*.rtf|Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Video (*.mp4;*.mov;*.avi;*.wmv;*.mkv)|*.mp4;*.mov;*.avi;*.wmv;*.mkv|Audio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != true) return;

        string clerkAttorney = PickAttorney();
        int processedCount = 0;

        foreach (var fileName in openFileDialog.FileNames)
        {
            string filePath = fileName;
            string fileNameOnly = Path.GetFileName(fileName);

            string fileContent = _viewModel.ReadFileContent(filePath);
            var (detailedAnalysis, _) = await _viewModel.GenerateDocumentAnalysisAsync(
                fileNameOnly, fileContent, "Submitted via Court Clerk");

            string defaultSummary = $"Document: {fileNameOnly}\n\n" +
                $"User Notes: Submitted via Court Clerk\n\n" +
                $"[AI ANALYSIS]\n{detailedAnalysis}";

            var editSummaryDialog = new Window
            {
                Title = $"Review & Edit - {fileNameOnly}",
                Width = 650, Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.CanResize
            };

            var instructionLabel = new Label
            {
                Content = "Edit the summary below. This will be broadcast to all agents as their memory of the document:",
                Margin = new Thickness(10), FontWeight = FontWeights.SemiBold
            };
            var textBox = new TextBox
            {
                Text = defaultSummary, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10), Height = 300
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            var okButton = new Button { Content = "Submit to Agents", Margin = new Thickness(5),
                Width = 120, Height = 30, Background = Brushes.DarkGreen,
                Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(5),
                Width = 80, Height = 30 };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(instructionLabel);
            mainPanel.Children.Add(textBox);
            mainPanel.Children.Add(buttonPanel);
            editSummaryDialog.Content = mainPanel;

            RoutedEventHandler okClick = (s, args) => { editSummaryDialog.DialogResult = true; editSummaryDialog.Close(); };
            RoutedEventHandler cancelClick = (s, args) => { editSummaryDialog.DialogResult = false; editSummaryDialog.Close(); };
            okButton.Click += okClick;
            cancelButton.Click += cancelClick;

            if (editSummaryDialog.ShowDialog() == true)
            {
                await _viewModel.AddEvidence(fileNameOnly, filePath, textBox.Text, detailedAnalysis, clerkAttorney);
                var reporter = _viewModel.JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter);
                if (reporter != null)
                    reporter.DocumentCount++;
                processedCount++;
            }
        }
        MessageBox.Show($"{processedCount} document(s) submitted and parsed into evidence.");
    }

    private async void AgentSeat_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var border = sender as FrameworkElement;
        var agent = border?.DataContext as Agent;

        if (agent != null && agent.Role == AgentRole.Witness)
        {
            foreach (var file in files)
            {
                await _viewModel.AddEvidence(
                    Path.GetFileName(file), file,
                    $"Document processed via {agent.Name}");
            }
            MessageBox.Show($"{files.Length} document(s) dropped into Witness Box.");
        }
    }

    private void ExhibitList_Click(object sender, RoutedEventArgs e)
    {
        var exhibitWindow = new ExhibitListWindow(_viewModel) { Owner = this };
        exhibitWindow.ShowDialog();
    }

    private async void ReevaluateEvidence_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReevaluateEvidenceAsync();
    }
}
