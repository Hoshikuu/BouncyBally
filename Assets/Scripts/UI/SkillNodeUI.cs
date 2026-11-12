using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Nodo de escena: valida referencias sin crear ni reparar objetos en Play.</summary>
public class SkillNodeUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private SkillDefinition skill;
    [SerializeField] private Button button;
    [SerializeField] private Image background, icon;
    [SerializeField] private TMP_Text statusText, nameText, symbolText;
    private SkillTreeController owner;
    public SkillDefinition Skill => skill;
    public RectTransform Rect => transform as RectTransform;

    private bool valid;
    public bool HasSceneReferences => skill != null && button != null && background != null && icon != null &&
        symbolText != null && statusText != null && GetComponent<CanvasRenderer>() != null;
    public void Configure(SkillDefinition definition, SkillTreeController controller)
    {
        // Solo enlaza datos; la vista y todos sus componentes ya existen en la escena.
        skill = definition; Bind(controller);
    }
    public void Bind(SkillTreeController controller)
    {
        valid = HasSceneReferences;
        if (!valid) { Debug.LogError("Skill node has missing scene references.", this); return; }
        owner = controller;
        button.onClick.RemoveListener(Select);
        button.onClick.AddListener(Select);
        symbolText.text = Symbol(skill.Effect);
        if (skill.Icon != null) icon.sprite = skill.Icon;
        symbolText.gameObject.SetActive(skill.Icon == null);
        if (nameText != null) nameText.gameObject.SetActive(false);
        icon.color = skill.SectionBall != null ? skill.SectionBall.DisplayColor : GaltonTheme.Accent;
        GaltonTheme.StyleButton(button);
    }
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
    private void Select() { if (owner != null && skill != null) owner.Select(skill); }
    /// <summary>Un clic selecciona; cada pareja de clics válida realiza como máximo una compra.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || !eventData.eligibleForClick || owner == null) return;
        owner.Select(skill);
        if (eventData.clickCount > 0 && eventData.clickCount % 2 == 0) owner.Buy(skill);
    }
    public void Refresh(SkillTreeController controller, bool selected)
    {
        if (!valid || !HasSceneReferences || controller == null) return;
        bool unlocked = controller.GetLevel(skill) > 0 || controller.MeetsRequirement(skill);
        bool complete = controller.IsComplete(skill);
        bool affordable = controller.CanBuy(skill);
        string state = complete ? "MAX" : !unlocked ? "LOCKED" : affordable ? "READY" : "NEED $";
        statusText.text = (selected ? "• " : "") + state + " " + controller.GetLevel(skill) + "/" + skill.MaxLevel;
        background.color = complete ? new Color(.22f, .25f, .62f) : !unlocked ? new Color(.19f, .19f, .24f) :
            affordable ? new Color(.05f, .48f, .30f) : new Color(.55f, .20f, .14f);
        statusText.color = Color.white;
        symbolText.color = unlocked ? GaltonTheme.Ink : GaltonTheme.Muted;
    }
    public static string Symbol(SkillEffect effect)
    {
        switch (effect)
        {
            case SkillEffect.BallValue: return "$";
            case SkillEffect.BallCount: return "+";
            case SkillEffect.BallSpeed: return ">>";
            case SkillEffect.Cooldown: return "T";
            case SkillEffect.PinBonus: return "P+";
            case SkillEffect.SlotBonus: return "S+";
            case SkillEffect.RewardMultiplier: return "x";
            case SkillEffect.AutoLaunch: return "A";
            case SkillEffect.UnlockBall: return "B";
            case SkillEffect.BoardRows: return "R+";
            case SkillEffect.SlotMultiplier: return "2x";
            case SkillEffect.PinTweaker: return "L/R";
            case SkillEffect.UnlockBatch: return "B+";
            case SkillEffect.Workshop: return "W";
            default: return "+";
        }
    }
}
