using System;
using System.Collections.ObjectModel;
using Verdict.Services;

namespace Verdict.Models;

/// <summary>
/// Represents an individual agent in the courtroom simulation (Juror, Judge, Lawyer, etc.).
/// 
/// Factored architecture — Agent delegates to specialized sub-objects:
///   <see cref="Demographics"/>  — age, gender, race, occupation, education, income, etc.
///   <see cref="BiasWeights"/>   — per-category bias dimension weight overrides
///   <see cref="ModelOverrides"/> — per-agent model, temperature, and token overrides
/// 
/// All original flat properties (Age, Gender, SelectedModel, etc.) are preserved
/// for XAML binding and backward compatibility. They delegate reads/writes through
/// the sub-objects listed above.
/// 
/// Memory decay / fuzzification logic lives in <see cref="MemoryDecayService"/>.
/// </summary>
public class Agent : ObservableObject
{
    // ──────────────────────────────────────────────
    //  Composed sub-objects (new factored API)
    // ──────────────────────────────────────────────
    private AgentDemographics _demographics = new();
    private BiasDimensionWeights _biasWeights = new();
    private AgentModelOverrides _modelOverrides = new();

    /// <summary>Opinion history for tracking verdict lean changes over time.</summary>
    public ObservableCollection<OpinionSnapshot> OpinionHistory { get; set; } = new();

    /// <summary>
    /// Records the current verdict lean as a snapshot for opinion-over-time tracking.
    /// </summary>
    public void RecordOpinionSnapshot(string phase, string cause)
    {
        OpinionHistory.Add(new OpinionSnapshot
        {
            Round = OpinionHistory.Count,
            Phase = phase,
            VerdictLean = VerdictLean,
            Cause = cause,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// All demographic and background characteristics for this agent.
    /// </summary>
    public AgentDemographics Demographics
    {
        get => _demographics;
        set => SetProperty(ref _demographics, value);
    }

    /// <summary>
    /// Per-category bias dimension weight overrides.
    /// Null values inherit global defaults from ModelWeightsWindow.
    /// </summary>
    public BiasDimensionWeights BiasWeights
    {
        get => _biasWeights;
        set => SetProperty(ref _biasWeights, value);
    }

    /// <summary>
    /// Per-agent model, temperature, and token-limit overrides.
    /// </summary>
    public AgentModelOverrides ModelOverrides
    {
        get => _modelOverrides;
        set => SetProperty(ref _modelOverrides, value);
    }

    // ──────────────────────────────────────────────
    //  Backing fields for Agent-owned properties
    // ──────────────────────────────────────────────
    private Guid _agentId = Guid.NewGuid();
    private string _name = "New Agent";
    private AgentRole _role;
    private double _sentiment = 0.5;
    private string _valuation = "$0";
    private double _bias = 0.0;
    private double _verdictLean = 0.5;
    private bool _isOccupied = false;
    private string _systemPrompt = string.Empty;
    private string _profile = string.Empty;
    private ObservableCollection<MemoryEntry> _memories = new();
    private ObservableCollection<MemoryEntry> _trialEvents = new();
    private string _judicialRulings = string.Empty;
    private string _writtenOpinions = string.Empty;
    private string _judicialTemperament = string.Empty;
    private double _settlementAuthority = 0.0;
    private double _riskPerception = 0.5;
    private string _roleSystemPrompt = string.Empty;

    // ── Communication constraints ──
    private string _communicationTeam = "Neutral";
    private string _observerType = string.Empty;
    private string _connectedAttorneyName = string.Empty;
    private bool _canCommunicateWithJury = false;
    private bool _isExposurePlanner = false;
    private bool _onlyAnswerWhenAsked = false;

    private double _consideredDamages = 0;
    private int _documentCount = 0;

    // ──────────────────────────────────────────────
    //  Core identity
    // ──────────────────────────────────────────────

    /// <summary>Stable identity for persistence across save/load.</summary>
    public Guid AgentId
    {
        get => _agentId;
        set => SetProperty(ref _agentId, value);
    }

    /// <summary>The display name of the agent.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Gets the initials of the agent from their name.</summary>
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_name)) return "+";

            var parts = _name.Split(new char[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "+";

            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();

            var firstInitial = parts[0].Length > 0 ? parts[0][0].ToString() : "";
            var lastInitial = parts[parts.Length - 1].Length > 0 ? parts[parts.Length - 1][0].ToString() : "";
            return (firstInitial + lastInitial).ToUpper();
        }
    }

    /// <summary>The functional role of the agent in the trial.</summary>
    public AgentRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    /// <summary>The system instructions that guide the LLM's behavior for this agent.</summary>
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    /// <summary>A detailed background or persona for the agent.</summary>
    public string Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    /// <summary>Indicates if this agent slot is filled with a specific persona.</summary>
    public bool IsOccupied
    {
        get => _isOccupied;
        set => SetProperty(ref _isOccupied, value);
    }

    // ──────────────────────────────────────────────
    //  Demographics (delegated to AgentDemographics)
    // ──────────────────────────────────────────────

    public string Gender
    {
        get => _demographics.Gender;
        set => _demographics.Gender = value;
    }

    public int Age
    {
        get => _demographics.Age;
        set => _demographics.Age = value;
    }

    public string Race
    {
        get => _demographics.Race;
        set => _demographics.Race = value;
    }

    public string Occupation
    {
        get => _demographics.Occupation;
        set => _demographics.Occupation = value;
    }

    public string EducationLevel
    {
        get => _demographics.EducationLevel;
        set => _demographics.EducationLevel = value;
    }

    public string IncomeLevel
    {
        get => _demographics.IncomeLevel;
        set => _demographics.IncomeLevel = value;
    }

    public string MediaConsumption
    {
        get => _demographics.MediaConsumption;
        set => _demographics.MediaConsumption = value;
    }

    public string ConsumerSegment
    {
        get => _demographics.ConsumerSegment;
        set => _demographics.ConsumerSegment = value;
    }

    public string MaritalStatus
    {
        get => _demographics.MaritalStatus;
        set => _demographics.MaritalStatus = value;
    }

    public string ParentalStatus
    {
        get => _demographics.ParentalStatus;
        set => _demographics.ParentalStatus = value;
    }

    public string Hobbies
    {
        get => _demographics.Hobbies;
        set => _demographics.Hobbies = value;
    }

    public string ReligiousAffiliation
    {
        get => _demographics.ReligiousAffiliation;
        set => _demographics.ReligiousAffiliation = value;
    }

    public string PoliticalAffiliation
    {
        get => _demographics.PoliticalAffiliation;
        set => _demographics.PoliticalAffiliation = value;
    }

    public string ZipCode
    {
        get => _demographics.ZipCode;
        set => _demographics.ZipCode = value;
    }

    public string ViewingCharacteristics
    {
        get => _demographics.ViewingCharacteristics;
        set => _demographics.ViewingCharacteristics = value;
    }

    /// <summary>Current mental/physical state during the trial (e.g. Bored, Focused, Skeptical).</summary>
    public string CurrentStatus
    {
        get => _demographics.CurrentStatus;
        set => _demographics.CurrentStatus = value;
    }

    /// <summary>Specialized training or expertise (e.g., Medical, Engineering, Legal).</summary>
    public string SpecializedKnowledge
    {
        get => _demographics.SpecializedKnowledge;
        set => _demographics.SpecializedKnowledge = value;
    }

    // ──────────────────────────────────────────────
    //  Simulation state
    // ──────────────────────────────────────────────

    /// <summary>Current emotional state or sentiment (0.0 to 1.0).</summary>
    public double Sentiment
    {
        get => _sentiment;
        set => SetProperty(ref _sentiment, value);
    }

    /// <summary>Current estimated valuation of the case from this agent's perspective.</summary>
    public string Valuation
    {
        get => _valuation;
        set => SetProperty(ref _valuation, value);
    }

    /// <summary>Inherent bias that affects how the agent processes information (-1.0 to 1.0).</summary>
    public double Bias
    {
        get => _bias;
        set => SetProperty(ref _bias, value);
    }

    /// <summary>Current lean towards a verdict (0.0 for Defendant, 1.0 for Prosecution/Plaintiff).</summary>
    public double VerdictLean
    {
        get => _verdictLean;
        set
        {
            if (HasOpinion)
                SetProperty(ref _verdictLean, value);
        }
    }

    /// <summary>The damages amount this agent personally considers appropriate.</summary>
    public double ConsideredDamages
    {
        get => _consideredDamages;
        set => SetProperty(ref _consideredDamages, value);
    }

    /// <summary>Number of documents processed by this agent (used for Clerk/Reporter display).</summary>
    public int DocumentCount
    {
        get => _documentCount;
        set => SetProperty(ref _documentCount, value);
    }

    // ──────────────────────────────────────────────
    //  Role capability checks
    // ──────────────────────────────────────────────

    public bool CanVote => Role == AgentRole.Juror || Role == AgentRole.AlternateJuror;
    public bool HasOpinion => (CanVote || Role == AgentRole.Judge) && Role != AgentRole.Reporter;
    public bool CanDeliberate => Role == AgentRole.Juror || Role == AgentRole.AlternateJuror;
    public bool IsClient => Role == AgentRole.Client;

    // ──────────────────────────────────────────────
    //  Collections
    // ──────────────────────────────────────────────

    /// <summary>Background memories, identity traits, and life experiences.</summary>
    public ObservableCollection<MemoryEntry> Memories
    {
        get => _memories;
        set => SetProperty(ref _memories, value);
    }

    /// <summary>Specific observations and evidence recorded during this trial.</summary>
    public ObservableCollection<MemoryEntry> TrialEvents
    {
        get => _trialEvents;
        set => SetProperty(ref _trialEvents, value);
    }

    /// <summary>Named bias factors with configurable weights (-1.0 to 1.0 each).</summary>
    public ObservableCollection<BiasFactor> BiasFactors
    {
        get
        {
            if (_biasFactors == null)
                _biasFactors = new ObservableCollection<BiasFactor>();
            return _biasFactors;
        }
        set => SetProperty(ref _biasFactors, value);
    }
    private ObservableCollection<BiasFactor>? _biasFactors;

    /// <summary>Collection of opinions this juror holds about other jurors.</summary>
    public ObservableCollection<JurorOpinion> Opinions { get; } = new();

    /// <summary>Running log of reporter exhibit summaries this agent has heard.</summary>
    public ObservableCollection<string> ExhibitLog { get; } = new();

    // ──────────────────────────────────────────────
    //  Judge-specific properties
    // ──────────────────────────────────────────────

    public string JudicialRulings
    {
        get => _judicialRulings;
        set => SetProperty(ref _judicialRulings, value);
    }

    public string WrittenOpinions
    {
        get => _writtenOpinions;
        set => SetProperty(ref _writtenOpinions, value);
    }

    public string JudicialTemperament
    {
        get => _judicialTemperament;
        set => SetProperty(ref _judicialTemperament, value);
    }

    // ──────────────────────────────────────────────
    //  Client-specific properties
    // ──────────────────────────────────────────────

    public double SettlementAuthority
    {
        get => _settlementAuthority;
        set => SetProperty(ref _settlementAuthority, value);
    }

    public double RiskPerception
    {
        get => _riskPerception;
        set => SetProperty(ref _riskPerception, value);
    }

    // ──────────────────────────────────────────────
    //  Bias dimension weights (delegated to BiasWeights)
    // ──────────────────────────────────────────────

    public double? AgeBiasWeight
    {
        get => _biasWeights.AgeBiasWeight;
        set => _biasWeights.AgeBiasWeight = value;
    }

    public double? GenderBiasWeight
    {
        get => _biasWeights.GenderBiasWeight;
        set => _biasWeights.GenderBiasWeight = value;
    }

    public double? EducationBiasWeight
    {
        get => _biasWeights.EducationBiasWeight;
        set => _biasWeights.EducationBiasWeight = value;
    }

    public double? IncomeBiasWeight
    {
        get => _biasWeights.IncomeBiasWeight;
        set => _biasWeights.IncomeBiasWeight = value;
    }

    public double? PoliticalBiasWeight
    {
        get => _biasWeights.PoliticalBiasWeight;
        set => _biasWeights.PoliticalBiasWeight = value;
    }

    public double? EthnicityBiasWeight
    {
        get => _biasWeights.EthnicityBiasWeight;
        set => _biasWeights.EthnicityBiasWeight = value;
    }

    public double? ReligionBiasWeight
    {
        get => _biasWeights.ReligionBiasWeight;
        set => _biasWeights.ReligionBiasWeight = value;
    }

    public double? JurorExperienceWeight
    {
        get => _biasWeights.JurorExperienceWeight;
        set => _biasWeights.JurorExperienceWeight = value;
    }

    public double? LegalKnowledgeWeight
    {
        get => _biasWeights.LegalKnowledgeWeight;
        set => _biasWeights.LegalKnowledgeWeight = value;
    }

    public double? ProfessionalBackgroundWeight
    {
        get => _biasWeights.ProfessionalBackgroundWeight;
        set => _biasWeights.ProfessionalBackgroundWeight = value;
    }

    public double? CommunityTiesWeight
    {
        get => _biasWeights.CommunityTiesWeight;
        set => _biasWeights.CommunityTiesWeight = value;
    }

    // ──────────────────────────────────────────────
    //  Model overrides (delegated to ModelOverrides)
    // ──────────────────────────────────────────────

    public string SelectedModel
    {
        get => _modelOverrides.SelectedModel;
        set => _modelOverrides.SelectedModel = value;
    }

    public double? ModelTemperatureOverride
    {
        get => _modelOverrides.ModelTemperatureOverride;
        set => _modelOverrides.ModelTemperatureOverride = value;
    }

    public int? ModelMaxTokensOverride
    {
        get => _modelOverrides.ModelMaxTokensOverride;
        set => _modelOverrides.ModelMaxTokensOverride = value;
    }

    // ──────────────────────────────────────────────
    //  Role system prompt override
    // ──────────────────────────────────────────────

    /// <summary>
    /// Overrides the built-in role system prompt for this agent.
    /// Empty string means use the default from AgentInteractionService.GetDefaultSystemPrompt.
    /// </summary>
    public string RoleSystemPrompt
    {
        get => _roleSystemPrompt;
        set => SetProperty(ref _roleSystemPrompt, value);
    }

    // ──────────────────────────────────────────────
    //  Communication constraints
    // ──────────────────────────────────────────────

    public string CommunicationTeam
    {
        get => _communicationTeam;
        set => SetProperty(ref _communicationTeam, value ?? string.Empty);
    }

    public string ObserverType
    {
        get => _observerType;
        set => SetProperty(ref _observerType, value);
    }

    public string ConnectedAttorneyName
    {
        get => _connectedAttorneyName;
        set => SetProperty(ref _connectedAttorneyName, value);
    }

    public bool CanCommunicateWithJury
    {
        get => _canCommunicateWithJury;
        set => SetProperty(ref _canCommunicateWithJury, value);
    }

    public bool IsExposurePlanner
    {
        get => _isExposurePlanner;
        set => SetProperty(ref _isExposurePlanner, value);
    }

    public bool OnlyAnswerWhenAsked
    {
        get => _onlyAnswerWhenAsked;
        set => SetProperty(ref _onlyAnswerWhenAsked, value);
    }

    // ──────────────────────────────────────────────
    //  Memory operations (delegated to MemoryDecayService)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Decays the strength of all memories and fuzzifies their content over time.
    /// Delegates to <see cref="MemoryDecayService"/>.
    /// </summary>
    public void DecayMemories(double factor = 0.95)
    {
        MemoryDecayService.DecayMemories(Memories, TrialEvents, factor);
    }

    /// <summary>Adds a new trial observation or reinforces an existing one.</summary>
    public void RecordTrialEvent(string content, double biasInfluence)
    {
        MemoryDecayService.RecordTrialEvent(TrialEvents, content, biasInfluence);
    }

    /// <summary>Adds a new background memory or reinforces an existing one.</summary>
    public void ReinforceMemory(string content, double biasInfluence)
    {
        MemoryDecayService.ReinforceMemory(Memories, content, biasInfluence);
    }

    /// <summary>
    /// Scans existing memories for content related to the new statement.
    /// If the statement references exhibits, any existing memory mentioning
    /// the same exhibit gets a strength boost against decay.
    /// </summary>
    public void ReinforceRelatedMemories(string newStatement)
    {
        MemoryDecayService.ReinforceRelatedMemories(Memories, TrialEvents, newStatement);
    }

    // ──────────────────────────────────────────────
    //  Behavioral methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Updates the verdict lean based on incoming influence and existing bias.
    /// Reporters do not form opinions and thus cannot update their verdict lean.
    /// </summary>
    public void UpdateVerdictLean(double influence)
    {
        if (!HasOpinion) return;
        double shift = influence * (1.0 + Bias);
        VerdictLean = Math.Clamp(VerdictLean + shift, 0.0, 1.0);
    }

    /// <summary>
    /// Performs a simulated sentiment analysis on courtroom content and updates emotional state.
    /// Reporters remain neutral and do not have their sentiment affected.
    /// </summary>
    public void AnalyzeSentiment(string content)
    {
        if (Role == AgentRole.Reporter) return;

        double shift = 0;
        string lower = content.ToLower();

        if (lower.Contains("objection") || lower.Contains("overruled")) shift = -0.02;
        else if (lower.Contains("sustained")) shift = 0.02;
        else if (lower.Contains("lied") || lower.Contains("false") || lower.Contains("inconsistent")) shift = -0.1;
        else if (lower.Contains("expert") || lower.Contains("dna") || lower.Contains("fact")) shift = 0.05;
        else if (lower.Contains("admitted") || lower.Contains("evidence")) shift = 0.03;

        Sentiment = Math.Clamp(Sentiment + (shift * (1.0 + Math.Abs(Bias))), 0.0, 1.0);
    }

    /// <summary>
    /// Updates the Valuation display string from the current ConsideredDamages value.
    /// Rounds to nearest thousand and formats as "$XK" or "$X.XM" for large amounts.
    /// </summary>
    public void UpdateValuationFromDamages()
    {
        double amount = ConsideredDamages;
        if (amount <= 0)
        {
            Valuation = "$0";
            return;
        }

        if (amount >= 1_000_000)
        {
            double millions = amount / 1_000_000.0;
            Valuation = $"${millions:F1}M";
        }
        else
        {
            double rounded = Math.Round(amount / 1000.0) * 1000.0;
            double inThousands = rounded / 1000.0;
            Valuation = $"${inThousands:F0}K";
        }
    }

    // ──────────────────────────────────────────────
    //  CopyTo – uses new sub-object CopyFrom methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Copies all properties from another agent into this one.
    /// Used for role assignment and slot population.
    /// Uses the new factored sub-object CopyFrom methods for cleaner transfer.
    /// </summary>
    public void CopyTo(Agent target)
    {
        // Core identity
        target.AgentId = this.AgentId;
        target.Name = this.Name;
        target.Role = this.Role;
        target.SystemPrompt = this.SystemPrompt;
        target.Profile = this.Profile;
        target.IsOccupied = true;

        // Simulation state
        target.Bias = this.Bias;
        target.VerdictLean = this.VerdictLean;
        target.Sentiment = this.Sentiment;
        target.ConsideredDamages = this.ConsideredDamages;

        // Composed sub-objects (uses CopyFrom for bulk transfer)
        target.Demographics.CopyFrom(this.Demographics);
        target.BiasWeights.CopyFrom(this.BiasWeights);
        target.ModelOverrides.CopyFrom(this.ModelOverrides);

        // Role-specific
        target.JudicialRulings = this.JudicialRulings;
        target.WrittenOpinions = this.WrittenOpinions;
        target.JudicialTemperament = this.JudicialTemperament;
        target.SettlementAuthority = this.SettlementAuthority;
        target.RiskPerception = this.RiskPerception;
        target.RoleSystemPrompt = this.RoleSystemPrompt;

        // Communication constraints
        target.ObserverType = this.ObserverType;
        target.ConnectedAttorneyName = this.ConnectedAttorneyName;
        target.CanCommunicateWithJury = this.CanCommunicateWithJury;
        target.IsExposurePlanner = this.IsExposurePlanner;
        target.CommunicationTeam = this.CommunicationTeam;
        target.OnlyAnswerWhenAsked = this.OnlyAnswerWhenAsked;

        // Collections
        foreach (var entry in ExhibitLog)
            target.ExhibitLog.Add(entry);
        foreach (var bf in BiasFactors)
            target.BiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
    }
}
