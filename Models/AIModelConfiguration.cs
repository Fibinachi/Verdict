using System.Collections.Generic;

namespace Verdict.Models;

public class AIModelConfiguration : ObservableObject
{
    private string _friendlyName = "New Model";
    private string _provider = "DeepSeek";
    private string _modelId = string.Empty;
    private string _endpoint = string.Empty;
    private string _apiKey = string.Empty;
    private string _status = "Not Tested";
    private bool _isTesting = false;
    private Dictionary<string, string> _customSettings = new();

    public string FriendlyName
    {
        get => _friendlyName;
        set => SetProperty(ref _friendlyName, value);
    }

    public string Provider
    {
        get => _provider;
        set => SetProperty(ref _provider, value);
    }

    public string ModelId
    {
        get => _modelId;
        set => SetProperty(ref _modelId, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        set => SetProperty(ref _endpoint, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public Dictionary<string, string> CustomSettings
    {
        get => _customSettings;
        set => SetProperty(ref _customSettings, value);
    }

    public double GetTemperature(double defaultValue = 0.7)
    {
        if (CustomSettings.TryGetValue("Temperature", out var tempStr) &&
            double.TryParse(tempStr, out var temp))
            return temp;
        return defaultValue;
    }

    public int GetMaxTokens(int defaultValue = 4096)
    {
        if (CustomSettings.TryGetValue("MaxTokens", out var tokensStr) &&
            int.TryParse(tokensStr, out var tokens))
            return tokens;
        return defaultValue;
    }
}
