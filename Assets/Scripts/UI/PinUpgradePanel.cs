using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista de compra: la escena contiene una instancia para pins y otra para filas.
/// Todos los controles se asignan desde el Inspector; este script no crea interfaz.
/// </summary>
public class PinUpgradePanel : MonoBehaviour
{

    [Header("Panel")]
    [SerializeField] private UIPanel panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button upgradeButton;

    [Header("Datos del panel")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text bonusText;
    [SerializeField] private TMP_Text chanceText;
    [SerializeField] private TMP_Text nextText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private Image multiplierSwatch;
    [SerializeField] private Image rightBar;
    [SerializeField] private CanvasGroup directionGroup;

    private Pin selectedPin;
    private GaltonBoard selectedBoard;
    private int selectedRow = -1;

    private void Awake()
    {
        closeButton.onClick.AddListener(panel.Hide);
        upgradeButton.onClick.AddListener(UpgradeSelected);
    }

    /// <summary>Abre la compra individual de un pin.</summary>
    public void ShowPin(Pin pin)
    {
        selectedPin = pin;
        selectedBoard = null;
        selectedRow = -1;
        Open();
    }

    /// <summary>Abre la vista de compra de una fila completa.</summary>
    public void ShowRow(GaltonBoard board, int row)
    {
        selectedPin = null;
        selectedBoard = board;
        selectedRow = row;
        Open();
    }

    private void Open()
    {
        panel.Show();
        Refresh();
    }

    public void Refresh()
    {
        if (!panel.IsOpen)
        {
            return;
        }
        if (selectedBoard != null && selectedRow >= 0 && selectedRow < selectedBoard.Rows)
        {
            DrawRow();
        }
        else if (selectedPin != null)
        {
            DrawPin();
        }
    }

    /// <summary>Muestra el siguiente bonus, precio y compra individual del pin seleccionado.</summary>
    private void DrawPin()
    {
        titleText.text = "Pin · level " + selectedPin.Level;
        multiplierText.text = "Bonus  +" + GaltonNumbers.Format(selectedPin.MultiplierBonus);
        bonusText.text = "Added to the ball multiplier";
        chanceText.text = "Left " + (selectedPin.LeftChance * 100f).ToString("0.#") +
                          "%   ·   Right " + (selectedPin.RightChance * 100f).ToString("0.#") + "%";
        multiplierSwatch.color = selectedPin.CurrentColor;
        directionGroup.alpha = 1f;
        rightBar.rectTransform.anchorMin = new Vector2(selectedPin.LeftChance, 0f);
        nextText.text = "Each upgrade adds +" + GaltonNumbers.Format(selectedPin.BonusPerLevel);
        costText.text = "Cost  $" + GaltonNumbers.Format(selectedPin.UpgradeCost);
        upgradeButton.interactable = selectedPin.CanUpgrade;
        upgradeText.text = upgradeButton.interactable ? "Upgrade pin" : "Insufficient funds / unavailable";
    }

    /// <summary>Resume niveles, bonus y precio de todos los pins de la fila.</summary>
    private void DrawRow()
    {
        IReadOnlyList<Pin> row = selectedBoard.GetRow(selectedRow);
        int minimumLevel = int.MaxValue;
        int maximumLevel = 0;
        double minimumBonus = double.MaxValue;
        double maximumBonus = 0d;
        double minimumIncrease = double.MaxValue;
        double maximumIncrease = 0d;
        foreach (Pin pin in row)
        {
            minimumLevel = Mathf.Min(minimumLevel, pin.Level);
            maximumLevel = Mathf.Max(maximumLevel, pin.Level);
            minimumBonus = System.Math.Min(minimumBonus, pin.MultiplierBonus);
            maximumBonus = System.Math.Max(maximumBonus, pin.MultiplierBonus);
            minimumIncrease = System.Math.Min(minimumIncrease, pin.BonusPerLevel);
            maximumIncrease = System.Math.Max(maximumIncrease, pin.BonusPerLevel);
        }

        titleText.text = "Row " + (selectedRow + 1) + " · " + row.Count + " pins";
        multiplierText.text = "Level " + Range(minimumLevel, maximumLevel);
        bonusText.text = "Bonus per pin  +" + Range(minimumBonus, maximumBonus);
        chanceText.text = "One purchase upgrades every pin in this row.";
        nextText.text = "Adds +" + Range(minimumIncrease, maximumIncrease) + " to each pin";
        costText.text = "Total  $" + GaltonNumbers.Format(selectedBoard.GetRowCost(selectedRow));
        multiplierSwatch.color = row[0].CurrentColor;
        directionGroup.alpha = 0f;
        upgradeButton.interactable = selectedBoard.CanUpgradeRow(selectedRow);
        upgradeText.text = upgradeButton.interactable ? "Upgrade row" : "Insufficient funds / unavailable";
    }

    private static string Range(double minimum, double maximum)
    {
        if (System.Math.Abs(minimum - maximum) < 0.000001d)
        {
            return GaltonNumbers.Format(minimum);
        }
        return GaltonNumbers.Format(minimum) + " – " + GaltonNumbers.Format(maximum);
    }

    /// <summary>Solicita una compra individual o de fila y actualiza el panel.</summary>
    private void UpgradeSelected()
    {
        if (selectedPin != null)
        {
            selectedPin.TryUpgrade();
        }
        else if (selectedBoard != null)
        {
            selectedBoard.TryUpgradeRow(selectedRow);
        }
        Refresh();
    }
}
