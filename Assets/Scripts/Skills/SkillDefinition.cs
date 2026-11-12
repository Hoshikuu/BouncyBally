using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Conserva los identificadores de los efectos originales para no romper su serialización.
public enum SkillEffect
{
    BallValue = 0, BallCount = 1, BallSpeed = 2, PinBonus = 3, SlotBonus = 4,
    UnlockBall = 5, BoardRows = 6, SlotMultiplier = 7,
    Cooldown = 8, RewardMultiplier = 9, AutoLaunch = 10, Custom = 11, PinTweaker = 12, Workshop = 13, UnlockBatch = 18
}

/// <summary>Una mejora corta, con destino explícito, precio, requisito y efecto configurable.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Skill")]
public class SkillDefinition : ScriptableObject
{
    [SerializeField] private Sprite icon;
    public Sprite Icon => icon;
    [SerializeField] private string title = "New Upgrade";
    [TextArea(2, 4)] [SerializeField] private string description = "Describe the upgrade.";
    [SerializeField] private SkillEffect effect;
    [Tooltip("Vacío: sección global. Un destino: sección de esa bola.")]
    [SerializeField] private List<BallDefinition> targetBalls = new List<BallDefinition>();
    [SerializeField] private BallDefinition unlockBall;
    [SerializeField] private SkillAction customAction;
    [Min(0.01f)] [SerializeField] private float amountPerLevel = 1f;
    [Min(1f)] [SerializeField] private float firstCost = 12f;
    [Min(1f)] [SerializeField] private float costGrowth = 2f;
    [Range(1, 3)] [SerializeField] private int maxLevel = 2;
    [SerializeField] private Vector2 position = new Vector2(0.2f, 0.8f);
    [SerializeField] private SkillDefinition prerequisite;
    [Min(1)] [SerializeField] private int prerequisiteLevel = 1;

    public bool IsBoardSkill => effect == SkillEffect.BoardRows || effect == SkillEffect.SlotMultiplier;
    public string Title => title;
    public string Description => description;
    public SkillEffect Effect => effect;
    public IReadOnlyList<BallDefinition> TargetBalls => targetBalls;
    public BallDefinition UnlockBall => unlockBall;
    public SkillAction CustomAction => customAction;
    public float AmountPerLevel => amountPerLevel;
    private bool IsSingleLevel => effect == SkillEffect.UnlockBall || effect == SkillEffect.AutoLaunch || effect >= SkillEffect.PinTweaker;
    public int MaxLevel => IsSingleLevel ? 1 : maxLevel;
    public Vector2 Position => position;
    public SkillDefinition Prerequisite => prerequisite;
    public int PrerequisiteLevel => prerequisiteLevel;
    public BallDefinition SectionBall => targetBalls.Count == 1 ? targetBalls[0] : null;

    public double CostAt(int level) => Math.Ceiling(firstCost * Math.Pow(Math.Round(costGrowth, 6), level));

    private void OnValidate()
    {
        firstCost = Mathf.Max(1f, firstCost);
        costGrowth = Mathf.Max(1f, costGrowth);
        amountPerLevel = Mathf.Max(0.01f, amountPerLevel);
        maxLevel = Mathf.Clamp(maxLevel, 1, 3);
        prerequisiteLevel = Mathf.Clamp(prerequisiteLevel, 1, prerequisite != null ? prerequisite.MaxLevel : 3);
        if (prerequisite == this) prerequisite = null;
        if (effect == SkillEffect.RewardMultiplier) amountPerLevel = Mathf.Max(1f, amountPerLevel);
        if (IsSingleLevel) maxLevel = 1;
        if (IsBoardSkill) targetBalls.Clear();
    }

    /// <summary>Explica en inglés la compra, su destino y su tamaño real.</summary>
    public string Details(int level, bool complete, GaltonBoard board)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("Level " + level + "/" + MaxLevel);
        text.AppendLine(SectionBall != null ? SectionBall.Title + " only" : "Global upgrade");
        switch (effect)
        {
            case SkillEffect.RewardMultiplier: text.AppendLine("Reward ×" + GaltonNumbers.Format(amountPerLevel) + " per level"); break;
            case SkillEffect.Cooldown: text.AppendLine("Cooldown reduction +" + (amountPerLevel * 100f).ToString("0") + "% (cap 85%)"); break;
            case SkillEffect.AutoLaunch: text.AppendLine("Unlocks the Auto toggle on the batch card"); break;
            case SkillEffect.BoardRows: text.AppendLine("+1 row and slot, applied when all batches finish"); break;
            case SkillEffect.SlotMultiplier: text.AppendLine("All slots ×2"); break;
            case SkillEffect.UnlockBall: text.AppendLine("Unlocks " + (unlockBall != null ? unlockBall.Title : "a ball")); break;
            case SkillEffect.PinTweaker: text.AppendLine("Unlocks paid Left / Right controls on each pin (30–70% limits, $50K first adjustment)"); break;
            case SkillEffect.UnlockBatch: text.AppendLine("Unlocks Batch " + Mathf.RoundToInt(amountPerLevel)); break;
            case SkillEffect.Custom: text.AppendLine("Custom effect"); break;
            default: text.AppendLine("Per level: +" + GaltonNumbers.Format(amountPerLevel)); break;
        }
        if (board != null && IsBoardSkill) text.AppendLine("Board: " + board.Rows + "/8 rows");
        if (!complete) text.AppendLine("Cost: $" + GaltonNumbers.Format(CostAt(level)));
        if (prerequisite != null) text.AppendLine("Requires " + prerequisite.Title + " · level " + prerequisiteLevel);
        return text.ToString();
    }
}
