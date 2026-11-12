using System;
using UnityEngine;

/// <summary>
/// Estadísticas comunes de las bolas. También representa la bola básica.
/// Calcula la recompensa; el controlador se encarga de ingresar el dinero.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BallBase : MonoBehaviour
{
    [Header("Estadísticas durante la partida")]
    [SerializeField] private double baseValue = 1d;
    [SerializeField] private double multiplierBonus;
    private GaltonColors colors;
    private SpriteRenderer ballRenderer;
    private Color identityColor = Color.white;

    // Notifica a los efectos: color de bola, color de estela y valor representado.
    public event Action<Color, Color, double> VisualChanged;
    public double BaseValue => baseValue;
    public double MultiplierBonus => multiplierBonus;
    public double VisualValue { get; private set; }

    /// <summary>Asigna el color identificativo del tipo de bola.</summary>
    public void SetIdentityColor(Color color) => identityColor = color;

    /// <summary>Asigna el valor de esta tanda y reinicia los bonus del recorrido.</summary>
    public virtual void Setup(double value, GaltonColors palette)
    {
        baseValue = GaltonNumbers.NonNegative(value);
        multiplierBonus = 0d;
        colors = palette;
        ballRenderer = GetComponent<SpriteRenderer>();
        UpdateVisualValue(baseValue);
    }

    /// <summary>Acumula el bonus y devuelve la cantidad aplicada para el texto flotante.</summary>
    public virtual double AddPinBonus(double amount)
    {
        double applied = GaltonNumbers.NonNegative(amount);
        multiplierBonus += applied;
        // Mientras no se conoce el slot, su multiplicador provisional es x1.
        UpdateVisualValue(baseValue * (1d + multiplierBonus));
        return applied;
    }

    /// <summary>Recompensa = valor base × (1 + bonus acumulado) × multiplicador del slot.</summary>
    public virtual double ComputeReward(double slotMultiplier)
    {
        double reward = baseValue * (1d + multiplierBonus) * Math.Max(1d, slotMultiplier);
        UpdateVisualValue(reward);
        return reward;
    }

    /// <summary>Cambia el aspecto sin modificar estadísticas ni entregar dinero.</summary>
    public void UpdateVisualValue(double value)
    {
        VisualValue = GaltonNumbers.NonNegative(value);
        Color valueColor = colors != null ? colors.ForBall(VisualValue) : Color.white;
        Color ballColor = Color.Lerp(identityColor, valueColor, 0.25f);
        Color trailColor = Color.Lerp(identityColor, colors != null ? colors.ForTrail(VisualValue) : Color.white, 0.3f);
        if (ballRenderer != null)
            ballRenderer.color = ballColor;
        VisualChanged?.Invoke(ballColor, trailColor, VisualValue);
    }
}
