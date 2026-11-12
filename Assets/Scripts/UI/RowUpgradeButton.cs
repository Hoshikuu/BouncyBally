using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Un clic abre la fila; cada pareja de clics compra exactamente una mejora.</summary>
public class RowUpgradeButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image background;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private TMP_Text costText;
    private GaltonBoard board;
    private PinPanelUI panel;
    private int row;

    public void Setup(GaltonBoard target, PinPanelUI informationPanel, int index)
    {
        board = target;
        panel = informationPanel;
        row = index;
    }

    public void SetVisible(bool value)
    {
        visibility.alpha = value ? 1f : 0f;
        visibility.interactable = value;
        visibility.blocksRaycasts = value;
    }

    public void Refresh()
    {
        costText.text = "+  $" + GaltonNumbers.Format(board.GetRowCost(row));
        background.color = board.CanUpgradeRow(row) ? GaltonTheme.SoftAccent : GaltonTheme.Surface;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || board == null)
            return;
        if (eventData.clickCount % 2 == 0)
            board.TryUpgradeRow(row);
        panel.ShowRow(board, row);
    }
}
