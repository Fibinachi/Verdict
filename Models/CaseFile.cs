using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Verdict.Models
{
    public class CaseFile : ObservableObject
    {
        private string _caseName = "Untitled Case";
        private string _caseNumber = "00-0000";
        private string _courtName = "Superior Court";
        private CaseMode _mode = CaseMode.Civil;
        private JurisdictionType _jurisdiction = JurisdictionType.State;
        private string _jurisdictionSpecifics = string.Empty;
        private int _jurorCount = 12;
        private DateTime _lastSaved = DateTime.Now;
        private TrialPhase _trialPhase = TrialPhase.Discovery;
        private CourtPhase _currentDebateStage = CourtPhase.OpeningStatements;
        private double _estimatedSettlement;
        private double _insuranceReserve;
        private double _globalTemperature = 0.7;
        private int _globalMaxTokens = 4096;
        private double _defaultBiasWeight = 0.0;

        public CaseFile()
        {
        }

        /// <summary>
        /// Adds default model configurations for a new case.
        /// This is separate from the constructor so that deserialization
        /// does not prepend defaults to saved model configurations.
        /// </summary>
        public void AddDefaultModels()
        {
            if (AvailableModels.Count > 0) return; // Don't overwrite existing models
            
            AvailableModels.Add(new AIModelConfiguration 
            { 
                FriendlyName = "OpenAI GPT-4o", 
                Provider = "OpenAI", 
                ModelId = "gpt-4o" 
            });
            AvailableModels.Add(new AIModelConfiguration 
            { 
                FriendlyName = "Claude 3.5 Sonnet", 
                Provider = "Anthropic", 
                ModelId = "claude-3-5-sonnet-20240620" 
            });
            AvailableModels.Add(new AIModelConfiguration 
            { 
                FriendlyName = "Google Gemini 1.5 Pro", 
                Provider = "Google", 
                ModelId = "gemini-1.5-pro" 
            });
        }

        // Method to ensure all collections are properly initialized
        public void EnsureCollectionsInitialized()
        {
            if (Agents == null) Agents = new List<Agent>();
            if (AvailableModels == null) AvailableModels = new List<AIModelConfiguration>();
            if (Instructions == null) Instructions = new JuryInstructions();
            if (Verdict == null) Verdict = new VerdictForm();
            if (Pleadings == null) Pleadings = new List<EvidenceDocument>();
            if (Evidence == null) Evidence = new List<EvidenceDocument>();
            if (EventLog == null) EventLog = new List<CourtroomEvent>();
            if (Plaintiffs == null) Plaintiffs = new List<string>();
            if (Defendants == null) Defendants = new List<string>();
            if (ProsecutionPlaintiffPrompt == null) ProsecutionPlaintiffPrompt = string.Empty;
            if (DefensePrompt == null) DefensePrompt = string.Empty;
            if (GeneralPrompt == null) GeneralPrompt = string.Empty;
        }

        public string CaseName
        {
            get => _caseName;
            set => SetProperty(ref _caseName, value);
        }

        public string CaseNumber
        {
            get => _caseNumber;
            set => SetProperty(ref _caseNumber, value);
        }

        public string CourtName
        {
            get => _courtName;
            set => SetProperty(ref _courtName, value);
        }

        public CaseMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public JurisdictionType Jurisdiction
        {
            get => _jurisdiction;
            set => SetProperty(ref _jurisdiction, value);
        }

        public string JurisdictionSpecifics
        {
            get => _jurisdictionSpecifics;
            set => SetProperty(ref _jurisdictionSpecifics, value);
        }

        public int JurorCount
        {
            get => _jurorCount;
            set => SetProperty(ref _jurorCount, value);
        }

        public DateTime LastSaved
        {
            get => _lastSaved;
            set => SetProperty(ref _lastSaved, value);
        }

        /// <summary>
        /// Current phase of the case: Discovery, Pretrial, or Trial
        /// </summary>
        public TrialPhase TrialPhase
        {
            get => _trialPhase;
            set => SetProperty(ref _trialPhase, value);
        }

        /// <summary>
        /// Current debate stage in the five-stage courtroom debate
        /// </summary>
        public CourtPhase CurrentDebateStage
        {
            get => _currentDebateStage;
            set => SetProperty(ref _currentDebateStage, value);
        }

        /// <summary>
        /// Estimated settlement value based on evidence exposure
        /// </summary>
        public double EstimatedSettlement
        {
            get => _estimatedSettlement;
            set => SetProperty(ref _estimatedSettlement, value);
        }

        /// <summary>
        /// Insurance reserve amount recommended based on evidence exposure
        /// </summary>
        public double InsuranceReserve
        {
            get => _insuranceReserve;
            set => SetProperty(ref _insuranceReserve, value);
        }

        public double GlobalTemperature
        {
            get => _globalTemperature;
            set => SetProperty(ref _globalTemperature, value);
        }

        public int GlobalMaxTokens
        {
            get => _globalMaxTokens;
            set => SetProperty(ref _globalMaxTokens, value);
        }

        public double DefaultBiasWeight
        {
            get => _defaultBiasWeight;
            set => SetProperty(ref _defaultBiasWeight, Math.Clamp(value, -1.0, 1.0));
        }

        public List<BiasFactor> DefaultBiasFactors { get; set; } = new()
        {
            new BiasFactor { Name = "Age", Weight = 0.55 },
            new BiasFactor { Name = "Gender", Weight = 0.45 },
            new BiasFactor { Name = "Education Level", Weight = 0.80 },
            new BiasFactor { Name = "Income Level", Weight = 0.50 },
            new BiasFactor { Name = "Political Affiliation", Weight = 0.90 },
            new BiasFactor { Name = "Ethnicity", Weight = 0.60 },
            new BiasFactor { Name = "Religion", Weight = 0.40 },
            new BiasFactor { Name = "Juror Experience", Weight = 0.70 },
            new BiasFactor { Name = "Legal Knowledge", Weight = 0.75 },
            new BiasFactor { Name = "Professional Background", Weight = 0.55 },
            new BiasFactor { Name = "Community Ties", Weight = 0.65 },
            new BiasFactor { Name = "Communication Style", Weight = 0.35 },
        };

        public List<Agent> Agents { get; set; } = new();
        public List<AIModelConfiguration> AvailableModels { get; set; } = new();
        
        // Parties
        public List<string> Plaintiffs { get; set; } = new();
        public List<string> Defendants { get; set; } = new();
        public string PlaintiffAttorney { get; set; } = string.Empty;
        public string DefenseAttorney { get; set; } = string.Empty;
        
        // Legal Framework
        public JuryInstructions Instructions { get; set; } = new();
        public VerdictForm Verdict { get; set; } = new();
        
        // Pleadings
        public List<EvidenceDocument> Pleadings { get; set; } = new();
        
        // Court Record
        public List<EvidenceDocument> Evidence { get; set; } = new();
        public List<CourtroomEvent> EventLog { get; set; } = new();

        // Attorney Directives
        public string ProsecutionPlaintiffPrompt { get; set; } = string.Empty;
        public string DefensePrompt { get; set; } = string.Empty;
        
        public string GeneralPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of deliberation turns to run when conducting jury deliberation.
        /// Default: 20.
        /// </summary>
        public int DeliberationRounds { get; set; } = 20;


        // Helper properties for multi-line text binding in UI
        [System.Text.Json.Serialization.JsonIgnore]
        public string PlaintiffsText
        {
            get => string.Join("\r\n", Plaintiffs);
            set => Plaintiffs = (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string DefendantsText
        {
            get => string.Join("\r\n", Defendants);
            set => Defendants = (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
    }
}
