using UnityEngine;

/// <summary>Conecta los dos paneles independientes de la escena con el tablero y la economía.</summary>
public class PinPanelUI : MonoBehaviour
{
    public static PinPanelUI Instance { get; private set; }
    [SerializeField] private PinUpgradePanel individualPanel;
    [SerializeField] private PinUpgradePanel rowPanel;
    private GameManager economy;

    private void Awake() => Instance = this;

    private void Start()
    {
        economy = GameManager.Instance;
        if (economy == null)
            return;
        economy.MoneyChanged += Refresh;
        if (economy.Board != null)
            economy.Board.Changed += Refresh;
    }

    private void OnDestroy()
    {
        if (economy != null)
        {
            economy.MoneyChanged -= Refresh;
            if (economy.Board != null)
                economy.Board.Changed -= Refresh;
        }
        if (Instance == this)
            Instance = null;
    }

    public void Show(Pin pin) => individualPanel.ShowPin(pin);
    public void ShowRow(GaltonBoard board, int row) => rowPanel.ShowRow(board, row);

    public void Refresh()
    {
        individualPanel.Refresh();
        rowPanel.Refresh();
    }
}
