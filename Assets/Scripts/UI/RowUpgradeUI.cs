using UnityEngine;

/// <summary>Posiciona ocho botones creados en la escena; activa solo las filas existentes.</summary>
public class RowUpgradeUI : MonoBehaviour
{
    [SerializeField] private GaltonBoard board;
    [SerializeField] private PinPanelUI informationPanel;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RowUpgradeButton[] buttons;
    [Min(0f)] [SerializeField] private float rightGap = 76f;
    private GameManager economy;

    private void Start()
    {
        economy = GameManager.Instance;
        for (int row = 0; row < buttons.Length; row++)
            buttons[row].Setup(board, informationPanel, row);
        board.Changed += Refresh;
        if (economy != null)
            economy.MoneyChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (board != null)
            board.Changed -= Refresh;
        if (economy != null)
            economy.MoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        for (int row = 0; row < buttons.Length; row++)
        {
            buttons[row].SetVisible(row < board.Rows);
            if (row < board.Rows)
                buttons[row].Refresh();
        }
    }

    /// <summary>Separa el borde izquierdo del botón del último pin en unidades del Canvas.</summary>
    private void LateUpdate()
    {
        if (worldCamera == null || canvas == null)
            return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransform area = (RectTransform)transform;
        for (int row = 0; row < Mathf.Min(buttons.Length, board.Rows); row++)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(board.GetPinPosition(row, row));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, uiCamera, out Vector2 local);
            RectTransform rect = (RectTransform)buttons[row].transform;
            float halfWidth = rect.rect.width * 0.5f;
            float x = Mathf.Min(local.x + rightGap + halfWidth, area.rect.xMax - halfWidth - 8f);
            rect.anchoredPosition = new Vector2(x, local.y);
        }
    }
}
