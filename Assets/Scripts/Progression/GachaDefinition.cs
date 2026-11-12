using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>Catálogo cerrado: precio en núcleos, requisito y premios disponibles sin duplicados.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Gacha")]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "CapsuleDefinition")]
public class GachaDefinition : ScriptableObject
{
    public string id;
    public string title = "New Gacha";
    public Color color = Color.cyan;
    [Min(1)] public int ticketCost = 1;
    public SkillDefinition requiredSkill;
    public RewardDefinition[] rewards;
}
