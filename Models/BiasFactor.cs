namespace Verdict.Models;

public class BiasFactor : ObservableObject
{
    private string _name = string.Empty;
    private double _weight = 0.0;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public double Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, Math.Clamp(value, -1.0, 1.0));
    }
}
