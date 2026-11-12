using UnityEngine;

public class BallBasket : BallSpecial
{
    public override float SpeedFactor => .7f; public override float BounceFactor => 1.8f; public override double AddPinBonus(double amount) => base.AddPinBonus(amount + .25d);
}
