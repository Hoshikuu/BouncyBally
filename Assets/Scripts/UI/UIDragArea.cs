using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Arrastra un panel o el contenido de un viewport creado en la escena.
/// Convierte el puntero al espacio local del padre para respetar la escala del Canvas.
/// </summary>
public class UIDragArea : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler,
                          IDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Vector2 graphSizeMultiplier = new Vector2(1.65f, 1.5f);
    private float MinZoom = .22f;
    private float MaxZoom = 2f;
    private Vector2 fixedSize;
    public void Configure(RectTransform graph, RectTransform view, Vector2 size, float min, float max)
    { target = graph; viewport = view; fixedSize = size; MinZoom = min; MaxZoom = max; ResetView(); }
    public void Focus(Vector2 point, float zoom)
    {
        ResetView();
        if (target == null || viewport == null) return;
        targetZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        target.localScale = new Vector3(targetZoom,targetZoom,1);
        Vector2 local = Vector2.Scale(point - target.pivot,target.rect.size);
        Vector3 offset = viewport.TransformPoint(viewport.rect.center) - target.TransformPoint(local);
        target.position += offset; ClampPosition();
    }
    private readonly Vector3[] corners = new Vector3[4];
    private Vector2 previousPointer;
    private Vector2 lastSize;
    private Vector2 velocity;
    private Vector2 zoomPointer;
    private Camera zoomCamera;
    private float targetZoom = 1f;
    private float lastDragTime;
    private int dragPointerId;
    private bool dragging;
    private bool resetPending;

    private void OnEnable()
    {
        if (target != null) targetZoom = target.localScale.x;
    }

    private void OnDisable()
    {
        dragging = false;
        velocity = Vector2.zero;
        if (target != null) targetZoom = target.localScale.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (viewport != null && (resetPending || lastSize != viewport.rect.size))
            ResetView();

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || resetPending) return;
        if (viewport != null && !Mathf.Approximately(target.localScale.x, targetZoom))
        {
            float zoom = Mathf.Lerp(target.localScale.x, targetZoom, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(zoom - targetZoom) < 0.0001f) zoom = targetZoom;
            ApplyZoom(zoom);
        }
        if (!dragging && velocity.sqrMagnitude > 0.01f)
        {
            // Integración exponencial: la distancia no depende de la tasa de fotogramas.
            float decay = Mathf.Exp(-8f * dt);
            target.anchoredPosition += velocity * ((1f - decay) / 8f);
            velocity *= decay;
        }
        else if (dragging && Time.unscaledTime - lastDragTime > 0.08f)
            velocity = Vector2.zero;
        ClampPosition();
    }

    /// <summary>Ajusta y centra todo el contenido al abrir o cambiar de sección.</summary>
    public void ResetView()
    {
        dragging = false;
        velocity = Vector2.zero;
        resetPending = false;
        if (target == null) return;
        targetZoom = target.localScale.x;
        if (viewport == null)
        {
            ClampPosition();
            return;
        }
        // Las vistas ya existen. El layout se mide sin forzar clipping global desde este callback.
        lastSize = viewport.rect.size;
        if (lastSize.x <= 0f || lastSize.y <= 0f)
        {
            resetPending = true;
            return;
        }
        Vector2 size = fixedSize.sqrMagnitude > 0 ? fixedSize : Vector2.Scale(lastSize, new Vector2(
            Mathf.Max(1f, graphSizeMultiplier.x), Mathf.Max(1f, graphSizeMultiplier.y)));
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        target.localScale = Vector3.one;
        Bounds bounds = ContentBounds();
        targetZoom = Mathf.Clamp(Mathf.Min(lastSize.x / Mathf.Max(0.001f, bounds.size.x),
            lastSize.y / Mathf.Max(0.001f, bounds.size.y)), MinZoom, MaxZoom);
        target.localScale = new Vector3(targetZoom, targetZoom, 1f);
        MoveInViewport((Vector3)viewport.rect.center - ContentBounds().center);
        ClampPosition();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = true;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        velocity = Vector2.zero;
        if (target != null) targetZoom = target.localScale.x;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragging || eventData.button != PointerEventData.InputButton.Left || target == null)
            return;
        dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target.parent as RectTransform, eventData.pressPosition, eventData.pressEventCamera,
            out previousPointer);
        if (!dragging) return;
        dragPointerId = eventData.pointerId;
        velocity = Vector2.zero;
        targetZoom = target.localScale.x;
        lastDragTime = Time.unscaledTime;
        // Arrastrar sobre un icono no debe seleccionarlo al soltar el ratón.
        eventData.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != dragPointerId ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target.parent as RectTransform, eventData.position, eventData.pressEventCamera,
                out Vector2 pointer))
            return;
        Vector2 before = target.anchoredPosition;
        target.anchoredPosition += pointer - previousPointer;
        previousPointer = pointer;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
            velocity = Vector2.Lerp(velocity, (target.anchoredPosition - before) / dt,
                1f - Mathf.Exp(-20f * dt));
        lastDragTime = Time.unscaledTime;
        eventData.eligibleForClick = false;
        ClampPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != dragPointerId) return;
        eventData.eligibleForClick = false;
        dragging = false;
        if (Time.unscaledTime - lastDragTime > 0.08f) velocity = Vector2.zero;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (target == null || viewport == null || resetPending || dragging ||
            !RectTransformUtility.RectangleContainsScreenPoint(viewport, eventData.position,
                eventData.enterEventCamera)) return;
        zoomPointer = eventData.position;
        zoomCamera = eventData.enterEventCamera;
        targetZoom = Mathf.Clamp(targetZoom * Mathf.Exp(eventData.scrollDelta.y * 0.12f),
            MinZoom, MaxZoom);
        velocity = Vector2.zero;
    }

    private void ApplyZoom(float zoom)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, zoomPointer,
                zoomCamera, out Vector2 localPoint)) return;
        // Compensa cada paso de escala, sin recentrar ni reiniciar la vista al ampliar.
        Vector3 before = target.TransformPoint(localPoint);
        target.localScale = new Vector3(zoom, zoom, 1f);
        target.position += before - target.TransformPoint(localPoint);
        ClampPosition();
    }

    private Bounds ContentBounds()
    {
        target.GetWorldCorners(corners);
        Bounds bounds = new Bounds(viewport.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            bounds.Encapsulate(viewport.InverseTransformPoint(corners[i]));
        return bounds;
    }

    private void MoveInViewport(Vector3 offset)
    {
        offset.z = 0f;
        target.position += viewport.TransformVector(offset);
    }

    private static float AxisCorrection(float min, float max, float viewMin, float viewMax)
    {
        // El contenido grande cubre el viewport; el pequeño puede moverse dentro de él.
        // Así los límites cambian de forma continua al cruzar el tamaño de ajuste.
        return max - min >= viewMax - viewMin
            ? Mathf.Clamp(0f, viewMax - max, viewMin - min)
            : Mathf.Clamp(0f, viewMin - min, viewMax - max);
    }

    private void ClampPosition()
    {
        if (target == null)
            return;
        Vector2 before = target.anchoredPosition;
        Vector2 position = before;
        if (viewport != null)
        {
            Bounds bounds = ContentBounds();
            Rect view = viewport.rect;
            MoveInViewport(new Vector3(AxisCorrection(bounds.min.x, bounds.max.x, view.xMin, view.xMax),
                AxisCorrection(bounds.min.y, bounds.max.y, view.yMin, view.yMax), 0f));
        }
        else if (target.parent is RectTransform parent)
        {
            position.x = Mathf.Clamp(position.x, Mathf.Min(0f, target.rect.width - parent.rect.width), 0f);
            position.y = Mathf.Clamp(position.y, Mathf.Min(0f, target.rect.height - parent.rect.height), 0f);
        }
        if (viewport == null) target.anchoredPosition = position;
        Vector2 correction = target.anchoredPosition - before;
        if (Mathf.Abs(correction.x) > 0.001f) velocity.x = 0f;
        if (Mathf.Abs(correction.y) > 0.001f) velocity.y = 0f;
    }
}
