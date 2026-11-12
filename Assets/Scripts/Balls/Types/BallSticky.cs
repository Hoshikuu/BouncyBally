using UnityEngine;

public class BallSticky : BallSpecial
{
    public override int Contacts => 3; public override float SpeedFactor => .8f;
}
