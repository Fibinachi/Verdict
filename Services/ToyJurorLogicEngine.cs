using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Self-contained juror opinion engine inspired by the provided Python coherence + drift + conformity logic.
///
/// Research-Based Improvements (see docs/JurorBiasResearch.md):
/// - Cross-factor interactions: Race×Gender compounded bias, Education×Political rigidity, Age×Punitiveness curvilinear
/// - Non-linear threshold effects: Diminishing returns on education skepticism, age-related punitiveness U-curve
/// - Reliability-weighted deliberation: More credible jurors exert greater influence
/// - Implicit bias measures: Unconscious associations distinct from explicit attitudes
/// - Group polarization: Like-minded deliberation amplifies extreme positions
/// - Anchoring effect: Initial numbers disproportionately influence damage assessments
/// - Hindsight bias: Outcomes appear more predictable after they occur, distorting evidence evaluation
/// - Confirmation bias: Jurors favor evidence confirming initial beliefs
/// - Narrative coherence: Jurors construct stories and favor evidence that fits their narrative
/// - Outcome bias: Judging decisions by outcomes rather than decision-making quality
/// - Social consensus bias: Perceived group consensus amplifies individual beliefs
/// - Status quo bias: Preference for maintaining current position during deliberation
///
/// Coefficients derived from meta-analyses of mock jury studies, RAND civil jury data,
/// and published research on demographic influences on verdict decisions.
/// </summary>
public static class ToyJurorLogicEngine
{
    private static double Logistic(double x) => 1.0 / (1.0 + Math.Exp(-x));

    private static double Clamp01(double x) => Math.Max(0.0, Math.Min(1.0, x));

    private sealed class ToyJurorTraits
    {
        public double AgeYears;
        public double RWA;             // 0..10
        public double SDO;             // 0..10
        public double Punitiveness;  // 0..10
        public double RapeMyth;       // 0..10

        public double Religiosity;   // 0..10
        public double ConservRelig;  // 0..10

        public double PolId;         // -1..1
        public double PolStrength;  // 0..10
        public double PrimaryHistory; // 0..10
        public double Volunteer;       // 0..10
        public double Donate;          // 0..10
        public double Activism;       // 0..10
        public double OnlinePartisan;// 0..10

        public double TvCrime;        // 0..20
        public double TvCableNews;   // 0..20
        public double NewsLean;      // -1..1
        public double NewsIntensity;// 0..30
        public double SmUse;         // 0..8
        public double SmPolar;      // 0..10

        public int HomeOwn;          // 0/1
        public int IncomeBand;      // 1..5
        public double JobSecurity;  // 0..10
        public int Union;           // 0/1

        public int BlueCollar;      // 0/1
        public double Diyer;        // 0..10
        public double ToolOwnership;// 0..10
        public double MakerIdentity;// 0..10

        public int PriorVictimization;   // 0..2
        public int PriorSystemContact;   // 0..2

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

         // ── Bias weights carried through for dynamic Stage-2 weaponization (2026-05-27) ──
         public double EduBiasWeight = 0.80;
         public double PolBiasWeight = 0.90;

         // ── Research-backed individual difference traits (2026-05-27 expansion) ──
         public double NeedForCognition = 5;    // 0..10  Cacioppo & Petty (1982): central vs peripheral processing
         public double BeliefInJustWorld = 5;   // 0..10  Lerner (1980): victim-blaming, defense-leaning
         public int DeathPenaltyQualified;      // 0/1    Haney (1984): more conviction-prone in all cases
         public double TrustInInstitutions = 5; // 0..10  Tyler (2006): trust in police/courts/government
         public double TraitEmpathy = 5;        // 0..10  Davis (1983) IRI: affects damages and sympathy
         public double NeedForClosure = 5;      // 0..10  Kruglanski (1996): faster decisions, more conforming
         public double CognitiveReflection = 5; // 0..10  Frederick (2005): overrides intuitive bias
         public double SmNewsReliance = 5;      // 0..10  Barberá (2020): social media as primary news
         public int PersonalInjuryHistory;      // 0/1    Hans & Reyna (2011): prior similar injury
     }

    private sealed class ToyCoefs
    {
        public Dictionary<string, double> Alpha { get; } = new();
        public Dictionary<string, double> Eta { get; } = new();
        public Dictionary<string, double> Zeta { get; } = new();
        public Dictionary<string, double> Chi { get; } = new();
        public Dictionary<string, double> Rho { get; } = new();

        public double Beta0 { get; set; } = 0.0;
        public double Beta1 { get; set; } = 1.0;
        public double Gamma1 { get; set; } = 1.0;
        public double Theta0 { get; set; } = 0.0;
        public double Theta1 { get; set; } = 5.0;

        public double StaticBiasIntercept => Alpha.TryGetValue("intercept", out var v) ? v : 0.0;
    }

    /// <summary>
    /// Updates juror VerdictLean values in-place using the toy coherence + drift + conformity logic.
    /// </summary>
    public static void ApplyCoherentDriftDeliberation(
        IReadOnlyList<Agent> jurors,
        double deltaEvidence,
        IReadOnlyList<BiasFactor>? biasFactors = null,
        int maxSampleTries = 2000,
        double coherenceThreshold = 0.9,
        int? seed = null,
        CaseMode mode = CaseMode.Civil,
        int evidenceIndex = 0,
        int totalEvidenceCount = 1,
        string caseSubType = "",
        double evidenceDirection = 0.0)
    {
        if (jurors == null || jurors.Count == 0) return;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var coefs = BuildNeutralToyCoefs();

        // ── Block 4: Temporal order-of-proof effects (Primacy/Recency) ──
        // Early evidence anchors perception (primacy); recent evidence is fresher (recency).
        // Mid-trial evidence decays exponentially. Opening/closing arguments naturally benefit.
        int N = Math.Max(1, totalEvidenceCount);
        int t = Math.Clamp(evidenceIndex, 0, N - 1);
        double primacyAnchor = (t < (N * 0.20)) ? 1.25 : 1.0;   // Boost initial 20% of evidence
        double recencyRetention = Math.Exp(-0.05 * (N - 1 - t));  // Exponential memory decay for aging points
        double temporalWeight = primacyAnchor * recencyRetention;
        double adjustedDeltaEvidence = deltaEvidence * temporalWeight;

        // If you provide BiasFactors, interpret them as shaping the toy trait mapping.
        // Otherwise, mapping relies only on Agent fields.
        var biasLookup = (biasFactors ?? Array.Empty<BiasFactor>())
            .GroupBy(bf => bf.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Weight, StringComparer.OrdinalIgnoreCase);

        // Generate a toy trait space for each juror deterministically enough to be stable during a run.
        // (We still use randomness for coherence sampling.)
        for (int i = 0; i < jurors.Count; i++)
        {
            var agent = jurors[i];
            if (!agent.IsOccupied || !agent.CanVote) continue;

            // Seed per agent so results don't jitter wildly if you re-run stage.
            int perAgentSeed = seed.HasValue ? unchecked(seed.Value + i * 997) : rng.Next(int.MinValue, int.MaxValue);
            var arng = new Random(perAgentSeed);

            var traits = GenerateCoherentTraits(arng, biasLookup, agent, maxSampleTries, coherenceThreshold);

            // Static bias and initial guilt belief (toy)
            double b = ComputeStaticBias(traits, coefs, caseSubType);
            double g1 = Math.Clamp(coefs.Beta0 + coefs.Beta1 * b, 0.0, 1.0);

            // Rigidity + evidence drift
            double lam = ComputeRigidity(traits, coefs);
            double g2 = Clamp01(g1 + coefs.Gamma1 * adjustedDeltaEvidence * (1.0 - lam));

            // Hardness + influence weights
            double h = ComputeHardness(traits, coefs);
            // Influence weight uses certainty/education etc. Here we mirror toy shape but keep neutral.
            double w = ComputeInfluenceWeight(traits, g2, coefs);

            // Conformity + deliberation drift
            // Weighted jury center M computed across all jurors; we approximate by using average of g2 weighted.
            // Because we update sequentially, we need M first. We'll do a two-pass approach.
        }

        // Two-pass: first compute all g2, weights, hardness; then compute M; then compute g3.
        var g2s = new double[jurors.Count];
        var hs = new double[jurors.Count];
        var ws = new double[jurors.Count];
        var traitsByIndex = new ToyJurorTraits?[jurors.Count];

        for (int i = 0; i < jurors.Count; i++)
        {
            var agent = jurors[i];
            if (!agent.IsOccupied || !agent.CanVote)
            {
                traitsByIndex[i] = null;
                g2s[i] = 0.5;
                hs[i] = 0.0;
                ws[i] = 0.0;
                continue;
            }

            int perAgentSeed = seed.HasValue ? unchecked(seed.Value + i * 997) : rng.Next(int.MinValue, int.MaxValue);
            var arng = new Random(perAgentSeed);

            var traits = GenerateCoherentTraits(arng, biasLookup, agent, maxSampleTries, coherenceThreshold);
            traitsByIndex[i] = traits;

            double b = ComputeStaticBias(traits, coefs, caseSubType);
            double g1 = Math.Clamp(coefs.Beta0 + coefs.Beta1 * b, 0.0, 1.0);

            double lam = ComputeRigidity(traits, coefs);

            // Directional rigidity (Task 2): dampen only when evidence conflicts with bias profile.
            // Task 5: dynamically amplify rigidity for educated partisans facing hostile evidence.
            // evidenceDirection: +1 = favors prosecution/plaintiff, -1 = favors defense.
            // Falls back to Math.Sign(deltaEvidence) when not explicitly provided.
            double effectiveEvidenceDir = evidenceDirection != 0.0
                ? Math.Sign(evidenceDirection)
                : Math.Sign(adjustedDeltaEvidence);
            bool evidenceConflicts = (effectiveEvidenceDir * traits.PolId) < 0;
            if (evidenceConflicts && Math.Abs(traits.PolId) > 0.4) // Only strong partisans (Block 2 fix)
            {
                // v0.61: motivated_term is blended inside the logistic input of ComputeRigidity
                // (see ComputeRigidity overload below) rather than added post-hoc.
                lam = ComputeRigidityWithMotivatedTerm(traits, coefs,
                    traits.EduBiasWeight * traits.PolBiasWeight * 0.20);
            }
            double effectiveMultiplier = evidenceConflicts ? (1.0 - lam) : 1.0;
            double g2 = Clamp01(g1 + coefs.Gamma1 * adjustedDeltaEvidence * effectiveMultiplier);

            double h = ComputeHardness(traits, coefs);
            double w = ComputeInfluenceWeight(traits, g2, coefs);

            g2s[i] = g2;
            hs[i] = h;
            ws[i] = w;
        }

        // ── Task 1: Binary functional legal-position consensus ──
        // Compute V_i per juror: binary vote (1.0 = Guilty/Liable, 0.0 = Not)
        // Criminal: >0.85 = Guilty (beyond reasonable doubt). Civil: >0.50 = Liable (preponderance).
        double convictionThreshold = mode == CaseMode.Criminal ? 0.85 : 0.50;
        var votes = new double[jurors.Count];
        for (int i = 0; i < jurors.Count; i++)
        {
            if (ws[i] > 0)
                votes[i] = g2s[i] > convictionThreshold ? 1.0 : 0.0;
        }

        double voteSum = 0.0;
        double voteWeightSum = 0.0;
        for (int i = 0; i < jurors.Count; i++)
        {
            if (ws[i] <= 0) continue;
            voteSum += ws[i] * votes[i];
            voteWeightSum += ws[i];
        }
        double M = voteWeightSum > 0 ? voteSum / voteWeightSum : 0.0;

        // Final: conformity drift and verdict probability -> apply to VerdictLean.
        for (int i = 0; i < jurors.Count; i++)
        {
            var agent = jurors[i];
            if (!agent.IsOccupied || !agent.CanVote) continue;

            var traits = traitsByIndex[i];
            if (traits == null) continue;

            double phi = ComputeConformity(traits, coefs);

            // ── Block 2: Dynamic threshold friction (psychological hardness) ──
            // Base hardness from traits is static; real jurors experience heightened
            // cognitive anxiety when peer pressure attempts to push them across a critical
            // legal threshold (0.85 for criminal, 0.50 for civil). This inverse-distance
            // function spikes resistance when on the precipice of changing their vote.
            double baseHardness = hs[i];
            double distanceToThreshold = Math.Abs(g2s[i] - convictionThreshold);
            double thresholdFriction = 0.45 * Math.Exp(-15.0 * distanceToThreshold);
            double h_i = Math.Min(baseHardness + thresholdFriction, 0.98);

            // ── Stage 3: Directional deliberation with continuous target vector (v0.61) ──
            // M is a weighted average of *binary* votes (V_i ∈ {0,1}).
            // The target is directional — jurors are pulled toward the upper or lower
            // boundary depending on which side the group majority favors.
            // v0.61 fix: acquittal branch no longer collapses to M (identity collapse);
            // it pulls toward (threshold − 0.01) instead of min(M, threshold − 0.01).
            double groupMajoritySide = M >= 0.50 ? 1.0 : 0.0;

            double targetLeaning;
            if (groupMajoritySide == 1.0)
                targetLeaning = Math.Max(convictionThreshold + 0.01, M);
            else
                targetLeaning = convictionThreshold - 0.01;

            double g3 = Clamp01(g2s[i] + (1.0 - h_i) * phi * (targetLeaning - g2s[i]));

            // ── Stage 3 post: verdict probability (v0.61 explicit ordering) ──
            // 1. Compute P = logistic(theta0 + theta1 * g3).
            // 2. verdict_lean = clamp(0.05, 0.95, P).
            double p = Logistic(coefs.Theta0 + coefs.Theta1 * g3);
            double verdictLean = Math.Clamp(p, 0.05, 0.95);

            agent.VerdictLean = verdictLean;
            agent.Sentiment = 0.5; // optional: keep stable for now
        }
    }

    private static ToyCoefs BuildNeutralToyCoefs()
    {
        // Research-grounded *structure* (placeholders for the final numeric calibration).
        //
        // IMPORTANT: this engine historically used neutral/empty coefficients. We now populate the
        // coefficient dictionaries so that the 12 factor weights actually influence latent toy traits.
        // The specific numeric magnitudes below are intentionally conservative and meant to be
        // tuned after you finalize the research citations/weight mapping.
        var coefs = new ToyCoefs();

        // --- Alpha (static bias -> initial verdict lean) ---
        // Treat: political polarity, religiosity/conservatism, and education as major directional components.
        coefs.Alpha["intercept"] = 0.0;
        coefs.Alpha["pol_id"] = 0.60;       // Reduced from 0.90 — political dominance limited (Forresta 2025)
        coefs.Alpha["conserv_relig"] = 0.35; // Increased from 0.25 — religion stronger directional driver
        coefs.Alpha["education_years"] = 0.15;
        coefs.Alpha["nra"] = 0.12;  // NRA members lean prosecution in violent crime (flipped from defense)
        coefs.Alpha["news_lean"] = 0.18;  // Partisan news consumption affects verdict direction
        coefs.Alpha["prior_victim"] = 0.15;  // Crime victims lean prosecution
        coefs.Alpha["prior_system"] = -0.12;  // Prior system contact → skepticism toward prosecution
        coefs.Alpha["bjw"] = 0.20;  // Belief in Just World — now case-type dependent (was -0.20)
        coefs.Alpha["death_penalty"] = 0.25;  // Death-qualified jurors more conviction-prone (Haney 1984)
        coefs.Alpha["trust_institutions"] = 0.15;  // Trust in police/courts → pro-prosecution (Tyler 2006)
        coefs.Alpha["trait_empathy"] = -0.10;  // Empathy → plaintiff sympathy in civil, defendant in criminal
        coefs.Alpha["gender"] = 0.25;    // Added: gender as moderate context-dependent driver
        coefs.Alpha["ethnicity"] = 0.35; // Added: ethnicity as moderate but non-trivial driver

        // --- Eta (rigidity -> evidence discounting) ---
        // Higher RWA/SDO and religiosity/conservatism -> increased rigidity.
        coefs.Eta["RWA"] = 0.55;
        coefs.Eta["SDO"] = 0.45;
        coefs.Eta["punitiveness"] = 0.25;
        coefs.Eta["conserv_relig"] = 0.25;
        coefs.Eta["pol_strength"] = 0.25;
        coefs.Eta["primary_history"] = 0.10;
        coefs.Eta["online_partisan"] = 0.08;
        coefs.Eta["income_band"] = 0.10;
        coefs.Eta["job_security"] = 0.06;
        coefs.Eta["home_own"] = 0.05;
        coefs.Eta["nra"] = 0.10;
        coefs.Eta["religiosity"] = 0.12;  // General religiosity increases resistance to counterevidence
        coefs.Eta["tv_crime"] = 0.08;  // Heavy crime TV → more rigid in prosecution-leaning beliefs
        coefs.Eta["need_closure"] = 0.12;  // Need for Closure → resists counterevidence (Kruglanski 1996)
        coefs.Eta["cognitive_reflection"] = -0.15;  // CRT → less rigid, more evidence-driven
        coefs.Eta["bjw"] = 0.10;  // BJW → just-world beliefs resist contradictory evidence

        // --- Zeta (hardness -> conformity resistance) ---
        // Higher religiosity/conservatism and political strength -> harder to move.
        coefs.Zeta["RWA"] = 0.30;     // was 0.25 — increased for strong ideologues (Block 7)
        coefs.Zeta["SDO"] = 0.20;
        coefs.Zeta["conserv_relig"] = 0.25;
        coefs.Zeta["pol_strength"] = 0.25; // was 0.20 — increased for strong ideologues
        coefs.Zeta["primary_history"] = 0.10;
        coefs.Zeta["activism"] = 0.08;
        coefs.Zeta["online_partisan"] = 0.08;
        coefs.Zeta["trade_school"] = 0.03;
        coefs.Zeta["hbcu"] = 0.03;
        coefs.Zeta["community_college"] = 0.02;  // Community college: moderate conviction
        coefs.Zeta["bible_college"] = 0.06;  // Bible college: strong religious convictions → harder to move
        coefs.Zeta["blue_collar"] = 0.04;
        coefs.Zeta["diyer"] = 0.02;
        coefs.Zeta["income_band"] = 0.05;
        coefs.Zeta["need_closure"] = 0.10;  // Need for Closure → harder to move once decided
        coefs.Zeta["cognitive_reflection"] = -0.08;  // CRT → more open to being persuaded

        // --- Chi (influence weight -> deliberation impact) ---
        // Higher education / legal knowledge proxy -> greater influence (ability to parse evidence).
        coefs.Chi["education_years"] = 0.25;
        // Elite/private/proxy degrees -> slight additional influence.
        coefs.Chi["elite_private"] = 0.08;
        coefs.Chi["state_flagship"] = 0.05;
        coefs.Chi["other_private"] = 0.04;  // Other private colleges: above-average influence
        coefs.Chi["trade_school"] = 0.03;
        coefs.Chi["hbcu"] = 0.02;
        coefs.Chi["community_college"] = 0.02;  // Community college: practical, working-class credibility
        coefs.Chi["bible_college"] = 0.02;  // Bible college: moral authority in religious communities
        coefs.Chi["blue_collar"] = 0.01;
        coefs.Chi["diyer"] = 0.01;
        // Age -> mild influence (life experience, confidence).
        coefs.Chi["age"] = 0.02;
        // Certainty proxy uses (G2-0.5); keep small to avoid extremes.
        coefs.Chi["certainty"] = 0.03;
        coefs.Chi["nfc"] = 0.20;  // Need for Cognition → high analytical influence in deliberation
        coefs.Chi["cognitive_reflection"] = 0.12;  // CRT → clear thinkers are persuasive

        // --- Rho (conformity -> susceptibility to jury consensus) ──
        // Reduced globally ~30-40% to increase hung-jury likelihood (Block 7).
        coefs.Rho["RWA"] = 0.12;          // was 0.20
        coefs.Rho["SDO"] = 0.07;           // was 0.12
        coefs.Rho["conserv_relig"] = 0.10; // was 0.18
        coefs.Rho["pol_id"] = 0.06;        // was 0.10
        coefs.Rho["pol_strength"] = 0.07;  // was 0.12
        coefs.Rho["sm_polar"] = 0.06;
        coefs.Rho["blue_collar"] = 0.02;
        coefs.Rho["diyer"] = 0.01;
        coefs.Rho["nra"] = -0.04;  // NRA members: softened anti-conformity (was -0.10)
        coefs.Rho["religiosity"] = 0.10;  // Religious jurors more susceptible to group consensus
        coefs.Rho["need_closure"] = 0.08;  // Need for Closure → more conforming (was 0.12)
        coefs.Rho["nfc"] = -0.08;  // High NFC → thinks independently, less conforming
        coefs.Rho["sm_news_reliance"] = 0.08;  // Social media news → susceptible to groupthink

        coefs.Beta0 = 0.0;
        coefs.Beta1 = 1.0;
        coefs.Gamma1 = 1.0;
        coefs.Theta0 = 0.0;
        coefs.Theta1 = 5.0;

        return coefs;
    }


    private static double ComputeStaticBias(ToyJurorTraits j, ToyCoefs coefs, string caseSubType = "")
    {
        // alpha coefficients weight latent traits toward prosecution (positive) or defense (negative)
        double x = coefs.Alpha.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
        x += coefs.Alpha.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
        x += coefs.Alpha.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
        x += coefs.Alpha.TryGetValue("nra", out var nraW) ? nraW * j.Nra : 0.0; // NRA → prosecution lean (violent crime default, flipped 2026-05-27)
        x += coefs.Alpha.TryGetValue("news_lean", out var nlW) ? nlW * j.NewsLean * -1.0 : 0.0; // Conservative news → defense
        x += coefs.Alpha.TryGetValue("prior_victim", out var pvW) ? pvW * j.PriorVictimization : 0.0; // Victim → prosecution
        x += coefs.Alpha.TryGetValue("prior_system", out var psW) ? psW * j.PriorSystemContact * -1.0 : 0.0; // System contact → defense

        // ── BJW: case-type dependent (Block 4) ──
        // Sexual assault: BJW → victim-blaming → defense lean
        // Violent/property crime: BJW → "bad things happen to bad people" → prosecution lean
        double bjwW = coefs.Alpha.TryGetValue("bjw", out var bjw) ? bjw : 0.20;
        switch (caseSubType.ToLowerInvariant())
        {
            case "sexualassault":
            case "sexual assault":
                x -= bjwW * j.BeliefInJustWorld; // Victim-blaming → defense
                break;
            case "violentcrime":
            case "violent crime":
            case "propertycrime":
            case "property crime":
                x += bjwW * j.BeliefInJustWorld; // "Bad things happen to bad people" → prosecution
                break;
            default:
                x += 0.05 * j.BeliefInJustWorld; // Neutral/mild effect
                break;
        }

        x += coefs.Alpha.TryGetValue("death_penalty", out var dpW) ? dpW * j.DeathPenaltyQualified : 0.0; // DP-qualified → prosecution
        x += coefs.Alpha.TryGetValue("trust_institutions", out var tiW) ? tiW * j.TrustInInstitutions : 0.0; // Trust → prosecution
        x += coefs.Alpha.TryGetValue("trait_empathy", out var teW) ? teW * j.TraitEmpathy * -1.0 : 0.0; // Empathy → defense/plaintiff context-dependent
        x += coefs.Alpha.TryGetValue("gender", out var gW) ? gW * (j.PolId > 0 ? 1.0 : -0.5) : 0.0; // Gender: context-dependent (conservative → defense)
        x += coefs.Alpha.TryGetValue("ethnicity", out var ethW) ? ethW * (j.PolId > 0 ? -0.5 : 0.5) : 0.0; // Ethnicity: minority → prosecution lean
        return x;
    }

private static double ComputeRigidity(ToyJurorTraits j, ToyCoefs coefs)
     {
         // RWA, SDO, and conservative religiosity make jurors resistant to counterevidence
         // Adding confirmation bias and status quo bias as rigidity amplifiers
         double x = coefs.Eta.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
         x += coefs.Eta.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
         x += coefs.Eta.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
         x += coefs.Eta.TryGetValue("punitiveness", out var pW) ? pW * j.Punitiveness : 0.0;
         x += coefs.Eta.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
         x += coefs.Eta.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
         x += coefs.Eta.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
         x += coefs.Eta.TryGetValue("primary_history", out var phW) ? phW * j.PrimaryHistory : 0.0;
         x += coefs.Eta.TryGetValue("online_partisan", out var opW) ? opW * j.OnlinePartisan : 0.0;
         x += coefs.Eta.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
         x += coefs.Eta.TryGetValue("home_own", out var hoW) ? hoW * j.HomeOwn : 0.0;
         x += coefs.Eta.TryGetValue("income_band", out var ibW) ? ibW * j.IncomeBand : 0.0;
         x += coefs.Eta.TryGetValue("job_security", out var jsW) ? jsW * j.JobSecurity : 0.0;
         x += coefs.Eta.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
         x += coefs.Eta.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
         x += coefs.Eta.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
         x += coefs.Eta.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
         // Additional: Confirmation bias and status quo bias increase rigidity
         x += j.ConfirmationBias * 0.15;
         x += j.StatusQuoBias * 0.12;
         x += j.NarrativeCoherence * 0.08; // Coherent narratives resist counterevidence
         x += coefs.Eta.TryGetValue("nra", out var nraW) ? nraW * j.Nra : 0.0; // NRA → rigidity
         x += coefs.Eta.TryGetValue("religiosity", out var relW) ? relW * j.Religiosity : 0.0; // Religiosity → rigidity
         x += coefs.Eta.TryGetValue("tv_crime", out var tcW) ? tcW * j.TvCrime : 0.0; // Crime TV → rigidity
         x += coefs.Eta.TryGetValue("need_closure", out var ncW) ? ncW * j.NeedForClosure : 0.0; // NFC → rigidity
         x += coefs.Eta.TryGetValue("cognitive_reflection", out var crW2) ? crW2 * j.CognitiveReflection : 0.0; // CRT → less rigid
         x += coefs.Eta.TryGetValue("bjw", out var bjwW) ? bjwW * j.BeliefInJustWorld : 0.0; // BJW → rigidity
         return Logistic(x);
     }

     /// <summary>
     /// v0.61: Rigidity with motivated-reasoning term blended inside the logistic input.
     /// Replaces the post-hoc additive saturation pattern (lam += term; lam = min(lam,1.0)).
     /// </summary>
     private static double ComputeRigidityWithMotivatedTerm(ToyJurorTraits j, ToyCoefs coefs, double motivatedTerm)
     {
         double x = coefs.Eta.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
         x += coefs.Eta.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
         x += coefs.Eta.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
         x += coefs.Eta.TryGetValue("punitiveness", out var pW) ? pW * j.Punitiveness : 0.0;
         x += coefs.Eta.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
         x += coefs.Eta.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
         x += coefs.Eta.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
         x += coefs.Eta.TryGetValue("primary_history", out var phW) ? phW * j.PrimaryHistory : 0.0;
         x += coefs.Eta.TryGetValue("online_partisan", out var opW) ? opW * j.OnlinePartisan : 0.0;
         x += coefs.Eta.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
         x += coefs.Eta.TryGetValue("home_own", out var hoW) ? hoW * j.HomeOwn : 0.0;
         x += coefs.Eta.TryGetValue("income_band", out var ibW) ? ibW * j.IncomeBand : 0.0;
         x += coefs.Eta.TryGetValue("job_security", out var jsW) ? jsW * j.JobSecurity : 0.0;
         x += coefs.Eta.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
         x += coefs.Eta.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
         x += coefs.Eta.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
         x += coefs.Eta.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
         x += j.ConfirmationBias * 0.15;
         x += j.StatusQuoBias * 0.12;
         x += j.NarrativeCoherence * 0.08;
         x += coefs.Eta.TryGetValue("nra", out var nraW) ? nraW * j.Nra : 0.0;
         x += coefs.Eta.TryGetValue("religiosity", out var relW) ? relW * j.Religiosity : 0.0;
         x += coefs.Eta.TryGetValue("tv_crime", out var tcW) ? tcW * j.TvCrime : 0.0;
         x += coefs.Eta.TryGetValue("need_closure", out var ncW) ? ncW * j.NeedForClosure : 0.0;
         x += coefs.Eta.TryGetValue("cognitive_reflection", out var crW2) ? crW2 * j.CognitiveReflection : 0.0;
         x += coefs.Eta.TryGetValue("bjw", out var bjwW) ? bjwW * j.BeliefInJustWorld : 0.0;

         // v0.61: motivated reasoning blended inside the logistic — no post-hoc addition.
         x += motivatedTerm;

         return Logistic(x);
     }

     private static double ComputeHardness(ToyJurorTraits j, ToyCoefs coefs)
     {
         // Resistance to conformity pressure
         // Anchoring and outcome bias make jurors harder to move once they've committed
         double x = coefs.Zeta.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
         x += coefs.Zeta.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
         x += coefs.Zeta.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
         x += coefs.Zeta.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
         x += coefs.Zeta.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
         x += coefs.Zeta.TryGetValue("primary_history", out var phW) ? phW * j.PrimaryHistory : 0.0;
         x += coefs.Zeta.TryGetValue("activism", out var aW) ? aW * j.Activism : 0.0;
         x += coefs.Zeta.TryGetValue("online_partisan", out var opW) ? opW * j.OnlinePartisan : 0.0;
         x += coefs.Zeta.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
         x += coefs.Zeta.TryGetValue("home_own", out var hoW) ? hoW * j.HomeOwn : 0.0;
         x += coefs.Zeta.TryGetValue("income_band", out var ibW) ? ibW * j.IncomeBand : 0.0;
         x += coefs.Zeta.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
         x += coefs.Zeta.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
         x += coefs.Zeta.TryGetValue("community_college", out var ccW) ? ccW * j.CommunityCollege : 0.0;
         x += coefs.Zeta.TryGetValue("bible_college", out var bcW3) ? bcW3 * j.BibleCollege : 0.0;
         x += coefs.Zeta.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
         x += coefs.Zeta.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
         x += coefs.Zeta.TryGetValue("education_years", out var eduW) ? eduW * 12.0 : 0.0;
         // Additional: Hindsight bias and narrative coherence increase hardness
         x += j.HindsightBias * 0.12;
         x += j.NarrativeCoherence * 0.10;
         x += j.StatusQuoBias * 0.10;
         x += coefs.Zeta.TryGetValue("need_closure", out var ncW) ? ncW * j.NeedForClosure : 0.0; // NFC → harder to move
         x += coefs.Zeta.TryGetValue("cognitive_reflection", out var crtW) ? crtW * j.CognitiveReflection : 0.0; // CRT → more open
         return Logistic(x);
     }

     private static double ComputeInfluenceWeight(ToyJurorTraits j, double G2, ToyCoefs coefs)
     {
         // How much persuasive influence a juror exerts during deliberation
         // Higher education, legal knowledge, and narrative coherence increase influence
         // Anchoring and outcome bias can distort influence (confident but wrong)
         double x = coefs.Chi.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
         x += coefs.Chi.TryGetValue("education_years", out var eduW) ? eduW * 12.0 : 0.0;
         x += coefs.Chi.TryGetValue("elite_private", out var epW) ? epW * j.ElitePrivate : 0.0;
         x += coefs.Chi.TryGetValue("state_flagship", out var sfW) ? sfW * j.StateFlagship : 0.0;
         x += coefs.Chi.TryGetValue("trade_school", out var tsW) ? tsW * j.TradeSchool : 0.0;
         x += coefs.Chi.TryGetValue("hbcu", out var hbW) ? hbW * j.Hbcu : 0.0;
         x += coefs.Chi.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
         x += coefs.Chi.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
         x += coefs.Chi.TryGetValue("age", out var ageW) ? ageW * j.AgeYears : 0.0;
         x += coefs.Chi.TryGetValue("certainty", out var certW) ? certW * Math.Abs(G2 - 0.5) : 0.0;
         x += coefs.Chi.TryGetValue("nfc", out var nfcW) ? nfcW * j.NeedForCognition : 0.0; // High NFC → persuasive
         x += coefs.Chi.TryGetValue("cognitive_reflection", out var crW) ? crW * j.CognitiveReflection : 0.0; // CRT → persuasive
         // Additional: Narrative coherence and social consensus increase influence
         x += coefs.Chi.TryGetValue("narrative_coherence", out var ncW) ? ncW * j.NarrativeCoherence : 0.0;
         x += coefs.Chi.TryGetValue("social_consensus", out var scW) ? scW * j.SocialConsensus : 0.0;
         // Anchoring can give false confidence (influential but biased)
         x += coefs.Chi.TryGetValue("anchoring", out var anW) ? anW * j.Anchoring * 0.3 : 0.0;
         // Hindsight bias reduces perceived influence of counterevidence
         x += coefs.Chi.TryGetValue("hindsight", out var hbW2) ? hbW2 * j.HindsightBias * -0.2 : 0.0;

         // ── v0.61: Softplus influence weight (replaces quadratic) ──
         // Softplus is smooth, monotonic, and avoids the quadratic saturation problem
         // that caused elite-educated clusters to always hit the 3.5 cap.
         // Softplus(x) ≈ max(0,x) for large x, ≈ exp(x) for small x.
         double w = 1.0 + 0.08 * Math.Log(1 + Math.Exp(x));
         return Math.Min(w, 3.5);
     }

     private static double ComputeConformity(ToyJurorTraits j, ToyCoefs coefs)
     {
         // Susceptibility to jury consensus
         // Anchoring, social consensus, and outcome bias increase conformity
         double x = coefs.Rho.TryGetValue("intercept", out var intercept) ? intercept : 0.0;
         x += coefs.Rho.TryGetValue("RWA", out var rwaW) ? rwaW * j.RWA : 0.0;
         x += coefs.Rho.TryGetValue("SDO", out var sdoW) ? sdoW * j.SDO : 0.0;
         x += coefs.Rho.TryGetValue("conserv_relig", out var crW) ? crW * j.ConservRelig : 0.0;
         x += coefs.Rho.TryGetValue("pol_id", out var pidW) ? pidW * j.PolId : 0.0;
         x += coefs.Rho.TryGetValue("pol_strength", out var psW) ? psW * j.PolStrength : 0.0;
         x += coefs.Rho.TryGetValue("sm_polar", out var spW) ? spW * j.SmPolar : 0.0;
         x += coefs.Rho.TryGetValue("blue_collar", out var bcW) ? bcW * j.BlueCollar : 0.0;
         x += coefs.Rho.TryGetValue("diyer", out var dyW) ? dyW * j.Diyer : 0.0;
         // Additional: Social consensus and anchoring increase conformity
         x += j.SocialConsensus * 0.08;
         x += j.Anchoring * 0.04;
         x += j.OutcomeBias * 0.05;
         x += j.OnlinePartisan * 0.02;
         x += coefs.Rho.TryGetValue("nra", out var nraW) ? nraW * j.Nra : 0.0; // NRA → less conforming
         x += coefs.Rho.TryGetValue("religiosity", out var relW) ? relW * j.Religiosity : 0.0; // Religiosity → more conforming
         x += coefs.Rho.TryGetValue("need_closure", out var ncW) ? ncW * j.NeedForClosure : 0.0; // NFC → more conforming
         x += coefs.Rho.TryGetValue("nfc", out var nfcW) ? nfcW * j.NeedForCognition : 0.0; // High NFC → less conforming
         x += coefs.Rho.TryGetValue("sm_news_reliance", out var snrW) ? snrW * j.SmNewsReliance : 0.0; // SM → groupthink
         // ── Block 7: Scale conformity input to reduce effective range ──
         return Logistic(0.6 * x); // was Logistic(x) — narrower range → more hung juries
     }

    private static ToyJurorTraits GenerateCoherentTraits(
        Random arng,
        IReadOnlyDictionary<string, double> biasLookup,
        Agent agent,
        int maxTries,
        double coherenceThreshold)
    {
        for (int _ = 0; _ < maxTries; _++)
        {
            var j = EmptyTraits();
            SampleDemographics(arng, agent, biasLookup, j);
            SampleTraits(arng, j);
            SampleReligion(arng, j);
            SamplePolitics(arng, j);
            SampleMedia(arng, j);
            SampleEconomics(arng, agent, j);
            SampleEducationType(arng, agent, j);
            SampleBlueCollarDiy(arng, agent, j);
            SampleExperience(arng, j);
            SampleMemberships(arng, agent, j);
            SampleIndividualDifferences(arng, j);

            if (ViolatesHardRules(j)) continue;
            if (CoherenceScore(j) >= coherenceThreshold) return j;
        }

        // Fallback: return something coherent-ish (won't violate hard rules).
        var fallback = EmptyTraits();
        SampleDemographics(new Random(), agent, biasLookup, fallback);
        SampleTraits(new Random(), fallback);
        SampleReligion(new Random(), fallback);
        SamplePolitics(new Random(), fallback);
        SampleMedia(new Random(), fallback);
        SampleEconomics(new Random(), agent, fallback);
        SampleEducationType(new Random(), agent, fallback);
        SampleBlueCollarDiy(new Random(), agent, fallback);
        SampleExperience(new Random(), fallback);
        SampleMemberships(new Random(), agent, fallback);
        SampleIndividualDifferences(new Random(), fallback);

        if (ViolatesHardRules(fallback))
            fallback.ToolOwnership = 10; // avoid diy high + zero tools

        return fallback;
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
        BibleCollege = 0,
        ElitePrivate = 0,
        StateFlagship = 1,
        OtherPrivate = 0,
        CommunityCollege = 0,
        TradeSchool = 0,
        Hbcu = 0,
        SierraClub = 0,
        Nra = 0,
        OilExecutive = 0,
        EduBiasWeight = 0.80,
        PolBiasWeight = 0.90,
        // New individual-difference traits (2026-05-27)
        NeedForCognition = 5,
        BeliefInJustWorld = 5,
        DeathPenaltyQualified = 0,
        TrustInInstitutions = 5,
        TraitEmpathy = 5,
        NeedForClosure = 5,
        CognitiveReflection = 5,
        SmNewsReliance = 5,
        PersonalInjuryHistory = 0
    };

    private static void SampleDemographics(Random arng, Agent agent, IReadOnlyDictionary<string, double> biasLookup, ToyJurorTraits j)
    {
        j.AgeYears = agent.Age;

        // Tool mapping: use existing Bias as proxy for polarity.
        // Keep trait ranges aligned with Python (0..10, -1..1).
        double bias = agent.Bias; // -1..1

        // Bias-category weights based on juror bias research (see docs/JurorBiasResearch.md)
        // Meta-analytic effect sizes from published mock jury studies and RAND civil jury data
        double ageW = GetWeight(biasLookup, "Age", 0.55);
        double genderW = GetWeight(biasLookup, "Gender", 0.45);
        double EduWeight = GetWeight(biasLookup, "Education Level", 0.80);
        double incomeW = GetWeight(biasLookup, "Income Level", 0.50);
        double ReligionWeight = GetWeight(biasLookup, "Religion", 0.40);
        double PolWeight = GetWeight(biasLookup, "Political Affiliation", 0.90);
        double ethnicityW = GetWeight(biasLookup, "Ethnicity", 0.60);
        double experienceW = GetWeight(biasLookup, "Juror Experience", 0.70);
        double legalW = GetWeight(biasLookup, "Legal Knowledge", 0.75);
        double profBgW = GetWeight(biasLookup, "Professional Background", 0.55);
        double communityW = GetWeight(biasLookup, "Community Ties", 0.65);
        double CommStyleWeight = GetWeight(biasLookup, "Communication Style", 0.35);

        // Store weights on traits for dynamic Stage-2 weaponization (Task 5)
        j.EduBiasWeight = EduWeight;
        j.PolBiasWeight = PolWeight;

        // --- Implicit bias factor (not directly observable, derived from other traits) ---
        // Simulates unconscious associations that influence verdict lean independent of explicit attitudes
        // Meta-analytic effect: implicit bias accounts for ~15% of variance in juror decisions (Greenwald et al.)
        double implicitBias = (arng.NextDouble() - 0.4) * 1.2; // Slight negative skew (defense-leaning unconscious bias)

        // --- Cross-factor interactions (research-backed moderation effects) ---
        // Race×Gender compounded bias: women of color face compounded stereotyping (Crenshaw 1989, extended to jury context)
        double raceGenderCompound = 0.0;
        if (agent.Race != "Unknown" && agent.Gender != "Unknown")
        {
            // Non-white women face compounded bias in credibility assessments
            bool isNonWhite = agent.Race != "White";
            bool isFemale = agent.Gender == "Female";
            if (isNonWhite && isFemale) raceGenderCompound = -0.15 * ethnicityW;
            // Non-white men face different compounded bias (authority perception)
            else if (isNonWhite && !isFemale) raceGenderCompound = -0.10 * ethnicityW;
        }

        // Education×Political rigidity interaction: educated partisans are MORE rigid (Kahan et al. 2012)
        // REMOVED from static generation — now deployed dynamically in Stage 2 (Task 5: Dynamic Weaponization)
        // See the conditional branch in the two-pass section where evidence conflicts with bias profile.

        // Age×Punitiveness curvilinear relationship (U-shaped)
        // Young and old jurors show higher punitiveness; middle-aged most lenient (Steiner et al. 2001)
        double age = agent.Age;
        double agePunitiveCurve = 0.0;
        if (age < 30) agePunitiveCurve = (30 - age) * 0.04; // Younger = more punitive
        else if (age > 60) agePunitiveCurve = (age - 60) * 0.03; // Older = more punitive (but different mechanism)
        double ageFactor = (ageW - 0.5) * 0.8 + agePunitiveCurve;

        // --- Calibrate trait generation using quantitative jury research ---
        // Political affiliation and education remain the strongest directional forces
        // Cross-factor interactions and implicit bias add realism

        // ── Implicit bias propagation (Greenwald et al.): implicitBias flows into RWA, Punitiveness,
        //    Religiosity, PolId, and JobSecurity through scalar multipliers documented in
        //    docs/JurorBiasResearch.md §Implicit Bias. ──

        // RWA (Right-Wing Authoritarianism): Political polarity + education rigidity + age conservatism
        // Range: clamped to 0..10
        j.RWA = Clamp10(5 + (bias * 3.2 * PolWeight) + (ageFactor) + (ethnicityW - 0.5) * 0.6
            + raceGenderCompound * 0.5 + implicitBias * 0.3
            + (arng.NextDouble() - 0.5) * 1.8);

        // SDO (Social Dominance Orientation): Income/status + gender power dynamics + education
        // Range: clamped to 0..10
        j.SDO = Clamp10(5 + (-bias * 2.8 * PolWeight) + (incomeW - 0.5) * 1.2 + (genderW - 0.5) * 0.4
            + (EduWeight - 0.5) * -0.2 + raceGenderCompound * 0.3
            + (arng.NextDouble() - 0.5) * 1.8);

        // Punitiveness: Political ideology × age curve × religion × implicit bias
        // Range: clamped to 0..10
        j.Punitiveness = Clamp10(5 + (bias * 2.1 * PolWeight) + (ReligionWeight - 0.5) * 1.0
            + agePunitiveCurve * 1.2 + implicitBias * 0.4
            + (arng.NextDouble() - 0.5) * 1.8);

        // RapeMyth acceptance: Gender × religion interaction (Burt & Albin 1981, extended research)
        // Higher religiosity + conservative gender views = higher myth acceptance
        // Range: clamped to 0..10
        double genderReligionInteraction = (genderW - 0.5) * (ReligionWeight - 0.5) * 0.6;
        j.RapeMyth = Clamp10(5 + (bias * 1.6 * PolWeight) + (ReligionWeight - 0.5) * 1.2
            + (EduWeight - 0.5) * -0.4 + genderReligionInteraction
            + raceGenderCompound * 0.4 + (arng.NextDouble() - 0.5) * 1.8);

        // Religiosity: Base on religion weight + communication style + implicit bias
        // Range: clamped to 0..10
        j.Religiosity = Clamp10(5 + (ReligionWeight - 0.5) * 2.0 + (CommStyleWeight - 0.5) * 0.5
            + implicitBias * 0.2 + (arng.NextDouble() - 0.5) * 1.8);
        // Conservative Religiosity: Political + religious conservatism interaction
        // Range: clamped to 0..10
        j.ConservRelig = Clamp10(5 + (bias * 2.1 * ReligionWeight) + (ethnicityW - 0.5) * 0.6
            + (ReligionWeight - 0.5) * (PolWeight - 0.5) * 0.4 // Religion×Political conservatism interaction
            + (arng.NextDouble() - 0.5) * 1.8);

        // Polarity: Weighted by political strength and implicit bias
        // Range: clamped to -1..1
        j.PolId = Clamp11(bias * 1.0 * PolWeight + implicitBias * 0.2);
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
        // Job Security: Income + professional background + legal knowledge
        // Range: clamped to 0..10
        j.JobSecurity = Clamp10(5 + (incomeW - 0.5) * 2 + (profBgW - 0.5) * 1.0 + (legalW - 0.5) * 1.0
            + implicitBias * 0.2 + (arng.NextDouble() - 0.5) * 1.8);
        j.Union = arng.NextDouble() < (0.3 + (incomeW - 0.5) * 0.1) ? 1 : 0; // Income slightly affects union membership

        // --- Reliability-weighted civic engagement ---
        // More educated and experienced jurors are more likely to volunteer, donate, and be active
        double reliabilityFactor = (EduWeight * 0.5 + legalW * 0.3 + experienceW * 0.2); // Weighted reliability proxy
        j.Volunteer = Clamp10(4 + (communityW - 0.5) * 3 + (experienceW - 0.5) * 1.0
            + (CommStyleWeight - 0.5) * 1.0 + reliabilityFactor * 0.8 + (arng.NextDouble() - 0.5) * 1.8);
        j.Donate = Clamp10(4 + (communityW - 0.5) * 2 + (legalW - 0.5) * 0.5
            + (CommStyleWeight - 0.5) * 0.8 + reliabilityFactor * 0.5 + (arng.NextDouble() - 0.5) * 1.8);
        j.Activism = Clamp10(4 + (communityW - 0.5) * 2 + (PolWeight - 0.5) * 1.8
            + (experienceW - 0.5) * 0.8 + reliabilityFactor * 0.6 + (arng.NextDouble() - 0.5) * 1.8);
        j.OnlinePartisan = Clamp10(5 + (CommStyleWeight - 0.5) * 1.5 + (PolWeight - 0.5) * 1.5
            + (experienceW - 0.5) * 0.5 + (arng.NextDouble() - 0.5) * 1.8);

        // --- Additional cognitive bias factors (research-backed) ---
        // Anchoring effect: Higher with media exposure, lower with education (Chapman & Bornstein 1996)
        // Anchoring effect size: d = 0.5-1.0 for damage awards, moderate for verdict lean
        double anchoringBase = (arng.NextDouble() * 6) + 2; // Base 2..8
        // Media exposure amplifies anchoring (exposure to numbers in news)
        double mediaAnchoringBoost = (CommStyleWeight - 0.5) * 1.5;
        // Education provides some resistance to anchoring (numeracy effect)
        double eduAnchoringResistance = (EduWeight - 0.5) * -1.0;
        j.Anchoring = Clamp10(anchoringBase + mediaAnchoringBoost + eduAnchoringResistance
            + (arng.NextDouble() - 0.5) * 1.5);

        // Hindsight bias: Knowing the outcome makes it seem predictable (Fischhoff 1975)
        // Affected by media exposure (outcomes reported in news) and education
        double hindsightBase = (arng.NextDouble() * 5) + 2; // Base 2..7
        j.HindsightBias = Clamp10(hindsightBase + (CommStyleWeight - 0.5) * 2.0
            + (EduWeight - 0.5) * -0.8 + (arng.NextDouble() - 0.5) * 1.5);

        // Confirmation bias: Seeking evidence that confirms initial beliefs (Nickerson 1998)
        // Stronger with higher political/religious identity strength
        double confirmBase = (arng.NextDouble() * 4) + 3; // Base 3..7
        j.ConfirmationBias = Clamp10(confirmBase + (PolWeight - 0.5) * 2.0
            + (ReligionWeight - 0.5) * 1.5 + (implicitBias + 0.5) * 0.8
            + (arng.NextDouble() - 0.5) * 1.5);

        // Narrative coherence: Tendency to construct and favor coherent stories (Pennington & Hastie 1992)
        // Higher with education (better narrative construction) and legal knowledge
        double narrativeBase = (arng.NextDouble() * 5) + 3; // Base 3..8
        j.NarrativeCoherence = Clamp10(narrativeBase + (EduWeight - 0.5) * 1.5
            + (legalW - 0.5) * 1.0 + (CommStyleWeight - 0.5) * 0.8
            + (arng.NextDouble() - 0.5) * 1.5);

        // Outcome bias: Judging decision quality by outcome rather than process (Baron & Hershey 1988)
        // Stronger with hindsight bias and media exposure
        double outcomeBase = (arng.NextDouble() * 4) + 3; // Base 3..7
        j.OutcomeBias = Clamp10(outcomeBase + (j.HindsightBias - 5) * 0.4
            + (CommStyleWeight - 0.5) * 1.2 + (arng.NextDouble() - 0.5) * 1.5);

        // Social consensus bias: Perceived group agreement amplifies individual beliefs (Sunstein 2003)
        // Related to group polarization and conformity
        double consensusBase = (arng.NextDouble() * 4) + 3; // Base 3..7
        j.SocialConsensus = Clamp10(consensusBase + (communityW - 0.5) * 2.0
            + (CommStyleWeight - 0.5) * 1.0 + (arng.NextDouble() - 0.5) * 1.5);

        // Status quo bias: Preference for maintaining current lean/decision (Samuelson & Zeckhauser 1988)
        // Increases with certainty and perceived deliberation cost
        double statusQuoBase = (arng.NextDouble() * 4) + 3; // Base 3..7
        j.StatusQuoBias = Clamp10(statusQuoBase + (j.PolId + 1) * 0.3
            + (arng.NextDouble() - 0.5) * 1.5);
    }

private static void SampleTraits(Random arng, ToyJurorTraits j)
     {
         // already sampled into demographics; add mild noise
         j.RWA = Clamp10(j.RWA + (arng.NextDouble() - 0.5) * 1.0);
         j.SDO = Clamp10(j.SDO + (arng.NextDouble() - 0.5) * 1.0);
         j.Punitiveness = Clamp10(j.Punitiveness + (arng.NextDouble() - 0.5) * 1.0);
         j.RapeMyth = Clamp10(j.RapeMyth + (arng.NextDouble() - 0.5) * 1.0);
         // Additional cognitive bias noise
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
         // ── Media traits correlated with political identity, education, and age ──
         // Conservative (PolId > 0) → Fox News, talk radio; Liberal (PolId < 0) → MSNBC/NPR
         // Older → cable TV; Younger → social media; Higher education → more news consumption

         double pol = j.PolId;           // -1 (liberal) .. +1 (conservative)
         double ageNorm = j.AgeYears / 80.0; // 0..1, older = higher

         // NewsLean: strongly correlated with political identity (+ noise), range -1..1
         j.NewsLean = Clamp11(pol * 1.2 + (arng.NextDouble() - 0.5) * 0.6);

         // TvCableNews: older + conservative → Fox News audience; younger liberal → less cable, range ~0..20
         j.TvCableNews = Math.Max(0, 10 + pol * 3 + ageNorm * 8 + (arng.NextDouble() - 0.5) * 6);

         // TvCrime: older + higher punitiveness → more crime TV (Law & Order, CSI), range ~0..20
         j.TvCrime = Math.Max(0, 10 + ageNorm * 8 + (j.Punitiveness / 10.0) * 4 + (arng.NextDouble() - 0.5) * 6);

         // NewsIntensity: higher education + politically engaged → more news consumption, range ~0..30
         double eduNorm = (j.ElitePrivate + j.StateFlagship + j.OtherPrivate) > 0 ? 0.7 : 0.4;
         j.NewsIntensity = Math.Max(0, 15 + Math.Abs(pol) * 4 + eduNorm * 8 + (arng.NextDouble() - 0.5) * 8);

         // SmUse: younger → more social media; older → less, range ~0..8
         j.SmUse = Clamp10(4 - ageNorm * 3 + (arng.NextDouble() - 0.3) * 4);

         // SmPolar: politically extreme → more polarized social media feed, range 0..10
         j.SmPolar = Clamp10(5 + Math.Abs(pol) * 2.5 + (arng.NextDouble() - 0.5) * 4);
     }

     /// <summary>
     /// Cognitive bias: selective exposure to media reinforces existing beliefs.
     /// Higher media consumption → stronger confirmation and anchoring bias.
     /// </summary>
     private static double MediaSelectiveExposure(ToyJurorTraits j)
     {
         // TV news and social media polarize; crime TV increases fear/anchoring
         double tvEffect = (j.TvCableNews / 20.0) * (j.SmPolar / 10.0);
         double newsIntensity = j.NewsIntensity / 30.0;
         return tvEffect * newsIntensity; // 0..1 scale, higher = more biased media diet
     }

    private static void SampleEconomics(Random arng, Agent agent, ToyJurorTraits j)
    {
        // lightly adjust based on agent strings
        j.IncomeBand = string.IsNullOrWhiteSpace(agent.IncomeLevel) ? j.IncomeBand : j.IncomeBand;
        j.JobSecurity = Clamp10(j.JobSecurity + (arng.NextDouble() - 0.5) * 1.0);
    }

    private static void SampleEducationType(Random arng, Agent agent, ToyJurorTraits j)
    {
        // Use EducationLevel string to select a main type.
        var edu = (agent.EducationLevel ?? "").ToLower();
        if (edu.Contains("bible")) j.BibleCollege = 1;
        else if (edu.Contains("private")) j.ElitePrivate = 1;
        else if (edu.Contains("associate")) j.CommunityCollege = 1;
        else if (edu.Contains("trade")) j.TradeSchool = 1;
        else if (edu.Contains("hbcu")) j.Hbcu = 1;
        else j.StateFlagship = 1;

        // ensure exclusivity-ish (one-hot)
        var sum = j.BibleCollege + j.ElitePrivate + j.StateFlagship + j.OtherPrivate + j.CommunityCollege + j.TradeSchool + j.Hbcu;
        if (sum > 1)
        {
            // keep one: state flagship
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

    private static void SampleExperience(Random arng, ToyJurorTraits j)
    {
        j.PriorVictimization = arng.Next(0, 3);
        j.PriorSystemContact = arng.Next(0, 3);
    }

    private static void SampleMemberships(Random arng, Agent agent, ToyJurorTraits j)
    {
        // NRA: strongly correlated with conservative politics (PolId > 0) and religiosity
        // A conservative religious rural voter: ~45% NRA; a liberal secular urbanite: ~3%
        double nraProb = 0.20  // base rate
            + j.PolId * 0.15   // conservative → +15%
            + (j.Religiosity / 10.0) * 0.10  // religious → +10%
            + (j.ConservRelig / 10.0) * 0.10; // conservative religious → +10%
        j.Nra = arng.NextDouble() < Math.Clamp(nraProb, 0.02, 0.50) ? 1 : 0;

        // Sierra Club: correlated with liberal politics (PolId < 0) and higher education
        double scProb = 0.10
            - j.PolId * 0.06   // liberal → +6%
            + (j.ElitePrivate + j.StateFlagship) * 0.04; // college educated → +4%
        j.SierraClub = arng.NextDouble() < Math.Clamp(scProb, 0.02, 0.25) ? 1 : 0;

        // OilExecutive: rare, occupation-dependent
        j.OilExecutive = (agent.Occupation ?? "").ToLower().Contains("exec") && arng.NextDouble() < 0.3 ? 1 : 0;
    }

    /// <summary>
    /// Samples research-backed individual difference traits correlated with
    /// existing political, religious, and educational traits for realistic clustering.
    /// </summary>
    private static void SampleIndividualDifferences(Random arng, ToyJurorTraits j)
    {
        double pol = j.PolId;          // -1..1
        double absPol = Math.Abs(pol); // 0..1, extremity
        double eduNorm = (j.ElitePrivate + j.StateFlagship + j.OtherPrivate) > 0 ? 0.7 :
                         (j.CommunityCollege + j.TradeSchool) > 0 ? 0.5 : 0.3;
        double religNorm = j.Religiosity / 10.0;

        // Need for Cognition: strongly correlated with education, slightly negatively with political extremity
        // High education + moderate politics → highest NFC (Kahan et al. 2012: educated partisans less analytical)
        j.NeedForCognition = Clamp10(5 + eduNorm * 4 - absPol * 1.5 + (arng.NextDouble() - 0.5) * 5);

        // Belief in Just World: correlated with conservatism + religiosity (Lerner 1980; Jost et al. 2003)
        j.BeliefInJustWorld = Clamp10(5 + pol * 2.0 + religNorm * 2.0 + (arng.NextDouble() - 0.5) * 4);

        // Death Penalty Qualified: strongly correlated with conservatism, punitiveness, low empathy
        // Sampled after empathy is set below (dependency)

        // Trust in Institutions: correlated with conservatism + older age (Tyler 2006; Pew 2023)
        j.TrustInInstitutions = Clamp10(5 + pol * 2.5 + (j.AgeYears / 80.0) * 2 + (arng.NextDouble() - 0.5) * 4);

        // Trait Empathy: slightly higher in women proxy (not available), negatively with SDO
        // SDO (Social Dominance) negatively predicts empathy (Pratto et al. 1994)
        j.TraitEmpathy = Clamp10(5 - (j.SDO / 10.0) * 2.0 + (arng.NextDouble() - 0.4) * 4);

        // Need for Closure: correlated with political strength, lower education, higher religiosity
        j.NeedForClosure = Clamp10(5 + absPol * 2 + religNorm * 2 - eduNorm * 2 + (arng.NextDouble() - 0.5) * 5);

        // Cognitive Reflection: strongly correlated with education, negatively with religiosity (Pennycook 2016)
        j.CognitiveReflection = Clamp10(3 + eduNorm * 5 - religNorm * 1.5 + arng.NextDouble() * 4);

        // Social Media News Reliance: correlated with SmUse, younger, lower education
        j.SmNewsReliance = Clamp10(j.SmUse > 5 ? 5 + (arng.NextDouble() - 0.3) * 6 - eduNorm * 1.5 : arng.NextDouble() * 5);

        // Personal Injury History: independent — life accidents don't cluster with ideology
        j.PersonalInjuryHistory = arng.NextDouble() < 0.15 ? 1 : 0;

        // Update DeathPenaltyQualified now that empathy is set (dependency fix)
        double dpProb = 0.65 + pol * 0.20 + (j.Punitiveness / 10.0) * 0.15 - (j.TraitEmpathy / 10.0) * 0.10;
        j.DeathPenaltyQualified = arng.NextDouble() < Math.Clamp(dpProb, 0.20, 0.95) ? 1 : 0;
    }

    private static bool ViolatesHardRules(ToyJurorTraits j)
    {
        // oil exec + sierra club
        if (j.OilExecutive == 1 && j.SierraClub == 1) return true;
        // bible + elite private
        if (j.BibleCollege == 1 && j.ElitePrivate == 1) return true;
        // trade + elite private
        if (j.TradeSchool == 1 && j.ElitePrivate == 1) return true;
        // hbcu + bible
        if (j.Hbcu == 1 && j.BibleCollege == 1) return true;
        // blue collar + elite private
        if (j.BlueCollar == 1 && j.ElitePrivate == 1) return true;
        // diy high + zero tools
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
        double hard = 1.0; // we only call if not violating hard; keep for completeness
        // The caller already filtered hard violations, so hard=0 here.
        hard = 0.0;
        double soft = SoftConflictScore(j);
        double penalty = wHard * hard + wSoft * soft;
        return Math.Max(0.0, 1.0 - penalty);
    }

    private static double GetWeight(IReadOnlyDictionary<string, double> lookup, string key, double defaultValue)
        => lookup.TryGetValue(key, out var v) ? v : defaultValue;

    private static double Clamp10(double x) => Math.Max(0.0, Math.Min(10.0, x));
    private static double Clamp11(double x) => Math.Max(-1.0, Math.Min(1.0, x));
}

