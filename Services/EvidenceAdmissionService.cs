using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;

namespace Verdict.Services;

public interface IEvidenceAdmissionService
{
    Task<EvidenceAdmissionResult> AddEvidenceAsync(
        string fileName,
        string path,
        string summary,
        string? existingDetailedAnalysis,
        string offeringAttorney,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IAgentInteractionService agentInteraction);

    Task<EvidenceAdmissionResult> AddTestimonyEvidenceAsync(
        string witnessName,
        string testimonyText,
        string offeringAttorney,
        bool offeredByPlaintiff,
        string? targetCharacterNameForContext,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IAgentInteractionService agentInteraction);
}

public sealed class EvidenceAdmissionResult
{
    public required EvidenceDocument Document { get; init; }
    public string TranscriptOutputAddition { get; init; } = string.Empty;
    public string? ReporterSummary { get; init; }
}

public sealed class EvidenceAdmissionService : IEvidenceAdmissionService
{
    private readonly IEvidenceAnalysisService _evidenceService;
    private readonly IJuryCalculationService _juryCalc;
    private readonly IYoke _yoke;


// IYoke is a tiny indirection to keep this service decoupled from MainViewModel
    // (it supplies the reporter and exposes occupied jurors).
    public EvidenceAdmissionService(
        IEvidenceAnalysisService evidenceService,
        IJuryCalculationService juryCalc,
        IYoke yoke)
    {
        _evidenceService = evidenceService ?? throw new ArgumentNullException(nameof(evidenceService));
        _juryCalc = juryCalc ?? throw new ArgumentNullException(nameof(juryCalc));
        _yoke = yoke ?? throw new ArgumentNullException(nameof(yoke));
    }


    public async Task<EvidenceAdmissionResult> AddEvidenceAsync(
        string fileName,
        string path,
        string summary,
        string? existingDetailedAnalysis,
        string offeringAttorney,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IAgentInteractionService agentInteraction)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Value cannot be null/empty.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Value cannot be null/empty.", nameof(path));
        if (currentCase is null) throw new ArgumentNullException(nameof(currentCase));
        if (allAgents is null) throw new ArgumentNullException(nameof(allAgents));
        if (agentInteraction is null) throw new ArgumentNullException(nameof(agentInteraction));

        string fileContent = _evidenceService.ReadFileContent(path);

        string detailedAnalysis;
        double llmDamages;
        if (!string.IsNullOrWhiteSpace(existingDetailedAnalysis))
        {
            detailedAnalysis = existingDetailedAnalysis;
            llmDamages = EvidenceAnalysisService.ExtractTotalDamagesFromAnalysisPublic(existingDetailedAnalysis);
        }
        else
        {
            (detailedAnalysis, llmDamages) = await _evidenceService.GenerateDocumentAnalysisAsync(
                fileName,
                fileContent,
                summary,
                currentCase,
                agentInteraction);
        }

        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        string mediaType = extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" ? "Image" : "Document";

        var doc = new EvidenceDocument
        {
            FileName = fileName,
            FilePath = path,
            Summary = summary,
            ExhibitNumber = currentCase.Evidence.Count + 1,
            MediaType = mediaType,
            DetailedAnalysis = detailedAnalysis,
            OfferingAttorney = offeringAttorney,
            IsOfferedByPlaintiffSide = !string.IsNullOrWhiteSpace(offeringAttorney)
                                       && !string.IsNullOrWhiteSpace(currentCase.PlaintiffAttorney)
                                       && offeringAttorney.Trim().Equals(currentCase.PlaintiffAttorney.Trim(), StringComparison.OrdinalIgnoreCase),
            IsDiscoveryComplete = currentCase.TrialPhase != TrialPhase.Trial
        };

        if (llmDamages > 0)
        {
            doc.EstimatedDamages = llmDamages;
            doc.EvidenceStrength = 0.85;
        }
        else
        {
            _evidenceService.AssessStrength(doc, summary, fileContent);
        }

        int nextPlaintiffSide = currentCase.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;
        int nextDefenseSide = currentCase.Evidence.Count(e => !e.IsOfferedByPlaintiffSide) + 1;
        if (doc.IsOfferedByPlaintiffSide) doc.PlaintiffSideExhibitNumber = nextPlaintiffSide;
        else doc.DefenseSideExhibitNumber = nextDefenseSide;

        currentCase.Evidence.Add(doc);
        _evidenceService.CalculateExposure(currentCase);
        _juryCalc.ApplyEvidenceInfluence(allAgents, doc, currentCase.Mode);


        string phasePrefix = currentCase.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "EVIDENCE"
        };

        string attorneyPrefix = string.IsNullOrEmpty(offeringAttorney) ? "" : $"Offered by: {offeringAttorney}\n";

        string transcriptAddition = $"[{phasePrefix}] Exhibit {doc.ExhibitNumber}: {fileName}\n" +
                                    $"{attorneyPrefix}" +
                                    $"{summary}\n" +
                                    $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}, " +
                                    $"Settlement: ${currentCase.EstimatedSettlement:N0}, " +
                                    $"Reserve: ${currentCase.InsuranceReserve:N0}";

        // Reporter and internalization
        var reporter = _yoke.Reporter;
        var defaultModel = currentCase.AvailableModels.FirstOrDefault();

        string? reporterSummary = null;
        if (reporter is not null)
        {
            var reporterModel = ResolveModelForAgent(currentCase, reporter) ?? defaultModel;
            if (reporterModel is not null)
            {
                reporterSummary = await agentInteraction.SummarizeEvidenceForRecordAsync(reporter, doc, currentCase, reporterModel);
                reporter.DocumentCount++;
                transcriptAddition += $"\n{reporter.Name}: {reporterSummary}";

                var jurors = _yoke.OccupiedJurors;
                foreach (var juror in jurors)
                {
                    var jurorModel = ResolveModelForAgent(currentCase, juror) ?? defaultModel;
                    string reaction = jurorModel is not null
                        ? await agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                        : reporterSummary;

                    juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
                }
            }
        }

        // Fallback if no reporter model
        reporterSummary ??= $"Exhibit {doc.ExhibitNumber}, {doc.FileName}, admitted into evidence. {summary}";


        // If reporter wasn't usable, internalize anyway
        if (reporter is null || _yoke.ReporterModelUnavailable)
        {
            foreach (var juror in _yoke.OccupiedJurors)
            {
                juror.RecordTrialEvent(reporterSummary, juror.Bias * 0.1);
            }
        }

        return new EvidenceAdmissionResult { Document = doc, ReporterSummary = reporterSummary, TranscriptOutputAddition = transcriptAddition };
    }

    public async Task<EvidenceAdmissionResult> AddTestimonyEvidenceAsync(
        string witnessName,
        string testimonyText,
        string offeringAttorney,
        bool offeredByPlaintiff,
        string? targetCharacterNameForContext,
        CaseFile currentCase,
        IEnumerable<Agent> allAgents,
        IAgentInteractionService agentInteraction)
    {
        if (string.IsNullOrWhiteSpace(testimonyText))
            throw new ArgumentException("Testimony text cannot be empty.", nameof(testimonyText));

        witnessName = string.IsNullOrWhiteSpace(witnessName) ? (targetCharacterNameForContext ?? "Witness") : witnessName.Trim();
        offeringAttorney ??= string.Empty;

        string callerSideLabel = offeredByPlaintiff ? "Plaintiff" : "Defense";

        string phasePrefix = currentCase.TrialPhase switch
        {
            TrialPhase.Discovery => "DISCOVERY",
            TrialPhase.Pretrial => "PRETRIAL",
            TrialPhase.Trial => "TRIAL",
            _ => "EVIDENCE"
        };

        string summary = $"Plaintiff/Defense calls {witnessName}.\n\n" +
                          $"Caller side: {callerSideLabel}.\n\n" +
                          $"Testimony (recorded verbatim by the clerk):\n{testimonyText}";

        var doc = new EvidenceDocument
        {
            FileName = $"Testimony - {witnessName}",
            FilePath = string.Empty,
            Summary = summary,
            ExhibitNumber = currentCase.Evidence.Count + 1,
            MediaType = "Document",
            DetailedAnalysis = null,
            OfferingAttorney = offeringAttorney,
            IsOfferedByPlaintiffSide = offeredByPlaintiff,
            IsDiscoveryComplete = currentCase.TrialPhase != TrialPhase.Trial
        };

        _evidenceService.AssessStrength(doc, summary, testimonyText);

        int nextPlaintiffSide = currentCase.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;
        int nextDefenseSide = currentCase.Evidence.Count(e => !e.IsOfferedByPlaintiffSide) + 1;
        if (doc.IsOfferedByPlaintiffSide) doc.PlaintiffSideExhibitNumber = nextPlaintiffSide;
        else doc.DefenseSideExhibitNumber = nextDefenseSide;

        currentCase.Evidence.Add(doc);
        _evidenceService.CalculateExposure(currentCase);
        _juryCalc.ApplyEvidenceInfluence(allAgents, doc, currentCase.Mode);


        string attorneyPrefix = string.IsNullOrEmpty(offeringAttorney) ? "" : $"Offered by: {offeringAttorney}\n";

        string transcriptAddition = $"[{phasePrefix}] Testimony Exhibit {doc.ExhibitNumber}: {doc.FileName}\n" +
                                    $"{attorneyPrefix}" +
                                    $"Strength: {doc.EvidenceStrength:P0}, Est. Damages: ${doc.EstimatedDamages:N0}, " +
                                    $"Settlement: ${currentCase.EstimatedSettlement:N0}, " +
                                    $"Reserve: ${currentCase.InsuranceReserve:N0}";

        var reporter = _yoke.Reporter;
        var defaultModel = currentCase.AvailableModels.FirstOrDefault();

        string reporterSummary;
        if (reporter is not null)
        {
            var reporterModel = ResolveModelForAgent(currentCase, reporter) ?? defaultModel;
            if (reporterModel is not null)
            {
                reporterSummary = await agentInteraction.SummarizeEvidenceForRecordAsync(reporter, doc, currentCase, reporterModel);
                reporter.DocumentCount++;
                transcriptAddition += $"\n{reporter.Name}: {reporterSummary}";
            }
            else
            {
                reporterSummary = $"Exhibit {doc.ExhibitNumber}, Testimony of {witnessName}, admitted into evidence.";
            }
        }
        else
        {
            reporterSummary = $"Exhibit {doc.ExhibitNumber}, Testimony of {witnessName}, admitted into evidence.";
        }

        // Jurors internalize reporter summary
        foreach (var juror in _yoke.OccupiedJurors)
        {
            var jurorModel = ResolveModelForAgent(currentCase, juror) ?? defaultModel;
            string reaction = jurorModel is not null
                ? await agentInteraction.InternalizeCourtRecordAsync(juror, reporterSummary, jurorModel)
                : reporterSummary;

            juror.RecordTrialEvent(reaction, juror.Bias * 0.1);
        }

        return new EvidenceAdmissionResult { Document = doc, ReporterSummary = reporterSummary, TranscriptOutputAddition = transcriptAddition };
    }

    private static AIModelConfiguration? ResolveModelForAgent(CaseFile currentCase, Agent agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.SelectedModel))
            return currentCase.AvailableModels.FirstOrDefault(m => m.FriendlyName == agent.SelectedModel);

        return null;
    }
}

// Minimal abstraction to avoid cyclic dependency on MainViewModel.
public interface IYoke
{
    Agent? Reporter { get; }
    IReadOnlyList<Agent> OccupiedJurors { get; }
    bool ReporterModelUnavailable { get; }
}

