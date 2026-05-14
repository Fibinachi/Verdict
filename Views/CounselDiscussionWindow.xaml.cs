using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Verdict.Models;
using Verdict.Services;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class CounselDiscussionWindow : Window
{
    private readonly MainViewModel _mainVm;
    private string _transcript = string.Empty;

    public string DiscussionTranscript 
    { 
        get => _transcript; 
        set { _transcript = value; DiscussionLog.Text = value; } 
    }

    public CounselDiscussionWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
    }

    private async void Simulate_Click(object sender, RoutedEventArgs e)
    {
        string topic = TopicInput.Text.Trim();
        if (string.IsNullOrEmpty(topic)) return;

        SimulateButton.IsEnabled = false;

        var lawyers = _mainVm.AllAgents.Where(a => a.Role == AgentRole.Lawyer && a.IsOccupied).ToList();
        if (lawyers.Count < 2)
        {
            MessageBox.Show("At least two lawyers must be seated to have a discussion.");
            SimulateButton.IsEnabled = true;
            return;
        }

        var defense = lawyers.FirstOrDefault(l => _mainVm.DefenseTeam.Contains(l));
        var prosecution = lawyers.FirstOrDefault(l => _mainVm.ProsecutionTeam.Contains(l));

        if (defense == null || prosecution == null)
        {
            MessageBox.Show("Need at least one lawyer from each team for a meaningful discussion.");
            SimulateButton.IsEnabled = true;
            return;
        }

        string result = $"--- Discussion on: {topic} ---\n";

        string prosecutionResponse = await GetLawyerResponse(prosecution, topic, "prosecution/plaintiff", defense.Name);
        result += $"{prosecution.Name}: {prosecutionResponse}\n";
        prosecution.Memories.Add(new MemoryEntry
        {
            Content = $"Counsel Discussion - {topic}: {prosecutionResponse}",
            Timestamp = DateTime.Now,
            Source = "CounselDiscussion",
            Strength = 1.0,
            IsFromDocument = false
        });

        string defenseResponse = await GetLawyerResponse(defense, topic, "defense", prosecution.Name);
        result += $"{defense.Name}: {defenseResponse}\n";
        defense.Memories.Add(new MemoryEntry
        {
            Content = $"Counsel Discussion - {topic}: {defenseResponse}",
            Timestamp = DateTime.Now,
            Source = "CounselDiscussion",
            Strength = 1.0,
            IsFromDocument = false
        });

        string rebuttal = await GetLawyerResponse(prosecution, $"Respond to {defense.Name}'s last point: {defenseResponse}", "prosecution/plaintiff", defense.Name);
        result += $"{prosecution.Name}: {rebuttal}\n";
        prosecution.Memories.Add(new MemoryEntry
        {
            Content = $"Counsel Discussion - {topic}: {rebuttal}",
            Timestamp = DateTime.Now,
            Source = "CounselDiscussion",
            Strength = 1.0,
            IsFromDocument = false
        });

        string counter = await GetLawyerResponse(defense, $"Respond to {prosecution.Name}'s last point: {rebuttal}", "defense", prosecution.Name);
        result += $"{defense.Name}: {counter}\n";
        defense.Memories.Add(new MemoryEntry
        {
            Content = $"Counsel Discussion - {topic}: {counter}",
            Timestamp = DateTime.Now,
            Source = "CounselDiscussion",
            Strength = 1.0,
            IsFromDocument = false
        });

        string judgeRuling = await GetJudgeRuling(topic, prosecution.Name, defense.Name, prosecutionResponse, defenseResponse, rebuttal, counter);
        result += $"\n[JUDGE'S RULING]\n{judgeRuling}\n";

        var judge = _mainVm.JudgeArea.FirstOrDefault(j => j.IsOccupied);
        foreach (var agent in new[] { prosecution, defense, judge })
        {
            if (agent == null) continue;
            agent.Memories.Add(new MemoryEntry
            {
                Content = $"Judge's Ruling on {topic}: {judgeRuling}",
                Timestamp = DateTime.Now,
                Source = "CounselDiscussion",
                Strength = 1.0,
                IsFromDocument = false
            });
        }

        DiscussionTranscript += result + "\n\n";
        TopicInput.Clear();
        SimulateButton.IsEnabled = true;
    }

    private async Task<string> GetLawyerResponse(Agent lawyer, string topic, string side, string opponentName)
    {
        try
        {
            var modelConfig = _mainVm.CurrentCase?.AvailableModels?
                .FirstOrDefault(m => m.FriendlyName == lawyer.SelectedModel)
                ?? _mainVm.CurrentCase?.AvailableModels?.FirstOrDefault();

            if (modelConfig == null)
                return GenerateFallbackResponse(lawyer, side);

            var provider = ProviderDiscoveryService.GetProviderByName(modelConfig.Provider);
            if (provider == null)
                return GenerateFallbackResponse(lawyer, side);

            if (lawyer.ModelTemperatureOverride.HasValue)
                modelConfig.CustomSettings["Temperature"] = lawyer.ModelTemperatureOverride.Value.ToString();
            if (lawyer.ModelMaxTokensOverride.HasValue)
                modelConfig.CustomSettings["MaxTokens"] = lawyer.ModelMaxTokensOverride.Value.ToString();

            string memoriesContext = lawyer.Memories.Any()
                ? $"Your past memories:\n{string.Join("\n", lawyer.Memories.Select(m => $"- {m.Content}"))}\n\n"
                : string.Empty;

            string systemPrompt = $"You are {lawyer.Name}, a {side} lawyer in a courtroom simulation. {lawyer.SystemPrompt}\n\n" +
                $"Your background: {lawyer.Profile}\n" +
                $"Your opponent is {opponentName}.\n\n" +
                memoriesContext +
                $"Respond in character as a sharp, persuasive lawyer. Keep responses concise (2-3 sentences). " +
                $"This is a private sidebar discussion between counsel, not in front of a jury.";

            string response = await provider.GenerateResponseAsync(
                modelConfig,
                systemPrompt,
                $"We are discussing: {topic}. Present your legal position clearly.");

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Error"))
                return GenerateFallbackResponse(lawyer, side);

            return response.Trim();
        }
        catch
        {
            return GenerateFallbackResponse(lawyer, side);
        }
    }

    private static string GenerateFallbackResponse(Agent lawyer, string side)
    {
        return side == "prosecution/plaintiff"
            ? "Our position is firm. The evidence supports our claims, and we intend to pursue this fully."
            : "We strongly disagree. The opposition's theory has significant gaps, and we will challenge every point.";
    }

    private async Task<string> GetJudgeRuling(string topic, string prosName, string defName,
        string prosArg, string defArg, string prosRebuttal, string defCounter)
    {
        var judge = _mainVm.JudgeArea.FirstOrDefault(j => j.IsOccupied);
        if (judge == null)
            return "No judge is seated to issue a ruling.";

        try
        {
            var modelConfig = _mainVm.CurrentCase?.AvailableModels?
                .FirstOrDefault(m => m.FriendlyName == judge.SelectedModel)
                ?? _mainVm.CurrentCase?.AvailableModels?.FirstOrDefault();

            if (modelConfig == null)
                return "The court will take this matter under advisement. Ruling to follow.";

            var provider = ProviderDiscoveryService.GetProviderByName(modelConfig.Provider);
            if (provider == null)
                return "The court will take this matter under advisement. Ruling to follow.";

            if (judge.ModelTemperatureOverride.HasValue)
                modelConfig.CustomSettings["Temperature"] = judge.ModelTemperatureOverride.Value.ToString();
            if (judge.ModelMaxTokensOverride.HasValue)
                modelConfig.CustomSettings["MaxTokens"] = judge.ModelMaxTokensOverride.Value.ToString();

            string discussionSummary =
                $"Topic: {topic}\n" +
                $"{prosName} (Prosecution): {prosArg}\n" +
                $"{defName} (Defense): {defArg}\n" +
                $"{prosName} rebuttal: {prosRebuttal}\n" +
                $"{defName} response: {defCounter}";

            string systemPrompt = $"You are {judge.Name}, the presiding judge in a courtroom simulation. {judge.SystemPrompt}\n\n" +
                $"Your background: {judge.Profile}\n" +
                $"Your judicial philosophy: {judge.JudicialRulings}\n\n" +
                $"Issue a clear, decisive ruling on the matter discussed by counsel. " +
                $"State your ruling, your reasoning (2-3 sentences), and any orders to the parties. " +
                $"Be authoritative and fair.";

            string response = await provider.GenerateResponseAsync(
                modelConfig,
                systemPrompt,
                $"Counsel have presented their positions on the following matter:\n\n{discussionSummary}\n\nIssue your ruling.");

            if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Error"))
                return "The court will take this matter under advisement. Ruling to follow.";

            return response.Trim();
        }
        catch
        {
            return "The court will take this matter under advisement. Ruling to follow.";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(DiscussionTranscript))
        {
            _mainVm.BroadcastEvent(
                "Private Counsel Discussion Recorded in Sidebar.", 
                new List<AgentRole> { AgentRole.Lawyer, AgentRole.Judge }, 
                isSidebar: true
            );
        }
        Close();
    }
}
