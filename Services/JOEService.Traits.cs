using System;
using System.Collections.Generic;
using Verdict.Models;

namespace Verdict.Services;

public static partial class JOEService
{
    private sealed class ToyJurorTraits
    {
        public double AgeYears;
        public double RWA;             // 0..10
        public double SDO;             // 0..10
        public double Punitiveness;    // 0..10
        public double RapeMyth;        // 0..10

        public double Religiosity;     // 0..10
        public double ConservRelig;    // 0..10

        public double PolId;           // -1..1
        public double PolStrength;     // 0..10
        public double PrimaryHistory;  // 0..10
        public double Volunteer;       // 0..10
        public double Donate;          // 0..10
        public double Activism;        // 0..10
        public double OnlinePartisan;  // 0..10

        public double TvCrime;         // 0..20
        public double TvCableNews;     // 0..20
        public double NewsLean;        // -1..1
        public double NewsIntensity;   // 0..30
        public double SmUse;           // 0..8
        public double SmPolar;         // 0..10

        public int HomeOwn;            // 0/1
        public int IncomeBand;         // 1..5
        public double JobSecurity;     // 0..10
        public int Union;              // 0/1

        public int BlueCollar;         // 0/1
        public double Diyer;           // 0..10
        public double ToolOwnership;   // 0..10
        public double MakerIdentity;   // 0..10

        public int PriorVictimization; // 0..2
        public int PriorSystemContact; // 0..2
        public int PriorJuryOutcome;   // 0=none, 1=hung, 2=convicted, 3=acquitted

        public double SystemJustification; // 0..10
        public double NeedForCognition;    // 0..10
        public double NeedForClosure;      // 0..10
        public double TraitAnxiety;        // 0..10

        // Personality-Texture Predictors (Soft but Useful)
        public double HumorLevityTendency;   // 0..10 (0=serious, 10=light-hearted)
        public double ConflictAvoidance;     // 0..10 (high = avoids conflict, goes along)
        public double DominanceAssertiveness; // 0..10 (predicts foreperson influence)
        public double PatienceImpulsivity;   // 0..10 (high = patient, low = impulsive)

        // Social-Identity Predictors (Contextual)
        public int UrbanRuralBackground;     // 0=rural, 1=urban
        public int MilitaryService;          // 0=no, 1=yes
        public int UnionMembership;          // 0=no, 1=yes
        public int ImmigrationGeneration;    // 1=first-gen, 2=second-gen, 3=third+ gen

        public double Openness;            // 0..10
        public double Agreeableness;       // 0..10 (cognitive empathy/perspective-taking)
        public double Neuroticism;         // 0..10

        public double MoralHarm;           // 0..10
        public double MoralFairness;       // 0..10
        public double MoralAuthority;      // 0..10
        public double MoralPurity;         // 0..10

        // Cognitive-Style Predictors (0..10)
        public double DetailOrientation;   // 0..10
        public double MemoryReliability;    // 0..10 (higher = more accurate recall)
        public double SuspicionTendency;    // 0..10 (higher = more doubt/discounting)

        // Emotional-Flavor Predictors (0..10)
        public double DisgustSensitivity;   // 0..10
        public double Compassion;           // 0..10
        public double AngerReactivity;      // 0..10

        // Knowledge-Domain Predictors (0..10)
        public double ScienceLiteracy;      // 0..10
        public double FinancialLiteracy;    // 0..10
        public double TechnologyFamiliarity; // 0..10

        public int MediaTrueCrime;       // 0..20
        public int MediaTabloid;         // 0..20
        public int MediaLocalCrime;      // 0..20
        public int MediaLongForm;        // 0..20
        public int MediaDocumentary;     // 0..20
        public int MediaLocalNews;       // 0..20
        public string MediaConsumptionType = "Generic"; // "TrueCrime", "Documentary", "LocalNews", "Tabloid", "Generic"

        public int BibleCollege;
        public int ElitePrivate;
        public int StateFlagship;
        public int OtherPrivate;
        public int CommunityCollege;
        public int TradeSchool;
        public int Hbcu;

        public int SierraClub;
        public int Nra;
        public int OilExecutive;
        public double Anchoring = 5;
        public double HindsightBias = 5;
        public double ConfirmationBias = 5;
        public double NarrativeCoherence = 5;
        public double OutcomeBias = 5;
        public double SocialConsensus = 5;
        public double StatusQuoBias = 5;
    }

    private static ToyJurorTraits EmptyTraits() => new ToyJurorTraits
    {
        AgeYears = 40,
        RWA = 5,
        SDO = 5,
        Punitiveness = 5,
        RapeMyth = 5,
        Religiosity = 5,
        ConservRelig = 5,
        PolId = 0,
        PolStrength = 5,
        PrimaryHistory = 5,
        Volunteer = 5,
        Donate = 5,
        Activism = 5,
        OnlinePartisan = 5,
        TvCrime = 10,
        TvCableNews = 10,
        NewsLean = 0,
        NewsIntensity = 15,
        SmUse = 4,
        SmPolar = 5,
        HomeOwn = 0,
        IncomeBand = 3,
        JobSecurity = 5,
        Union = 0,
        BlueCollar = 0,
        Diyer = 4,
        ToolOwnership = 4,
        MakerIdentity = 4,
        PriorVictimization = 1,
        PriorSystemContact = 1,
        PriorJuryOutcome = 0,
        SystemJustification = 5,
        NeedForCognition = 5,
        NeedForClosure = 5,
        TraitAnxiety = 5,
        HumorLevityTendency = 5,
        ConflictAvoidance = 5,
        DominanceAssertiveness = 5,
        PatienceImpulsivity = 5,
        UrbanRuralBackground = 1,
        MilitaryService = 0,
        UnionMembership = 0,
        ImmigrationGeneration = 2,
        Openness = 5,
        Agreeableness = 5,
        Neuroticism = 5,
        MoralHarm = 5,
        MoralFairness = 5,
        MoralAuthority = 5,
        MoralPurity = 5,
        DetailOrientation = 5,
        MemoryReliability = 5,
        SuspicionTendency = 5,
        DisgustSensitivity = 5,
        Compassion = 5,
        AngerReactivity = 5,
        ScienceLiteracy = 5,
        FinancialLiteracy = 5,
        TechnologyFamiliarity = 5,
        MediaTrueCrime = 10,
        MediaTabloid = 10,
        MediaLocalCrime = 10,
        MediaLongForm = 10,
        MediaDocumentary = 10,
        MediaLocalNews = 10,
        MediaConsumptionType = "Generic",
        BibleCollege = 0,
        ElitePrivate = 0,
        StateFlagship = 1,
        OtherPrivate = 0,
        CommunityCollege = 0,
        TradeSchool = 0,
        Hbcu = 0,
        SierraClub = 0,
        Nra = 0,
        OilExecutive = 0
    };

    private static ToyJurorTraits GenerateCoherentTraits(
        Random arng,
        IReadOnlyDictionary<string, double> biasLookup,
        Agent agent,
        int maxTries,
        double coherenceThreshold)
    {
        var attemptLimit = Math.Max(1, Math.Min(maxTries, 20));
        var traits = EmptyTraits();

        for (int attempt = 0; attempt < attemptLimit; attempt++)
        {
            SampleDemographics(arng, agent, biasLookup, traits);
            SampleTraits(arng, traits);
            SampleReligion(arng, traits);
            SamplePolitics(arng, traits);
            SampleMedia(arng, traits);
            SampleEconomics(arng, agent, traits);
            SampleEducationType(arng, agent, traits);
            SampleBlueCollarDiy(arng, agent, traits);
            SampleExperience(arng, agent, traits);
            SampleMemberships(arng, agent, traits);

            RepairHardRuleConflicts(arng, agent, traits);

            if (coherenceThreshold <= 0.0)
                return traits;

            var coherence = CoherenceScore(traits);
            if (coherence >= coherenceThreshold)
                return traits;

            if (attempt + 1 < attemptLimit)
                ReduceSoftConflict(arng, traits);
        }

        RepairHardRuleConflicts(arng, agent, traits);
        return traits;
    }

    private static void RepairHardRuleConflicts(Random arng, Agent agent, ToyJurorTraits j)
    {
        if (j.OilExecutive == 1 && j.SierraClub == 1)
        {
            if (!string.IsNullOrWhiteSpace(agent.Occupation) && agent.Occupation.Contains("exec", StringComparison.OrdinalIgnoreCase))
                j.SierraClub = 0;
            else
                j.OilExecutive = 0;
        }

        if (j.BibleCollege == 1 && j.ElitePrivate == 1)
            j.ElitePrivate = 0;

        if (j.TradeSchool == 1 && j.ElitePrivate == 1)
            j.ElitePrivate = 0;

        if (j.BlueCollar == 1 && j.ElitePrivate == 1)
            j.ElitePrivate = 0;

        if (j.Hbcu == 1 && j.BibleCollege == 1)
            j.BibleCollege = 0;

        if (j.Diyer >= 7 && j.ToolOwnership <= 1)
            j.ToolOwnership = Clamp10(2.0 + arng.NextDouble() * 4.0);

        if (j.BibleCollege + j.ElitePrivate + j.StateFlagship + j.OtherPrivate + j.CommunityCollege + j.TradeSchool + j.Hbcu == 0)
            j.StateFlagship = 1;
    }

    private static void ReduceSoftConflict(Random arng, ToyJurorTraits j)
    {
        if (j.SierraClub == 1 && j.Nra == 1)
        {
            if (arng.NextDouble() < 0.5)
                j.Nra = 0;
            else
                j.SierraClub = 0;
        }

        if (j.Hbcu == 1)
        {
            if (j.RWA > 7)
                j.RWA = Math.Max(5.0, j.RWA - (arng.NextDouble() * 2.0));
            if (j.SDO > 7)
                j.SDO = Math.Max(5.0, j.SDO - (arng.NextDouble() * 2.0));
        }

        if (j.BlueCollar == 1 && j.IncomeBand == 5)
            j.IncomeBand = 4;

        if (j.BibleCollege == 1 && j.PolId < -0.5 && j.Activism > 7)
            j.Activism = 6;
    }

    private static void SampleDemographics(Random arng, Agent agent, IReadOnlyDictionary<string, double> biasLookup, ToyJurorTraits j)
    {
        j.AgeYears = agent.Age;
        double bias = agent.Bias;

        double ageW = GetWeight(biasLookup, "Age", 0.55);
        double genderW = GetWeight(biasLookup, "Gender", 0.45);
        double eduW = GetWeight(biasLookup, "Education Level", 0.80);
        double incomeW = GetWeight(biasLookup, "Income Level", 0.50);
        double religionW = GetWeight(biasLookup, "Religion", 0.40);
        double polW = GetWeight(biasLookup, "Political Affiliation", 0.90);
        double ethnicityW = GetWeight(biasLookup, "Ethnicity", 0.60);
        double experienceW = GetWeight(biasLookup, "Juror Experience", 0.70);
        double legalW = GetWeight(biasLookup, "Legal Knowledge", 0.75);
        double profBgW = GetWeight(biasLookup, "Professional Background", 0.55);
        double communityW = GetWeight(biasLookup, "Community Ties", 0.65);
        double commStyleW = GetWeight(biasLookup, "Communication Style", 0.35);

        double implicitBias = (arng.NextDouble() - 0.4) * 1.2;

        double raceGenderCompound = 0.0;
        if (agent.Race != "Unknown" && agent.Gender != "Unknown")
        {
            bool isNonWhite = agent.Race != "White";
            bool isFemale = agent.Gender == "Female";
            if (isNonWhite && isFemale) raceGenderCompound = -0.15 * ethnicityW;
            else if (isNonWhite && !isFemale) raceGenderCompound = -0.10 * ethnicityW;
        }

        double eduPolRigidity = eduW * polW * 0.3;

        double age = agent.Age;
        double agePunitiveCurve = 0.0;
        if (age < 30) agePunitiveCurve = (30 - age) * 0.04;
        else if (age > 60) agePunitiveCurve = (age - 60) * 0.03;
        double ageFactor = (ageW - 0.5) * 0.8 + agePunitiveCurve;

        j.RWA = Clamp10(5 + (bias * 3.2 * polW) + ageFactor + (ethnicityW - 0.5) * 0.6
            + eduPolRigidity + raceGenderCompound * 0.5 + implicitBias * 0.3
            + (arng.NextDouble() - 0.5) * 1.8);

        j.SDO = Clamp10(5 + (-bias * 2.8 * polW) + (incomeW - 0.5) * 1.2 + (genderW - 0.5) * 0.4
            + (eduW - 0.5) * -0.2 + raceGenderCompound * 0.3
            + (arng.NextDouble() - 0.5) * 1.8);

        j.Punitiveness = Clamp10(5 + (bias * 2.1 * polW) + (religionW - 0.5) * 1.0
            + agePunitiveCurve * 1.2 + implicitBias * 0.4
            + (arng.NextDouble() - 0.5) * 1.8);

        double genderReligionInteraction = (genderW - 0.5) * (religionW - 0.5) * 0.6;
        j.RapeMyth = Clamp10(5 + (bias * 1.6 * polW) + (religionW - 0.5) * 1.2
            + (eduW - 0.5) * -0.4 + genderReligionInteraction
            + raceGenderCompound * 0.4 + (arng.NextDouble() - 0.5) * 1.8);

        j.Religiosity = Clamp10(5 + (religionW - 0.5) * 2.0 + (commStyleW - 0.5) * 0.5
            + implicitBias * 0.2 + (arng.NextDouble() - 0.5) * 1.8);
        j.ConservRelig = Clamp10(5 + (bias * 2.1 * religionW) + (ethnicityW - 0.5) * 0.6
            + (religionW - 0.5) * (polW - 0.5) * 0.4
            + (arng.NextDouble() - 0.5) * 1.8);

        j.PolId = Clamp11(bias * 1.0 * polW + implicitBias * 0.2);
        j.HomeOwn = (agent.Occupation ?? "").ToLower().Contains("retired") ? 1 : (arng.NextDouble() < 0.5 ? 1 : 0);
        j.IncomeBand = Math.Clamp(1 + (int)Math.Floor(agent.IncomeLevel switch
        {
            "Lower Class" => arng.NextDouble() * 1.5,
            "Working Class" => 1 + arng.NextDouble() * 1.5,
            "Middle Class" => 2 + arng.NextDouble() * 1.5,
            "Upper Middle Class" => 3 + arng.NextDouble() * 1.5,
            "Upper Class" => 4 + arng.NextDouble(),
            _ => arng.NextDouble() * 5
        }), 1, 5);
        j.JobSecurity = Clamp10(5 + (incomeW - 0.5) * 2 + (profBgW - 0.5) * 1.0 + (legalW - 0.5) * 1.0
            + implicitBias * 0.2 + (arng.NextDouble() - 0.5) * 1.8);
        j.Union = arng.NextDouble() < (0.3 + (incomeW - 0.5) * 0.1) ? 1 : 0;

        double reliabilityFactor = (eduW * 0.5 + legalW * 0.3 + experienceW * 0.2);
        j.Volunteer = Clamp10(4 + (communityW - 0.5) * 3 + (experienceW - 0.5) * 1.0
            + (commStyleW - 0.5) * 1.0 + reliabilityFactor * 0.8 + (arng.NextDouble() - 0.5) * 1.8);
        j.Donate = Clamp10(4 + (communityW - 0.5) * 2 + (legalW - 0.5) * 0.5
            + (commStyleW - 0.5) * 0.8 + reliabilityFactor * 0.5 + (arng.NextDouble() - 0.5) * 1.8);
        j.Activism = Clamp10(4 + (communityW - 0.5) * 2 + (polW - 0.5) * 1.8
            + (experienceW - 0.5) * 0.8 + reliabilityFactor * 0.6 + (arng.NextDouble() - 0.5) * 1.8);
        j.OnlinePartisan = Clamp10(5 + (commStyleW - 0.5) * 1.5 + (polW - 0.5) * 1.5
            + (experienceW - 0.5) * 0.5 + (arng.NextDouble() - 0.5) * 1.8);

        double anchoringBase = (arng.NextDouble() * 6) + 2;
        double mediaAnchoringBoost = (commStyleW - 0.5) * 1.5;
        double eduAnchoringResistance = (eduW - 0.5) * -1.0;
        j.Anchoring = Clamp10(anchoringBase + mediaAnchoringBoost + eduAnchoringResistance
            + (arng.NextDouble() - 0.5) * 1.5);

        double hindsightBase = (arng.NextDouble() * 5) + 2;
        j.HindsightBias = Clamp10(hindsightBase + (commStyleW - 0.5) * 2.0
            + (eduW - 0.5) * -0.8 + (arng.NextDouble() - 0.5) * 1.5);

        double confirmBase = (arng.NextDouble() * 4) + 3;
        j.ConfirmationBias = Clamp10(confirmBase + (polW - 0.5) * 2.0
            + (religionW - 0.5) * 1.5 + (implicitBias + 0.5) * 0.8
            + (arng.NextDouble() - 0.5) * 1.5);

        double narrativeBase = (arng.NextDouble() * 5) + 3;
        j.NarrativeCoherence = Clamp10(narrativeBase + (eduW - 0.5) * 1.5
            + (legalW - 0.5) * 1.0 + (commStyleW - 0.5) * 0.8
            + (arng.NextDouble() - 0.5) * 1.5);

        double outcomeBase = (arng.NextDouble() * 4) + 3;
        j.OutcomeBias = Clamp10(outcomeBase + (j.HindsightBias - 5) * 0.4
            + (commStyleW - 0.5) * 1.2 + (arng.NextDouble() - 0.5) * 1.5);

        double consensusBase = (arng.NextDouble() * 4) + 3;
        j.SocialConsensus = Clamp10(consensusBase + (communityW - 0.5) * 2.0
            + (commStyleW - 0.5) * 1.0 + (arng.NextDouble() - 0.5) * 1.5);

        double statusQuoBase = (arng.NextDouble() * 4) + 3;
        j.StatusQuoBias = Clamp10(statusQuoBase + (j.PolId + 1) * 0.3
            + (arng.NextDouble() - 0.5) * 1.5);

        j.PriorJuryOutcome = agent.PriorJuryOutcome;

        j.MediaConsumptionType = agent.MediaConsumptionType;
        double isTrueCrime = agent.MediaConsumptionType == "TrueCrime" ? 1.0 : 0.0;
        double isDocumentary = agent.MediaConsumptionType == "Documentary" ? 1.0 : 0.0;
        double isTabloid = agent.MediaConsumptionType == "Tabloid" ? 1.0 : 0.0;
        double isLocalNews = agent.MediaConsumptionType == "LocalNews" ? 1.0 : 0.0;
        j.MediaTrueCrime = (int)(isTrueCrime * arng.NextDouble() * 20);
        j.MediaDocumentary = (int)(isDocumentary * arng.NextDouble() * 20);
        j.MediaTabloid = (int)(isTabloid * arng.NextDouble() * 20);
        j.MediaLocalNews = (int)(isLocalNews * arng.NextDouble() * 20);

        j.Agreeableness = Clamp10(5 + (agent.Agreeableness - 5) + (arng.NextDouble() - 0.5) * 1.5);
    }

    private static void SampleTraits(Random arng, ToyJurorTraits j)
    {
        j.RWA = Clamp10(j.RWA + (arng.NextDouble() - 0.5) * 1.0);
        j.SDO = Clamp10(j.SDO + (arng.NextDouble() - 0.5) * 1.0);
        j.Punitiveness = Clamp10(j.Punitiveness + (arng.NextDouble() - 0.5) * 1.0);
        j.RapeMyth = Clamp10(j.RapeMyth + (arng.NextDouble() - 0.5) * 1.0);
        j.Anchoring = Clamp10(j.Anchoring + (arng.NextDouble() - 0.5) * 1.2);
        j.HindsightBias = Clamp10(j.HindsightBias + (arng.NextDouble() - 0.5) * 1.0);
        j.ConfirmationBias = Clamp10(j.ConfirmationBias + (arng.NextDouble() - 0.5) * 1.0);
        j.NarrativeCoherence = Clamp10(j.NarrativeCoherence + (arng.NextDouble() - 0.5) * 0.8);
        j.OutcomeBias = Clamp10(j.OutcomeBias + (arng.NextDouble() - 0.5) * 0.8);
        j.SocialConsensus = Clamp10(j.SocialConsensus + (arng.NextDouble() - 0.5) * 1.0);
        j.StatusQuoBias = Clamp10(j.StatusQuoBias + (arng.NextDouble() - 0.5) * 0.8);
    }

    private static void SampleReligion(Random arng, ToyJurorTraits j)
    {
        j.Religiosity = Clamp10(j.Religiosity + (arng.NextDouble() - 0.5) * 1.0);
        j.ConservRelig = Clamp10(j.ConservRelig + (arng.NextDouble() - 0.5) * 1.0);
    }

    private static void SamplePolitics(Random arng, ToyJurorTraits j)
    {
        j.PolStrength = Clamp10(5 + (arng.NextDouble() - 0.5) * 3);
        j.PrimaryHistory = Clamp10(arng.NextDouble() * 10);
        j.Volunteer = Clamp10(arng.NextDouble() * 10);
        j.Donate = Clamp10(arng.NextDouble() * 10);
        j.Activism = Clamp10(arng.NextDouble() * 10);
        j.OnlinePartisan = Clamp10(arng.NextDouble() * 10);
    }

    private static void SampleMedia(Random arng, ToyJurorTraits j)
    {
        j.TvCrime = arng.NextDouble() * 20;
        j.TvCableNews = arng.NextDouble() * 20;
        j.NewsLean = (arng.NextDouble() - 0.5) * 2;
        j.NewsIntensity = arng.NextDouble() * 30;
        j.SmUse = arng.NextDouble() * 8;
        j.SmPolar = arng.NextDouble() * 10;
    }

    private static double MediaSelectiveExposure(ToyJurorTraits j)
    {
        double tvEffect = (j.TvCableNews / 20.0) * (j.SmPolar / 10.0);
        double newsIntensity = j.NewsIntensity / 30.0;
        return tvEffect * newsIntensity;
    }

    private static void SampleEconomics(Random arng, Agent agent, ToyJurorTraits j)
    {
        j.IncomeBand = string.IsNullOrWhiteSpace(agent.IncomeLevel) ? j.IncomeBand : j.IncomeBand;
        j.JobSecurity = Clamp10(j.JobSecurity + (arng.NextDouble() - 0.5) * 1.0);
    }

    private static void SampleEducationType(Random arng, Agent agent, ToyJurorTraits j)
    {
        var edu = (agent.EducationLevel ?? "").ToLower();
        if (edu.Contains("bible")) j.BibleCollege = 1;
        else if (edu.Contains("private")) j.ElitePrivate = 1;
        else if (edu.Contains("associate")) j.CommunityCollege = 1;
        else if (edu.Contains("trade")) j.TradeSchool = 1;
        else if (edu.Contains("hbcu")) j.Hbcu = 1;
        else j.StateFlagship = 1;

        var sum = j.BibleCollege + j.ElitePrivate + j.StateFlagship + j.OtherPrivate + j.CommunityCollege + j.TradeSchool + j.Hbcu;
        if (sum > 1)
        {
            j.BibleCollege = j.ElitePrivate = j.OtherPrivate = j.CommunityCollege = j.TradeSchool = j.Hbcu = 0;
            j.StateFlagship = 1;
        }
    }

    private static void SampleBlueCollarDiy(Random arng, Agent agent, ToyJurorTraits j)
    {
        j.BlueCollar = (agent.Occupation ?? "").ToLower().Contains("blue") ? 1 : 0;
        j.Diyer = arng.NextDouble() * 10;
        j.ToolOwnership = arng.NextDouble() * 10;
        j.MakerIdentity = arng.NextDouble() * 10;
    }

    private static void SampleExperience(Random arng, Agent agent, ToyJurorTraits j)
    {
        j.PriorVictimization = agent.PriorVictimizationHistory;
        j.PriorSystemContact = arng.Next(0, 3);
        j.SystemJustification = agent.SystemJustification;
        j.NeedForCognition = agent.NeedForCognition;
        j.NeedForClosure = agent.NeedForClosure;
        j.HumorLevityTendency = agent.HumorLevityTendency;
        j.ConflictAvoidance = agent.ConflictAvoidance;
        j.DominanceAssertiveness = agent.DominanceAssertiveness;
        j.PatienceImpulsivity = agent.PatienceImpulsivity;
        j.UrbanRuralBackground = agent.UrbanRuralBackground;
        j.MilitaryService = agent.MilitaryService;
        j.UnionMembership = agent.UnionMembership;
        j.ImmigrationGeneration = agent.ImmigrationGeneration;
        j.DetailOrientation = agent.DetailOrientation;
        j.MemoryReliability = agent.MemoryReliability;
        j.SuspicionTendency = agent.SuspicionTendency;
        j.DisgustSensitivity = agent.DisgustSensitivity;
        j.Compassion = agent.Compassion;
        j.AngerReactivity = agent.AngerReactivity;
        j.ScienceLiteracy = agent.ScienceLiteracy;
        j.FinancialLiteracy = agent.FinancialLiteracy;
        j.TechnologyFamiliarity = agent.TechnologyFamiliarity;
    }

    private static void SampleMemberships(Random arng, Agent agent, ToyJurorTraits j)
    {
        j.Nra = arng.NextDouble() < 0.2 ? 1 : 0;
        j.SierraClub = arng.NextDouble() < 0.1 ? 1 : 0;
        j.OilExecutive = (agent.Occupation ?? "").ToLower().Contains("exec") && arng.NextDouble() < 0.3 ? 1 : 0;
    }

    private static bool ViolatesHardRules(ToyJurorTraits j)
    {
        if (j.OilExecutive == 1 && j.SierraClub == 1) return true;
        if (j.BibleCollege == 1 && j.ElitePrivate == 1) return true;
        if (j.TradeSchool == 1 && j.ElitePrivate == 1) return true;
        if (j.Hbcu == 1 && j.BibleCollege == 1) return true;
        if (j.BlueCollar == 1 && j.ElitePrivate == 1) return true;
        if (j.Diyer >= 7 && j.ToolOwnership <= 1) return true;
        return false;
    }

    private static double SoftConflictScore(ToyJurorTraits j)
    {
        double score = 0.0;
        if (j.Hbcu == 1 && (j.RWA > 7 || j.SDO > 7)) score += 1.0;
        if (j.BlueCollar == 1 && j.IncomeBand == 5) score += 0.5;
        if (j.BibleCollege == 1 && j.PolId < -0.5 && j.Activism > 7) score += 1.0;
        if (j.SierraClub == 1 && j.Nra == 1) score += 1.0;
        return score;
    }

    private static double CoherenceScore(ToyJurorTraits j, double wHard = 10.0, double wSoft = 1.0)
    {
        double hard = 0.0;
        double soft = SoftConflictScore(j);
        double penalty = wHard * hard + wSoft * soft;
        return Math.Max(0.0, 1.0 - penalty);
    }

    private static double GetWeight(IReadOnlyDictionary<string, double> lookup, string key, double defaultValue)
        => lookup.TryGetValue(key, out var v) ? v : defaultValue;

    private static double Clamp10(double x) => Math.Max(0.0, Math.Min(10.0, x));
    private static double Clamp11(double x) => Math.Max(-1.0, Math.Min(1.0, x));
}
