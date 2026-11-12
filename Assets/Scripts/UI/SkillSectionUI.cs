using UnityEngine;
using UnityEngine.UI;

/// <summary>Sección global o de un tipo concreto. Conserva todos sus nodos en la escena.</summary>
public class SkillSectionUI : MonoBehaviour
{
    [SerializeField] private BallDefinition ball;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Button tab;
    [SerializeField] private Image tabBackground;
    public BallDefinition Ball => ball;
    public bool IsVisible { get; private set; }

    public void Bind(SkillTreeController controller) => tab.onClick.AddListener(() => controller.SelectSection(this));

    public void SetVisible(bool value)
    {
        IsVisible = value;
        visibility.alpha = value ? 1f : 0f;
        visibility.interactable = value;
        visibility.blocksRaycasts = value;
        tabBackground.color = value ? GaltonTheme.SoftAccent : GaltonTheme.Surface;
    }
}
