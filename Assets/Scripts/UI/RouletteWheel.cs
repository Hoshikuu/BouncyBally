using UnityEngine;
using UnityEngine.UI;

/// <summary>Diez sectores iguales, con el mismo color que sus etiquetas en el Canvas.</summary>
public class RouletteWheel : MaskableGraphic
{
    public static readonly int[] Multipliers = { 0, 1, 5, 0, 1, 10, 1, 0, 5, 1 };
    public static Color SectorColor(int multiplier)
    {
        switch (multiplier)
        {
            case 0: return new Color(.8f, .16f, .22f);
            case 1: return new Color(.16f, .105f, .30f);
            case 5: return new Color(.06f, .48f, .65f);
            default: return new Color(.12f, .65f, .32f);
        }
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        for (int sector = 0; sector < 10; sector++)
        {
            Color tint = SectorColor(Multipliers[sector]);
            for (int step = 0; step < 12; step++)
            {
                float a = (sector * 36 - 18 + step * 3) * Mathf.Deg2Rad;
                float b = a + 3 * Mathf.Deg2Rad;
                int start = mesh.currentVertCount;
                mesh.AddVert(rect.center, tint, Vector2.zero);
                mesh.AddVert(rect.center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius, tint, Vector2.zero);
                mesh.AddVert(rect.center + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * radius, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }
        }
    }
}
