using UnityEngine;
public enum ChallengeMetric { Earned, BatchValue, ManualBatches, EdgeRatio, CompletedBatches, DistinctModules }

/// <summary>Objetivo de una sola reclamación; los retos son también la fuente de núcleos.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Goal")]
public class ChallengeDefinition : ScriptableObject
{
    public string id;
    public string title = "New Goal";
    [TextArea] public string description;
    public ChallengeMetric metric;
    [Min(.01f)] public double target = 1;
    [Min(0)] public int minimumSamples;
    [Min(1)] public int tickets = 1;
    public SkillDefinition requiredSkill;
}
