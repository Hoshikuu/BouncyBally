using UnityEngine;

public class BallTrick : BallSpecial
{
    public static bool FirstBounceRight = true; public override bool Direction(int row, bool right) => row == 0 ? FirstBounceRight : right;
}
