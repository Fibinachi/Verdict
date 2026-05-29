using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Verdict.ViewModels;
using Verdict.Models;
using Verdict.Services;
using Verdict.Views;

namespace Verdict;

/// <summary>
/// Core MainWindow: construction, chat input, file operations (open/save/recent), and transcript import.
/// Agent, evidence, stage, and settings handlers are in separate partial class files.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel _viewModel;
    private readonly ISettingsService _settingsService = new SettingsService();

    /// <summary>Exposes all agents in the current case for cross-window access.</summary>
    public IEnumerable<Agent> GetAllAgents() => _viewModel.AllAgents;

    /// <summary>Prosecution/Plaintiff team agents.</summary>
    public System.Collections.ObjectModel.ObservableCollection<Agent> ProsecutionTeam => _viewModel.ProsecutionTeam;

    /// <summary>Defense team agents.</summary>
    public System.Collections.ObjectModel.ObservableCollection<Agent> DefenseTeam => _viewModel.DefenseTeam;

    static MainWindow()
    {
        System.Console.WriteLine("MAINWINDOW STATIC CTOR");
    }

    public MainWindow()
    {
        System.Console.WriteLine("MAINWINDOW CTOR START");
        InitializeComponent();
        System.Console.WriteLine("MAINWINDOW INITIALIZECOMPONENT DONE");
        _viewModel = new MainViewModel();
        System.Console.WriteLine("MAINWINDOW VIEWMODEL DONE");
        this.DataContext = _viewModel;
        PopulateRecentFilesMenu();
        System.Console.WriteLine("MAINWINDOW CTOR DONE");

        // Auto-resume any interrupted downloads from previous sessions
        _ = ResumeDownloadsOnStartupAsync();
    }

    /// <summary>
    /// Scans for incomplete downloads from previous sessions and resumes them
    /// in the background. Shows a notification when each completes or fails.
    /// </summary>
    private async Task ResumeDownloadsOnStartupAsync()
    {
        // Brief delay to let the UI finish loading before we start
        await Task.Delay(500);

        await ModelDownloadService.ResumeAllIncompleteDownloadsAsync((modelId, success) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (success)
                {
                    var shortName = modelId.Split('/').LastOrDefault() ?? modelId;
                    TranscriptOutputAddLine($"[DOWNLOAD] Resumed: {shortName} — download complete.");
                }
                else
                {
                    var shortName = modelId.Split('/').LastOrDefault() ?? modelId;
                    TranscriptOutputAddLine($"[DOWNLOAD] Failed to resume: {shortName}. You can retry from Model Settings.");
                }
            });
        });
    }

    private void TranscriptOutputAddLine(string text)
    {
        _viewModel.TranscriptOutput += "\n" + text;
    }

    // Chat / Podium

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            string content = ChatInput.Text.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                _viewModel.ProcessTranscriptLine("Attorney/Moderator", content);
                ChatInput.Clear();
            }
        }
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        string content = ChatInput.Text.Trim();
        if (!string.IsNullOrEmpty(content))
        {
            _viewModel.ProcessTranscriptLine("Attorney/Moderator", content);
            ChatInput.Clear();
        }
    }

    private void Podium_Click(object sender, RoutedEventArgs e)
    {
        ChatInput.Focus();
    }

    // File: Save / Open / New / Exit

    private void SaveSimulation_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Verdict Case (*.jur)|*.jur",
            DefaultExt = "jur"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            _viewModel.SaveCase(saveFileDialog.FileName);
            _settingsService.AddRecentFile(saveFileDialog.FileName);
            PopulateRecentFilesMenu();
            MessageBox.Show("Simulation saved.");
        }
    }

    private void SaveCaseAs_Click(object sender, RoutedEventArgs e)
    {
        SaveSimulation_Click(sender, e);
    }

    private void NewCase_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Start a new case? All unsaved changes will be lost.", "New Case",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _viewModel.NewCase();
        }
    }

    private void OpenCase_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Verdict Case (*.jur)|*.jur",
            DefaultExt = "jur"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _viewModel.LoadCase(openFileDialog.FileName);
            _settingsService.AddRecentFile(openFileDialog.FileName);
            PopulateRecentFilesMenu();
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // Default Cases

    private void LoadDefaultCase(string caseFileName)
    {
        if (_viewModel.CurrentCase?.Agents?.Count > 0 &&
            MessageBox.Show("Load a default case? Any unsaved changes will be lost.", "Default Case",
            MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        string[] searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DefaultCases", caseFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "DefaultCases", caseFileName)
        };

        string? casePath = searchPaths.FirstOrDefault(File.Exists);
        if (casePath == null)
        {
            MessageBox.Show($"Default case file '{caseFileName}' not found.", "Default Case",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _viewModel.LoadCase(casePath);
    }

    private void DefaultCase_AppleRiver_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("apple-river.jur");

    private void DefaultCase_AppleJuice_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("apple-juice-murders.jur");

    private void DefaultCase_NCAccident_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("nc-comparative-fault-accident.jur");

    private void DefaultCase_SmokingMemo_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("smoking-memo.jur");

    private void DefaultCase_LockedRoom_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("circumstantial-locked-room.jur");

    private void DefaultCase_FewBadApples_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("few-bad-apples.jur");

    private void DefaultCase_TwelveAgents_Click(object sender, RoutedEventArgs e) =>
        LoadDefaultCase("twelve-mildly-bothered-agents.jur");

    // Transcript

    private async void ImportTranscript_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await _viewModel.LoadTranscript(openFileDialog.FileName);
        }
    }

    private async void ExtractEntities_Click(object sender, RoutedEventArgs e)
    {
        var transcript = _viewModel.TranscriptOutput;
        if (string.IsNullOrEmpty(transcript) || transcript == "Court is in session. Awaiting transcript...")
        {
            MessageBox.Show("No transcript loaded. Please import a transcript first.", "Extract Entities",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_viewModel.CurrentCase.AvailableModels.Any())
        {
            MessageBox.Show("No AI model configured. Please configure an LLM first.", "Extract Entities",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _viewModel.ExtractEntitiesFromTranscriptAsync();
            MessageBox.Show("Entity extraction complete. Check the court record for extracted charges, evidence, and witnesses.",
                "Extract Entities", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Entity extraction failed: {ex.Message}", "Extract Entities",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Recent Files Menu

    private void PopulateRecentFilesMenu()
    {
        var recentFiles = _settingsService.GetRecentFiles();
        RecentFilesMenu.Items.Clear();

        if (recentFiles.Count == 0)
        {
            RecentFilesMenu.IsEnabled = false;
            RecentFilesMenu.Visibility = Visibility.Collapsed;
            return;
        }

        RecentFilesMenu.IsEnabled = true;
        RecentFilesMenu.Visibility = Visibility.Visible;

        for (int i = 0; i < recentFiles.Count; i++)
        {
            var filePath = recentFiles[i];
            var displayName = $"{i + 1}. {Path.GetFileNameWithoutExtension(filePath)}";
            var menuItem = new MenuItem
            {
                Header = displayName,
                ToolTip = filePath,
                Tag = filePath
            };
            menuItem.Click += RecentFileMenuItem_Click;
            RecentFilesMenu.Items.Add(menuItem);
        }

        RecentFilesMenu.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += (_, _) =>
        {
            _settingsService.ClearRecentFiles();
            PopulateRecentFilesMenu();
        };
        RecentFilesMenu.Items.Add(clearItem);
    }

    private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
        {
            if (File.Exists(filePath))
            {
                _viewModel.LoadCase(filePath);
            }
            else
            {
                var result = MessageBox.Show(
                    $"The file '{filePath}' no longer exists.\nRemove it from the recent files list?",
                    "File Not Found", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _settingsService.RemoveRecentFile(filePath);
                    PopulateRecentFilesMenu();
                }
            }
        }
    }
}
