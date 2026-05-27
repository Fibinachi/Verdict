using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Interface for the agent interaction service that manages courtroom LLM interactions.
/// </summary>
public interface IAgentInteractionService
{
    /// <summary>
    /// Generates a courtroom statement from an agent.
    /// </summary>
    Task<InteractionResult> GenerateStatementAsync(StatementRequest request);

    /// <summary>
    /// Conducts direct or cross-examination of a witness.
    /// </summary>
    Task<(AgentMessage Question, AgentMessage Answer)> ConductExaminationAsync(ExaminationRequest request);

    /// <summary>
    /// Generates the opening statement for a party.
    /// </summary>
    Task<AgentMessage> GenerateOpeningStatementAsync(AgentRole role, CaseFile caseData, List<AgentMessage> transcript, AIModelConfiguration model);

    /// <summary>
    /// Generates the closing argument for a party.
    /// </summary>
    Task<AgentMessage> GenerateClosingArgumentAsync(AgentRole role, CaseFile caseData, List<AgentMessage> transcript, AIModelConfiguration model);

    /// <summary>
    /// Conducts jury deliberation.
    /// </summary>
    Task<DeliberationResult> ConductDeliberationAsync(DeliberationRequest request);

    /// <summary>
    /// Generates the verdict announcement.
    /// </summary>
    Task<AgentMessage> GenerateVerdictAsync(CaseFile caseData, DeliberationResult deliberation, AIModelConfiguration model);

    /// <summary>
    /// Generates a judge's ruling on an objection.
    /// </summary>
    Task<AgentMessage> GenerateRulingAsync(string objection, AIModelConfiguration model);

    /// <summary>
    /// Queries legal research for a specific topic.
    /// </summary>
    Task<string> QueryLegalResearchAsync(string query, string jurisdiction, AIModelConfiguration model);

    /// <summary>
    /// Has an agent internalize a neutral court record through the lens of their own biases and background,
    /// returning a paraphrased memory string.
    /// </summary>
    Task<string> InternalizeCourtRecordAsync(Agent agent, string neutralRecord, AIModelConfiguration model);

    /// <summary>
    /// Has the court reporter produce a plain-language summary and explanation of an exhibit
    /// for the official record — what it is, what it shows, and why it matters legally.
    /// </summary>
    Task<string> SummarizeEvidenceForRecordAsync(Agent reporter, EvidenceDocument doc, CaseFile caseFile, AIModelConfiguration model);
}

/// <summary>
/// Service for managing agent interactions in the courtroom simulation.
/// </summary>
public class AgentInteractionService : IAgentInteractionService
{
    private readonly ILLMProviderModule?[] _providers;
    private readonly ILLMProviderModule? _defaultProvider;

    public AgentInteractionService(IEnumerable<ILLMProviderModule> providers)
    {
        _providers = providers.ToArray();
        _defaultProvider = _providers.FirstOrDefault();
    }

    private ILLMProviderModule? GetProvider(AIModelConfiguration? modelConfig)
    {
        if (modelConfig == null) return _defaultProvider;

        // Try exact match first
        var match = _providers.FirstOrDefault(p =>
            p?.ProviderName.Equals(modelConfig.Provider, StringComparison.OrdinalIgnoreCase) == true);
        if (match != null) return match;

        // Try lenient match (ignore spaces and hyphens) for backward compatibility
        var normalizedConfig = NormalizeProviderName(modelConfig.Provider);
        match = _providers.FirstOrDefault(p =>
            p != null && NormalizeProviderName(p.ProviderName) == normalizedConfig);

        // Try prefix match (e.g., "Google" matches "Google Gemini", "Alibaba" matches "Alibaba Cloud")
        if (match == null)
        {
            match = _providers.FirstOrDefault(p =>
                p != null && NormalizeProviderName(p.ProviderName).StartsWith(normalizedConfig));
        }

        if (match != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentInteraction] Provider name mismatch corrected: '{modelConfig.Provider}' -> '{match.ProviderName}'");
            return match;
        }

        // Last resort: use default provider
        System.Diagnostics.Debug.WriteLine(
            $"[AgentInteraction] No provider found for '{modelConfig.Provider}', falling back to default: {_defaultProvider?.ProviderName ?? "null"}");
        return _defaultProvider;
    }

    private static string NormalizeProviderName(string name)
    {
        return (name ?? "").Replace(" ", "").Replace("-", "").ToLowerInvariant();
    }

    public async Task<InteractionResult> GenerateStatementAsync(StatementRequest request)
    {
        var provider = GetProvider(request.Model);
        if (provider == null)
        {
            return new InteractionResult { Success = false, Error = "No LLM provider available" };
        }

        try
        {
            var prompt = BuildStatementPrompt(request);
            // Use the agent's configured role prompt if set, otherwise fall back to the built-in default
            string systemPrompt = !string.IsNullOrWhiteSpace(request.Agent?.RoleSystemPrompt)
                ? request.Agent.RoleSystemPrompt
                : GetRoleSystemPrompt(request.Role);

            var response = await provider.GenerateResponseAsync(
                request.Model ?? new AIModelConfiguration { Provider = provider.ProviderName },
                systemPrompt,
                prompt
            );

            var message = new AgentMessage
            {
                Speaker = request.SpeakerName,
                Role = request.Role,
                Content = response,
                Type = request.Type,
                ReplyToId = request.ReplyToMessage,
                IsFromAI = true
            };

            return new InteractionResult 
            { 
                Success = true, 
                Message = message,
                SentimentShift = CalculateSentimentShift(response, request.Role)
            };
        }
        catch (Exception ex)
        {
            return new InteractionResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<(AgentMessage Question, AgentMessage Answer)> ConductExaminationAsync(ExaminationRequest request)
    {
        var provider = GetProvider(request.Model);
        if (provider == null)
        {
            throw new InvalidOperationException("No LLM provider available");
        }

        // Generate question
        var questionPrompt = BuildExaminationPrompt(request, true);
        var questionResponse = await provider.GenerateResponseAsync(
            request.Model ?? new AIModelConfiguration { Provider = provider.ProviderName },
            GetExaminationPrompt(request.ExaminerRole, request.IsCross),
            questionPrompt
        );

        var question = new AgentMessage
        {
            Speaker = request.ExaminerName,
            Role = request.ExaminerRole,
            Content = questionResponse,
            Type = MessageType.Question,
            IsFromAI = true
        };

        // Generate answer
        var answerPrompt = BuildAnswerPrompt(request, questionResponse);
        var answerResponse = await provider.GenerateResponseAsync(
            request.Model ?? new AIModelConfiguration { Provider = provider.ProviderName },
            GetWitnessPrompt(request.WitnessRole, request.IsCross),
            answerPrompt
        );

        var answer = new AgentMessage
        {
            Speaker = request.WitnessName,
            Role = request.WitnessRole,
            Content = answerResponse,
            Type = MessageType.Testimony,
            ReplyToId = question.Id,
            IsFromAI = true
        };

        return (question, answer);
    }

    public async Task<AgentMessage> GenerateOpeningStatementAsync(
        AgentRole role, 
        CaseFile caseData, 
        List<AgentMessage> transcript, 
        AIModelConfiguration model)
    {
        var provider = GetProvider(model);
        if (provider == null)
        {
            throw new InvalidOperationException("No LLM provider available");
        }

        var isProsecution = role == AgentRole.Lawyer && 
            caseData.ProsecutionPlaintiffPrompt.Contains("plaintiff", StringComparison.OrdinalIgnoreCase);
        
        var party = isProsecution ? "plaintiff" : "defendant";
        var prompt = $@"You are the {party}'s attorney. Deliver a compelling opening statement.

Case: {caseData.CaseName}
Case Number: {caseData.CaseNumber}
Court: {caseData.CourtName}
Type: {caseData.Mode}

{(isProsecution ? caseData.ProsecutionPlaintiffPrompt : caseData.DefensePrompt)}

Present your client's case persuasively. Under 40 words. Be professional and confident. CRITICAL: Refer to your client only as ""my client"" or ""the {party}"", never by name.";

        var response = await provider.GenerateResponseAsync(
            model,
            GetRoleSystemPrompt(role),
            prompt
        );

        return new AgentMessage
        {
            Speaker = $"{party}'s Attorney",
            Role = role,
            Content = response,
            Type = MessageType.Statement,
            IsFromAI = true
        };
    }

    public async Task<AgentMessage> GenerateClosingArgumentAsync(
        AgentRole role, 
        CaseFile caseData, 
        List<AgentMessage> transcript, 
        AIModelConfiguration model)
    {
        var provider = GetProvider(model);
        if (provider == null)
        {
            throw new InvalidOperationException("No LLM provider available");
        }

        var isProsecution = role == AgentRole.Lawyer && 
            caseData.ProsecutionPlaintiffPrompt.Contains("plaintiff", StringComparison.OrdinalIgnoreCase);
        
        var party = isProsecution ? "plaintiff" : "defendant";
        var evidenceList = string.Join(", ", caseData.Evidence.Select(e => e.Summary));
        
        var prompt = $@"You are the {party}'s attorney. Deliver a powerful closing argument.

Case: {caseData.CaseName}
Case Number: {caseData.CaseNumber}
Jurisdiction: {caseData.Jurisdiction}
Evidence presented: {evidenceList}

Summarize why your client should win. Under 40 words. Be persuasive and cite specific evidence. CRITICAL: Refer to your client only as ""my client"" or ""the {party}"", never by name.";

        var response = await provider.GenerateResponseAsync(
            model,
            GetRoleSystemPrompt(role),
            prompt
        );

        return new AgentMessage
        {
            Speaker = $"{party}'s Attorney",
            Role = role,
            Content = response,
            Type = MessageType.Statement,
            IsFromAI = true
        };
    }

    public async Task<DeliberationResult> ConductDeliberationAsync(DeliberationRequest request)
    {
        var provider = GetProvider(request.Model);
        if (provider == null)
        {
            throw new InvalidOperationException("No LLM provider available");
        }

        var model = request.Model ?? new AIModelConfiguration { Provider = provider.ProviderName };
        var caseData = request.CaseData ?? new CaseFile();
        
        // Build transcript summary
        var transcriptSummary = new StringBuilder();
        foreach (var msg in request.Transcript.TakeLast(10))
        {
            transcriptSummary.AppendLine($"{msg.Speaker}: {msg.Content}");
        }

        var evidenceList = string.Join(", ", caseData.Evidence.Select(e => e.Summary));

        // Conduct multi-round deliberation
        var votes = new Dictionary<string, double>();
        var jurorCount = Math.Min(3, request.Jurors.Count(j => j.IsOccupied && j.CanVote));
        
        string reasoning = "";
        string finalVerdict = "";

        for (int i = 0; i < jurorCount; i++)
        {
            var juror = request.Jurors.First(j => j.IsOccupied && j.CanVote);
            
            var prompt = $@"You are Juror {i + 1} in this deliberation. Share your thoughts on the case.

Case: {caseData.CaseName}
Evidence: {evidenceList}
Transcript Summary:
{transcriptSummary}

{(i > 0 ? $"Previous juror said: \"{reasoning}\"" : "")}

Express your view in under 25 words. Say who should win and briefly why.";

            var response = await provider.GenerateResponseAsync(
                model,
                GetRoleSystemPrompt(AgentRole.Juror),
                prompt
            );

            // Parse verdict from response
            var leansProsecution = response.ToLower().Contains("plaintiff") || 
                response.ToLower().Contains("prosecution") ||
                response.ToLower().Contains("guilty");
            
            votes[$"Juror {i + 1}"] = leansProsecution ? 0.8 : 0.2;
            reasoning = response;
            
            if (i == jurorCount - 1)
            {
                finalVerdict = leansProsecution ? "Plaintiff" : "Defendant";
            }
        }

        // Calculate confidence
        var proVotes = votes.Values.Count(v => v > 0.5);
        var confidence = (double)proVotes / votes.Count;

        return new DeliberationResult
        {
            Verdict = finalVerdict,
            Confidence = confidence,
            Reasoning = reasoning,
            IndividualVotes = votes
        };
    }

    public async Task<AgentMessage> GenerateVerdictAsync(
        CaseFile caseData, 
        DeliberationResult deliberation, 
        AIModelConfiguration model)
    {
        var provider = GetProvider(model);
        if (provider == null)
        {
            throw new InvalidOperationException("No LLM provider available");
        }

        var prompt = $@"You are the presiding judge. Announce the verdict and explain your reasoning.

Case: {caseData.CaseName}
Case Number: {caseData.CaseNumber}

The jury has reached a verdict: {deliberation.Verdict}
Confidence: {deliberation.Confidence:P0}
Reasoning: {deliberation.Reasoning}

Deliver the verdict formally with legal reasoning. Under 50 words. Be authoritative and final.";

        var response = await provider.GenerateResponseAsync(
            model,
            GetRoleSystemPrompt(AgentRole.Judge),
            prompt
        );

        return new AgentMessage
        {
            Speaker = "Judge",
            Role = AgentRole.Judge,
            Content = response,
            Type = MessageType.Verdict,
            IsFromAI = true
        };
    }

    public Task<AgentMessage> GenerateRulingAsync(string objection, AIModelConfiguration model)
    {
        // Determine if the objection should be sustained based on keywords
        var isSustained = objection.ToLower().Contains("hearsay") || 
            objection.ToLower().Contains("irrelevant") ||
            objection.ToLower().Contains("speculation");
        
        var response = isSustained ? "Objection sustained." : "Objection overruled.";

        return Task.FromResult(new AgentMessage
        {
            Speaker = "Judge",
            Role = AgentRole.Judge,
            Content = response,
            Type = MessageType.Ruling,
            IsFromAI = true
        });
    }

    public Task<string> QueryLegalResearchAsync(string query, string jurisdiction, AIModelConfiguration model)
    {
        var result = $"[Legal Research] {query} in {jurisdiction}: Reference to relevant statutes and case law would be returned here.";
        return Task.FromResult(result);
    }

    public async Task<string> InternalizeCourtRecordAsync(Agent agent, string neutralRecord, AIModelConfiguration model)
    {
        var provider = GetProvider(model);
        if (provider == null) return neutralRecord;

        string biasDirection = agent.Bias > 0.1 ? "plaintiff/prosecution" :
                               agent.Bias < -0.1 ? "defense/defendant" : "neither side";

        // Use the agent's configured role prompt if set, otherwise build the default internalization prompt
        string systemPrompt = !string.IsNullOrWhiteSpace(agent.RoleSystemPrompt)
            ? agent.RoleSystemPrompt
            : $"You are {agent.Name}, a {agent.Age}-year-old {agent.Occupation} " +
              $"({agent.EducationLevel}, {agent.PoliticalAffiliation}, {agent.ReligiousAffiliation}). " +
              $"Your personal bias leans toward {biasDirection} (bias score: {agent.Bias:+0.00;-0.00;0.00}). " +
              $"Your current emotional state: {agent.CurrentStatus}. " +
              "Rephrase the court record below as a single internal thought — how YOU personally perceived and remember it, " +
              "colored by your background and bias. Do NOT invent new facts. Under 30 words. First person only.";

        try
        {
            return await provider.GenerateResponseAsync(model, systemPrompt, neutralRecord);
        }
        catch
        {
            return neutralRecord;
        }
    }

    public async Task<string> SummarizeEvidenceForRecordAsync(Agent reporter, EvidenceDocument doc, CaseFile caseFile, AIModelConfiguration model)
    {
        var provider = GetProvider(model);
        if (provider == null)
        {
            string fallback = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}, has been admitted into evidence. {doc.Summary}";
            reporter.ExhibitLog.Add(fallback);
            return fallback;
        }

        // Use the reporter's configured role prompt if set, otherwise use the built-in default
        string systemPrompt = !string.IsNullOrWhiteSpace(reporter.RoleSystemPrompt)
            ? reporter.RoleSystemPrompt
            : "You are the official court reporter. Your job is to read an exhibit into the record clearly and plainly " +
              "so that every juror understands what it is, what it shows, and why it is legally significant. " +
              "Do NOT speculate about how jurors will react or what they should think. " +
              "State only facts: what the document is, who created it, what it contains, and what it establishes. " +
              "2-4 sentences. Formal, neutral, factual.";

        string userPrompt =
            $"Read the following exhibit into the record:\n\n" +
            $"Exhibit Number: {doc.ExhibitNumber}\n" +
            $"File: {doc.FileName}\n" +
            $"Media Type: {doc.MediaType}\n" +
            $"Summary: {doc.Summary}\n" +
            (string.IsNullOrEmpty(doc.DetailedAnalysis) ? "" : $"Analysis: {doc.DetailedAnalysis[..Math.Min(500, doc.DetailedAnalysis.Length)]}\n");

        try
        {
            string summary = await provider.GenerateResponseAsync(model, systemPrompt, userPrompt);
            // Append to the reporter's running exhibit log so jurors can re-evaluate later
            reporter.ExhibitLog.Add($"[Exhibit {doc.ExhibitNumber}] {summary}");
            return summary;
        }
        catch
        {
            string fallback = $"Exhibit {doc.ExhibitNumber}, {doc.FileName}, has been admitted into evidence. {doc.Summary}";
            reporter.ExhibitLog.Add(fallback);
            return fallback;
        }
    }

    #region Prompt Builders

    private string BuildStatementPrompt(StatementRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine(request.Context);
        
        if (!string.IsNullOrEmpty(request.ReplyToMessage))
        {
            sb.AppendLine($"\nPrevious message: {request.ReplyToMessage}");
        }

        if (request.CaseData != null)
        {
            sb.AppendLine($"\nCase: {request.CaseData.CaseName}");
            sb.AppendLine($"\nMode: {request.CaseData.Mode}");
        }

        return sb.ToString();
    }

    private string BuildExaminationPrompt(ExaminationRequest request, bool isQuestion)
    {
        var examinerType = request.IsCross ? "Cross-examine" : "Direct examination of";
        var sb = new StringBuilder();
        
        sb.AppendLine($"You are the {request.ExaminerName}. {examinerType} the witness.");
        
        if (isQuestion)
        {
            sb.AppendLine($"\nWitness: {request.WitnessName}");
            sb.AppendLine($"\nBackground: {request.WitnessBackground}");
            if (!string.IsNullOrEmpty(request.ExpectedTestimony))
            {
                sb.AppendLine($"\nExpected testimony: {request.ExpectedTestimony}");
            }
            sb.AppendLine("\nAsk ONE brief, direct question. Under 25 words.");
        }

        return sb.ToString();
    }

    private string BuildAnswerPrompt(ExaminationRequest request, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are the witness. Answer the question honestly.");
        sb.AppendLine($"\nBackground: {request.WitnessBackground}");
        if (!string.IsNullOrEmpty(request.ExpectedTestimony))
        {
            sb.AppendLine($"\nYour testimony: {request.ExpectedTestimony}");
        }
        sb.AppendLine($"\nQuestion: {question}");
        sb.AppendLine("\nAnswer in under 25 words. Be direct and honest.");

        return sb.ToString();
    }

    #endregion

    #region Role Prompts

    /// <summary>
    /// Returns the built-in default system prompt for a given role.
    /// Exposed as public static so the UI can display and reset per-agent overrides.
    /// </summary>
    public static string GetDefaultSystemPrompt(AgentRole role) => role switch
    {
        AgentRole.Judge =>
            "You are the presiding judge in this courtroom. You manage the entire trial process.\n" +
            "Keep responses under 40 words. Be authoritative, fair, and formal. Use phrases like 'The court will now...' Move the trial along briskly.",

        AgentRole.Lawyer =>
            "You are an attorney in this courtroom.\n" +
            "Keep responses under 40 words. Be professional, persuasive, and confident. Present clear arguments with evidence. Cite relevant laws when appropriate.",

        AgentRole.Juror =>
            "You are a juror. You are a regular citizen serving on the jury to decide this case fairly.\n" +
            "Keep responses under 25 words. Be thoughtful, fair-minded, and focused on facts. Speak conversationally.",

        AgentRole.Witness =>
            "You are a witness in this case. You have relevant information about what happened.\n" +
            "Keep responses under 25 words. Be honest, straightforward, and focused on facts you know.",

        AgentRole.Reporter =>
            "You are the court reporter. Your sole function is to produce a neutral, factual one-sentence record of what just occurred in court.\n" +
            "Do NOT express opinions, emotions, analysis, or legal conclusions. Do NOT use words like 'apparently', 'seems', 'clearly', or 'importantly'.\n" +
            "State only observable facts: who spoke, what was said or done. Under 25 words. Begin with the speaker's name or role.",

        _ => "You are a courtroom participant. Keep responses brief and professional."
    };

    private string GetRoleSystemPrompt(AgentRole role) => GetDefaultSystemPrompt(role);

    private string GetExaminationPrompt(AgentRole examinerRole, bool isCross)
    {
        var type = isCross ? "cross-examination" : "direct examination";
        return $@"You are an attorney conducting {type}. Keep questions brief and targeted.";
    }

    private string GetWitnessPrompt(AgentRole witnessRole, bool isCross)
    {
        return @"You are a witness. Answer questions honestly based on what you know. Keep responses brief.";
    }

    #endregion

    #region Helper Methods

    private double CalculateSentimentShift(string response, AgentRole role)
    {
        var lower = response.ToLower();
        double shift = 0;

        if (lower.Contains("objection") || lower.Contains("overruled")) shift = -0.05;
        else if (lower.Contains("sustained")) shift = 0.05;
        else if (lower.Contains("lied") || lower.Contains("false")) shift = -0.1;
        else if (lower.Contains("evidence") || lower.Contains("proven")) shift = 0.05;

        return shift;
    }

    #endregion
}
