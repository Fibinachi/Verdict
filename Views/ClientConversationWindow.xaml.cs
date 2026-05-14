using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Verdict.Models;
using Verdict.Services;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class ClientConversationWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly Agent _client;
    private readonly Agent _lawyer;
    private string _transcript = string.Empty;

    public string ConversationTranscript 
    { 
        get => _transcript; 
        set { _transcript = value; ConversationLog.Text = value; } 
    }

    public ClientConversationWindow(MainViewModel mainVm, Agent client, Agent lawyer)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _client = client;
        _lawyer = lawyer;
        Title = $"Privileged: {_lawyer.Name} & {_client.Name}";
    }

    private async void Consult_Click(object sender, RoutedEventArgs e)
    {
        string message = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string result = $"--- Private Consultation ---\n";
        result += $"{_lawyer.Name}: {message}\n";

        string response = await GetClientResponse(message);

        result += $"{_client.Name}: {response}\n";

        if (_client.SettlementAuthority > 0)
        {
            result += $"[SYSTEM: Current Authority remains at ${_client.SettlementAuthority:N0}]\n";
        }

        // Store memories
        _lawyer.Memories.Add(new MemoryEntry
        {
            Content = $"Asked client: {message}",
            Source = "ClientConsultation",
            Timestamp = DateTime.Now,
            Strength = 1.0,
            IsFromDocument = false
        });

        _client.Memories.Add(new MemoryEntry
        {
            Content = $"Responded to lawyer: {response}",
            Source = "ClientConsultation",
            Timestamp = DateTime.Now,
            Strength = 1.0,
            IsFromDocument = false
        });

        ConversationTranscript += result + "\n";
        MessageInput.Clear();
    }

    private async Task<string> GetClientResponse(string message)
    {
        try
        {
            var modelConfig = _mainVm.CurrentCase?.AvailableModels?
                .FirstOrDefault(m => m.FriendlyName == _client.SelectedModel)
                ?? _mainVm.CurrentCase?.AvailableModels?.FirstOrDefault();

            if (modelConfig == null)
                return GenerateFallbackResponse();

            var provider = ProviderDiscoveryService.GetProviderByName(modelConfig.Provider);
            if (provider == null)
                return GenerateFallbackResponse();

            string memoriesContext = _client.Memories.Any()
                ? $"Your past memories:\n{string.Join("\n", _client.Memories.Select(m => $"- {m.Content}"))}\n\n"
                : string.Empty;

            string systemPrompt = $"You are {_client.Name}, a client in a legal case. {_client.SystemPrompt}\n\n" +
                $"Client profile:\n" +
                $"Age: {_client.Age}, Gender: {_client.Gender}, Race: {_client.Race}\n" +
                $"Occupation: {_client.Occupation}, Education: {_client.EducationLevel}\n" +
                $"Income: {_client.IncomeLevel}, Marital Status: {_client.MaritalStatus}\n" +
                $"Risk Perception: {_client.RiskPerception:P0}\n" +
                $"Settlement Authority: ${_client.SettlementAuthority:N0}\n\n" +
                memoriesContext +
                $"You are consulting with your lawyer, {_lawyer.Name}. Respond in character as a client discussing your case. " +
                $"Keep responses concise (2-3 sentences). Be realistic about your concerns and expectations.";

            string response = await provider.GenerateResponseAsync(
                modelConfig,
                systemPrompt,
                $"Your lawyer asks: {message}\n\nRespond as the client.");

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Error"))
                return GenerateFallbackResponse();

            return response.Trim();
        }
        catch
        {
            return GenerateFallbackResponse();
        }
    }

    private string GenerateFallbackResponse()
    {
        if (_client.RiskPerception > 0.7)
            return "I'm very concerned about how the jury is leaning. Should we increase our settlement offer?";
        else if (_client.RiskPerception < 0.3)
            return "I don't see any liability here. Let's keep pushing the defense strategy.";
        else
            return "I understand the risks. Let's proceed as discussed.";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ConversationTranscript))
        {
            _mainVm.BroadcastEvent(
                $"Privileged Attorney-Client consultation concluded for {_lawyer.Name}'s team.", 
                new List<AgentRole> { _lawyer.Role }, // Only the team can "know" it happened
                isSidebar: true
            );
        }
        Close();
    }
}
