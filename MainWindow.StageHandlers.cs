using System.Windows;
using Verdict.Models;

namespace Verdict;

/// <summary>
/// Trial-phase and court-phase event handlers: discovery, pretrial, trial stages, deliberation.
/// </summary>
public partial class MainWindow
{
    private async void AdvanceStage_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.DeliberateNextTurn();
    }

    private async void AutoDeliberate_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AutoSimulateTrial();
    }

    private async void SetTrialPhase_Discovery_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentCase.TrialPhase = TrialPhase.Discovery;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.CaseGeneration;
        _viewModel.RefreshPhaseLabels();
        _viewModel.TranscriptOutput = "[PHASE] Discovery\n\n";
        await _viewModel.RunDiscoveryPhase();
    }

    private async void SetTrialPhase_Pretrial_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentCase.TrialPhase = TrialPhase.Pretrial;
        _viewModel.CurrentCase.CurrentDebateStage = CourtPhase.LegalResearch;
        _viewModel.RefreshPhaseLabels();
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
            _viewModel.RefreshPhaseLabels();
            _viewModel.TranscriptOutput = "[PHASE] Trial\n\nThe trial is now in session.\n";
        }
    }
}
