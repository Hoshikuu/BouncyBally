using UnityEngine;

public class BallSteel : BallSpecial
{
    public override float SpeedFactor => .2f; public override float SizeFactor => 1.3f; public override float BounceFactor => .3f; public override double ComputeReward(double slot) => base.ComputeReward(slot) * 3d;
}
