using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Halo, destello y estela arcade sin coroutines ni cambios de recompensa.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BallVisualEffects : MonoBehaviour
{
    [Header("Glow")]
    [Min(0f)] [SerializeField] private float lightIntensity = 0.25f;
    [Min(0.05f)] [SerializeField] private float lightRadius = 1.9f;
    [Min(1f)] [SerializeField] private float haloScale = 1.2f;
    [Range(0f, 1f)] [SerializeField] private float haloOpacity = 0.35f;

    [Header("Trail")]
    [Min(0.05f)] [SerializeField] private float trailDuration = 0.14f;
    [Min(0.01f)] [SerializeField] private float trailWidth = 0.12f;

    // Solo una fracción de las 250 bolas usa luces URP; todas conservan halo y estela.
    private const int MaxActiveLights = 24;
    private static int activeLights;
    private bool ownsLight;
    private bool motionWasReduced;
    private BallBase stats;
    private Light2D glowLight;
    private SpriteRenderer halo;
    private TrailRenderer trail;
    private readonly Gradient trailFade = new Gradient();
    private readonly GradientColorKey[] trailColorKeys = new GradientColorKey[3];
    private static readonly GradientAlphaKey[] TrailAlphaKeys = {
        new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f)
    };
    private Color currentColor = Color.white;
    private float currentIntensity, valueProgress, baseTrailWidth, bounceRemaining;
    private const float BounceDuration = 0.18f;

    private void Awake()
    {
        stats = GetComponent<BallBase>();
        SpriteRenderer body = GetComponent<SpriteRenderer>();
        body.sprite = GaltonTheme.Disc;
        currentColor = body.color;
        GameObject glow = new GameObject("Ball outline");
        glow.transform.SetParent(transform, false);
        halo = glow.AddComponent<SpriteRenderer>();
        halo.sprite = body.sprite;
        halo.sharedMaterial = body.sharedMaterial;
        halo.sortingLayerID = body.sortingLayerID;
        halo.sortingOrder = body.sortingOrder - 1;
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = Mathf.Clamp(trailDuration, 0.05f, 0.25f);
        trail.minVertexDistance = 0.08f;
        trail.numCornerVertices = 0;
        trail.numCapVertices = 0;
        trail.alignment = LineAlignment.View;
        trail.sortingLayerID = body.sortingLayerID;
        trail.sortingOrder = body.sortingOrder - 1;
        // Comparte el material del sprite para evitar un material por bola.
        trail.sharedMaterial = body.sharedMaterial;
        if (stats != null) stats.VisualChanged += UpdateVisuals;
        UpdateVisuals(currentColor, currentColor, stats != null ? stats.VisualValue : 0d);
    }

    private void OnEnable()
    {
        TryAcquireLight();
        if (trail != null) { trail.Clear(); trail.emitting = true; }
    }

    private void OnDisable()
    {
        bounceRemaining = 0f;
        ReleaseLight();
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        if (halo != null) ApplyPulse(0f);
    }

    private void OnDestroy()
    {
        ReleaseLight();
        if (stats != null) stats.VisualChanged -= UpdateVisuals;
    }

    private void TryAcquireLight()
    {
        if (ownsLight || activeLights >= MaxActiveLights || !isActiveAndEnabled) return;
        if (glowLight == null)
        {
            glowLight = gameObject.AddComponent<Light2D>();
            glowLight.lightType = Light2D.LightType.Point;
        }
        ownsLight = true;
        activeLights++;
        glowLight.pointLightOuterRadius = Mathf.Clamp(lightRadius, 0.05f, 2f);
        glowLight.color = currentColor;
        glowLight.intensity = currentIntensity;
        glowLight.enabled = true;
    }

    private void ReleaseLight()
    {
        if (!ownsLight) return;
        ownsLight = false;
        activeLights = Mathf.Max(0, activeLights - 1);
        if (glowLight != null) glowLight.enabled = false;
    }

    /// <summary>El valor solo regula la presentación; las estadísticas se conservan intactas.</summary>
    private void UpdateVisuals(Color ballColor, Color trailColor, double visualValue)
    {
        currentColor = ballColor;
        valueProgress = Mathf.Clamp01((float)System.Math.Log10(System.Math.Max(0d,
            System.Math.Min(visualValue, 10000d)) + 1d) / 2f);
        currentIntensity = Mathf.Clamp(lightIntensity * (1f + valueProgress), 0f, 0.8f);
        baseTrailWidth = Mathf.Clamp(trailWidth * (1f + valueProgress * 0.85f), 0.01f, 0.28f);
        if (glowLight != null) glowLight.color = ballColor;
        trailColorKeys[0] = new GradientColorKey(Color.Lerp(trailColor, Color.white, 0.6f), 0f);
        trailColorKeys[1] = new GradientColorKey(trailColor, 0.3f);
        trailColorKeys[2] = new GradientColorKey(Color.Lerp(trailColor, GaltonTheme.Accent, 0.3f), 1f);
        trailFade.SetKeys(trailColorKeys, TrailAlphaKeys);
        trail.colorGradient = trailFade;
        ApplyPulse(GaltonEffects.ReducedMotion ? 0f : BouncePulse());
    }

    /// <summary>Reinicia un único pulso acotado; los contactos nunca apilan animaciones.</summary>
    public void PlayBounce()
    {
        if (!isActiveAndEnabled) return;
        TryAcquireLight();
        bounceRemaining = GaltonEffects.ReducedMotion ? 0f : BounceDuration;
        ApplyPulse(GaltonEffects.ReducedMotion ? 0f : 1f);
    }

    private void Update()
    {
        bool reduced = GaltonEffects.ReducedMotion;
        // Las bolas en reposo visual no reescriben renderers cada fotograma.
        if (bounceRemaining <= 0f && motionWasReduced == reduced) return;
        motionWasReduced = reduced;
        bounceRemaining = reduced ? 0f :
            Mathf.Max(0f, bounceRemaining - Time.unscaledDeltaTime);
        ApplyPulse(BouncePulse());
    }

    private float BouncePulse()
    {
        float t = bounceRemaining / BounceDuration;
        return t * t;
    }

    private void ApplyPulse(float pulse)
    {
        if (ownsLight && glowLight != null)
            glowLight.intensity = Mathf.Min(1.2f, currentIntensity * (1f + pulse * 1.6f));
        Color haloColor = Color.Lerp(currentColor, Color.white, 0.3f + pulse * 0.5f);
        haloColor.a = Mathf.Clamp01(haloOpacity * (1f + valueProgress * 0.4f + pulse * 0.65f));
        halo.color = haloColor;
        // Nunca se escala el objeto raíz: no cambia el recorrido ni la geometría de la bola.
        halo.transform.localScale = Vector3.one * (Mathf.Clamp(haloScale, 1f, 1.8f) * (1f + pulse * 0.28f));
        trail.widthMultiplier = baseTrailWidth * (1f + pulse * 0.3f);
    }
}
