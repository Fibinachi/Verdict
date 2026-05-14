using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Tests;

public class ModelTests : BaseTestClass
{
    public static async Task<(int passed, int failed, List<string> failures)> GetTestResults()
    {
        Console.WriteLine("\n=== MODEL TESTS ===");

        ResetCounters();
        
        TestAgentModel();
        TestCaseFileModel();
        TestAddDefaultModels();
        TestEvidenceDocumentModel();
        TestMemoryEntryModel();
        TestCourtroomEventModel();
        TestAIModelConfiguration();
        TestJuryInstructions();
        TestVerdictForm();
        TestExtractedEntities();
        TestAgentInteractionModels();
        TestChargeAndCauseOfAction();
        TestObservableObject();

        return GetResults();
    }

    public static async Task RunAllTests()
    {
        var results = await GetTestResults();
        Console.WriteLine($"  MODEL TESTS RESULT: {results.passed} passed, {results.failed} failed");
    }

    private static void TestAgentModel()
    {
        Console.WriteLine("\n─── Agent Model ───");
        
        // Basic creation
        var agent = new Agent { Role = AgentRole.Juror, Name = "Test Juror", Bias = 0.5 };
        Assert(agent.Name == "Test Juror", "Agent name set correctly");
        Assert(agent.Role == AgentRole.Juror, "Agent role set correctly");
        Assert(agent.Bias == 0.5, "Agent bias set correctly");
        Assert(agent.Sentiment == 0.5, "Agent default sentiment is 0.5");
        Assert(agent.VerdictLean == 0.5, "Agent default verdict lean is 0.5");
        Assert(!agent.IsOccupied, "Agent default IsOccupied is false");
        Assert(agent.CanVote, "Juror CanVote is true");
        Assert(agent.HasOpinion, "Juror HasOpinion is true");
        Assert(agent.CanDeliberate, "Juror CanDeliberate is true");
        Assert(!agent.IsClient, "Juror IsClient is false");

        // Role-based properties
        var judge = new Agent { Role = AgentRole.Judge };
        Assert(!judge.CanVote, "Judge CanVote is false");
        Assert(judge.HasOpinion, "Judge HasOpinion is true");
        Assert(!judge.CanDeliberate, "Judge CanDeliberate is false");

        var reporter = new Agent { Role = AgentRole.Reporter };
        Assert(!reporter.HasOpinion, "Reporter HasOpinion is false");
        Assert(!reporter.CanVote, "Reporter CanVote is false");

        var client = new Agent { Role = AgentRole.Client };
        Assert(client.IsClient, "Client IsClient is true");

        var alternate = new Agent { Role = AgentRole.AlternateJuror };
        Assert(alternate.CanVote, "AlternateJuror CanVote is true");
        Assert(alternate.CanDeliberate, "AlternateJuror CanDeliberate is true");

        // Sentiment analysis
        double initialSentiment = agent.Sentiment;
        agent.AnalyzeSentiment("The witness lied today.");
        Assert(agent.Sentiment < initialSentiment, "Sentiment decreases on 'lied' keyword");

        agent.Sentiment = 0.5;
        agent.AnalyzeSentiment("The expert provided DNA evidence.");
        Assert(agent.Sentiment > 0.5, "Sentiment increases on 'expert'/'DNA' keywords");

        agent.Sentiment = 0.5;
        agent.AnalyzeSentiment("Objection! Overruled!");
        Assert(agent.Sentiment < 0.5, "Sentiment decreases on 'objection'/'overruled'");

        agent.Sentiment = 0.5;
        agent.AnalyzeSentiment("Sustained.");
        Assert(agent.Sentiment > 0.5, "Sentiment increases on 'sustained'");

        // Reporter remains neutral
        reporter.Sentiment = 0.5;
        reporter.AnalyzeSentiment("The witness lied today.");
        Assert(reporter.Sentiment == 0.5, "Reporter sentiment remains neutral");

        // Verdict lean updates
        agent.VerdictLean = 0.5;
        agent.UpdateVerdictLean(0.1);
        Assert(agent.VerdictLean > 0.5, "Verdict lean increases on positive influence");
        Assert(agent.VerdictLean <= 1.0, "Verdict lean clamped to max 1.0");

        agent.VerdictLean = 0.5;
        agent.UpdateVerdictLean(-0.3);
        Assert(agent.VerdictLean < 0.5, "Verdict lean decreases on negative influence");
        Assert(agent.VerdictLean >= 0.0, "Verdict lean clamped to min 0.0");

        // Reporter cannot update verdict lean
        reporter.VerdictLean = 0.5;
        reporter.UpdateVerdictLean(0.1);
        Assert(reporter.VerdictLean == 0.5, "Reporter verdict lean does not change");

        // Trial events
        agent.RecordTrialEvent("Evidence 1 admitted", 0.1);
        Assert(agent.TrialEvents.Any(e => e.Content == "Evidence 1 admitted"), "Trial event recorded");
        Assert(agent.TrialEvents.First(e => e.Content == "Evidence 1 admitted").Strength > 1.0, "Trial event strength includes bias influence");

        // Reinforce existing trial event
        agent.RecordTrialEvent("Evidence 1 admitted", 0.1);
        var existing = agent.TrialEvents.First(e => e.Content == "Evidence 1 admitted");
        Assert(existing.Strength > 1.1, "Existing trial event strength reinforced");

        // Memories
        agent.ReinforceMemory("Background memory", 0.0);
        Assert(agent.Memories.Any(m => m.Content == "Background memory"), "Memory recorded");

        // Memory decay
        agent.Memories.Add(new MemoryEntry { Content = "Test", Strength = 1.0 });
        agent.DecayMemories(0.5);
        Assert(agent.Memories.Any(m => m.Content == "Test" && m.Strength == 0.5), "Memory decay works");

        // CopyTo
        var source = new Agent
        {
            Name = "Source",
            Role = AgentRole.Judge,
            Gender = "Male",
            Age = 45,
            Race = "White",
            Occupation = "Lawyer",
            EducationLevel = "JD",
            IncomeLevel = "Upper Class",
            Bias = 0.3,
            VerdictLean = 0.7,
            Sentiment = 0.8,
            IsOccupied = true,
            SystemPrompt = "Test prompt",
            Profile = "Test profile"
        };
        var target = new Agent();
        source.CopyTo(target);
        Assert(target.Name == "Source", "CopyTo copies Name");
        Assert(target.Role == AgentRole.Judge, "CopyTo copies Role");
        Assert(target.Gender == "Male", "CopyTo copies Gender");
        Assert(target.Age == 45, "CopyTo copies Age");
        Assert(target.Bias == 0.3, "CopyTo copies Bias");
        Assert(target.VerdictLean == 0.7, "CopyTo copies VerdictLean");
        Assert(target.Sentiment == 0.8, "CopyTo copies Sentiment");
        Assert(target.IsOccupied, "CopyTo sets IsOccupied to true");
        Assert(target.SystemPrompt == "Test prompt", "CopyTo copies SystemPrompt");

        // Default values
        var defaultAgent = new Agent();
        Assert(defaultAgent.Name == "New Agent", "Default agent name is 'New Agent'");
        Assert(defaultAgent.Role == AgentRole.Judge, "Default agent role is Judge (first enum)");
        Assert(defaultAgent.SpecializedKnowledge == "None (General Public)", "Default specialized knowledge");
        Assert(defaultAgent.CurrentStatus == "Attentive", "Default current status");
        Assert(defaultAgent.PoliticalAffiliation == "Independent", "Default political affiliation");
        Assert(defaultAgent.MaritalStatus == "Single", "Default marital status");
        Assert(defaultAgent.ParentalStatus == "No Children", "Default parental status");
        Assert(defaultAgent.IncomeLevel == "Middle Class", "Default income level");
        Assert(defaultAgent.EducationLevel == "High School", "Default education level");
        Assert(defaultAgent.MediaConsumption == "Mainstream News", "Default media consumption");
        Assert(defaultAgent.RiskPerception == 0.5, "Default risk perception");
        Assert(string.IsNullOrEmpty(defaultAgent.SelectedModel), "Default selected model is empty");
    }

    private static void TestCaseFileModel()
    {
        Console.WriteLine("\n─── CaseFile Model ───");

        var caseFile = new CaseFile();
        Assert(caseFile.CaseName == "Untitled Case", "Default case name");
        Assert(caseFile.CaseNumber == "00-0000", "Default case number");
        Assert(caseFile.CourtName == "Superior Court", "Default court name");
        Assert(caseFile.Mode == CaseMode.Civil, "Default mode is Civil");
        Assert(caseFile.Jurisdiction == JurisdictionType.State, "Default jurisdiction is State");
        Assert(caseFile.JurorCount == 12, "Default juror count is 12");
        Assert(caseFile.TrialPhase == TrialPhase.Discovery, "Default trial phase is Discovery");
        Assert(caseFile.CurrentDebateStage == CourtPhase.OpeningStatements, "Default debate stage");
        // Constructor no longer adds models; must call AddDefaultModels() explicitly
        Assert(caseFile.AvailableModels.Count == 0, "Constructor does not pre-configure models");
        Assert(caseFile.Agents.Count == 0, "Default agents list is empty");
        Assert(caseFile.Evidence.Count == 0, "Default evidence list is empty");
        Assert(caseFile.EventLog.Count == 0, "Default event log is empty");
        Assert(caseFile.Pleadings.Count == 0, "Default pleadings list is empty");
        Assert(caseFile.Instructions != null, "Instructions initialized");
        Assert(caseFile.Verdict != null, "Verdict form initialized");

        // Property changes
        caseFile.CaseName = "Test Case";
        Assert(caseFile.CaseName == "Test Case", "CaseName property change");
        
        caseFile.CaseNumber = "23-CV-1234";
        Assert(caseFile.CaseNumber == "23-CV-1234", "CaseNumber property change");
        
        caseFile.Mode = CaseMode.Criminal;
        Assert(caseFile.Mode == CaseMode.Criminal, "Mode property change");
        
        caseFile.Jurisdiction = JurisdictionType.Federal;
        Assert(caseFile.Jurisdiction == JurisdictionType.Federal, "Jurisdiction property change");
        
        caseFile.TrialPhase = TrialPhase.Trial;
        Assert(caseFile.TrialPhase == TrialPhase.Trial, "TrialPhase property change");
        
        caseFile.CurrentDebateStage = CourtPhase.ClosingArguments;
        Assert(caseFile.CurrentDebateStage == CourtPhase.ClosingArguments, "CurrentDebateStage property change");
        
        caseFile.EstimatedSettlement = 500000;
        Assert(caseFile.EstimatedSettlement == 500000, "EstimatedSettlement property change");
        
        caseFile.InsuranceReserve = 625000;
        Assert(caseFile.InsuranceReserve == 625000, "InsuranceReserve property change");

        // EnsureCollectionsInitialized
        var emptyCase = new CaseFile();
        emptyCase.Agents = null;
        emptyCase.AvailableModels = null;
        emptyCase.Instructions = null;
        emptyCase.Verdict = null;
        emptyCase.Pleadings = null;
        emptyCase.Evidence = null;
        emptyCase.EventLog = null;
        emptyCase.EnsureCollectionsInitialized();
        Assert(emptyCase.Agents != null, "EnsureCollectionsInitialized creates Agents");
        Assert(emptyCase.AvailableModels != null, "EnsureCollectionsInitialized creates AvailableModels");
        Assert(emptyCase.Instructions != null, "EnsureCollectionsInitialized creates Instructions");
        Assert(emptyCase.Verdict != null, "EnsureCollectionsInitialized creates Verdict");
        Assert(emptyCase.Pleadings != null, "EnsureCollectionsInitialized creates Pleadings");
        Assert(emptyCase.Evidence != null, "EnsureCollectionsInitialized creates Evidence");
        Assert(emptyCase.EventLog != null, "EnsureCollectionsInitialized creates EventLog");
    }

    private static void TestAddDefaultModels()
    {
        Console.WriteLine("\n─── AddDefaultModels ───");

        // Fresh CaseFile should have no models
        var cf = new CaseFile();
        Assert(cf.AvailableModels.Count == 0, "Fresh CaseFile has no models");

        // Calling AddDefaultModels adds 3 default models
        cf.AddDefaultModels();
        Assert(cf.AvailableModels.Count == 3, "AddDefaultModels adds 3 models");
        Assert(cf.AvailableModels[0].FriendlyName == "OpenAI GPT-4o", "First default model is OpenAI GPT-4o");
        Assert(cf.AvailableModels[1].FriendlyName == "Claude 3.5 Sonnet", "Second default model is Claude 3.5 Sonnet");
        Assert(cf.AvailableModels[2].FriendlyName == "Google Gemini 1.5 Pro", "Third default model is Google Gemini 1.5 Pro");

        // Calling AddDefaultModels again should NOT duplicate
        cf.AddDefaultModels();
        Assert(cf.AvailableModels.Count == 3, "AddDefaultModels is idempotent (no duplicates)");

        // Pre-populated models should not be overwritten
        var cf2 = new CaseFile();
        cf2.AvailableModels.Add(new AIModelConfiguration { FriendlyName = "DeepSeek V3", Provider = "DeepSeek", ModelId = "deepseek-v3" });
        cf2.AddDefaultModels();
        Assert(cf2.AvailableModels.Count == 1, "AddDefaultModels does not add defaults when models already exist");
        Assert(cf2.AvailableModels[0].FriendlyName == "DeepSeek V3", "Existing model preserved after AddDefaultModels");
    }

    private static void TestEvidenceDocumentModel()
    {
        Console.WriteLine("\n─── EvidenceDocument Model ───");

        var doc = new EvidenceDocument();
        Assert(string.IsNullOrEmpty(doc.FileName), "Default FileName is empty");
        Assert(doc.EvidenceStrength == 0.5, "Default evidence strength is 0.5");
        Assert(doc.EstimatedDamages == 0, "Default estimated damages is 0");
        Assert(!doc.IsDiscoveryComplete, "Default IsDiscoveryComplete is false");
        Assert(doc.ExhibitNumber == 0, "Default exhibit number is 0");

        doc.FileName = "ExhibitA.pdf";
        doc.Summary = "Medical report showing injury";
        doc.EvidenceStrength = 0.85;
        doc.EstimatedDamages = 150000;
        doc.ExhibitNumber = 1;
        doc.IsDiscoveryComplete = true;
        Assert(doc.FileName == "ExhibitA.pdf", "EvidenceDocument FileName set");
        Assert(doc.Summary == "Medical report showing injury", "EvidenceDocument Summary set");
        Assert(doc.EvidenceStrength == 0.85, "EvidenceDocument EvidenceStrength set");
        Assert(doc.EstimatedDamages == 150000, "EvidenceDocument EstimatedDamages set");
        Assert(doc.ExhibitNumber == 1, "EvidenceDocument ExhibitNumber set");
        Assert(doc.IsDiscoveryComplete, "EvidenceDocument IsDiscoveryComplete set");
    }

    private static void TestMemoryEntryModel()
    {
        Console.WriteLine("\n─── MemoryEntry Model ───");

        var mem = new MemoryEntry();
        Assert(string.IsNullOrEmpty(mem.Content), "Default MemoryEntry content is empty");
        Assert(mem.Strength == 1.0, "Default MemoryEntry strength is 1.0");
        Assert(!mem.IsFromDocument, "Default MemoryEntry IsFromDocument is false");

        mem.Content = "Test memory";
        mem.Strength = 0.75;
        mem.IsFromDocument = true;
        Assert(mem.Content == "Test memory", "MemoryEntry Content set");
        Assert(mem.Strength == 0.75, "MemoryEntry Strength set");
        Assert(mem.IsFromDocument, "MemoryEntry IsFromDocument set");
    }

    private static void TestCourtroomEventModel()
    {
        Console.WriteLine("\n─── CourtroomEvent Model ───");

        var evt = new CourtroomEvent();
        Assert(string.IsNullOrEmpty(evt.Description), "Default CourtroomEvent description is empty");
        Assert(!evt.IsSidebar, "Default CourtroomEvent IsSidebar is false");
        Assert(evt.VisibleTo.Count == 0, "Default CourtroomEvent VisibleTo is empty");

        evt.Description = "Witness testified";
        evt.IsSidebar = true;
        evt.VisibleTo.Add(AgentRole.Judge);
        Assert(evt.Description == "Witness testified", "CourtroomEvent Description set");
        Assert(evt.IsSidebar, "CourtroomEvent IsSidebar set");
        Assert(evt.VisibleTo.Contains(AgentRole.Judge), "CourtroomEvent VisibleTo contains Judge");
    }

    private static void TestAIModelConfiguration()
    {
        Console.WriteLine("\n─── AIModelConfiguration Model ───");

        var config = new AIModelConfiguration();
        Assert(config.FriendlyName == "New Model", "Default friendly name");
        Assert(config.Provider == "OpenAI", "Default provider is OpenAI");
        Assert(string.IsNullOrEmpty(config.ModelId), "Default model ID is empty");
        Assert(string.IsNullOrEmpty(config.Endpoint), "Default endpoint is empty");
        Assert(string.IsNullOrEmpty(config.ApiKey), "Default API key is empty");
        Assert(config.Status == "Not Tested", "Default status is 'Not Tested'");
        Assert(!config.IsTesting, "Default IsTesting is false");
        Assert(config.CustomSettings.Count == 0, "Default custom settings is empty");

        config.FriendlyName = "GPT-4o";
        config.Provider = "OpenAI";
        config.ModelId = "gpt-4o";
        config.Endpoint = "https://api.openai.com/v1";
        config.ApiKey = "sk-test";
        config.Status = "Connected";
        config.IsTesting = true;
        config.CustomSettings["temperature"] = "0.7";
        Assert(config.FriendlyName == "GPT-4o", "AIModelConfiguration FriendlyName set");
        Assert(config.Provider == "OpenAI", "AIModelConfiguration Provider set");
        Assert(config.ModelId == "gpt-4o", "AIModelConfiguration ModelId set");
        Assert(config.ApiKey == "sk-test", "AIModelConfiguration ApiKey set");
        Assert(config.Status == "Connected", "AIModelConfiguration Status set");
        Assert(config.IsTesting, "AIModelConfiguration IsTesting set");
        Assert(config.CustomSettings["temperature"] == "0.7", "AIModelConfiguration CustomSettings set");
    }

    private static void TestJuryInstructions()
    {
        Console.WriteLine("\n─── JuryInstructions Model ───");

        var instructions = new JuryInstructions();
        Assert(string.IsNullOrEmpty(instructions.Text), "Default instructions text is empty");
        Assert(instructions.Standard == LegalStandard.BeyondReasonableDoubt, "Default standard (first enum)");

        instructions.Text = "You must find the defendant guilty beyond a reasonable doubt.";
        instructions.Standard = LegalStandard.PreponderanceOfEvidence;
        Assert(instructions.Text.Contains("reasonable doubt"), "JuryInstructions Text set");
        Assert(instructions.Standard == LegalStandard.PreponderanceOfEvidence, "JuryInstructions Standard set");
    }

    private static void TestVerdictForm()
    {
        Console.WriteLine("\n─── VerdictForm Model ───");

        var form = new VerdictForm();
        Assert(form.Charges.Count == 0, "Default charges list is empty");
        Assert(form.CausesOfAction.Count == 0, "Default causes of action list is empty");
        Assert(form.DamagesAwarded == 0, "Default damages awarded is 0");

        form.Charges.Add(new Charge { Name = "Theft", IsGuilty = true });
        Assert(form.Charges.Count == 1, "VerdictForm can add charges");
        Assert(form.Charges[0].Name == "Theft", "Charge name preserved");
        Assert(form.Charges[0].IsGuilty, "Charge guilty flag preserved");

        form.CausesOfAction.Add(new CauseOfAction { Name = "Negligence", IsLiable = true });
        Assert(form.CausesOfAction.Count == 1, "VerdictForm can add causes of action");
        Assert(form.CausesOfAction[0].Name == "Negligence", "CauseOfAction name preserved");

        form.DamagesAwarded = 500000m;
        Assert(form.DamagesAwarded == 500000m, "VerdictForm damages awarded set");
    }

    private static void TestExtractedEntities()
    {
        Console.WriteLine("\n─── ExtractedEntities Model ───");

        var entities = new ExtractedEntities();
        Assert(entities.Charges.Count == 0, "Default charges empty");
        Assert(entities.Evidence.Count == 0, "Default evidence empty");
        Assert(entities.Witnesses.Count == 0, "Default witnesses empty");
        Assert(entities.LegalIssues.Count == 0, "Default legal issues empty");

        entities.Charges.Add(new ExtractedCharge { Name = "Burglary", Severity = "Felony" });
        Assert(entities.Charges[0].Name == "Burglary", "ExtractedCharge name set");
        Assert(entities.Charges[0].Severity == "Felony", "ExtractedCharge severity set");

        entities.Evidence.Add(new ExtractedEvidence { Type = "Document", Description = "Contract", Strength = 0.8 });
        Assert(entities.Evidence[0].Type == "Document", "ExtractedEvidence type set");
        Assert(entities.Evidence[0].Strength == 0.8, "ExtractedEvidence strength set");

        entities.Witnesses.Add(new ExtractedWitness { Name = "John Doe", Role = "Expert", Testified = true });
        Assert(entities.Witnesses[0].Name == "John Doe", "ExtractedWitness name set");
        Assert(entities.Witnesses[0].Testified, "ExtractedWitness testified flag");

        entities.LegalIssues.Add("Self-defense");
        Assert(entities.LegalIssues[0] == "Self-defense", "Legal issue added");

        entities.CauseOfAction = "Negligence";
        Assert(entities.CauseOfAction == "Negligence", "Cause of action set");

        entities.CaseSummary = "A case about a car accident";
        Assert(entities.CaseSummary == "A case about a car accident", "Case summary set");
    }

    private static void TestAgentInteractionModels()
    {
        Console.WriteLine("\n─── AgentInteraction Models ───");

        // AgentMessage
        var msg = new AgentMessage();
        Assert(!string.IsNullOrEmpty(msg.Id), "AgentMessage has auto-generated ID");
        Assert(msg.Type == MessageType.Statement, "Default message type is Statement");
        Assert(msg.EmotionalTone == 0.5, "Default emotional tone is 0.5");
        Assert(msg.IsFromAI, "Default IsFromAI is true");
        Assert(msg.LegalCitations.Count == 0, "Default legal citations empty");

        msg.Speaker = "Prosecutor";
        msg.Content = "The defendant is guilty.";
        msg.Type = MessageType.Statement;
        Assert(msg.Speaker == "Prosecutor", "AgentMessage Speaker set");
        Assert(msg.Content == "The defendant is guilty.", "AgentMessage Content set");

        // LegalCitation
        var citation = new LegalCitation();
        Assert(string.IsNullOrEmpty(citation.Code), "Default citation code empty");
        citation.Code = "IPC 302";
        citation.Section = "302";
        citation.Description = "Murder";
        citation.Jurisdiction = "India";
        citation.FavorsPlaintiff = true;
        Assert(citation.Code == "IPC 302", "LegalCitation Code set");
        Assert(citation.FavorsPlaintiff, "LegalCitation FavorsPlaintiff set");

        // InteractionResult
        var result = new InteractionResult();
        Assert(!result.Success, "Default InteractionResult Success is false");
        result.Success = true;
        result.Message = msg;
        result.Error = null;
        Assert(result.Success, "InteractionResult Success set");
        Assert(result.Message?.Content == "The defendant is guilty.", "InteractionResult Message set");

        // StatementRequest
        var request = new StatementRequest();
        Assert(request.Type == MessageType.Statement, "Default StatementRequest type");
        request.SpeakerName = "Defense Attorney";
        request.Context = "Closing argument";
        Assert(request.SpeakerName == "Defense Attorney", "StatementRequest SpeakerName set");

        // ExaminationRequest
        var exam = new ExaminationRequest();
        Assert(!exam.IsCross, "Default ExaminationRequest IsCross is false");
        exam.IsCross = true;
        exam.ExaminerName = "Prosecutor";
        exam.WitnessName = "Dr. Smith";
        Assert(exam.IsCross, "ExaminationRequest IsCross set");
        Assert(exam.ExaminerName == "Prosecutor", "ExaminationRequest ExaminerName set");

        // DeliberationRequest
        var delib = new DeliberationRequest();
        Assert(delib.Jurors.Count == 0, "Default DeliberationRequest jurors empty");
        delib.Jurors.Add(new Agent { Name = "Juror 1" });
        Assert(delib.Jurors.Count == 1, "DeliberationRequest jurors added");

        // DeliberationResult
        var delibResult = new DeliberationResult();
        Assert(string.IsNullOrEmpty(delibResult.Verdict), "Default DeliberationResult verdict empty");
        Assert(delibResult.Confidence == 0, "Default DeliberationResult confidence is 0");
        delibResult.Verdict = "Plaintiff";
        delibResult.Confidence = 0.75;
        Assert(delibResult.Verdict == "Plaintiff", "DeliberationResult Verdict set");
        Assert(delibResult.Confidence == 0.75, "DeliberationResult Confidence set");

        // RoleConfiguration
        var roleConfig = new RoleConfiguration();
        Assert(roleConfig.MaxResponseWords == 40, "Default MaxResponseWords is 40");
        roleConfig.Role = AgentRole.Judge;
        roleConfig.CanRule = true;
        Assert(roleConfig.CanRule, "RoleConfiguration CanRule set");

        // PhaseConfiguration
        var phaseConfig = new PhaseConfiguration();
        Assert(phaseConfig.MinParticipants == 2, "Default MinParticipants is 2");
        phaseConfig.Phase = CourtPhase.ClosingArguments;
        Assert(phaseConfig.Phase == CourtPhase.ClosingArguments, "PhaseConfiguration Phase set");

        // AgentCaseData
        var caseData = new AgentCaseData();
        Assert(string.IsNullOrEmpty(caseData.CaseNumber), "Default AgentCaseData case number empty");
        caseData.CaseNumber = "23-CV-1234";
        caseData.CaseType = "Civil";
        Assert(caseData.CaseNumber == "23-CV-1234", "AgentCaseData CaseNumber set");
    }

    private static void TestChargeAndCauseOfAction()
    {
        Console.WriteLine("\n─── Charge & CauseOfAction Models ───");

        // Charge
        var charge = new Charge { Name = "Theft" };
        Assert(charge.Name == "Theft", "Charge name set");
        Assert(!charge.IsGuilty, "Default Charge IsGuilty is false");
        Assert(charge.Elements.Count == 0, "Default Charge elements empty");

        charge.Elements.Add(new Element { Name = "Taking", Description = "Took property", IsProven = true });
        Assert(charge.Elements.Count == 1, "Charge element added");
        Assert(charge.Elements[0].IsProven, "Element IsProven set");

        charge.IsGuilty = true;
        Assert(charge.IsGuilty, "Charge IsGuilty set");

        // CauseOfAction
        var coa = new CauseOfAction { Name = "Negligence" };
        Assert(coa.Name == "Negligence", "CauseOfAction name set");
        Assert(!coa.IsLiable, "Default CauseOfAction IsLiable is false");

        coa.Elements.Add(new Element { Name = "Duty", Description = "Owed duty of care" });
        Assert(coa.Elements.Count == 1, "CauseOfAction element added");

        coa.IsLiable = true;
        Assert(coa.IsLiable, "CauseOfAction IsLiable set");

        // Element
        var element = new Element { Name = "Causation", Description = "Proximate cause" };
        Assert(element.Name == "Causation", "Element name set");
        Assert(!element.IsProven, "Default Element IsProven is false");
        element.IsProven = true;
        Assert(element.IsProven, "Element IsProven set");
    }

    private static void TestObservableObject()
    {
        Console.WriteLine("\n─── ObservableObject Model ───");

        // ObservableObject is abstract, so we test via a concrete subclass
        var config = new AIModelConfiguration();
        Assert(config != null, "Concrete ObservableObject subclass can be instantiated");

        // Test SetProperty via property changes
        config.FriendlyName = "Test Model";
        Assert(config.FriendlyName == "Test Model", "SetProperty works via FriendlyName setter");

        config.Provider = "TestProvider";
        Assert(config.Provider == "TestProvider", "SetProperty works via Provider setter");
    }
}