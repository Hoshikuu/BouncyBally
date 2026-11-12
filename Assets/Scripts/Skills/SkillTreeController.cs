using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Un árbol de objetos ya colocados en escena. Ninguna vista se crea durante Play.</summary>
public class SkillTreeController : MonoBehaviour
{
    [SerializeField] private List<BallDefinition> balls = new List<BallDefinition>();
    [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();
    [SerializeField] private GameManager game;
    [SerializeField] private GameObject window;
    [SerializeField] private RectTransform graph, viewport;
    [SerializeField] private UIDragArea drag;
    [SerializeField] private SkillTreeLines connections;
    [SerializeField] private SkillNodeUI[] nodeViews;
    [SerializeField] private Button closeButton, fitButton, buyButton;
    [SerializeField] private TMP_Text title, description, price;
    private readonly Dictionary<SkillDefinition,int> levels = new Dictionary<SkillDefinition,int>();
    private SkillDefinition selected;
    private bool initialized, openedBefore;
    public IReadOnlyList<BallDefinition> Balls => balls;
    public IReadOnlyList<SkillDefinition> Skills => skills;
    public bool IsOpen => window != null && window.activeSelf;
    private void Awake()
    {
        BouncyBallySettings config = BouncyBallySettings.Current;
        if (config == null) return;
        balls = new List<BallDefinition>(config.balls ?? new BallDefinition[0]);
        skills = new List<SkillDefinition>(config.skills ?? new SkillDefinition[0]);
    }
    public bool Initialize()
    {
        if (initialized) return true;
        if (game == null || window == null || graph == null || viewport == null || drag == null || connections == null ||
            connections.GetComponent<CanvasRenderer>() == null || nodeViews == null || closeButton == null || fitButton == null ||
            buyButton == null || title == null || description == null || price == null)
        { Debug.LogError("Skill tree has missing scene references.",this); return false; }
        foreach (SkillNodeUI node in nodeViews)
            if (node == null || !node.HasSceneReferences)
            { Debug.LogError("Skill tree contains an incomplete scene node.",this); return false; }
        foreach (SkillNodeUI node in nodeViews) node.Bind(this);
        ApplyNodePositions();
        connections.SetNodes(nodeViews);
        drag.Configure(graph,viewport,game.Settings.graphSize,game.Settings.minimumZoom,game.Settings.maximumZoom);
        closeButton.onClick.AddListener(Close);fitButton.onClick.AddListener(Fit);buyButton.onClick.AddListener(BuySelected);
        game.MoneyChanged += Refresh;game.ProgressChanged += Refresh;
        initialized=true;window.SetActive(false);return true;
    }
    private void Fit() => drag.ResetView();
    public void Open()
    {
        if (!initialized) return;
        window.SetActive(true);window.transform.SetAsLastSibling();
        // El viewport puede cubrir el encabezado al ampliar el árbol.
        closeButton.transform.SetAsLastSibling();
        ApplyNodePositions();
        if (!openedBefore) { drag.Focus(new Vector2(.5f,.85f),game.Settings.openingZoom); openedBefore = true; }
        if (selected == null) foreach (SkillDefinition skill in skills)
            if (skill != null && skill.Prerequisite == null && (skill.SectionBall == null || game.IsBallUnlocked(skill.SectionBall)))
            { selected=skill;break; }
        Refresh();
    }
    /// <summary>Aplica las posiciones guardadas en los assets al árbol de la escena.</summary>
    private void ApplyNodePositions()
    {
        foreach (SkillNodeUI node in nodeViews)
        {
            if (node == null || node.Skill == null || node.Rect == null) continue;
            node.Rect.anchorMin = node.Skill.Position;
            node.Rect.anchorMax = node.Skill.Position;
            node.Rect.anchoredPosition = Vector2.zero;
        }
        connections.SetVerticesDirty();
    }
    public void Close() { if (window != null) window.SetActive(false); }
    public void ResetLevels()
    {
        levels.Clear();
        Refresh();
    }
    public int GetLevel(SkillDefinition skill) => skill != null && levels.TryGetValue(skill,out int value) ? value : 0;
    public bool MeetsRequirement(SkillDefinition skill) => skill != null && game != null &&
        (skill.SectionBall == null || game.IsBallUnlocked(skill.SectionBall)) &&
        (skill.Prerequisite == null || (skills.Contains(skill.Prerequisite) && GetLevel(skill.Prerequisite) >= skill.PrerequisiteLevel));
    public bool IsComplete(SkillDefinition skill) => skill != null && (GetLevel(skill) >= skill.MaxLevel ||
        (skill.Effect == SkillEffect.BoardRows && game != null && game.Board != null && !game.Board.CanExpand));
    public bool CanBuy(SkillDefinition skill)
    {
        if (skill == null || game == null || !skills.Contains(skill) || IsComplete(skill) || !MeetsRequirement(skill) || !game.CanApplySkill(skill)) return false;
        if (skill.Effect == SkillEffect.UnlockBall && (skill.UnlockBall == null || !balls.Contains(skill.UnlockBall) ||
            skill.UnlockBall.Prefab == null || game.IsBallUnlocked(skill.UnlockBall))) return false;
        double cost=skill.CostAt(GetLevel(skill));
        return cost>0 && !double.IsInfinity(cost) && !double.IsNaN(cost) && game.Money>=cost;
    }
    public void Select(SkillDefinition skill) { if (!skills.Contains(skill)) return;selected=skill;Refresh(); }
    public void SelectSection(SkillSectionUI section) { }
    public void Buy(SkillDefinition skill) { Select(skill); BuySelected(); }
    private void BuySelected()
    {
        if (!CanBuy(selected)) return;
        SkillDefinition bought=selected;int level=GetLevel(bought);
        if (!game.TrySpendMoney(bought.CostAt(level))) return;
        levels[bought]=level+1;game.ApplySkill(bought);Refresh();
        game.Board.GetComponent<GaltonEffects>()?.PlaySlot(game.Board.GetSpawnPosition(),GaltonTheme.Accent,0);
    }
    private void Refresh()
    {
        if (!initialized || !IsOpen || game == null) return;
        foreach (SkillNodeUI node in nodeViews) if (node != null) node.Refresh(this,node.Skill==selected);
        if (selected == null) return;
        title.text=selected.Title;
        description.text=selected.Description+(!MeetsRequirement(selected)?"\nRequires "+(selected.Prerequisite!=null?selected.Prerequisite.Title:"ball unlock"):"");
        buyButton.interactable=CanBuy(selected);price.text=IsComplete(selected)?"MAX":"$"+GaltonNumbers.Format(selected.CostAt(GetLevel(selected)));
    }
    private void OnDestroy()
    {
        if (!initialized) return;
        if (game != null) { game.MoneyChanged-=Refresh;game.ProgressChanged-=Refresh; }
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (fitButton != null) fitButton.onClick.RemoveListener(Fit);
        if (buyButton != null) buyButton.onClick.RemoveListener(BuySelected);
    }
}
