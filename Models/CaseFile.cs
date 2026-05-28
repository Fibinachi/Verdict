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

            // Add a default entry for each active provider, auto-detecting API keys from environment
            var providers = new (string FriendlyName, string Provider, string ModelId, string Endpoint, string EnvVar)[]
            {
                ("DeepSeek-V3",   "DeepSeek",      "deepseek-chat",                "https://api.deepseek.com/v1",             "DEEPSEEK_API_KEY"),
                ("Google Gemini", "Google Gemini",  "gemini-2.0-flash",             "https://generativelanguage.googleapis.com", "GEMINI_API_KEY"),
                ("OpenAI GPT-4o", "OpenAI",         "gpt-4o",                       "https://api.openai.com/v1",               "OPENAI_API_KEY"),
                ("Anthropic Claude","Anthropic",    "claude-sonnet-4-20250514",     "https://api.anthropic.com",               "ANTHROPIC_API_KEY"),
                ("Grok",           "Grok",           "grok-3-beta",                  "https://api.x.ai/v1",                     "GROK_API_KEY"),
                ("Alibaba Qwen",   "Alibaba Cloud",  "qwen-turbo",                   "https://dashscope.aliyuncs.com/compatible-mode/v1", "DASHSCOPE_API_KEY"),
                ("NVIDIA NIM",     "NVIDIA",         "meta/llama-3.3-70b-instruct",  "https://integrate.api.nvidia.com/v1",     "NVIDIA_API_KEY"),
                ("Intel Gaudi",    "Intel",          "meta-llama/Meta-Llama-3.3-70B-Instruct", "https://vault.habana.ai",        "INTEL_API_KEY"),
            };

            foreach (var (friendlyName, provider, modelId, endpoint, envVar) in providers)
            {
                var apiKey = Environment.GetEnvironmentVariable(envVar) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    AvailableModels.Add(new AIModelConfiguration
                    {
                        FriendlyName = friendlyName,
                        Provider = provider,
                        ModelId = modelId,
                        Endpoint = endpoint,
                        ApiKey = apiKey
                    });
                }
            }

            // Always add ONNX and HuggingFace (no API key needed)
            AvailableModels.Add(new AIModelConfiguration
            {
                FriendlyName = "ONNX Local",
                Provider = "ONNX",
                ModelId = "llmware/llama-3.2-1b-instruct-onnx",
                Endpoint = ""
            });
            AvailableModels.Add(new AIModelConfiguration
            {
                FriendlyName = "HF TinyLlama",
                Provider = "Hugging Face",
                ModelId = "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF",
                Endpoint = ""
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

        /// <summary>
        /// The default AI model (FriendlyName) assigned to newly generated jurors.
        /// When set, all generated jurors will use this model. Leave empty to
        /// let each juror fall back to the first available model.
        /// </summary>
        public string DefaultJurorModel { get; set; } = string.Empty;

        /// <summary>Default model for the Judge role.</summary>
        public string DefaultJudgeModel { get; set; } = string.Empty;

        /// <summary>Default model for Plaintiff/Prosecution lawyers.</summary>
        public string DefaultProsecutionModel { get; set; } = string.Empty;

        /// <summary>Default model for Defense lawyers.</summary>
        public string DefaultDefenseModel { get; set; } = string.Empty;

        /// <summary>Default model for Witnesses.</summary>
        public string DefaultWitnessModel { get; set; } = string.Empty;

        /// <summary>Default model for the Court Reporter.</summary>
        public string DefaultReporterModel { get; set; } = string.Empty;

        /// <summary>Default model for Client roles.</summary>
        public string DefaultClientModel { get; set; } = string.Empty;

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
