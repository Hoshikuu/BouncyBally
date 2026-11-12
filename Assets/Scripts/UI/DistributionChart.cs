using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Últimas 128 llegadas: barras por slot, multiplicador y recuento sobre etiquetas preexistentes.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DistributionChart : MaskableGraphic
{
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private TMP_Text summary;
    private readonly Queue<int> recent = new Queue<int>();
    private readonly int[] counts = new int[GaltonBoard.MaxRows + 1];
    private int slots = 7;
    private GaltonBoard board;
    public void SetBoard(GaltonBoard value)
    {
        board = value;
        if (board != null) SetSlots(board.Rows + 1);
        RefreshLabels(); SetVerticesDirty();
    }
    public void SetSlots(int value)
    {
        int next = Mathf.Clamp(value, 2, counts.Length);
        if (slots != next)
        {
            slots = next; recent.Clear(); System.Array.Clear(counts, 0, counts.Length);
        }
        RefreshLabels(); SetVerticesDirty();
    }
    public void AddResult(int column)
    {
        if (column < 0 || column >= slots) return;
        recent.Enqueue(column); counts[column]++;
        if (recent.Count > 128) counts[recent.Dequeue()]--;
        RefreshLabels(); SetVerticesDirty();
    }
    private void RefreshLabels()
    {
        if (summary != null) summary.text = "LANDINGS BY SLOT · " + recent.Count + "/128 recent balls\nBar height = landing count · Labels = slot multiplier, count and share";
        if (slotLabels == null) return;
        for (int i = 0; i < slotLabels.Length; i++)
        {
            TMP_Text label = slotLabels[i]; if (label == null) continue;
            label.gameObject.SetActive(i < slots);
            if (i >= slots) continue;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(i / (float)slots, 0);
            rect.anchorMax = new Vector2((i + 1f) / slots, 0);
            rect.anchoredPosition = new Vector2(0, -34);
            rect.sizeDelta = new Vector2(-8, 62);
            label.text = "x" + GaltonNumbers.Format(board != null ? board.GetMultiplier(i) : 1) + "\n" + counts[i] + " · " + (recent.Count > 0 ? 100f * counts[i] / recent.Count : 0f).ToString("0.#") + "%";
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect rect = rectTransform.rect;
        int maximum = 1;
        for (int i = 0; i < slots; i++) maximum = Mathf.Max(maximum, counts[i]);
        float width = rect.width / slots;
        for (int i = 0; i < slots; i++)
        {
            float height = Mathf.Max(2f, rect.height * counts[i] / maximum);
            float x = rect.xMin + i * width + 2f; int first = vh.currentVertCount;
            Color tint = board != null && board.Colors != null ? board.Colors.ForSlot(board.GetMultiplier(i)) : GaltonTheme.Accent;
            vh.AddVert(new Vector3(x, rect.yMin), tint, Vector2.zero);
            vh.AddVert(new Vector3(x, rect.yMin + height), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width - 4f, rect.yMin + height), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width - 4f, rect.yMin), tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
