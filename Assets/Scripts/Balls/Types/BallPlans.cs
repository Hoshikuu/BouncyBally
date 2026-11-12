using UnityEngine;
/// <summary>Planos obtenidos por constructoras; separados del dinero y de las habilidades.</summary>
public static class BallPlans
{
    private const string Key = "BouncyBally.ConstructorPlans";
    public static int Count => PlayerPrefs.GetInt(Key, 0);
    public static void Add(int count)
    { PlayerPrefs.SetInt(Key, (int)System.Math.Min(int.MaxValue, (long)Count + System.Math.Max(0, count))); PlayerPrefs.Save(); }
}
