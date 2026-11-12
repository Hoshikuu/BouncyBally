using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Catálogo y balance central. Todos los valores de diseño se editan en el editor de Unity.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Settings")]
public class BouncyBallySettings : ScriptableObject
{
    private static BouncyBallySettings current;
    public static BouncyBallySettings Current => current != null ? current :
        (current = Resources.Load<BouncyBallySettings>("BouncyBallySettings"));
    public string gameTitle = "Bouncy Bally";
    public BallDefinition[] balls;
    public SkillDefinition[] skills;
    public PinModifierDefinition[] modifiers;
    public ChallengeDefinition[] challenges;
    [FormerlySerializedAs("capsules")] public GachaDefinition[] gachas;
    [Header("Inicio y tablero")]
    [Min(0)] public double startingMoney;
    [Range(1, 8)] public int startingRows = 6;
    [Range(.4f, .95f)] public float boardWidth = .78f;
    [Range(.4f, .95f)] public float boardHeight = .84f;
    [Min(.1f)] public float pinScale = .45f;
    [Min(.1f)] public float ballScale = .28f;
    [Min(.05f)] public float segmentDuration = .34f;
    [Min(0)] public float spawnInterval = .11f;
    [Range(0.01f, 2f)] public float payoutMultiplier = .65f;
    [Range(1, 500)] public int maxActiveBalls = 250;
    public bool rechargeDuringFlight = true;
    [Header("Árbol e interfaz")]
    public Vector2 graphSize = new Vector2(4000, 2800);
    [Range(100, 240)] public float nodeSize = 170;
    [Range(.15f, 1f)] public float minimumZoom = .22f;
    [Range(1f, 3f)] public float maximumZoom = 2f;
    [Range(.25f, 1.5f)] public float openingZoom = .75f;
    [Range(80, 180)] public float batchCardHeight = 112;
    [Range(14, 32)] public float textSize = 19;
    [Header("Pin Tweaker")]
    [Range(.01f, .2f)] public float tuningStep = .05f;
    [Min(1)] public double tuningFirstCost = 50000;
    [Range(1.01f, 1.5f)] public float tuningExponent = 1.1f;
    [Min(.1f)] public float pulseDuration = 2f;
    [Range(1, 12)] public int startingModuleSlots = 3;
    [Header("Efectos")]
    [Range(0, 3)] public float effectIntensity = 1.7f;
    [Range(0, .5f)] public float shakeDistance = .035f;
    [Range(64, 1600)] public int particleBudget = 900;
}
