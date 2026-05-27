using System;
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

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;
    private readonly ISettingsService _settingsService = new SettingsService();

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
    }

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

    private async void AdvanceStage_Click(object sender, RoutedEventArgs e)
    {
        // Turn-based deliberation step (repurposed button)
        await _viewModel.DeliberateNextTurn();
    }

    private async void AutoDeliberate_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AutoSimulateTrial();
    }




    private async void AgentSeat_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var border = sender as FrameworkElement;
            var agent = border?.DataContext as Agent;

            if (agent != null && agent.Role == AgentRole.Witness)
            {
                foreach (var file in files)
                {
                    await _viewModel.AddEvidence(
                        System.IO.Path.GetFileName(file),
                        file,
                        $"Document processed via {agent.Name}"
                    );
                }
                MessageBox.Show($"{files.Length} document(s) dropped into Witness Box.");
            }
        }
    }

    private void AddAgent_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            // Create a copy of the agent to avoid direct modifications if it's unoccupied
            var agentToEdit = agent.IsOccupied ? agent : new Agent 
            { 
                Role = agent.Role, 
                Name = agent.Name.StartsWith("+") ? "" : agent.Name 
            };

            // Apply role-appropriate defaults for new agents
            if (!agent.IsOccupied)
            {
                ApplyRoleDefaults(agentToEdit);
            }
            
            var profileWindow = new Views.AgentProfileWindow(agentToEdit, _viewModel.EffectiveModels)
            {
                Owner = this
            };

            if (profileWindow.ShowDialog() == true)
            {
                // Read back values from the wrapper (which holds the actual edits)
                // For occupied agents, agentToEdit is the same reference as agent,
                // but the dialog edits a separate AgentWithAvailableModels wrapper.
                if (profileWindow.DataContext is AgentWithAvailableModels wrapper)
                {
                    if (!agent.IsOccupied)
                    {
                        agent.Name = string.IsNullOrWhiteSpace(wrapper.Name) ? $"Active {wrapper.Role}" : wrapper.Name;
                        agent.Gender = wrapper.Gender;
                        agent.Age = wrapper.Age;
                        agent.Race = wrapper.Race;
                        agent.Occupation = wrapper.Occupation;
                        agent.EducationLevel = wrapper.EducationLevel;
                        agent.IncomeLevel = wrapper.IncomeLevel;
                        agent.ZipCode = wrapper.ZipCode;
                        agent.PoliticalAffiliation = wrapper.PoliticalAffiliation;
                        agent.ReligiousAffiliation = wrapper.ReligiousAffiliation;
                        agent.SpecializedKnowledge = wrapper.SpecializedKnowledge;
                        agent.MaritalStatus = wrapper.MaritalStatus;
                        agent.ParentalStatus = wrapper.ParentalStatus;
                        agent.SettlementAuthority = wrapper.SettlementAuthority;
                        agent.RiskPerception = wrapper.RiskPerception;
                        agent.MediaConsumption = wrapper.MediaConsumption;
                        agent.ConsumerSegment = wrapper.ConsumerSegment;
                        agent.Hobbies = wrapper.Hobbies;
                        agent.ViewingCharacteristics = wrapper.ViewingCharacteristics;
                        agent.CurrentStatus = wrapper.CurrentStatus;
                        agent.SystemPrompt = wrapper.SystemPrompt;
                    }
                    
                    // Always copy the selected model from the wrapper
                    agent.SelectedModel = wrapper.SelectedModel;
                }
                
                _viewModel.OccupySlot(agent);
            }
        }
        else
        {
            MessageBox.Show("Could not create agent profile. Invalid agent data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Applies role-appropriate default values for new agents.
    /// Lawyers default to educated attorneys (age 25+) with legal expertise.
    /// Judges default to occupation "Judge" with relevant education.
    /// </summary>
    private static void ApplyRoleDefaults(Agent agent)
    {
        switch (agent.Role)
        {
            case AgentRole.Judge:
                agent.CommunicationTeam = "Neutral";
                agent.OnlyAnswerWhenAsked = true;
                agent.Occupation = "Judge";
                agent.EducationLevel = "Juris Doctor (JD)";
                agent.Age = Math.Max(agent.Age, 40);
                agent.SpecializedKnowledge = "Criminal Law, Tort Law, Civil Law, Family Law";
                break;

            case AgentRole.Lawyer:
                // Default unknown side; user can override in AgentProfile.
                agent.CommunicationTeam = "Neutral";

                agent.Age = Math.Max(agent.Age, 25);
                agent.Occupation = "Attorney";
                agent.EducationLevel = "Juris Doctor (JD)";
                agent.SpecializedKnowledge = "Criminal Law, Tort Law, Civil Law";
                break;

            case AgentRole.Witness:
                agent.Occupation = string.IsNullOrWhiteSpace(agent.Occupation) || agent.Occupation == "Unemployed"
                    ? "Professional" : agent.Occupation;
                break;

            case AgentRole.Reporter:
                agent.Occupation = "Court Reporter";
                agent.EducationLevel = "Associate Degree";
                break;


            default:
                break; 

        }
    }


    private void GenerateLikelyJuror_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        var agent = menuItem?.Tag as Agent;

        if (agent != null)
        {
            // Check if this is a juror seat (either empty or already occupied by a juror)
            if (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror)
            {
                // Get jurisdiction from the current case
                string jurisdiction = _viewModel.CurrentCase.JurisdictionSpecifics ?? "Richland County, South Carolina";
                
                // Parse county and state
                string[] parts = jurisdiction.Split(',');
                string county = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : "Richland County";
                string state = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : "South Carolina";

                // Generate a new juror based on demographics
                var newJuror = _viewModel.GenerateSingleJuror(county, state);
                
                // Update the existing agent with the new juror's properties
                agent.Name = newJuror.Name;
                agent.Gender = newJuror.Gender;
                agent.Age = newJuror.Age;
                agent.Race = newJuror.Race;
                agent.Occupation = newJuror.Occupation;
                agent.EducationLevel = newJuror.EducationLevel;
                agent.IncomeLevel = newJuror.IncomeLevel;
                agent.ZipCode = newJuror.ZipCode;
                agent.PoliticalAffiliation = newJuror.PoliticalAffiliation;
                agent.ReligiousAffiliation = newJuror.ReligiousAffiliation;
                agent.SpecializedKnowledge = newJuror.SpecializedKnowledge;
                agent.MaritalStatus = newJuror.MaritalStatus;
                agent.ParentalStatus = newJuror.ParentalStatus;
                agent.SettlementAuthority = newJuror.SettlementAuthority;
                agent.RiskPerception = newJuror.RiskPerception;
                agent.MediaConsumption = newJuror.MediaConsumption;
                agent.ConsumerSegment = newJuror.ConsumerSegment;
                agent.Hobbies = newJuror.Hobbies;
                agent.ViewingCharacteristics = newJuror.ViewingCharacteristics;
                agent.CurrentStatus = newJuror.CurrentStatus;
                agent.SystemPrompt = newJuror.SystemPrompt;
                agent.Profile = newJuror.Profile;
                agent.Bias = newJuror.Bias;
                agent.VerdictLean = newJuror.VerdictLean;
                agent.Sentiment = newJuror.Sentiment;

                // Copy trial-event memories so the new juror has the same
                // evidence impressions and event log as all other jurors.
                agent.TrialEvents.Clear();
                foreach (var mem in newJuror.TrialEvents)
                    agent.TrialEvents.Add(mem);

                // Mark as occupied if not already
                if (!agent.IsOccupied)
                {
                    _viewModel.OccupySlot(agent);
                }

                // Update the transcript output
                _viewModel.TranscriptOutput = $"[JUROR GENERATED] New juror generated based on {county}, {state} demographics";
            }
            else
            {
                MessageBox.Show("This feature is only available for juror seats.", "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("Could not generate juror. Invalid agent data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAgent_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Agent saved to global casefile.");
    }

    private void ExportCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Agent agent && agent.IsOccupied)
        {
            CharacterManager.SaveCharacter(agent);
            MessageBox.Show($"Character profile saved to {CharacterManager.RepositoryPath}");
        }
    }

    private void DeleteAgent_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            agent.IsOccupied = false;
            agent.Name = "Unoccupied " + agent.Role;
        }
    }

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
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Height = 28, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 12, 12) };
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
                System.IO.Path.GetFileName(openFileDialog.FileName),
                openFileDialog.FileName,
                "Admitted Document",
                null,
                attorney
            );
            MessageBox.Show("Document admitted to court record and memory broadcast.");
        }
    }

    private void EditReporterProfile_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            var profileWindow = new Views.AgentProfileWindow(agent, _viewModel.EffectiveModels)
            {
                Owner = this
            };

            if (profileWindow.ShowDialog() == true)
            {
                // Copy back the selected model and system prompt from the wrapper
                if (profileWindow.DataContext is AgentWithAvailableModels wrapper)
                {
                    agent.SelectedModel = wrapper.SelectedModel;
                    agent.SystemPrompt = wrapper.SystemPrompt;
                }
            }
        }
    }

    private async void ClerkSeat_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Evidence (*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg)|*.pdf;*.txt;*.docx;*.rtf;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.mp3;*.wav;*.ogg|Documents (*.pdf;*.txt;*.docx;*.rtf)|*.pdf;*.txt;*.docx;*.rtf|Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Video (*.mp4;*.mov;*.avi;*.wmv;*.mkv)|*.mp4;*.mov;*.avi;*.wmv;*.mkv|Audio (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string clerkAttorney = PickAttorney();
            int processedCount = 0;
            foreach (var fileName in openFileDialog.FileNames)
            {
                string filePath = fileName;
                string fileNameOnly = System.IO.Path.GetFileName(fileName);

                // Read the file content and generate the AI analysis FIRST
                string fileContent = _viewModel.ReadFileContent(filePath);
                var (detailedAnalysis, _) = await _viewModel.GenerateDocumentAnalysisAsync(
                    fileNameOnly, fileContent, "Submitted via Court Clerk");

                // Default summary text for the dialog
                string defaultSummary = $"Document: {fileNameOnly}\n\n" +
                    $"User Notes: Submitted via Court Clerk\n\n" +
                    $"[AI ANALYSIS]\n{detailedAnalysis}";

                // Show dialog to edit the summary (now pre-filled with AI analysis)
                var editSummaryDialog = new Window
                {
                    Title = $"Review & Edit - {fileNameOnly}",
                    Width = 650,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.CanResize
                };

                // Create UI elements
                var instructionLabel = new Label
                {
                    Content = "Edit the summary below. This will be broadcast to all agents as their memory of the document:",
                    Margin = new Thickness(10),
                    FontWeight = FontWeights.SemiBold
                };
                var textBox = new TextBox
                {
                    Text = defaultSummary,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10),
                    Height = 300
                };
                
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
                var okButton = new Button { Content = "Submit to Agents", Margin = new Thickness(5), Width = 120, Height = 30, Background = System.Windows.Media.Brushes.DarkGreen, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
                var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(5), Width = 80, Height = 30 };
                
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                var mainPanel = new StackPanel();
                mainPanel.Children.Add(instructionLabel);
                mainPanel.Children.Add(textBox);
                mainPanel.Children.Add(buttonPanel);

                editSummaryDialog.Content = mainPanel;

                RoutedEventHandler okClick = (s, args) => { 
                    editSummaryDialog.DialogResult = true;
                    editSummaryDialog.Close(); 
                };
                
                RoutedEventHandler cancelClick = (s, args) => { 
                    editSummaryDialog.DialogResult = false;
                    editSummaryDialog.Close(); 
                };

                okButton.Click += okClick;
                cancelButton.Click += cancelClick;

                // Show dialog modally
                if (editSummaryDialog.ShowDialog() == true)
                {
                    // Add the evidence with the user-edited summary
                    // This summary (including the AI analysis) will be broadcast to agents
                    // Pass the already-generated detailed analysis to avoid a duplicate LLM call
                    await _viewModel.AddEvidence(fileNameOnly, filePath, textBox.Text, detailedAnalysis, clerkAttorney);
                    // Increment the reporter's document count
                    var reporter = _viewModel.JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter);
                    if (reporter != null)
                        reporter.DocumentCount++;
                    processedCount++;
                }
            }
            MessageBox.Show($"{processedCount} document(s) submitted and parsed into evidence.");
        }
    }

    private void ExhibitList_Click(object sender, RoutedEventArgs e)
    {
        var exhibitWindow = new ExhibitListWindow(_viewModel)
        {
            Owner = this
        };
        exhibitWindow.ShowDialog();
    }

    private async void ReevaluateEvidence_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReevaluateEvidenceAsync();
    }

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

    private void NewCase_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Start a new case? All unsaved changes will be lost.", "New Case", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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

    private void LoadDefaultCase(string caseFileName)
    {
        if (_viewModel.CurrentCase?.Agents?.Count > 0 &&
            MessageBox.Show("Load a default case? Any unsaved changes will be lost.", "Default Case", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        // Look for default case files relative to the executable (deployed) and project root (dev)
        string[] searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DefaultCases", caseFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "DefaultCases", caseFileName)
        };

        string? casePath = searchPaths.FirstOrDefault(File.Exists);
        if (casePath == null)
        {
            MessageBox.Show($"Default case file '{caseFileName}' not found.", "Default Case", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _viewModel.LoadCase(casePath);
    }

    private void DefaultCase_AppleRiver_Click(object sender, RoutedEventArgs e)
    {
        LoadDefaultCase("apple-river.jur");
    }

    private void DefaultCase_AppleJuice_Click(object sender, RoutedEventArgs e)
    {
        LoadDefaultCase("apple-juice-murders.jur");
    }

    private void DefaultCase_NCAccident_Click(object sender, RoutedEventArgs e)
    {
        LoadDefaultCase("nc-comparative-fault-accident.jur");
    }

    private void DefaultCase_SmokingMemo_Click(object sender, RoutedEventArgs e)
    {
        LoadDefaultCase("smoking-memo.jur");
    }

    private void SaveCaseAs_Click(object sender, RoutedEventArgs e)
    {
        SaveSimulation_Click(sender, e);
    }

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
            MessageBox.Show("No transcript loaded. Please import a transcript first.", "Extract Entities", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if AI model is configured
        if (!_viewModel.CurrentCase.AvailableModels.Any())
        {
            MessageBox.Show("No AI model configured. Please configure an LLM first.", "Extract Entities", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _viewModel.ExtractEntitiesFromTranscriptAsync();
            MessageBox.Show("Entity extraction complete. Check the court record for extracted charges, evidence, and witnesses.", "Extract Entities", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Entity extraction failed: {ex.Message}", "Extract Entities", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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
            var displayName = $"{i + 1}. {System.IO.Path.GetFileNameWithoutExtension(filePath)}";
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
            if (System.IO.File.Exists(filePath))
            {
                _viewModel.LoadCase(filePath);
            }
            else
            {
                var result = MessageBox.Show(
                    $"The file '{filePath}' no longer exists.\nRemove it from the recent files list?",
                    "File Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _settingsService.RemoveRecentFile(filePath);
                    PopulateRecentFilesMenu();
                }
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CounselDiscussion_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.CounselDiscussionWindow(_viewModel)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void ClientConsultation_Click(object sender, RoutedEventArgs e)
    {
        var clients = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Client && a.IsOccupied).ToList();
        var lawyers = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Lawyer && a.IsOccupied).ToList();

        if (!clients.Any() || !lawyers.Any())
        {
            MessageBox.Show("Need at least one seated Client and one seated Lawyer.");
            return;
        }

        // Simplification: just pick the first pair for now
        // In a real app, you'd select which team is consulting
        var window = new Views.ClientConversationWindow(_viewModel, clients.First(), lawyers.First())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void AgentChat_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.Tag as Agent;
        if (agent == null || !agent.IsOccupied) return;

        var window = new Views.AgentChatWindow(_viewModel, agent)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void JurorReport_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.Tag as Agent;
        if (agent == null || !agent.IsOccupied) return;

        var reportWindow = new Views.JurorReportWindow(agent, _viewModel.CurrentCase?.Mode ?? Models.CaseMode.Civil)
        {
            Owner = this
        };
        reportWindow.ShowDialog();
    }

    private async void SetTrialPhase_Discovery_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentCase.TrialPhase = TrialPhase.Discovery;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.CaseGeneration;
        _viewModel.TranscriptOutput = "[PHASE] Discovery\n\n";
        await _viewModel.RunDiscoveryPhase();
    }

    private async void SetTrialPhase_Pretrial_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentCase.TrialPhase = TrialPhase.Pretrial;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.LegalResearch;
        _viewModel.TranscriptOutput = "[PHASE] Pretrial\n\n";
        await _viewModel.RunPretrialEvaluation();
    }

    private async void SetCourtPhase_OpeningStatements_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        await _viewModel.EnterOpeningStatements();
    }


    private async void SetCourtPhase_WitnessTestimony_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        await _viewModel.EnterWitnessTestimony();
    }


    private async void SetCourtPhase_CrossExamination_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        await _viewModel.EnterCrossExamination();
    }


    private void SetCourtPhase_PartyStatements_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        _viewModel.CurrentDebateStage = CourtPhase.PartyStatements;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.PartyStatements;
        _viewModel.TranscriptOutput = "[STAGE] Party Statements";
    }


    private async void SetCourtPhase_ClosingArguments_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        await _viewModel.EnterClosingArguments();
    }


    private void SetCourtPhase_JuryInstructions_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        _viewModel.CurrentDebateStage = CourtPhase.JuryInstructions;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.JuryInstructions;
        _viewModel.TranscriptOutput = "[STAGE] Jury Instructions\n\n";
        _viewModel.GenerateJuryInstructions();
    }

    private void SetCourtPhase_JuryDeliberation_Click(object sender, RoutedEventArgs e)
    {
        SetTrialPhaseToTrial();
        _viewModel.CurrentDebateStage = CourtPhase.JuryDeliberation;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.JuryDeliberation;
        _viewModel.TranscriptOutput = "[STAGE] Jury Deliberation";
    }

    /// <summary>
    /// Sets the case to Trial phase if not already in Trial mode.
    /// </summary>
    private void SetTrialPhaseToTrial()
    {
        if (_viewModel.CurrentCase.TrialPhase != TrialPhase.Trial)
        {
            _viewModel.CurrentCase.TrialPhase = TrialPhase.Trial;
            _viewModel.TranscriptOutput = "[PHASE] Trial\n\nThe trial is now in session.\n";
        }
    }


    private void LoadAgent_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Load Agent feature coming soon.");
    private void BuildAgent_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Build Agent feature coming soon.");
    private void DesignPrompts_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Design Prompts feature coming soon.");
    private void BatchUpdatePrompts_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Batch Update feature coming soon.");

    private void GeneralSettings_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Settings feature coming soon.");
    
    private void GenerateCase_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.CaseSelectionDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedFilePath != null)
            {
                if (MessageBox.Show("Load this demo case? Unsaved changes will be lost.",
                    "Load Demo", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _viewModel.LoadCase(dialog.SelectedFilePath);
                }
            }
            else
            {
                // Blank Case button was clicked
                if (MessageBox.Show("Start a new blank case? Unsaved changes will be lost.",
                    "New Case", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _viewModel.NewCase();
                }
            }
        }
    }

    private void CaseSettings_Click(object sender, RoutedEventArgs e)
    {
        var originalPhase = _viewModel.CurrentCase.TrialPhase;
        var viewModel = new CaseSettingsViewModel(_viewModel.CurrentCase);
        var window = new Views.CaseSettingsWindow(viewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            // Re-initialize the courtroom to pick up any changes to party names or attorneys
            _viewModel.ReinitializeCourtroom();

            // Check if the trial phase was changed
            if (_viewModel.CurrentCase.TrialPhase != originalPhase)
            {
                var message = $"Trial phase changed from {originalPhase} to {_viewModel.CurrentCase.TrialPhase}. " +
                             "You can now modify inputs from this phase and earlier phases. " +
                             "For example, if you changed from Trial to Discovery, you can now add or modify evidence. " +
                             "Remember to proceed through the phases in order for best results.";
                
                MessageBox.Show(message, "Phase Changed - Capabilities Updated", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            _viewModel.UpdateWindowTitle();
            MessageBox.Show("Case settings updated.");
        }
    }

    private void DefaultCaseSettings_Click(object sender, RoutedEventArgs e)
    {
        CaseFile defaultCase;
        try
        {
            defaultCase = _viewModel.GetDefaultSettings();
            // Ensure we have a valid CaseFile instance even if GetDefaultSettings returns null
            if (defaultCase == null)
            {
                defaultCase = new CaseFile 
                { 
                    CaseName = "New Case",
                    Mode = CaseMode.Civil,
                    Jurisdiction = JurisdictionType.State,
                    JurorCount = 12
                };
            }
            else
            {
                // Ensure all collections are properly initialized (fixes null reference issues from JSON deserialization)
                defaultCase.EnsureCollectionsInitialized();
            }
        }
        catch (Exception ex)
        {
            // Log the exception if you have logging or handle it appropriately
            MessageBox.Show($"Error loading default settings: {ex.Message}. Creating new default settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            // Create a fresh CaseFile instance as fallback
            defaultCase = new CaseFile 
            { 
                CaseName = "New Case",
                Mode = CaseMode.Civil,
                Jurisdiction = JurisdictionType.State,
                JurorCount = 12
            };
            defaultCase.EnsureCollectionsInitialized();
        }
        
        var originalPhase = defaultCase.TrialPhase;
        var viewModel = new CaseSettingsViewModel(defaultCase, true);
        var window = new Views.CaseSettingsWindow(viewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            // Check if the trial phase was changed
            if (defaultCase.TrialPhase != originalPhase)
            {
                var message = $"Default trial phase changed from {originalPhase} to {defaultCase.TrialPhase}. " +
                             "This will be applied to new cases. " +
                             "Remember that changing to an earlier phase allows modification of inputs from that phase and earlier.";
                
                MessageBox.Show(message, "Default Phase Changed", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            _viewModel.SaveDefaultSettings(defaultCase);
            MessageBox.Show("Default case settings saved.");
        }
    }

    private void CaseCustomModels_Click(object sender, RoutedEventArgs e)
    {
        // Open LLM config with ONLY this case's models — changes stay in the .jur file
        var viewModel = new ModelSettingsViewModel(
            _viewModel.CurrentCase.AvailableModels,
            _viewModel.CurrentCase.GlobalTemperature,
            _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors,
            _viewModel.CurrentCase.DefaultJurorModel);
        var window = new Views.ModelSettingsWindow(viewModel) { Owner = this };

        if (window.ShowDialog() == true)
        {
            if (viewModel.Models.Count > 0)
            {
                _viewModel.CurrentCase.AvailableModels.Clear();
                _viewModel.CurrentCase.AvailableModels.AddRange(viewModel.Models);
            }
            _viewModel.CurrentCase.GlobalTemperature = viewModel.GlobalTemperature;
            _viewModel.CurrentCase.GlobalMaxTokens = viewModel.GlobalMaxTokens;
            _viewModel.CurrentCase.DefaultJurorModel = viewModel.DefaultJurorModel;
            // Note: NOT saving to universal defaults — these are case-only
        }
    }

    private void LLMConfiguration_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new ModelSettingsViewModel(
            _viewModel.EffectiveModels,
            _viewModel.CurrentCase.GlobalTemperature,
            _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors,
            _viewModel.CurrentCase.DefaultJurorModel);
        var window = new Views.ModelSettingsWindow(viewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            if (viewModel.Models.Count > 0)
            {
                _viewModel.CurrentCase.AvailableModels.Clear();
                _viewModel.CurrentCase.AvailableModels.AddRange(viewModel.Models);
            }
            _viewModel.CurrentCase.GlobalTemperature = viewModel.GlobalTemperature;
            _viewModel.CurrentCase.GlobalMaxTokens = viewModel.GlobalMaxTokens;
            _viewModel.CurrentCase.DefaultJurorModel = viewModel.DefaultJurorModel;
            _viewModel.CurrentCase.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.DefaultBiasFactors)
                _viewModel.CurrentCase.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });

            // Persist model config changes to default settings so they survive restarts
            _viewModel.SaveDefaultSettings(_viewModel.CurrentCase);
            
            // Auto-assign the newly configured model to all agents that don't have one selected.
            // Prefer models with real API keys (not empty or placeholder values like "sk-").
            if (viewModel.Models.Count > 0)
            {
                var preferredModel = viewModel.Models.LastOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.ApiKey) && m.ApiKey.Trim() != "sk-")
                    ?? viewModel.Models.LastOrDefault();

                if (preferredModel != null)
                {
                    // Warn if the selected model has no real API key
                    if (string.IsNullOrWhiteSpace(preferredModel.ApiKey) || preferredModel.ApiKey.Trim() == "sk-")
                    {
                        MessageBox.Show(
                            $"Model '{preferredModel.FriendlyName}' has no API key configured.\n" +
                            "LLM calls will fail until you configure a valid API key.\n\n" +
                            "Open LLM Configuration, select the model, and click 'Configure...' to set your key.",
                            "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    foreach (var agent in _viewModel.AllAgents.Where(a => a.IsOccupied))
                    {
                        if (string.IsNullOrWhiteSpace(agent.SelectedModel))
                            agent.SelectedModel = preferredModel.FriendlyName;
                    }
                }
            }
            
            // Persist the updated models to default settings so they survive app restart
            var defaults = _viewModel.GetDefaultSettings();
            defaults.AvailableModels.Clear();
            defaults.AvailableModels.AddRange(viewModel.Models);
            defaults.GlobalTemperature = viewModel.GlobalTemperature;
            defaults.GlobalMaxTokens = viewModel.GlobalMaxTokens;
            defaults.DefaultJurorModel = viewModel.DefaultJurorModel;
            defaults.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.DefaultBiasFactors)
                defaults.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
            _viewModel.SaveDefaultSettings(defaults);
            
            MessageBox.Show("LLM Configuration updated and saved.");
        }
    }

    private void ModelWeights_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new ModelSettingsViewModel(
            _viewModel.CurrentCase.AvailableModels,
            _viewModel.CurrentCase.GlobalTemperature,
            _viewModel.CurrentCase.GlobalMaxTokens,
            _viewModel.CurrentCase.DefaultBiasFactors);

        var window = new Views.ModelWeightsWindow(viewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _viewModel.CurrentCase.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.GetCurrentBiasWeightFactors())
                _viewModel.CurrentCase.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });

            var defaults = _viewModel.GetDefaultSettings();
            defaults.DefaultBiasFactors.Clear();
            foreach (var bf in viewModel.GetCurrentBiasWeightFactors())
                defaults.DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
            _viewModel.SaveDefaultSettings(defaults);

            MessageBox.Show("Model weights updated and saved.", "Model Weights", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void GenerateJury_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GenerateJury();
        MessageBox.Show("Jury generated based on case jurisdiction demographics.");
    }

    private void GenerateAlternates_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GenerateAlternates(2);
        MessageBox.Show("Alternate jurors generated based on case jurisdiction demographics.");
    }

    private void CourtroomLayoutSettings_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Layout Settings feature coming soon.");

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Verdict - Jury Verdict Modeling Simulation\nVersion 1.0", "About Verdict");
    }

    private void AgentSeat_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var border = sender as Border;
        if (border?.ContextMenu != null)
        {
            var agent = border.DataContext as Agent;
            foreach (var item in border.ContextMenu.Items.OfType<MenuItem>())
            {
                if (item.Header.ToString() == "Chat")
                {
                    item.Visibility = (agent != null && agent.IsOccupied)
                        ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (item.Header.ToString() == "Juror Report")
                {
                    item.Visibility = (agent != null && agent.IsOccupied &&
                        (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror))
                        ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    item.Visibility = Visibility.Visible;
                }
            }
        }
    }

    /// <summary>
    /// Adds a client to a lawyer's team.
    /// </summary>
    private void AddClientToLawyer_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as FrameworkElement;
        var agent = menuItem?.DataContext as Agent;
        if (agent != null)
        {
            // Find a lawyer that could take this client
            var lawyers = _viewModel.AllAgents.Where(a => a.Role == AgentRole.Lawyer && a.IsOccupied).ToList();
            if (!lawyers.Any())
            {
                MessageBox.Show("No lawyers are seated. Please add a lawyer first.", "No Lawyer Available", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Create a new client agent
            var clientAgent = new Agent
            {
                Role = AgentRole.Client,
                Name = $"Client (of {agent.Name.Split(' ').LastOrDefault() ?? agent.Name})",
                IsOccupied = true
            };
            
            // Add client to the gallery/observer area (clients sit as observers)
            _viewModel.Gallery.Add(clientAgent);
            
            MessageBox.Show($"Client assigned to {agent.Name}. Client added to gallery area.",
                "Client Added", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Opens a savable text report window for the current case.
    /// The user can review, edit, and save the report as a text file.
    /// </summary>
    private void GenerateReport_Click(object sender, RoutedEventArgs e)
    {
        var reportText = _viewModel.GenerateTextReport();
        var reportWindow = new Views.ReportWindow(reportText, _viewModel.CurrentCase.CaseName ?? "Case Report")
        {
            Owner = this
        };
        reportWindow.ShowDialog();
    }

    /// <summary>
    /// Applies dynamic role assignment based on case analysis.
    /// </summary>
    private void DynamicRoleAssignment_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("This will analyze the current case and automatically assign agents to recommended roles. Continue?",
            "Dynamic Role Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ApplyDynamicRoleAssignment();
            MessageBox.Show("Dynamic role assignment completed. Check the court record for details.",
                "Role Assignment", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
