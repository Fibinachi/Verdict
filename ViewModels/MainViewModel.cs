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
    private readonly IDeliberationService _deliberationService;
    public IAgentInteractionService AgentInteractionService => _agentInteraction;

    private CaseFile _currentCase = new();
    private string _transcriptOutput = "";
    private CourtPhase _currentDebateStage = CourtPhase.OpeningStatements;
    private string _windowTitle = "VERDICT";
    private string _generatedClosingArgument = string.Empty;

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    /// <summary>
    /// Returns "PROSECUTION TABLE" for criminal cases, "PLAINTIFF TABLE" for civil.
    /// </summary>
    public string ProsecutionTableLabel =>
        (_currentCase?.Mode == CaseMode.Criminal) ? "PROSECUTION TABLE" : "PLAINTIFF TABLE";

    /// <summary>
    /// Updates the window title based on current case information.
    /// Format: VERDICT - Whoever v. Whoever et al. - (CASE STAGE)
    /// </summary>
    public void UpdateWindowTitle()
    {
        var caseName = _currentCase?.CaseName ?? "Untitled Case";
        var plaintiffs = _currentCase?.Plaintiffs ?? new();
        var defendants = _currentCase?.Defendants ?? new();
        bool isCriminal = _currentCase?.Mode == CaseMode.Criminal;

        string plaintiffPart = plaintiffs.Count > 0 ? string.Join(", ", plaintiffs) : "Unknown";
        string defendantPart = defendants.Count > 0 ? string.Join(", ", defendants) : "Unknown";

        // If there are multiple parties, add "et al."
        string plaintiffDisplay = plaintiffs.Count > 1 ? $"{plaintiffs[0]} et al." : plaintiffPart;
        string defendantDisplay = defendants.Count > 1 ? $"{defendants[0]} et al." : defendantPart;

        // Criminal cases show "State" or "People" as the prosecuting party
        string prosecutingDisplay = isCriminal ? "State" : plaintiffDisplay;

        string caseStage = _currentCase?.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "UNKNOWN"
        };

        WindowTitle = $"VERDICT - {prosecutingDisplay} v. {defendantDisplay} - ({caseStage})";
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

    /// <summary>Dynamic label for the court record panel header.</summary>
    public string CourtRecordLabel => _currentDebateStage switch
    {
        CourtPhase.CaseGeneration => "CASE GENERATION",
        CourtPhase.LegalResearch => "LEGAL RESEARCH",
        CourtPhase.OpeningStatements => "OPENING STATEMENTS",
        CourtPhase.WitnessTestimony => "WITNESS TESTIMONY",
        CourtPhase.CrossExamination => "CROSS EXAMINATION",
        CourtPhase.PartyStatements => "PARTY STATEMENTS",
        CourtPhase.ClosingArguments => "CLOSING ARGUMENTS",
        CourtPhase.JuryInstructions => "JURY INSTRUCTIONS",
        CourtPhase.JuryDeliberation => "JURY DELIBERATION",
        CourtPhase.VerdictAnnouncement => "VERDICT",
        _ => "COURT RECORD"
    };

    /// <summary>Human-readable trial phase label for the status bar.</summary>
    public string TrialPhaseLabel => CurrentCase?.TrialPhase switch
    {
        TrialPhase.Discovery => "Discovery",
        TrialPhase.Pretrial => "Pretrial",
        TrialPhase.Trial => "Trial",
        _ => "Pre-Trial"
    };

    /// <summary>Dynamic threshold label: LIABILITY SCORE for civil, CONVICTION SCORE for criminal.</summary>
    public string VerdictThresholdLabel => CurrentCase?.Mode == CaseMode.Criminal
        ? "CONVICTION:"
        : "LIABILITY:";

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
            OnPropertyChanged(nameof(ProsecutionTableLabel));
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

    public CourtPhase CurrentDebateStage
    {
        get => _currentDebateStage;
        set
        {
            if (SetProperty(ref _currentDebateStage, value))
            {
                // Keep the case file in sync
                if (_currentCase != null)
                    _currentCase.CurrentDebateStage = value;

                // Memories decay with each stage transition —
                // exact numbers become approximate, then ranges, then vague impressions.
                // Using 0.92 (8% per transition) so 3 transitions preserve ~78% of memory strength.
                foreach (var agent in AllAgents.Where(a => a.IsOccupied))
                    agent.DecayMemories(0.92);

                OnPropertyChanged(nameof(CurrentDebateStageDisplay));
                OnPropertyChanged(nameof(CourtRecordLabel));
            }
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
    /// Refreshes phase-dependent labels (TrialPhaseLabel, VerdictThresholdLabel, CourtRecordLabel).
    /// Called from stage handlers after changing TrialPhase or related properties.
    /// </summary>
    public void RefreshPhaseLabels()
    {
        OnPropertyChanged(nameof(TrialPhaseLabel));
        OnPropertyChanged(nameof(VerdictThresholdLabel));
        OnPropertyChanged(nameof(CourtRecordLabel));
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
    public string LikelyVerdict => _juryCalc.LikelyVerdict(Jurors, _currentCase?.Mode ?? CaseMode.Civil);

    public ObservableCollection<Agent> JudgeArea { get; } = new();
    public ObservableCollection<Agent> Jurors { get; } = new();
    public ObservableCollection<Agent> DefenseTeam { get; } = new();
    public ObservableCollection<Agent> ProsecutionTeam { get; } = new();
    public ObservableCollection<Agent> Gallery { get; } = new();
    public ObservableCollection<Agent> AlternateJurors { get; } = new();

    public ObservableCollection<EvidenceDocument> Exhibits { get; } = new();

    /// <summary>
    /// Returns an aggregation of all agents in all areas of the courtroom.
    /// Materialized with ToList() to prevent "Collection was modified" exceptions
    /// when the underlying ObservableCollections change during enumeration.
    /// </summary>
    public IEnumerable<Agent> AllAgents => 
        JudgeArea.Concat(Jurors).Concat(AlternateJurors).Concat(DefenseTeam).Concat(ProsecutionTeam).Concat(Gallery).ToList();

    /// <summary>
    /// Merges case-specific models with universal defaults. Case models take priority
    /// when a model with the same Provider+ModelId exists in both. This ensures every
    /// case has access to all configured providers while preserving case-specific API keys.
    /// </summary>
    public List<AIModelConfiguration> EffectiveModels
    {
        get
        {
            var merged = new List<AIModelConfiguration>();
            var seen = new HashSet<string>();

            // Case-specific models first (highest priority)
            foreach (var m in CurrentCase.AvailableModels)
            {
                var key = $"{m.Provider}|{m.ModelId}".ToLowerInvariant();
                if (seen.Add(key))
                    merged.Add(m);
            }

            // Universal defaults fill in gaps
            var defaults = _settingsService.GetDefaultCaseSettings();
            defaults.EnsureCollectionsInitialized();
            foreach (var m in defaults.AvailableModels)
            {
                var key = $"{m.Provider}|{m.ModelId}".ToLowerInvariant();
                if (seen.Add(key))
                    merged.Add(m);
            }

            return merged;
        }
    }

    /// <summary>
    /// Looks up a model by friendly name, searching both case-specific and universal defaults.
    /// </summary>
    public AIModelConfiguration? FindModel(string friendlyName)
    {
        return EffectiveModels.FirstOrDefault(m => m.FriendlyName == friendlyName);
    }

    /// <summary>
    /// Returns the best available model — first from the case, then from universal defaults.
    /// </summary>
    public AIModelConfiguration? DefaultModel => EffectiveModels.FirstOrDefault();

    public MainViewModel()
        : this(new CaseService(), new TranscriptService(), new SettingsService(),
               new JuryDemographicsService(), new CourtroomManagerService(),
               new EvidenceAnalysisService(), new DebateService(),
               new JuryCalculationService(), null,
               new AgentInteractionService(ProviderDiscoveryService.GetAvailableProviders()),
               new DeliberationService())
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
        IAgentInteractionService? agentInteraction = null,
        IDeliberationService? deliberationService = null)
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
        _deliberationService = deliberationService ?? new DeliberationService();

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
        // Move alternates from Gallery to dedicated AlternateJurors collection
        MoveAlternatesFromGallery();
        RefreshExhibits();

        // Default to Jury Deliberation on a new case
        CurrentDebateStage = CourtPhase.JuryDeliberation;

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
    /// Parses county and state from JurisdictionSpecifics and CourtName.
    /// </summary>
    public void GenerateJury()
    {
        var (county, state) = ParseJurisdiction();

        // Generate only 12 regular jurors (no alternates)
        var generatedJurors = _juryService.GenerateJuryPanel(12, 0, county, state);

        // Replace current Jurors collection contents
        Jurors.Clear();
        foreach (var juror in generatedJurors)
        {
            Jurors.Add(juror);
        }

        // Replay trial history through all new jurors so they have the same
        // stimuli (event log + exhibit memories) as any existing jurors.
        foreach (var juror in generatedJurors)
        {
            ProcessTrialHistoryForJuror(juror);
        }

        TranscriptOutput = $"[JURY GENERATED] {generatedJurors.Count} jurors generated for {county}, {state}";
        if (CurrentCase.EventLog.Count > 0 || CurrentCase.Evidence.Count > 0)
            TranscriptOutput += $"\n  → Trial history replayed: {CurrentCase.EventLog.Count} events, {CurrentCase.Evidence.Count} exhibits";
        BroadcastEvent($"Jury panel generated based on {county} County demographics", 
            Enum.GetValues<AgentRole>().ToList());
    }

    /// <summary>
    /// Generates alternate jurors based on the case's jurisdiction demographics.
    /// Also replays trial history so alternates have the same stimuli as regular jurors.
    /// </summary>
    public void GenerateAlternates(int count = 2)
    {
        var (county, state) = ParseJurisdiction();

        var generatedAlternates = _juryService.GenerateJuryPanel(0, count, county, state);

        AlternateJurors.Clear();
        foreach (var alternate in generatedAlternates)
        {
            alternate.IsOccupied = true;
            AlternateJurors.Add(alternate);
            // Replay trial history so alternates share the same stimuli
            ProcessTrialHistoryForJuror(alternate);
        }

        TranscriptOutput = $"[ALTERNATES GENERATED] {generatedAlternates.Count} alternate jurors generated for {county}, {state}";
        BroadcastEvent($"Alternate jurors generated based on {county} County demographics",
            Enum.GetValues<AgentRole>().ToList());
    }

    /// <summary>
    /// Parses county and state from the current case's jurisdiction metadata.
    /// Tries multiple sources: JurisdictionSpecifics (comma format), CourtName,
    /// and JurisdictionSpecifics (sentence format like "Wisconsin. Self-defense...").
    /// </summary>
    private (string county, string state) ParseJurisdiction()
    {
        var specifics = CurrentCase.JurisdictionSpecifics ?? "";
        var courtName = CurrentCase.CourtName ?? "";

        // ── Try comma-separated "County, State" format first ──
        if (!string.IsNullOrWhiteSpace(specifics))
        {
            string[] parts = specifics.Split(',');
            if (parts.Length >= 2)
            {
                string first = parts[0].Trim();
                string second = parts[1].Trim();

                // Heuristic: if first part looks like a county (contains "County") and
                // second looks like a state (no legal terminology)
                bool firstIsCounty = first.Contains("County", StringComparison.OrdinalIgnoreCase);
                bool secondIsState = !second.Contains("Stat.") && !second.Contains("defense")
                    && !second.Contains("Code") && !second.Contains("§") && second.Length < 30;

                if (firstIsCounty && secondIsState)
                    return (first, second);

                // If first is a known state name, reverse
                if (CountyDemographicsDatabase.StateNameToAbbreviation(first) != "SC"
                    && !first.Contains("County", StringComparison.OrdinalIgnoreCase)
                    && secondIsState)
                    return (second, first);
            }

            // ── Single-part JurisdictionSpecifics like "Wisconsin. Self-defense..." ──
            // Extract the state name from the beginning (before first period or space)
            string? stateFromSpecifics = ExtractStateFromText(specifics);
            if (!string.IsNullOrEmpty(stateFromSpecifics) && stateFromSpecifics != "SC")
            {
                // Extract county from CourtName
                string? countyFromCourt = ExtractCountyFromCourtName(courtName);
                if (!string.IsNullOrEmpty(countyFromCourt))
                    return (countyFromCourt, stateFromSpecifics);

                // Fall back to state-level demographics
                return ("Statewide", stateFromSpecifics);
            }
        }

        // ── Try CourtName for county ──
        string? countyFromCourtName = ExtractCountyFromCourtName(courtName);
        if (!string.IsNullOrEmpty(countyFromCourtName))
        {
            // Try to get state from JurisdictionSpecifics
            string? stateFromText = ExtractStateFromText(specifics);
            if (!string.IsNullOrEmpty(stateFromText) && stateFromText != "SC")
                return (countyFromCourtName, stateFromText);
        }

        // ── Ultimate fallback ──
        return ("Richland County", "South Carolina");
    }

    /// <summary>
    /// Extracts a county name from a court name like "St. Croix County Circuit Court".
    /// </summary>
    private static string? ExtractCountyFromCourtName(string courtName)
    {
        if (string.IsNullOrWhiteSpace(courtName)) return null;

        // Pattern: "X County Circuit Court" or "X County Superior Court" etc.
        int countyIdx = courtName.IndexOf(" County", StringComparison.OrdinalIgnoreCase);
        if (countyIdx > 0)
        {
            return courtName.Substring(0, countyIdx + " County".Length).Trim();
        }
        return null;
    }

    /// <summary>
    /// Extracts a state name/abbreviation from text like "Wisconsin. Self-defense..."
    /// or "Richland County, South Carolina".
    /// </summary>
    private static string? ExtractStateFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Known state names (longest first to match "South Carolina" before "South Dakota")
        string[] stateNames = { "South Carolina", "North Carolina", "South Dakota", "North Dakota",
            "West Virginia", "New Hampshire", "New Jersey", "New Mexico", "New York",
            "Rhode Island", "District of Columbia",
            "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado",
            "Connecticut", "Delaware", "Florida", "Georgia", "Hawaii", "Idaho",
            "Illinois", "Indiana", "Iowa", "Kansas", "Kentucky", "Louisiana",
            "Maine", "Maryland", "Massachusetts", "Michigan", "Minnesota",
            "Mississippi", "Missouri", "Montana", "Nebraska", "Nevada",
            "Ohio", "Oklahoma", "Oregon", "Pennsylvania", "Tennessee", "Texas",
            "Utah", "Vermont", "Virginia", "Washington", "Wisconsin", "Wyoming" };

        foreach (var stateName in stateNames)
        {
            if (text.StartsWith(stateName, StringComparison.OrdinalIgnoreCase))
                return stateName;

            // Also check for "in {State}" or ", {State}" patterns
            int idx = text.IndexOf(stateName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                // Make sure it's a standalone word
                char before = idx > 0 ? text[idx - 1] : ' ';
                int afterIdx = idx + stateName.Length;
                char after = afterIdx < text.Length ? text[afterIdx] : ' ';
                if (!char.IsLetter(before) && !char.IsLetter(after))
                    return stateName;
            }
        }

        return null;
    }

    /// <summary>
    /// Moves any AlternateJuror agents from the Gallery collection into the
    /// dedicated AlternateJurors collection (called after InitializeCourtroom).
    /// </summary>
    private void MoveAlternatesFromGallery()
    {
        var alternates = Gallery.Where(a => a.Role == AgentRole.AlternateJuror).ToList();
        foreach (var alt in alternates)
        {
            Gallery.Remove(alt);
            AlternateJurors.Add(alt);
        }
    }

    /// <summary>
    /// Returns a condensed summary of all evidence for LLM context prompts.
    /// </summary>
    private string GetEvidenceSummary()
    {
        if (CurrentCase.Evidence.Count == 0)
            return "No evidence has been entered into the record yet.";

        return string.Join("\n", CurrentCase.Evidence.Select((e, i) =>
            $"[Exhibit {e.ExhibitNumber}] {e.FileName} — {e.Summary}" +
            (string.IsNullOrEmpty(e.DetailedAnalysis) ? "" : $"\n    Analysis: {e.DetailedAnalysis}")));
    }

    /// <summary>
    /// Processes all existing evidence through each juror's psychology during Discovery.
    /// Jurors form bias-colored trial event memories of each exhibit.
    /// </summary>
    public async Task RunDiscoveryPhase()
    {
        if (CurrentCase.Evidence.Count == 0)
        {
            TranscriptOutput += "No evidence to process. Use Admit Evidence to add exhibits.\n";
            return;
        }

        var defaultModel = DefaultModel;
        int processed = 0;

        foreach (var doc in CurrentCase.Evidence)
        {
            var reporterSummary = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}: {doc.Summary}";

            foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
            {
                AIModelConfiguration? jurorModel = CurrentCase.AvailableModels
                    .FirstOrDefault(m => m.FriendlyName == juror.SelectedModel) ?? defaultModel;

                string reaction = jurorModel != null
                    ? await _agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                    : reporterSummary;

                juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
                juror.AnalyzeSentiment(reporterSummary);
            }
            processed++;
        }

        TranscriptOutput += $"Discovery complete. {processed} exhibit(s) processed through all jurors.\n";
        TranscriptOutput += "Jurors have formed initial impressions of the evidence.\n";
    }

    /// <summary>
    /// Evaluates all evidence through each juror's psychology during Pretrial.
    /// Jurors establish their initial verdict lean based solely on the evidence,
    /// before any attorney arguments or opening statements.
    /// </summary>
    public async Task RunPretrialEvaluation()
    {
        if (CurrentCase.Evidence.Count == 0)
        {
            TranscriptOutput += "No evidence to evaluate. Add evidence during Discovery first.\n";
            return;
        }

        var defaultModel = DefaultModel;
        int jurorCount = 0;

        // First pass: ensure all evidence is processed as memories
        foreach (var doc in CurrentCase.Evidence)
        {
            var reporterSummary = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}: {doc.Summary}";

            foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
            {
                // Skip if juror already has a memory of this exhibit
                bool hasMemory = juror.TrialEvents.Any(t =>
                    t.Content.Contains($"exhibit {doc.ExhibitNumber}", StringComparison.OrdinalIgnoreCase));
                if (hasMemory) continue;

                AIModelConfiguration? jurorModel = CurrentCase.AvailableModels
                    .FirstOrDefault(m => m.FriendlyName == juror.SelectedModel) ?? defaultModel;

                string reaction = jurorModel != null
                    ? await _agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                    : reporterSummary;

                juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
            }
        }

        // Second pass: apply all evidence influence to establish initial verdict lean
        foreach (var doc in CurrentCase.Evidence)
            _juryCalc.ApplyEvidenceInfluence(AllAgents, doc);

        // Log each juror's initial lean
        var jurors = AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        TranscriptOutput += "PRETRIAL EVALUATION COMPLETE\n";
        TranscriptOutput += $"Evidence evaluated: {CurrentCase.Evidence.Count} exhibit(s)\n";
        TranscriptOutput += $"Jurors polled: {jurors.Count}\n\n";

        TranscriptOutput += "Initial verdict leans (evidence only, no arguments):\n";
        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        foreach (var juror in jurors.OrderByDescending(j => j.VerdictLean))
        {
            string position = BurdenOfProof.GetPositionLabel(juror.VerdictLean, isCriminal);
            TranscriptOutput += $"  {juror.Name}: {juror.VerdictLean:P1} → {position}\n";
        }

        jurorCount = jurors.Count;
        int proCount = BurdenOfProof.CountProsecution(jurors, isCriminal);
        int defCount = BurdenOfProof.CountDefense(jurors, isCriminal);
        string proLabel = BurdenOfProof.ProsecutionLabel(isCriminal);
        string defLabel = BurdenOfProof.DefenseLabel();
        TranscriptOutput += $"\nPrediction: {proCount} for {proLabel}, {defCount} for {defLabel}";
        TranscriptOutput += $" ({jurorCount - proCount - defCount} undecided/undetermined)\n";

        NotifyJuryUpdate();
    }

    /// <summary>
    /// Called when the Opening Statements court phase is selected.
    /// Sets the stage and auto-generates opening statements for both sides via the LLM.
    /// </summary>
    public async Task EnterOpeningStatements()
    {
        CurrentDebateStage = CourtPhase.OpeningStatements;
        CurrentCase.CurrentDebateStage = CourtPhase.OpeningStatements;

        var evidenceSummary = GetEvidenceSummary();
        var transcript = BuildTranscriptSummary();

        TranscriptOutput = "[STAGE] Opening Statements\n\n";

        // Pre-trial juror poll — capture baseline opinions before any arguments
        GeneratePreTrialQuestionnaire();
        TranscriptOutput += "\nThe court is now in session for opening statements.\n";

        // Plaintiff / Prosecution opening statement
        var plaintiffName = CurrentCase.PlaintiffAttorney;
        if (string.IsNullOrWhiteSpace(plaintiffName)) plaintiffName = "Prosecutor";

        await GenerateNamedResponse(ProsecutionTeam, plaintiffName,
            $"You are delivering the opening statement for the plaintiff/prosecution in this case. " +
            $"Be as PERSUASIVE as possible — your goal is to convince the jury of your case. " +
            $"Write approximately 500 words. Present a compelling narrative of what the evidence " +
            $"will prove, frame every fact in the light most favorable to your side. Address the " +
            $"jury directly. Reference specific exhibits by number " +
            $"(e.g., \"Exhibit 1 will show...\", \"As we'll prove with Exhibit 3...\") " +
            $"to anchor your narrative in the evidence.\n\nCase evidence:\n{evidenceSummary}", transcript);

        // Defense opening statement
        var defenseName = CurrentCase.DefenseAttorney;
        if (string.IsNullOrWhiteSpace(defenseName)) defenseName = "Defense Attorney";

        await GenerateNamedResponse(DefenseTeam, defenseName,
            $"You are delivering the opening statement for the defense. " +
            $"Be as PERSUASIVE as possible — your goal is to convince the jury that the " +
            $"plaintiff/prosecution has not met their burden. Write approximately 500 words. " +
            $"Present a compelling narrative that counters the plaintiff's claims, reframing " +
            $"every piece of evidence in the light most favorable to your client. Address the " +
            $"jury directly. Reference specific exhibits by number " +
            $"(e.g., \"Exhibit 2 actually proves...\", \"The defense will rely on Exhibit 4...\") " +
            $"to anchor your narrative in the evidence.\n\nCase evidence:\n{evidenceSummary}", transcript);

        BroadcastEvent("Opening statements concluded. The trial will proceed to witness testimony.",
            Enum.GetValues<AgentRole>().ToList());
    }

    /// <summary>
    /// Called when the Closing Arguments court phase is selected.
    /// Auto-generates persuasive closing arguments for both sides via the LLM.
    /// </summary>
    public async Task EnterClosingArguments()
    {
        CurrentDebateStage = CourtPhase.ClosingArguments;
        CurrentCase.CurrentDebateStage = CourtPhase.ClosingArguments;

        var evidenceSummary = GetEvidenceSummary();
        var transcript = BuildTranscriptSummary();

        TranscriptOutput = "[STAGE] Closing Arguments\n\nThe court is now in session for closing arguments.\n";

        // Plaintiff / Prosecution closing argument
        var plaintiffName = CurrentCase.PlaintiffAttorney;
        if (string.IsNullOrWhiteSpace(plaintiffName)) plaintiffName = "Prosecutor";

        await GenerateNamedResponse(ProsecutionTeam, plaintiffName,
            $"You are delivering the closing argument for the plaintiff/prosecution in this case. " +
            $"Be as PERSUASIVE as possible — this is your last chance to convince the jury. " +
            $"Write approximately 500 words. Summarize the evidence and explain why it proves " +
            $"your case beyond any doubt. Frame every exhibit in the light most favorable to " +
            $"your side. Address the jury directly and call for a verdict in your favor. " +
            $"Reference specific exhibits by number " +
            $"(e.g., \"Exhibit 1 clearly proves...\", \"Exhibit 3 is undeniable...\") " +
            $"to anchor your argument in the evidence.\n\nCase evidence:\n{evidenceSummary}", transcript);

        // Defense closing argument
        var defenseName = CurrentCase.DefenseAttorney;
        if (string.IsNullOrWhiteSpace(defenseName)) defenseName = "Defense Attorney";

        await GenerateNamedResponse(DefenseTeam, defenseName,
            $"You are delivering the closing argument for the defense. " +
            $"Be as PERSUASIVE as possible — this is your last chance to convince the jury. " +
            $"Write approximately 500 words. Summarize the evidence and explain why it raises " +
            $"reasonable doubt (or fails to meet the burden of proof). Reframe every exhibit " +
            $"in the light most favorable to your client. Address the jury directly and call " +
            $"for a verdict in your favor. Reference specific exhibits by number " +
            $"(e.g., \"Exhibit 2 actually shows...\", \"The defense proved through Exhibit 4...\") " +
            $"to anchor your argument in the evidence.\n\nCase evidence:\n{evidenceSummary}", transcript);

        BroadcastEvent("Closing arguments concluded. The case will now go to the jury.",
            Enum.GetValues<AgentRole>().ToList());
    }

    /// <summary>
    /// Called when the Witness Testimony court phase is selected.
    /// Populates the Witness Box with a witness derived from case evidence
    /// and generates their direct testimony with specific exhibit references.
    /// </summary>
    /// <summary>
    /// Processes a witness statement through all jurors — each juror forms a bias-colored
    /// memory of the testimony, and their verdict lean may shift based on the evidence cited.
    /// The juror's own bias interacts with the witness's apparent side to produce
    /// confirmation bias (agreeing witnesses get more credence, opposing witnesses get skepticism).
    /// </summary>
    private async Task ProcessTestimonyForJurors(string testimony, string witnessName,
        double witnessBias = 0.0, string witnessRole = "fact witness")
    {
        var defaultModel = DefaultModel;

        // Determine which side this witness leans toward for the juror prompt
        string witnessSide = witnessBias > 0.1 ? "plaintiff/prosecution" :
                              witnessBias < -0.1 ? "defense/defendant" : "neutral";

        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            AIModelConfiguration? jurorModel = CurrentCase.AvailableModels
                .FirstOrDefault(m => m.FriendlyName == juror.SelectedModel) ?? defaultModel;

            // Build a record that includes the witness's side so the juror can filter through bias
            string recordForJuror = $"{witnessName} ({witnessRole}, a {witnessSide} witness) testified: {testimony}";

            // Juror internalizes the testimony through their own bias
            string reaction = jurorModel != null
                ? await _agentInteraction.InternalizeCourtRecordAsync(juror, recordForJuror, jurorModel)
                : testimony;

            // --- Bias-Congruence Memory Modulation ---
            // agreement > 0  → juror and witness lean the same direction → confirmation bias (boost)
            // agreement < 0  → juror and witness lean opposite → skepticism (dampen)
            double agreement = juror.Bias * witnessBias;
            double biasInfluence;

            if (agreement > 0)
            {
                // Aligned: juror finds witness credible — memory forms more strongly
                biasInfluence = juror.Bias * 0.15 + agreement * 0.25;
                // Verdict lean shifts more readily (confirmation bias)
                juror.UpdateVerdictLean(agreement * 0.08);
            }
            else if (agreement < 0)
            {
                // Opposed: juror is skeptical — memory forms more weakly
                biasInfluence = juror.Bias * 0.15 - Math.Abs(agreement) * 0.15;
                // Minor resistance shift — juror may push back slightly
                juror.UpdateVerdictLean(-Math.Abs(agreement) * 0.03);
            }
            else
            {
                // Neutral witness — standard treatment
                biasInfluence = juror.Bias * 0.10;
                juror.UpdateVerdictLean(witnessBias * 0.02);
            }

            // Clamp and record
            biasInfluence = Math.Clamp(biasInfluence, -0.5, 1.0);
            juror.RecordTrialEvent(reaction, biasInfluence);

            // Reinforce any existing memories of exhibits mentioned in this testimony
            juror.ReinforceRelatedMemories(testimony);
        }

        // Apply evidence influence from any exhibits referenced in the testimony
        // (exhibit references will reinforce stored evidence)
        foreach (var doc in CurrentCase.Evidence)
        {
            string refStr = $"exhibit {doc.ExhibitNumber}".ToLowerInvariant();
            if (testimony.Contains(refStr, StringComparison.OrdinalIgnoreCase))
            {
                _juryCalc.ApplyEvidenceInfluence(
                    AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)),
                    doc);
            }
        }

        NotifyJuryUpdate();
    }

    /// <summary>
    /// Generates an LLM response for a named attorney, finding them in the appropriate counsel table.
    /// Returns the response text, or empty string on failure.
    /// </summary>
    private async Task<string> GenerateAttorneyQuestion(ObservableCollection<Agent> counselTeam,
        string speakerName, string context, List<AgentMessage> transcript)
    {
        var agent = counselTeam.FirstOrDefault(a => a.IsOccupied);
        if (agent == null) return "";

        AIModelConfiguration? modelConfig = null;
        if (!string.IsNullOrEmpty(agent.SelectedModel))
            modelConfig = FindModel(agent.SelectedModel);
        modelConfig ??= DefaultModel;
        if (modelConfig == null) return "";

        var request = new StatementRequest
        {
            Role = agent.Role,
            SpeakerName = speakerName,
            Context = context,
            Type = MessageType.Statement,
            CaseData = CurrentCase,
            Transcript = transcript,
            Model = modelConfig
        };

        var result = await _agentInteraction.GenerateStatementAsync(request);
        if (result.Success && result.Message != null)
            return result.Message.Content;

        return "";
    }

    /// <summary>
    /// Determines whether an exhibit suggests a live witness should testify about it,
    /// based on keywords in the filename and summary. Document-only exhibits
    /// (contracts, photos, general records) are just read into the record.
    /// </summary>
    private static bool ExhibitSuggestsWitness(EvidenceDocument doc)
    {
        var combined = (doc.FileName + " " + doc.Summary).ToLowerInvariant();
        // These suggest a person with direct knowledge should testify
        return combined.Contains("witness") || combined.Contains("testimony") ||
               combined.Contains("interview") || combined.Contains("statement") ||
               combined.Contains("report by") || combined.Contains("authored by") ||
               combined.Contains("observation") || combined.Contains("examination") ||
               combined.Contains("finding") || combined.Contains("analysis by") ||
               combined.Contains("assessment") || combined.Contains("evaluation") ||
               combined.Contains("testified") || combined.Contains("deposition") ||
               combined.Contains("affidavit") || combined.Contains("declaration") ||
               combined.Contains("medical") || combined.Contains("diagnosis") ||
               combined.Contains("police") || combined.Contains("arrest") ||
               combined.Contains("investigat") || combined.Contains("forensic");
    }

    /// <summary>
    /// Infers a witness archetype from the exhibits they will testify about.
    /// </summary>
    private static string InferWitnessRole(IEnumerable<EvidenceDocument> exhibits)
    {
        var combined = string.Join(" ", exhibits.Select(e => (e.FileName + " " + e.Summary).ToLowerInvariant()));

        if (combined.Contains("medical") || combined.Contains("doctor") || combined.Contains("diagnosis") ||
            combined.Contains("injury") || combined.Contains("hospital") || combined.Contains("treatment"))
            return "medical expert";
        if (combined.Contains("police") || combined.Contains("officer") || combined.Contains("arrest") ||
            combined.Contains("crime scene") || combined.Contains("investigat"))
            return "law enforcement officer";
        if (combined.Contains("financial") || combined.Contains("account") || combined.Contains("audit") ||
            combined.Contains("tax") || combined.Contains("valuation") || combined.Contains("damages"))
            return "financial analyst";
        if (combined.Contains("forensic") || combined.Contains("dna") || combined.Contains("fingerprint") ||
            combined.Contains("ballistic") || combined.Contains("lab"))
            return "forensic expert";
        if (combined.Contains("photo") || combined.Contains("video") || combined.Contains("recording") ||
            combined.Contains("surveillance"))
            return "eyewitness";
        if (combined.Contains("expert") || combined.Contains("specialist") || combined.Contains("consult"))
            return "expert witness";
        if (combined.Contains("witness") || combined.Contains("bystander") || combined.Contains("saw"))
            return "eyewitness";
        if (combined.Contains("plaintiff") || combined.Contains("victim"))
            return "plaintiff/victim";
        if (combined.Contains("defendant") || combined.Contains("accused"))
            return "character witness";

        return "fact witness";
    }

    /// <summary>
    /// Runs a complete witness cycle: direct testimony → attorney Q&A → cross examination → dismissal.
    /// Jurors update memories at every step.
    /// </summary>
    private async Task RunWitnessCycle(string witnessName, string witnessRole,
        string directTestimony, List<EvidenceDocument> assignedExhibits)
    {
        var transcript = BuildTranscriptSummary();
        var evidenceSummary = GetEvidenceSummary();
        var model = DefaultModel;
        var witnessSlot = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Witness);

        if (witnessSlot == null) return;

        // Clear previous occupant
        if (witnessSlot.IsOccupied)
        {
            witnessSlot.IsOccupied = false;
            witnessSlot.Name = "Witness Box";
            witnessSlot.Profile = "";
        }

        // Populate the box
        witnessSlot.IsOccupied = true;
        witnessSlot.Name = witnessName;
        witnessSlot.Profile = witnessRole;
        witnessSlot.AgentId = Guid.NewGuid();
        if (model != null)
            witnessSlot.SelectedModel = model.FriendlyName;
        witnessSlot.Bias = witnessRole.Contains("plaintiff") || witnessRole.Contains("prosecut") ? 0.3 :
                           witnessRole.Contains("defense") || witnessRole.Contains("character") ? -0.3 : 0.0;

        TranscriptOutput += $"\n\n─── Witness: {witnessName} ({witnessRole}) ───";
        TranscriptOutput += $"\nThe clerk calls {witnessName} to the stand.";
        TranscriptOutput += $"\n{witnessName}: {directTestimony}";
        BroadcastEvent($"{witnessName} gave direct testimony",
            new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });

        // Jurors form memories from direct testimony (bias-congruence active)
        await ProcessTestimonyForJurors(directTestimony, witnessName, witnessSlot.Bias, witnessRole);

        // Determine which side called this witness
        bool plaintiffCalled = witnessSlot.Bias >= 0;
        var plaintiffName = string.IsNullOrWhiteSpace(CurrentCase.PlaintiffAttorney) ? "Prosecutor" : CurrentCase.PlaintiffAttorney;
        var defenseName = string.IsNullOrWhiteSpace(CurrentCase.DefenseAttorney) ? "Defense Attorney" : CurrentCase.DefenseAttorney;
        var callingTeam = plaintiffCalled ? ProsecutionTeam : DefenseTeam;
        var callingAttorney = plaintiffCalled ? plaintiffName : defenseName;

        // Calling attorney asks about assigned exhibits
        foreach (var exhibit in assignedExhibits.Take(3))
        {
            string topic = $"Exhibit {exhibit.ExhibitNumber} ({exhibit.FileName}): {exhibit.Summary}";
            string question = await GenerateAttorneyQuestion(callingTeam, callingAttorney,
                $"You are {callingAttorney}, questioning your witness {witnessName} on direct examination. " +
                $"Ask a specific question about {topic}. Be pointed and direct.\n\n" +
                $"Witness role: {witnessRole}\n\nExhibits:\n{evidenceSummary}", transcript);

            if (string.IsNullOrEmpty(question)) continue;

            TranscriptOutput += $"\n{callingAttorney}: {question}";
            BroadcastEvent($"{callingAttorney}: {question}",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });

            // Witness answers with spin
            string answer = "";
            if (model != null)
            {
                var req = new StatementRequest
                {
                    Role = AgentRole.Witness, SpeakerName = witnessName,
                    Context = $"You are {witnessName} ({witnessRole}), questioned by {callingAttorney}. " +
                              $"They ask: \"{question}\"\n\nRULES:\n1. Truthful, based on evidence.\n" +
                              $"2. SPIN evidence to help {callingAttorney}'s case.\n" +
                              $"3. Reference exhibits by number.\n4. Be detailed.\n\nEvidence:\n{evidenceSummary}",
                    Type = MessageType.Statement, CaseData = CurrentCase,
                    Transcript = transcript, Model = model
                };
                var r = await _agentInteraction.GenerateStatementAsync(req);
                if (r.Success && r.Message != null) answer = r.Message.Content;
            }
            if (string.IsNullOrEmpty(answer)) answer = "The evidence speaks for itself.";
            TranscriptOutput += $"\n{witnessName}: {answer}";
            BroadcastEvent($"{witnessName}: {TruncateContent(answer, 200)}",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
            await ProcessTestimonyForJurors(answer, witnessName, witnessSlot.Bias, witnessRole);
        }

        // Cross examination by opposing side
        var opposingAttorney = plaintiffCalled ? defenseName : plaintiffName;
        var opposingTeam = plaintiffCalled ? DefenseTeam : ProsecutionTeam;
        TranscriptOutput += $"\n--- Cross examination by {opposingAttorney} ---";

        string[] crossQuestions =
        [
            $"{opposingAttorney}: {witnessName}, you testified about certain exhibits. Let me ask you — isn't it true there are aspects of the evidence you didn't mention that contradict your version?",
            $"{opposingAttorney}: One more thing — given the full record, isn't it possible your recollection is incomplete?"
        ];

        foreach (var crossQ in crossQuestions)
        {
            TranscriptOutput += $"\n{crossQ}";
            BroadcastEvent(crossQ,
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });

            string crossAnswer = "";
            if (model != null)
            {
                string questionText = crossQ.Replace($"{opposingAttorney}: ", "");
                var req = new StatementRequest
                {
                    Role = AgentRole.Witness, SpeakerName = witnessName,
                    Context = $"You are {witnessName} ({witnessRole}), cross-examined by {opposingAttorney}. " +
                              $"They ask: \"{questionText}\"\n\nRULES:\n1. Truthful, based ONLY on evidence.\n" +
                              $"2. You may use biased language favoring YOUR side, but do not fabricate.\n" +
                              $"3. If you don't know, say so.\n4. Reference exhibits.\n\nEvidence:\n{evidenceSummary}",
                    Type = MessageType.Statement, CaseData = CurrentCase,
                    Transcript = transcript, Model = model
                };
                var r = await _agentInteraction.GenerateStatementAsync(req);
                if (r.Success && r.Message != null) crossAnswer = r.Message.Content;
            }
            if (string.IsNullOrEmpty(crossAnswer)) crossAnswer = "I stand by my earlier testimony.";
            TranscriptOutput += $"\n{witnessName}: {crossAnswer}";
            BroadcastEvent($"{witnessName}: {TruncateContent(crossAnswer, 200)}",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
            await ProcessTestimonyForJurors(crossAnswer, witnessName, witnessSlot.Bias, witnessRole);
        }

        TranscriptOutput += $"\n{opposingAttorney}: No further questions, Your Honor.";
        TranscriptOutput += $"\n[Witness {witnessName} is dismissed.]";

        // Clear witness from box
        witnessSlot.IsOccupied = false;
        witnessSlot.Name = "Witness Box";
        witnessSlot.Profile = "";
    }

    /// <summary>
    /// Fully automated witness testimony phase. Analyzes evidence, determines which exhibits
    /// suggest a live witness, generates appropriate witnesses, and runs each through
    /// direct testimony → attorney Q&A → cross examination → dismissal.
    /// Document-only exhibits are simply noted as read into the record.
    /// </summary>
    public async Task EnterWitnessTestimony()
    {
        CurrentDebateStage = CourtPhase.WitnessTestimony;
        CurrentCase.CurrentDebateStage = CourtPhase.WitnessTestimony;

        var evidenceSummary = GetEvidenceSummary();
        var transcript = BuildTranscriptSummary();
        var model = DefaultModel;

        TranscriptOutput = "[STAGE] Witness Testimony\n\n";

        if (CurrentCase.Evidence.Count == 0)
        {
            TranscriptOutput += "No evidence has been entered. Add exhibits during Discovery first.\n";
            return;
        }

        // Classify exhibits: those that suggest a witness vs document-only
        var witnessExhibits = CurrentCase.Evidence.Where(ExhibitSuggestsWitness).ToList();
        var documentExhibits = CurrentCase.Evidence.Where(e => !ExhibitSuggestsWitness(e)).ToList();

        // Note document-only exhibits
        if (documentExhibits.Any())
        {
            TranscriptOutput += "Documentary exhibits read into the record:\n";
            foreach (var doc in documentExhibits)
                TranscriptOutput += $"  • Exhibit {doc.ExhibitNumber}: {doc.FileName} — {doc.Summary}\n";
            BroadcastEvent($"{documentExhibits.Count} documentary exhibits entered into the record.",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
            TranscriptOutput += "\n";
        }

        if (witnessExhibits.Count == 0)
        {
            TranscriptOutput += "No evidence requires a live witness. The documentary record is complete.\n";
            await SimpleJudgeResponse("All evidence has been entered into the record. We will proceed.");
            return;
        }

        // Group witness exhibits into witness archetypes
        var witnessGroups = new List<List<EvidenceDocument>>();
        var remaining = new List<EvidenceDocument>(witnessExhibits);

        // Generate up to 3 witnesses, each handling related exhibits
        int maxWitnesses = Math.Min(3, remaining.Count);
        for (int w = 0; w < maxWitnesses; w++)
        {
            var group = new List<EvidenceDocument> { remaining[0] };
            remaining.RemoveAt(0);

            // Try to add related exhibits to this witness
            var related = remaining.Where(e =>
                (e.FileName + " " + e.Summary).ToLowerInvariant().Contains(
                    (group[0].FileName + " " + group[0].Summary).ToLowerInvariant().Split(' ', '.').FirstOrDefault(s => s.Length > 4) ?? "") ||
                remaining.Count <= 1).Take(2).ToList();
            foreach (var r in related) { group.Add(r); remaining.Remove(r); }

            witnessGroups.Add(group);
        }

        // Generate and run each witness
        await SimpleJudgeResponse("We will now hear witness testimony. The prosecution/plaintiff may call their first witness.");

        for (int i = 0; i < witnessGroups.Count; i++)
        {
            var exhibits = witnessGroups[i];
            var assignedSummary = string.Join("\n", exhibits.Select(e =>
                $"[Exhibit {e.ExhibitNumber}] {e.FileName}: {e.Summary}"));
            string witnessRole = InferWitnessRole(exhibits);

            // Generate witness via LLM
            string witnessName = $"Witness {i + 1}";
            string witnessTestimony = "";

            if (model != null)
            {
                var req = new StatementRequest
                {
                    Role = AgentRole.Witness,
                    SpeakerName = "Witness",
                    Context = $"You are being called as a {witnessRole} in this case. State your full name " +
                              $"and your professional/personal role. Then deliver your direct testimony " +
                              $"about what you know, based on these exhibits. Be specific and detailed. " +
                              $"You MUST reference exhibit numbers.\n\nYour assigned exhibits:\n{assignedSummary}\n\n" +
                              $"Full case evidence:\n{evidenceSummary}",
                    Type = MessageType.Statement, CaseData = CurrentCase,
                    Transcript = transcript, Model = model
                };
                var result = await _agentInteraction.GenerateStatementAsync(req);
                if (result.Success && result.Message != null)
                {
                    witnessName = result.Message.Speaker ?? $"Witness {i + 1}";
                    witnessTestimony = result.Message.Content;
                }
            }

            if (string.IsNullOrEmpty(witnessTestimony))
                witnessTestimony = $"I am a {witnessRole} and I reviewed the relevant exhibits.";

            await RunWitnessCycle(witnessName, witnessRole, witnessTestimony, exhibits);
        }

        // All done
        TranscriptOutput += $"\n\n=== ALL WITNESSES HAVE TESTIFIED ===";
        await SimpleJudgeResponse("All witnesses have been heard. The evidence is complete. We will proceed to closing arguments.");
    }

    /// <summary>
    /// Called when the Cross Examination court phase is selected.
    /// The opposing attorney automatically questions the witness for several rounds,
    /// then the judge dismisses the witness.
    /// </summary>
    public async Task EnterCrossExamination()
    {
        CurrentDebateStage = CourtPhase.CrossExamination;
        CurrentCase.CurrentDebateStage = CourtPhase.CrossExamination;

        var transcript = BuildTranscriptSummary();
        var evidenceSummary = GetEvidenceSummary();

        TranscriptOutput = "[STAGE] Cross Examination\n\n";

        // Find the witness on the stand
        var witness = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Witness && a.IsOccupied);
        if (witness == null)
        {
            TranscriptOutput += "No witness is on the stand to be cross-examined.\n";
            await SimpleJudgeResponse("There is no witness to cross-examine. We will move to the next phase.");
            return;
        }

        // Determine which side cross-examines based on the witness's profile/bias
        bool witnessLeansPlaintiff = witness.Bias > 0 || string.IsNullOrEmpty(witness.Profile);
        string attorneyName;
        ObservableCollection<Agent> attorneyTeam;

        if (witnessLeansPlaintiff)
        {
            attorneyName = string.IsNullOrWhiteSpace(CurrentCase.DefenseAttorney) ? "Defense Attorney" : CurrentCase.DefenseAttorney;
            attorneyTeam = DefenseTeam;
        }
        else
        {
            attorneyName = string.IsNullOrWhiteSpace(CurrentCase.PlaintiffAttorney) ? "Prosecutor" : CurrentCase.PlaintiffAttorney;
            attorneyTeam = ProsecutionTeam;
        }

        await SimpleJudgeResponse($"We will now proceed to cross examination. {attorneyName}, you may question the witness.");

        // Run 3 rounds of cross-examination: attorney asks, witness answers
        string[] crossQuestions =
        [
            $"{attorneyName}: {witness.Name}, you testified earlier about this case. I'd like to ask you a few questions. Let's start — what specific evidence did you personally review before forming your conclusions?",
            $"{attorneyName}: I see. Now, isn't it true that there are details in the evidence that contradict your version of events? Let's review what the records actually show — can you explain that discrepancy?",
            $"{attorneyName}: One final question. Given everything in the evidence, isn't it possible that things happened differently than you described?"
        ];

        for (int round = 0; round < crossQuestions.Length; round++)
        {
            TranscriptOutput += $"\n{crossQuestions[round]}";
            BroadcastEvent(crossQuestions[round],
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });

            // Witness responds
            string witnessQuestion = $"Question from {attorneyName}: \"{crossQuestions[round].Replace($"{attorneyName}: ", "")}\"";

            string biasDescription = witness.Bias > 0.3
                ? "you tend to favor the prosecution/plaintiff side"
                : witness.Bias < -0.3
                    ? "you tend to favor the defense side"
                    : "you are neutral";

            var model = DefaultModel;
            if (model != null)
            {
                var request = new StatementRequest
                {
                    Role = AgentRole.Witness,
                    SpeakerName = witness.Name,
                    Context = $"You are {witness.Name}, a witness being cross-examined by {attorneyName}. " +
                              $"Your role: {witness.Profile}\n\n" +
                              $"The attorney asks: {witnessQuestion}\n\n" +
                              $"RULES:\n" +
                              $"1. Answer truthfully based ONLY on what the evidence actually shows.\n" +
                              $"2. If you don't know or the evidence doesn't address it, say so.\n" +
                              $"3. You may use biased language — {biasDescription} — in how you describe the evidence.\n" +
                              $"4. Reference specific exhibits by number.\n\n" +
                              $"Evidence in this case:\n{evidenceSummary}",
                    Type = MessageType.Statement,
                    CaseData = CurrentCase,
                    Transcript = transcript,
                    Model = model
                };

                var result = await _agentInteraction.GenerateStatementAsync(request);
                if (result.Success && result.Message != null)
                {
                    TranscriptOutput += $"\n{witness.Name}: {result.Message.Content}";
                    BroadcastEvent($"{witness.Name}: {result.Message.Content}",
                        new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
                }
            }
        }

        // Attorney concludes
        TranscriptOutput += $"\n{attorneyName}: I have no further questions, Your Honor.";
        BroadcastEvent($"{attorneyName}: I have no further questions.",
            new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });

        // Judge dismisses the witness
        await SimpleJudgeResponse($"Thank you, {witness.Name}. You may step down. The witness is dismissed.");
    }

    /// <summary>
    /// Generates an LLM response for a specific named attorney from a counsel table collection.
    /// </summary>
    private async Task GenerateNamedResponse(ObservableCollection<Agent> counselTeam, string speakerName,
        string context, List<AgentMessage> transcript)
    {
        var agent = counselTeam.FirstOrDefault(a => a.IsOccupied);
        if (agent == null)
        {
            TranscriptOutput += $"\n[No {speakerName} available to speak]";
            return;
        }

        AIModelConfiguration? modelConfig = null;
        if (!string.IsNullOrEmpty(agent.SelectedModel))
            modelConfig = FindModel(agent.SelectedModel);
        modelConfig ??= DefaultModel;

        if (modelConfig == null)
        {
            TranscriptOutput += $"\n[No model configured for {speakerName}]";
            return;
        }

        var request = new StatementRequest
        {
            Role = agent.Role,
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
    /// Generates a simple one-line response from the judge (used for stage transitions).
    /// </summary>
    public async Task SimpleJudgeResponse(string statement)
    {
        var transcript = BuildTranscriptSummary();
        await GenerateAgentResponse(AgentRole.Judge, "Judge", statement, transcript);
    }

    /// <summary>
    /// Generates an LLM response from the witness on the stand during Cross Examination.
    /// The witness must be truthful and stick to what the evidence shows,
    /// but may use biased language based on their background and role.
    /// </summary>
    private async Task GenerateWitnessResponse(string speaker, string content, List<AgentMessage> transcript)
    {
        var witness = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Witness && a.IsOccupied);
        if (witness == null)
        {
            TranscriptOutput += "\n[No witness on the stand to answer questions.]";
            return;
        }

        AIModelConfiguration? modelConfig = null;
        if (!string.IsNullOrEmpty(witness.SelectedModel))
            modelConfig = FindModel(witness.SelectedModel);
        modelConfig ??= DefaultModel;

        if (modelConfig == null)
        {
            TranscriptOutput += "\n[No model configured for witness response.]";
            return;
        }

        string biasDescription = DescribeBias(witness);

        var request = new StatementRequest
        {
            Role = AgentRole.Witness,
            SpeakerName = witness.Name,
            Context = $"You are {witness.Name}, a witness testifying under cross examination. " +
                      $"Your role in this case: {witness.Profile}\n\n" +
                      $"You are being questioned by {speaker}. Their question: \"{content}\"\n\n" +
                      $"You MUST follow these rules:\n" +
                      $"1. ONLY answer based on what the evidence actually shows — do not fabricate.\n" +
                      $"2. Be truthful — if you don't know or the evidence doesn't address it, say so.\n" +
                      $"3. You may use biased language in HOW you describe the evidence " +
                      $"(e.g., {biasDescription}).\n" +
                      $"4. Reference specific exhibits by number where applicable.\n\n" +
                      $"Evidence in the case:\n{GetEvidenceSummary()}",
            Type = MessageType.Statement,
            CaseData = CurrentCase,
            Transcript = transcript,
            Model = modelConfig
        };

        var result = await _agentInteraction.GenerateStatementAsync(request);
        if (result.Success && result.Message != null)
        {
            TranscriptOutput += $"\n{witness.Name}: {result.Message.Content}";
            BroadcastEvent($"{witness.Name}: {result.Message.Content}",
                new List<AgentRole> { AgentRole.Judge, AgentRole.Juror, AgentRole.AlternateJuror, AgentRole.Lawyer, AgentRole.Reporter });
        }
    }

    /// <summary>
    /// Generates a natural-language description of how this witness's bias
    /// might color their language when describing evidence.
    /// </summary>
    private static string DescribeBias(Agent agent)
    {
        double bias = agent.Bias;
        if (bias > 0.3)
            return "you tend to favor the prosecution/plaintiff side and frame evidence in a way that supports their case";
        if (bias < -0.3)
            return "you tend to favor the defense side and frame evidence in a way that supports their position";
        if (bias > 0.1)
            return "you lean slightly toward the prosecution/plaintiff in how you describe things";
        if (bias < -0.1)
            return "you lean slightly toward the defense in how you describe things";
        return "you are neutral and describe evidence objectively";
    }

    /// <summary>
    /// Generates a single juror based on the case's jurisdiction demographics.
    /// Parses county and state from JurisdictionSpecifics and generates appropriate juror.
    /// Also replays the case's trial history (event log + exhibits) so the new juror
    /// has the same memory stimuli as existing jurors.
    /// </summary>
    public Agent GenerateSingleJuror(string county, string state = "South Carolina")
    {
        if (string.IsNullOrWhiteSpace(county))
            county = "Richland County";
        if (string.IsNullOrWhiteSpace(state))
            state = "South Carolina";

        var juror = _juryService.GenerateJuror(county, state);
        juror.Role = AgentRole.Juror;

        // Replay the trial history so this juror has the same stimuli as existing jurors
        ProcessTrialHistoryForJuror(juror);

        return juror;
    }

    /// <summary>
    /// Replays the current case's event log and exhibits through a juror in trial order,
    /// creating the same TrialEvents and evidence memories that existing jurors have.
    /// This ensures all jurors share identical stimuli regardless of when they were generated.
    /// </summary>
    private void ProcessTrialHistoryForJuror(Agent juror)
    {
        // Set the default juror model from the per-role mapping, if configured.
        var roleModel = GetDefaultModelForRole(juror.Role, juror.CommunicationTeam, CurrentCase);
        if (!string.IsNullOrWhiteSpace(roleModel))
            juror.SelectedModel = roleModel;

        // 1. Replay event log entries in chronological order (transcript lines, rulings, etc.)
        foreach (var evt in CurrentCase.EventLog)
        {
            if (string.IsNullOrWhiteSpace(evt.Description)) continue;
            juror.RecordTrialEvent(evt.Description, juror.Bias * 0.1);
            juror.AnalyzeSentiment(evt.Description);
        }

        // 2. Process each exhibit to form evidence memories with exhibit references
        foreach (var doc in CurrentCase.Evidence)
        {
            // Same format as AutoDeliberate auto-processing so searches find these
            string memoryContent = $"[Exhibit {doc.ExhibitNumber}] {doc.FileName}: " +
                (!string.IsNullOrWhiteSpace(doc.DetailedAnalysis)
                    ? doc.DetailedAnalysis
                    : doc.Summary);

            juror.RecordTrialEvent(memoryContent, juror.Bias * 0.1);
            juror.AnalyzeSentiment($"Exhibit {doc.ExhibitNumber}, {doc.FileName}: {doc.Summary}");

            // Apply evidence influence to update initial VerdictLean
            _juryCalc.ApplyEvidenceInfluence(new[] { juror }, doc);
        }
    }

    /// <summary>
    /// Returns the FriendlyName of the per-role default model from the case,
    /// or null if no role-specific default is configured.
    /// For lawyers, distinguishes prosecution vs defense via CommunicationTeam.
    /// </summary>
    public static string? GetDefaultModelForRole(AgentRole role, string communicationTeam, CaseFile c)
    {
        return role switch
        {
            AgentRole.Judge => string.IsNullOrEmpty(c.DefaultJudgeModel) ? null : c.DefaultJudgeModel,
            AgentRole.Lawyer when string.Equals(communicationTeam, "Plaintiff", StringComparison.OrdinalIgnoreCase)
                => string.IsNullOrEmpty(c.DefaultProsecutionModel) ? null : c.DefaultProsecutionModel,
            AgentRole.Lawyer when string.Equals(communicationTeam, "Defendant", StringComparison.OrdinalIgnoreCase)
                => string.IsNullOrEmpty(c.DefaultDefenseModel) ? null : c.DefaultDefenseModel,
            AgentRole.Lawyer => string.IsNullOrEmpty(c.DefaultDefenseModel) ? null : c.DefaultDefenseModel,
            AgentRole.Witness => string.IsNullOrEmpty(c.DefaultWitnessModel) ? null : c.DefaultWitnessModel,
            AgentRole.Reporter => string.IsNullOrEmpty(c.DefaultReporterModel) ? null : c.DefaultReporterModel,
            AgentRole.Client => string.IsNullOrEmpty(c.DefaultClientModel) ? null : c.DefaultClientModel,
            AgentRole.Juror or AgentRole.AlternateJuror
                => string.IsNullOrEmpty(c.DefaultJurorModel) ? null : c.DefaultJurorModel,
            _ => null
        };
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
        string mediaType = extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "Image",
            ".mp4" or ".mov" or ".avi" or ".wmv" or ".mkv" or ".webm" => "Video",
            ".mp3" or ".wav" or ".ogg" or ".wma" or ".m4a" or ".flac" => "Audio",
            _ => "Document"
        };

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
            // Still determine category and assess probative value for LLM-analyzed evidence
            doc.EvidenceCategory = EvidenceAnalysisService.DetermineEvidenceCategory(doc.MediaType, summary, fileContent);
            doc.IsTestimonial = doc.EvidenceCategory == "Testimony";
            EvidenceAnalysisService.AssessProbativeValue(doc, summary, fileContent);
        }
        else
        {
            // Assess strength using both the user summary and the actual file content
            // This also sets EvidenceCategory, ProbativeValue, and WitnessCredibility
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
            ? (FindModel(reporter.SelectedModel)
               ?? DefaultModel)
            : DefaultModel;

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
        var defaultModel = DefaultModel;
        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            AIModelConfiguration? jurorModel = FindModel(juror.SelectedModel)
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
    private readonly List<string> _deliberationHistory = new();
    private string _earlyThoughtLeaderName = string.Empty;

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

    /// <summary>
    /// Generates a comprehensive mock jury questionnaire report showing
    /// evidence polling, witness impressions, juror profiles, and case polling data.
    /// </summary>
    /// <summary>
    /// Pre-trial juror poll — captures each juror's baseline lean, sentiment,
    /// background demographics, and what evidence they consider important
    /// before hearing any arguments or testimony.
    /// </summary>
    public void GeneratePreTrialQuestionnaire()
    {
        var sb = new System.Text.StringBuilder();
        var jurors = AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();

        double avgLean = jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0.5;
        double avgSentiment = jurors.Any() ? jurors.Average(j => j.Sentiment) : 0.5;

        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        string plaintiffLabel = isCriminal ? "Prosecution" : "Plaintiff";
        string defendantLabel = "Defense";

        sb.AppendLine($"\n{'═',60}");
        sb.AppendLine("  PRE-TRIAL JUROR QUESTIONNAIRE — BASELINE POLL");
        sb.AppendLine($"  {DateTime.Now:MMMM dd, yyyy HH:mm}");
        sb.AppendLine($"{'═',60}\n");

        sb.AppendLine($"  CASE: {CurrentCase.CaseName}");
        sb.AppendLine($"  MODE: {CurrentCase.Mode}  |  JURISDICTION: {CurrentCase.Jurisdiction}");
        sb.AppendLine();

        // ── Baseline Overview ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  BASELINE JURY OVERVIEW");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine($"  Total jurors polled: {jurors.Count}");
        sb.AppendLine($"  Average Lean: {avgLean:P1} (0% = {defendantLabel}, 100% = {plaintiffLabel})");
        sb.AppendLine($"  Average Sentiment: {avgSentiment:P1}");
        sb.AppendLine($"  Default lean direction: {(avgLean > 0.5 ? plaintiffLabel : defendantLabel)}-leaning jury");
        sb.AppendLine();

        // ── Individual Juror Baseline Profiles ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  INDIVIDUAL JUROR BASELINES");
        sb.AppendLine($"  {'─',50}");

        foreach (var juror in jurors.OrderByDescending(j => j.VerdictLean))
        {
            string initialPosition = BurdenOfProof.GetPositionLabel(juror.VerdictLean, isCriminal);

            sb.AppendLine($"  {juror.Name} — Initial Lean: {juror.VerdictLean:P1} → {initialPosition}");
            sb.AppendLine($"    Age: {juror.Age}  |  Gender: {juror.Gender}  |  Race: {juror.Race}");
            sb.AppendLine($"    Education: {juror.EducationLevel}  |  Income: {juror.IncomeLevel}");
            sb.AppendLine($"    Occupation: {juror.Occupation}");
            sb.AppendLine($"    Political: {juror.PoliticalAffiliation}  |  Religion: {juror.ReligiousAffiliation}");
            sb.AppendLine($"    Marital: {juror.MaritalStatus}  |  Parental: {juror.ParentalStatus}");
            sb.AppendLine($"    Media: {juror.MediaConsumption}");
            sb.AppendLine($"    Bias: {juror.Bias:F2}  |  Sentiment: {juror.Sentiment:P1}");

            // Suggest what type of evidence this juror is likely to find compelling
            // based on their demographics and bias
            var evidenceTendency = DescribeEvidenceTendency(juror);
            sb.AppendLine($"    Likely to find compelling: {evidenceTendency}");
            sb.AppendLine();
        }

        // ── Pre-Trial Assessment ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  PRE-TRIAL ASSESSMENT");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine($"  This jury enters with a {(avgLean > 0.55 ? plaintiffLabel : avgLean < 0.45 ? defendantLabel : "neutral")} lean.");
        sb.AppendLine($"  Before hearing any evidence, {jurors.Count(j => Math.Abs(j.VerdictLean - 0.5) > 0.05)} of {jurors.Count} jurors already have a leaning.");
        sb.AppendLine($"  Key demographics to watch: education level, political affiliation, and media consumption.");
        sb.AppendLine();

        TranscriptOutput += sb.ToString();
    }

    /// <summary>
    /// Describes what type of evidence a juror is likely to find compelling
    /// based on their demographics and bias profile.
    /// </summary>
    private static string DescribeEvidenceTendency(Agent juror)
    {
        var tendencies = new List<string>();

        if (juror.Bias > 0.3)
            tendencies.Add("plaintiff/prosecution evidence");
        else if (juror.Bias < -0.3)
            tendencies.Add("defense evidence");

        if (juror.EducationLevel?.Contains("College") == true || juror.EducationLevel?.Contains("Graduate") == true)
            tendencies.Add("expert testimony and documentary evidence");
        else if (juror.EducationLevel?.Contains("High School") == true)
            tendencies.Add("eyewitness testimony and physical evidence");

        if (juror.IncomeLevel?.Contains("Upper") == true)
            tendencies.Add("financial and economic impact evidence");
        else if (juror.IncomeLevel?.Contains("Lower") == true)
            tendencies.Add("evidence of personal harm and hardship");

        if (juror.MediaConsumption?.Contains("Conservative") == true)
            tendencies.Add("law enforcement and forensic evidence");
        else if (juror.MediaConsumption?.Contains("Progressive") == true)
            tendencies.Add("scientific and expert analysis");

        if (juror.Occupation?.Contains("Medical") == true || juror.Occupation?.Contains("Doctor") == true)
            tendencies.Add("medical records and expert medical testimony");
        else if (juror.Occupation?.Contains("Legal") == true || juror.Occupation?.Contains("Attorney") == true)
            tendencies.Add("procedural and documentary evidence");

        return tendencies.Count > 0 ? string.Join("; ", tendencies) : "general factual testimony";
    }

    /// <summary>
    /// Generates formal jury instructions based on the case type, charges/causes of action,
    /// and applicable legal standards. Uses real-world instruction patterns.
    /// </summary>
    public void GenerateJuryInstructions()
    {
        var sb = new System.Text.StringBuilder();
        var verdict = CurrentCase.Verdict;
        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        string court = CurrentCase.CourtName ?? "Superior Court";
        string presiding = "The Court";

        sb.AppendLine($"\n{'═',60}");
        sb.AppendLine($"  {court.ToUpperInvariant()}");
        sb.AppendLine($"  JURY INSTRUCTIONS");
        sb.AppendLine($"  Case No. {CurrentCase.CaseNumber}");
        sb.AppendLine($"{'═',60}\n");

        sb.AppendLine("  MEMBERS OF THE JURY:");
        sb.AppendLine();
        sb.AppendLine("  You have heard all of the evidence and the arguments of the attorneys. It is now");
        sb.AppendLine("  my duty to instruct you on the law that applies to this case.");
        sb.AppendLine();

        // ── Burden of Proof ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  I. BURDEN OF PROOF");
        sb.AppendLine($"  {'─',50}");

        if (isCriminal)
        {
            sb.AppendLine();
            sb.AppendLine("  The State has the burden of proving every element of each charge beyond a");
            sb.AppendLine("  reasonable doubt. This burden never shifts to the defendant. The defendant");
            sb.AppendLine("  is presumed to be innocent throughout the trial. This presumption of");
            sb.AppendLine("  innocence is alone sufficient to acquit the defendant, unless you are");
            sb.AppendLine("  satisfied beyond a reasonable doubt of the defendant's guilt based on the");
            sb.AppendLine("  evidence.");
            sb.AppendLine();
            sb.AppendLine("  A reasonable doubt is a doubt based upon reason and common sense. It is a");
            sb.AppendLine("  doubt for which a reason can be given, arising from a fair and rational");
            sb.AppendLine("  consideration of the evidence or lack of evidence. It is not a merely");
            sb.AppendLine("  possible doubt, because everything relating to human affairs is open to");
            sb.AppendLine("  some possible or imaginary doubt.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("  The plaintiff has the burden of proving each element of each claim by a");
            sb.AppendLine("  preponderance of the evidence. This means the plaintiff must convince you");
            sb.AppendLine("  that a fact is more likely true than not. If the evidence is evenly");
            sb.AppendLine("  balanced, or if you find that the defendant's explanation is equally");
            sb.AppendLine("  likely, then the plaintiff has not met this burden and you must find for");
            sb.AppendLine("  the defendant on that claim.");
            sb.AppendLine();
            sb.AppendLine("  The burden of proof on any affirmative defense raised by the defendant");
            sb.AppendLine("  is also by a preponderance of the evidence.");
        }

        // ── Elements to Prove ──
        sb.AppendLine();
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine(isCriminal ? "  II. CHARGES AND ELEMENTS" : "  II. CLAIMS AND ELEMENTS");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine();

        if (isCriminal && verdict.Charges.Count > 0)
        {
            for (int i = 0; i < verdict.Charges.Count; i++)
            {
                var charge = verdict.Charges[i];
                sb.AppendLine($"  CHARGE {i + 1}: {charge.Name}");
                sb.AppendLine();
                sb.AppendLine("  To find the defendant guilty of this charge, the State must prove");
                sb.AppendLine("  each of the following elements beyond a reasonable doubt:");
                sb.AppendLine();
                for (int e = 0; e < charge.Elements.Count; e++)
                {
                    var element = charge.Elements[e];
                    sb.AppendLine($"    {e + 1}. {element.Name}:");
                    sb.AppendLine($"       {element.Description}");
                    sb.AppendLine();
                }
                if (i < verdict.Charges.Count - 1)
                    sb.AppendLine("  ---");
                sb.AppendLine();
            }
        }
        else if (!isCriminal && verdict.CausesOfAction.Count > 0)
        {
            for (int i = 0; i < verdict.CausesOfAction.Count; i++)
            {
                var coa = verdict.CausesOfAction[i];
                sb.AppendLine($"  CLAIM {i + 1}: {coa.Name}");
                sb.AppendLine();
                sb.AppendLine("  To find the defendant liable on this claim, the plaintiff must prove");
                sb.AppendLine("  each of the following elements by a preponderance of the evidence:");
                sb.AppendLine();
                for (int e = 0; e < coa.Elements.Count; e++)
                {
                    var element = coa.Elements[e];
                    sb.AppendLine($"    {e + 1}. {element.Name}:");
                    sb.AppendLine($"       {element.Description}");
                    sb.AppendLine();
                }
                if (i < verdict.CausesOfAction.Count - 1)
                    sb.AppendLine("  ---");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine(isCriminal
                ? "  The defendant is charged with criminal offenses. You must consider"
                : "  The plaintiff has brought civil claims against the defendant. You must consider");
            sb.AppendLine("  the evidence presented and apply the law as instructed by the Court.");
            sb.AppendLine("  Your verdict must be based solely on the evidence and these instructions.");
            sb.AppendLine();
        }

        // ── Evidence Evaluation ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  III. EVALUATING EVIDENCE");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine();
        sb.AppendLine("  You are the sole judges of the credibility of the witnesses and the weight");
        sb.AppendLine("  to be given to the evidence. In determining credibility, you may consider:");
        sb.AppendLine();
        sb.AppendLine("    • The witness's opportunity and ability to observe");
        sb.AppendLine("    • The witness's memory and manner of testifying");
        sb.AppendLine("    • Any interest, bias, or prejudice the witness may have");
        sb.AppendLine("    • The reasonableness of the testimony in light of all evidence");
        sb.AppendLine("    • Whether the testimony is corroborated by other evidence");
        sb.AppendLine();
        sb.AppendLine("  You should consider all of the evidence admitted in this case. Exhibits");
        sb.AppendLine("  admitted into evidence may be examined by you during deliberation. You are");
        sb.AppendLine("  not to consider any evidence that was not admitted or that was stricken.");
        sb.AppendLine("  The arguments and statements of counsel are not evidence.");
        sb.AppendLine();

        // ── Deliberation Instructions ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  IV. DELIBERATION AND VERDICT");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine();
        sb.AppendLine("  Your verdict must be based solely on the evidence presented in this trial");
        sb.AppendLine("  and these instructions of law. You must not be influenced by sympathy,");
        sb.AppendLine("  passion, or prejudice.");
        sb.AppendLine();
        if (isCriminal)
        {
            sb.AppendLine("  Your verdict on each charge must be unanimous. You must deliberate");
            sb.AppendLine("  together, discussing the evidence with one another in a spirit of");
            sb.AppendLine("  fairness and cooperation. Each of you must decide the case for");
            sb.AppendLine("  yourself, but only after considering the evidence with your fellow");
            sb.AppendLine("  jurors. Do not surrender your honest belief as to the weight or");
            sb.AppendLine("  effect of the evidence solely because of the opinion of other jurors.");
            sb.AppendLine();
            sb.AppendLine("  For each charge, you must return one of the following verdicts:");
            sb.AppendLine("    • GUILTY — if the State has proven every element beyond a reasonable doubt");
            sb.AppendLine("    • NOT GUILTY — if the State has failed to prove any element beyond");
            sb.AppendLine("      a reasonable doubt");
        }
        else
        {
            sb.AppendLine("  Your verdict must be unanimous if there are no more than 12 jurors.");
            sb.AppendLine("  You must deliberate together in a spirit of fairness and cooperation.");
            sb.AppendLine("  Each of you must decide for yourself, but only after considering the");
            sb.AppendLine("  evidence with your fellow jurors.");
            sb.AppendLine();
            sb.AppendLine("  For each claim, you must return one of the following verdicts:");
            sb.AppendLine("    • LIABLE — if the plaintiff has proven every element by a");
            sb.AppendLine("      preponderance of the evidence");
            sb.AppendLine("    • NOT LIABLE — if the plaintiff has failed to prove any element");
            sb.AppendLine("      by a preponderance of the evidence");
        }
        sb.AppendLine();

        if (!isCriminal)
        {
            sb.AppendLine($"  {'─',50}");
            sb.AppendLine("  V. DAMAGES");
            sb.AppendLine($"  {'─',50}");
            sb.AppendLine();
            sb.AppendLine("  If you find the defendant liable, you must then determine the amount");
            sb.AppendLine("  of damages, if any, to award the plaintiff. Damages are intended to");
            sb.AppendLine("  compensate the plaintiff for the harm caused by the defendant's conduct.");
            sb.AppendLine("  Your award must be based on the evidence and not on speculation.");
            sb.AppendLine();
        }

        sb.AppendLine($"  {'─',50}");
        sb.AppendLine(isCriminal ? "  V. CLOSING INSTRUCTION" : "  VI. CLOSING INSTRUCTION");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine();
        sb.AppendLine("  These instructions contain all the law that applies to this case. You");
        sb.AppendLine("  must not infer that anything else is the law. If any part of these");
        sb.AppendLine("  instructions seems inconsistent, accept the explanation given by the Court");
        sb.AppendLine("  as the correct statement of the law.");
        sb.AppendLine();
        sb.AppendLine("  You may now retire to the jury room to begin your deliberations.");
        sb.AppendLine($"\n  Dated: {DateTime.Now:MMMM dd, yyyy}");
        sb.AppendLine($"\n  {'─',50}");
        sb.AppendLine($"  {presiding}");
        sb.AppendLine();

        // Save to case file
        CurrentCase.Instructions = new Verdict.Models.JuryInstructions
        {
            Text = sb.ToString(),
            Standard = isCriminal ? LegalStandard.BeyondReasonableDoubt : LegalStandard.PreponderanceOfEvidence
        };

        TranscriptOutput += sb.ToString();
    }

    /// <summary>
    /// Generates a comprehensive mock jury questionnaire report showing
    /// evidence polling, witness impressions, juror profiles, and case polling data.
    /// </summary>
    public void GenerateMockJuryQuestionnaire()
    {
        var sb = new System.Text.StringBuilder();
        var jurors = AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        var witnesses = AllAgents.Where(a => a.IsOccupied && a.Role == AgentRole.Witness).ToList();

        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        string plaintiffLabel = isCriminal ? "Prosecution" : "Plaintiff";
        string defendantLabel = "Defense";

        int proCount = BurdenOfProof.CountProsecution(jurors, isCriminal);
        int defCount = BurdenOfProof.CountDefense(jurors, isCriminal);
        int undCount = jurors.Count - proCount - defCount;
        double avgLean = jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0.5;
        double avgSentiment = jurors.Any() ? jurors.Average(j => j.Sentiment) : 0.5;

        sb.AppendLine($"\n{'═',60}");
        sb.AppendLine("  MOCK JURY QUESTIONNAIRE — CLOSING REPORT");
        sb.AppendLine($"  {DateTime.Now:MMMM dd, yyyy HH:mm}");
        sb.AppendLine($"{'═',60}\n");

        sb.AppendLine($"  CASE: {CurrentCase.CaseName}");
        sb.AppendLine($"  MODE: {CurrentCase.Mode}  |  JURISDICTION: {CurrentCase.Jurisdiction}");
        sb.AppendLine($"  COURT: {CurrentCase.CourtName}");
        sb.AppendLine();

        // ── Jury Overview ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  JURY OVERVIEW");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine($"  Total jurors polled: {jurors.Count}");
        sb.AppendLine($"  Lean {plaintiffLabel}: {proCount}  |  Lean {defendantLabel}: {defCount}  |  Undecided: {undCount}");
        sb.AppendLine($"  Average Lean: {avgLean:P1} (0% = {defendantLabel}, 100% = {plaintiffLabel})");
        sb.AppendLine($"  Average Sentiment: {avgSentiment:P1}");
        double convictionThreshold = BurdenOfProof.GetConvictionThreshold(isCriminal);
        sb.AppendLine($"  Burden of proof threshold: {convictionThreshold:P1} {(isCriminal ? "(beyond reasonable doubt)" : "(preponderance of evidence)")}");
        sb.AppendLine($"  Predicted Outcome: {(proCount > defCount ? $"Likely for {plaintiffLabel}" : defCount > proCount ? $"Likely for {defendantLabel}" : "Too close to call / Hung jury possible")}");
        sb.AppendLine();

        // ── Evidence Polling ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  EVIDENCE POLLING");
        sb.AppendLine($"  {'─',50}");

        if (CurrentCase.Evidence.Any())
        {
            foreach (var exhibit in CurrentCase.Evidence)
            {
                string exhibitRef = $"exhibit {exhibit.ExhibitNumber}".ToLowerInvariant();
                
                // Find juror memories that mention this exhibit — search by both
                // "exhibit X" reference AND by file name (case-insensitive)
                var relevantMemories = jurors
                    .SelectMany(j => j.TrialEvents
                        .Where(m => m.Content.Contains(exhibitRef, StringComparison.OrdinalIgnoreCase)
                                 || m.Content.Contains(exhibit.FileName, StringComparison.OrdinalIgnoreCase))
                        .Select(m => (Juror: j, Memory: m)))
                    .ToList();

                double avgMemStr = relevantMemories.Any() ? relevantMemories.Average(x => x.Memory.Strength) : 0;
                var mostImpressed = relevantMemories.OrderByDescending(x => x.Memory.Strength).Take(3).Select(x => x.Juror.Name).ToList();
                var leastImpressed = relevantMemories.OrderBy(x => x.Memory.Strength).Take(2).Select(x => x.Juror.Name).ToList();

                sb.AppendLine($"  Exhibit {exhibit.ExhibitNumber}: {exhibit.FileName} (Strength: {exhibit.EvidenceStrength:P0})");
                sb.AppendLine($"    Case summary: {exhibit.Summary}");
                if (relevantMemories.Any())
                {
                    sb.AppendLine($"    Avg juror memory strength: {avgMemStr:P1}");
                    sb.AppendLine($"    Most persuaded by this exhibit: {(mostImpressed.Any() ? string.Join(", ", mostImpressed) : "N/A")}");
                    sb.AppendLine($"    Least persuaded by this exhibit: {(leastImpressed.Any() ? string.Join(", ", leastImpressed) : "N/A")}");
                }
                else
                {
                    // Evidence was loaded from case file but not yet processed through jurors via Discovery/Pretrial.
                    // The exhibit summary was available to jurors during deliberation.
                    sb.AppendLine($"    (Evidence reviewed via case summary — run Discovery phase for per-juror memory tracking)");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No evidence was entered in this case.\n");
        }

        // ── Witness Impressions ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  WITNESS IMPRESSIONS");
        sb.AppendLine($"  {'─',50}");

        if (witnesses.Any())
        {
            foreach (var witness in witnesses)
            {
                var witnessMemories = jurors
                    .SelectMany(j => j.TrialEvents
                        .Where(m => m.Content.Contains(witness.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(m => (Juror: j, Memory: m)))
                    .ToList();

                sb.AppendLine($"  Witness: {witness.Name} ({witness.Profile})");
                sb.AppendLine($"    Jurors who referenced this witness: {witnessMemories.Count}");
                if (witnessMemories.Any())
                {
                    double avgWitStr = witnessMemories.Average(x => x.Memory.Strength);
                    sb.AppendLine($"    Avg memory strength: {avgWitStr:P1}");
                    var mostFavorable = witnessMemories.OrderByDescending(x => x.Memory.Strength).First();
                    sb.AppendLine($"    Most favorable impression from: {mostFavorable.Juror.Name}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No witnesses testified in this case.\n");
        }

        // ── Individual Juror Profiles ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  INDIVIDUAL JUROR PROFILES");
        sb.AppendLine($"  {'─',50}");

        foreach (var juror in jurors.OrderByDescending(j => j.VerdictLean))
        {
            string position = BurdenOfProof.GetPositionLabel(juror.VerdictLean, isCriminal);
            var topMemories = juror.TrialEvents
                .OrderByDescending(m => m.Strength)
                .Take(3)
                .Select(m => $"{TruncateContent(m.Content, 60)} (str: {m.Strength:P0})")
                .ToList();

            sb.AppendLine($"  {juror.Name} — Lean: {juror.VerdictLean:P1} {plaintiffLabel} → {position}");
            sb.AppendLine($"    Demographics: {juror.Age}yo {juror.Gender}, {juror.Race}");
            sb.AppendLine($"    Education: {juror.EducationLevel}  |  Income: {juror.IncomeLevel}");
            sb.AppendLine($"    Political: {juror.PoliticalAffiliation}  |  Bias: {juror.Bias:F2}");
            sb.AppendLine($"    Sentiment: {juror.Sentiment:P1}  |  Memories: {juror.TrialEvents.Count}");
            if (!isCriminal && juror.ConsideredDamages > 0)
                sb.AppendLine($"    Considered Damages: ${juror.ConsideredDamages:N0}");
            if (topMemories.Any())
            {
                sb.AppendLine($"    Top impressions:");
                foreach (var mem in topMemories)
                    sb.AppendLine($"      • {mem}");
            }
            sb.AppendLine();
        }

        // ── Key Findings Summary ──
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine("  KEY FINDINGS");
        sb.AppendLine($"  {'─',50}");
        sb.AppendLine($"  The jury leans {((avgLean > 0.55 ? proCount : defCount).ToString())}-{(avgLean > 0.55 ? defCount : proCount).ToString()} toward {(avgLean > 0.5 ? plaintiffLabel : defendantLabel)}.");
        
        // Find the exhibit with the highest impact: prefer average memory strength,
        // but fall back to evidence strength when no per-juror memories exist yet.
        var exhibitPolling = CurrentCase.Evidence
            .Select(e =>
            {
                string refStr = $"exhibit {e.ExhibitNumber}".ToLowerInvariant();
                var mems = jurors.SelectMany(j => j.TrialEvents
                    .Where(m => m.Content.Contains(refStr, StringComparison.OrdinalIgnoreCase)
                             || m.Content.Contains(e.FileName, StringComparison.OrdinalIgnoreCase)));
                double avgMem = mems.Any() ? mems.Average(m => m.Strength) : 0;
                // Blend: 70% memory strength + 30% evidence strength, so exhibits
                // with 0 memories still show meaningful impact scores.
                double blended = avgMem > 0 ? avgMem : e.EvidenceStrength;
                return (Exhibit: e, AvgStr: blended, MemCount: mems.Count());
            })
            .OrderByDescending(x => x.AvgStr)
            .ToList();

        if (exhibitPolling.Any())
        {
            var mostImpactful = exhibitPolling.First();
            string strengthLabel = mostImpactful.MemCount > 0
                ? $"avg memory strength {mostImpactful.AvgStr:P1}"
                : $"evidence strength {mostImpactful.AvgStr:P0} (no per-juror memory data — run Discovery for detailed tracking)";
            sb.AppendLine($"  Most impactful exhibit: {mostImpactful.Exhibit.FileName} ({strengthLabel})");
        }

        sb.AppendLine($"  Overall juror sentiment: {avgSentiment:P1}");

        // Damages summary for civil cases
        if (!isCriminal)
        {
            var withDamages = jurors.Where(j => j.ConsideredDamages > 0).ToList();
            if (withDamages.Any())
            {
                double medD = withDamages.OrderBy(j => j.ConsideredDamages)
                    .ElementAt(withDamages.Count / 2).ConsideredDamages;
                sb.AppendLine($"  Jury Damages Consensus: median ${medD:N0} (range ${withDamages.Min(j => j.ConsideredDamages):N0} – ${withDamages.Max(j => j.ConsideredDamages):N0})");
                sb.AppendLine($"  Evidence Estimated Damages: ${CurrentCase.Evidence.Sum(e => e.EstimatedDamages):N0}");
                if (CurrentCase.Verdict.DamagesAwarded > 0)
                    sb.AppendLine($"  Final Damages Awarded: ${CurrentCase.Verdict.DamagesAwarded:N0}");
            }
            sb.AppendLine($"  Settlement Value: ${CurrentCase.EstimatedSettlement:N0} | Insurance Reserve: ${CurrentCase.InsuranceReserve:N0}");
        }
        sb.AppendLine();

        TranscriptOutput += sb.ToString();
    }

    /// <summary>
    /// Truncates a string to the given max length with ellipsis.
    /// </summary>
    private static string TruncateContent(string content, int maxLen)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLen)
            return content;
        return content[..(maxLen - 3)] + "...";
    }

    private void ConcludeDeliberation(List<Agent> jurors, bool hungPossible)
    {
        var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        if (!voters.Any())
        {
            TranscriptOutput = "[JURY REPORT] No jurors seated.";
            return;
        }

        double maxLean = voters.Max(v => v.VerdictLean);
        double minLean = voters.Min(v => v.VerdictLean);
        double spread = maxLean - minLean;

        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        int proCount = BurdenOfProof.CountProsecution(voters, isCriminal);
        int defCount = BurdenOfProof.CountDefense(voters, isCriminal);
        int undCount = voters.Count - proCount - defCount;

        bool reachesConsensus = spread <= 0.10;
        bool isUnanimousSide = (proCount == voters.Count || defCount == voters.Count);

        // A unanimous side vote (all jurors on one side) IS a verdict,
        // even if the lean spread is wide. Spread only matters for close splits.
        bool canReturnVerdict = reachesConsensus || isUnanimousSide;

        string verdictLine;
        if (canReturnVerdict)
        {
            if (proCount > defCount)
                verdictLine = isCriminal
                    ? "VERDICT: GUILTY \u2014 The jury finds the defendant guilty of the charges."
                    : "VERDICT: LIABLE \u2014 The jury finds for the plaintiff.";
            else
                verdictLine = isCriminal
                    ? "VERDICT: NOT GUILTY \u2014 The jury acquits the defendant. Self-defense was not disproven beyond a reasonable doubt."
                    : "VERDICT: NOT LIABLE \u2014 The jury finds for the defense.";
        }
        else if (hungPossible)
        {
            verdictLine = isCriminal
                ? $"HUNG JURY \u2014 Unable to reach unanimous verdict. Vote: {proCount} Guilty / {defCount} Not Guilty" + (undCount > 0 ? $" / {undCount} Undecided" : "")
                : $"HUNG JURY \u2014 Unable to reach unanimous verdict. Vote: {proCount} Liable / {defCount} Not Liable";
        }
        else
        {
            string lean = proCount > defCount ? (isCriminal ? "Guilty" : "Liable") : (isCriminal ? "Not Guilty" : "Not Liable");
            verdictLine = $"MAJORITY LEANS: {lean} ({Math.Max(proCount, defCount)} of {voters.Count} jurors)";
        }

        var early = string.IsNullOrEmpty(_earlyThoughtLeaderName) ? "(not identified)" : _earlyThoughtLeaderName;
        string proLabel = BurdenOfProof.ProsecutionLabel(isCriminal);
        string defLabel = BurdenOfProof.DefenseLabel();

        // ── Compute damages award for civil cases where the plaintiff won ──
        string damagesLine = "";
        if (!isCriminal && canReturnVerdict && proCount > defCount)
        {
            // Collect considered damages from jurors who voted liable
            var liableJurors = voters
                .Where(v => BurdenOfProof.GetPositionLabel(v.VerdictLean, false) == "LIABLE")
                .ToList();

            if (liableJurors.Any() && liableJurors.Any(j => j.ConsideredDamages > 0))
            {
                // Use median for robustness against outliers (real juries converge on middle ground)
                var damagesValues = liableJurors
                    .Where(j => j.ConsideredDamages > 0)
                    .Select(j => j.ConsideredDamages)
                    .OrderBy(d => d)
                    .ToList();

                double medianDamages;
                int mid = damagesValues.Count / 2;
                if (damagesValues.Count % 2 == 0)
                    medianDamages = (damagesValues[mid - 1] + damagesValues[mid]) / 2.0;
                else
                    medianDamages = damagesValues[mid];

                // Round to nearest hundred for a realistic award
                medianDamages = Math.Round(medianDamages / 100.0) * 100.0;

                CurrentCase.Verdict.DamagesAwarded = (decimal)medianDamages;

                // Update exposure numbers to reflect the jury's actual award
                CurrentCase.EstimatedSettlement = medianDamages * 0.85;
                CurrentCase.InsuranceReserve = medianDamages * 1.15;

                // Format for display
                string damagesDisplay = medianDamages >= 1_000_000
                    ? $"${medianDamages / 1_000_000:F2} million"
                    : $"${medianDamages:N0}";

                damagesLine = $"\nDAMAGES AWARDED: {damagesDisplay} " +
                    $"(jury median; range: ${damagesValues.Min():N0} – ${damagesValues.Max():N0})\n" +
                    $"  Updated Settlement Value: ${CurrentCase.EstimatedSettlement:N0} | " +
                    $"Insurance Reserve: ${CurrentCase.InsuranceReserve:N0}";
            }
            else
            {
                // No juror formed a damages opinion — fall back to evidence estimates
                double evidenceTotal = CurrentCase.Evidence.Sum(e => e.EstimatedDamages);
                if (evidenceTotal > 0)
                {
                    CurrentCase.Verdict.DamagesAwarded = (decimal)evidenceTotal;
                    damagesLine = $"\nDAMAGES AWARDED: ${evidenceTotal:N0} (based on evidence estimates; jury did not reach a damages consensus)";
                }
                else
                {
                    damagesLine = "\nDAMAGES AWARDED: Nominal ($1) — no evidence of monetary damages was presented.";
                    CurrentCase.Verdict.DamagesAwarded = 1m;
                }
            }
        }

        string summary = $"Jury Deliberation Complete\n" +
                        $"Rounds: {_deliberationRound}/{CurrentCase.DeliberationRounds} | " +
                        $"Early Thought Leader: {early}\n" +
                        $"Final Vote: {proCount} {proLabel} / {defCount} {defLabel}" + (undCount > 0 ? $" / {undCount} Undecided" : "") + $"\n" +
                        $"Lean Spread: {minLean:F2} \u2013 {maxLean:F2} (spread: {spread:F2})\n\n" +
                        $"{verdictLine}{damagesLine}\n\n" +
                        $"--- Individual Juror Positions ---";

        TranscriptOutput = $"{TranscriptOutput}\n\n{new string('=', 50)}\n{summary}";

        // Show each juror's final position with their considered damages
        foreach (var j in voters.OrderByDescending(v => v.VerdictLean))
        {
            string position = BurdenOfProof.GetPositionLabel(j.VerdictLean, isCriminal);
            string damagesInfo = !isCriminal && j.ConsideredDamages > 0
                ? $" | Damages: ${j.ConsideredDamages:N0}"
                : "";
            TranscriptOutput += $"\n  {j.Name}: Lean={j.VerdictLean:F2} Bias={j.Bias:F2} \u2192 {position}{damagesInfo}";
        }

        TranscriptOutput += $"\n{new string('=', 50)}";
        BroadcastEvent(verdictLine, Enum.GetValues<AgentRole>().ToList(), isSidebar: false);
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
        _deliberationHistory.Clear();
        _earlyThoughtLeaderName = string.Empty;
        _deliberationInitialized = true;

        TranscriptOutput = "[JURY REPORT] Deliberation begins. Generating juror turns...";
        OnPropertyChanged(nameof(TranscriptOutput));

        // lock stage for UI
        _currentDebateStage = CourtPhase.JuryDeliberation;
    }


    /// <summary>
    /// Runs the ENTIRE trial automatically — Discovery → Pretrial → Trial phases →
    /// Deliberation — then displays the report in a text window with export.
    /// </summary>
    public async Task AutoSimulateTrial()
    {
        TranscriptOutput = "[AUTOPILOT] Starting full trial simulation...\n";
        if (CurrentCase.Evidence.Count == 0)
        {
            TranscriptOutput += "[AUTOPILOT] No evidence to process. Add exhibits first.\n";
            return;
        }

        var jurors = AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        if (jurors.Count == 0)
        {
            TranscriptOutput += "[AUTOPILOT] No jurors seated. Generate a jury first.\n";
            return;
        }

        // ═══ PHASE 1: DISCOVERY — process evidence through jurors ═══
        TranscriptOutput += "\n─── DISCOVERY PHASE ───\n";
        foreach (var doc in CurrentCase.Evidence)
        {
            var reporterSummary = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}: {doc.Summary}";
            foreach (var juror in jurors)
            {
                string memoryContent = $"[Exhibit {doc.ExhibitNumber}] {doc.FileName}: " +
                    (!string.IsNullOrWhiteSpace(doc.DetailedAnalysis) ? doc.DetailedAnalysis : doc.Summary);
                juror.RecordTrialEvent(memoryContent, juror.Bias * 0.1);
                juror.AnalyzeSentiment(reporterSummary);
            }
        }
        TranscriptOutput += $"Processed {CurrentCase.Evidence.Count} exhibits through {jurors.Count} jurors.\n";

        // ═══ PHASE 2: PRETRIAL — apply evidence influence + record initial opinions ═══
        TranscriptOutput += "\n─── PRETRIAL PHASE ───\n";
        foreach (var doc in CurrentCase.Evidence)
            _juryCalc.ApplyEvidenceInfluence(jurors, doc);

        foreach (var juror in jurors)
        {
            juror.RecordOpinionSnapshot("Pretrial", "Initial evidence assessment");
        }
        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        TranscriptOutput += "Initial verdict leans (evidence only):\n";
        foreach (var juror in jurors.OrderByDescending(j => j.VerdictLean))
            TranscriptOutput += $"  {juror.Name}: {juror.VerdictLean:P0} → {BurdenOfProof.GetPositionLabel(juror.VerdictLean, isCriminal)}\n";

        // ═══ PHASE 3: OPENING STATEMENTS ═══
        TranscriptOutput += "\n─── OPENING STATEMENTS ───\n";
        CurrentCase.TrialPhase = TrialPhase.Trial;
        CurrentCase.CurrentDebateStage = CourtPhase.OpeningStatements;
        await Task.Delay(200);

        // Prosecution/Plaintiff opening
        if (!string.IsNullOrWhiteSpace(CurrentCase.ProsecutionPlaintiffPrompt))
        {
            TranscriptOutput += $"\n{CurrentCase.PlaintiffAttorney ?? "Prosecution"} (Opening):\n";
            var proOpening = await GeneratePhaseStatement(
                CurrentCase.ProsecutionPlaintiffPrompt, "Opening Statement", "prosecution");
            TranscriptOutput += $"\"{TruncateContent(proOpening, 500)}\"\n";
        }

        // Defense opening
        if (!string.IsNullOrWhiteSpace(CurrentCase.DefensePrompt))
        {
            TranscriptOutput += $"\n{CurrentCase.DefenseAttorney ?? "Defense"} (Opening):\n";
            var defOpening = await GeneratePhaseStatement(
                CurrentCase.DefensePrompt, "Opening Statement", "defense");
            TranscriptOutput += $"\"{TruncateContent(defOpening, 500)}\"\n";
        }

        // Record post-opening opinion snapshots
        foreach (var juror in jurors)
            juror.RecordOpinionSnapshot("Opening Statements", "Both sides presented");

        // ═══ PHASE 4: WITNESS TESTIMONY + CROSS ═══
        TranscriptOutput += "\n─── WITNESS TESTIMONY ───\n";
        CurrentCase.CurrentDebateStage = CourtPhase.WitnessTestimony;
        await Task.Delay(100);

        // For each exhibit, have a "witness" discuss it
        int exhibitCount = Math.Min(CurrentCase.Evidence.Count, 5);
        for (int i = 0; i < exhibitCount; i++)
        {
            var doc = CurrentCase.Evidence[i];
            TranscriptOutput += $"\nWitness discusses Exhibit {doc.ExhibitNumber} ({doc.FileName}):\n";
            
            string witnessPrompt = $"You are a witness discussing {doc.FileName}. " +
                $"Summary: {doc.Summary}. Speak naturally about what you know, 2-3 sentences.";
            var testimony = await GeneratePhaseStatement(witnessPrompt, "Witness Testimony", "neutral");
            TranscriptOutput += $"\"{TruncateContent(testimony, 400)}\"\n";
            BroadcastEvent($"Witness testimony about Exhibit {doc.ExhibitNumber}: {testimony}",
                new List<AgentRole> { AgentRole.Juror, AgentRole.AlternateJuror });
        }

        // Cross-examination
        TranscriptOutput += "\n─── CROSS EXAMINATION ───\n";
        CurrentCase.CurrentDebateStage = CourtPhase.CrossExamination;
        await Task.Delay(100);
        TranscriptOutput += "(Cross-examination of key evidence presented.)\n";

        // Record post-witness opinion snapshots
        foreach (var juror in jurors)
            juror.RecordOpinionSnapshot("Witnesses/Cross", "All testimony heard");

        // ═══ PHASE 5: CLOSING ARGUMENTS ═══
        TranscriptOutput += "\n─── CLOSING ARGUMENTS ───\n";
        CurrentCase.CurrentDebateStage = CourtPhase.ClosingArguments;
        await Task.Delay(100);

        if (!string.IsNullOrWhiteSpace(CurrentCase.ProsecutionPlaintiffPrompt))
        {
            var proClose = await GeneratePhaseStatement(
                CurrentCase.ProsecutionPlaintiffPrompt, "Closing Argument", "prosecution");
            TranscriptOutput += $"\n{CurrentCase.PlaintiffAttorney ?? "Prosecution"} (Closing):\n\"{TruncateContent(proClose, 500)}\"\n";
        }
        if (!string.IsNullOrWhiteSpace(CurrentCase.DefensePrompt))
        {
            var defClose = await GeneratePhaseStatement(
                CurrentCase.DefensePrompt, "Closing Argument", "defense");
            TranscriptOutput += $"\n{CurrentCase.DefenseAttorney ?? "Defense"} (Closing):\n\"{TruncateContent(defClose, 500)}\"\n";
        }

        // Record post-closing opinion snapshots
        foreach (var juror in jurors)
            juror.RecordOpinionSnapshot("Closing Arguments", "Final arguments heard");

        // Record post-trial opinion snapshots
        foreach (var juror in jurors)
            juror.RecordOpinionSnapshot("Post-Trial", "All evidence and arguments heard");

        // ═══ PHASE 6: DELIBERATION ═══
        await AutoDeliberate();

        // ═══ PHASE 7: REPORT ═══
        var report = GenerateTrialReport();
        var reportWindow = new Views.TrialReportWindow(report, CurrentCase.CaseName)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        reportWindow.ShowDialog();
    }

    /// <summary>
    /// Generates an LLM response for a trial phase (opening, closing, testimony).
    /// </summary>
    private async Task<string> GeneratePhaseStatement(string prompt, string phase, string role)
    {
        if (CurrentCase.AvailableModels.Count == 0)
        {
            CurrentCase.AddDefaultModels();
            if (CurrentCase.AvailableModels.Count == 0)
                return $"[No model available for {phase}]";
        }

        var model = CurrentCase.AvailableModels.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.ApiKey))
                 ?? CurrentCase.AvailableModels.LastOrDefault()
                 ?? CurrentCase.AvailableModels.FirstOrDefault();

        if (model == null) return $"[No model for {phase}]";

        try
        {
            var provider = ProviderDiscoveryService.GetProviderByName(model.Provider);
            if (provider == null) return $"[Provider not found: {model.Provider}]";

            string systemPrompt = role switch
            {
                "prosecution" => "You are a prosecutor delivering arguments in court. Be persuasive and logical.",
                "defense" => "You are a defense attorney delivering arguments in court. Be persuasive and logical.",
                _ => "You are a witness in court. Be truthful and speak naturally."
            };

            var response = await provider.GenerateResponseAsync(model, systemPrompt,
                $"{phase}: {prompt}\n\nDeliver your {phase.ToLowerInvariant()}. Keep it to 3-5 sentences, professional tone.");
            return response ?? $"[{phase} generated]";
        }
        catch
        {
            return $"[{phase} - LLM call failed]";
        }
    }

    /// <summary>
    /// Generates the full trial report with opinion graphs as ASCII charts.
    /// </summary>
    private string GenerateTrialReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"  VERDICT TRIAL REPORT");
        sb.AppendLine($"  {CurrentCase.CaseName}");
        sb.AppendLine($"  Court: {CurrentCase.CourtName}");
        sb.AppendLine($"  Mode: {CurrentCase.Mode}  |  Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('═', 60));

        // ── Jury Overview ──
        var jurors = AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
        int proCount = BurdenOfProof.CountProsecution(jurors, isCriminal);
        int defCount = BurdenOfProof.CountDefense(jurors, isCriminal);
        double avgLean = jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0;
        string proLabel = isCriminal ? "Guilty" : "Liable";
        string defLabel = isCriminal ? "Not Guilty" : "Not Liable";

        sb.AppendLine($"\nJURY OVERVIEW");
        sb.AppendLine($"  Total Jurors: {jurors.Count}");
        sb.AppendLine($"  Vote: {proCount} {proLabel} / {defCount} {defLabel}");
        sb.AppendLine($"  Average Lean: {avgLean:P0}");
        sb.AppendLine($"  Lean Spread: {jurors.Max(j => j.VerdictLean) - jurors.Min(j => j.VerdictLean):P0}");

        // ── Evidence ──
        sb.AppendLine($"\nEVIDENCE ({CurrentCase.Evidence.Count} exhibits)");
        foreach (var doc in CurrentCase.Evidence)
        {
            sb.AppendLine($"  Exhibit {doc.ExhibitNumber}: {doc.FileName}");
            sb.AppendLine($"    Strength: {doc.EvidenceStrength:P0}  |  Type: {doc.MediaType}");
            sb.AppendLine($"    Summary: {TruncateContent(doc.Summary, 200)}");
        }

        // ── Individual Juror Reports with Opinion Graphs ──
        sb.AppendLine($"\n{'─',60}");
        sb.AppendLine("INDIVIDUAL JUROR REPORTS (with opinion-over-time graphs)");
        sb.AppendLine(new string('─', 60));

        foreach (var juror in jurors.OrderByDescending(j => j.VerdictLean))
        {
            string position = BurdenOfProof.GetPositionLabel(juror.VerdictLean, isCriminal);
            sb.AppendLine($"\n  {juror.Name}  |  Final Lean: {juror.VerdictLean:P0} → {position}");
            sb.AppendLine($"  {juror.Age}yo {juror.Gender}, {juror.Race}  |  {juror.Occupation}");
            sb.AppendLine($"  Education: {juror.EducationLevel}  |  Income: {juror.IncomeLevel}");
            sb.AppendLine($"  Political: {juror.PoliticalAffiliation}  |  Bias: {juror.Bias:F2}");

            // Opinion graph as ASCII chart
            if (juror.OpinionHistory.Count > 0)
            {
                sb.AppendLine($"\n  OPINION HISTORY ({juror.OpinionHistory.Count} snapshots):");
                sb.AppendLine(string.Format("  {0,-18} {1,-8} {2,-10} Cause", "Phase", "Lean", "Change"));
                sb.AppendLine($"  {new string('-', 18)} {new string('-', 8)} {new string('-', 10)} {new string('-', 30)}");

                double prevLean = -1;
                foreach (var snap in juror.OpinionHistory)
                {
                    string change = prevLean >= 0 ? $"{(snap.VerdictLean - prevLean):+#0.00;-#0.00}" : "  ---";
                    sb.AppendLine(string.Format("  {0,-18} {1,6:P0}  {2,8}  {3}", 
                        snap.Phase, snap.VerdictLean, change, TruncateContent(snap.Cause, 45)));
                    prevLean = snap.VerdictLean;
                }

                // ASCII sparkline
                sb.AppendLine($"\n  Lean trend: ");
                DrawAsciiSparkline(sb, juror.OpinionHistory.Select(s => s.VerdictLean).ToList());
            }
            else
            {
                sb.AppendLine($"  (No opinion tracking data available)");
            }
        }

        // ── Transcript Excerpt ──
        sb.AppendLine($"\n{'─',60}");
        sb.AppendLine("TRIAL TRANSCRIPT (last 2000 chars)");
        sb.AppendLine(new string('─', 60));
        string transcript = TranscriptOutput ?? "";
        if (transcript.Length > 2000)
            transcript = "...(earlier transcript omitted)...\n" + transcript[^2000..];
        sb.AppendLine(transcript);

        return sb.ToString();
    }

    /// <summary>
    /// Draws a simple ASCII sparkline showing opinion trend.
    /// </summary>
    private static void DrawAsciiSparkline(System.Text.StringBuilder sb, List<double> values)
    {
        if (values.Count < 2) { sb.AppendLine("  (need at least 2 data points)"); return; }
        double min = values.Min(), max = values.Max();
        double range = max - min;
        if (range < 0.01) range = 0.01;
        int height = 5;

        for (int row = height - 1; row >= 0; row--)
        {
            double threshold = min + (range * row / (height - 1));
            sb.Append("  ");
            foreach (var v in values)
            {
                if (v >= threshold) sb.Append('█');
                else sb.Append(' ');
            }
            if (row == height - 1) sb.Append($"  {max:P0}");
            else if (row == 0) sb.Append($"  {min:P0}");
            sb.AppendLine();
        }
        sb.Append("  ");
        for (int i = 0; i < values.Count; i++) sb.Append('▀');
        sb.AppendLine($"  {values.Count} points");
    }

    /// <summary>
    /// Runs all remaining deliberation rounds automatically, then
    /// generates the mock jury questionnaire and closes the court.
    /// </summary>
    public async Task AutoDeliberate()
    {
        if (!_deliberationInitialized)
            StartDeliberation();

        var jurors = _deliberationService
            .GetDeliberatingJurors(AllAgents)
            .Where(j => j.CanVote)
            .ToList();

        if (jurors.Count == 0)
        {
            TranscriptOutput = "[DELIBERATION] No jurors seated.";
            return;
        }

        // Auto-process evidence into juror TrialEvents if they haven't been through Discovery yet.
        // Without this, jurors deliberate without any memory of the exhibits.
        bool anyJurorHasMemories = jurors.Any(j => j.TrialEvents.Count > 0);
        if (!anyJurorHasMemories && CurrentCase.Evidence.Count > 0)
        {
            TranscriptOutput += "\n[AUTOPILOT] Processing evidence through jurors...\n";
            foreach (var doc in CurrentCase.Evidence)
            {
                var reporterSummary = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}: {doc.Summary}";
                foreach (var juror in jurors)
                {
                    // Prepend exhibit reference so questionnaire searches can find it.
                    // Format: "[Exhibit 1] Cell Phone Video - Confrontation.mp4: ... summary ..."
                    string memoryContent = $"[Exhibit {doc.ExhibitNumber}] {doc.FileName}: " +
                        (!string.IsNullOrWhiteSpace(doc.DetailedAnalysis)
                            ? doc.DetailedAnalysis
                            : doc.Summary);
                    juror.RecordTrialEvent(memoryContent, juror.Bias * 0.1);
                    juror.AnalyzeSentiment(reporterSummary);
                }
            }
            TranscriptOutput += $"Processed {CurrentCase.Evidence.Count} exhibits into juror memories.\n";
        }

        TranscriptOutput += "\n\n[AUTOPILOT] Running all deliberation rounds...\n";

        // Ensure at least one model is configured for LLM-driven deliberation.
        // If the user cleared models in Model Settings, restore defaults.
        if (CurrentCase.AvailableModels.Count == 0)
            CurrentCase.AddDefaultModels();

        // Run all remaining rounds, showing full deliberation transcript
        while (_deliberationRound < CurrentCase.DeliberationRounds)
        {
            _deliberationRound++;
            if (_spokenJurorIds.Count >= jurors.Count)
                _spokenJurorIds.Clear();

            try
            {
                var result = await _deliberationService.ExecuteTurnAsync(
                    jurors, _spokenJurorIds, _deliberationHistory,
                    CurrentCase, _agentInteraction, _deliberationRound);

                if (string.IsNullOrEmpty(_earlyThoughtLeaderName) && result.Persuasiveness >= 0.5)
                    _earlyThoughtLeaderName = result.SpeakingJuror.Name;

                _deliberationHighlights.Add(BuildTurnHighlight(
                    result.SpeakingJuror, result.Statement, result.Persuasiveness >= 0.5));

                // ── Show full round in transcript ──
                string roundOutput = $"\n\n--- Round {_deliberationRound} ---\n";
                roundOutput += $"[Round {_deliberationRound}] {result.SpeakingJuror.Name} " +
                    $"(lean {result.SpeakingJuror.VerdictLean:P0}, bias {result.SpeakingJuror.Bias:+#;-#;0}): " +
                    $"\"{result.Statement}\"\n";

                // Record opinion snapshots for the speaker (self-reinforcement)
                result.SpeakingJuror.RecordOpinionSnapshot(
                    $"Round {_deliberationRound}", $"Spoke: {TruncateContent(result.Statement, 80)}");

                // Show who was influenced and record their opinion changes
                if (result.InfluenceShifts.Count > 0)
                {
                    var influenced = result.InfluenceShifts
                        .Where(s => Math.Abs(s.Shift) > 0.005)
                        .OrderByDescending(s => Math.Abs(s.Shift))
                        .Take(5)
                        .Select(s =>
                        {
                            s.Juror.RecordOpinionSnapshot(
                                $"Round {_deliberationRound}",
                                $"Influenced by {result.SpeakingJuror.Name} ({s.Shift:+#0.00;-#0.00})");
                            return $"  {s.Juror.Name} shifted {s.Shift:+#0.00;-#0.00} " +
                                $"(now {s.Juror.VerdictLean:P0})";
                        })
                        .ToList();
                    if (influenced.Any())
                        roundOutput += string.Join("\n", influenced) + "\n";
                }

                // Show damages shifts (civil cases only)
                if (result.DamagesShifts.Count > 0)
                {
                    var dShifts = result.DamagesShifts
                        .Where(s => Math.Abs(s.NewDamages - s.OldDamages) > 100)
                        .OrderByDescending(s => Math.Abs(s.NewDamages - s.OldDamages))
                        .Take(3)
                        .Select(s =>
                        {
                            string oldStr = s.OldDamages > 0 ? $"${s.OldDamages:N0}" : "$0";
                            string newStr = $"${s.NewDamages:N0}";
                            return $"  {s.Juror.Name} damages: {oldStr} → {newStr}";
                        })
                        .ToList();
                    if (dShifts.Any())
                    {
                        roundOutput += "  [Damages influence]\n";
                        roundOutput += string.Join("\n", dShifts) + "\n";
                    }
                }

                // Periodic jury snapshot every 4 rounds or at detection of shift
                bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
                if (_deliberationRound % 4 == 0)
                {
                    int proCount = BurdenOfProof.CountProsecution(jurors, isCriminal);
                    int defCount = BurdenOfProof.CountDefense(jurors, isCriminal);
                    double maxLean = jurors.Max(j => j.VerdictLean);
                    double minLean = jurors.Min(j => j.VerdictLean);
                    string proLabel = isCriminal ? "Guilty" : "Liable";
                    string defLabel = isCriminal ? "Not Guilty" : "Not Liable";
                    roundOutput += $"  ── Snapshot ── {proCount} {proLabel} / {defCount} {defLabel} | " +
                        $"Lean range: {minLean:P0} – {maxLean:P0} | Spread: {maxLean - minLean:P0}";

                    // Include damages range for civil cases
                    if (!isCriminal)
                    {
                        var jurorsWithDamages = jurors.Where(j => j.ConsideredDamages > 0).ToList();
                        if (jurorsWithDamages.Any())
                        {
                            double minD = jurorsWithDamages.Min(j => j.ConsideredDamages);
                            double maxD = jurorsWithDamages.Max(j => j.ConsideredDamages);
                            double medD = jurorsWithDamages.OrderBy(j => j.ConsideredDamages)
                                .ElementAt(jurorsWithDamages.Count / 2).ConsideredDamages;
                            roundOutput += $" | Damages: ${minD:N0} – ${maxD:N0} (median ${medD:N0})";
                        }
                    }
                    roundOutput += "\n";
                }

                TranscriptOutput += roundOutput;

                NotifyJuryUpdate();

                if (_deliberationService.IsDeliberationComplete(
                        jurors, _deliberationRound, CurrentCase.DeliberationRounds,
                        out _, out bool isHung, CurrentCase.Mode))
                {
                    ConcludeDeliberation(jurors, hungPossible: isHung);
                    break;
                }
            }
            catch (InvalidOperationException ex)
            {
                TranscriptOutput = $"[DELIBERATION] {ex.Message}";
                break;
            }
        }

        // Generate the mock jury questionnaire
        GenerateMockJuryQuestionnaire();

        // Close the court
        CurrentDebateStage = CourtPhase.CourtClosing;
        CurrentCase.CurrentDebateStage = CourtPhase.CourtClosing;
        TranscriptOutput += "\n[COURT CLOSED] The trial has concluded.";
    }

    public async Task DeliberateNextTurn()
    {
        if (!_deliberationInitialized)
            StartDeliberation();

        var jurors = _deliberationService
            .GetDeliberatingJurors(AllAgents)
            .Where(j => j.CanVote)
            .ToList();

        if (jurors.Count == 0)
        {
            TranscriptOutput = "[DELIBERATION] No jurors seated.";
            return;
        }

        // Ensure at least one model is configured
        if (CurrentCase.AvailableModels.Count == 0)
            CurrentCase.AddDefaultModels();

        // Determine which juror gets to speak next: strongest feelings that haven't spoken yet.
        // Strong opinion threshold is based on distance from the neutral point (0.5).
        _deliberationRound++;
        if (_deliberationRound > CurrentCase.DeliberationRounds)
        {
            ConcludeDeliberation(jurors, hungPossible: true);
            return;
        }

        if (_spokenJurorIds.Count >= jurors.Count)
            _spokenJurorIds.Clear();

        try
        {
            var result = await _deliberationService.ExecuteTurnAsync(
                jurors, _spokenJurorIds, _deliberationHistory,
                CurrentCase, _agentInteraction, _deliberationRound);

            if (string.IsNullOrEmpty(_earlyThoughtLeaderName) && result.Persuasiveness >= 0.5)
                _earlyThoughtLeaderName = result.SpeakingJuror.Name;

            _deliberationHighlights.Add(BuildTurnHighlight(
                result.SpeakingJuror, result.Statement, result.Persuasiveness >= 0.5));

            NotifyJuryUpdate();

            // Build a conversational transcript showing the jury discussion
            bool isCriminal = CurrentCase.Mode == CaseMode.Criminal;
            string leanLabel = result.Direction > 0 ? "PROSECUTION" : (result.Direction < 0 ? "DEFENSE" : "UNDECIDED");
            string statusLabel = result.IsAmbivalent ? "[AMBIVALENT]" : (result.Persuasiveness >= 0.5 ? "[CONFIDENT]" : "[TENTATIVE]");
            string verdictTerm = isCriminal ? "guilty/acquit" : "liable/not liable";

            // Show the conversation: who spoke, their lean, and what they said
            string line = $"Juror {result.SpeakingJuror.Name} {statusLabel} (leans {leanLabel}):\n  \"{result.Statement}\"";
            
            // Warn if fallback was used (LLM call failed)
            if (result.UsedFallback && !string.IsNullOrEmpty(result.FallbackReason))
                line += $"\n  \u26A0 [FALLBACK] LLM unavailable: {result.FallbackReason}";
            
            // Show influence on other jurors
            if (result.InfluenceShifts.Count > 0)
            {
                var influenced = result.InfluenceShifts
                    .Where(s => Math.Abs(s.Shift) > 0.005)
                    .Select(s => $"{s.Juror.Name} ({s.Shift:+0.00;-0.00})")
                    .ToList();
                if (influenced.Count > 0)
                    line += $"\n  \u2192 Influenced: {string.Join(", ", influenced)}";
            }

            // Show damages shifts for civil cases
            if (result.DamagesShifts.Count > 0)
            {
                var dShifts = result.DamagesShifts
                    .Where(s => Math.Abs(s.NewDamages - s.OldDamages) > 100)
                    .Take(3)
                    .Select(s =>
                    {
                        string o = s.OldDamages > 0 ? $"${s.OldDamages:N0}" : "$0";
                        return $"{s.Juror.Name} ({o} → ${s.NewDamages:N0})";
                    })
                    .ToList();
                if (dShifts.Any())
                    line += $"\n  \u2192 Damages influenced: {string.Join(", ", dShifts)}";
            }

            // Show current vote tally
            var voters = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
            int proCount = voters.Count(v => v.VerdictLean > 0.5);
            int defCount = voters.Count(v => v.VerdictLean < 0.5);
            int undCount = voters.Count(v => Math.Abs(v.VerdictLean - 0.5) < 0.01);
            string voteTally = isCriminal
                ? $"Guilty: {proCount} | Not Guilty: {defCount}" + (undCount > 0 ? $" | Undecided: {undCount}" : "")
                : $"Liable: {proCount} | Not Liable: {defCount}" + (undCount > 0 ? $" | Undecided: {undCount}" : "");
            line += $"\n  Vote: {voteTally}";

            // Add damages tally for civil cases
            if (!isCriminal)
            {
                var withDamages = voters.Where(v => v.ConsideredDamages > 0).ToList();
                if (withDamages.Any())
                {
                    double medD = withDamages.OrderBy(v => v.ConsideredDamages)
                        .ElementAt(withDamages.Count / 2).ConsideredDamages;
                    line += $"\n  Damages: median ${medD:N0} (range ${withDamages.Min(v => v.ConsideredDamages):N0} – ${withDamages.Max(v => v.ConsideredDamages):N0})";
                }
            }

            TranscriptOutput = $"{TranscriptOutput}\n\n--- Round {_deliberationRound} ---\n{line}";
            BroadcastEvent(line, Enum.GetValues<AgentRole>().ToList(), isSidebar: false);

            if (_deliberationService.IsDeliberationComplete(
                    jurors, _deliberationRound, CurrentCase.DeliberationRounds,
                    out _, out bool isHung, CurrentCase.Mode))
            {
                ConcludeDeliberation(jurors, hungPossible: isHung);
            }
        }
        catch (InvalidOperationException ex)
        {
            TranscriptOutput = $"[DELIBERATION] {ex.Message}";
        }

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

        // Gentle memory fade on each interaction — over the course of a trial,
        // exact numbers blur into approximations, then ranges, then vague impressions.
        // Using 0.995 (0.5% per line) so it takes ~140 transcript lines to halve memory strength,
        // rather than ~34 lines with the old 0.98 factor.
        foreach (var agent in AllAgents.Where(a => a.IsOccupied))
            agent.DecayMemories(0.995);

        // Broadcast without applying transcript influence and without triggering any LLM stage reactions.
        var roles = Enum.GetValues<AgentRole>().ToList();
        BroadcastEvent(TranscriptOutput, roles);

        // Trigger stage-based AI responses (now subject to runtime restrictions).
        // TriggerAgentResponses is designed to be called after user input.
        await TriggerAgentResponses(speaker, content);

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
                    reporterModel = FindModel(reporter.SelectedModel);
                reporterModel ??= DefaultModel;

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
                        var model = DefaultModel;
                        foreach (var agent in AllAgents.Where(a => a.IsOccupied && a.Role != AgentRole.Reporter))
                        {
                            AIModelConfiguration? agentModel = null;
                            if (!string.IsNullOrEmpty(agent.SelectedModel))
                                agentModel = FindModel(agent.SelectedModel);
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
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney",
                            $"Respond to the opening statement. Reference specific exhibits by number " +
                            $"(e.g., \"Exhibit 2 shows...\", \"The defense will rely on Exhibit 4...\") " +
                            $"to ground your response in the evidence.\n\n{GetEvidenceSummary()}", transcript);
                    }
                    else if (speaker.Contains("Defense"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor",
                            $"Respond to the opening statement. Reference specific exhibits by number " +
                            $"(e.g., \"Exhibit 1 proves...\", \"Exhibit 3 clearly shows...\") " +
                            $"to ground your response in the evidence.\n\n{GetEvidenceSummary()}", transcript);
                    }
                    break;

                case CourtPhase.WitnessTestimony:
                    // Have the judge acknowledge
                    await GenerateAgentResponse(AgentRole.Judge, "Judge",
                        $"Acknowledge the testimony just given, referencing the specific exhibits mentioned. " +
                        $"Speaker: {speaker}. Content: {content}\n\nExhibits in evidence:\n{GetEvidenceSummary()}", transcript);
                    break;

                case CourtPhase.CrossExamination:
                    // The witness answers the examining attorney's question.
                    // They must be truthful and stick to what the evidence shows,
                    // but may use biased language in how they describe things.
                    await GenerateWitnessResponse(speaker, content, transcript);
                    break;

                case CourtPhase.ClosingArguments:
                    // Have the opposing counsel respond
                    if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney",
                            $"Deliver a PERSUASIVE rebuttal closing argument. Write approximately 500 words. " +
                            $"Counter the plaintiff's case point by point, reframing every exhibit " +
                            $"in the light most favorable to the defense. Reference specific exhibits " +
                            $"by number (e.g., \"Exhibit 2 contradicts that...\"). " +
                            $"Appeal to the jury to find in your favor.\n\n{GetEvidenceSummary()}", transcript);
                    }
                    else if (speaker.Contains("Defense"))
                    {
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor",
                            $"Deliver a PERSUASIVE rebuttal closing argument. Write approximately 500 words. " +
                            $"Reinforce why the evidence proves your case, reframing every exhibit " +
                            $"in the light most favorable to the plaintiff/prosecution. Reference specific " +
                            $"exhibits by number (e.g., \"Exhibit 1 proves our case...\"). " +
                            $"Appeal to the jury to find in your favor.\n\n{GetEvidenceSummary()}", transcript);
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
        modelConfig ??= DefaultModel;

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
        try
        {
            var loadedCase = _caseService.LoadCase(filePath);
            if (loadedCase != null)
            {
                LoadCaseInternal(loadedCase);
            }
        }
        catch (Exception ex)
        {
            TranscriptOutput = $"[ERROR] Failed to load case: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadCase error: {ex}");
            System.Windows.MessageBox.Show(
                $"Failed to load the case file.\n\n{ex.Message}",
                "Load Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void LoadCaseInternal(CaseFile loadedCase)
    {
        loadedCase.EnsureCollectionsInitialized();
        CurrentCase = loadedCase;
        RefreshExhibits();

        // Restore the courtroom agent collections from the loaded case's agents
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, loadedCase);
        // Move alternates from Gallery to dedicated AlternateJurors collection
        MoveAlternatesFromGallery();

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
                    var altSlot = (seatMatched ?? AlternateJurors.FirstOrDefault(a => !a.IsOccupied));
                    if (altSlot != null) agent.CopyTo(altSlot);
                    break;

                case AgentRole.Lawyer:
                    // Place lawyers on the correct side based on CommunicationTeam
                    var isDefense = string.Equals(agent.CommunicationTeam, "Defendant", StringComparison.OrdinalIgnoreCase);
                    var isProsecution = string.Equals(agent.CommunicationTeam, "Plaintiff", StringComparison.OrdinalIgnoreCase);

                    if (isDefense)
                    {
                        var defSlot = (seatMatched ?? DefenseTeam.FirstOrDefault(a => !a.IsOccupied));
                        if (defSlot != null) agent.CopyTo(defSlot);
                    }
                    else if (isProsecution)
                    {
                        var prosSlot = (seatMatched ?? ProsecutionTeam.FirstOrDefault(a => !a.IsOccupied));
                        if (prosSlot != null) agent.CopyTo(prosSlot);
                    }
                    else
                    {
                        var lawyerSlot = (seatMatched ?? DefenseTeam.FirstOrDefault(a => !a.IsOccupied));
                        if (lawyerSlot != null)
                            agent.CopyTo(lawyerSlot);
                        else
                        {
                            var prosSlot2 = (seatMatched ?? ProsecutionTeam.FirstOrDefault(a => !a.IsOccupied));
                            if (prosSlot2 != null) agent.CopyTo(prosSlot2);
                        }
                    }
                    break;

                case AgentRole.Client:
                case AgentRole.Observer:
                    var obsSlot = (seatMatched ?? Gallery.FirstOrDefault(a => a.Role == agent.Role && !a.IsOccupied));
                    if (obsSlot != null) agent.CopyTo(obsSlot);
                    break;
            }
        }

        // Seed jurors' TrialEvents and ExhibitLog from loaded evidence so deliberation has context.
        // Use DetailedAnalysis (LLM-generated) when available, otherwise fall back to Summary.
        // This mirrors what happens during a live session when AddEvidence is called.
        foreach (var doc in loadedCase.Evidence)
        {
            string exhibitSummary = $"Exhibit {doc.ExhibitNumber}: {doc.FileName} - {doc.Summary}";

            // Build rich evidence context prefixed with exhibit identifier so jurors
            // can reference specific exhibits during deliberation.
            string analysisContent = !string.IsNullOrWhiteSpace(doc.DetailedAnalysis)
                ? doc.DetailedAnalysis
                : !string.IsNullOrWhiteSpace(doc.Summary)
                    ? doc.Summary
                    : doc.FileName;
            string richContext = $"[Exhibit {doc.ExhibitNumber}: {doc.FileName}] {analysisContent}";

            foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
            {
                if (!juror.ExhibitLog.Contains(exhibitSummary))
                    juror.ExhibitLog.Add(exhibitSummary);

                // Also seed TrialEvents with the rich analysis so ExtractKeyPoints finds it
                if (!juror.TrialEvents.Any(e => e.Content == richContext))
                    juror.RecordTrialEvent(richContext, juror.Bias * 0.1);
            }
            var reporter = AllAgents.FirstOrDefault(a => a.IsOccupied && a.Role == AgentRole.Reporter);
            if (reporter != null && !reporter.ExhibitLog.Contains(exhibitSummary))
                reporter.ExhibitLog.Add(exhibitSummary);
        }

        // Set reporter's document count from loaded evidence
        var loadedReporter = AllAgents.FirstOrDefault(a => a.IsOccupied && a.Role == AgentRole.Reporter);
        if (loadedReporter != null)
            loadedReporter.DocumentCount = loadedCase.Evidence.Count;

        // Auto-assign the default model to all agents that don't have a valid model selected.
        // This ensures jurors and other agents can immediately use the configured LLM after case load.

        // First, ensure the DeepSeek model from the Models folder is available
        var modelsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        if (System.IO.Directory.Exists(modelsFolder))
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var jsonFile in System.IO.Directory.GetFiles(modelsFolder, "*.json"))
            {
                try
                {
                    var modelJson = System.IO.File.ReadAllText(jsonFile);
                    var model = System.Text.Json.JsonSerializer.Deserialize<AIModelConfiguration>(modelJson, opts);
                    if (model != null && !loadedCase.AvailableModels.Any(m => m.FriendlyName == model.FriendlyName))
                        loadedCase.AvailableModels.Add(model);
                }
                catch { /* skip malformed */ }
            }
        }

        // Remove models whose providers are not active (no matching provider registered)
        var activeProviders = ProviderDiscoveryService.GetAvailableProviders();
        loadedCase.AvailableModels.RemoveAll(m =>
            !activeProviders.Any(p => p.ProviderName.Equals(m.Provider, StringComparison.OrdinalIgnoreCase)));

        // Fallback model when no role-specific default is set (prefer one with an API key)
        var fallbackModel = loadedCase.AvailableModels.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.ApiKey))
                         ?? loadedCase.AvailableModels.LastOrDefault()
                         ?? loadedCase.AvailableModels.FirstOrDefault();

        if (fallbackModel != null)
        {
            foreach (var agent in AllAgents.Where(a => a.IsOccupied))
            {
                bool hasValidModel = !string.IsNullOrWhiteSpace(agent.SelectedModel)
                    && loadedCase.AvailableModels.Any(m => m.FriendlyName == agent.SelectedModel);
                if (!hasValidModel)
                {
                    agent.SelectedModel = GetDefaultModelForRole(agent.Role, agent.CommunicationTeam, loadedCase)
                                       ?? fallbackModel.FriendlyName;
                }
            }
        }

        // Debate stage is disabled; always present as deliberation.
        _currentDebateStage = CourtPhase.JuryDeliberation;
        OnPropertyChanged(nameof(CurrentDebateStage));
        OnPropertyChanged(nameof(CurrentDebateStageDisplay));


        // Notify UI of case property changes
        OnPropertyChanged(nameof(CurrentCase));
        NotifyJuryUpdate();

        TranscriptOutput = $"[CASE LOADED] Case '{loadedCase.CaseName}' loaded successfully. {loadedCase.Evidence.Count} exhibits in record.";
        UpdateWindowTitle();
        RefreshPhaseLabels();
    }

/// <summary>
    /// Extracts entities from the loaded transcript using LLM analysis.
    /// Delegates entity mapping to ICaseEntityMapper.
    /// </summary>
    public async Task ExtractEntitiesFromTranscriptAsync()
    {
        var model = DefaultModel;
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

        var defaultModel = DefaultModel;
        int jurorCount = 0;

        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            // Clear previous evidence reactions so fresh psychology is applied
            juror.TrialEvents.Clear();
            juror.ConsideredDamages = 0;
            juror.VerdictLean = 0.5;

            AIModelConfiguration? jurorModel = FindModel(juror.SelectedModel)
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
    /// Generates a plain-text report for the current case and returns it as a string.
    /// Opens the ReportWindow to display it.
    /// </summary>
    public string GenerateTextReport()
    {
        var reportService = new ReportGenerationService();
        return reportService.BuildTextReport(CurrentCase, AllAgents);
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

