using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Guarda la economía y las mejoras durante la partida. Actualiza el saldo del HUD.
/// No guarda archivos: al salir de Play se reinicia el progreso, igual que antes.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // La misma estructura sirve para mejoras globales y mejoras de un tipo de bola.
    [Serializable]
    private class BallUpgrades
    {
        public double value;
        public int count;
        public float speed;
        public double pin;
        public double slot;
        public double reward = 1d;
        public float cooldown;
        public bool autoLaunch;
    }

    [Header("Economía")]
    [SerializeField] private double money;
    [SerializeField] private BallUpgrades globalUpgrades = new BallUpgrades();
    [SerializeField] private GaltonBoard board;
    [Header("Textos de la interfaz")]
    [SerializeField] private TMP_Text moneyText;

    private readonly HashSet<BallDefinition> unlockedBalls = new HashSet<BallDefinition>();
    private readonly Dictionary<BallDefinition, BallUpgrades> ballUpgrades = new Dictionary<BallDefinition, BallUpgrades>();
    private double displayedMoney;
    private readonly Queue<IncomeSample> income = new Queue<IncomeSample>();
    private double recentIncome;
    public double IncomePerMinute => recentIncome;
    public double LastBatch { get; private set; }
    private struct IncomeSample { public float time; public double amount; }
    public void ReportBatch(double total) { LastBatch = total; DrawBalance(); }
    public double Money => money;
    public bool PinTweakerUnlocked { get; private set; }
    public bool WorkshopUnlocked { get; private set; }
    public int UnlockedBatches { get; private set; } = 1;
    public int ModuleSlots => Settings.startingModuleSlots;
    public BouncyBallySettings Settings { get; private set; }
    [SerializeField] private SkillTreeController tree;
    [SerializeField] private PinModuleInventory inventory;
    [SerializeField] private ProgressionSystem progression;
    [SerializeField] private RewardCollection collection;
    public SkillTreeController Tree => tree;
    public PinModuleInventory Inventory => inventory;
    public ProgressionSystem Progression => progression;
    public RewardCollection Collection => collection;
    public double RunEarned { get; private set; }
    private BallDefinition pulseSource;
    private float pulseUntil;
    public void TriggerPulse(BallDefinition source) { pulseSource = source; pulseUntil = Time.time + Settings.pulseDuration; }
    public double PulseFactor(BallDefinition ball) => Time.time < pulseUntil && pulseSource != ball ? 1d + pulseSource.RuleAmount : 1d;
    public bool CanGrantCollectionReward(RewardDefinition reward)
    {
        if (reward == null || string.IsNullOrWhiteSpace(reward.id) || !IsFinite(reward.amount)) return false;
        if (reward.effect == CollectionRewardEffect.Modifier) return reward.modifier != null && !Inventory.OwnedModules.Any(x => x != null && x.id == reward.modifier.id);
        if (reward.effect == CollectionRewardEffect.RewardFactor) return reward.amount >= 1 && IsFinite(globalUpgrades.reward * reward.amount);
        if (reward.effect == CollectionRewardEffect.BallValue) return reward.amount > 0 && IsFinite(globalUpgrades.value + reward.amount);
        if (reward.effect == CollectionRewardEffect.PinBonus) return reward.amount > 0 && IsFinite(globalUpgrades.pin + reward.amount);
        if (reward.effect == CollectionRewardEffect.SlotBonus) return reward.amount > 0 && IsFinite(globalUpgrades.slot + reward.amount);
        if (reward.effect == CollectionRewardEffect.UnlockBall) return reward.ball != null && !IsBallUnlocked(reward.ball);
        return reward.amount > 0;
    }
    public void ApplyCollectionReward(RewardDefinition reward)
    {
        switch (reward.effect)
        {
            case CollectionRewardEffect.Modifier: Inventory.Grant(reward.modifier); break;
            case CollectionRewardEffect.BallValue: globalUpgrades.value += reward.amount; break;
            case CollectionRewardEffect.RewardFactor: globalUpgrades.reward *= System.Math.Max(1, reward.amount); break;
            case CollectionRewardEffect.PinBonus: globalUpgrades.pin += reward.amount; break;
            case CollectionRewardEffect.SlotBonus: globalUpgrades.slot += reward.amount; break;
            case CollectionRewardEffect.UnlockBall: unlockedBalls.Add(reward.ball); break;
        }
        ProgressChanged?.Invoke(); MoneyChanged?.Invoke();
    }
    public double BallValue => 1d + globalUpgrades.value;
    public event Action ProgressChanged;
    public GaltonBoard Board => board;
    public event Action MoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Settings = BouncyBallySettings.Current;
        money = Settings.startingMoney;
        Progression.Initialize(this, Inventory, Settings);
        Collection.Initialize(this, Inventory, Progression, Settings);
    }

    private void Start()
    {
        if (Camera.main != null)
            Camera.main.backgroundColor = GaltonTheme.Background;
        displayedMoney = money;
        DrawBalance();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Suaviza el contador mostrado. El dinero real cambia de inmediato.
    private void Update()
    {
        while (income.Count > 0 && Time.time - income.Peek().time >= 60f)
            recentIncome = Math.Max(0d, recentIncome - income.Dequeue().amount);
        if (displayedMoney == money) { DrawBalance(); return; }
        double difference = money - displayedMoney;
        displayedMoney = Math.Abs(difference) < 0.01d
            ? money : displayedMoney + difference * (1d - Math.Exp(-10d * Time.unscaledDeltaTime));
        DrawBalance();
    }

    public void BindMoney(TMP_Text view) { moneyText = view; DrawBalance(); }

    private void DrawBalance()
    {
        if (moneyText == null)
            return;
        moneyText.color = GaltonTheme.Ink;
        moneyText.text = "$" + GaltonNumbers.Format(displayedMoney) +
            "\n<size=16>$" + GaltonNumbers.Format(recentIncome) + "/min   Last +$" + GaltonNumbers.Format(LastBatch) + "</size>";
    }

    /// <summary>Descuenta una compra válida e informa a los paneles de que deben actualizarse.</summary>
    public bool TrySpendMoney(double cost)
    {
        if (!IsFinite(cost) || cost <= 0d || money < cost)
            return false;
        money -= cost;
        MoneyChanged?.Invoke();
        return true;
    }

    /// <summary>Ingresa una recompensa positiva y evita números inválidos en la economía.</summary>
    public void AddMoney(double amount)
    {
        if (!IsFinite(amount) || amount <= 0d || !IsFinite(money + amount))
            return;
        money += amount;
        RunEarned += amount;
        Progression?.NotifyEarned(amount);
        income.Enqueue(new IncomeSample { time = Time.time, amount = amount });
        recentIncome += amount;
        MoneyChanged?.Invoke();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public bool IsBallUnlocked(BallDefinition type)
    {
        return type != null && (type.StartsUnlocked || unlockedBalls.Contains(type));
    }

    // Cada tipo consulta su propio estado. Las mejoras globales se aplican después de los datos base.
    public double GetBallValue(BallDefinition type) => Math.Max(0.1d, type.StartingValue + globalUpgrades.value + GetUpgrades(type).value);
    public int GetBallCount(BallDefinition type) => Mathf.Clamp(type.StartingCount + globalUpgrades.count + GetUpgrades(type).count, 1, 250);
    public float GetBallSpeed(BallDefinition type) => Mathf.Clamp(type.StartingSpeed + globalUpgrades.speed + GetUpgrades(type).speed, 0.1f, 8f);
    public double GetPinBonus(BallDefinition type) => globalUpgrades.pin + GetUpgrades(type).pin;
    public double GetSlotBonus(BallDefinition type) => globalUpgrades.slot + GetUpgrades(type).slot;
    public double GetRewardFactor(BallDefinition type) => globalUpgrades.reward * GetUpgrades(type).reward;
    public float GetCooldown(BallDefinition type) => Mathf.Max(0.5f, type.BatchCooldown *
        (1f - Mathf.Clamp(globalUpgrades.cooldown + GetUpgrades(type).cooldown, 0f, 0.85f)));
    public bool HasAutoLaunch(BallDefinition type) => globalUpgrades.autoLaunch || GetUpgrades(type).autoLaunch;

    private BallUpgrades GetUpgrades(BallDefinition type)
    {
        if (!ballUpgrades.TryGetValue(type, out BallUpgrades upgrades))
        {
            upgrades = new BallUpgrades();
            ballUpgrades.Add(type, upgrades);
        }
        return upgrades;
    }

    /// <summary>Rechaza destinos bloqueados, efectos inválidos y descuentos ya limitados.</summary>
    public bool CanApplySkill(SkillDefinition skill)
    {
        if (skill == null) return false;
        if (skill.Effect == SkillEffect.PinTweaker) return WorkshopUnlocked && !PinTweakerUnlocked;
        if (skill.Effect == SkillEffect.UnlockBatch) return Mathf.RoundToInt(skill.AmountPerLevel) == UnlockedBatches + 1 && UnlockedBatches < BallSpawner.MaxBatches;
        if (skill.Effect == SkillEffect.Workshop) return !WorkshopUnlocked;
        if (skill.IsBoardSkill && (board == null || !board.enabled)) return false;
        if (skill.Effect == SkillEffect.BoardRows && !board.CanExpand) return false;
        if (skill.Effect == SkillEffect.Custom)
            return skill.CustomAction != null && skill.CustomAction.CanApply(this, skill);
        foreach (BallDefinition type in skill.TargetBalls)
        {
            if (type == null || !IsBallUnlocked(type)) return false;
            if (skill.Effect == SkillEffect.AutoLaunch && HasAutoLaunch(type)) return false;
            if (skill.Effect == SkillEffect.BallCount && GetBallCount(type) >= 250) return false;
            if (skill.Effect == SkillEffect.BallSpeed && GetBallSpeed(type) >= 8f) return false;
            if (skill.Effect == SkillEffect.Cooldown && GetCooldown(type) <= Mathf.Max(0.5f, type.BatchCooldown * 0.15f) + 0.001f) return false;
        }
        return true;
    }

    /// <summary>Aplica una compra ya pagada. Las expansiones esperan al final de la tanda.</summary>
    public void ApplySkill(SkillDefinition skill)
    {
        switch (skill.Effect)
        {
            case SkillEffect.UnlockBatch: UnlockedBatches = Mathf.Clamp(Mathf.RoundToInt(skill.AmountPerLevel), 1, BallSpawner.MaxBatches); break;
            case SkillEffect.Workshop: WorkshopUnlocked = true; break;
            case SkillEffect.PinTweaker: PinTweakerUnlocked = true; break;
            case SkillEffect.BoardRows: board.RequestExpansion(); break;
            case SkillEffect.SlotMultiplier: board.UpgradeSlots(); break;
            case SkillEffect.UnlockBall: unlockedBalls.Add(skill.UnlockBall); break;
            case SkillEffect.Custom: skill.CustomAction.Apply(this, skill); break;
            default:
                if (skill.TargetBalls.Count == 0)
                    ApplyBonus(globalUpgrades, skill.Effect, skill.AmountPerLevel);
                else
                    foreach (BallDefinition type in skill.TargetBalls)
                        if (type != null)
                            ApplyBonus(GetUpgrades(type), skill.Effect, skill.AmountPerLevel);
                break;
        }
        ProgressChanged?.Invoke();
        MoneyChanged?.Invoke();
    }

    // Una única función modifica tanto las mejoras globales como las específicas.
    private static void ApplyBonus(BallUpgrades stats, SkillEffect effect, float amount)
    {
        switch (effect)
        {
            case SkillEffect.BallValue: stats.value += amount; break;
            case SkillEffect.BallCount: stats.count += Mathf.Max(1, Mathf.RoundToInt(amount)); break;
            case SkillEffect.BallSpeed: stats.speed += amount; break;
            case SkillEffect.PinBonus: stats.pin += amount; break;
            case SkillEffect.SlotBonus: stats.slot += amount; break;
            case SkillEffect.Cooldown: stats.cooldown += amount; break;
            case SkillEffect.RewardMultiplier: stats.reward *= Math.Max(1d, amount); break;
            case SkillEffect.AutoLaunch: stats.autoLaunch = true; break;
        }
    }
}
