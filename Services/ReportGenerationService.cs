using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

// TODO(2026-10-04): PDF report generation was removed for licensing reasons
// (itext7 is AGPL; QuestPDF commercial terms conflict with PolyForm Noncommercial).
// Reintroduce with a license-compatible PDF library when ready.
public interface IReportGenerationService
{
    string BuildTextReport(CaseFile caseFile, IEnumerable<Agent> allAgents);
}

public class ReportGenerationService : IReportGenerationService
{
    public string BuildTextReport(CaseFile caseFile, IEnumerable<Agent> allAgents)
    {
        var agents = allAgents.ToList();
        var jurors = agents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        double avgLean = jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0.5;
        int proCount = jurors.Count(j => j.VerdictLean > 0.6);
        int defCount = jurors.Count(j => j.VerdictLean < 0.4);
        int undecidedCount = jurors.Count(j => j.VerdictLean >= 0.4 && j.VerdictLean <= 0.6);

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("===================================================================");
        sb.AppendLine($"  CASE REPORT â€” {caseFile.CaseName ?? "Untitled Case"}");
        sb.AppendLine($"  Court: {caseFile.CourtName ?? "Superior Court"}");
        sb.AppendLine($"  Case No: {caseFile.CaseNumber ?? "00-0000"}");
        sb.AppendLine($"  Mode: {caseFile.Mode} | Phase: {caseFile.TrialPhase} | Stage: {caseFile.CurrentDebateStage}");
        sb.AppendLine($"  Jurisdiction: {caseFile.Jurisdiction}" + (string.IsNullOrEmpty(caseFile.JurisdictionSpecifics) ? "" : $" â€” {caseFile.JurisdictionSpecifics}"));
        sb.AppendLine($"  Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}");
        sb.AppendLine("===================================================================");
        sb.AppendLine();

        // Case prompts
        if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt) || !string.IsNullOrEmpty(caseFile.DefensePrompt))
        {
            sb.AppendLine("â”€â”€â”€ ATTORNEY DIRECTIVES â”€â”€â”€");
            if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt))
                sb.AppendLine($"  Prosecution/Plaintiff: {caseFile.ProsecutionPlaintiffPrompt}");
            if (!string.IsNullOrEmpty(caseFile.DefensePrompt))
                sb.AppendLine($"  Defense: {caseFile.DefensePrompt}");
            sb.AppendLine();
        }

        // Jury instructions summary
        if (caseFile.Instructions != null && !string.IsNullOrEmpty(caseFile.Instructions.Text))
        {
            sb.AppendLine("â”€â”€â”€ JURY INSTRUCTIONS â”€â”€â”€");
            sb.AppendLine($"  {caseFile.Instructions.Text[..Math.Min(300, caseFile.Instructions.Text.Length)]}...");
            sb.AppendLine();
        }

        // Financials
        if (caseFile.EstimatedSettlement > 0 || caseFile.InsuranceReserve > 0)
        {
            sb.AppendLine("â”€â”€â”€ FINANCIAL ANALYSIS â”€â”€â”€");
            if (caseFile.EstimatedSettlement > 0)
                sb.AppendLine($"  Estimated Settlement: ${caseFile.EstimatedSettlement:N0}");
            if (caseFile.InsuranceReserve > 0)
                sb.AppendLine($"  Recommended Reserve:   ${caseFile.InsuranceReserve:N0}");
            sb.AppendLine();
        }

        // Evidence
        sb.AppendLine("â”€â”€â”€ EVIDENCE LOG â”€â”€â”€");
        if (caseFile.Evidence.Any())
        {
            foreach (var doc in caseFile.Evidence)
            {
                sb.AppendLine($"  Exhibit {doc.ExhibitNumber}: {doc.FileName}");
                sb.AppendLine($"    Summary:        {doc.Summary}");
                sb.AppendLine($"    Type:           {doc.MediaType}");
                sb.AppendLine($"    Strength:       {doc.EvidenceStrength:P0}");
                sb.AppendLine($"    Est. Damages:   ${doc.EstimatedDamages:N0}");
                sb.AppendLine($"    Offered By:     {doc.OfferingAttorney ?? "Unknown"}");
                if (!string.IsNullOrEmpty(doc.DetailedAnalysis))
                    sb.AppendLine($"    Analysis:       {doc.DetailedAnalysis[..Math.Min(200, doc.DetailedAnalysis.Length)]}...");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No evidence has been entered.");
            sb.AppendLine();
        }

        // Courtroom roster
        sb.AppendLine("â”€â”€â”€ COURTROOM ROSTER â”€â”€â”€");
        var occupiedAgents = agents.Where(a => a.IsOccupied).ToList();
        if (occupiedAgents.Any())
        {
            foreach (var agent in occupiedAgents.OrderBy(a => a.Role))
            {
                var line = $"  [{agent.Role}] {agent.Name}";
                if (agent.CanVote)
                    line += $" | Lean: {agent.VerdictLean:P0} | Sentiment: {agent.Sentiment:P0}";
                sb.AppendLine(line);
            }
        }
        else
        {
            sb.AppendLine("  No agents seated.");
        }
        sb.AppendLine();

        // Jury analysis
        if (jurors.Any())
        {
            sb.AppendLine("â”€â”€â”€ JURY ANALYSIS â”€â”€â”€");
            sb.AppendLine($"  Total Jurors:        {jurors.Count}");
            sb.AppendLine($"  Average Lean:        {avgLean:P0} (0% = Defense, 100% = Plaintiff/Prosecution)");
            sb.AppendLine($"  Lean Prosecution:    {proCount}");
            sb.AppendLine($"  Lean Defense:        {defCount}");
            sb.AppendLine($"  Undecided:           {undecidedCount}");
            sb.AppendLine();

            sb.AppendLine("  Individual Juror Breakdown:");
            sb.AppendLine("  Name                 | Role | Bias  | Lean | Sent. | Damages");
            sb.AppendLine("  " + new string('-', 70));
            foreach (var juror in jurors)
            {
                sb.AppendLine($"  {juror.Name,-20} | {juror.Role,-5} | {juror.Bias,5:+0.00;-0.00; 0.00} | {juror.VerdictLean,4:P0} | {juror.Sentiment,4:P0} | ${juror.ConsideredDamages,10:N0}");
            }
            sb.AppendLine();

            // Memories summary
            sb.AppendLine("â”€â”€â”€ JUROR MEMORY SUMMARIES â”€â”€â”€");
            foreach (var juror in jurors)
            {
                var memories = juror.TrialEvents.Concat(juror.Memories)
                    .OrderByDescending(m => m.Strength).Take(5).ToList();
                sb.AppendLine($"  {juror.Name} (Bias: {juror.Bias:+0.00;-0.00; 0.00}):");
                foreach (var m in memories)
                    sb.AppendLine($"    [{m.Strength:P0}] {m.Content[..Math.Min(100, m.Content.Length)]}");
            }
            sb.AppendLine();

            // Predicted outcome
            sb.AppendLine("â”€â”€â”€ PREDICTED OUTCOME â”€â”€â”€");
            string outcome = avgLean > 0.55
                ? "Predicted Verdict: Likely for Plaintiff/Prosecution"
                : avgLean < 0.45
                    ? "Predicted Verdict: Likely for Defendant"
                    : "Predicted Verdict: Too close to call / Hung jury possible";
            sb.AppendLine($"  {outcome}");

            // Note the burden of proof threshold if criminal
            if (caseFile.Mode == CaseMode.Criminal)
            {
                double convictionThreshold = BurdenOfProof.GetConvictionThreshold(true);
                int aboveThreshold = jurors.Count(j => j.VerdictLean > convictionThreshold);
                sb.AppendLine($"  Criminal case: {aboveThreshold} of {jurors.Count} jurors above reasonable-doubt threshold ({convictionThreshold:P0})");
                sb.AppendLine($"  (Jurors between 0.50-{convictionThreshold:P0} have reasonable doubt = functionally NOT GUILTY)");
            }
            sb.AppendLine();
        }

        // Event log (last 30)
        sb.AppendLine("â”€â”€â”€ EVENT LOG (Recent) â”€â”€â”€");
        var recentEvents = caseFile.EventLog.TakeLast(30).ToList();
        if (recentEvents.Any())
        {
            foreach (var evt in recentEvents)
                sb.AppendLine($"  [{evt.Timestamp:HH:mm:ss}] {evt.Description}");
        }
        else
        {
            sb.AppendLine("  No events logged.");
        }

        sb.AppendLine();
        sb.AppendLine("===================================================================");
        sb.AppendLine("  END OF REPORT");
        sb.AppendLine("===================================================================");

        return sb.ToString();
    }
}