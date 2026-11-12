using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tarjeta de una posición de batch; la bola asignada se consulta al controlador.</summary>
public class BallBatchCard : MonoBehaviour
{
    [SerializeField, Range(0, 3)] private int batchIndex;
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private GamePresentation presentation;
    [SerializeField] private Image fill, identity;
    [SerializeField] private Button launch, auto;
    [SerializeField] private TMP_Text label, autoLabel, title, details;
    private bool initialized;
    public int BatchIndex => batchIndex;
    public RectTransform Rect => transform as RectTransform;
    public bool Initialize()
    {
        if (initialized) return true;
        launch.onClick.AddListener(Launch); auto.onClick.AddListener(ToggleAuto);
        GaltonTheme.StyleButton(launch); initialized = true; return true;
    }
    private void Launch() => presentation.RequestLaunch(batchIndex);
    private void ToggleAuto() => spawner.ToggleAuto(batchIndex);
    public void SetVisible(bool value) => gameObject.SetActive(value);
    private void Update()
    {
        if (!initialized || GameManager.Instance == null) return;
        BallDefinition ball = spawner.GetBall(batchIndex);
        BallBatchState state = spawner.GetState(batchIndex);
        if (ball == null || state == null) return;
        title.text = "BATCH " + (batchIndex + 1) + " · " + ball.Title;
        identity.color = ball.DisplayColor;
        GameManager game = GameManager.Instance;
        details.text = game.GetBallCount(ball) + " balls · $" + GaltonNumbers.Format(game.GetBallValue(ball)) + " · " + game.GetBallSpeed(ball).ToString("0.#") + " speed";
        launch.interactable = spawner.CanLaunch(batchIndex);
        fill.rectTransform.anchorMax = new Vector2(state.Progress, 1);
        label.text = state.Phase == BallBatchPhase.Ready ? "LAUNCH [" + (batchIndex + 1) + "]" : state.Phase == BallBatchPhase.Dropping ? state.Remaining + " LEFT" : state.RemainingCooldown.ToString("0.0") + "s";
        auto.gameObject.SetActive(game.HasAutoLaunch(ball));
        autoLabel.text = state.Auto ? "AUTO ON" : "AUTO OFF";
    }
    private void OnDestroy()
    {
        if (!initialized) return;
        launch.onClick.RemoveListener(Launch); auto.onClick.RemoveListener(ToggleAuto);
    }
}
