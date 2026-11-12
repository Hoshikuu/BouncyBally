using UnityEngine;

/// <summary>Potencia cada tercer pin sin duplicar las estadísticas de la bola básica.</summary>
public class BallLucky : BallBase
{
    [Header("Efecto de suerte")]
    [Min(1)] [SerializeField] private int everyNPins = 3;
    [Min(1f)] [SerializeField] private float luckyPinMultiplier = 2f;
    [Min(0f)] [SerializeField] private float luckyFlatBonus = 0.1f;
    private int pinsTouched;

    /// <summary>Reinicia los contactos y prepara las estadísticas comunes.</summary>
    public override void Setup(double value, GaltonColors palette)
    {
        pinsTouched = 0;
        base.Setup(value, palette);
    }

    /// <summary>Potencia el bonus cuando corresponde y lo acumula mediante la clase base.</summary>
    public override double AddPinBonus(double amount)
    {
        pinsTouched++;
        if (pinsTouched % Mathf.Max(1, everyNPins) == 0)
            amount = amount * Mathf.Max(1f, luckyPinMultiplier) + luckyFlatBonus;
        return base.AddPinBonus(amount);
    }
}
