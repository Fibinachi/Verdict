using System;
using System.Collections.Generic;
using System.Linq;

namespace Verdict.Models;

public class BiasTerrain
{
    public string JurorName { get; set; } = string.Empty;
    
    public double[] GridEA { get; set; } = Array.Empty<double>();
    
    public double[] GridEB { get; set; } = Array.Empty<double>();
    
    public double[,] GridZ { get; set; } = new double[0, 0];
    
    public BiasTerrain(BiasSurfaceDefinition surface, double groupPressure = 0.0, int resolution = 80)
    {
        JurorName = surface.JurorName;
        GridEA = new double[resolution];
        GridEB = new double[resolution];
        GridZ = new double[resolution, resolution];
        
        for (int i = 0; i < resolution; i++)
        {
            GridEA[i] = -3.0 + (6.0 * i / (resolution - 1));
            GridEB[i] = -3.0 + (6.0 * i / (resolution - 1));
        }
        
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                GridZ[i, j] = surface.Evaluate(GridEA[i], GridEB[j], groupPressure);
            }
        }
    }
    
    public double GetInterpolatedBias(double eA, double eB, BiasSurfaceDefinition surface, double groupPressure = 0.0)
    {
        if (GridEA.Length == 0) return surface.Evaluate(eA, eB, groupPressure);
        
        int resolution = GridEA.Length;
        double step = 6.0 / (resolution - 1);
        
        double normEA = (eA + 3.0) / step;
        double normEB = (eB + 3.0) / step;
        
        int i0 = (int)Math.Floor(normEA);
        int j0 = (int)Math.Floor(normEB);
        
        i0 = Math.Max(0, Math.Min(resolution - 2, i0));
        j0 = Math.Max(0, Math.Min(resolution - 2, j0));
        
        double t = normEA - i0;
        double u = normEB - j0;
        
        double z00 = GridZ[i0, j0];
        double z10 = GridZ[i0 + 1, j0];
        double z01 = GridZ[i0, j0 + 1];
        double z11 = GridZ[i0 + 1, j0 + 1];
        
        return (1 - t) * (1 - u) * z00 + t * (1 - u) * z10 + (1 - t) * u * z01 + t * u * z11;
    }
}