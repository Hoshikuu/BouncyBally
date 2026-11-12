using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>Colores, estilo de botones y dos formas compartidas por la interfaz y el tablero.</summary>
public static class GaltonTheme
{
    public static readonly Color Background = new Color(.035f, .025f, .10f);
    public static readonly Color Surface = new Color(.16f, .105f, .30f);
    public static readonly Color Ink = new Color(0.94f, 0.96f, 0.95f);
    public static readonly Color Muted = new Color(0.69f, 0.76f, 0.74f);
    public static readonly Color Accent = new Color(.25f, .95f, .91f);
    public static readonly Color SoftAccent = new Color(.18f, .38f, .48f);
    public static readonly Color Peach = new Color(.51f, .26f, .16f);
    public static readonly Color Line = new Color(.39f, .29f, .64f);
    public static readonly Color Locked = new Color(.10f, .075f, .18f);

    /// <summary>Unifica los colores de reposo, selección, pulsación y desactivación.</summary>
    public static void StyleButton(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.98f, 0.93f);
        colors.pressedColor = new Color(0.72f, 0.87f, 0.77f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.62f, 0.68f, 0.65f, 0.7f);
        button.colors = colors;
    }
    private static TMP_FontAsset font;
    public static TMP_FontAsset Font => font != null ? font : (font =
        Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset);

    // Las formas se generan una vez y se comparten entre todos los objetos.
    private static Sprite disc;
    private static Sprite card;
    private static Sprite glow;

    /// <summary>Halo radial suave para separar el tablero del fondo.</summary>
    public static Sprite Glow
    {
        get {
            if (glow != null) return glow;
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f));
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            glow = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return glow;
        }
    }

    public static Sprite Disc
    {
        get {
            if (disc == null)
            {
                disc = CreateShape(64, 64, 32f, 64f, Vector4.zero);
            }
            return disc;
        }
    }

    public static Sprite Card
    {
        get {
            if (card == null)
            {
                card = CreateShape(64, 64, 8f, 100f, new Vector4(10f, 10f, 10f, 10f));
            }
            return card;
        }
    }

    /// <summary>Genera una textura pequeña con bordes suaves para reutilizarla como sprite.</summary>
    private static Sprite CreateShape(
        int width, int height, float radius, float pixelsPerUnit, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Galton simple shape";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - width * 0.5f) - (width * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - height * 0.5f) - (height * 0.5f - radius), 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                // Relieve, brillo superior y grano determinista sin imágenes externas.
                float heightLight = .72f + .25f * y / height;
                float grain = ((x * 13 + y * 7) % 9) * .003f;
                float shine = border == Vector4.zero
                    ? Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(width * .34f, height * .7f)) / (width * .65f)), 3f) * .4f
                    : (y > height - 5 ? .14f : 0f);
                float value = Mathf.Clamp01(heightLight + shine + grain);
                pixels[y * width + x] = new Color(value, value, value, alpha * alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture,
                                      new Rect(0f, 0f, width, height),
                                      new Vector2(0.5f, 0.5f),
                                      pixelsPerUnit,
                                      0,
                                      SpriteMeshType.FullRect,
                                      border);
        return sprite;
    }
}
