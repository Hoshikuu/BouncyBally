using UnityEngine;

public class BallChemical : BallSpecial
{
    private int contacts;
    public override void Setup(double value, GaltonColors palette) { contacts = 0; base.Setup(value, palette); }
    public override void Contact(Pin pin)
    {
        if (++contacts % 3 != 0) return;
        var others = Object.FindObjectsByType<BallBase>(FindObjectsSortMode.None);
        var candidates = new System.Collections.Generic.List<BallBase>();
        foreach (var other in others) if (other != this && other.gameObject.scene == gameObject.scene) candidates.Add(other);
        if (candidates.Count > 0) candidates[Random.Range(0, candidates.Count)].AddPinBonus(.5d);
        else AddPinBonus(.5d);
    }
}
