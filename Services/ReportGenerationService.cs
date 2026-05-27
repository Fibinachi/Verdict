using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Verdict.Models;

namespace Verdict.Services;

public interface IReportGenerationService
{
    void GenerateReport(string filePath, CaseFile caseFile, IEnumerable<Agent> allAgents);
    string BuildTextReport(CaseFile caseFile, IEnumerable<Agent> allAgents);
}

public class ReportGenerationService : IReportGenerationService
{
    static ReportGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void GenerateReport(string filePath, CaseFile caseFile, IEnumerable<Agent> allAgents)
    {
        if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            filePath += ".pdf";

        var agents = allAgents.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Times New Roman"));

                page.Header().Element(c => ComposeHeader(c, caseFile));
                page.Content().Element(c => ComposeContent(c, caseFile, agents));
                page.Footer().Element(c => ComposeFooter(c, caseFile));
            });
        }).GeneratePdf(filePath);
    }

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
        sb.AppendLine($"  CASE REPORT — {caseFile.CaseName ?? "Untitled Case"}");
        sb.AppendLine($"  Court: {caseFile.CourtName ?? "Superior Court"}");
        sb.AppendLine($"  Case No: {caseFile.CaseNumber ?? "00-0000"}");
        sb.AppendLine($"  Mode: {caseFile.Mode} | Phase: {caseFile.TrialPhase} | Stage: {caseFile.CurrentDebateStage}");
        sb.AppendLine($"  Jurisdiction: {caseFile.Jurisdiction}" + (string.IsNullOrEmpty(caseFile.JurisdictionSpecifics) ? "" : $" — {caseFile.JurisdictionSpecifics}"));
        sb.AppendLine($"  Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}");
        sb.AppendLine("===================================================================");
        sb.AppendLine();

        // Case prompts
        if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt) || !string.IsNullOrEmpty(caseFile.DefensePrompt))
        {
            sb.AppendLine("─── ATTORNEY DIRECTIVES ───");
            if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt))
                sb.AppendLine($"  Prosecution/Plaintiff: {caseFile.ProsecutionPlaintiffPrompt}");
            if (!string.IsNullOrEmpty(caseFile.DefensePrompt))
                sb.AppendLine($"  Defense: {caseFile.DefensePrompt}");
            sb.AppendLine();
        }

        // Jury instructions summary
        if (caseFile.Instructions != null && !string.IsNullOrEmpty(caseFile.Instructions.Text))
        {
            sb.AppendLine("─── JURY INSTRUCTIONS ───");
            sb.AppendLine($"  {caseFile.Instructions.Text[..Math.Min(300, caseFile.Instructions.Text.Length)]}...");
            sb.AppendLine();
        }

        // Financials
        if (caseFile.EstimatedSettlement > 0 || caseFile.InsuranceReserve > 0)
        {
            sb.AppendLine("─── FINANCIAL ANALYSIS ───");
            if (caseFile.EstimatedSettlement > 0)
                sb.AppendLine($"  Estimated Settlement: ${caseFile.EstimatedSettlement:N0}");
            if (caseFile.InsuranceReserve > 0)
                sb.AppendLine($"  Recommended Reserve:   ${caseFile.InsuranceReserve:N0}");
            sb.AppendLine();
        }

        // Evidence
        sb.AppendLine("─── EVIDENCE LOG ───");
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
        sb.AppendLine("─── COURTROOM ROSTER ───");
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
            sb.AppendLine("─── JURY ANALYSIS ───");
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
            sb.AppendLine("─── JUROR MEMORY SUMMARIES ───");
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
            sb.AppendLine("─── PREDICTED OUTCOME ───");
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
        sb.AppendLine("─── EVENT LOG (Recent) ───");
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

    private void ComposeHeader(IContainer container, CaseFile caseFile)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().AlignCenter().Text(caseFile.CourtName ?? "Superior Court").Bold().FontSize(14);

            column.Item().AlignCenter().Text(c =>
            {
                if (caseFile.Mode == CaseMode.Civil)
                    c.Span("IN THE CIVIL DIVISION").Bold().FontSize(12);
                else
                    c.Span("IN THE CRIMINAL DIVISION").Bold().FontSize(12);
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);

            column.Item().AlignCenter().Text(caseFile.CaseName ?? "Untitled Case").Bold().FontSize(13);
            column.Item().AlignCenter().Text($"Case No. {caseFile.CaseNumber ?? "00-0000"}").FontSize(11);

            var jurisdictionText = $"Jurisdiction: {caseFile.Jurisdiction}";
            if (!string.IsNullOrEmpty(caseFile.JurisdictionSpecifics))
                jurisdictionText += $" - {caseFile.JurisdictionSpecifics}";
            column.Item().AlignCenter().Text(jurisdictionText).FontSize(10);

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container, CaseFile caseFile, List<Agent> agents)
    {
        var jurors = agents.Where(a => a.IsOccupied && (a.Role == AgentRole.Juror || a.Role == AgentRole.AlternateJuror)).ToList();
        double avgLean = jurors.Any() ? jurors.Average(j => j.VerdictLean) : 0.5;
        int proCount = jurors.Count(j => j.VerdictLean > 0.6);
        int defCount = jurors.Count(j => j.VerdictLean < 0.4);
        int undecidedCount = jurors.Count(j => j.VerdictLean >= 0.4 && j.VerdictLean <= 0.6);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Text("CASE SUMMARY").Bold().FontSize(12);
            column.Item().PaddingLeft(20).Column(c2 =>
            {
                c2.Item().Text($"Case Mode: {caseFile.Mode}");
                c2.Item().Text($"Case Phase: {caseFile.TrialPhase}");
                c2.Item().Text($"Current Debate Stage: {caseFile.CurrentDebateStage}");
                c2.Item().Text($"Total Evidence Items: {caseFile.Evidence.Count}");
                c2.Item().Text($"Total Events Logged: {caseFile.EventLog.Count}");
            });

            column.Item().PaddingVertical(5);

            if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt) || !string.IsNullOrEmpty(caseFile.DefensePrompt))
            {
                column.Item().Text("ATTORNEY DIRECTIVES").Bold().FontSize(12);
                if (!string.IsNullOrEmpty(caseFile.ProsecutionPlaintiffPrompt))
                    column.Item().PaddingLeft(20).Text($"Prosecution/Plaintiff: {caseFile.ProsecutionPlaintiffPrompt}");
                if (!string.IsNullOrEmpty(caseFile.DefensePrompt))
                    column.Item().PaddingLeft(20).Text($"Defense: {caseFile.DefensePrompt}");
                column.Item().PaddingVertical(5);
            }

            if (caseFile.EstimatedSettlement > 0 || caseFile.InsuranceReserve > 0)
            {
                column.Item().Text("FINANCIAL ANALYSIS").Bold().FontSize(12);
                column.Item().PaddingLeft(20).Column(c2 =>
                {
                    if (caseFile.EstimatedSettlement > 0)
                        c2.Item().Text($"Estimated Settlement: ${caseFile.EstimatedSettlement:N0}");
                    if (caseFile.InsuranceReserve > 0)
                        c2.Item().Text($"Recommended Reserve: ${caseFile.InsuranceReserve:N0}");
                });
                column.Item().PaddingVertical(5);
            }

            if (caseFile.Evidence.Any())
            {
                column.Item().Text("EVIDENCE LOG").Bold().FontSize(12);
                foreach (var evidence in caseFile.Evidence)
                {
                    column.Item().PaddingLeft(20).Column(c2 =>
                    {
                        c2.Item().Text($"Exhibit {evidence.ExhibitNumber}: {evidence.Summary ?? evidence.FileName}");
                        c2.Item().Text($"Strength: {evidence.EvidenceStrength:P0} | Est. Damages: ${evidence.EstimatedDamages:N0}");
                    });
                }
                column.Item().PaddingVertical(5);
            }

            column.Item().Text("COURTROOM ROSTER").Bold().FontSize(12);
            var occupiedAgents = agents.Where(a => a.IsOccupied).ToList();
            if (occupiedAgents.Any())
            {
                foreach (var agent in occupiedAgents)
                {
                    var line = $"{agent.Role}: {agent.Name}";
                    if (agent.CanVote)
                        line += $" | Lean: {agent.VerdictLean:P0} | Sentiment: {agent.Sentiment:P0}";
                    column.Item().PaddingLeft(20).Text(line);
                }
            }
            else
            {
                column.Item().PaddingLeft(20).Text("No agents seated.");
            }

            column.Item().PaddingVertical(5);

            if (jurors.Any())
            {
                column.Item().Text("JURY ANALYSIS").Bold().FontSize(12);
                column.Item().PaddingLeft(20).Column(c2 =>
                {
                    c2.Item().Text($"Average Lean: {avgLean:P0} (0% = Defense, 100% = Plaintiff/Prosecution)");
                    c2.Item().Text($"Lean Prosecution/Plaintiff: {proCount}");
                    c2.Item().Text($"Lean Defense: {defCount}");
                    c2.Item().Text($"Undecided: {undecidedCount}");
                });

                column.Item().PaddingVertical(5);
                column.Item().Text("PREDICTED OUTCOME").Bold().FontSize(12);
                column.Item().PaddingLeft(20).Text(result =>
                {
                    var outcome = avgLean > 0.55 ? 
                        "Predicted Verdict: Likely for Plaintiff/Prosecution" : 
                        avgLean < 0.45 ? 
                        "Predicted Verdict: Likely for Defendant" : 
                        "Predicted Verdict: Too close to call / Hung jury possible";
                    
                    result.Span(outcome).Bold();
                });
            }
        });
    }

    private void ComposeFooter(IContainer container, CaseFile caseFile)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}")
                .FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text($"Last Saved: {caseFile.LastSaved:MMMM dd, yyyy}")
                .FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
