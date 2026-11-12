using UnityEngine.Serialization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Taller conectado exclusivamente a los componentes ya guardados en la escena.</summary>
public class PinWorkshopUI : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private GamePresentation presentation;
    [SerializeField] private PinModuleInventory inventory;
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private TMP_Text leftText;
    [SerializeField] private TMP_Text rightText;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private Button left;
    [SerializeField] private Button right;
    [SerializeField] private Button upgrade;
    [SerializeField] private Button closeButton;
    [FormerlySerializedAs("capsulesButton")] [SerializeField] private Button gachaButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button cancelButton;

    [Serializable]
    public sealed class ModuleRow
    {
        public PinModifierDefinition modifier;
        public Button button;
        public Image preview;
        public TMP_Text label;
    }

    [SerializeField] private ModuleRow[] moduleRows;
    [SerializeField] private TMP_Text emptyText;

    private Pin selected;
    private PinModifierDefinition armed;
    private bool placing;
    private bool initialized;
    private GameManager subscribedGame;
    private GaltonBoard subscribedBoard;
    private PinModuleInventory subscribedInventory;
    private Action refreshCallback;
    private Button[] listenerButtons;
    private UnityAction[] listenerCallbacks;

    public bool IsOpen => root != null && root.activeSelf;

    /// <summary>Valida todo antes de enlazar; repetir la llamada no duplica suscripciones.</summary>
    public bool Initialize()
    {
        if (initialized) return true;
        if (!HasValidReferences()) return false;

        subscribedGame = game;
        subscribedBoard = game.Board;
        subscribedInventory = inventory;
        refreshCallback = Refresh;
        listenerButtons = new Button[7 + moduleRows.Length];
        listenerCallbacks = new UnityAction[listenerButtons.Length];
        CacheListener(0, left, TuneLeft);
        CacheListener(1, right, TuneRight);
        CacheListener(2, upgrade, UpgradeSelected);
        CacheListener(3, closeButton, Close);
        // El delegado pertenece al taller, nunca a otro componente que pueda destruirse antes.
        CacheListener(4, gachaButton, OpenGacha);
        CacheListener(5, removeButton, ArmRemoval);
        CacheListener(6, cancelButton, CancelPlacement);
        for (int i = 0; i < moduleRows.Length; i++)
        {
            PinModifierDefinition modifier = moduleRows[i].modifier;
            CacheListener(7 + i, moduleRows[i].button, () => Arm(modifier));
            moduleRows[i].button.gameObject.SetActive(false);
        }

        for (int i = 0; i < listenerButtons.Length; i++)
            listenerButtons[i].onClick.AddListener(listenerCallbacks[i]);
        subscribedGame.MoneyChanged += refreshCallback;
        subscribedGame.ProgressChanged += refreshCallback;
        subscribedBoard.Changed += refreshCallback;
        subscribedInventory.Changed += refreshCallback;
        initialized = true;
        Close();
        Refresh();
        return true;
    }

    private bool HasValidReferences()
    {
        if (game == null || game.Board == null || presentation == null || inventory == null ||
            root == null || detail == null || leftText == null || rightText == null ||
            upgradeText == null || emptyText == null || left == null || right == null ||
            upgrade == null || closeButton == null || gachaButton == null ||
            removeButton == null || cancelButton == null || moduleRows == null || moduleRows.Length == 0)
            return false;

        Button[] controls = { left, right, upgrade, closeButton, gachaButton, removeButton, cancelButton };
        for (int i = 0; i < controls.Length; i++)
            for (int j = 0; j < i; j++)
                if (controls[i] == controls[j]) return false;
        if (detail == leftText || detail == rightText || detail == upgradeText || detail == emptyText ||
            leftText == rightText || leftText == upgradeText || leftText == emptyText ||
            rightText == upgradeText || rightText == emptyText || upgradeText == emptyText)
            return false;

        for (int i = 0; i < moduleRows.Length; i++)
        {
            ModuleRow row = moduleRows[i];
            if (row == null || row.modifier == null || row.button == null ||
                row.preview == null || row.label == null) return false;
            foreach (Button control in controls)
                if (row.button == control) return false;
            if (row.label == detail || row.label == leftText || row.label == rightText ||
                row.label == upgradeText || row.label == emptyText) return false;
            for (int j = 0; j < i; j++)
            {
                ModuleRow previous = moduleRows[j];
                if (row.modifier == previous.modifier || row.modifier.id == previous.modifier.id ||
                    row.button == previous.button || row.preview == previous.preview || row.label == previous.label)
                    return false;
            }
        }
        return true;
    }

    private void CacheListener(int index, Button button, UnityAction callback)
    {
        listenerButtons[index] = button;
        listenerCallbacks[index] = callback;
    }

    private bool GateWorkshop() => initialized && game != null && inventory != null && game.WorkshopUnlocked;

    public void Open()
    {
        if (!Initialize() || !GateWorkshop()) return;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        // OnPointerExit borra también el brillo de hover aunque el panel tape el pin.
        if (selected != null)
        {
            selected.SetSelected(false);
            selected.OnPointerExit(null);
        }
        selected = null;
        CancelPlacement();
        if (root != null) root.SetActive(false);
    }

    public void CancelPlacement()
    {
        placing = false;
        armed = null;
        if (initialized) RefreshDetail();
    }

    private void Arm(PinModifierDefinition modifier)
    {
        if (!GateWorkshop() || !IsOpen || modifier == null || inventory.Available(modifier) <= 0) return;
        armed = modifier;
        placing = true;
        RefreshDetail();
    }

    private void ArmRemoval()
    {
        if (!GateWorkshop() || !IsOpen) return;
        armed = null;
        placing = true;
        RefreshDetail();
    }

    private void OpenGacha()
    {
        if (GateWorkshop() && IsOpen && presentation != null) presentation.OpenGacha();
    }

    public bool Select(Pin pin)
    {
        if (!GateWorkshop() || !IsOpen || pin == null) return false;
        // Se consume el clic de colocación incluso si Equip rechaza la operación.
        // Pin usa este resultado para impedir mejoras accidentales durante el doble clic.
        bool placementAction = placing;
        if (selected != null) selected.SetSelected(false);
        selected = pin;
        selected.SetSelected(true);
        if (placementAction)
        {
            PinModifierDefinition modifier = armed;
            CancelPlacement();
            inventory.Equip(pin, modifier);
        }
        Refresh();
        return placementAction;
    }

    private void UpgradeSelected()
    {
        if (!GateWorkshop() || !IsOpen || selected == null) return;
        selected.TryUpgrade();
        Refresh();
    }

    private void TuneLeft() => Tune(-1);
    private void TuneRight() => Tune(1);

    private void Tune(int direction)
    {
        if (!GateWorkshop() || !IsOpen || selected == null) return;
        selected.TryTune(direction);
        Refresh();
    }

    public void Refresh()
    {
        if (!initialized || root == null) return;
        bool unlocked = GateWorkshop();
        if (!unlocked) Close();
        RefreshInventory(unlocked);
        RefreshDetail();
        removeButton.interactable = unlocked;
        cancelButton.interactable = unlocked && placing;
        gachaButton.gameObject.SetActive(game.Settings.gachas != null && game.Settings.gachas.Length > 0);
        gachaButton.interactable = unlocked && presentation != null;
    }

    private void RefreshInventory(bool unlocked)
    {
        bool any = false;
        int visible = 0;
        RectTransform content = null;
        foreach (ModuleRow row in moduleRows)
        {
            // Las filas conservan sus componentes de escena; solo se compacta su posición.
            bool owned = false;
            if (inventory != null && row.modifier != null)
                foreach (PinModifierDefinition modifier in inventory.OwnedModules)
                    if (modifier == row.modifier) { owned = true; break; }
            row.button.gameObject.SetActive(owned);
            row.button.interactable = owned && unlocked && inventory.Available(row.modifier) > 0;
            if (!owned) continue;
            any = true;
            RectTransform rect = row.button.transform as RectTransform;
            if (rect != null) { rect.anchoredPosition = new Vector2(0f,-74f*visible++); content = rect.parent as RectTransform; }
            row.label.text = row.modifier.symbol + "  " + row.modifier.title +
                (inventory.Available(row.modifier) > 0 ? "" : " - USED");
            row.preview.sprite = row.modifier.icon;
            row.preview.preserveAspect = true;
            row.preview.enabled = row.modifier.icon != null;
            if (row.button.image != null)
                row.button.image.color = Color.Lerp(GaltonTheme.Surface, row.modifier.color, .4f);
        }
        if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x,Mathf.Max(80f,visible*74f));
        emptyText.text = "OPEN A GACHA";
        emptyText.gameObject.SetActive(!any);
    }

    private void RefreshDetail()
    {
        if (!initialized) return;
        bool unlocked = GateWorkshop();
        bool has = selected != null;
        detail.text = placing ? (armed != null ? armed.description : "SELECT A PIN TO REMOVE MODULE") :
            has ? "L " + (selected.LeftChance * 100).ToString("0") + "%   R " +
                (selected.RightChance * 100).ToString("0") + "%\n+" +
                GaltonNumbers.Format(selected.MultiplierBonus) +
                (selected.Modifier != null ? " - " + selected.Modifier.title : "") : "SELECT A PIN";
        upgrade.interactable = unlocked && has && selected.CanUpgrade;
        upgradeText.text = has ? "UPGRADE $" + GaltonNumbers.Format(selected.UpgradeCost) : "UPGRADE";
        bool tuningUnlocked = unlocked && game.PinTweakerUnlocked;
        left.interactable = tuningUnlocked && has && selected.CanTune(-1);
        right.interactable = tuningUnlocked && has && selected.CanTune(1);
        leftText.text = "LEFT\n" + (!tuningUnlocked ? "LOCKED" : has ? Cost(-1) : "");
        rightText.text = "RIGHT\n" + (!tuningUnlocked ? "LOCKED" : has ? Cost(1) : "");
        cancelButton.interactable = unlocked && placing;
    }

    private string Cost(int step)
    {
        double value = selected.TuningCost(step);
        return double.IsInfinity(value) ? "MAX" : "$" + GaltonNumbers.Format(value);
    }

    private void OnDestroy()
    {
        if (!initialized) return;
        initialized = false;
        // Solo se retiran los delegados guardados, sin consultar singletons ni formar callbacks de pares.
        if (subscribedInventory != null) subscribedInventory.Changed -= refreshCallback;
        if (subscribedGame != null)
        {
            subscribedGame.MoneyChanged -= refreshCallback;
            subscribedGame.ProgressChanged -= refreshCallback;
        }
        if (subscribedBoard != null) subscribedBoard.Changed -= refreshCallback;
        for (int i = 0; i < listenerButtons.Length; i++)
            if (listenerButtons[i] != null) listenerButtons[i].onClick.RemoveListener(listenerCallbacks[i]);
        Close();
    }
}
