using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Efectos arcade compartidos con presupuestos fijos para tandas grandes.</summary>
public class GaltonEffects : MonoBehaviour
{
    /// <summary>Desactiva sacudidas y pulsos de escala sin alterar premios ni partículas.</summary>
    public static bool ReducedMotion { get; set; }

    [Header("Pin impacts")]
    [Min(0)] [SerializeField] private int pinSparks = 3;
    [Min(0.05f)] [SerializeField] private float textDuration = 0.65f;
    [Min(0f)] [SerializeField] private float textRise = 0.38f;
    [SerializeField] private TMP_FontAsset floatingFont;

    [Header("Slot payouts")]
    [Min(0)] [SerializeField] private int slotSparks = 9;
    [Min(0f)] [SerializeField] private float slotFlashIntensity = 0.35f;
    [Min(0.01f)] [SerializeField] private float slotFlashDuration = 0.28f;

    [Header("Scene labels (fixed pool)")]
    [SerializeField] private TextMeshPro[] floatingLabels;
    [Header("Camera shake")]
    [Range(0f, 0.2f)] [SerializeField] private float maxShakeDistance = 0.035f;

    private int ParticleLimit => BouncyBallySettings.Current != null ? BouncyBallySettings.Current.particleBudget : 900;
    private const int FrameParticleLimit = 220;
    private const int PinParticleLimit = 90; // Reserva capacidad para los pagos.
    private const int FlashLimit = 8;
    private const int LabelLimit = 32;
    private struct LabelState
    {
        public TextMeshPro Label;
        public Vector3 Start;
        public Color Color;
        public float Age, Duration, Scale, Drift;
        public bool Active, Payout;
    }
    private struct FlashState
    {
        public Light2D Light;
        public float Age, Strength;
        public bool Active;
    }
    private LabelState[] labels;
    private readonly FlashState[] flashes = new FlashState[FlashLimit];
    private ParticleSystem sparks;
    private Material sparkMaterial;
    private int budgetFrame = -1, emittedThisFrame, pinsThisFrame;
    private float nextPinTextTime, trauma;
    private Camera shakeCamera, renderedCamera;
    private Vector3 cameraRestPosition;
    private bool cameraOffsetApplied;

    /// <summary>Precalienta los pools; ningún impacto crea objetos ni coroutines.</summary>
    private void Awake()
    {
        int count = floatingLabels == null ? 0 : floatingLabels.Length;
        labels = new LabelState[Mathf.Min(count, LabelLimit)];
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            TextMeshPro label = floatingLabels[i];
            if (label == null) continue;
            label.gameObject.SetActive(false);
            bool duplicate = false;
            for (int j = 0; j < used; j++)
                if (labels[j].Label == label) duplicate = true;
            if (duplicate || used == labels.Length) continue;
            label.font = GaltonTheme.Font;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 12f;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(14f, 2f);
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 100;
            labels[used++].Label = label;
        }
        for (int i = 0; i < flashes.Length; i++)
        {
            GameObject flash = new GameObject("Payout flash");
            flash.transform.SetParent(transform, false);
            Light2D light = flash.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 0f;
            flash.SetActive(false);
            flashes[i].Light = light;
        }
        GameObject emitter = new GameObject("Galton Sparks");
        emitter.transform.SetParent(transform, false);
        sparks = emitter.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = 0.06f;
        main.startLifetime = 0.3f;
        main.maxParticles = ParticleLimit;
        var emission = sparks.emission;
        emission.enabled = false;
        var shape = sparks.shape;
        shape.enabled = false;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
        var overLife = sparks.colorOverLifetime;
        overLife.enabled = true;
        overLife.color = new ParticleSystem.MinMaxGradient(fade);
        var sizeLife = sparks.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        ParticleSystemRenderer particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            sparkMaterial = new Material(shader);
            sparkMaterial.mainTexture = GaltonTheme.Disc.texture;
            // El shader URP necesita superficie transparente para respetar el desvanecido.
            if (sparkMaterial.HasProperty("_Surface")) sparkMaterial.SetFloat("_Surface", 1f);
            if (sparkMaterial.HasProperty("_ZWrite")) sparkMaterial.SetFloat("_ZWrite", 0f);
            if (sparkMaterial.HasProperty("_SrcBlend")) sparkMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (sparkMaterial.HasProperty("_DstBlend")) sparkMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
            sparkMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparkMaterial.renderQueue = (int)RenderQueue.Transparent;
            particleRenderer.sharedMaterial = sparkMaterial;
        }
        particleRenderer.sortingOrder = 80;
        sparks.Play();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        Camera.onPreCull += BeginBuiltinCamera;
        Camera.onPostRender += EndBuiltinCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        Camera.onPreCull -= BeginBuiltinCamera;
        Camera.onPostRender -= EndBuiltinCamera;
        RestoreCamera();
        trauma = 0f;
        if (sparks != null) sparks.Clear();
        if (labels != null)
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].Label != null) labels[i].Label.gameObject.SetActive(false);
                labels[i].Active = false;
            }
        for (int i = 0; i < flashes.Length; i++)
        {
            if (flashes[i].Light != null) flashes[i].Light.gameObject.SetActive(false);
            flashes[i].Active = false;
        }
    }

    private void OnDestroy()
    {
        RestoreCamera();
        if (sparkMaterial != null) Destroy(sparkMaterial);
    }

    /// <summary>Chispas blancas y de color con microimpactos que nunca se acumulan sin límite.</summary>
    public void PlayPin(Vector3 position, Color color, double bonus)
    {
        if (!isActiveAndEnabled) return;
        float intensity = BouncyBallySettings.Current != null ? BouncyBallySettings.Current.effectIntensity : 1.7f;
        EmitBurst(position, color, Mathf.Clamp(Mathf.RoundToInt(pinSparks * 3 * intensity), 0, 24), 1.8f, 0.075f, false);
        if (Time.unscaledTime >= nextPinTextTime)
        {
            SpawnText("+" + bonus.ToString("0.##"), position + Vector3.up * 0.25f, color, 1.7f, false);
            nextPinTextTime = Time.unscaledTime + 0.04f;
        }
        AddShake(0.025f);
    }

    /// <summary>Abanico, anillo y fogonazo escalados al pago real; no entrega ni modifica dinero.</summary>
    public void PlaySlot(Vector3 position, Color color, double reward)
    {
        if (!isActiveAndEnabled) return;
        float intensity = BouncyBallySettings.Current != null ? BouncyBallySettings.Current.effectIntensity : 1.7f;
        float tier = Mathf.Clamp01((float)System.Math.Log10(System.Math.Max(0d, System.Math.Min(reward, 1e8d)) + 1d) / 5f);
        if (slotSparks > 0)
        {
            EmitBurst(position, color, Mathf.Clamp(Mathf.RoundToInt(slotSparks * 3 * intensity), 0, 64) + Mathf.RoundToInt(tier * 14f), 2.8f + tier * 2f, 0.11f, true);
            EmitRing(position, Color.Lerp(color, Color.white, 0.55f), 18 + Mathf.RoundToInt(tier * 10f), 1.6f + tier);
        }
        SpawnText("+$" + GaltonNumbers.Format(reward), position + Vector3.up * 0.3f, color, 3.2f + tier, true);
        if (slotFlashIntensity > 0f) PlayFlash(position, color, 1.5f + tier);
        AddShake((0.38f + tier * 0.3f) * intensity);
    }

    private int TakeParticles(int requested, bool payout)
    {
        if (sparks == null) return 0;
        if (budgetFrame != Time.frameCount)
        {
            budgetFrame = Time.frameCount;
            emittedThisFrame = pinsThisFrame = 0;
        }
        int available = Mathf.Min(FrameParticleLimit - emittedThisFrame, ParticleLimit - sparks.particleCount);
        if (!payout) available = Mathf.Min(available, PinParticleLimit - pinsThisFrame);
        int count = Mathf.Clamp(requested, 0, Mathf.Max(0, available));
        emittedThisFrame += count;
        if (!payout) pinsThisFrame += count;
        return count;
    }

    private void EmitBurst(Vector3 position, Color color, int requested, float speed, float size, bool payout)
    {
        int count = TakeParticles(requested, payout);
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (payout) direction = new Vector2(direction.x, Mathf.Abs(direction.y) + 0.35f).normalized;
            sparks.Emit(new ParticleSystem.EmitParams {
                position = position,
                velocity = direction * Random.Range(speed * 0.45f, speed),
                startColor = i % 3 == 0 ? Color.Lerp(color, Color.white, 0.8f) : color,
                startSize = size * Random.Range(0.55f, 1.4f),
                startLifetime = payout ? Random.Range(0.3f, 0.6f) : Random.Range(0.12f, 0.28f)
            }, 1);
        }
    }

    private void EmitRing(Vector3 position, Color color, int requested, float speed)
    {
        int count = TakeParticles(requested, true);
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            sparks.Emit(new ParticleSystem.EmitParams {
                position = position + direction * 0.08f,
                velocity = direction * speed,
                startColor = color, startSize = 0.065f, startLifetime = 0.3f
            }, 1);
        }
    }

    private void SpawnText(string value, Vector3 position, Color color, float size, bool payout)
    {
        int selected = -1;
        float oldest = -1f;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].Label == null) continue;
            if (!labels[i].Active) { selected = i; break; }
            // Los pagos tienen prioridad; se recicla antes un bonus de pin.
            if (payout && (selected < 0 || (!labels[i].Payout && labels[selected].Payout) ||
                (labels[i].Payout == labels[selected].Payout && labels[i].Age > oldest)))
            { selected = i; oldest = labels[i].Age; }
        }
        if (selected < 0) return;
        TextMeshPro label = labels[selected].Label;
        labels[selected] = new LabelState {
            Label = label, Start = position, Color = Color.Lerp(color, Color.white, payout ? 0.55f : 0.25f),
            Duration = Mathf.Clamp(textDuration * (payout ? 1.4f : 1f), 0.1f, 1.5f),
            Scale = 0.11f * size, Drift = Random.Range(-0.14f, 0.14f), Active = true, Payout = payout
        };
        label.text = value;
        label.transform.position = position;
        label.transform.localScale = Vector3.one * labels[selected].Scale;
        label.color = labels[selected].Color;
        label.gameObject.SetActive(true);
    }

    private void PlayFlash(Vector3 position, Color color, float strength)
    {
        int selected = 0;
        for (int i = 0; i < flashes.Length; i++)
        {
            if (!flashes[i].Active) { selected = i; break; }
            if (flashes[i].Age > flashes[selected].Age) selected = i;
        }
        flashes[selected].Age = 0f;
        flashes[selected].Strength = strength;
        flashes[selected].Active = true;
        Light2D light = flashes[selected].Light;
        light.transform.position = position;
        light.color = Color.Lerp(color, Color.white, 0.3f);
        light.pointLightOuterRadius = 0.9f + strength * 0.3f;
        light.intensity = ReducedMotion ? 0f : Mathf.Min(1.4f, slotFlashIntensity * strength);
        light.gameObject.SetActive(true);
    }

    private void Update()
    {
        // Recupera también la base si un render se interrumpió antes de su callback final.
        RestoreCamera();
        float dt = Time.unscaledDeltaTime;
        trauma = ReducedMotion ? 0f : Mathf.MoveTowards(trauma, 0f, dt * 3.8f);
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labels[i].Active || labels[i].Label == null) continue;
            LabelState state = labels[i];
            state.Age += dt;
            float t = Mathf.Clamp01(state.Age / state.Duration);
            float ease = 1f - (1f - t) * (1f - t);
            // En movimiento reducido, el texto permanece quieto y solo pierde opacidad.
            state.Label.transform.position = state.Start + (ReducedMotion ? Vector3.zero :
                new Vector3(state.Drift * ease, Mathf.Clamp(textRise, 0f, 1f) * ease, 0f));
            float pop = ReducedMotion ? 1f : 1f + 0.24f * Mathf.Sin(Mathf.Min(1f, t * 4f) * Mathf.PI);
            state.Label.transform.localScale = Vector3.one * (state.Scale * pop);
            Color color = state.Color;
            color.a = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
            state.Label.color = color;
            if (t >= 1f) { state.Active = false; state.Label.gameObject.SetActive(false); }
            labels[i] = state;
        }
        for (int i = 0; i < flashes.Length; i++)
        {
            if (!flashes[i].Active) continue;
            flashes[i].Age += dt;
            float t = Mathf.Clamp01(flashes[i].Age / Mathf.Clamp(slotFlashDuration, 0.04f, 0.5f));
            flashes[i].Light.intensity = ReducedMotion ? 0f :
                Mathf.Min(1.4f, slotFlashIntensity * flashes[i].Strength) * (1f - t) * (1f - t);
            if (t >= 1f) { flashes[i].Active = false; flashes[i].Light.gameObject.SetActive(false); }
        }
    }

    private void AddShake(float amount)
    {
        // Los impactos simultáneos no suman vibración; prevalece el más intenso.
        if (!ReducedMotion) trauma = Mathf.Max(trauma, Mathf.Min(.65f, amount * .65f));
    }

    // Desplaza solo durante el render: el tablero y los controladores siempre ven la posición base.
    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera) => ApplyCameraShake(camera);
    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == renderedCamera) RestoreCamera();
    }
    private void BeginBuiltinCamera(Camera camera)
    {
        if (GraphicsSettings.currentRenderPipeline == null) ApplyCameraShake(camera);
    }
    private void EndBuiltinCamera(Camera camera)
    {
        if (GraphicsSettings.currentRenderPipeline == null && camera == renderedCamera) RestoreCamera();
    }
    private void ApplyCameraShake(Camera camera)
    {
        if (ReducedMotion || trauma <= 0f || cameraOffsetApplied) return;
        if (shakeCamera == null || !shakeCamera.isActiveAndEnabled) shakeCamera = Camera.main;
        if (camera != shakeCamera || camera == null) return;
        cameraRestPosition = camera.transform.localPosition;
        renderedCamera = camera;
        float clock = Time.unscaledTime * 19f;
        Vector3 offset = new Vector3(Mathf.Sin(clock), Mathf.Sin(clock * 1.37f + 0.8f), 0f);
        // La magnitud total, no cada eje, queda limitada incluso con 250 impactos simultáneos.
        offset = Vector3.ClampMagnitude(offset, 1f) * (Mathf.Clamp(BouncyBallySettings.Current != null ? BouncyBallySettings.Current.shakeDistance : maxShakeDistance, 0f, .05f) * trauma * trauma);
        camera.transform.localPosition = cameraRestPosition + offset;
        cameraOffsetApplied = true;
    }
    private void RestoreCamera()
    {
        if (cameraOffsetApplied && renderedCamera != null)
            renderedCamera.transform.localPosition = cameraRestPosition;
        cameraOffsetApplied = false;
        renderedCamera = null;
    }
}
