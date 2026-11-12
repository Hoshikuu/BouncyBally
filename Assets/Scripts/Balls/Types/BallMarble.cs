using UnityEngine;

public class BallMarble : BallSpecial
{
    public override float SpeedFactor => .8f + variation;
    private float variation;
    public override void Setup(double value, GaltonColors palette) { variation = Random.Range(0f, .6f); base.Setup(value, palette); }
    public override System.Collections.IEnumerator Resolve(BallController controller, System.Action<double> done)
    { done(controller.IsFirstArrival ? 2d : 1d); yield break; }
}
