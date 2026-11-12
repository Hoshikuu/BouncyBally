using System;

/// <summary>Límites simétricos y coste según el número de ajustes pagados, incluidos los de regreso.</summary>
public static class PinTuning
{
    public const int MaxSteps = 4;
    public const float StepSize = 0.05f;
    public const float MinimumChance = 0.3f;
    public const float MaximumChance = 0.7f;

    public static double Cost(int purchases, int direction, double first = 50000d, double exponent = 1.1d)
    {
        if ((direction != -1 && direction != 1) || purchases < 0 || double.IsNaN(first) || double.IsInfinity(first) || first <= 0 || double.IsNaN(exponent) || double.IsInfinity(exponent) || exponent < 1)
            return double.PositiveInfinity;
        double power = Math.Log(first) + (double)purchases * purchases * Math.Log(exponent);
        // Un precio que no cabe en la economía se presenta como no disponible y nunca se cobra.
        if (power > Math.Log(double.MaxValue) - 1) return double.PositiveInfinity;
        double value = first * Math.Pow(exponent, (double)purchases * purchases);
        return double.IsInfinity(value) ? double.PositiveInfinity : Math.Ceiling(value < 1e12 ? Math.Round(value, 6) : value);
    }
}
