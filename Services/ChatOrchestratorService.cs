using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

public interface IChatOrchestratorService
{
    Task<ChatOrchestrationResult> ProcessChatInputLineAsync(
        string speaker,
        string content,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IEnumerable<Agent> judgeArea,
        IAgentInteractionService agentInteraction);
}

public sealed class ChatOrchestrationResult
{
    public required string TranscriptOutput { get; init; }
    public required IReadOnlyList<DeliberationBroadcast> Broadcasts { get; init; }
    public required IReadOnlyList<AgentTrialEventRecord> TrialEventUpdates { get; init; }
}

public sealed class DeliberationBroadcast
{
    public required string Message { get; init; }
    public required IReadOnlyList<AgentRole> VisibleTo { get; init; }
}

public sealed class AgentTrialEventRecord
{
    public required Guid AgentId { get; init; }
    public required string Memory { get; init; }
    public required double BiasMultiplier { get; init; }
}

// This service intentionally keeps orchestration logic only.
// MainViewModel remains responsible for applying results to ObservableCollections / UI.
public sealed class ChatOrchestratorService : IChatOrchestratorService
{
    public async Task<ChatOrchestrationResult> ProcessChatInputLineAsync(
        string speaker,
        string content,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IEnumerable<Agent> judgeArea,
        IAgentInteractionService agentInteraction)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new ChatOrchestrationResult
            {
                TranscriptOutput = string.Empty,
                Broadcasts = Array.Empty<DeliberationBroadcast>(),
                TrialEventUpdates = Array.Empty<AgentTrialEventRecord>()
            };
        }

        string transcriptOutput = string.IsNullOrWhiteSpace(speaker) ? content : $"{speaker}: {content}";
        var broadcasts = new List<DeliberationBroadcast>
        {
            new DeliberationBroadcast
            {
                Message = transcriptOutput,
                VisibleTo = Enum.GetValues<AgentRole>().ToList()
            }
        };

        var trialUpdates = new List<AgentTrialEventRecord>();

        var reporter = judgeArea.FirstOrDefault(a => a.Role == AgentRole.Reporter && a.IsOccupied);
        var transcriptSummary = new List<AgentMessage>
        {
            new AgentMessage
            {
                Speaker = "System",
                Content = transcriptOutput,
                Type = MessageType.Statement,
                IsFromAI = false
            }
        };

        if (reporter != null)
        {
            AIModelConfiguration? reporterModel = null;
            if (!string.IsNullOrEmpty(reporter.SelectedModel))
                reporterModel = currentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == reporter.SelectedModel);

            reporterModel ??= currentCase.AvailableModels.FirstOrDefault();

            if (reporterModel != null)
            {
                var reporterRequest = new StatementRequest
                {
                    Role = AgentRole.Reporter,
                    SpeakerName = reporter.Name,
                    Context = $"Record the following courtroom statement neutrally. Speaker: {speaker}. Statement: {content}",
                    Type = MessageType.Statement,
                    CaseData = currentCase,
                    Transcript = transcriptSummary,
                    Model = reporterModel
                };

                var reporterResult = await agentInteraction.GenerateStatementAsync(reporterRequest);
                if (reporterResult.Success && reporterResult.Message != null)
                {
                    string record = reporterResult.Message.Content;
                    transcriptOutput += $"\n{reporter.Name}: {record}";

                    var model = currentCase.AvailableModels.FirstOrDefault();
                    foreach (var agent in allAgents.Where(a => a.IsOccupied && a.Role != AgentRole.Reporter))
                    {
                        AIModelConfiguration? agentModel = null;
                        if (!string.IsNullOrEmpty(agent.SelectedModel))
                            agentModel = currentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);

                        agentModel ??= model;

                        if (agent.Role == AgentRole.Juror || agent.Role == AgentRole.AlternateJuror)
                        {
                            string memory = agentModel != null
                                ? await agentInteraction.InternalizeCourtRecordAsync(agent, record, agentModel)
                                : record;

                            trialUpdates.Add(new AgentTrialEventRecord
                            {
                                AgentId = agent.AgentId,
                                Memory = memory,
                                BiasMultiplier = agent.Bias * 0.1
                            });
                        }
                        else
                        {
                            trialUpdates.Add(new AgentTrialEventRecord
                            {
                                AgentId = agent.AgentId,
                                Memory = record,
                                BiasMultiplier = 0.0
                            });
                        }
                    }
                }
            }
        }

        // Stage routing stays minimal until the rest of the stage implementation exists.
        var stage = currentCase.CurrentDebateStage;
        if (stage == CourtPhase.OpeningStatements)
        {
            broadcasts.Add(new DeliberationBroadcast
            {
                Message = $"[Stage Placeholder] Opening statements processing for {speaker}.",
                VisibleTo = new List<AgentRole> { AgentRole.Judge, AgentRole.Lawyer }
            });
        }

        return new ChatOrchestrationResult
        {
            TranscriptOutput = transcriptOutput,
            Broadcasts = broadcasts,
            TrialEventUpdates = trialUpdates
        };
    }
}

