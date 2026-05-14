using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Verdict.Models;
using Verdict.Services;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class AgentChatWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly Agent _agent;
    private string _conversationLog = string.Empty;

    public AgentChatWindow(MainViewModel mainVm, Agent agent)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _agent = agent;
        Title = $"Chat with {_agent.Name}";
        HeaderText.Text = $"CHAT WITH {_agent.Name} ({_agent.Role})";
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        string message = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string result = $"--- Chat with {_agent.Name} ---\n";
        result += $"You: {message}\n";

        // Try to get a real LLM response first
        string response = await GetLLMResponse(message);

        // Fall back to role-based canned response if LLM fails or returns empty
        if (string.IsNullOrEmpty(response) || response.StartsWith("(Error") || response.StartsWith("(No LLM"))
        {
            response = GenerateRoleBasedResponse();
        }

        result += $"{_agent.Name}: {response}\n";

        _conversationLog += result + "\n";
        ConversationLog.Text = _conversationLog;
        MessageInput.Clear();

        // Save the exchange as MemoryEntries tagged with source "Chat"
        var userMemory = new MemoryEntry
        {
            Content = $"You said: {message}",
            Timestamp = DateTime.Now,
            Source = "Chat",
            Strength = 1.0
        };
        var agentMemory = new MemoryEntry
        {
            Content = $"{_agent.Name} responded: {response}",
            Timestamp = DateTime.Now,
            Source = "Chat",
            Strength = 1.0
        };
        _agent.Memories.Add(userMemory);
        _agent.Memories.Add(agentMemory);
    }

    private async Task<string> GetLLMResponse(string userMessage)
    {
        try
        {
            // Find the agent's model configuration
            var modelConfig = _mainVm.CurrentCase?.AvailableModels?
                .FirstOrDefault(m => m.FriendlyName == _agent.SelectedModel);

            if (modelConfig == null)
            {
                // Fall back to first available model
                modelConfig = _mainVm.CurrentCase?.AvailableModels?.FirstOrDefault();
            }

            if (modelConfig == null)
                return "(No LLM model configured for this agent)";

            // Get the provider
            var provider = ProviderDiscoveryService.GetProviderByName(modelConfig.Provider);
            if (provider == null)
                return $"(Provider '{modelConfig.Provider}' not found)";

            // Build the system prompt with agent context
            string systemPrompt = $"You are {_agent.Name}, a {_agent.Role} in a courtroom simulation. {_agent.SystemPrompt}\n\n" +
                $"Your background: {_agent.Profile}\n" +
                $"Your current sentiment: {_agent.Sentiment:P0}\n" +
                $"Your verdict lean: {_agent.VerdictLean:P0} towards plaintiff\n\n" +
                $"Respond in character as this {_agent.Role}. Keep responses concise (2-4 sentences). " +
                $"This is a private conversation, not a courtroom proceeding.";

            // Call the LLM with agent-level overrides
            if (_agent.ModelTemperatureOverride.HasValue)
                modelConfig.CustomSettings["Temperature"] = _agent.ModelTemperatureOverride.Value.ToString();
            if (_agent.ModelMaxTokensOverride.HasValue)
                modelConfig.CustomSettings["MaxTokens"] = _agent.ModelMaxTokensOverride.Value.ToString();

            string response = await provider.GenerateResponseAsync(
                modelConfig,
                systemPrompt,
                userMessage);

            return response;
        }
        catch (Exception ex)
        {
            return $"(Error: {ex.Message})";
        }
    }

    private string GenerateRoleBasedResponse()
    {
        var random = new Random();

        switch (_agent.Role)
        {
            case AgentRole.Juror:
            case AgentRole.AlternateJuror:
            {
                string[] jurorResponses =
                [
                    "Well, I'm still weighing the evidence. Some of it seems credible, but I'd like to hear more before making up my mind.",
                    "I think the prosecution has presented some compelling points, but I'm not fully convinced yet.",
                    "The defense's argument makes a lot of sense to me. I'm leaning in their direction.",
                    "Honestly, I'm having trouble following all the legal jargon. Can someone explain it more simply?",
                    "I believe the evidence speaks for itself. I'm pretty firm on my position.",
                    "I'm keeping an open mind. Both sides have valid points, and I want to hear everything before deciding.",
                    "That witness seemed unreliable to me. I'm not sure I can trust their testimony.",
                    "The documents submitted as evidence are quite clear. That's influencing my opinion.",
                    "I'm discussing this with my fellow jurors during breaks. We have differing views.",
                    "I need more time to think about this. It's a serious decision and I don't take it lightly.",
                    "Based on what I've heard so far, I'm leaning toward the plaintiff's side.",
                    "The judge's instructions were helpful. I'm trying to apply the law as instructed.",
                    "I have some doubts about the chain of custody for that evidence.",
                    "My gut feeling tells me something doesn't add up with the defense's story.",
                    "I appreciate the court's patience. I want to make sure we get this right."
                ];
                return jurorResponses[random.Next(jurorResponses.Length)];
            }

            case AgentRole.Lawyer:
            {
                string[] lawyerResponses =
                [
                    "Based on my legal analysis, the precedents in this jurisdiction strongly support our position.",
                    "I would advise caution here. The opposing counsel is skilled and we need to be strategic.",
                    "Let me review the relevant statutes. I believe there's a strong argument we can make.",
                    "My strategy would be to focus on the key evidence that supports our narrative.",
                    "I've prepared a motion on this matter. The legal framework is on our side.",
                    "Objection, Your Honor! The question calls for speculation.",
                    "The burden of proof rests with the opposing party, and I don't believe they can meet it.",
                    "I recommend we approach the bench to discuss this matter.",
                    "Let me confer with my client before responding to that.",
                    "The discovery materials clearly show a pattern that benefits our case."
                ];
                return lawyerResponses[random.Next(lawyerResponses.Length)];
            }

            case AgentRole.Judge:
            {
                string[] judgeResponses =
                [
                    "The court has reviewed the submissions and will rule accordingly.",
                    "I'll allow that line of questioning, but please keep it relevant.",
                    "Counselor, please approach the bench.",
                    "The jury will disregard that last statement.",
                    "I'm prepared to issue my ruling on this matter.",
                    "Let's maintain decorum in the courtroom, please.",
                    "I'll hear arguments from both sides before making a decision.",
                    "The objection is sustained. Please rephrase the question.",
                    "I'm instructing the jury on the applicable law at this time.",
                    "We'll take a brief recess while the court considers this matter."
                ];
                return judgeResponses[random.Next(judgeResponses.Length)];
            }

            case AgentRole.Witness:
            {
                string[] witnessResponses =
                [
                    "I recall the events of that day very clearly. Let me describe what I saw.",
                    "I'm not entirely sure about the exact date, but I remember the incident.",
                    "Under oath, I can confirm that I was present at the location in question.",
                    "I've given my statement to the investigators already. It's all in there.",
                    "To the best of my knowledge, that's what happened.",
                    "I may have been mistaken about some details. It was a stressful situation.",
                    "Yes, I can identify the defendant. I got a good look at them.",
                    "I don't remember seeing that particular document before.",
                    "I'm testifying truthfully to the best of my recollection.",
                    "The other party's version of events doesn't match what I witnessed."
                ];
                return witnessResponses[random.Next(witnessResponses.Length)];
            }

            case AgentRole.Client:
            {
                string[] clientResponses =
                [
                    "I'm really concerned about how this is going. What are my options?",
                    "I trust your judgment on this. Just keep me informed of any developments.",
                    "I don't think the other side is being reasonable at all.",
                    "Can you explain what's happening in court today? I'm a bit lost.",
                    "I want to settle this if possible. The stress is getting to me.",
                    "I'm prepared to see this through to trial if necessary.",
                    "What are the chances we'll win if this goes to court?",
                    "I've been thinking about this a lot. I have some questions about the strategy.",
                    "The other party's offer is unacceptable. We can do better.",
                    "Thank you for explaining everything so clearly. I appreciate your help."
                ];
                return clientResponses[random.Next(clientResponses.Length)];
            }

            case AgentRole.Reporter:
            {
                string[] reporterResponses =
                [
                    "I have the official record of those proceedings ready for review.",
                    "Let me check my notes. Yes, that testimony was recorded.",
                    "The transcript of today's session will be available by tomorrow.",
                    "I've logged all exhibits into the official record.",
                    "I can provide a certified copy of any document in the record.",
                    "The court reporter's notes are complete and accurate.",
                    "I've transcribed the proceedings verbatim as required.",
                    "All objections and rulings have been noted in the record.",
                    "The record reflects the proceedings accurately.",
                    "I'm available to read back any portion of the testimony."
                ];
                return reporterResponses[random.Next(reporterResponses.Length)];
            }

            default:
            {
                string[] defaultResponses =
                [
                    "I understand your question. Let me think about that carefully.",
                    "That's an interesting point. Here's my perspective on it.",
                    "I appreciate you asking. I've been considering this matter.",
                    "Based on my understanding of the situation, I'd say...",
                    "Let me consider all the information before responding.",
                    "I see where you're coming from. Here's what I think.",
                    "That's a fair question. Let me give you my honest opinion.",
                    "I've been following the proceedings closely. Here's my take.",
                    "I'm glad you asked about that. It's an important consideration.",
                    "From my perspective, the situation is quite nuanced."
                ];
                return defaultResponses[random.Next(defaultResponses.Length)];
            }
        }
    }

    private void WipeMemories_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"This will permanently delete all chat memories for {_agent.Name}. Are you sure?",
            "Wipe Memories",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Remove all MemoryEntries with Source == "Chat"
            var chatMemories = _agent.Memories.Where(m => m.Source == "Chat").ToList();
            foreach (var memory in chatMemories)
            {
                _agent.Memories.Remove(memory);
            }

            // Also clear the conversation log display
            _conversationLog = string.Empty;
            ConversationLog.Text = _conversationLog;

            MessageBox.Show($"Chat memories wiped for {_agent.Name}.", "Memories Cleared",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
