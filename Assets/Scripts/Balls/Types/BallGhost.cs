using UnityEngine;

public class BallGhost : BallSpecial
{
    public override float SpeedFactor => 2f; public override float SizeFactor => .6f; public override double ComputeReward(double slot) => base.ComputeReward(slot) * .5d;
}
