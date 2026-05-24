using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.ViewModels;

namespace Verdict.Services;

/// <summary>
/// Built-in demo scenarios that preload evidence/testimony so the jury is gamified immediately.
/// Evidence is injected as testimony/text exhibits (no external PDFs required).
/// </summary>
public static class BuiltInCaseScenarios
{
    public enum BuiltInScenario
    {
        RebrandedOJTrial,
        RebrandedSaccoVanzetti,
        RebrandedExxonTrial,
        CredibilityGapEitherWayTrial,
        RebrandedAppleRiver
    }

    public static string ScenarioDisplayName(BuiltInScenario s) => s switch
    {
        BuiltInScenario.RebrandedOJTrial => "Apple Juice Trial",
        BuiltInScenario.RebrandedSaccoVanzetti => "Sacco & Vanzetti",
        BuiltInScenario.RebrandedExxonTrial => "Rebranded Exxon Trial",
        BuiltInScenario.CredibilityGapEitherWayTrial => "The Credibility Gap",
        BuiltInScenario.RebrandedAppleRiver => "Apple River Confrontation",
        _ => s.ToString()
    };

    public static CaseFile BuildScenarioCaseFile(BuiltInScenario scenario)
    {
        // Start from default case settings, then override scenario-specific fields.
        var c = new CaseFile();

        // Parties/attorneys + mode defaults.
        switch (scenario)
        {
            case BuiltInScenario.RebrandedExxonTrial:
                c.Mode = CaseMode.Civil;
                c.CaseName = scenario.ToString();
                c.Plaintiffs = new List<string> { "The Community Plaintiffs" };
                c.Defendants = new List<string> { "ExxonMobil (Rebranded)" };
                c.PlaintiffAttorney = "Plaintiffs' Counsel";
                c.DefenseAttorney = "Exxon Defense Counsel";
                break;

            case BuiltInScenario.RebrandedOJTrial:
            case BuiltInScenario.RebrandedSaccoVanzetti:
                c.Mode = CaseMode.Criminal;
                c.CaseName = scenario.ToString();
                c.Plaintiffs = new List<string> { "The People" };
                c.Defendants = new List<string> { scenario == BuiltInScenario.RebrandedOJTrial
                    ? "OJ (Rebranded Defendant)"
                    : "Sacco & Vanzetti (Rebranded)" };
                c.PlaintiffAttorney = "Prosecution";
                c.DefenseAttorney = "Defense";

                if (scenario == BuiltInScenario.RebrandedSaccoVanzetti)
                    c.Defendants = new List<string> { "Sacco (Rebranded)", "Vanzetti (Rebranded)" };

                break;

            case BuiltInScenario.RebrandedAppleRiver:
                c.Mode = CaseMode.Criminal;
                c.CaseName = "State v. Miller (Apple River)";
                c.Plaintiffs = new List<string> { "The State" };
                c.Defendants = new List<string> { "Mark Miller (Rebranded)" };
                c.PlaintiffAttorney = "District Attorney";
                c.DefenseAttorney = "Defense Counsel";
                break;

            case BuiltInScenario.CredibilityGapEitherWayTrial:
            default:
                // Keep this as civil by default; evidence/juror logic supports both modes.
                c.Mode = CaseMode.Civil;
                c.CaseName = scenario.ToString();
                c.Plaintiffs = new List<string> { "The People (Civil Claimants)" };
                c.Defendants = new List<string> { "The Other Party" };
                c.PlaintiffAttorney = "Plaintiffs' Counsel";
                c.DefenseAttorney = "Defense";
                break;
        }

        // Evidence strength/estimated damages are computed from text; we still set civil exposure hints.
        if (c.Mode == CaseMode.Civil)
        {
            c.EstimatedSettlement = 25000000;
            c.InsuranceReserve = 5000000;

            // Causes of action (toy label list; juror logic uses other signals but UI can show this)
            c.Verdict.CausesOfAction = new List<CauseOfAction>
            {
                new CauseOfAction { Name = "Environmental Tort (Rebranded)" }
            };

            // Clear charges for civil scenarios if the model stores them.
            c.Verdict.Charges = new List<Charge>();
        }
        else
        {
            c.EstimatedSettlement = 0;
            c.InsuranceReserve = 0;

            // Charges (toy label list)
            c.Verdict.Charges = new List<Charge>
            {
                new Charge { Name = "Serious Felony (Rebranded)" }
            };

            // Clear causes for criminal scenarios if the model stores them.
            c.Verdict.CausesOfAction = new List<CauseOfAction>();
        }

        // Bias factors: preserve CaseFile defaults; callers may override via Settings.
        return c;
    }

    public static async Task PreloadEvidenceAsync(MainViewModel vm, BuiltInScenario scenario)
    {
        // Ensure jury is present; evidence admission populates evidence memories internally.
        if (!vm.Jurors.Any())
            await vm.GenerateJury();

        // Admit testimony exhibits (reset handled by admission/internalization flow).
        switch (scenario)
        {
            case BuiltInScenario.RebrandedOJTrial:
                await AdmitOJEvidence(vm);
                break;
            case BuiltInScenario.RebrandedSaccoVanzetti:
                await AdmitSaccoVanzettiEvidence(vm);
                break;
            case BuiltInScenario.RebrandedExxonTrial:
                await AdmitExxonEvidence(vm);
                break;
            case BuiltInScenario.RebrandedAppleRiver:
                await AdmitAppleRiverEvidence(vm);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        // Make sure exhibits refresh.
        vm.RefreshExhibits();
    }

    private static async Task AdmitOJEvidence(MainViewModel vm)
    {
        // Prosecution narrative (evidence for guilt)
        await vm.AddTestimonyEvidence(
            witnessName: "Detective Rowan",
            testimonyText:
                "I reviewed the incident timeline and documented inconsistencies. The prosecution presented forensic indicators that strongly connect the defendant to the events. I believe the defendant's explanation does not match the physical timeline.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true,
            targetCharacterNameForContext: "Detective Rowan");

        await vm.AddTestimonyEvidence(
            witnessName: "Forensic Analyst Kline",
            testimonyText:
                "The forensic comparison showed matches consistent with the defendant. While no test is perfect, the pattern of results supports the prosecution's theory. I did not find alternative sources that convincingly explain the match.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true);

        await vm.AddTestimonyEvidence(
            witnessName: "Eyewitness Maya Stone",
            testimonyText:
                "I saw a struggle and later identified the defendant. I understand memories can be imperfect, but my account is based on what I observed during the incident.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true);

        // Defense narrative (reasonable doubt)
        await vm.AddTestimonyEvidence(
            witnessName: "Defense Expert Dr. Ortiz",
            testimonyText:
                "The forensic methods have known limitations. Contamination and statistical interpretation can affect conclusions. There are plausible alternative explanations consistent with the defense position.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Officer Blake",
            testimonyText:
                "My observations show gaps in the timeline. Evidence handling procedures raise questions. I cannot rule out mistakes or mislabeling.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Character Witness Lena Park",
            testimonyText:
                "I know the defendant as a person of character and community ties. I believe they are being treated unfairly.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);
    }

    private static async Task AdmitSaccoVanzettiEvidence(MainViewModel vm)
    {
        // Prosecution (conviction narrative)
        await vm.AddTestimonyEvidence(
            witnessName: "Inspector Moreau",
            testimonyText:
                "We obtained witness accounts and documents that place the defendants at the relevant location. The prosecution contends the evidence demonstrates intent and participation beyond reasonable doubt.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true);

        await vm.AddTestimonyEvidence(
            witnessName: "Ballistics Examiner Wills",
            testimonyText:
                "The ballistic analysis indicated consistency between recovered materials and those associated with the defendants. The matches were not incidental.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true);

        await vm.AddTestimonyEvidence(
            witnessName: "Merchant Elias Reed",
            testimonyText:
                "I recall seeing the defendants near the incident. I am confident in my memory because of the circumstances and subsequent events.",
            offeringAttorney: "Prosecution",
            offeredByPlaintiff: true);

        // Defense (reasonable doubt narrative)
        await vm.AddTestimonyEvidence(
            witnessName: "Defense Attorney Samuels",
            testimonyText:
                "The prosecution's timeline relies on shifting testimony. There is evidence of coercion concerns and unreliable identification.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Linguist Expert Dr. Hart",
            testimonyText:
                "Translation issues can distort meaning. Some documentary interpretations presented by the state may be inaccurate.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Community Leader Rosa Quintero",
            testimonyText:
                "I believe the defendants have community support. The case appears driven by politics rather than purely reliable facts.",
            offeringAttorney: "Defense",
            offeredByPlaintiff: false);
    }

    private static async Task AdmitExxonEvidence(MainViewModel vm)
    {
        // Plaintiffs (liability/damages)
        await vm.AddTestimonyEvidence(
            witnessName: "Coastal Scientist Dr. Nguyen",
            testimonyText:
                "Sampling indicates contamination consistent with industrial activity. The extent of ecological harm supports the plaintiffs' theory of causation and foreseeable risk.",
            offeringAttorney: "Plaintiffs' Counsel",
            offeredByPlaintiff: true);

        await vm.AddTestimonyEvidence(
            witnessName: "Small Business Owner Carla James",
            testimonyText:
                "After the spill, tourism collapsed. I lost revenue, and my community has suffered ongoing economic damage.",
            offeringAttorney: "Plaintiffs' Counsel",
            offeredByPlaintiff: true);

        await vm.AddTestimonyEvidence(
            witnessName: "Documentarian Martin Pike",
            testimonyText:
                "Internal reports show warnings about safety and mitigation. The plaintiffs argue the defendant acted with disregard.",
            offeringAttorney: "Plaintiffs' Counsel",
            offeredByPlaintiff: true);

        // Defense (no causation / lesser damages)
        await vm.AddTestimonyEvidence(
            witnessName: "Exxon Risk Manager Trevor Shaw",
            testimonyText:
                "We followed safety protocols and responded appropriately. Other sources could explain the contamination. The plaintiffs' damages estimates are exaggerated.",
            offeringAttorney: "Exxon Defense Counsel",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Independent Analyst Prof. Chen",
            testimonyText:
                "The data has uncertainties. Correlation does not equal causation. It is possible the harm arises from multiple contributing factors.",
            offeringAttorney: "Exxon Defense Counsel",
            offeredByPlaintiff: false);

        await vm.AddTestimonyEvidence(
            witnessName: "Economist Dana Brooks",
            testimonyText:
                "Economic harm should be modeled with baseline controls. Some claimed losses overlap with normal seasonal fluctuations and unrelated market trends.",
            offeringAttorney: "Exxon Defense Counsel",
            offeredByPlaintiff: false);
    }
}
