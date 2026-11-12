using System;
using UnityEditor;
using UnityEngine;

public static class BallTypeChecks
{
    [MenuItem("Tools/Bouncy Bally/Check new ball types")]
    public static void Run()
    {
        var definitions = Resources.LoadAll<BallDefinition>("ExtraBalls");
        if (definitions.Length != 11) throw new Exception("Expected eleven new ball types, without Egg.");
        foreach (var definition in definitions)
            if (definition.Prefab == null || definition.Prefab.GetComponent<BallSpecial>() == null)
                throw new Exception("Missing behaviour: " + definition.name);
        int winners = 0;
        for (int sector = 0; sector < 10; sector++)
        {
            double payout = BallGambler.RewardFactor(sector);
            if (payout == 10) winners++;
            else if (payout != 0) throw new Exception("Losing sectors must pay zero.");
        }
        if (winners != 1) throw new Exception("Roulette must have exactly 10% winning sectors.");
        var go = new GameObject("Ball checks");
        try
        {
            var sticky = go.AddComponent<BallSticky>();
            if (sticky.Contacts != 3) throw new Exception("Sticky must revisit its pin.");
            UnityEngine.Object.DestroyImmediate(sticky);
            var trick = go.AddComponent<BallTrick>();
            bool previous = BallTrick.FirstBounceRight;
            try
            {
                BallTrick.FirstBounceRight = false;
                if (trick.Direction(0, true) || !trick.Direction(1, true)) throw new Exception("Trick direction applies only to first bounce.");
                BallTrick.FirstBounceRight = true;
                if (!trick.Direction(0, false)) throw new Exception("Trick must support both directions.");
            }
            finally { BallTrick.FirstBounceRight = previous; }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Debug.Log("Ball type checks passed.");
    }
}
