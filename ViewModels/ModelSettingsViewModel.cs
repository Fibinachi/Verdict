using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Verdict.Models;

namespace Verdict.ViewModels;

public class ModelSettingsViewModel : ViewModelBase
{
    private AIModelConfiguration? _selectedModel;
    private double _globalTemperature = 0.7;
    private int _globalMaxTokens = 4096;

    private static string ModelsFolderPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");

    public ObservableCollection<AIModelConfiguration> Models { get; }
    public ObservableCollection<BiasFactor> DefaultBiasFactors { get; } = new();

    private double _ageBiasWeight = 0.55;
    private double _genderBiasWeight = 0.45;
    private double _educationBiasWeight = 0.80;
    private double _incomeBiasWeight = 0.50;
    private double _politicalBiasWeight = 0.90;
    private double _ethnicityBiasWeight = 0.60;
    private double _religionBiasWeight = 0.40;
    private double _jurorExperienceWeight = 0.70;
    private double _legalKnowledgeWeight = 0.75;
    private double _professionalBackgroundWeight = 0.55;
    private double _communityTiesWeight = 0.65;
    private double _communicationStyleWeight = 0.35;

    public double AgeBiasWeight
    {
        get => _ageBiasWeight;
        set => SetProperty(ref _ageBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double GenderBiasWeight
    {
        get => _genderBiasWeight;
        set => SetProperty(ref _genderBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double EducationBiasWeight
    {
        get => _educationBiasWeight;
        set => SetProperty(ref _educationBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double IncomeBiasWeight
    {
        get => _incomeBiasWeight;
        set => SetProperty(ref _incomeBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double PoliticalBiasWeight
    {
        get => _politicalBiasWeight;
        set => SetProperty(ref _politicalBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double EthnicityBiasWeight
    {
        get => _ethnicityBiasWeight;
        set => SetProperty(ref _ethnicityBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double ReligionBiasWeight
    {
        get => _religionBiasWeight;
        set => SetProperty(ref _religionBiasWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double JurorExperienceWeight
    {
        get => _jurorExperienceWeight;
        set => SetProperty(ref _jurorExperienceWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double LegalKnowledgeWeight
    {
        get => _legalKnowledgeWeight;
        set => SetProperty(ref _legalKnowledgeWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double ProfessionalBackgroundWeight
    {
        get => _professionalBackgroundWeight;
        set => SetProperty(ref _professionalBackgroundWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double CommunityTiesWeight
    {
        get => _communityTiesWeight;
        set => SetProperty(ref _communityTiesWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public double CommunicationStyleWeight
    {
        get => _communicationStyleWeight;
        set => SetProperty(ref _communicationStyleWeight, Math.Clamp(value, 0.0, 1.0));
    }

    public AIModelConfiguration? SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public double GlobalTemperature
    {
        get => _globalTemperature;
        set => SetProperty(ref _globalTemperature, value);
    }

    public int GlobalMaxTokens
    {
        get => _globalMaxTokens;
        set => SetProperty(ref _globalMaxTokens, value);
    }

    public ModelSettingsViewModel(IEnumerable<AIModelConfiguration> initialModels,
        double globalTemperature = 0.7, int globalMaxTokens = 4096,
        IEnumerable<BiasFactor>? defaultBiasFactors = null)
    {
        Models = new ObservableCollection<AIModelConfiguration>(initialModels);
        GlobalTemperature = globalTemperature;
        GlobalMaxTokens = globalMaxTokens;

        if (defaultBiasFactors != null)
        {
            foreach (var bf in defaultBiasFactors)
                DefaultBiasFactors.Add(new BiasFactor { Name = bf.Name, Weight = bf.Weight });

            SetBiasWeightsFromFactors(defaultBiasFactors);
        }

        LoadFromFolder();
    }

    public void SetBiasWeightsFromFactors(IEnumerable<BiasFactor>? biasFactors)
    {
        if (biasFactors == null) return;

        foreach (var factor in biasFactors)
        {
            if (factor == null || string.IsNullOrWhiteSpace(factor.Name))
                continue;

            switch (factor.Name.Trim().ToLowerInvariant())
            {
                case "age":
                case "age weight":
                case "age bias":
                    AgeBiasWeight = factor.Weight;
                    break;
                case "gender":
                case "gender weight":
                case "gender bias":
                    GenderBiasWeight = factor.Weight;
                    break;
                case "education level":
                case "education":
                case "education weight":
                    EducationBiasWeight = factor.Weight;
                    break;
                case "income level":
                case "income":
                case "income weight":
                    IncomeBiasWeight = factor.Weight;
                    break;
                case "political affiliation":
                case "political":
                case "political weight":
                    PoliticalBiasWeight = factor.Weight;
                    break;
                case "ethnicity":
                case "race / ethnicity":
                case "ethnicity weight":
                    EthnicityBiasWeight = factor.Weight;
                    break;
                case "religion":
                case "religious affiliation":
                case "religion weight":
                    ReligionBiasWeight = factor.Weight;
                    break;
                case "juror experience":
                case "juror experience weight":
                case "experience":
                    JurorExperienceWeight = factor.Weight;
                    break;
                case "legal knowledge":
                case "legal knowledge weight":
                    LegalKnowledgeWeight = factor.Weight;
                    break;
                case "professional background":
                case "professional background weight":
                    ProfessionalBackgroundWeight = factor.Weight;
                    break;
                case "community ties":
                case "community ties weight":
                    CommunityTiesWeight = factor.Weight;
                    break;
                case "communication style":
                case "communication style weight":
                    CommunicationStyleWeight = factor.Weight;
                    break;
            }
        }
    }

    public IEnumerable<BiasFactor> GetCurrentBiasWeightFactors()
    {
        return new List<BiasFactor>
        {
            new BiasFactor { Name = "Age", Weight = AgeBiasWeight },
            new BiasFactor { Name = "Gender", Weight = GenderBiasWeight },
            new BiasFactor { Name = "Education Level", Weight = EducationBiasWeight },
            new BiasFactor { Name = "Income Level", Weight = IncomeBiasWeight },
            new BiasFactor { Name = "Political Affiliation", Weight = PoliticalBiasWeight },
            new BiasFactor { Name = "Ethnicity", Weight = EthnicityBiasWeight },
            new BiasFactor { Name = "Religion", Weight = ReligionBiasWeight },
            new BiasFactor { Name = "Juror Experience", Weight = JurorExperienceWeight },
            new BiasFactor { Name = "Legal Knowledge", Weight = LegalKnowledgeWeight },
            new BiasFactor { Name = "Professional Background", Weight = ProfessionalBackgroundWeight },
            new BiasFactor { Name = "Community Ties", Weight = CommunityTiesWeight },
            new BiasFactor { Name = "Communication Style", Weight = CommunicationStyleWeight }
        };
    }

    public void LoadFromFolder()
    {
        string folderPath = ModelsFolderPath;
        if (!Directory.Exists(folderPath)) return;

        foreach (string jsonFile in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(jsonFile);
                var model = JsonSerializer.Deserialize<AIModelConfiguration>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                if (model != null)
                {
                    string baseName = model.FriendlyName;
                    int suffix = 1;
                    while (Models.Any(m => m.FriendlyName == model.FriendlyName))
                    {
                        model.FriendlyName = $"{baseName} ({suffix})";
                        suffix++;
                    }
                    Models.Add(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load {jsonFile}: {ex.Message}");
            }
        }
    }

    public void SaveSelectedToFolder()
    {
        if (SelectedModel == null) return;

        string folderPath = ModelsFolderPath;
        Directory.CreateDirectory(folderPath);

        string fileName = $"{SelectedModel.ModelId ?? "model"}.json".ToLower();
        string filePath = Path.Combine(folderPath, fileName);

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(SelectedModel, options);
        File.WriteAllText(filePath, json);
    }

    public void DeleteModel()
    {
        if (SelectedModel == null) return;

        try
        {
            string folderPath = ModelsFolderPath;
            if (Directory.Exists(folderPath))
            {
                foreach (string jsonFile in Directory.GetFiles(folderPath, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonFile);
                        var model = JsonSerializer.Deserialize<AIModelConfiguration>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (model != null && model.ModelId == SelectedModel.ModelId)
                        {
                            File.Delete(jsonFile);
                            break;
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting model file: {ex.Message}");
        }

        Models.Remove(SelectedModel);
        SelectedModel = null;
    }
}
