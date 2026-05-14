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
