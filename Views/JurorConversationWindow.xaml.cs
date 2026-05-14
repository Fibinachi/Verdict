using System;
using System.Windows;
using Verdict.Models;
using Verdict.ViewModels;

namespace Verdict.Views;

public partial class JurorConversationWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly Agent _juror;
    private string _conversationLog = string.Empty;

    public string ConversationTranscript
    {
        get => _conversationLog;
        set { _conversationLog = value; ConversationLog.Text = value; }
    }

    private static readonly string[] JurorResponses =
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

    public JurorConversationWindow(MainViewModel mainVm, Agent juror)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _juror = juror;
        Title = $"Chat with Juror - {_juror.Name}";
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        string message = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string result = $"--- Conversation with {_juror.Name} ---\n";
        result += $"You: {message}\n";

        // Pick a simulated juror response
        var random = new Random();
        string response = JurorResponses[random.Next(JurorResponses.Length)];
        result += $"{_juror.Name}: {response}\n";

        ConversationTranscript += result + "\n";
        MessageInput.Clear();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // No memory saved, just close
        Close();
    }
}
