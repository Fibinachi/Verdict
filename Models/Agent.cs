using System;
using System.Collections.ObjectModel;
using System.Linq;
namespace Verdict.Models;

/// <summary>
/// Represents an individual agent in the courtroom simulation (Juror, Judge, Lawyer, etc.).
/// </summary>
public class Agent : ObservableObject
{
    private Guid _agentId = Guid.NewGuid();
    private string _name = "New Agent";
    private AgentRole _role;
    private double _sentiment = 0.5;
    private string _valuation = "$0";
    private double _bias = 0.0;
    private double _verdictLean = 0.5; // 0.0 for Defendant, 1.0 for Prosecution/Plaintiff
    private bool _isOccupied = false;
    private string _systemPrompt = string.Empty;
    private string _profile = string.Empty;
    private string _politicalAffiliation = "Independent";
    private string _zipCode = "00000";
    private string _viewingCharacteristics = string.Empty;
    private string _gender = "Unknown";
    private int _age = 18;
    private string _race = "Unknown";
    private string _occupation = "Unemployed";
    private string _educationLevel = "High School";
    private string _incomeLevel = "Middle Class";
    private string _mediaConsumption = "Mainstream News";
    private string _consumerSegment = "General";
    private string _maritalStatus = "Single";
    private string _parentalStatus = "No Children";
    private string _hobbies = string.Empty;
    private string _religiousAffiliation = "Non-religious";
    private string _currentStatus = "Attentive";
    private string _specializedKnowledge = "None (General Public)";
    private ObservableCollection<MemoryEntry> _memories = new();
    private ObservableCollection<MemoryEntry> _trialEvents = new();
    private ObservableCollection<MemoryEntry> _chatEvents = new();
    private ObservableCollection<EvidencePerception> _perceivedEvidence = new();

    private string _judicialRulings = string.Empty;
    private string _writtenOpinions = string.Empty;
    private string _judicialTemperament = string.Empty;
    private double _settlementAuthority = 0.0;
    private double _riskPerception = 0.5; // 0.0 to 1.0 (Low to High Risk)
    private string _selectedModel = string.Empty; // The selected model for this agent
    private double? _modelTemperatureOverride; // null = use global default
    private int? _modelMaxTokensOverride; // null = use global default

    // --- Per-agent role system prompt (empty = use built-in default) ---
    private string _roleSystemPrompt = string.Empty;

    // --- Per-agent bias dimension weights (null = use global default from ModelWeightsWindow) ---
    private double? _ageBiasWeight;
    private double? _genderBiasWeight;
    private double? _educationBiasWeight;
    private double? _incomeBiasWeight;
    private double? _politicalBiasWeight;
    private double? _ethnicityBiasWeight;
    private double? _religionBiasWeight;
    private double? _jurorExperienceWeight;
    private double? _legalKnowledgeWeight;
    private double? _professionalBackgroundWeight;
    private double? _communityTiesWeight;

    // --- Litigation communication constraints / affiliations ---
    // "Team" determines which side the agent belongs to (Plaintiff/Defendant/Neutral).
    // Used to restrict who can communicate with whom (e.g., jurors can't message lawyers).
    private string _communicationTeam = "Neutral"; // "Plaintiff", "Defendant", or "Neutral"

    // For observers (e.g., insurance adjuster) this stores which attorney they are attached to.
    // Also used for restriction: observer can only communicate with their connected attorney (and judge).
    private string _observerType = string.Empty;
    private string _connectedAttorneyName = string.Empty;
    private bool _canCommunicateWithJury = false;
    private bool _isExposurePlanner = false;

    // Legacy/compat: keep empty unless explicitly set.
    private bool _onlyAnswerWhenAsked = false;



    /// <summary>
    /// Stable identity for persistence across save/load.
    /// </summary>
    public Guid AgentId
    {
        get => _agentId;
        set => SetProperty(ref _agentId, value);
    }

    /// <summary>
    /// The display name of the agent.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets the initials of the agent from their name.
    /// </summary>
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_name))
                return "+";

            var parts = _name.Split(new char[] { ' ', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "+";

            if (parts.Length == 1)
            {
                // Return first two characters of the single name if it has at least 2 chars
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            }

            // Take first character of first and last part
            var firstInitial = parts[0].Length > 0 ? parts[0][0].ToString() : "";
            var lastInitial = parts[parts.Length - 1].Length > 0 ? parts[parts.Length - 1][0].ToString() : "";
            
            return (firstInitial + lastInitial).ToUpper();
        }
    }

    public string Gender
    {
        get => _gender;
        set => SetProperty(ref _gender, value);
    }

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public string Race
    {
        get => _race;
        set => SetProperty(ref _race, value);
    }

    public string Occupation
    {
        get => _occupation;
        set => SetProperty(ref _occupation, value);
    }

    public string EducationLevel
    {
        get => _educationLevel;
        set => SetProperty(ref _educationLevel, value);
    }

    public string IncomeLevel
    {
        get => _incomeLevel;
        set => SetProperty(ref _incomeLevel, value);
    }

    public string MediaConsumption
    {
        get => _mediaConsumption;
        set => SetProperty(ref _mediaConsumption, value);
    }

    public string ConsumerSegment
    {
        get => _consumerSegment;
        set => SetProperty(ref _consumerSegment, value);
    }

    public string MaritalStatus
    {
        get => _maritalStatus;
        set => SetProperty(ref _maritalStatus, value);
    }

    public string ParentalStatus
    {
        get => _parentalStatus;
        set => SetProperty(ref _parentalStatus, value);
    }

    public string Hobbies
    {
        get => _hobbies;
        set => SetProperty(ref _hobbies, value);
    }

    public string ReligiousAffiliation
    {
        get => _religiousAffiliation;
        set => SetProperty(ref _religiousAffiliation, value);
    }

    /// <summary>
    /// Current mental/physical state during the trial (e.g. Bored, Focused, Skeptical).
    /// </summary>
    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    /// <summary>
    /// Specialized training or expertise (e.g., Medical, Engineering, Legal). 
    /// Defaults to "None (General Public)" for average understanding.
    /// </summary>
    public string SpecializedKnowledge
    {
        get => _specializedKnowledge;
        set => SetProperty(ref _specializedKnowledge, value);
    }

    /// <summary>
    /// Background memories, identity traits, and life experiences.
    /// </summary>
    public ObservableCollection<MemoryEntry> Memories
    {
        get => _memories;
        set => SetProperty(ref _memories, value);
    }

    /// <summary>
    /// Named bias factors with configurable weights (-1.0 to 1.0 each).
    /// Negative weights favor defense, positive favor plaintiff.
    /// </summary>
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

    /// <summary>
    /// Collection of opinions this juror holds about other jurors.
    /// Each opinion reflects the juror's personality traits and bias influences.
    /// </summary>
    public ObservableCollection<JurorOpinion> Opinions { get; } = new();

    /// <summary>
    /// Specific observations and evidence recorded during this trial.
    /// </summary>
    public ObservableCollection<MemoryEntry> TrialEvents
    {
        get => _trialEvents;
        set => SetProperty(ref _trialEvents, value);
    }

    /// <summary>
    /// Chat-only transcript entries. These are for analysis/conversation and
    /// must not affect juror bias/lean or trial internalization.
    /// </summary>
    public ObservableCollection<MemoryEntry> ChatEvents
    {
        get => _chatEvents;
        set => SetProperty(ref _chatEvents, value);
    }

    /// <summary>
    /// Per-agent determination of evidence strength and trust.
    /// </summary>
    public ObservableCollection<EvidencePerception> PerceivedEvidence
    {
        get => _perceivedEvidence;
        set => SetProperty(ref _perceivedEvidence, value);
    }


    /// <summary>
    /// For Judge roles: past rulings and legal precedents established by this judge.
    /// </summary>
    public string JudicialRulings
    {
        get => _judicialRulings;
        set => SetProperty(ref _judicialRulings, value);
    }

    /// <summary>
    /// For Judge roles: excerpt from written opinions to capture legal philosophy.
    /// </summary>
    public string WrittenOpinions
    {
        get => _writtenOpinions;
        set => SetProperty(ref _writtenOpinions, value);
    }

    /// <summary>
    /// For Judge roles: description of judicial temperament and behavior in trial.
    /// </summary>
    public string JudicialTemperament
    {
        get => _judicialTemperament;
        set => SetProperty(ref _judicialTemperament, value);
    }

    /// <summary>
    /// For Client roles (e.g. Insurance): The maximum dollar amount authorized for settlement.
    /// </summary>
    public double SettlementAuthority
    {
        get => _settlementAuthority;
        set => SetProperty(ref _settlementAuthority, value);
    }

    /// <summary>
    /// For Client roles: How the agent perceives the risk of trial (0.0 Low, 1.0 High).
    /// </summary>
    public double RiskPerception
    {
        get => _riskPerception;
        set => SetProperty(ref _riskPerception, value);
    }

    public string PoliticalAffiliation
    {
        get => _politicalAffiliation;
        set => SetProperty(ref _politicalAffiliation, value);
    }

    public string ZipCode
    {
        get => _zipCode;
        set => SetProperty(ref _zipCode, value);
    }

    public string ViewingCharacteristics
    {
        get => _viewingCharacteristics;
        set => SetProperty(ref _viewingCharacteristics, value);
    }

    /// <summary>
    /// The system instructions that guide the LLM's behavior for this agent.
    /// </summary>
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    /// <summary>
    /// A detailed background or persona for the agent.
    /// </summary>
    public string Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    /// <summary>
    /// The functional role of the agent in the trial.
    /// </summary>
    public AgentRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    /// <summary>
    /// Current emotional state or sentiment (0.0 to 1.0).
    /// </summary>
    public double Sentiment
    {
        get => _sentiment;
        set => SetProperty(ref _sentiment, value);
    }

    /// <summary>
    /// Current estimated valuation of the case from this agent's perspective.
    /// </summary>
    public string Valuation
    {
        get => _valuation;
        set => SetProperty(ref _valuation, value);
    }

    /// <summary>
    /// Inherent bias that affects how the agent processes information (-1.0 to 1.0).
    /// </summary>
    public double Bias
    {
        get => _bias;
        set => SetProperty(ref _bias, value);
    }

    /// <summary>
    /// Current lean towards a verdict (0.0 for Defendant, 1.0 for Prosecution/Plaintiff).
    /// Only applicable if <see cref="HasOpinion"/> is true.
    /// </summary>
    public double VerdictLean
    {
        get => _verdictLean;
        set 
        { 
            if (HasOpinion)
            {
                SetProperty(ref _verdictLean, value); 
            }
        }
    }

    public bool CanVote => IsOccupied && (Role == AgentRole.Juror || Role == AgentRole.AlternateJuror);

    // Only jurors/judge form opinions and update verdict lean in this simulation.
    // Stakeholders should be selectable/created, but they should not deliberate/vote
    // and should not update verdict lean unless explicitly intended.
    public bool HasOpinion => IsOccupied && ((CanVote || Role == AgentRole.Judge) && Role != AgentRole.Reporter);

    public bool CanDeliberate => IsOccupied && (Role == AgentRole.Juror || Role == AgentRole.AlternateJuror);
    public bool IsClient => Role == AgentRole.Client;

    private double _consideredDamages = 0;
    
    /// <summary>
    /// The damages amount this agent personally considers appropriate for the case.
    /// Updated as evidence is submitted, influenced by their background and bias.
    /// </summary>
    public double ConsideredDamages
    {
        get => _consideredDamages;
        set => SetProperty(ref _consideredDamages, value);
    }

    private int _documentCount = 0;
    
    /// <summary>
    /// Number of documents processed by this agent (used for Clerk/Reporter display).
    /// </summary>
    public int DocumentCount
    {
        get => _documentCount;
        set => SetProperty(ref _documentCount, value);
    }

    /// <summary>
    /// Indicates if this agent slot is filled with a specific persona.
    /// </summary>
    public bool IsOccupied
    {
        get => _isOccupied;
        set => SetProperty(ref _isOccupied, value);
    }

    /// <summary>
    /// Decays the strength of all memories.
    /// </summary>
    /// <param name="factor">The decay factor (default 0.95).</param>
    public void DecayMemories(double factor = 0.95)
    {
        foreach (var memory in Memories)
        {
            memory.Strength *= factor;
        }
        foreach (var trialEvent in TrialEvents)
        {
            trialEvent.Strength *= factor;
        }
    }

    /// <summary>
    /// Adds a new trial observation or reinforces an existing one.
    /// </summary>
    public void RecordTrialEvent(string content, double biasInfluence)
    {
        var existing = TrialEvents.FirstOrDefault(m => m.Content == content);
        if (existing != null)
        {
            existing.Strength *= (1.0 + biasInfluence);
        }
        else
        {
            TrialEvents.Add(new MemoryEntry { Content = content, Strength = 1.0 + biasInfluence });
        }
    }

    /// <summary>
    /// Adds a new background memory or reinforces an existing one.
    /// </summary>
    public void ReinforceMemory(string content, double biasInfluence)
    {
        var existing = Memories.FirstOrDefault(m => m.Content == content);
        if (existing != null)
        {
            existing.Strength *= (1.0 + biasInfluence);
        }
        else
        {
            Memories.Add(new MemoryEntry { Content = content, Strength = 1.0 + biasInfluence });
        }
    }

    /// <summary>
    /// Updates the verdict lean based on incoming influence and existing bias.
    /// Reporters do not form opinions and thus cannot update their verdict lean.
    /// </summary>
    public void UpdateVerdictLean(double influence)
    {
        if (!HasOpinion) return; // Reporters and other non-opinion agents should not update verdict lean
        
        // Opinion shift influenced by existing bias
        double shift = influence * (1.0 + Bias);
        VerdictLean = Math.Clamp(VerdictLean + shift, 0.0, 1.0);
    }

    /// <summary>
    /// Performs a simulated sentiment analysis on courtroom content and updates emotional state.
    /// Reporters remain neutral and do not have their sentiment affected.
    /// </summary>
    public void AnalyzeSentiment(string content)
    {
        if (Role == AgentRole.Reporter) return; // Reporters remain neutral
        
        double shift = 0;
        string lower = content.ToLower();

        // Keywords that typically trigger emotional responses in trial
        if (lower.Contains("objection") || lower.Contains("overruled")) shift = -0.02;
        else if (lower.Contains("sustained")) shift = 0.02;
        else if (lower.Contains("lied") || lower.Contains("false") || lower.Contains("inconsistent")) shift = -0.1;
        else if (lower.Contains("expert") || lower.Contains("dna") || lower.Contains("fact")) shift = 0.05;
        else if (lower.Contains("admitted") || lower.Contains("evidence")) shift = 0.03;

        // Apply shift influenced by current bias (people react more strongly if it confirms/denies bias)
        Sentiment = Math.Clamp(Sentiment + (shift * (1.0 + Math.Abs(Bias))), 0.0, 1.0);
    }

    /// <summary>
    /// Copies all properties from another agent into this one.
    /// Used for role assignment and slot population.
    /// </summary>
    public void CopyTo(Agent target)
    {
        target.AgentId = this.AgentId;
        target.Name = this.Name;
        target.Role = this.Role;
        target.Gender = this.Gender;
        target.Age = this.Age;
        target.Race = this.Race;
        target.Occupation = this.Occupation;
        target.EducationLevel = this.EducationLevel;
        target.IncomeLevel = this.IncomeLevel;
        target.ZipCode = this.ZipCode;
        target.PoliticalAffiliation = this.PoliticalAffiliation;
        target.ReligiousAffiliation = this.ReligiousAffiliation;
        target.SpecializedKnowledge = this.SpecializedKnowledge;
        target.MaritalStatus = this.MaritalStatus;
        target.ParentalStatus = this.ParentalStatus;
        target.SettlementAuthority = this.SettlementAuthority;
        target.RiskPerception = this.RiskPerception;
        target.MediaConsumption = this.MediaConsumption;
        target.ConsumerSegment = this.ConsumerSegment;
        target.Hobbies = this.Hobbies;
        target.ViewingCharacteristics = this.ViewingCharacteristics;
        target.CurrentStatus = this.CurrentStatus;
        target.SystemPrompt = this.SystemPrompt;
        target.Profile = this.Profile;
        target.Bias = this.Bias;
        target.VerdictLean = this.VerdictLean;
        target.Sentiment = this.Sentiment;
        target.IsOccupied = true;
        target.ConsideredDamages = this.ConsideredDamages;
        target.ChatEvents.Clear();
        foreach (var e in ChatEvents)
            target.ChatEvents.Add(new MemoryEntry { Content = e.Content, Strength = e.Strength, Timestamp = e.Timestamp, IsFromDocument = e.IsFromDocument, Source = e.Source });

        target.JudicialRulings = this.JudicialRulings;
        target.WrittenOpinions = this.WrittenOpinions;
        target.JudicialTemperament = this.JudicialTemperament;
        target.ObserverType = this.ObserverType;
        target.ConnectedAttorneyName = this.ConnectedAttorneyName;
        target.CanCommunicateWithJury = this.CanCommunicateWithJury;
        target.IsExposurePlanner = this.IsExposurePlanner;
        target.CommunicationTeam = this.CommunicationTeam;
        target.OnlyAnswerWhenAsked = this.OnlyAnswerWhenAsked;
        target.SelectedModel = this.SelectedModel;

        target.ModelTemperatureOverride = this.ModelTemperatureOverride;
        target.ModelMaxTokensOverride = this.ModelMaxTokensOverride;
        target.AgeBiasWeight = this.AgeBiasWeight;
        target.GenderBiasWeight = this.GenderBiasWeight;
        target.EducationBiasWeight = this.EducationBiasWeight;
        target.IncomeBiasWeight = this.IncomeBiasWeight;
        target.PoliticalBiasWeight = this.PoliticalBiasWeight;
        target.EthnicityBiasWeight = this.EthnicityBiasWeight;
        target.ReligionBiasWeight = this.ReligionBiasWeight;
        target.JurorExperienceWeight = this.JurorExperienceWeight;
        target.LegalKnowledgeWeight = this.LegalKnowledgeWeight;
        target.ProfessionalBackgroundWeight = this.ProfessionalBackgroundWeight;
        target.CommunityTiesWeight = this.CommunityTiesWeight;
        target.RoleSystemPrompt = this.RoleSystemPrompt;
        foreach (var entry in ExhibitLog)
            target.ExhibitLog.Add(entry);
        foreach (var bf in BiasFactors)
            target.BiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });
        
        target.PerceivedEvidence.Clear();
        foreach (var pe in PerceivedEvidence)
            target.PerceivedEvidence.Add(new EvidencePerception { ExhibitNumber = pe.ExhibitNumber, PerceivedStrength = pe.PerceivedStrength, TrustIndex = pe.TrustIndex });

// Optional bias-research predictors (defaults are fine for older cases)
         target.PriorVictimizationHistory = this.PriorVictimizationHistory;
         target.SystemJustification = this.SystemJustification;
         target.NeedForCognition = this.NeedForCognition;
         target.NeedForClosure = this.NeedForClosure;
         target.TraitAnxiety = this.TraitAnxiety;
         target.MediaConsumptionType = this.MediaConsumptionType;
         target.PriorJuryOutcome = this.PriorJuryOutcome;
         target.Openness = this.Openness;
         target.Agreeableness = this.Agreeableness;
         target.Neuroticism = this.Neuroticism;
         target.MoralHarm = this.MoralHarm;
         target.MoralFairness = this.MoralFairness;
         target.MoralAuthority = this.MoralAuthority;
         target.MoralPurity = this.MoralPurity;

         // Personality-Texture predictors
         target.HumorLevityTendency = this.HumorLevityTendency;
         target.ConflictAvoidance = this.ConflictAvoidance;
         target.DominanceAssertiveness = this.DominanceAssertiveness;
         target.PatienceImpulsivity = this.PatienceImpulsivity;

         // Social-Identity predictors
         target.UrbanRuralBackground = this.UrbanRuralBackground;
         target.MilitaryService = this.MilitaryService;
         target.UnionMembership = this.UnionMembership;
         target.ImmigrationGeneration = this.ImmigrationGeneration;

         // Cognitive/Emotional/Knowledge predictors
         target.DetailOrientation = this.DetailOrientation;
         target.MemoryReliability = this.MemoryReliability;
         target.SuspicionTendency = this.SuspicionTendency;
         target.DisgustSensitivity = this.DisgustSensitivity;
         target.Compassion = this.Compassion;
         target.AngerReactivity = this.AngerReactivity;
         target.ScienceLiteracy = this.ScienceLiteracy;
         target.FinancialLiteracy = this.FinancialLiteracy;
         target.TechnologyFamiliarity = this.TechnologyFamiliarity;
     }



    // NOTE: ExhibitLog is copied in CopyTo above. CommunicationTeam/OnlyAnswerWhenAsked
    // are also copied there for correct save/load + editor flows.

    // ---------------------------------------------------------------------
    // Additional juror predictors (bias research)
    // These are optional inputs for the toy math engine. Defaults keep
    // backwards compatibility with existing saved cases.
    // ---------------------------------------------------------------------



    /// <summary>
    /// Per-agent bias dimension weights. Null means inherit the global value from ModelWeightsWindow.
    /// Range 0.0–1.0.
    /// </summary>
    public double? AgeBiasWeight          { get => _ageBiasWeight;          set => SetProperty(ref _ageBiasWeight,          value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? GenderBiasWeight       { get => _genderBiasWeight;       set => SetProperty(ref _genderBiasWeight,       value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? EducationBiasWeight    { get => _educationBiasWeight;    set => SetProperty(ref _educationBiasWeight,    value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? IncomeBiasWeight       { get => _incomeBiasWeight;       set => SetProperty(ref _incomeBiasWeight,       value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? PoliticalBiasWeight    { get => _politicalBiasWeight;    set => SetProperty(ref _politicalBiasWeight,    value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? EthnicityBiasWeight    { get => _ethnicityBiasWeight;    set => SetProperty(ref _ethnicityBiasWeight,    value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? ReligionBiasWeight     { get => _religionBiasWeight;     set => SetProperty(ref _religionBiasWeight,     value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? JurorExperienceWeight  { get => _jurorExperienceWeight;  set => SetProperty(ref _jurorExperienceWeight,  value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? LegalKnowledgeWeight   { get => _legalKnowledgeWeight;   set => SetProperty(ref _legalKnowledgeWeight,   value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? ProfessionalBackgroundWeight { get => _professionalBackgroundWeight; set => SetProperty(ref _professionalBackgroundWeight, value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }
    public double? CommunityTiesWeight    { get => _communityTiesWeight;    set => SetProperty(ref _communityTiesWeight,    value.HasValue ? Math.Clamp(value.Value, 0, 1) : null); }

    /// <summary>
    /// Overrides the built-in role system prompt for this agent.
    /// Empty string means use the default from AgentInteractionService.GetDefaultSystemPrompt.
    /// </summary>
    public string RoleSystemPrompt
    {
        get => _roleSystemPrompt;
        set => SetProperty(ref _roleSystemPrompt, value);
    }

    /// <summary>
    /// Running log of reporter exhibit summaries this agent has heard, keyed by exhibit number.
    /// Used so jurors can re-evaluate all evidence when their psychology changes.
    /// </summary>
    public ObservableCollection<string> ExhibitLog { get; } = new();

    /// <summary>
    /// The litigation observer subtype (e.g., InsuranceAdjuster, ClaimsAnalyst).
    /// </summary>
    /// <summary>
    /// Logical side affiliation used for communication restrictions.
    /// Common values: "Plaintiff", "Defendant", "Neutral".
    /// </summary>
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

    /// <summary>
    /// If true, the agent should be treated as "judge-like" (answers only when asked)
    /// by restricting spontaneous participation.
    /// </summary>
    public bool OnlyAnswerWhenAsked
    {
        get => _onlyAnswerWhenAsked;
        set => SetProperty(ref _onlyAnswerWhenAsked, value);
    }


    /// <summary>
    /// Name of the attorney this observer is connected to.
    /// </summary>
    public string ConnectedAttorneyName
    {
        get => _connectedAttorneyName;
        set => SetProperty(ref _connectedAttorneyName, value);
    }

    /// <summary>
    /// If false, this observer must not influence/communicate with the jury.
    /// </summary>
    public bool CanCommunicateWithJury
    {
        get => _canCommunicateWithJury;
        set => SetProperty(ref _canCommunicateWithJury, value);
    }

    /// <summary>
    /// If true, observer estimates outcomes/exposure to protect the attorney/client.
    /// </summary>
    public bool IsExposurePlanner
    {
        get => _isExposurePlanner;
        set => SetProperty(ref _isExposurePlanner, value);
    }

    /// <summary>
    /// The selected model that this agent should use for AI interactions.
    /// If empty, the system will use the first available working model.
    /// </summary>
    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    /// <summary>
    /// Overrides the default model temperature for this agent (0.0-2.0).
    /// Null means use the global default from case settings.
    /// </summary>
    public double? ModelTemperatureOverride
    {
        get => _modelTemperatureOverride;
        set => SetProperty(ref _modelTemperatureOverride, value.HasValue ? Math.Clamp(value.Value, 0.0, 2.0) : null);
    }

    /// <summary>
    /// Overrides the default max tokens for this agent.
    /// Null means use the global default from case settings.
    /// </summary>
    public int? ModelMaxTokensOverride
    {
        get => _modelMaxTokensOverride;
        set => SetProperty(ref _modelMaxTokensOverride, value.HasValue ? Math.Max(64, value.Value) : null);
    }

    // ---------------------------------------------------------------------
    // Additional juror predictors (bias research)
    // These are optional inputs for the toy math engine. Defaults keep
    // backwards compatibility with existing saved cases.
    // ---------------------------------------------------------------------

    // Prior victimization history (0..2)
    private int _priorVictimizationHistory;
    public int PriorVictimizationHistory
    {
        get => _priorVictimizationHistory;
        set => SetProperty(ref _priorVictimizationHistory, Math.Clamp(value, 0, 2));
    }

    // Trust in institutions / system justification (0..10)
    private double _systemJustification;
    public double SystemJustification
    {
        get => _systemJustification;
        set => SetProperty(ref _systemJustification, Math.Clamp(value, 0.0, 10.0));
    }

    // Need-for-cognition / need-for-closure (0..10)
    private double _needForCognition;
    public double NeedForCognition
    {
        get => _needForCognition;
        set => SetProperty(ref _needForCognition, Math.Clamp(value, 0.0, 10.0));
    }

private double _needForClosure;
     public double NeedForClosure
     {
         get => _needForClosure;
         set => SetProperty(ref _needForClosure, Math.Clamp(value, 0.0, 10.0));
     }

     // Trait anxiety (0..10)
     private double _traitAnxiety;
     public double TraitAnxiety
     {
         get => _traitAnxiety;
         set => SetProperty(ref _traitAnxiety, Math.Clamp(value, 0.0, 10.0));
     }

     // --- Personality-Texture Predictors (Soft but Useful) ---
     // Humor/Levity Tendency (0=serious, 10=light-hearted)
     private double _humorLevityTendency;
     public double HumorLevityTendency
     {
         get => _humorLevityTendency;
         set => SetProperty(ref _humorLevityTendency, Math.Clamp(value, 0.0, 10.0));
     }

     // Conflict Avoidance (high=goes along with majority, low=holdout tendency)
     private double _conflictAvoidance;
     public double ConflictAvoidance
     {
         get => _conflictAvoidance;
         set => SetProperty(ref _conflictAvoidance, Math.Clamp(value, 0.0, 10.0));
     }

     // Dominance/Assertiveness (predicts foreperson influence weight)
     private double _dominanceAssertiveness;
     public double DominanceAssertiveness
     {
         get => _dominanceAssertiveness;
         set => SetProperty(ref _dominanceAssertiveness, Math.Clamp(value, 0.0, 10.0));
     }

     // Patience/Impulsivity (high=patient/slow process, low=impulsive/quick verdict)
     private double _patienceImpulsivity;
     public double PatienceImpulsivity
     {
         get => _patienceImpulsivity;
         set => SetProperty(ref _patienceImpulsivity, Math.Clamp(value, 0.0, 10.0));
     }

     // --- Social-Identity Predictors (Contextual) ---
     // Urban vs Rural Background (0=rural, 1=urban)
     private int _urbanRuralBackground;
     public int UrbanRuralBackground
     {
         get => _urbanRuralBackground;
         set => SetProperty(ref _urbanRuralBackground, Math.Clamp(value, 0, 1));
     }

     // Military Service (0=no, 1=yes)
     private int _militaryService;
     public int MilitaryService
     {
         get => _militaryService;
         set => SetProperty(ref _militaryService, Math.Clamp(value, 0, 1));
     }

     // Union Membership (0=no, 1=yes)
     private int _unionMembership;
     public int UnionMembership
     {
         get => _unionMembership;
         set => SetProperty(ref _unionMembership, Math.Clamp(value, 0, 1));
     }

     // Immigration Generation (1=first-gen, 2=second, 3=third+)
     private int _immigrationGeneration;
     public int ImmigrationGeneration
     {
         get => _immigrationGeneration;
         set => SetProperty(ref _immigrationGeneration, Math.Clamp(value, 1, 3));
     }

     // Media consumption type (string category)
    private string _mediaConsumptionType = "Generic";
    public string MediaConsumptionType
    {
        get => _mediaConsumptionType;
        set => SetProperty(ref _mediaConsumptionType, value ?? "Generic");
    }

    // Prior jury outcome anchor (0..2)
    // 0 = none/unknown, 1 = hung, 2 = convicted/weakly aligned, 3.. => convicted-acquitted separate not stored here
    // We'll interpret 1 = hung, 2 = convicted, 3 = acquitted via numeric mapping in the engine.
    private int _priorJuryOutcome;
    public int PriorJuryOutcome
    {
        get => _priorJuryOutcome;
        set => SetProperty(ref _priorJuryOutcome, Math.Clamp(value, 0, 3));
    }

    // Big Five (0..10)
    private double _openness;
    public double Openness
    {
        get => _openness;
        set => SetProperty(ref _openness, Math.Clamp(value, 0.0, 10.0));
    }

    private double _agreeableness;
    public double Agreeableness
    {
        get => _agreeableness;
        set => SetProperty(ref _agreeableness, Math.Clamp(value, 0.0, 10.0));
    }

    private double _neuroticism;
    public double Neuroticism
    {
        get => _neuroticism;
        set => SetProperty(ref _neuroticism, Math.Clamp(value, 0.0, 10.0));
    }

    // Moral foundations (0..10)
    private double _moralHarm;
    public double MoralHarm
    {
        get => _moralHarm;
        set => SetProperty(ref _moralHarm, Math.Clamp(value, 0.0, 10.0));
    }

    private double _moralFairness;
    public double MoralFairness
    {
        get => _moralFairness;
        set => SetProperty(ref _moralFairness, Math.Clamp(value, 0.0, 10.0));
    }

    private double _moralAuthority;
    public double MoralAuthority
    {
        get => _moralAuthority;
        set => SetProperty(ref _moralAuthority, Math.Clamp(value, 0.0, 10.0));
    }

    private double _moralPurity;
    public double MoralPurity
    {
        get => _moralPurity;
        set => SetProperty(ref _moralPurity, Math.Clamp(value, 0.0, 10.0));
    }

    // ---------------------------------------------------------------------
    // Cognitive/Emotional/Knowledge predictors (path shaping)
    // These are optional inputs for the toy juror engine.
    // Defaults keep backwards compatibility with older saved cases.
    // ---------------------------------------------------------------------

    // A. Detail Orientation (0..10)
    private double _detailOrientation = 5.0;
    public double DetailOrientation
    {
        get => _detailOrientation;
        set => SetProperty(ref _detailOrientation, Math.Clamp(value, 0.0, 10.0));
    }

    // B. Memory Reliability (0..10) where higher = more accurate recall
    private double _memoryReliability = 5.0;
    public double MemoryReliability
    {
        get => _memoryReliability;
        set => SetProperty(ref _memoryReliability, Math.Clamp(value, 0.0, 10.0));
    }

    // C. Suspicion Tendency (0..10) higher => doubts people
    private double _suspicionTendency = 5.0;
    public double SuspicionTendency
    {
        get => _suspicionTendency;
        set => SetProperty(ref _suspicionTendency, Math.Clamp(value, 0.0, 10.0));
    }

    // Emotional predictors
    // Disgust Sensitivity (0..10)
    private double _disgustSensitivity = 5.0;
    public double DisgustSensitivity
    {
        get => _disgustSensitivity;
        set => SetProperty(ref _disgustSensitivity, Math.Clamp(value, 0.0, 10.0));
    }

    // Sympathy/Compassion Baseline (0..10)
    private double _compassion = 5.0;
    public double Compassion
    {
        get => _compassion;
        set => SetProperty(ref _compassion, Math.Clamp(value, 0.0, 10.0));
    }

    // Anger Reactivity (0..10)
    private double _angerReactivity = 5.0;
    public double AngerReactivity
    {
        get => _angerReactivity;
        set => SetProperty(ref _angerReactivity, Math.Clamp(value, 0.0, 10.0));
    }

    // Knowledge-domain predictors (0..10)
    private double _scienceLiteracy = 5.0;
    public double ScienceLiteracy
    {
        get => _scienceLiteracy;
        set => SetProperty(ref _scienceLiteracy, Math.Clamp(value, 0.0, 10.0));
    }

    private double _financialLiteracy = 5.0;
    public double FinancialLiteracy
    {
        get => _financialLiteracy;
        set => SetProperty(ref _financialLiteracy, Math.Clamp(value, 0.0, 10.0));
    }

    private double _technologyFamiliarity = 5.0;
    public double TechnologyFamiliarity
    {
        get => _technologyFamiliarity;
        set => SetProperty(ref _technologyFamiliarity, Math.Clamp(value, 0.0, 10.0));
    }
}

/// <summary>
/// Represents an agent's subjective view of a specific piece of evidence.
/// </summary>
public class EvidencePerception : ObservableObject
{
    private int _exhibitNumber;
    private double _perceivedStrength;
    private double _trustIndex = 1.0;

    public int ExhibitNumber
    {
        get => _exhibitNumber;
        set => SetProperty(ref _exhibitNumber, value);
    }

    public double PerceivedStrength
    {
        get => _perceivedStrength;
        set => SetProperty(ref _perceivedStrength, value);
    }

    public double TrustIndex
    {
        get => _trustIndex;
        set => SetProperty(ref _trustIndex, value);
    }
}
