using System.Collections;
using UnityEngine;

/// <summary>Estado de una bola. Las funciones y estadísticas se editan en su asset Custom behavior.</summary>
public class BallSpecial : BallBase
{
    private BallBehaviour behavior;
    private int touched;
    private float variation;
    public void Configure(BallBehaviour custom) { behavior = custom; }
    public float SpeedFactor => behavior == null ? 1 : behavior.speedFactor + variation;
    public float SizeFactor => behavior == null ? 1 : behavior.sizeFactor;
    public float BounceFactor => behavior == null ? 1 : behavior.bounceFactor;
    public int Contacts => behavior == null ? 1 : behavior.contacts;
    public override void Setup(double value, GaltonColors colors)
    {
        touched = 0;
        variation = behavior == null ? 0 : Random.Range(0, behavior.speedVariation);
        base.Setup(value, colors);
        if (behavior != null && behavior.ability == BallBehaviour.Ability.Constructor)
        {
            var hat = new GameObject("Constructor hat"); hat.transform.SetParent(transform, false);
            hat.transform.localPosition = new Vector3(0, .35f, 0);
            hat.transform.localScale = new Vector3(1.2f, .35f, 1);
            var image = hat.AddComponent<SpriteRenderer>(); var source = GetComponent<SpriteRenderer>();
            image.sprite = source.sprite; image.color = Color.yellow;
            image.sortingLayerID = source.sortingLayerID; image.sortingOrder = source.sortingOrder + 1;
        }
    }
    public bool Direction(int row, bool right) => behavior != null && behavior.ability == BallBehaviour.Ability.Trick && row == 0 ? behavior.firstBounceRight : right;
    public override double AddPinBonus(double amount) => base.AddPinBonus(behavior == null ? amount : amount * behavior.pinBonusFactor + behavior.flatPinBonus);
    public override double ComputeReward(double slot) => base.ComputeReward(slot) * (behavior == null ? 1 : behavior.rewardFactor);
    public void Contact(Pin pin)
    {
        touched++;
        if (behavior == null || behavior.ability != BallBehaviour.Ability.Chemical || touched % behavior.chemicalEveryContacts != 0) return;
        var candidates = new System.Collections.Generic.List<BallBase>();
        foreach (var other in Object.FindObjectsByType<BallBase>(FindObjectsSortMode.None))
            if (other != this && other.gameObject.scene == gameObject.scene) candidates.Add(other);
        (candidates.Count == 0 ? this : candidates[Random.Range(0, candidates.Count)]).AddPinBonus(behavior.chemicalBonus);
    }
    public IEnumerator Resolve(BallController controller, System.Action<double> done)
    {
        if (behavior == null) { done(1); yield break; }
        switch (behavior.ability)
        {
            case BallBehaviour.Ability.Gambling:
                yield return GamblingPanel.Instance.Spin(this, done); yield break;
            case BallBehaviour.Ability.Constructor: BallPlans.Add(behavior.plansPerLanding); break;
            case BallBehaviour.Ability.Marble: done(controller.IsFirstArrival ? behavior.raceWinnerFactor : 1); yield break;
        }
        done(1);
    }
}
