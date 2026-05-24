using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using Verdict.Models;
using Verdict.Services;

namespace Verdict.Views
{
    public partial class ModelDetailsDialog : Window
    {
    private readonly ILLMProviderModule _module;
    public AIModelConfiguration Model => (AIModelConfiguration)DataContext;

    private CancellationTokenSource? _downloadCts;
    private Task? _downloadTask;
    private bool _isDownloading;

    public ModelDetailsDialog(AIModelConfiguration model, ILLMProviderModule module)
    {
        InitializeComponent();
        _module = module;
        DataContext = model;
        BuildDynamicFields();
        UpdateApiKeyLink();
        SetupLoadModelsButton();
    }

        private void UpdateApiKeyLink()
        {
            // Set the API key URL based on the provider
            if (_module.ProviderName == "ONNX")
            {
                ApiKeyLink.NavigateUri = null;
                ApiKeyLink.Inlines.Clear();
                ApiKeyLink.Inlines.Add("No API key needed - runs locally");
                return;
            }

            string apiKeyUrl = _module.ProviderName switch
            {
                "OpenAI" => "https://platform.openai.com/api-keys",
                "Anthropic" => "https://console.anthropic.com/api-keys",
                "Google Gemini" => "https://aistudio.google.com/app/apikey",
                "Ollama" => "https://ollama.com/download",
                "Hugging Face" => "https://huggingface.co/settings/tokens",
                "Alibaba Cloud" => "https://dashscope.console.aliyun.com",
                "NVIDIA" => "https://build.nvidia.com",
                "Intel" => "https://vault.habana.ai",
                "DeepSeek" => "https://platform.deepseek.com/api_keys",
                "Grok" => "https://console.x.ai/",
                _ => "https://platform.openai.com/api-keys"
            };
            ApiKeyLink.NavigateUri = new Uri(apiKeyUrl);
        }

    private void SetupLoadModelsButton()
    {
        // Show the Load Models button for providers that support model listing
        // Currently implemented for Google Gemini and Grok (which uses OpenAI-compatible API)
        if (_module.ProviderName == "Google Gemini" || _module.ProviderName == "Grok" || 
            _module.ProviderName == "OpenAI" || _module.ProviderName == "Anthropic" ||
            _module.ProviderName == "DeepSeek" || _module.ProviderName == "Alibaba Cloud" ||
            _module.ProviderName == "NVIDIA" || _module.ProviderName == "Intel" ||
            _module.ProviderName == "Hugging Face" || _module.ProviderName == "ONNX" ||
            _module.ProviderName == "Ollama")
        {
            LoadModelsButton.Visibility = Visibility.Visible;
        }
    }

    private void BuildDynamicFields()
    {
        var panel = new StackPanel();

        // Add a FriendlyName field at the top so users can customize the model name
        var friendlyNameLabel = new TextBlock { Text = "Model Name", Margin = new Thickness(0, 0, 0, 5) };
        var friendlyNameInput = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
        Binding friendlyNameBinding = new Binding("FriendlyName")
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        friendlyNameInput.SetBinding(TextBox.TextProperty, friendlyNameBinding);
        panel.Children.Add(friendlyNameLabel);
        panel.Children.Add(friendlyNameInput);

        foreach (var field in _module.ConfigFields)
        {
            var label = new TextBlock { Text = field.Label, Margin = new Thickness(0, 0, 0, 5) };
            Control input;

            if (field.IsPassword)
            {
                input = new TextBox(); // Using TextBox for simplicity in this demo, usually PasswordBox
            }
            else
            {
                input = new TextBox();
            }

            input.Margin = new Thickness(0, 0, 0, 15);
            
            // Bind to ModelId, Endpoint, ApiKey, or CustomSettings
            Binding binding = new Binding();
            if (field.Key == "ModelId") binding.Path = new PropertyPath("ModelId");
            else if (field.Key == "Endpoint") binding.Path = new PropertyPath("Endpoint");
            else if (field.Key == "ApiKey") binding.Path = new PropertyPath("ApiKey");
            else binding.Path = new PropertyPath($"CustomSettings[{field.Key}]");

            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            input.SetBinding(TextBox.TextProperty, binding);

            // If this is the ModelId field, add a tooltip to indicate it can be loaded via the Load Models button
            if (field.Key == "ModelId")
            {
                label.ToolTip = "Click 'Load Models...' to select from available models";
            }

            panel.Children.Add(label);
            panel.Children.Add(input);
        }
        
        // Replace the ItemsControl content with our generated panel
        DynamicFieldsControl.Content = panel;
    }

    public void ShowError(string message)
    {
        ErrorTextBox.Text = message;
        ErrorTextBox.Visibility = Visibility.Visible;
    }

    public void ShowDownloadProgress(string status, double percent, string currentFile)
    {
        DownloadProgressSection.Visibility = Visibility.Visible;
        DownloadStatusText.Text = status;
        DownloadProgressBar.Value = percent;
        DownloadFileText.Text = currentFile;
    }

    public void HideDownloadProgress()
    {
        DownloadProgressSection.Visibility = Visibility.Collapsed;
        DownloadProgressBar.Value = 0;
        DownloadFileText.Text = "";
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        Model.IsTesting = true;
        Model.Status = "Testing...";
        TestButton.IsEnabled = false;
        ErrorTextBox.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _module.TestConnectionAsync(Model);
            Model.Status = result;
        }
        catch (Exception ex)
        {
            ShowError($"Test connection failed: {ex.Message}");
            Model.Status = "Connection failed";
        }
        finally
        {
            Model.IsTesting = false;
            TestButton.IsEnabled = true;
        }
    }

    private async void LoadModels_Click(object sender, RoutedEventArgs e)
    {
        // Only validate API key for providers that need it (ONNX and HuggingFace download from HF with no key)
        if (_module.ProviderName != "ONNX" && _module.ProviderName != "Hugging Face" && string.IsNullOrWhiteSpace(Model.ApiKey))
        {
            MessageBox.Show("Please enter your API key first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            ErrorMessage.Visibility = Visibility.Collapsed;
            ErrorTextBox.Visibility = Visibility.Collapsed;

            // Get available models from the provider
            var availableModels = await _module.GetAvailableModelsAsync(Model);

            // Show model selection dialog
            var modelSelectionDialog = new ModelSelectionDialog(availableModels) { Owner = this };
            if (modelSelectionDialog.ShowDialog() == true)
            {
                var selected = modelSelectionDialog.SelectedModelId;

                // The list entries are formatted as "Name (info) - repo-id".
                // Extract just the repo ID (everything after the last " - " separator).
                var separator = " - ";
                var lastSepIndex = selected.LastIndexOf(separator, StringComparison.Ordinal);
                if (lastSepIndex >= 0)
                {
                    selected = selected[(lastSepIndex + separator.Length)..];
                }

                Model.ModelId = selected;

                // Update the friendly name to reflect the actual model name
                var modelName = selected.Split('/').LastOrDefault() ?? selected;
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    Model.FriendlyName = modelName;
                }
            }

            // After selecting a model:
            // - For most remote providers, a download may be appropriate.
            // - For ONNX specifically, we do NOT auto-load/test or auto-init runtime from the selection flow.
            //   Downloading ONNX files (when applicable) is deferred to the explicit download/test actions.
            if (!string.IsNullOrWhiteSpace(Model.ModelId) && Model.ModelId.Contains("/"))
            {
                // Start downloading in the background for providers that fetch ONNX/HF model files.
                // This avoids later runtime init crashes caused by incomplete/missing model artifacts.
                if (_module.ProviderName == "Hugging Face" || _module.ProviderName == "ONNX")
                {
                    _downloadCts?.Cancel();
                    _downloadCts = new CancellationTokenSource();
                    _isDownloading = true;
                    _downloadTask = DownloadSelectedModelAsync(_downloadCts.Token);
                }
            }
        }
        catch (Exception ex)
        {
            HideDownloadProgress();
            ShowError($"Error loading models: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async Task DownloadSelectedModelAsync(CancellationToken ct)
    {
        _isDownloading = true;

        try
        {
            ShowDownloadProgressSafe("Downloading model...", 0, "Starting...");

            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                var percent = info.TotalBytes > 0
                    ? Math.Min(100.0, (double)info.BytesDownloaded / info.TotalBytes * 100)
                    : 0.0;
                var status = info.FilesCompleted > 0
                    ? $"Downloading {info.CurrentFile} ({info.FilesCompleted}/{info.TotalFiles})"
                    : "Downloading model...";

                DispatcherSafe(() => ShowDownloadProgress(status, percent, info.CurrentFile));
            });

            if (_module is Providers.HuggingFaceModelBase hfBase)
            {
                await hfBase.DownloadModelAsync(Model, progress, ct);
            }

            DispatcherSafe(() =>
            {
                HideDownloadProgress();
                Model.Status = ModelDownloadStatus.ReadyDownloadComplete;
            });
        }
        catch (OperationCanceledException)
        {
            // Ignore cancels (eg. dialog closed/replaced)
        }
        catch (Exception ex)
        {
            DispatcherSafe(() =>
            {
                HideDownloadProgress();
                ShowError($"Download failed: {ex.Message}");
            });
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private void DispatcherSafe(Action action)
    {
        try
        {
            if (!IsVisible) return;
            Dispatcher.Invoke(action);
        }
        catch
        {
            // Window/UI may be closing; ignore to avoid crashes.
        }
    }

    private void ShowDownloadProgressSafe(string status, double percent, string currentFile)
    {
        try
        {
            ShowDownloadProgress(status, percent, currentFile);
        }
        catch
        {
            // Ignore UI exceptions if window is closing.
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Allow closing while the background download continues,
        // but prevent crashes by ensuring background task doesn't touch UI after the window is closed.
        DialogResult = true;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
}
