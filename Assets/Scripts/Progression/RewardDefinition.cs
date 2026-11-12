using UnityEngine;
public enum CollectionRewardEffect { Modifier, BallValue, RewardFactor, PinBonus, SlotBonus, UnlockBall }

/// <summary>Premio único compartido entre todas las cajas. El ID impide repetirlo.</summary>
[CreateAssetMenu(menuName = "Bouncy Bally/Reward")]
public class RewardDefinition : ScriptableObject
{
    public string id;
    public string title = "New Reward";
    [TextArea] public string description;
    public CollectionRewardEffect effect;
    public PinModifierDefinition modifier;
    public BallDefinition ball;
    [Min(.01f)] public double amount = 1;
    [Min(.01f)] public float weight = 1;
}
