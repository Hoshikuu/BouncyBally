using System;
using System.Globalization;

/// <summary>Valida valores visuales y abrevia números grandes sin alterar los cálculos.</summary>
public static class GaltonNumbers
{
    private static readonly string[] Units = { "", "K", "M", "B", "T", "Qa", "Qi" };

    /// <summary>Convierte valores visuales inválidos o negativos en un valor seguro.</summary>
    public static double NonNegative(double value)
    {
        if (double.IsNaN(value) || value < 0d)
        {
            return 0d;
        }
        if (double.IsPositiveInfinity(value))
        {
            return double.MaxValue;
        }
        return value;
    }

    /// <summary>Muestra hasta dos decimales y usa K, M, B, T, Qa o Qi cuando corresponde.</summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value))
        {
            return "0";
        }
        if (double.IsInfinity(value))
        {
            return "∞";
        }
        double magnitude = Math.Abs(value);
        int unit = 0;
        while (magnitude >= 1000d && unit < Units.Length - 1)
        {
            magnitude /= 1000d;
            unit++;
        }
        if (unit == Units.Length - 1 && magnitude >= 1000d)
        {
            return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
        }

        double shown = unit == 0 ? value : magnitude * Math.Sign(value);
        return shown.ToString(unit == 0 ? "#,##0.##" : "0.##", CultureInfo.InvariantCulture) + Units[unit];
    }
}
