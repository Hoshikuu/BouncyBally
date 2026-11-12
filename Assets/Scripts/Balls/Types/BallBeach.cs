using UnityEngine;

public class BallBeach : BallSpecial
{
    public override float SpeedFactor => .5f; public override float SizeFactor => 1.2f; public override double ComputeReward(double slot) => base.ComputeReward(slot) * 2d;
}
