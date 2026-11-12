using UnityEngine;

/// <summary>Datos iniciales de un tipo de bola; el progreso no modifica este asset.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Ball")]
public class BallDefinition : ScriptableObject
{
    public enum BallRule { Standard, Alternating, Pulse }
    [SerializeField] private BallRule rule;
    [Min(0)] [SerializeField] private float ruleAmount = .25f;
    public BallRule Rule => rule;
    public float RuleAmount => ruleAmount;
    [SerializeField] private string title = "New Ball";
    [TextArea(2, 4)] [SerializeField] private string description;
    [SerializeField] private BallController prefab;
    [SerializeField] private bool startsUnlocked;
    [Min(0.1f)] [SerializeField] private double startingValue = 1d;
    [Min(1)] [SerializeField] private int startingCount = 3;
    [Min(0.1f)] [SerializeField] private float startingSpeed = 1f;
    [Min(0.5f)] [SerializeField] private float batchCooldown = 2.5f;
    [SerializeField] private Color displayColor = new Color(0.43f, 0.78f, 0.63f);
    [SerializeField] private string symbol = "B";

    public string Title => title;
    public string Description => description;
    public BallController Prefab => prefab;
    public bool StartsUnlocked => startsUnlocked;
    public double StartingValue => startingValue;
    public int StartingCount => startingCount;
    public float StartingSpeed => startingSpeed;
    public float BatchCooldown => batchCooldown;
    public Color DisplayColor => displayColor;
    public string Symbol => symbol;
}
