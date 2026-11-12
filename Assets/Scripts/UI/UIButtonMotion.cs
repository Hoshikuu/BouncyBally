using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Respuesta elástica del botón con tiempo no escalado, también en menús.</summary>
public class UIButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private float target = 1f;
    private bool hovered;
    private Vector3 initial;
    private void Awake() => initial = transform.localScale;
    private void Update()
    {
        float scale = GaltonEffects.ReducedMotion ? 1f : target;
        transform.localScale = Vector3.Lerp(transform.localScale, initial * scale, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
    }
    public void OnPointerEnter(PointerEventData e) { hovered = true; target = 1.035f; }
    public void OnPointerExit(PointerEventData e) { hovered = false; target = 1f; }
    public void OnPointerDown(PointerEventData e) { if (GetComponent<Button>().IsInteractable()) target = .94f; }
    public void OnPointerUp(PointerEventData e) => target = hovered ? 1.035f : 1f;
    private void OnDisable() { target = 1f; hovered = false; transform.localScale = initial; }
}
