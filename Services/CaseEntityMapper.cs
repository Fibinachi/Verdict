using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Maps extracted entities (charges, evidence, witnesses) from a transcript
/// onto a CaseFile and recalculates exposure.
/// </summary>
public interface ICaseEntityMapper
{
    string MapToCaseFile(CaseFile caseFile, ExtractedEntities entities);
}

public class CaseEntityMapper : ICaseEntityMapper
{
    private readonly IEvidenceAnalysisService _evidenceService;

    public CaseEntityMapper(IEvidenceAnalysisService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    public string MapToCaseFile(CaseFile caseFile, ExtractedEntities entities)
    {
        var details = new List<string>();

        // Map parties (plaintiffs and defendants)
        if (entities.Plaintiffs.Count > 0)
        {
            caseFile.Plaintiffs.Clear();
            foreach (var p in entities.Plaintiffs)
                caseFile.Plaintiffs.Add(p);
            details.Add($"{entities.Plaintiffs.Count} plaintiff(s)");
        }
        if (entities.Defendants.Count > 0)
        {
            caseFile.Defendants.Clear();
            foreach (var d in entities.Defendants)
                caseFile.Defendants.Add(d);
            details.Add($"{entities.Defendants.Count} defendant(s)");
        }

        // Map attorneys
        if (!string.IsNullOrEmpty(entities.PlaintiffAttorney))
        {
            caseFile.PlaintiffAttorney = entities.PlaintiffAttorney;
            details.Add($"plaintiff atty: {entities.PlaintiffAttorney}");
        }
        if (!string.IsNullOrEmpty(entities.DefenseAttorney))
        {
            caseFile.DefenseAttorney = entities.DefenseAttorney;
            details.Add($"defense atty: {entities.DefenseAttorney}");
        }

        // Map charges to Pleadings
        foreach (var charge in entities.Charges)
        {
            caseFile.Pleadings.Add(new EvidenceDocument
            {
                FileName = charge.Name,
                Summary = $"{charge.Description} (Statute: {charge.Statute}, {charge.Severity})"
            });
        }
        if (entities.Charges.Count > 0)
            details.Add($"{entities.Charges.Count} charge(s)");

        // Map evidence
        foreach (var ev in entities.Evidence)
        {
            var doc = new EvidenceDocument
            {
                FileName = $"{ev.Type}: {ev.Description}",
                Summary = ev.Description,
                EvidenceStrength = ev.Strength,
                IsDiscoveryComplete = true
            };
            doc.ExhibitNumber = caseFile.Evidence.Count + 1;

            // Side indexing: fall back to attorney-known info when available.
            // Since extracted evidence doesn't include an offering attorney today,
            // we conservatively mark it as plaintiff-side.
            doc.IsOfferedByPlaintiffSide = true;
            doc.PlaintiffSideExhibitNumber = caseFile.Evidence.Count(e => e.IsOfferedByPlaintiffSide) + 1;

            caseFile.Evidence.Add(doc);
        }
        if (entities.Evidence.Count > 0)
            details.Add($"{entities.Evidence.Count} evidence item(s)");

        // Legal issues → GeneralPrompt
        if (entities.LegalIssues.Any())
        {
            caseFile.GeneralPrompt = $"Legal Issues: {string.Join("; ", entities.LegalIssues)}\n{caseFile.GeneralPrompt}";
            details.Add("legal issues");
        }

        // Cause of action
        if (!string.IsNullOrEmpty(entities.CauseOfAction))
        {
            caseFile.JurisdictionSpecifics =
                $"Cause of Action: {entities.CauseOfAction}\n{caseFile.JurisdictionSpecifics}";
            details.Add($"cause: {entities.CauseOfAction}");
        }

        // Case summary → truncated case name
        if (!string.IsNullOrEmpty(entities.CaseSummary))
        {
            caseFile.CaseName = entities.CaseSummary.Length > 50
                ? entities.CaseSummary[..50] + "..."
                : entities.CaseSummary;
        }

        // Recalculate exposure
        if (entities.Evidence.Any())
            _evidenceService.CalculateExposure(caseFile);

        return details.Count > 0 ? string.Join(", ", details) : "no entities found";
    }
}
