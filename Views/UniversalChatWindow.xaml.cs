using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Verdict.Models;
using Verdict.Services;
using Verdict.ViewModels;

namespace Verdict.Views;

/// <summary>
/// A universal chat and evidence tool window that allows communication with any seated agent
/// and submission of testimony as evidence. Supports two modes: Chat (direct conversation)
/// and Testimony (evidence admission with attorney/witness metadata).
/// </summary>
/// <remarks>
/// <para><b>Chat Mode:</b> Sends a message to the selected target agent via ProcessChatInputLine,
/// which processes the message through the courtroom's stage-appropriate interaction pipeline.</para>
/// <para><b>Testimony Mode:</b> Submits the content as evidence, recording the testimony
/// with attorney attribution and caller side information. Currently under development.</para>
/// </remarks>
public partial class UniversalChatWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly List<Agent> _seatedAgents;
    private string _conversationLog = string.Empty;

    /// <summary>
    /// Attorneys filtered from seated agents, used for testimony attribution in evidence mode.
    /// </summary>
    private readonly List<Agent> _attorneys;

    /// <summary>
    /// Initializes a new instance of the UniversalChatWindow.
    /// Populates agent combos from the main view model's occupied agents.
    /// </summary>
    /// <param name="mainVm">The main view model containing all agents and case data.</param>
    public UniversalChatWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;

        _seatedAgents = _mainVm.AllAgents.Where(a => a.IsOccupied).ToList();
        _attorneys = _seatedAgents.Where(a => a.Role == AgentRole.Lawyer).ToList();

        TargetAgentCombo.ItemsSource = _seatedAgents;
        TargetAgentCombo.DisplayMemberPath = "Name";
        if (_seatedAgents.Any()) TargetAgentCombo.SelectedIndex = 0;

        AttorneyCombo.ItemsSource = _attorneys;
        AttorneyCombo.DisplayMemberPath = "Name";
        if (_attorneys.Any()) AttorneyCombo.SelectedIndex = 0;

        WitnessAgentCombo.ItemsSource = _seatedAgents.Where(a => a.Role == AgentRole.Witness).ToList();
        WitnessAgentCombo.DisplayMemberPath = "Name";
        if (WitnessAgentCombo.Items.Count > 0) WitnessAgentCombo.SelectedIndex = 0;

        ModeCombo.SelectionChanged += (_, _) => SyncModeUi();
        SyncModeUi();
    }

    /// <summary>
    /// Synchronizes UI element states based on the selected mode.
    /// When in Testimony mode, witness/attorney controls are enabled.
    /// </summary>
    private void SyncModeUi()
    {
        bool isTestimony = (ModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Introduce as Testimony (Evidence)";
        WitnessPromptLabel.Text = isTestimony
            ? "Witness name for testimony (optional if Witness slot is selected)"
            : "Witness name for testimony (optional if Witness slot is selected)";

        WitnessAgentCombo.IsEnabled = isTestimony;
        AttorneyCombo.IsEnabled = isTestimony;
        CallerSideCombo.IsEnabled = isTestimony;
        TestimonyTextBox.IsEnabled = true;
    }

    /// <summary>
    /// Gets the string content of a ComboBox's selected item.
    /// </summary>
    private string SelectedComboContent(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Handles the Send button click. Sends the message in Chat mode or submits
    /// as evidence in Testimony mode.
    /// </summary>
    private void Send_Click(object sender, RoutedEventArgs e)
    {
        string testimonyOrChat = TestimonyTextBox.Text.Trim();
        if (string.IsNullOrEmpty(testimonyOrChat)) return;

        var selectedTarget = TargetAgentCombo.SelectedItem as Agent;
        if (selectedTarget == null)
        {
            MessageBox.Show("Select a target agent.");
            return;
        }

        var mode = (ModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Chat";

        _conversationLog += $"\n--- {mode} ({selectedTarget.Name}) ---\n";

        if (mode == "Introduce as Testimony (Evidence)")
        {
            var submittingAttorney = AttorneyCombo.SelectedItem as Agent;
            string offeringAttorney = submittingAttorney?.Name ?? "";

            string callerSide = SelectedComboContent(CallerSideCombo);
            bool offeredByPlaintiff = callerSide.Equals("Plaintiff", StringComparison.OrdinalIgnoreCase);

            var selectedWitnessAgent = WitnessAgentCombo.SelectedItem as Agent;
            string witnessName = selectedWitnessAgent?.Name ?? "";

            _conversationLog += $"[EVIDENCE ADMITTED] Testimony by {witnessName}" +
                               $" (Caller side: {callerSide}).\n";
        }
        else
        {
            _mainVm.ProcessChatInputLine(selectedTarget.Name, testimonyOrChat);
            _conversationLog += $"You: {testimonyOrChat}\n";
        }

        ConversationLog.Text = _conversationLog;
        TestimonyTextBox.Clear();
    }

    /// <summary>
    /// Clears the witness selection.
    /// </summary>
    private void ClearWitness_Click(object sender, RoutedEventArgs e)
    {
        if (WitnessAgentCombo.Items.Count == 0) return;
        WitnessAgentCombo.SelectedIndex = -1;
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}