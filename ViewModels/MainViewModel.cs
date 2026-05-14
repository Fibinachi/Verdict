// MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.ViewModels;

/// <summary>
/// The main view model for the courtroom simulation, coordinating between
/// dedicated services for courtroom management, evidence analysis, debate,
/// jury calculations, and entity mapping.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ICaseService _caseService;
    private readonly ITranscriptService _transcriptService;
    private readonly ISettingsService _settingsService;
    private readonly IJuryDemographicsService _juryService;
    private readonly ICourtroomManagerService _courtroomManager;
    private readonly IEvidenceAnalysisService _evidenceService;
    private readonly IDebateService _debateService;
    private readonly IJuryCalculationService _juryCalc;
    private readonly ICaseEntityMapper _entityMapper;
    private readonly IAgentInteractionService _agentInteraction;
    public IAgentInteractionService AgentInteractionService => _agentInteraction;

    private CaseFile _currentCase = new();
    private string _transcriptOutput = "";
    private CourtPhase _currentDebateStage = CourtPhase.OpeningStatements;
    private string _windowTitle = "VERDICT";
    private bool _isGeneratingClosingArguments = false;
    private string _generatedClosingArgument = string.Empty;

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    /// <summary>
    /// Updates the window title based on current case information.
    /// Format: VERDICT - Whoever v. Whoever et al. - (CASE STAGE)
    /// </summary>
    public void UpdateWindowTitle()
    {
        var caseName = _currentCase?.CaseName ?? "Untitled Case";
        var plaintiffs = _currentCase?.Plaintiffs ?? new();
        var defendants = _currentCase?.Defendants ?? new();

        string plaintiffPart = plaintiffs.Count > 0 ? string.Join(", ", plaintiffs) : "Unknown";
        string defendantPart = defendants.Count > 0 ? string.Join(", ", defendants) : "Unknown";

        // If there are multiple parties, add "et al."
        string plaintiffDisplay = plaintiffs.Count > 1 ? $"{plaintiffs[0]} et al." : plaintiffPart;
        string defendantDisplay = defendants.Count > 1 ? $"{defendants[0]} et al." : defendantPart;

        string caseStage = _currentCase?.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "UNKNOWN"
        };

        WindowTitle = $"VERDICT - {plaintiffDisplay} v. {defendantDisplay} - ({caseStage})";
    }

    public string CurrentDebateStageDisplay => _currentDebateStage switch
    {
        CourtPhase.OpeningStatements => "Opening Statements",
        CourtPhase.WitnessTestimony => "Witness Testimony",
        CourtPhase.CrossExamination => "Cross Examination",
        CourtPhase.PartyStatements => "Party Statements",
        CourtPhase.ClosingArguments => "Closing Arguments",
        CourtPhase.JuryDeliberation => "Jury Deliberation",
        _ => "Unknown Stage"
    };

    public CaseFile CurrentCase
    {
        get => _currentCase;
        set 
        {
            var caseFile = value ?? new CaseFile();
            caseFile.AddDefaultModels();
            
            // Check if we're changing to a different phase
            if (_currentCase is not null && caseFile.TrialPhase != _currentCase.TrialPhase)
            {
                var oldPhase = _currentCase.TrialPhase;
                var newPhase = caseFile.TrialPhase;
                
                // Call the adjustment method to handle phase changes appropriately
                AdjustDataForPhaseChange(newPhase, oldPhase);
            }
            
            // Ensure caseFile is not null before setting
            #pragma warning disable CS8601
            SetProperty(ref _currentCase, caseFile);
            #pragma warning restore CS8601
        }
    }
    
    /// <summary>
    /// The output of the court transcript for display in the UI.
    /// </summary>
    public string TranscriptOutput
    {
        get => _transcriptOutput;
        set => SetProperty(ref _transcriptOutput, value);
    }

    // Debate stage UI and logic are disabled; deliberation is always the active stage.
    public CourtPhase CurrentDebateStage
    {
        get => CourtPhase.JuryDeliberation;
        set
        {
            // Keep backing field consistent for bindings that read it, but do not allow stage changes.
            if (_currentDebateStage != CourtPhase.JuryDeliberation)
            {
                _currentDebateStage = CourtPhase.JuryDeliberation;
                OnPropertyChanged(nameof(CurrentDebateStage));
                OnPropertyChanged(nameof(CurrentDebateStageDisplay));
            }
            TranscriptOutput = TranscriptOutput; // no-op
        }
    }


    public void AdvanceDebateStage()
    {
        var stages = Enum.GetValues<CourtPhase>();
        var currentIndex = Array.IndexOf(stages, CurrentDebateStage);
        
        // Move to next stage, or loop back to first if at end
        var nextIndex = (currentIndex + 1) % stages.Length;
        CurrentDebateStage = stages[nextIndex];
    }
    
    /// <summary>
    /// Calculated aggregate of jury leanings.
    /// </summary>
    public double JuryLiabilityAverage => _juryCalc.AverageLean(Jurors);

    /// <summary>
    /// Jury's estimated damages amount based on evidence.
    /// </summary>
    public double EstimatedDamages => CurrentCase.Evidence.Sum(e => e.EstimatedDamages);

    /// <summary>
    /// Human-readable prediction of the current jury state.
    /// </summary>
    public string LikelyVerdict => _juryCalc.LikelyVerdict(Jurors);

    public ObservableCollection<Agent> JudgeArea { get; } = new();
    public ObservableCollection<Agent> Jurors { get; } = new();
    public ObservableCollection<Agent> DefenseTeam { get; } = new();
    public ObservableCollection<Agent> ProsecutionTeam { get; } = new();
    public ObservableCollection<Agent> Gallery { get; } = new();

    public ObservableCollection<EvidenceDocument> Exhibits { get; } = new();

    /// <summary>
    /// Returns an aggregation of all agents in all areas of the courtroom.
    /// </summary>
    public IEnumerable<Agent> AllAgents => 
        JudgeArea.Concat(Jurors).Concat(DefenseTeam).Concat(ProsecutionTeam).Concat(Gallery);

    public MainViewModel()
        : this(new CaseService(), new TranscriptService(), new SettingsService(),
               new JuryDemographicsService(), new CourtroomManagerService(),
               new EvidenceAnalysisService(), new DebateService(),
               new JuryCalculationService(), null,
               new AgentInteractionService(ProviderDiscoveryService.GetAvailableProviders()))
    {
    }

    public MainViewModel(
        ICaseService caseService,
        ITranscriptService transcriptService,
        ISettingsService settingsService,
        IJuryDemographicsService juryService,
        ICourtroomManagerService courtroomManager,
        IEvidenceAnalysisService evidenceService,
        IDebateService debateService,
        IJuryCalculationService juryCalc,
        ICaseEntityMapper? entityMapper,
        IAgentInteractionService? agentInteraction = null)
    {
        _caseService = caseService;
        _transcriptService = transcriptService;
        _settingsService = settingsService;
        _juryService = juryService;
        _courtroomManager = courtroomManager;
        _evidenceService = evidenceService;
        _debateService = debateService;
        _juryCalc = juryCalc;
        _entityMapper = entityMapper ?? new CaseEntityMapper(evidenceService);
        _agentInteraction = agentInteraction ?? new AgentInteractionService(ProviderDiscoveryService.GetAvailableProviders());

        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, _currentCase);
        NewCase();
    }

    /// <summary>
    /// Re-initializes the courtroom layout from the current case data.
    /// Used after case settings are updated (e.g., party names, attorneys).
    /// </summary>
    public void ReinitializeCourtroom()
    {
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, CurrentCase);
    }

    /// <summary>
    /// Resets the courtroom for a new case, loading default settings.
    /// </summary>
    public void NewCase()
    {
        var defaults = _settingsService.GetDefaultCaseSettings();
        defaults.EnsureCollectionsInitialized();
        CurrentCase = defaults;
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, defaults);
        RefreshExhibits();

        // Debate stage disabled: always deliberation.
        _currentDebateStage = CourtPhase.JuryDeliberation;

        TranscriptOutput = "Court is in session. Awaiting transcript...";
        UpdateWindowTitle();
    }


    /// <summary>
    /// Returns the current default settings.
    /// </summary>
    public CaseFile GetDefaultSettings() 
    { 
        var defaults = _settingsService.GetDefaultCaseSettings();
        defaults.EnsureCollectionsInitialized(); // Ensure all collections are properly initialized
        return defaults;
    }

    /// <summary>
    /// Saves new default settings.
    /// </summary>
    public void SaveDefaultSettings(CaseFile defaults) => _settingsService.SaveDefaultCaseSettings(defaults);

    // Initialization delegated to ICourtroomManagerService

    /// <summary>
    /// Generates a jury panel based on the case's jurisdiction demographics.
    /// Parses county and state from JurisdictionSpecifics and generates appropriate jurors.
    /// </summary>
    public void GenerateJury()
    {
        string jurisdiction = CurrentCase.JurisdictionSpecifics ?? "";

        if (string.IsNullOrWhiteSpace(jurisdiction))
        {
            jurisdiction = "Richland County, South Carolina";
        }

        // Try to parse county and state
        string[] parts = jurisdiction.Split(',');
        string county = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : "Richland County";
        string state = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : "South Carolina";

        var generatedJurors = _juryService.GenerateJuryPanel(12, 2, county, state);

        // Replace current Jurors collection contents
        Jurors.Clear();
        foreach (var juror in generatedJurors)
        {
            Jurors.Add(juror);
        }

        TranscriptOutput = $"[JURY GENERATED] {generatedJurors.Count} jurors generated for {county}, {state}";
        BroadcastEvent($"Jury panel generated based on {county} County demographics", 
            Enum.GetValues<AgentRole>().ToList());
    }

    /// <summary>
    /// Generates a single juror based on the case's jurisdiction demographics.
    /// Parses county and state from JurisdictionSpecifics and generates appropriate juror.
    /// </summary>
    public Agent GenerateSingleJuror(string county, string state = "South Carolina")
    {
        if (string.IsNullOrWhiteSpace(county))
        {
            county = "Richland County";
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            state = "South Carolina";
        }

        // Generate a single juror using the jury service
        var juror = _juryService.GenerateJuror(county, state);
        juror.Role = AgentRole.Juror;
        
        return juror;
    }

    public void OccupySlot(Agent agent) =>
        _courtroomManager.OccupySlot(agent, DefenseTeam, ProsecutionTeam);

    /// <summary>
    /// Broadcasts a courtroom event to all agents who can see it.
    /// Delegates to IDebateService which handles memory, sentiment, and status updates.
    /// </summary>
    public void BroadcastEvent(string description, List<AgentRole> visibleTo, bool isSidebar = false)
    {
        // Filter recipients by runtime conversation restrictions.
        // In this simulation, we treat the "speaker" as a neutral/unknown moderator for broadcast events.
        Agent? virtualSpeaker = null;

        var filtered = AllAgents
            .Where(a => a.IsOccupied)
            .Where(a => visibleTo.Contains(a.Role))
            .Where(r => ConversationRestrictionPolicy.CanCommunicate(virtualSpeaker, r, isJudgeAnswer: false))
            .ToList();

        _debateService.BroadcastEvent(CurrentCase, filtered, description, visibleTo, isSidebar);
        NotifyJuryUpdate();
    }


/// <summary>
    /// Admits evidence into the case file and broadcasts it to the courtroom,
    /// delegating strength assessment and exposure calculation to IEvidenceAnalysisService.
    /// Generates a detailed document analysis for agents to review and form opinions.
    /// All phases now consistently calculate exposure, apply influence, and update the UI.
    /// </summary>
    public async Task AddEvidence(string fileName, string path, string summary, string? existingDetailedAnalysis = null, string offeringAttorney = "")
    {
        // Read the actual file content for analysis
        string fileContent = _evidenceService.ReadFileContent(path);

        // Generate a detailed analysis using the LLM (or fallback keyword analysis)
        // This includes per-defendant damages and a total calculated by the LLM
        // If an existing analysis was already generated (e.g., from ClerkSeat_Click), reuse it
        string detailedAnalysis;
        double llmDamages;
        if (!string.IsNullOrEmpty(existingDetailedAnalysis))
        {
            detailedAnalysis = existingDetailedAnalysis;
            llmDamages = EvidenceAnalysisService.ExtractTotalDamagesFromAnalysisPublic(existingDetailedAnalysis);
        }
        else
        {
            (detailedAnalysis, llmDamages) = await _evidenceService.GenerateDocumentAnalysisAsync(
                fileName, fileContent, summary, CurrentCase, _agentInteraction);
        }

        // Determine media type based on file extension
        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        string mediaType = extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" ? "Image" : "Document";

        var doc = new EvidenceDocument
        {
            FileName = fileName,
            FilePath = path,
            Summary = summary,
            ExhibitNumber = CurrentCase.Evidence.Count + 1,
            MediaType = mediaType,
            DetailedAnalysis = detailedAnalysis,
            OfferingAttorney = offeringAttorney,

            // Side indexing for future usage
            IsOfferedByPlaintiffSide = !string.IsNullOrWhiteSpace(offeringAttorney)
                && !string.IsNullOrWhiteSpace(CurrentCase.PlaintiffAttorney)
                && offeringAttorney.Trim().Equals(CurrentCase.PlaintiffAttorney.Trim(), StringComparison.OrdinalIgnoreCase),
        };

        // Use the LLM-calculated damages if available, otherwise fall back to keyword assessment
        if (llmDamages > 0)
        {
            doc.EstimatedDamages = llmDamages;
            doc.EvidenceStrength = 0.85; // LLM-analyzed documents get higher confidence
        }
        else
        {
            // Assess strength using both the user summary and the actual file content
            _evidenceService.AssessStrength(doc, summary, fileContent);
        }

        // Side numbering (per presenting party)
        int nextPlaintiffSide = CurrentCase.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;
        int nextDefenseSide = CurrentCase.Evidence.Count(e => !e.IsOfferedByPlaintiffSide) + 1;
        if (doc.IsOfferedByPlaintiffSide)
            doc.PlaintiffSideExhibitNumber = nextPlaintiffSide;
        else
            doc.DefenseSideExhibitNumber = nextDefenseSide;

        // Common operations for every phase: store, calculate exposure, apply influence
        CurrentCase.Evidence.Add(doc);
        RefreshExhibits();
        _evidenceService.CalculateExposure(CurrentCase);
        _juryCalc.ApplyEvidenceInfluence(AllAgents, doc);

        // Phase-specific display and flags
        string phasePrefix = CurrentCase.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "EVIDENCE"
        };

        string attorneyPrefix = string.IsNullOrEmpty(offeringAttorney) ? "" : $"Offered by: {offeringAttorney}\n";
        doc.IsDiscoveryComplete = CurrentCase.TrialPhase != TrialPhase.Trial;
        TranscriptOutput = $"[{phasePrefix}] Exhibit {doc.ExhibitNumber}: {fileName}\n" +
                           $"{attorneyPrefix}" +
                           $"{summary}\n" +
                           $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}, " +
                           $"Settlement: ${CurrentCase.EstimatedSettlement:N0}, " +
                           $"Reserve: ${CurrentCase.InsuranceReserve:N0}";

        // Broadcast the full user-edited summary to all agents so they form rich memories
        BroadcastEvent($"{phasePrefix} Evidence - Exhibit {doc.ExhibitNumber}: {fileName}\n{attorneyPrefix}{summary}\n" +
                       $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}",
            Enum.GetValues<AgentRole>().ToList());

        // Evidence loop: reporter reads the exhibit into the record, then each juror
        // independently internalizes that reporter summary through their own bias.
        var reporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
        AIModelConfiguration? reporterModel = reporter != null
            ? (CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == reporter.SelectedModel)
               ?? CurrentCase.AvailableModels.FirstOrDefault())
            : CurrentCase.AvailableModels.FirstOrDefault();

        string reporterSummary;
        if (reporter != null && reporterModel != null)
        {
            reporterSummary = await _agentInteraction.SummarizeEvidenceForRecordAsync(reporter, doc, CurrentCase, reporterModel);
            reporter.DocumentCount++;
            TranscriptOutput += $"\n{reporter.Name}: {reporterSummary}";
        }
        else
        {
            reporterSummary = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}, admitted into evidence. {summary}";
        }

        // Each juror reads the reporter's summary and forms their own bias-colored reaction
        var defaultModel = CurrentCase.AvailableModels.FirstOrDefault();
        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            AIModelConfiguration? jurorModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == juror.SelectedModel)
                                               ?? defaultModel;
            string reaction = jurorModel != null
                ? await _agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                : reporterSummary;
            juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
        }
        
        NotifyJuryUpdate();
    }

    /// <summary>
    /// Refreshes the Exhibits collection from the current case evidence list.
    /// </summary>
    public void RefreshExhibits()
    {
        Exhibits.Clear();
        foreach (var doc in CurrentCase.Evidence)
        {
            Exhibits.Add(doc);
        }
    }

    /// <summary>
    /// Reads the content of a file for analysis. Delegates to IEvidenceAnalysisService.
    /// </summary>
    public string ReadFileContent(string filePath) =>
        _evidenceService.ReadFileContent(filePath);

    /// <summary>
    /// Generates a detailed document analysis using the LLM. Delegates to IEvidenceAnalysisService.
    /// Used by the clerk UI to show the analysis before the user submits.
    /// </summary>
    public Task<(string Analysis, double TotalDamages)> GenerateDocumentAnalysisAsync(
        string fileName, string fileContent, string userSummary) =>
        _evidenceService.GenerateDocumentAnalysisAsync(
            fileName, fileContent, userSummary, CurrentCase, _agentInteraction);

    /// <summary>
    /// Delegates exposure calculation to IEvidenceAnalysisService.
    /// </summary>
    public void CalculateExposure() => _evidenceService.CalculateExposure(CurrentCase);

    private void NotifyJuryUpdate()
    {
        OnPropertyChanged(nameof(JuryLiabilityAverage));
        OnPropertyChanged(nameof(LikelyVerdict));
    }

    // ------------------------------
    // Turn-based deliberation
    // ------------------------------

    private readonly HashSet<Guid> _spokenJurorIds = new();
    private int _deliberationRound;
    private readonly List<string> _deliberationHighlights = new();
    private string _earlyThoughtLeaderName = string.Empty;
    private bool _deliberationConcluded;

    private bool _deliberationInitialized;

    // --- Helpers for mock jury reporting ---
    private string BuildJurorEvidenceMemory(Agent juror)
    {
        // Use the juror’s own recorded memories (no neutral records).
        // Keeps the prompt focused on what they already internalized.
        if (juror?.TrialEvents == null || juror.TrialEvents.Count == 0) return "(no remembered trial events)";
        return string.Join("\n", juror.TrialEvents
            .TakeLast(10)
            .Select(e => $"- {e.Content}"));
    }

    private string BuildJurorExhibitMemory(Agent juror)
    {
        // If reporter exhibit log was replayed into juror trial events, TrialEvents is enough,
        // but we keep this for compatibility with existing UI/reporting.
        if (juror?.ExhibitLog == null || juror.ExhibitLog.Count == 0) return "(no remembered exhibit summaries)";
        return string.Join("\n", juror.ExhibitLog.TakeLast(8));
    }

    private string BuildRecentJuryTurnTranscript(IEnumerable<Agent> jurors)
    {
        // Only use the court record highlights already placed into TranscriptOutput.
        // The UI only needs context, not the full transcript.
        return string.IsNullOrWhiteSpace(TranscriptOutput)
            ? "(no recent jury statements)"
            : TranscriptOutput.Length > 1200 ? TranscriptOutput[^1200..] : TranscriptOutput;
    }

    private string BuildTurnHighlight(Agent juror, string jurorLine, bool isStrong)
    {
        // Extract “what evidence mattered” heuristically.
        // We keep this robust: if the LLM provides no explicit evidence tokens, we fallback to lean.
        string tone = isStrong ? "Strong" : "Moderate";
        return $"[Round {_deliberationRound}] {tone} view by {juror.Name}: {jurorLine}";
    }

    private string BuildInterimMockReportPrefix(string label)
    {
        // Keep mock report compact.
        var early = string.IsNullOrEmpty(_earlyThoughtLeaderName) ? "(not identified yet)" : _earlyThoughtLeaderName;
        return $"[JURY REPORT] {label} | Early thought leader: {early} | Likely verdict: {LikelyVerdict}";
    }

    private void ConcludeDeliberation(List<Agent> jurors, bool hungPossible)
    {
        // Consensus is based on the existing LikelyVerdict + lean spread.
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!voters.Any())
        {
            TranscriptOutput = "[JURY REPORT] No jurors seated.";
            return;
        }

        double maxLean = voters.Max(v => v.VerdictLean);
        double minLean = voters.Min(v => v.VerdictLean);
        double spread = maxLean - minLean;

        bool reachesConsensus = spread <= 0.10; // tolerance: close in lean
        string outcome = LikelyVerdict;

        if (hungPossible && !reachesConsensus)
            outcome = "Hung jury possible / no clear consensus";

        var early = string.IsNullOrEmpty(_earlyThoughtLeaderName) ? "(not identified)" : _earlyThoughtLeaderName;
        TranscriptOutput = $"[JURY REPORT] Deliberation concluded. Early thought leader: {early}. {outcome}. Rounds used: {_deliberationRound}/{CurrentCase.DeliberationRounds}.";

        BroadcastEvent(TranscriptOutput, Enum.GetValues<AgentRole>().ToList(), isSidebar: false);
    }

    private void RecordAndBroadcastJuryTurn(Agent juror, string jurorLine, bool leansPlaintiff)
    {
        // Ensure highlight-only UI record; do not spam full turns.
        // Also keeps the transcript box scroll behavior (UI already scrolls).
        string prefix = leansPlaintiff ? "Plaintiff lean" : "Defense lean";
        string entry = $"{juror.Name} ({prefix}): {jurorLine}";
        BroadcastEvent(entry, Enum.GetValues<AgentRole>().ToList(), isSidebar: false);
    }


    public void StartDeliberation()
    {
        _spokenJurorIds.Clear();
        _deliberationRound = 0;
        _deliberationHighlights.Clear();
        _earlyThoughtLeaderName = string.Empty;
        _deliberationConcluded = false;
        _deliberationInitialized = true;

        TranscriptOutput = "[JURY REPORT] Deliberation begins. Generating juror turns...";
        OnPropertyChanged(nameof(TranscriptOutput));

        // lock stage for UI
        _currentDebateStage = CourtPhase.JuryDeliberation;
    }


    public async Task DeliberateNextTurn()
    {
        if (!_deliberationInitialized)
            StartDeliberation();

        var jurors = AllAgents
            .Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror) && a.CanVote)
            .ToList();

        if (jurors.Count == 0)
        {
            TranscriptOutput = "[DELIBERATION] No jurors seated.";
            return;
        }

        // Determine which juror gets to speak next: strongest feelings that haven't spoken yet.
        // Strong opinion threshold is based on distance from the neutral point (0.5).
        const double strongThreshold = 0.18;

        var candidates = jurors
            .Where(j => !_spokenJurorIds.Contains(j.AgentId))

            .Select(j => new
            {
                Agent = j,
                Strength = Math.Abs(j.VerdictLean - 0.5)
            })
            .OrderByDescending(x => x.Strength)
            .ToList();

        if (!candidates.Any())
        {
            TranscriptOutput = "[DELIBERATION] All jurors with strong opinions have spoken.";
            return;
        }

        var next = candidates[0];
        bool isStrong = next.Strength >= strongThreshold;

        _spokenJurorIds.Add(next.Agent.AgentId);



        string side = next.Agent.VerdictLean >= 0.5 ? "plaintiff" : "defense";

        // Build a simple turn line. We can later replace this with LLM output.
        // For now, it derives from the juror’s lean and a short evidence-based rationale from existing memories/exhibits.
        string rationale = isStrong
            ? "because the evidence aligns with my current assessment of liability/damages"
            : "because I still have questions and need clarification from other jurors";

        string line = isStrong
            ? $"{next.Agent.Name} (strong): I’m convinced the case supports the {side} side {rationale}."
            : $"{next.Agent.Name} (medium/unswayed): I’m not fully decided—can someone explain how the evidence impacts {side} liability/damages?";

        // Apply deliberation update: jurors “drift” slightly toward the group consensus.
        // This keeps the turns dynamic even without stage-specific LLM responses.
        var jurorList = jurors;
        var deltaEvidence = (next.Agent.VerdictLean - 0.5) * 0.25; // small movement bias
        ToyJurorLogicEngine.ApplyCoherentDriftDeliberation(jurorList, deltaEvidence, null, seed: null);

        // Recalculate verdict display
        NotifyJuryUpdate();

        TranscriptOutput = $"{TranscriptOutput}\n\n{line}";

        // Broadcast this turn into the core court record (so the user sees it in the UI record area).
        BroadcastEvent(line, Enum.GetValues<AgentRole>().ToList(), isSidebar: false);
    }

/// <summary>
    /// Processes a single line of transcript, delegating influence calculation
    /// to IDebateService and opinion spread to IJuryCalculationService.
    /// Also triggers LLM-based agent responses based on the current debate stage.
    /// </summary>
    // Transcript is now secondary: we record/broadcast it for context only.
    // Evidence admission is what drives juror weighing.
    public async void ProcessTranscriptLine(string speaker, string content)

    {
        if (string.IsNullOrEmpty(content)) return;

        TranscriptOutput = string.IsNullOrWhiteSpace(speaker)
            ? content
            : $"{speaker}: {content}";

        // Broadcast without applying transcript influence and without triggering any LLM stage reactions.
        var roles = Enum.GetValues<AgentRole>().ToList();
        BroadcastEvent(TranscriptOutput, roles);

        // Trigger stage-based AI responses (now subject to runtime restrictions).
        // TriggerAgentResponses is designed to be called after user input.
        TriggerAgentResponses(speaker, content);

    }



    /// <summary>
    /// Triggers appropriate LLM-based agent responses based on the current debate stage.
    /// </summary>
    private async Task TriggerAgentResponses(string speaker, string content)
    {
        try
        {
            var stage = CurrentCase.CurrentDebateStage;
            var transcript = BuildTranscriptSummary();

            // Reporter always produces a neutral factual record first,
            // then broadcasts it as a memory to all agents.
            var reporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
            if (reporter != null)
            {
                AIModelConfiguration? reporterModel = null;
                if (!string.IsNullOrEmpty(reporter.SelectedModel))
                    reporterModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == reporter.SelectedModel);
                reporterModel ??= CurrentCase.AvailableModels.FirstOrDefault();

                if (reporterModel != null)
                {
                    var reporterRequest = new StatementRequest
                    {
                        Role = AgentRole.Reporter,
                        SpeakerName = reporter.Name,
                        Context = $"Record the following courtroom statement neutrally. Speaker: {speaker}. Statement: {content}",
                        Type = MessageType.Statement,
                        CaseData = CurrentCase,
                        Transcript = transcript,
                        Model = reporterModel
                    };

                    var reporterResult = await _agentInteraction.GenerateStatementAsync(reporterRequest);
                    if (reporterResult.Success && reporterResult.Message != null)
                    {
                        string record = reporterResult.Message.Content;
                        TranscriptOutput += $"\n{reporter.Name}: {record}";

                        // Jurors internalize the record through their own bias/background and record personal impact.
                        // Non-juror agents (lawyers, judge, etc.) store the neutral record as-is.
                        // Reporters do not form personal reactions.
                        var model = CurrentCase.AvailableModels.FirstOrDefault();
                        foreach (var agent in AllAgents.Where(a => a.IsOccupied && a.Role != AgentRole.Reporter))
                        {
                            AIModelConfiguration? agentModel = null;
                            if (!string.IsNullOrEmpty(agent.SelectedModel))
                                agentModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);
                            agentModel ??= model;

                            if (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror)
                            {
                                // Jurors form a personal, bias-colored reaction and record it with impact
                                string memory = agentModel != null
                                    ? await _agentInteraction.InternalizeCourtRecordAsync(agent, record, agentModel)
                                    : record;
                                agent.RecordTrialEvent(memory, agent.Bias * 0.1);
                            }
                            else
                            {
                                // Other agents (judge, lawyers, witnesses) store the neutral record without bias impact
                                agent.RecordTrialEvent(record, 0.0);
                            }
                        }

                        reporter.DocumentCount++;
                    }
                }
            }

            switch (stage)
            {
                case CourtPhase.OpeningStatements:
                    // After a user message, have the opposing counsel respond
                    if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney", "Respond to the opening statement.", transcript);
                    }
                    else if (speaker.Contains("Defense"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor", "Respond to the opening statement.", transcript);
                    }
                    break;

                case CourtPhase.WitnessTestimony:
                case CourtPhase.CrossExamination:
                    // Have the judge respond to the testimony
                    await GenerateAgentResponse(AgentRole.Judge, "Judge", $"Acknowledge the testimony and move the trial forward. Speaker: {speaker}", transcript);
                    break;

                case CourtPhase.ClosingArguments:
                    // Have the opposing counsel respond
                    if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney", "Respond to the closing argument.", transcript);
                    }
                    else if (speaker.Contains("Defense"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor", "Respond to the closing argument.", transcript);
                    }
                    break;

                default:
                    // Default: have the judge acknowledge
                    await GenerateAgentResponse(AgentRole.Judge, "Judge", $"Acknowledge the statement from {speaker}.", transcript);
                    break;
            }
        }
        catch (Exception ex)
        {
            TranscriptOutput += $"\n[AI RESPONSE ERROR] {ex.Message}";
        }
    }

    /// <summary>
    /// Generates an LLM response for a specific agent role and appends it to the transcript.
    /// </summary>
    private async Task GenerateAgentResponse(AgentRole role, string speakerName, string context, List<AgentMessage> transcript)
    {
        // Find an agent of this role to get their selected model
        var agent = AllAgents.FirstOrDefault(a => a.Role == role && a.IsOccupied);
        if (agent == null) return;

        // Find the model configuration for this agent
        AIModelConfiguration? modelConfig = null;
        if (!string.IsNullOrEmpty(agent.SelectedModel))
        {
            modelConfig = CurrentCase.AvailableModels
                .FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);
        }
        modelConfig ??= CurrentCase.AvailableModels.FirstOrDefault();

        var request = new StatementRequest
        {
            Role = role,
            SpeakerName = speakerName,
            Context = context,
            Type = MessageType.Statement,
            CaseData = CurrentCase,
            Transcript = transcript,
            Model = modelConfig
        };

        var result = await _agentInteraction.GenerateStatementAsync(request);
        if (result.Success && result.Message != null)
        {
            TranscriptOutput += $"\n{result.Message.Speaker}: {result.Message.Content}";
            BroadcastEvent($"{result.Message.Speaker}: {result.Message.Content}",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
        }
    }

    /// <summary>
    /// Builds a summary of recent transcript entries for context.
    /// </summary>
    private List<AgentMessage> BuildTranscriptSummary()
    {
        var messages = new List<AgentMessage>();
        if (!string.IsNullOrEmpty(TranscriptOutput))
        {
            messages.Add(new AgentMessage
            {
                Speaker = "System",
                Content = TranscriptOutput,
                Type = MessageType.Statement,
                IsFromAI = false
            });
        }
        return messages;
    }

    /// <summary>
    /// Loads a transcript file and processes it line by line.
    /// </summary>
    public async Task LoadTranscript(string filePath)
    {
        foreach (var (speaker, content) in _transcriptService.LoadTranscript(filePath))
        {
            ProcessTranscriptLine(speaker, content);
            await Task.Delay(100); // Small delay between lines for readability
        }
    }

    /// <summary>
    /// Saves the current case state to a file.
    /// </summary>
    public void SaveCase(string filePath)
    {
        _caseService.SaveCase(filePath, CurrentCase, AllAgents);
    }

    /// <summary>
    /// Loads a case from a file and restores the courtroom state.
    /// </summary>
    public void LoadCase(string filePath)
    {
        var loadedCase = _caseService.LoadCase(filePath);
        if (loadedCase != null)
        {
            loadedCase.EnsureCollectionsInitialized();
            CurrentCase = loadedCase;
            RefreshExhibits();

            // Restore the courtroom agent collections from the loaded case's agents
            _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, loadedCase);

            foreach (var agent in loadedCase.Agents)
            {
                if (!agent.IsOccupied) continue;

                // Match loaded agents to existing seat templates by AgentId to preserve observer edits.
                // If the ID is not found (older save), fall back to first available slot.
                var seatMatched = AllAgents.FirstOrDefault(a => a.Role == agent.Role && a.AgentId == agent.AgentId && !a.Equals(agent));

                switch (agent.Role)
                {
                    case AgentRole.Judge:
                    case AgentRole.Witness:
                    case AgentRole.Reporter:
                        var judgeSlot = (seatMatched ?? JudgeArea.FirstOrDefault(a => a.Role == agent.Role && !a.IsOccupied));
                        if (judgeSlot != null) agent.CopyTo(judgeSlot);
                        break;

                    case AgentRole.Juror:
                        var jurorSlot = (seatMatched ?? Jurors.FirstOrDefault(a => !a.IsOccupied));
                        if (jurorSlot != null) agent.CopyTo(jurorSlot);
                        break;

                    case AgentRole.AlternateJuror:
                        var altSlot = (seatMatched ?? Gallery.FirstOrDefault(a => a.Role == AgentRole.AlternateJuror && !a.IsOccupied));
                        if (altSlot != null) agent.CopyTo(altSlot);
                        break;

                    case AgentRole.Lawyer:
                        var lawyerSlot = (seatMatched ?? DefenseTeam.FirstOrDefault(a => !a.IsOccupied));
                        if (lawyerSlot != null)
                        {
                            agent.CopyTo(lawyerSlot);
                        }
                        else
                        {
                            var prosSlot = (seatMatched ?? ProsecutionTeam.FirstOrDefault(a => !a.IsOccupied));
                            if (prosSlot != null) agent.CopyTo(prosSlot);
                        }
                        break;

                    case AgentRole.Client:
                    case AgentRole.Observer:
                        var obsSlot = (seatMatched ?? Gallery.FirstOrDefault(a => a.Role == agent.Role && !a.IsOccupied));
                        if (obsSlot != null) agent.CopyTo(obsSlot);
                        break;
                }
            }

            // Debate stage is disabled; always present as deliberation.
            _currentDebateStage = CourtPhase.JuryDeliberation;
            OnPropertyChanged(nameof(CurrentDebateStage));
            OnPropertyChanged(nameof(CurrentDebateStageDisplay));


            // Notify UI of case property changes
            OnPropertyChanged(nameof(CurrentCase));
            NotifyJuryUpdate();

            TranscriptOutput = $"[CASE LOADED] Case '{loadedCase.CaseName}' loaded successfully.";
            UpdateWindowTitle();
        }
    }

/// <summary>
    /// Extracts entities from the loaded transcript using LLM analysis.
    /// Delegates entity mapping to ICaseEntityMapper.
    /// </summary>
    public async Task ExtractEntitiesFromTranscriptAsync()
    {
        var model = CurrentCase.AvailableModels.FirstOrDefault();
        if (model == null)
        {
            TranscriptOutput = "[ERROR] No AI model configured. Please configure a model first.";
            return;
        }

        var transcript = TranscriptOutput;
        if (string.IsNullOrEmpty(transcript) || transcript == "Court is in session. Awaiting transcript...")
        {
            TranscriptOutput = "[ERROR] No transcript loaded to extract entities from.";
            return;
        }

        TranscriptOutput = "[EXTRACTING] Analyzing transcript for entities...";

        try
        {
            var entities = await _transcriptService.ExtractEntitiesAsync(transcript, model);
            string summary = _entityMapper.MapToCaseFile(CurrentCase, entities);
            TranscriptOutput = $"[EXTRACTED] {summary}";
        }
        catch (Exception ex)
        {
            TranscriptOutput = $"[ERROR] Entity extraction failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Resets case data appropriately when changing to an earlier phase.
    /// Delegates opinion reset to IJuryCalculationService.
    /// </summary>
    public void AdjustDataForPhaseChange(TrialPhase newPhase, TrialPhase oldPhase)
    {
        if (newPhase < oldPhase)
        {
            if (oldPhase == TrialPhase.Trial && newPhase < TrialPhase.Trial)
            {
                _juryCalc.ResetTrialOpinions(Jurors);
                TranscriptOutput = $"[PHASE CHANGE] Returned to {newPhase}, trial opinions reset.";
            }
            else if (oldPhase == TrialPhase.Pretrial && newPhase == TrialPhase.Discovery)
            {
                CurrentCase.EstimatedSettlement = 0;
                CurrentCase.InsuranceReserve = 0;
                TranscriptOutput = $"[PHASE CHANGE] Returned to Discovery phase, exposure reset.";
            }
        }
        else if (newPhase > oldPhase)
        {
            TranscriptOutput = $"[PHASE CHANGE] Advanced to {newPhase} phase.";
        }
        
        NotifyJuryUpdate();
    }

    // TODO: Implement test runner when Tests project is properly integrated
    // public static async Task TestAsync()
    // {
    //     await Verdict.Tests.TestHarness.RunTests();
    // }

    /// <summary>
    /// Re-evaluates all admitted evidence for every juror using their current psychology.
    /// The reporter's exhibit log is replayed through each juror's current RoleSystemPrompt,
    /// bias, and demographics — clearing old trial events and replacing them with fresh reactions.
    /// </summary>
    public async Task ReevaluateEvidenceAsync()
    {
        var reporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
        if (reporter == null || reporter.ExhibitLog.Count == 0)
        {
            TranscriptOutput = "[REEVALUATE] No reporter exhibit log found. Admit evidence first.";
            return;
        }

        var defaultModel = CurrentCase.AvailableModels.FirstOrDefault();
        int jurorCount = 0;

        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            // Clear previous evidence reactions so fresh psychology is applied
            juror.TrialEvents.Clear();
            juror.ConsideredDamages = 0;
            juror.VerdictLean = 0.5;

            AIModelConfiguration? jurorModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == juror.SelectedModel)
                                               ?? defaultModel;

            // Replay every exhibit through the juror's current psychology
            foreach (string exhibitSummary in reporter.ExhibitLog)
            {
                string reaction = jurorModel != null
                    ? await _agentInteraction.InternalizeCourtRecordAsync(juror, exhibitSummary, jurorModel)
                    : exhibitSummary;
                juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
            }

            jurorCount++;
        }

        // Recalculate damages and verdict lean from scratch
        foreach (var doc in CurrentCase.Evidence)
            _juryCalc.ApplyEvidenceInfluence(AllAgents, doc);

        NotifyJuryUpdate();
        TranscriptOutput = $"[REEVALUATE] {jurorCount} jurors re-evaluated {reporter.ExhibitLog.Count} exhibits with current psychology.";
    }

    /// <summary>
    /// Generates a PDF report for the current case.
    /// </summary>
    public void GeneratePDFReport(string filePath)
    {
        _caseService.GenerateReport(filePath, CurrentCase, AllAgents);
        TranscriptOutput = $"[REPORT] PDF case report generated at {filePath}";
    }

    /// <summary>
    /// Applies dynamic role assignment based on case analysis.
    /// Automatically assigns agents to recommended roles.
    /// </summary>
    public void ApplyDynamicRoleAssignment()
    {
        var assignmentService = new AgentAssignmentService();
        var plan = assignmentService.AnalyzeCase(CurrentCase);

        // Generate default agents
        var defaultAgents = assignmentService.GenerateDefaultAgents(CurrentCase);

        // Assign default agents to appropriate slots
        foreach (var agent in defaultAgents)
        {
            if (agent.Role == AgentRole.Judge)
            {
                var judgeSlot = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Judge && !a.IsOccupied);
                if (judgeSlot != null)
                {
                    agent.CopyTo(judgeSlot);
                }
            }
            else if (agent.Role == AgentRole.Witness)
            {
                var witnessSlot = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Witness && !a.IsOccupied);
                if (witnessSlot != null)
                {
                    agent.CopyTo(witnessSlot);
                }
            }
        }

        // Add extra lawyer slots as recommended
        assignmentService.ApplyAssignmentPlan(plan, JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery);

        // Update court with analysis
        TranscriptOutput = $"[ROLE ASSIGNMENT] {plan.StrategyDescription} " +
                          $"Recommended: {plan.RecommendedProsecutionAttorneys} prosecutors, " +
                          $"{plan.RecommendedDefenseAttorneys} defense attorneys, " +
                          $"{plan.RecommendedExperts.Count} experts. " +
                          $"Complexity: {plan.ComplexityRating}/10.";
        BroadcastEvent($"Dynamic role assignment completed: {plan.StrategyDescription}",
            Enum.GetValues<AgentRole>().ToList());
    }
}
