using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Probabilidad de dirección, bonus, nivel y animación de un pin. Permite compras individuales y por fila.</summary>
public class Pin : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Dirección (0 = izquierda, 1 = derecha)")]
    [Range(.3f, .7f)] [SerializeField] private float rightChance = 0.5f;

    [Header("Multiplicador: x1 equivale a bonus 0")]
    [SerializeField] private float multiplierBonus = 0f;

    [Header("Coste y bonus de cada nivel")]
    [Min(0f)] [SerializeField] private float bonusPerLevel = 0.1f;
    [Min(1f)] [SerializeField] private float initialUpgradeCost = 10f;
    [Min(1f)] [SerializeField] private float upgradeCostMultiplier = 1.6f;
    [Min(0)] [SerializeField] private int level = 0;

    [Header("Pulso de impacto")]
    [Min(0.01f)] [SerializeField] private float impactDuration = 0.16f;
    [Range(0f, 0.6f)] [SerializeField] private float impactGrow = 0.18f;

    [Header("Selección con el cursor")]
    [Range(0f, 1f)] [SerializeField] private float hoverBrightness = 0.3f;
    private bool hovered;
    private bool selectedInWorkshop;
    private bool placementClickSequence;
    public void SetSelected(bool value) { selectedInWorkshop = value; RefreshColor(); }
    private GaltonBoard board;
    private SpriteRenderer spriteRenderer;
    private GaltonColors colorPalette;
    private Coroutine impactRoutine;
    private Vector3 restingScale;

    [SerializeField] private int tuningStep;
    [SerializeField, Min(0)] private int tuningPurchases;
    public PinModifierDefinition Modifier { get; private set; }
    private readonly System.Collections.Generic.Dictionary<int,int> batchHits = new System.Collections.Generic.Dictionary<int,int>();
    public int TuningStep => tuningStep;
    public float RightChance => Mathf.Clamp(rightChance + tuningStep * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.tuningStep : .05f), PinTuning.MinimumChance, PinTuning.MaximumChance);

    public double TuningCost(int direction)
    {
        float next = RightChance + direction * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.tuningStep : PinTuning.StepSize);
        if (next < PinTuning.MinimumChance - .0001f || next > PinTuning.MaximumChance + .0001f) return double.PositiveInfinity;
        var config = BouncyBallySettings.Current;
        return config != null ? PinTuning.Cost(tuningPurchases,direction,config.tuningFirstCost,System.Math.Round(config.tuningExponent,6)) : double.PositiveInfinity;
    }
    public bool CanTune(int direction) => GameManager.Instance != null && GameManager.Instance.PinTweakerUnlocked &&
        (direction == -1 || direction == 1) &&
        rightChance + (tuningStep + direction) * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.tuningStep : .05f) >= PinTuning.MinimumChance - 0.0001f &&
        rightChance + (tuningStep + direction) * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.tuningStep : .05f) <= PinTuning.MaximumChance + 0.0001f &&
        GameManager.Instance.Money >= TuningCost(direction);

    /// <summary>Cobra antes de cambiar la probabilidad; deshacer también cuesta dinero.</summary>
    public bool TryTune(int direction)
    {
        if (!CanTune(direction) || !GameManager.Instance.TrySpendMoney(TuningCost(direction))) return false;
        tuningStep += direction;
        if (tuningPurchases < int.MaxValue) tuningPurchases++;
        board?.NotifyPinUpgrade();
        PlayImpact();
        return true;
    }

    public void SetModifier(PinModifierDefinition modifier)
    {
        Modifier = modifier; batchHits.Clear(); RefreshColor(); PlayImpact(); board?.NotifyPinUpgrade();
    }
    public void ForgetBatch(int batch) => batchHits.Remove(batch);
    /// <summary>Resuelve sinergias por contacto. Cada tanda mantiene su propio contador de visitas.</summary>
    public double ResolveContactBonus(double bonus, int batch, int alternating, int chain)
    {
        if (Modifier == null) return bonus;
        GameManager.Instance.Progression.NotifyModuleUse(Modifier);
        int hits = batchHits.TryGetValue(batch,out int previous) ? previous + 1 : 1;
        batchHits[batch] = hits;
        double amount = Modifier.amount * (1d + Mathf.Abs(RightChance - .5f) * 2f * Modifier.biasSynergy);
        switch (Modifier.effect)
        {
            case PinModifierEffect.AddBonus: return bonus + amount;
            case PinModifierEffect.LuckyBonus: return bonus + (UnityEngine.Random.value < Modifier.chance ? amount : 0);
            case PinModifierEffect.Echo: return bonus * Math.Max(1,amount);
            case PinModifierEffect.Streak: return bonus + (hits % Math.Max(1,Modifier.hitThreshold) == 0 ? amount : 0);
            case PinModifierEffect.Alternating: return bonus + amount * alternating;
            case PinModifierEffect.Chain: return bonus + amount * chain;
            default: return bonus;
        }
    }
    public float LeftChance => 1f - RightChance;
    public int Level => level;
    public double BonusPerLevel => Math.Max(0d, bonusPerLevel);
    public double MultiplierBonus => Math.Max(0d, multiplierBonus) + level * BonusPerLevel;
    public double DisplayMultiplier => 1d + MultiplierBonus;
    public Color CurrentColor
    {
        get {
            if (Modifier != null) return GaltonColors.VisiblePinColor(Modifier.color);
            if (colorPalette != null)
            {
                return colorPalette.ForPin(Level);
            }
            return GaltonColors.VisiblePinColor(GaltonColors.FallbackColor(DisplayMultiplier));
        }
    }

    public double UpgradeCost
    {
        get {
            double firstCost = Math.Max(1f, initialUpgradeCost);
            // Redondear la tasa evita que un float convierta 16 en 16.0000002 y cobre 17.
            double growth = Math.Round(Math.Max(1f, upgradeCostMultiplier), 6);
            return Math.Ceiling(firstCost * Math.Pow(growth, level));
        }
    }

    public bool CanUpgrade => BonusPerLevel > 0d && GameManager.Instance != null && GameManager.Instance.WorkshopUnlocked &&
                              GameManager.Instance.Money >= UpgradeCost &&
                              !double.IsNaN(UpgradeCost) && !double.IsInfinity(UpgradeCost);

    /// <summary>El tablero recibe también los cambios de compras individuales.</summary>
    public void BindBoard(GaltonBoard owner) => board = owner;

    /// <summary>Cobra un solo pin y comunica el nuevo coste a los botones y paneles.</summary>
    public bool TryUpgrade()
    {
        if (!CanUpgrade || !GameManager.Instance.TrySpendMoney(UpgradeCost))
            return false;
        IncreaseLevel();
        board?.NotifyPinUpgrade();
        PinPanelUI.Instance?.Refresh();
        return true;
    }

    /// <summary>Conserva el ajuste efectivo de datos antiguos dentro de los nuevos límites.</summary>
    private void NormalizeTuning()
    {
        float step = BouncyBallySettings.Current != null ? Mathf.Clamp(BouncyBallySettings.Current.tuningStep, .01f, .2f) : PinTuning.StepSize;
        float effective = float.IsNaN(rightChance) || float.IsInfinity(rightChance) ? .5f : Mathf.Clamp(rightChance + tuningStep * step, PinTuning.MinimumChance, PinTuning.MaximumChance);
        rightChance = .5f;
        tuningStep = Mathf.Clamp(Mathf.RoundToInt((effective - .5f) / step), -Mathf.FloorToInt(.20001f / step), Mathf.FloorToInt(.20001f / step));
        tuningPurchases = Math.Max(Math.Abs(tuningStep), tuningPurchases);
    }
    private void Awake()
    {
        NormalizeTuning();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (BouncyBallySettings.Current != null) transform.localScale = Vector3.one * BouncyBallySettings.Current.pinScale;
        restingScale = transform.localScale;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = GaltonTheme.Disc;
        }

        // El collider del prefab y el raycaster de la cámara están configurados en los assets.
        RefreshColor();
    }

    private void OnValidate()
    {
        NormalizeTuning();
        multiplierBonus = Mathf.Max(0f, multiplierBonus);
        bonusPerLevel = Mathf.Max(0f, bonusPerLevel);
        initialUpgradeCost = Mathf.Max(1f, initialUpgradeCost);
        upgradeCostMultiplier = Mathf.Max(1f, upgradeCostMultiplier);
        level = Mathf.Max(0, level);

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        RefreshColor();
    }

    /// <summary>Sortea la dirección según la probabilidad configurada.</summary>
    public bool GoesRight()
    {
        return UnityEngine.Random.value < RightChance;
    }

    /// <summary>Reinicia el pulso visual si llega otra bola durante la animación.</summary>
    public void PlayImpact()
    {
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
        }
        impactRoutine = StartCoroutine(ImpactRoutine());
    }

    /// <summary>Aumenta brevemente tamaño y brillo; después recupera el aspecto normal.</summary>
    private IEnumerator ImpactRoutine()
    {
        float duration = Mathf.Max(0.01f, impactDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            transform.localScale = restingScale * (1f + pulse * (GaltonEffects.ReducedMotion ? 0f : Mathf.Max(.35f, impactGrow)));
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(CurrentColor, Color.white,
                    Mathf.Max(selectedInWorkshop ? .65f : hovered ? hoverBrightness : 0f, pulse * 0.55f));
            }
            yield return null;
        }
        transform.localScale = restingScale;
        RefreshColor();
        impactRoutine = null;
    }

    private void OnDisable()
    {
        hovered = false;
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }
        if (restingScale != Vector3.zero)
        {
            transform.localScale = restingScale;
        }
        RefreshColor();
    }

    /// <summary>Asigna la paleta compartida y actualiza el aspecto.</summary>
    public void SetColorPalette(GaltonColors palette)
    {
        colorPalette = palette;
        RefreshColor();
    }

    /// <summary>Añade un nivel después del pago individual o del pago conjunto de la fila.</summary>
    internal void IncreaseLevel()
    {
        level++;
        RefreshColor();
    }

    /// <summary>Un clic abre la compra individual; cada doble clic compra un nivel.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (GamePresentation.Instance != null)
            {
                bool placed = GamePresentation.Instance.SelectPin(this);
                if (eventData.clickCount == 1) placementClickSequence = placed;
                else placementClickSequence |= placed;
                // Una secuencia de colocación nunca debe comprar niveles por accidente.
                if (GameManager.Instance.WorkshopUnlocked && !placementClickSequence && eventData.clickCount % 2 == 0) TryUpgrade();
            }
            else PinPanelUI.Instance?.Show(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshColor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshColor();
    }

    private void RefreshColor()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(CurrentColor, Color.white, selectedInWorkshop ? .65f : hovered ? hoverBrightness : 0f);
        }
    }
}
