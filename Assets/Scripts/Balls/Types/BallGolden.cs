using UnityEngine;

public class BallGolden : BallSpecial
{
    public override double ComputeReward(double slot) => base.ComputeReward(slot) * 10d;
}
