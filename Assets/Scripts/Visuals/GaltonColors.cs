using System;
using UnityEngine;

/// <summary>Paleta configurable: pins por nivel, bola por valor, estela por ciclos y slots por multiplicador.</summary>
public class GaltonColors : ScriptableObject
{
    [Header("Pins: color según nivel")]
    [Min(1)] [SerializeField] private int pinLevelAtWhite = 30;
    [SerializeField] private Gradient pinColors = DefaultRgbGradient();

    [Header("Ciclos de la bola y de la estela")]
    [Tooltip("Valor visual en el que la bola vuelve a blanco y la estela avanza.")]
    [Min(1f)] [SerializeField] private float ballCycleValue = 1000000f;
    [Tooltip("Colores dentro de un ciclo. La posición usa una escala logarítmica.")]
    [SerializeField] private Gradient ballColors = DefaultBallGradient();
    [Tooltip("Ciclos completos necesarios para alcanzar el último color de la estela.")]
    [Min(1)] [SerializeField] private int trailStepsToEnd = 4;
    [Tooltip("Colores de la estela según los ciclos completados.")]
    [SerializeField] private Gradient trailColors = DefaultTrailGradient();

    [Header("Slots: color según multiplicador")]
    [Min(2)] [SerializeField] private int slotMultiplierAtWhite = 32;
    [SerializeField] private Gradient slotColors = DefaultSlotGradient();

    /// <summary>Obtiene el color por nivel, limitado al recorrido del gradiente.</summary>
    public Color ForPin(int level)
    {
        return VisiblePinColor(pinColors.Evaluate(Mathf.Clamp01(level / (float)Mathf.Max(1, pinLevelAtWhite))));
    }

    /// <summary>Representa el valor dentro de su ciclo con una escala logarítmica.</summary>
    public Color ForBall(double value)
    {
        double safeValue = GaltonNumbers.NonNegative(value);
        double cycle = Math.Max(1d, ballCycleValue);
        double withinCycle = safeValue % cycle;

        // La escala logarítmica diferencia 1k, 10k y 100k dentro del mismo ciclo de 1M.
        double progress = Math.Log10(withinCycle + 1d) / Math.Log10(cycle + 1d);
        return ballColors.Evaluate((float)progress);
    }

    /// <summary>Avanza el gradiente por cada ciclo completo del valor de la bola.</summary>
    public Color ForTrail(double value)
    {
        double safeValue = GaltonNumbers.NonNegative(value);
        double completedCycles = Math.Floor(safeValue / Math.Max(1d, ballCycleValue));
        double progress = Math.Min(1d, completedCycles / Math.Max(1, trailStepsToEnd));
        return trailColors.Evaluate((float)progress);
    }

    /// <summary>Obtiene el color del multiplicador del slot.</summary>
    public Color ForSlot(double multiplier)
    {
        float t = (float)(Math.Log(Math.Max(1d, multiplier), 2d) / Math.Log(Math.Max(2, slotMultiplierAtWhite), 2d));
        return slotColors.Evaluate(Mathf.Clamp01(t));
    }

    /// <summary>Conserva el tono dinámico y garantiza luminosidad también sin iluminación adicional.</summary>
    public static Color VisiblePinColor(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        return Color.Lerp(Color.HSVToRGB(hue, Mathf.Max(.42f, saturation), Mathf.Max(.88f, value)), Color.white, .16f);
    }
    private static Gradient DefaultSlotGradient()
    {
        Gradient result = new Gradient();
        result.SetKeys(new[] {
            new GradientColorKey(new Color(.48f,.61f,.74f), 0f),
            new GradientColorKey(new Color(.25f,.58f,1f), .2f),
            new GradientColorKey(new Color(.25f,.86f,.51f), .4f),
            new GradientColorKey(new Color(.72f,.43f,1f), .6f),
            new GradientColorKey(new Color(1f,.55f,.24f), .8f),
            new GradientColorKey(new Color(1f,.83f,.28f), 1f)
        }, new[] { new GradientAlphaKey(1f,0f), new GradientAlphaKey(1f,1f) });
        return result;
    }
    private static Gradient DefaultRgbGradient()
    {
        Gradient result = new Gradient();
        result.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.28f, 0.58f, 0.40f), 0f),      // Verde
                new GradientColorKey(new Color(0.24f, 0.61f, 0.66f), 1f / 3f), // Turquesa
                new GradientColorKey(new Color(0.91f, 0.57f, 0.31f), 2f / 3f), // Naranja
                new GradientColorKey(new Color(0.76f, 0.39f, 0.43f), 1f)       // Coral
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return result;
    }

    private static Gradient DefaultBallGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                                 new GradientColorKey(Color.red, 0.5f),
                                 new GradientColorKey(Color.blue, 2f / 3f),
                                 new GradientColorKey(Color.green, 5f / 6f),
                                 new GradientColorKey(Color.black, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private static Gradient DefaultTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                                 new GradientColorKey(Color.red, 0.25f),
                                 new GradientColorKey(Color.blue, 0.5f),
                                 new GradientColorKey(Color.green, 0.75f),
                                 new GradientColorKey(Color.black, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
    /// <summary>Colores de reserva cuando un objeto no tiene una paleta asignada.</summary>
    public static Color FallbackColor(double multiplier)
    {
        if (multiplier < 1.001d)
        {
            return new Color(0.78f, 0.83f, 0.88f);
        } // x1
        if (multiplier < 1.5d)
        {
            return new Color(0.38f, 0.88f, 0.69f);
        } // Bonus pequeño
        if (multiplier < 2d)
        {
            return new Color(0.30f, 0.82f, 0.96f);
        }
        if (multiplier < 4d)
        {
            return new Color(0.36f, 0.64f, 1.00f);
        } // x2
        if (multiplier < 8d)
        {
            return new Color(0.76f, 0.49f, 0.98f);
        } // x4
        if (multiplier < 16d)
        {
            return new Color(1.00f, 0.75f, 0.34f);
        } // x8
        return new Color(1.00f, 0.42f, 0.69f);
    }
}
