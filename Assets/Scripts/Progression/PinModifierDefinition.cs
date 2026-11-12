using UnityEngine;
public enum PinModifierEffect { AddBonus, LuckyBonus, Echo, Streak, Alternating, Chain }

/// <summary>Un modificador único con regla configurable, icono, color y parámetros.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Pin Modifier")]
public class PinModifierDefinition : ScriptableObject
{
    public string id;
    public string title = "New Pin";
    public string symbol = "+";
    [TextArea] public string description;
    public Sprite icon;
    public Color color = Color.cyan;
    public PinModifierEffect effect;
    [Min(0)] public double amount = 1;
    [Range(0, 1)] public float chance = .2f;
    [Min(1)] public int hitThreshold = 3;
    [Range(0, 1)] public float biasSynergy = .25f;
}
