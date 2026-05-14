using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

public static class ConversationRestrictionPolicy
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool CanCommunicate(Agent? speaker, Agent recipient, bool isJudgeAnswer)
    {
        if (recipient == null) return false;

        // Judge can talk to anyone, but OnlyAnswerWhenAsked gates spontaneous participation.
        if (recipient.Role == AgentRole.Judge)
        {
            if (speaker?.Role == AgentRole.Judge)
                return true;

            // If judge is being addressed, allow recipient-side (judge listening/answering)
            return true;
        }

        // If the speaker is a judge-like agent, enforce OnlyAnswerWhenAsked.
        if (speaker?.Role == AgentRole.Judge)
        {
            if (speaker.OnlyAnswerWhenAsked && !isJudgeAnswer)
                return false;

            return true; // judge answers are allowed
        }

        // Observers can only communicate with their connected attorney (and judge).
        if (speaker?.Role == AgentRole.Observer)
        {
            if (recipient.Role == AgentRole.Judge)
                return true;

            if (speaker.CanCommunicateWithJury == false && (recipient.Role == AgentRole.Juror || recipient.Role == AgentRole.AlternateJuror))
                return false;

            if (recipient.Role == AgentRole.Lawyer)
            {
                if (string.IsNullOrWhiteSpace(speaker.ConnectedAttorneyName))
                    return false;

                return Comparer.Equals(speaker.ConnectedAttorneyName, recipient.Name);
            }

            return false;
        }

        // Jurors cannot message lawyers (one-way: lawyers -> jurors).
        if (speaker?.Role == AgentRole.Juror || speaker?.Role == AgentRole.AlternateJuror)
        {
            if (recipient.Role == AgentRole.Lawyer)
                return false;

            // Jurors may only communicate with each other via jury deliberation, not here.
            // Allow juror->juror within the jury.
            if (recipient.Role == AgentRole.Juror || recipient.Role == AgentRole.AlternateJuror)
                return true;

            // Jurors may address judge (only as asked) but this simulation treats judge as responder, so block.
            return false;
        }

        // Lawyers -> Jurors one-way
        if (speaker?.Role == AgentRole.Lawyer)
        {
            if (recipient.Role == AgentRole.Juror || recipient.Role == AgentRole.AlternateJuror)
            {
                // Team restrictions: rely on CommunicationTeam affinity.
                // If sender is neutral, allow. If sender is Plaintiff, only allow recipients whose team isn't Defendant (neutral allowed).
                // NOTE: In this app, jurors are not explicitly tagged with CommunicationTeam, so we default to allow juror reception.
                return true;
            }

            // Lawyers do not message other roles directly in this runtime chat model (besides judge/jury).
            if (recipient.Role == AgentRole.Judge)
                return true;

            if (recipient.Role == AgentRole.Lawyer)
                return false;

            return false;
        }

        // Witness/reporters/clients: conservative defaults.
        // They may only be heard by judge/jurors for record-keeping.
        if (speaker?.Role == AgentRole.Witness || speaker?.Role == AgentRole.Reporter || speaker?.Role == AgentRole.Client)
        {
            if (recipient.Role == AgentRole.Judge)
                return true;

            if (recipient.Role == AgentRole.Juror || recipient.Role == AgentRole.AlternateJuror)
                return true;

            return false;
        }

        // Default deny for unknown or unsupported combinations.
        return false;
    }

    public static IReadOnlyCollection<Agent> FilterRecipients(
        Agent? speaker,
        IEnumerable<Agent> candidateRecipients,
        bool isJudgeAnswer)
    {
        return candidateRecipients
            .Where(r => CanCommunicate(speaker, r, isJudgeAnswer))
            .ToList();
    }
}

