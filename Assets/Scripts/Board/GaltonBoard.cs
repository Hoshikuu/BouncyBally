using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
/// <summary>Genera el tablero, mejora filas completas y aplica expansiones entre tandas.</summary>
public class GaltonBoard : MonoBehaviour
{
    public const int MaxRows = 8;

    [Header("Tablero")]
    [Range(1, MaxRows)] [SerializeField] private int rows = 6;
    [Min(0.1f)] [SerializeField] private float horizontalSpacing = 1f;
    [Min(0.1f)] [SerializeField] private float verticalSpacing = 0.85f;
    [SerializeField] private float topY = 3f;
    [SerializeField] private bool centerOnCamera = true;

    [Header("Prefabs")]
    [SerializeField] private Pin pinPrefab;
    [Tooltip("Nueve slots creados en la escena; los sobrantes permanecen ocultos.")]
    [SerializeField] private SlotView[] slotViews;

    [Header("Valores iniciales de slots; la expansión conserva el patrón")]
    [SerializeField] private int[] multipliers = { 8, 4, 2, 1, 2, 4, 8 };
    [SerializeField] private GaltonColors colorPalette;

    private readonly List<Pin[]> pins = new List<Pin[]>();
    private int initialRows;
    private int requestedRows;
    private int slotUpgradeLevel;
    private float spacingX;
    private float spacingY;
    private float layoutTop;
    private Vector3 layoutCenter;
    private float lastAspect;
    private float lastCameraSize;

    public int Rows => rows;
    public int RequestedRows => requestedRows;
    public int SlotUpgradeLevel => slotUpgradeLevel;
    public GaltonColors Colors => colorPalette;
    public bool CanExpand => requestedRows < MaxRows;
    public event Action Changed;

    private void Awake()
    {
        rows = BouncyBallySettings.Current != null ? BouncyBallySettings.Current.startingRows : rows;
        rows = Mathf.Clamp(rows, 1, MaxRows);
        initialRows = rows;
        requestedRows = rows;
    }

    private void Start()
    {
        UpdateLayout();
        AddMissingRows();
        RefreshSlots();

    }

    /// <summary>Solicita una fila nueva; no mueve el tablero mientras hay bolas activas.</summary>
    public void RequestExpansion()
    {
        if (!CanExpand)
        {
            return;
        }
        requestedRows++;
        Changed?.Invoke();
    }

    // Se ejecuta cuando terminan todas las bolas. Conserva los pins y sus niveles.
    /// <summary>Aplica las filas pendientes y adapta el tablero a la cámara al empezar una tanda.</summary>
    public void ApplyPendingExpansion()
    {
        Camera camera = Camera.main;
        bool viewChanged =
            camera != null && (camera.aspect != lastAspect || camera.orthographicSize != lastCameraSize);
        if (rows == requestedRows && !viewChanged)
        {
            return;
        }
        rows = requestedRows;
        UpdateLayout();
        AddMissingRows();
        for (int row = 0; row < pins.Count; row++)
        {
            for (int column = 0; column < pins[row].Length; column++)
            {
                pins[row][column].transform.position = GetPinPosition(row, column);
            }
        }
        RefreshSlots();
        Changed?.Invoke();
    }

    /// <summary>Crea únicamente filas nuevas para conservar las mejoras existentes.</summary>
    private void AddMissingRows()
    {
        while (pins.Count < rows)
        {
            int row = pins.Count;
            Pin[] layer = new Pin[row + 1];
            for (int column = 0; column < layer.Length; column++)
            {
                Pin pin = Instantiate(pinPrefab, GetPinPosition(row, column), Quaternion.identity, transform);
                pin.BindBoard(this);
                pin.SetColorPalette(colorPalette);
                layer[column] = pin;
            }
            pins.Add(layer);
        }
    }

    /// <summary>Calcula separación y centrado dentro de la cámara ortográfica.</summary>
    private void UpdateLayout()
    {
        spacingX = horizontalSpacing;
        spacingY = verticalSpacing;
        layoutTop = topY;
        layoutCenter = transform.position;
        Camera boardCamera = Camera.main;
        if (!centerOnCamera || boardCamera == null || !boardCamera.orthographic)
        {
            return;
        }

        lastAspect = boardCamera.aspect;
        lastCameraSize = boardCamera.orthographicSize;
        float viewHeight = boardCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * boardCamera.aspect;
        float boardHeight = rows * verticalSpacing + 1.6f;
        float boardWidth = rows * horizontalSpacing + 4.4f;
        float fit = Mathf.Min(1.35f, viewWidth * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.boardWidth : .78f) / boardWidth, viewHeight * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.boardHeight : .84f) / boardHeight);
        spacingX *= fit;
        spacingY *= fit;
        layoutCenter = new Vector3(
            boardCamera.transform.position.x, boardCamera.transform.position.y, transform.position.z);
        layoutTop = (rows * spacingY - 0.7f) * 0.5f;
    }

    /// <summary>Reinicia el tablero únicamente cuando el spawner ya no tiene bolas activas.</summary>
    public void ResetRun()
    {
        foreach (Pin[] row in pins) foreach (Pin pin in row) if (pin != null) Destroy(pin.gameObject);
        pins.Clear(); rows = initialRows; requestedRows = rows; slotUpgradeLevel = 0;
        UpdateLayout(); AddMissingRows(); RefreshSlots(); Changed?.Invoke();
    }
    public void EndBatch(int id)
    { foreach (Pin[] row in pins) foreach (Pin pin in row) pin.ForgetBatch(id); }

    public Pin GetPin(int row, int column)
    {
        return pins[row][column];
    }

    public IReadOnlyList<Pin> GetRow(int row)
    {
        return pins[row];
    }

    /// <summary>Informa de compras individuales para actualizar el precio total de su fila.</summary>
    internal void NotifyPinUpgrade() => Changed?.Invoke();

    /// <summary>Suma el precio individual de todos los pins de una fila.</summary>
    public double GetRowCost(int row)
    {
        if (row < 0 || row >= pins.Count)
            return double.PositiveInfinity;
        double total = 0d;
        foreach (Pin pin in pins[row])
        {
            total += pin.UpgradeCost;
        }
        return total;
    }

    /// <summary>Comprueba que todos los pins sean mejorables y que alcance el dinero.</summary>
    public bool CanUpgradeRow(int row)
    {
        if (row < 0 || row >= pins.Count)
        {
            return false;
        }
        foreach (Pin pin in pins[row])
        {
            if (pin.BonusPerLevel <= 0d)
            {
                return false;
            }
        }
        double cost = GetRowCost(row);
        return GameManager.Instance != null && !double.IsInfinity(cost) && !double.IsNaN(cost) &&
               GameManager.Instance.Money >= cost;
    }

    /// <summary>Cobra una vez y sube un nivel a todos los pins de la fila.</summary>
    public bool TryUpgradeRow(int row)
    {
        if (!CanUpgradeRow(row))
        {
            return false;
        }
        if (!GameManager.Instance.TrySpendMoney(GetRowCost(row)))
        {
            return false;
        }
        foreach (Pin pin in pins[row])
        {
            pin.IncreaseLevel();
        }
        Changed?.Invoke();
        PinPanelUI.Instance?.Refresh();
        return true;
    }

    /// <summary>Duplica todos los multiplicadores y actualiza sus etiquetas.</summary>
    public void UpgradeSlots()
    {
        slotUpgradeLevel++;
        RefreshSlots();
        Changed?.Invoke();
    }

    /// <summary>Activa los slots de la escena y actualiza posición, valor y color.</summary>
    private void RefreshSlots()
    {
        for (int column = 0; column < slotViews.Length; column++)
        {
            bool visible = column <= rows;
            slotViews[column].gameObject.SetActive(visible);
            if (!visible)
                continue;
            slotViews[column].transform.position = GetSlotPosition(column);
            slotViews[column].Setup(GetMultiplier(column), colorPalette, spacingX * 0.88f);
        }
    }

    public Vector3 GetPinPosition(int row, int column)
    {
        float x = (column - row * 0.5f) * spacingX;
        float y = layoutTop - row * spacingY;
        return layoutCenter + new Vector3(x, y, 0f);
    }

    public Vector3 GetSlotPosition(int column)
    {
        float x = (column - rows * 0.5f) * spacingX;
        return layoutCenter + new Vector3(x, layoutTop - rows * spacingY, 0f);
    }

    public Vector3 GetSpawnPosition()
    {
        return GetPinPosition(0, 0) + Vector3.up * 0.9f;
    }

    /// <summary>Combina el valor del tablero con las duplicaciones compradas.</summary>
    public double GetMultiplier(int column)
    {
        if (column < 0 || column > rows)
        {
            return 1d;
        }
        double baseValue = BaseSlotMultiplier(column);
        return baseValue * Math.Pow(2d, slotUpgradeLevel);
    }
    // La expansión conserva la orientación de valores: bordes fuertes o centro fuerte.
    private double BaseSlotMultiplier(int column)
    {
        int distanceFromEdge = Math.Min(column, rows - column);
        if (multipliers == null || multipliers.Length == 0)
        {
            return Math.Pow(2d, distanceFromEdge);
        }
        if (rows == initialRows && column < multipliers.Length)
        {
            return Math.Max(1, multipliers[column]);
        }

        // Cada nueva fila duplica el valor exterior y mantiene el perfil simétrico en potencias de dos.
        double outerValue = Math.Max(1, multipliers[0]);
        double rowGrowth = Math.Pow(2d, Math.Max(0, rows - initialRows));
        double step = Math.Pow(2d, distanceFromEdge);
        int center = multipliers.Length / 2;
        bool edgesAreStronger = multipliers[0] >= multipliers[center];
        if (edgesAreStronger)
        {
            return Math.Max(1d, outerValue * rowGrowth / step);
        }
        return outerValue * rowGrowth * step;
    }

}
