using UnityEngine;

public class BallConstructor : BallSpecial
{
    public override void Setup(double value, GaltonColors palette)
    {
        base.Setup(value, palette);
        var hat = new GameObject("Constructor hat");
        hat.transform.SetParent(transform, false);
        hat.transform.localPosition = new Vector3(0, .35f, 0);
        hat.transform.localScale = new Vector3(1.2f, .35f, 1);
        var renderer = hat.AddComponent<SpriteRenderer>();
        var source = GetComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = Color.yellow;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
    }
    public override System.Collections.IEnumerator Resolve(BallController controller, System.Action<double> done)
    { BallPlans.Add(1); done(1d); yield break; }
}
