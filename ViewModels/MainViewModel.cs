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
public partial class MainViewModel : ViewModelBase
{
    private static List<BiasFactor>? ToBiasFactorsFromModelCustomSettings(Dictionary<string, string>? customSettings)
    {
        if (customSettings == null || customSettings.Count == 0) return null;

        // ModelWeightsWindow edits these scalar bias weights; persist them into the model's CustomSettings.
        // Translate them into the BiasFactors list that JOEService consumes.
        // Expected keys (see ModelWeightsWindow.xaml bindings):
        // AgeBiasWeight, GenderBiasWeight, EducationBiasWeight, IncomeBiasWeight,
        // PoliticalBiasWeight, EthnicityBiasWeight, ReligionBiasWeight,
        // JurorExperienceWeight, LegalKnowledgeWeight, ProfessionalBackgroundWeight,
        // CommunityTiesWeight, CommunicationStyleWeight

        var map = new List<BiasFactor>();

        void Add(string key, string biasName)
        {
            if (!customSettings.TryGetValue(key, out var str)) return;
            if (string.IsNullOrWhiteSpace(str)) return;
            if (!double.TryParse(str, out var v)) return;
            map.Add(new BiasFactor { Name = biasName, Weight = Math.Clamp(v, 0.0, 1.0) });
        }

        // BiasFactor names must match the ones used in JOEService.ComputeRigidity (it looks up weights in coefs via trait values,
        // but it ultimately uses BiasFactors only for biasLookup keys; the engine builds its lookup from bf.Name).
        // The toy engine expects lower-level terms like "pol_id" etc, but it only uses biasFactors to influence trait generation;
        // in practice, the rest of the toy engine reads agent traits, not these BiasFactor names.
        // Still, we preserve the existing CaseFile defaults convention:
        Add("AgeBiasWeight", "Age");
        Add("GenderBiasWeight", "Gender");
        Add("EducationBiasWeight", "Education Level");
        Add("IncomeBiasWeight", "Income Level");
        Add("PoliticalBiasWeight", "Political Affiliation");
        Add("EthnicityBiasWeight", "Ethnicity");
        Add("ReligionBiasWeight", "Religion");
        Add("JurorExperienceWeight", "Juror Experience");
        Add("LegalKnowledgeWeight", "Legal Knowledge");
        Add("ProfessionalBackgroundWeight", "Professional Background");
        Add("CommunityTiesWeight", "Community Ties");
        Add("CommunicationStyleWeight", "Communication Style");

        return map.Count == 0 ? null : map;
    }

    // Service dependencies
    private readonly ICaseService _caseService;
    private readonly ITranscriptService? _transcriptService;
    private readonly ISettingsService _settingsService;
    private readonly IJuryDemographicsService _juryService;
    private readonly ICourtroomManagerService _courtroomManager;
    private readonly IEvidenceAnalysisService _evidenceService;
    private readonly IDebateService _debateService;
    private readonly IJuryCalculationService _juryCalc;
    private readonly ICaseEntityMapper _entityMapper;
    private readonly IAgentInteractionService _agentInteraction;
    private readonly IInsuranceAdjusterPricingService _adjusterPricingService;

    private string _lastDeliberationStatement = string.Empty;
    public AsyncRelayCommand<string[]> SubmitClerkDocumentsCommand { get; }

    public Func<string, string, string?>? ShowClerkEvidenceSummaryEditor { get; set; }

    private InsuranceAdjusterState? _insuranceAdjusterState;

    public InsuranceAdjusterState? InsuranceAdjusterState
    {
        get => _insuranceAdjusterState;
        private set => SetProperty(ref _insuranceAdjusterState, value);
    }

/// <summary>
/// Exposes the agent interaction service for use by other components.
/// </summary>
public IAgentInteractionService AgentInteractionService => _agentInteraction;

// Deliberation state fields (used by MainViewModel.Deliberation.cs)
private readonly HashSet<Guid> _spokenJurorIds = new();
private int _deliberationRound;
 private bool _deliberationInitialized;

 private CaseFile _currentCase = new();
 private string _transcriptOutput = "";
 private string _windowTitle = "VERDICT";

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    // CurrentDebateStageDisplay always returns "Jury Deliberation" since that is the only active stage.
    // Other court phases are developmental placeholders.
    public string CurrentDebateStageDisplay => "Jury Deliberation";

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

    // All CourtPhase stages except JuryDeliberation are developmental placeholders.
    // JuryDeliberation is the only active stage for user interaction.
    // TODO: Opening Statements, Witness Testimony, Cross Examination, Party Statements,
    // and Closing Arguments stages require LLM-driven agent behavior implementation.
    public CourtPhase CurrentDebateStage
    {
        get => CourtPhase.JuryDeliberation;
        set
        {
            // Only JuryDeliberation stage is active; other stages are not yet implemented.
            OnPropertyChanged(nameof(CurrentDebateStage));
            OnPropertyChanged(nameof(CurrentDebateStageDisplay));
        }
    }

    // AdvanceDebateStage is disabled - all court phases except JuryDeliberation are developmental placeholders.
    // JuryDeliberation is the only active stage for user interaction.
    public void AdvanceDebateStage()
    {
        // No-op: court phases are not yet implemented. Only JuryDeliberation stage is active.
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
    /// For criminal cases this is a convict/acquit outcome (unanimity).
    /// For civil cases this is a majority outcome.
    /// </summary>
    public string LikelyVerdict => _juryCalc.FinalVerdict(Jurors, CurrentCase?.Mode ?? CaseMode.Civil);

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
        : this(new CaseService(), null, new SettingsService(),
            new JuryDemographicsService(), new CourtroomManagerService(),
            new EvidenceAnalysisService(), new DebateService(),
            new JuryCalculationService(), new InsuranceAdjusterPricingService(), null,
            new AgentInteractionService(ProviderDiscoveryService.GetAvailableProviders()))
    {
    }

    public MainViewModel(
        ICaseService caseService,
        ITranscriptService? transcriptService,
        ISettingsService settingsService,
        IJuryDemographicsService juryService,
        ICourtroomManagerService courtroomManager,
        IEvidenceAnalysisService evidenceService,
        IDebateService debateService,
        IJuryCalculationService juryCalc,
        IInsuranceAdjusterPricingService adjusterPricing,
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
        _adjusterPricingService = adjusterPricing;
        _entityMapper = entityMapper ?? new CaseEntityMapper(evidenceService);
        _agentInteraction = agentInteraction ?? new AgentInteractionService(ProviderDiscoveryService.GetAvailableProviders());

        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, _currentCase);
        NewCase();

        SubmitClerkDocumentsCommand = new AsyncRelayCommand<string[]>(ExecuteSubmitClerkDocumentsAsync);
    }

    private async Task ExecuteSubmitClerkDocumentsAsync(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0) return;
        if (ShowClerkEvidenceSummaryEditor == null) return;

        string clerkAttorney = string.Join(", ", DefenseTeam.Concat(ProsecutionTeam)
            .Where(a => a.IsOccupied && a.Role == AgentRole.Lawyer)
            .Select(a => a.Name));

        int processedCount = 0;
        foreach (var filePath in filePaths)
        {
            string fileNameOnly = System.IO.Path.GetFileName(filePath);
            string fileContent = _evidenceService.ReadFileContent(filePath);
            var (detailedAnalysis, _) = await _evidenceService.GenerateDocumentAnalysisAsync(
                fileNameOnly, fileContent, "Submitted via Court Clerk", CurrentCase, _agentInteraction);

            string defaultSummary = $"Document: {fileNameOnly}\n\n" +
                $"User Notes: Submitted via Court Clerk\n\n" +
                $"[AI ANALYSIS]\n{detailedAnalysis}";

            string? editedSummary = ShowClerkEvidenceSummaryEditor(fileNameOnly, defaultSummary);
            if (editedSummary != null)
            {
                await AddEvidence(fileNameOnly, filePath, editedSummary, detailedAnalysis, clerkAttorney);
                var reporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter);
                if (reporter != null)
                    reporter.DocumentCount++;
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            var result = processedCount == 1
                ? "1 document submitted and parsed into evidence."
                : $"{processedCount} documents submitted and parsed into evidence.";
        }
    }

    public void ReinitializeCourtroom()
    {
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, CurrentCase);
    }

    public void NewCase()
    {
        var defaults = _settingsService.GetDefaultCaseSettings();
        defaults.EnsureCollectionsInitialized();
        CurrentCase = defaults;
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, defaults);
        RefreshExhibits();

        TranscriptOutput = "Court is in session. Awaiting transcript...";
        UpdateWindowTitle();
    }

    public CaseFile GetDefaultSettings()
    {
        var defaults = _settingsService.GetDefaultCaseSettings();
        defaults.EnsureCollectionsInitialized();
        return defaults;
    }

    public void SaveDefaultSettings(CaseFile defaults) => _settingsService.SaveDefaultCaseSettings(defaults);

    public async Task GenerateJury()
    {
        string jurisdiction = CurrentCase.JurisdictionSpecifics ?? "";

        if (string.IsNullOrWhiteSpace(jurisdiction))
        {
            jurisdiction = "Richland County, South Carolina";
        }

        string[] parts = jurisdiction.Split(',');
        string county = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : "Richland County";
        string state = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : "South Carolina";

        var generatedJurors = _juryService.GenerateJuryPanel(12, 2, county, state);

        Jurors.Clear();
        foreach (var juror in generatedJurors)
        {
            // Generated jurors must be marked occupied so the seat template renders their avatar/icon.
            juror.IsOccupied = true;

            if (CurrentCase.DefaultBiasFactors != null && CurrentCase.DefaultBiasFactors.Count > 0)
            {
                juror.BiasFactors.Clear();
                foreach (var bf in CurrentCase.DefaultBiasFactors)
                    juror.BiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
            }

            Jurors.Add(juror);
        }

        if (CurrentCase.Evidence.Count > 0)
        {
            foreach (var juror in Jurors)
            {
                // When generating a new jury, jurors must re-create their own evidence memories
                // from the reporter's neutral record (not the reporter's private memory).
                //
                // We preserve any existing TrialEvents on the juror instance, but we also
                // ensure each juror gets a fresh internalization from the reporter record
                // so that their impressions are regenerated for this jury.
            juror.ConsideredDamages = 0;
                juror.VerdictLean = 0.5;

                // Ensure the selected model's bias weight configuration is applied to the jurors.
                // ModelWeightsWindow edits live on the model's settings/custom settings; translate them
                // into BiasFactors so JOEService can consume them.
                var selectedModelCfg = CurrentCase.AvailableModels.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(juror.SelectedModel) &&
                    m.FriendlyName == juror.SelectedModel) ??
                    CurrentCase.AvailableModels.FirstOrDefault();

                var configuredFactors = selectedModelCfg?.CustomSettings != null
                    ? ToBiasFactorsFromModelCustomSettings(selectedModelCfg.CustomSettings)
                    : null;



                if (configuredFactors != null && configuredFactors.Count > 0)
                {
                    juror.BiasFactors.Clear();
                    foreach (var bf in configuredFactors)
                        juror.BiasFactors.Add(bf);
                }
            }


            // Re-internalize each admitted exhibit impression from the reporter log when regenerating the jury.
            // EvidenceAdmissionService stores the reporter's exhibit summaries in reporter.ExhibitLog.
            var reporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
            var reporterModel = reporter != null
                ? (CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == reporter.SelectedModel)
                   ?? CurrentCase.AvailableModels.FirstOrDefault())
                : null;

            foreach (var juror in Jurors)
            {
                // Reset only this juror's derived impressions; reporter summaries remain neutral/factual.
                juror.TrialEvents.Clear();
                juror.ConsideredDamages = 0;

                foreach (var exhibitSummary in reporter?.ExhibitLog ?? Enumerable.Empty<string>())
                {
                    var reaction = reporterModel != null
                        ? await _agentInteraction.InternalizeCourtRecordAsync(juror, exhibitSummary, reporterModel)
                        : exhibitSummary;

                    // Ensure juror memories are discoverable in JurorReportWindow:
                    // TrialEvents are bound to "Trial Memories".
                    juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
                }

                // If no exhibits exist yet, still add a baseline memory so the UI isn't empty.
                if (!(reporter?.ExhibitLog ?? Enumerable.Empty<string>()).Any())
                {
                    juror.TrialEvents.Add(new MemoryEntry
                    {
                        Content = "(No evidence has been admitted yet.)",
                        Strength = 0.2,
                        Timestamp = DateTime.Now,
                        IsFromDocument = false,
                        Source = "Baseline"
                    });
                }

                juror.VerdictLean = 0.5;
            }


            foreach (var doc in CurrentCase.Evidence)
            {
                double baseStrength = _evidenceService.AssessStrength(doc, doc.Summary, "");
                foreach (var juror in Jurors)
                {
                    juror.PerceivedEvidence.Add(new EvidencePerception { ExhibitNumber = doc.ExhibitNumber, PerceivedStrength = _evidenceService.CalculatePerceivedStrength(juror, doc, baseStrength) });
                }
                _juryCalc.ApplyEvidenceInfluence(AllAgents, doc, CurrentCase.Mode);
            }

            NotifyJuryUpdate();
        }

        TranscriptOutput = $"[JURY GENERATED] {generatedJurors.Count} jurors generated for {county}, {state}";
        BroadcastEvent($"Jury panel generated based on {county} County demographics",
            Enum.GetValues<AgentRole>().ToList());
    }

    public Agent GenerateSingleJuror(string county, string state = "South Carolina")
    {
        if (string.IsNullOrWhiteSpace(county)) county = "Richland County";
        if (string.IsNullOrWhiteSpace(state)) state = "South Carolina";

        var juror = _juryService.GenerateJuror(county, state);
        juror.Role = AgentRole.Juror;
        return juror;
    }

    public void OccupySlot(Agent agent) => _courtroomManager.OccupySlot(agent, DefenseTeam, ProsecutionTeam);

    public void BroadcastEvent(string description, List<AgentRole> visibleTo, bool isSidebar = false)
    {
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
    /// Admits evidence into the case file and broadcasts it to the courtroom.
    /// </summary>
    public async Task AddEvidence(string fileName, string path, string summary, string? existingDetailedAnalysis = null, string offeringAttorney = "")
    {
        string fileContent = _evidenceService.ReadFileContent(path);

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

        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        string mediaType = extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" ? "Image" : "Document";

        double baseStrength = 0.5;
        var doc = new EvidenceDocument
        {
            FileName = fileName,
            FilePath = path,
            Summary = summary,
            ExhibitNumber = CurrentCase.Evidence.Count + 1,
            MediaType = mediaType,
            DetailedAnalysis = detailedAnalysis,
            OfferingAttorney = offeringAttorney,
            IsOfferedByPlaintiffSide = !string.IsNullOrWhiteSpace(offeringAttorney)
                                       && !string.IsNullOrWhiteSpace(CurrentCase.PlaintiffAttorney)
                                       && offeringAttorney.Trim().Equals(CurrentCase.PlaintiffAttorney.Trim(), StringComparison.OrdinalIgnoreCase),
        };

        if (llmDamages > 0)
        {
            doc.EstimatedDamages = llmDamages;
            doc.EvidenceStrength = 0.85;
        }
        else
        {
            baseStrength = _evidenceService.AssessStrength(doc, summary, fileContent);
        }

        int nextPlaintiffSide = CurrentCase.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;
        int nextDefenseSide = CurrentCase.Evidence.Count(e => !e.IsOfferedByPlaintiffSide) + 1;
        if (doc.IsOfferedByPlaintiffSide) doc.PlaintiffSideExhibitNumber = nextPlaintiffSide;
        else doc.DefenseSideExhibitNumber = nextDefenseSide;

        CurrentCase.Evidence.Add(doc);
        RefreshExhibits();
        _evidenceService.CalculateExposure(CurrentCase);
        _juryCalc.ApplyEvidenceInfluence(AllAgents, doc, CurrentCase.Mode);

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

        BroadcastEvent($"{phasePrefix} Evidence - Exhibit {doc.ExhibitNumber}: {fileName}\n{attorneyPrefix}{summary}\n" +
                       $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}",
            Enum.GetValues<AgentRole>().ToList());

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

        var defaultModel = CurrentCase.AvailableModels.FirstOrDefault();
        foreach (var juror in AllAgents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)))
        {
            AIModelConfiguration? jurorModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == juror.SelectedModel)
                                               ?? defaultModel;
            string reaction = jurorModel != null
                ? await _agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                : reporterSummary;
            
            // Per-agent perceived strength determination
            double perceived = _evidenceService.CalculatePerceivedStrength(juror, doc, baseStrength);
            juror.PerceivedEvidence.Add(new EvidencePerception { ExhibitNumber = doc.ExhibitNumber, PerceivedStrength = perceived });
            
            juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
        }

        NotifyJuryUpdate();
    }

    /// <summary>
    /// Adds testimony entered as evidence (no file). This creates an EvidenceDocument so it
    /// appears in the Exhibit List and influences jurors.
    /// </summary>
    public async Task AddTestimonyEvidence(
        string witnessName,
        string testimonyText,
        string offeringAttorney,
        bool offeredByPlaintiff,
        string? targetCharacterNameForContext = null)
    {
        if (string.IsNullOrWhiteSpace(testimonyText))
            return;

        witnessName = string.IsNullOrWhiteSpace(witnessName) ? (targetCharacterNameForContext ?? "Witness") : witnessName.Trim();
        offeringAttorney ??= string.Empty;

        string callerSideLabel = offeredByPlaintiff ? "Plaintiff" : "Defense";

        string phasePrefix = CurrentCase.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "EVIDENCE"
        };

        // Clerk record / exhibit summary
        string summary = $"Plaintiff/Defense calls {witnessName}.\n\n" +
                          $"Caller side: {callerSideLabel}.\n" +
                          $"Testimony (recorded verbatim by the clerk):\n{testimonyText}";

        var doc = new EvidenceDocument
        {
            FileName = $"Testimony - {witnessName}",
            FilePath = string.Empty,
            Summary = summary,
            ExhibitNumber = CurrentCase.Evidence.Count + 1,
            MediaType = "Document",
            DetailedAnalysis = null,
            OfferingAttorney = offeringAttorney,
            IsOfferedByPlaintiffSide = offeredByPlaintiff,
            IsDiscoveryComplete = CurrentCase.TrialPhase != TrialPhase.Trial
        };

        // Strength and damages from the testimony text
        _evidenceService.AssessStrength(doc, summary, testimonyText);

        // Side numbering (per presenting party)
        int nextPlaintiffSide = CurrentCase.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;
        int nextDefenseSide = CurrentCase.Evidence.Count(e => !e.IsOfferedByPlaintiffSide) + 1;
        if (doc.IsOfferedByPlaintiffSide) doc.PlaintiffSideExhibitNumber = nextPlaintiffSide;
        else doc.DefenseSideExhibitNumber = nextDefenseSide;

        CurrentCase.Evidence.Add(doc);
        RefreshExhibits();
        _evidenceService.CalculateExposure(CurrentCase);
        _juryCalc.ApplyEvidenceInfluence(AllAgents, doc, CurrentCase.Mode);

        string attorneyPrefix = string.IsNullOrEmpty(offeringAttorney) ? "" : $"Offered by: {offeringAttorney}\n";
        TranscriptOutput = $"[{phasePrefix}] Testimony Exhibit {doc.ExhibitNumber}: {doc.FileName}\n" +
                           $"{attorneyPrefix}" +
                           $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}, " +
                           $"Settlement: ${CurrentCase.EstimatedSettlement:N0}, " +
                           $"Reserve: ${CurrentCase.InsuranceReserve:N0}";

        BroadcastEvent($"{phasePrefix} Testimony - Exhibit {doc.ExhibitNumber}: {doc.FileName}\n{attorneyPrefix}{summary}\n" +
                       $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}",
            Enum.GetValues<AgentRole>().ToList());

        // Reporter records into the court record (neutral-ish)
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
            reporterSummary = $"Exhibit {doc.ExhibitNumber}, Testimony of {witnessName}, admitted into evidence.";
        }

        // Jurors internalize the reporter summary with their bias
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

    public void RefreshExhibits()
    {
        Exhibits.Clear();
        foreach (var doc in CurrentCase.Evidence)
            Exhibits.Add(doc);
    }

    public string ReadFileContent(string filePath) => _evidenceService.ReadFileContent(filePath);

    public Task<(string Analysis, double TotalDamages)> GenerateDocumentAnalysisAsync(
        string fileName, string fileContent, string userSummary) =>
        _evidenceService.GenerateDocumentAnalysisAsync(
            fileName, fileContent, userSummary, CurrentCase, _agentInteraction);

    public void CalculateExposure() => _evidenceService.CalculateExposure(CurrentCase);

    private void NotifyJuryUpdate()
    {
        OnPropertyChanged(nameof(JuryLiabilityAverage));
        OnPropertyChanged(nameof(LikelyVerdict));

        // Insurance/settlement is a civil exposure artifact; hide it for criminal trials.
        UpdateInsuranceAdjusterState();
    }

    private void UpdateInsuranceAdjusterState()
    {
        if (CurrentCase?.Mode == CaseMode.Criminal)
        {
            InsuranceAdjusterState = null;
            OnPropertyChanged(nameof(InsuranceAdjusterState));
            return;
        }

        var adjusterState = CurrentCase != null ? _adjusterPricingService.ComputeState(Jurors, CurrentCase) : null;
        InsuranceAdjusterState = adjusterState;

        // Notify bindings that depend on the nested object.
        OnPropertyChanged(nameof(InsuranceAdjusterState));
    }


    // ------------------------------
    // Deliberation Observable Output
    // ------------------------------
    public ObservableCollection<DeliberationEntry> DeliberationLog { get; } = new();

    /// <summary>
    /// Starts a new deliberation session, clearing previous logs and initializing state.
    /// </summary>
    public void StartDeliberation()
    {
        _spokenJurorIds.Clear();
        _deliberationRound = 0;
        _deliberationInitialized = true;
        _lastDeliberationStatement = string.Empty;
        DeliberationLog.Clear();
        
        DeliberationLog.Add(new DeliberationEntry 
        { 
            Speaker = "Court", 
            Message = "Deliberation begins. Jurors will now discuss the evidence.",
            Timestamp = DateTime.Now
        });
        
        TranscriptOutput = "[JURY DELIBERATION] Jurors are now discussing the evidence...";
        OnPropertyChanged(nameof(TranscriptOutput));
    }

    /// <summary>
    /// Runs a single deliberation turn asynchronously, allowing observers to see each juror's input.
    /// </summary>
    public async Task DeliberateNextTurnAsync()
    {
        if (!_deliberationInitialized)
            StartDeliberation();

        var jurors = AllAgents
            .Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror) && a.CanVote)
            .ToList();

        if (!jurors.Any())
        {
            var noJurorsMsg = new DeliberationEntry 
            { 
                Speaker = "Court", 
                Message = "No jurors seated for deliberation.",
                Timestamp = DateTime.Now
            };
            DeliberationLog.Add(noJurorsMsg);
            TranscriptOutput = "[DELIBERATION] No jurors seated.";
            return;
        }

        var candidates = jurors
            .Where(j => !_spokenJurorIds.Contains(j.AgentId))
            .Select(j => new { Agent = j, Strength = Math.Abs(j.VerdictLean - 0.5) })
            .OrderByDescending(x => x.Strength)
            .ToList();

        if (!candidates.Any())
        {
            var completeMsg = new DeliberationEntry 
            { 
                Speaker = "Court", 
                Message = "All jurors have had opportunity to speak. Deliberation continues...",
                Timestamp = DateTime.Now
            };
            DeliberationLog.Add(completeMsg);
            
            // Reset for another round
            _spokenJurorIds.Clear();
            candidates = jurors
                .Select(j => new { Agent = j, Strength = Math.Abs(j.VerdictLean - 0.5) })
                .OrderByDescending(x => x.Strength)
                .Take(3)
                .ToList();
        }

        var next = candidates.FirstOrDefault();
        if (next == null) return;

        _spokenJurorIds.Add(next.Agent.AgentId);
        _deliberationRound++;

        // Tie-break: treat exact/near-neutral as neutral so we don't bias the transcript toward plaintiff.
        const double neutralEps = 1e-9;
        string side = Math.Abs(next.Agent.VerdictLean - 0.5) <= neutralEps
            ? "neutral"
            : (next.Agent.VerdictLean > 0.5 ? "plaintiff" : "defense");

        var model = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == next.Agent.SelectedModel) 
                    ?? CurrentCase.AvailableModels.FirstOrDefault();

        string line;
        if (model != null)
        {
            string memories = string.Join("\n", next.Agent.TrialEvents.Select(e => $"- {e.Content}").TakeLast(5));
            string context = $"You are in the jury room. Your current lean is {next.Agent.VerdictLean:P0} toward {side}.\n" +
                             $"Your underlying bias score is {next.Agent.Bias:+0.00;-0.00;0.00} (positive favors plaintiff, negative favors defense).\n\n" +
                             "Your MISSION: Persuasively bring the other jurors to your position. State your reasoning clearly, " +
                             "interpreting the trial memories through the lens of your personal background and biases. " +
                             "However, you must not be dogmatic; listen to the logic used by others. If they present a reasoning " +
                             "of an exhibit that contradicts yours or offers a new perspective, allow yourself to be cognitively open " +
                             "to shifting your own stance. You are seeking a just consensus.\n\n" +
                             $"Your Recent Memories:\n{memories}\n\n";

            if (!string.IsNullOrEmpty(_lastDeliberationStatement))
            {
                context += $"The previous juror argued: \"{_lastDeliberationStatement}\"\n\n" +
                           "Address their reasoning directly. If you agree with their interpretation, reinforce it to pull the room further toward that side. " +
                           "If their logic conflicts with your bias or memories, politely point out the flaw and persuade the room toward your view.";
            }
            else
            {
                context += "You are the first to speak. Open the floor by stating your position and your strongest supporting exhibit.";
            }

            var request = new StatementRequest
            {
                Role = AgentRole.Juror,
                Agent = next.Agent,
                SpeakerName = next.Agent.Name,
                Context = context,
                Type = MessageType.Deliberation,
                CaseData = CurrentCase,
                Model = model,
                Transcript = BuildTranscriptSummary()
            };

            var response = await _agentInteraction.GenerateStatementAsync(request);
            if (response.Success && response.Message != null)
            {
                line = response.Message.Content;
            }
            else
            {
                // Fallback for model failure
                double leanPercent = next.Agent.VerdictLean * 100;
                line = $"I'm leaning toward the {side} side ({leanPercent:F0}%). We need to look closely at the evidence provided.";
            }
        }
        else
        {
            double leanPercent = next.Agent.VerdictLean * 100;
            line = $"{next.Agent.Name}: My view is leaning toward the {side} side based on the evidence ({leanPercent:F0}%).";
        }

        _lastDeliberationStatement = line;

        var entry = new DeliberationEntry 
        { 
            Speaker = next.Agent.Name, 
            Message = line,
            Timestamp = DateTime.Now,
            IsJuror = true
        };
        DeliberationLog.Add(entry);

        TranscriptOutput = $"{TranscriptOutput}\n\n{line}";
        BroadcastEvent(line, Enum.GetValues<AgentRole>().ToList());

        // Argument exchange: when this juror speaks, store a lightweight argument memory
        // for the other jurors so future turns can reference what was argued.
        string issue = InferIssueFromText(line);
        string argumentSummary = $"[JUROR ARGUMENT] {next.Agent.Name} argued: \"{line}\"";

        foreach (var juror in jurors.Where(j => j.AgentId != next.Agent.AgentId))
        {
            juror.RecordTrialEvent(argumentSummary, juror.Bias * 0.05);
        }

        // Apply conformity influence from this juror's statement (issue-weighted + heterogeneous strength)
        await ApplyDeliberationInfluence(next.Agent, jurors, issue);

        NotifyJuryUpdate();
    }

    /// <summary>
    /// Applies conformity influence when a juror speaks, influencing others' leanings.
    /// </summary>
    private async Task ApplyDeliberationInfluence(Agent speakingJuror, List<Agent> allJurors, string issue)
    {
        double speakerLean = speakingJuror.VerdictLean;
        double speakerConfidence = Math.Abs(speakerLean - 0.5) * 2.0; // 0.0 to 1.0 scale

        // Heterogeneous conformity:
        // - use juror.Bias magnitude as a proxy for conviction/independence strength.
        // - issue weighting: apply slightly different conformity for liability vs damages cues.
        double issueWeight = issue == "damages" ? 1.15 : 1.0;

        foreach (var juror in allJurors.Where(j => j.AgentId != speakingJuror.AgentId))
        {
            // Resistance: combo of inherent bias and current verdict conviction.
            double currentConviction = Math.Abs(juror.VerdictLean - 0.5) * 2.0;
            double independence = 1.0 - Math.Min(0.7, (Math.Abs(juror.Bias) + currentConviction) / 2.0);
            
            // Influence is higher if the speaker is confident and the recipient is open (high independence).
            double influenceFactor = 0.06 * issueWeight * independence * (0.5 + speakerConfidence * 0.5);

            // Consensus Pull: Move the recipient a percentage of the distance toward the speaker.
            double distance = speakerLean - juror.VerdictLean;
            juror.VerdictLean = Math.Clamp(juror.VerdictLean + (distance * influenceFactor), 0.0, 1.0);
        }

        await Task.CompletedTask;
    }

    private static string InferIssueFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "liability";

        var t = text.ToLowerInvariant();

        // Damages cues
        if (t.Contains("damages") || t.Contains("medical") || t.Contains("pain") || t.Contains("lost") ||
            t.Contains("settlement") || t.Contains("future") || t.Contains("wage") || t.Contains("salary"))
            return "damages";

        // Otherwise default to liability
        return "liability";
    }

    /// <summary>
    /// Runs the complete deliberation process, showing each juror's turn.
    /// </summary>
    public async Task RunFullDeliberationAsync()
    {
        StartDeliberation();
        
        // Limit rounds to prevent infinite loops, but allow enough for consensus
        int maxRounds = Jurors.Count * 3;
        
        for (int i = 0; i < maxRounds; i++)
        {
            await DeliberateNextTurnAsync();
            await Task.Delay(500); // Small delay for visibility

            if (IsConsensusReached())
            {
                DeliberationLog.Add(new DeliberationEntry
                {
                    Speaker = "Court",
                    Message = "Consensus has been reached. The jury is ready with a verdict.",
                    Timestamp = DateTime.Now
                });
                break;
            }
        }

        // Final verdict summary
        DeliberationLog.Add(new DeliberationEntry 
        { 
            Speaker = "Court", 
            Message = $"Deliberation complete. Final jury lean: {JuryLiabilityAverage:P0} toward {LikelyVerdict}.",
            Timestamp = DateTime.Now
        });
        
        TranscriptOutput = $"[DELIBERATION COMPLETE] {LikelyVerdict} - Liability Score: {JuryLiabilityAverage:P0}";
    }

    /// <summary>
    /// Checks if the jury has reached a functional consensus based on the trial mode.
    /// </summary>
    private bool IsConsensusReached()
    {
        var votingJurors = Jurors.Where(j => j.CanVote).ToList();
        if (!votingJurors.Any()) return false;

        // Strong lean thresholds
        bool favorsPlaintiff(Agent a) => a.VerdictLean > 0.55;
        bool favorsDefendant(Agent a) => a.VerdictLean < 0.45;

        if (CurrentCase.Mode == CaseMode.Criminal)
        {
            // Criminal requires unanimity (all on same side of 0.5 and not ambivalent)
            return votingJurors.All(favorsPlaintiff) || votingJurors.All(favorsDefendant);
        }
        else
        {
            // Civil requires a significant majority (e.g., 9 out of 12)
            int threshold = (int)Math.Ceiling(votingJurors.Count * 0.75);
            return votingJurors.Count(favorsPlaintiff) >= threshold || votingJurors.Count(favorsDefendant) >= threshold;
        }
    }

    // ------------------------------
    // Chat plumbing (existing)
    // ------------------------------

    public async void ProcessChatInputLine(string speaker, string content)
    {
        if (string.IsNullOrEmpty(content)) return;

        TranscriptOutput = string.IsNullOrWhiteSpace(speaker) ? content : $"{speaker}: {content}";

        var roles = Enum.GetValues<AgentRole>().ToList();
        BroadcastEvent(TranscriptOutput, roles);

        await TriggerAgentResponses(speaker, content);
    }

    private async Task TriggerAgentResponses(string speaker, string content)
    {
        try
        {
            var stage = CurrentCase.CurrentDebateStage;
            var transcript = BuildTranscriptSummary();

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

                        var model = CurrentCase.AvailableModels.FirstOrDefault();
                        foreach (var agent in AllAgents.Where(a => a.IsOccupied && a.Role != AgentRole.Reporter))
                        {
                            AIModelConfiguration? agentModel = null;
                            if (!string.IsNullOrEmpty(agent.SelectedModel))
                                agentModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);
                            agentModel ??= model;

                            if (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror)
                            {
                                string memory = agentModel != null
                                    ? await _agentInteraction.InternalizeCourtRecordAsync(agent, record, agentModel)
                                    : record;
                                agent.RecordTrialEvent(memory, agent.Bias * 0.1);
                            }
                            else
                            {
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
                    if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff"))
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney", "Respond to the opening statement.", transcript);
                    else if (speaker.Contains("Defense"))
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor", "Respond to the opening statement.", transcript);
                    break;

                case CourtPhase.WitnessTestimony:
                case CourtPhase.CrossExamination:
                    await GenerateAgentResponse(AgentRole.Judge, "Judge", $"Acknowledge the testimony and move the trial forward. Speaker: {speaker}", transcript);
                    break;

                case CourtPhase.ClosingArguments:
                    if (speaker.Contains("Prosecutor") || speaker.Contains("Plaintiff"))
                        await GenerateAgentResponse(AgentRole.Lawyer, "Defense Attorney", "Respond to the closing argument.", transcript);
                    else if (speaker.Contains("Defense"))
                        await GenerateAgentResponse(AgentRole.Lawyer, "Prosecutor", "Respond to the closing argument.", transcript);
                    break;

                default:
                    await GenerateAgentResponse(AgentRole.Judge, "Judge", $"Acknowledge the statement from {speaker}.", transcript);
                    break;
            }
        }
        catch (Exception ex)
        {
            TranscriptOutput += $"\n[AI RESPONSE ERROR] {ex.Message}";
        }
    }

    private async Task GenerateAgentResponse(AgentRole role, string speakerName, string context, List<AgentMessage> transcript)
    {
        var agent = AllAgents.FirstOrDefault(a => a.Role == role && a.IsOccupied);
        if (agent == null) return;

        AIModelConfiguration? modelConfig = null;
        if (!string.IsNullOrEmpty(agent.SelectedModel))
            modelConfig = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);
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

    public async Task LoadTranscript(string filePath)
    {
        if (_transcriptService == null)
        {
            TranscriptOutput = "[ERROR] Transcript service not available.";
            return;
        }

        foreach (var (speaker, content) in _transcriptService.LoadTranscript(filePath))
        {
            ProcessChatInputLine(speaker, content);
            await Task.Delay(100);
        }
    }

    public void SaveCase(string filePath) => _caseService.SaveCase(filePath, CurrentCase, AllAgents);

    public void LoadCase(string filePath)
    {
        var loadedCase = _caseService.LoadCase(filePath);
        if (loadedCase == null) return;

        loadedCase.EnsureCollectionsInitialized();
        CurrentCase = loadedCase;
        RefreshExhibits();
        _courtroomManager.InitializeCourtroom(JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery, loadedCase);

        var totalEvidenceCount = CurrentCase.Evidence?.Count ?? 0;
        var loadedReporter = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
        if (loadedReporter != null) loadedReporter.DocumentCount = totalEvidenceCount;

        OnPropertyChanged(nameof(CurrentCase));
        NotifyJuryUpdate();
        TranscriptOutput = $"[CASE LOADED] Case '{loadedCase.CaseName}' loaded successfully.";
        UpdateWindowTitle();
    }

    public async Task ExtractEntitiesFromTranscriptAsync()
    {
        if (_transcriptService == null)
        {
            TranscriptOutput = "[ERROR] Transcript service not available.";
            return;
        }

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
            juror.TrialEvents.Clear();
            juror.ConsideredDamages = 0;
            juror.VerdictLean = 0.5;

            AIModelConfiguration? jurorModel = CurrentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == juror.SelectedModel)
                                               ?? defaultModel;

            foreach (string exhibitSummary in reporter.ExhibitLog)
            {
                string reaction = jurorModel != null
                    ? await _agentInteraction.InternalizeCourtRecordAsync(juror, exhibitSummary, jurorModel)
                    : exhibitSummary;
                juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
            }

            jurorCount++;
        }

        foreach (var doc in CurrentCase.Evidence)
            _juryCalc.ApplyEvidenceInfluence(AllAgents, doc, CurrentCase.Mode);

        NotifyJuryUpdate();
        TranscriptOutput = $"[REEVALUATE] {jurorCount} jurors re-evaluated {reporter.ExhibitLog.Count} exhibits with current psychology.";
    }

    public void GeneratePDFReport(string filePath)
    {
        _caseService.GenerateReport(filePath, CurrentCase, AllAgents);
        TranscriptOutput = $"[REPORT] PDF case report generated at {filePath}";
    }

    public void ApplyDynamicRoleAssignment()
    {
        var assignmentService = new AgentAssignmentService();
        var plan = assignmentService.AnalyzeCase(CurrentCase);

        var defaultAgents = assignmentService.GenerateDefaultAgents(CurrentCase);

        foreach (var agent in defaultAgents)
        {
            if (agent.Role == AgentRole.Judge)
            {
                var judgeSlot = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Judge && !a.IsOccupied);
                if (judgeSlot != null) agent.CopyTo(judgeSlot);
            }
            else if (agent.Role == AgentRole.Witness)
            {
                var witnessSlot = JudgeArea.FirstOrDefault(a => a.Role == AgentRole.Witness && !a.IsOccupied);
                if (witnessSlot != null) agent.CopyTo(witnessSlot);
            }
        }

        assignmentService.ApplyAssignmentPlan(plan, JudgeArea, Jurors, DefenseTeam, ProsecutionTeam, Gallery);

        TranscriptOutput = $"[ROLE ASSIGNMENT] {plan.StrategyDescription} " +
                            $"Recommended: {plan.RecommendedProsecutionAttorneys} prosecutors, " +
                            $"{plan.RecommendedDefenseAttorneys} defense attorneys, " +
                            $"{plan.RecommendedExperts.Count} experts. " +
                            $"Complexity: {plan.ComplexityRating}/10.";
        BroadcastEvent($"Dynamic role assignment completed: {plan.StrategyDescription}",
            Enum.GetValues<AgentRole>().ToList());
    }
}
