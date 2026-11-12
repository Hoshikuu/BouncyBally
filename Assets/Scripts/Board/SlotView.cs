using TMPro;
using UnityEngine;

/// <summary>Muestra el multiplicador del slot y ajusta su placa al ancho del tablero.</summary>
public class SlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text multiplierText;

    /// <summary>Actualiza el texto, el color y el ancho del slot.</summary>
    public void Setup(double multiplier, GaltonColors colors, float width, float height)
    {
        multiplierText.font = GaltonTheme.Font;
        multiplierText.fontStyle = FontStyles.Bold;
        // Ajusta el texto al ancho de la placa sin partir el multiplicador.
        float localWidth = width / Mathf.Max(0.01f, transform.localScale.x);
        float localHeight = height / Mathf.Max(0.01f, transform.localScale.y);
        multiplierText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, localWidth * 0.92f);
        multiplierText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, localHeight * 0.85f);
        multiplierText.textWrappingMode = TextWrappingModes.NoWrap;
        multiplierText.enableAutoSizing = true;
        multiplierText.fontSizeMin = 1f;
        multiplierText.fontSizeMax = 5f;
        multiplierText.text = "x" + GaltonNumbers.Format(multiplier);
        Color accent =
            colors != null ? colors.ForSlot(multiplier) : GaltonColors.FallbackColor(multiplier);
        Color plateColor = Color.Lerp(GaltonTheme.Surface, accent, .75f);
        float luminance = plateColor.r * .2126f + plateColor.g * .7152f + plateColor.b * .0722f;
        multiplierText.color = luminance > .48f ? new Color(.035f,.025f,.10f) : GaltonTheme.Ink;
        SpriteRenderer plate = GetComponent<SpriteRenderer>();
        if (plate != null)
        {
            plate.sprite = GaltonTheme.Card;
            plate.drawMode = SpriteDrawMode.Sliced;
            plate.size = new Vector2(localWidth, localHeight);
            plate.color = plateColor;
        }
    }
}
