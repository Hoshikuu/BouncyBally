using UnityEngine;

[CreateAssetMenu(menuName = "Bouncy Bally/Custom Ball Behavior")]
public class BallBehaviour : ScriptableObject
{
    public enum Ability { None, Trick, Chemical, Marble, Constructor, Gambling }
    public Ability ability;
    public float speedFactor = 1, sizeFactor = 1, bounceFactor = 1;
    public int contacts = 1;
    public double flatPinBonus, pinBonusFactor = 1, rewardFactor = 1;
    public float speedVariation;
    public bool firstBounceRight = true;
    public int chemicalEveryContacts = 3;
    public double chemicalBonus = .5;
    public double raceWinnerFactor = 2;
    public int plansPerLanding = 1;
    [Tooltip("Solo estas variantes pueden aparecer como sustitución aleatoria.")]
    public bool randomOnly;
    [Range(0, 1)] public float appearanceChance;
}
