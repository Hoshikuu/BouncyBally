using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Ventanas de progresión enlazadas exclusivamente a objetos de la escena.</summary>
public class ProgressionUI : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private GamePresentation presentation;
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private GameObject goals, stats;
    [FormerlySerializedAs("capsules")] [SerializeField] private GameObject gacha;
    [SerializeField] private TMP_Text tickets, prize, statistics;
    [SerializeField] private DistributionChart chart;
    [SerializeField] private ScrollRect gachaScroll;
    [SerializeField] private Button[] closeButtons;

    [Serializable]
    public sealed class GoalRow
    {
        public ChallengeDefinition goal;
        public TMP_Text label;
        public Button claim;
        public TMP_Text claimLabel;
    }

    [Serializable]
    public sealed class BoxRow
    {
        public GachaDefinition box;
        public TMP_Text label, odds;
        public Button open;
        public TMP_Text openLabel;
    }


    [SerializeField] private GoalRow[] rows;
    [SerializeField] private BoxRow[] boxes;

    private struct ButtonCallback
    {
        public Button button;
        public UnityAction callback;
    }

    private readonly List<ButtonCallback> buttonCallbacks = new List<ButtonCallback>();
    private bool initialized, subscribed;
    private GameManager subscribedGame;
    private ProgressionSystem subscribedProgression;
    private RewardCollection subscribedCollection;
    private BallSpawner subscribedSpawner;
    private GaltonBoard subscribedBoard;
    private Action refreshCallback, boardChangedCallback;
    private Action<int> landingCallback;

    public bool IsOpen => (goals != null && goals.activeSelf) ||
        (gacha != null && gacha.activeSelf) || (stats != null && stats.activeSelf);

    /// <summary>GamePresentation.Start debe llamar antes de usar las ventanas; admite reintentos.</summary>
    public bool Initialize()
    {
        if (initialized) return true;
        subscribedGame = game;
        subscribedProgression = game.Progression;
        subscribedCollection = game.Collection;
        subscribedSpawner = spawner;
        subscribedBoard = game.Board;
        refreshCallback = Refresh;
        boardChangedCallback = BoardChanged;
        landingCallback = RecordLanding;

        foreach (GoalRow row in rows ?? Array.Empty<GoalRow>())
        {
            ChallengeDefinition item = row.goal;
            Bind(row.claim, () => { game.Progression.Claim(item); Refresh(); });
        }
        foreach (BoxRow row in boxes ?? Array.Empty<BoxRow>())
        {
            GachaDefinition item = row.box;
            Bind(row.open, () =>
            {
                if (game.Collection.TryOpen(item, out RewardDefinition reward))
                {
                    if (prize != null) prize.text = reward.title;
                    game.Board.GetComponent<GaltonEffects>()?.PlaySlot(game.Board.GetSpawnPosition(), item.color, 1000);
                }
                Refresh();
            });
        }
        UnityAction closeCallback = () =>
        {
            if (initialized && presentation != null) presentation.CloseDataWindow();
        };
        foreach (Button button in closeButtons) Bind(button, closeCallback);

        subscribedProgression.Changed += refreshCallback;
        subscribedCollection.Changed += refreshCallback;
        subscribedGame.ProgressChanged += refreshCallback;
        subscribedSpawner.BallLanded += landingCallback;
        subscribedBoard.Changed += boardChangedCallback;
        subscribed = true;
        initialized = true;
        chart.raycastTarget = false;
        BoardChanged();
        CloseAll();
        return true;
    }





    private void Bind(Button button, UnityAction callback)
    {
        button.onClick.AddListener(callback);
        buttonCallbacks.Add(new ButtonCallback { button = button, callback = callback });
    }

    public void OpenGoals()
    {
        if (!initialized || presentation == null || goals == null) return;
        presentation.ShowDataWindow(goals);
        Refresh();
    }

    public void OpenStats()
    {
        if (!initialized || presentation == null || stats == null) return;
        presentation.ShowDataWindow(stats);
        Refresh();
    }

    public void OpenGacha()
    {
        if (!initialized || game == null || !game.WorkshopUnlocked || presentation == null || gacha == null ||
            gachaScroll == null || gachaScroll.content == null || gachaScroll.viewport == null) return;
        presentation.ShowDataWindow(gacha);
        Refresh();
        // La máscara debe medir el contenido después de activar la ventana, no con el panel oculto.
        gachaScroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gachaScroll.content);
        gachaScroll.verticalNormalizedPosition = 1f;
        gachaScroll.content.anchoredPosition = Vector2.zero;
        gachaScroll.viewport.GetComponent<RectMask2D>()?.PerformClipping();
    }


    public void CloseAll()
    {
        // También funciona antes de inicializar o después de una validación fallida.
        if (goals != null) goals.SetActive(false);
        if (gacha != null) gacha.SetActive(false);
        if (stats != null) stats.SetActive(false);
    }

    public void RecordLanding(int column)
    {
        if (initialized && chart != null) chart.AddResult(column);
    }

    public void BoardChanged()
    {
        if (initialized && chart != null && subscribedBoard != null) chart.SetBoard(subscribedBoard);
    }

    private void Refresh()
    {
        if (!initialized || game == null || spawner == null || !IsOpen) return;
        foreach (GoalRow row in rows ?? Array.Empty<GoalRow>())
        {
            bool ratio = row.goal.metric == ChallengeMetric.EdgeRatio;
            double value = game.Progression.Value(row.goal);
            string progress = ratio ? (value * 100).ToString("0") + "% / " + (row.goal.target * 100).ToString("0") + "%" :
                GaltonNumbers.Format(value) + " / " + GaltonNumbers.Format(row.goal.target);
            row.label.text = row.goal.title + "\n" + progress + " · " + row.goal.tickets + " cores";
            bool claimed = game.Progression.IsClaimed(row.goal);
            bool canClaim = game.Progression.CanClaim(row.goal);
            row.claim.interactable = canClaim;
            row.claimLabel.text = claimed ? "CLAIMED" : canClaim ? "CLAIM" : "IN PROGRESS";
            row.claimLabel.color = Color.white;
            if (row.claim.targetGraphic != null)
                row.claim.targetGraphic.color = claimed ? new Color(.23f, .27f, .48f) :
                    canClaim ? new Color(.08f, .62f, .37f) : new Color(.34f, .25f, .22f);
            // Evita que el tinte de desactivación oculte el color de estado.
            ColorBlock claimColors = row.claim.colors;
            claimColors.disabledColor = Color.white;
            row.claim.colors = claimColors;
        }
        if (tickets != null) tickets.text = game.Progression.Tickets + " CORES";
        foreach (BoxRow row in boxes ?? Array.Empty<BoxRow>())
        {
            row.label.text = row.box.title + " · " + row.box.ticketCost + " cores · " + game.Collection.Remaining(row.box) + " left";
            row.open.interactable = game.Collection.CanOpen(row.box);
            row.openLabel.text = game.Collection.Remaining(row.box) == 0 ? "EMPTY" : "OPEN";
            var lines = new List<string>();
            if (row.box.rewards != null) foreach (RewardDefinition reward in row.box.rewards)
            {
                float chance = game.Collection.Chance(row.box, reward);
                if (reward != null && chance > 0) lines.Add(reward.title + " " + (chance * 100).ToString("0.#") + "%");
            }
            row.odds.text = string.Join("   ·   ", lines);
        }
        statistics.text = "$" + GaltonNumbers.Format(game.IncomePerMinute) + "/min   Last $" + GaltonNumbers.Format(game.LastBatch);
    }

    private void OnDestroy()
    {
        if (!initialized && !subscribed) return;
        initialized = false;
        if (subscribed)
        {
            // Se usan los emisores y delegates originales, sin acceder a propiedades de otros componentes.
            if (subscribedProgression != null) subscribedProgression.Changed -= refreshCallback;
            if (subscribedCollection != null) subscribedCollection.Changed -= refreshCallback;
            if (subscribedGame != null) subscribedGame.ProgressChanged -= refreshCallback;
            if (subscribedSpawner != null) subscribedSpawner.BallLanded -= landingCallback;
            if (subscribedBoard != null) subscribedBoard.Changed -= boardChangedCallback;
            subscribed = false;
        }
        foreach (ButtonCallback binding in buttonCallbacks)
            if (binding.button != null) binding.button.onClick.RemoveListener(binding.callback);
        buttonCallbacks.Clear();
        subscribedGame = null;
        subscribedProgression = null;
        subscribedCollection = null;
        subscribedSpawner = null;
        subscribedBoard = null;
        refreshCallback = null;
        boardChangedCallback = null;
        landingCallback = null;
    }
}
