using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Configura cuatro posiciones mediante botones y textos ya serializados en escena.</summary>
public class BatchConfigurationUI : MonoBehaviour
{
    [Serializable] private sealed class BatchSlot
    {
        public Button button;
        public TMP_Text label;
        public Image identity;
    }
    [Serializable] private sealed class BallChoice
    {
        public BallDefinition ball;
        public Button button;
        public TMP_Text label;
        public Image identity;
    }
    [SerializeField] private GameManager game;
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private GameObject root;
    [SerializeField] private Button openButton, closeButton;
    [SerializeField] private BatchSlot[] slots;
    [SerializeField] private BallChoice[] choices;
    [SerializeField] private TMP_Text hint;
    private int selected;
    private bool initialized;
    public bool IsOpen => root != null && root.activeSelf;
    public bool Initialize()
    {
        if (initialized) return true;
        for (int i = 0; i < slots.Length; i++) { int index = i; slots[i].button.onClick.AddListener(() => { selected = index; Refresh(); }); }
        foreach (BallChoice choice in choices) { BallDefinition ball = choice.ball; choice.button.onClick.AddListener(() => { spawner.AssignBall(selected, ball); Refresh(); }); }
        openButton.onClick.AddListener(Open); closeButton.onClick.AddListener(Close);
        game.ProgressChanged += Refresh; spawner.ConfigurationChanged += Refresh;
        initialized = true; root.SetActive(false); Refresh(); return true;
    }
    private void Open() { GamePresentation.Instance.OpenBatchConfiguration(root); Refresh(); }
    public void Close() { if (root != null) root.SetActive(false); }
    private void Refresh()
    {
        if (!initialized) return;
        int unlocked = 0;
        foreach (BallDefinition ball in spawner.Balls) if (game.IsBallUnlocked(ball)) unlocked++;
        openButton.gameObject.SetActive(unlocked >= 2);
        for (int i = 0; i < slots.Length; i++)
        {
            bool available = spawner.IsBatchUnlocked(i); BallDefinition ball = spawner.GetBall(i);
            slots[i].button.interactable = available;
            slots[i].label.text = "BATCH " + (i + 1) + "\n" + (available ? ball != null ? ball.Title : "Basic Ball" : "LOCKED") + (i == selected ? "\nSELECTED" : "");
            slots[i].identity.color = available && ball != null ? ball.DisplayColor : GaltonTheme.Muted;
        }
        bool ready = spawner.GetState(selected) != null && spawner.GetState(selected).Phase == BallBatchPhase.Ready;
        hint.text = ready ? "Select a batch, then choose one unlocked ball type." : "Wait for this batch to finish and recharge before changing its ball.";
        foreach (BallChoice choice in choices)
        {
            bool available = game.IsBallUnlocked(choice.ball);
            choice.button.interactable = available && ready;
            choice.label.text = choice.ball.Title + (available ? "\nAVAILABLE" : "\nLOCKED");
            choice.identity.color = available ? choice.ball.DisplayColor : GaltonTheme.Muted;
        }
    }
    private void Update() { if (IsOpen) Refresh(); }
    private void OnDestroy()
    {
        if (!initialized) return;
        if (game != null) game.ProgressChanged -= Refresh;
        if (spawner != null) spawner.ConfigurationChanged -= Refresh;
    }
}
