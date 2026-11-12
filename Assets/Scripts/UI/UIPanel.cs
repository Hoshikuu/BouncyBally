using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ventana persistente: cambia visibilidad e interacción sin activar/desactivar transforms.
/// Las pulsaciones sobre pins, filas y el botón de apertura se resuelven como una sola acción.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Button opener;
    private static UIPanel current;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private Canvas canvas;
    private bool isOpen;
    private int openedFrame;
    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent() => current = null;

    private void Awake()
    {
        if (visibility == null) visibility = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        SetVisible(false);
    }

    private void OnDisable()
    {
        if (current == this) current = null;
        isOpen = false;
    }

    /// <summary>Es idempotente: el segundo clic no reinicia ni reordena la ventana.</summary>
    public void Show()
    {
        if (isOpen) return;
        if (current != null && current != this) current.Hide();
        current = this;
        transform.SetAsLastSibling();
        openedFrame = Time.frameCount;
        SetVisible(true);
    }

    public void Hide()
    {
        if (!isOpen) return;
        SetVisible(false);
        if (current == this) current = null;
        // Un control oculto no debe seguir seleccionado para teclado o mando.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetVisible(bool value)
    {
        isOpen = value;
        visibility.alpha = value ? 1f : 0f;
        visibility.interactable = value;
        visibility.blocksRaycasts = value;
    }

    private void LateUpdate()
    {
        if (!isOpen || Time.frameCount == openedFrame || !TryGetPress(out Vector2 position)) return;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, position, uiCamera)) return;
        if (IsPanelAction(position)) return;
        Hide();
    }

    /// <summary>
    /// Se inspecciona solo el primer resultado: no se atraviesan otros controles de la interfaz.
    /// Abrir/mejorar desde un pin o fila sustituye el panel al recibir el clic, sin ocultación intermedia.
    /// </summary>
    private bool IsPanelAction(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        if (hits.Count == 0 || hits[0].gameObject == null) return false;
        Transform hit = hits[0].gameObject.transform;
        return hit.GetComponentInParent<Pin>() != null || hit.GetComponentInParent<RowUpgradeButton>() != null ||
               (opener != null && (hit == opener.transform || hit.IsChildOf(opener.transform)));
    }

    private static bool TryGetPress(out Vector2 position)
    {
        position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        return false;
    }
}
