using System;
using UnityEditor;
using UnityEngine;

/// <summary>Comprobaciones rápidas de reglas puras. No requieren paquetes de tests ni cambian la partida.</summary>
public static class GaltonGameplayChecks
{
    [MenuItem("Tools/Bouncy Bally/Run checks")]
    public static void Run()
    {
        Require(PinTuning.Cost(0, 1) == 50000d, "First adjustment must cost 50000.");
        Require(PinTuning.Cost(1, 1) == 55000d, "Second adjustment follows 1.1^(n*n).");
        Require(PinTuning.Cost(2, 1) == 73205d, "Third adjustment follows n squared.");
        Require(double.IsPositiveInfinity(PinTuning.Cost(int.MaxValue, 1)), "Overflow must be rejected.");
        Require(double.IsPositiveInfinity(PinTuning.Cost(0, 0)), "Invalid direction must be rejected.");
        Require(PinTuning.MinimumChance == .3f && PinTuning.MaximumChance == .7f, "Tuning bounds must remain 30–70%.");
        for (int purchases = 0; purchases < 50; purchases++)
        {
            Require(PinTuning.Cost(purchases, 1) == PinTuning.Cost(purchases, -1), "Direction must not change the paid adjustment count.");
            Require(PinTuning.Cost(purchases + 1, 1) >= PinTuning.Cost(purchases, 1), "Each adjustment must cost at least the previous one.");
        }

        BallBatchState batch = new BallBatchState();
        Require(batch.TryBegin(3, 2.5f), "A ready batch must launch.");
        Require(!batch.TryBegin(3, 2.5f), "A batch cannot launch twice.");
        batch.CompleteBall();
        batch.CompleteBall();
        batch.Advance(60f);
        Require(batch.Phase == BallBatchPhase.Dropping && batch.Remaining == 1, "Cooldown cannot start before the final ball.");
        Require(batch.CompleteBall(), "Final ball must complete.");
        Require(!batch.CompleteBall(), "Completion must be idempotent.");
        batch.Advance(2f);
        Require(batch.Phase == BallBatchPhase.CoolingDown, "Cooldown must run its full duration.");
        batch.Advance(.5f);
        Require(batch.Phase == BallBatchPhase.Ready && batch.Progress == 1f, "Cooldown must return to ready.");
        BallBatchState concurrent = new BallBatchState();
        Require(concurrent.TryBegin(2, 2.5f, true), "Concurrent batch must launch.");
        concurrent.Advance(3f);
        Require(concurrent.Phase == BallBatchPhase.Dropping, "Recharge cannot make an unfinished batch ready.");
        concurrent.CompleteBall(); concurrent.CompleteBall();
        Require(concurrent.Phase == BallBatchPhase.Ready, "Finished batch must use recharge already elapsed in flight.");

        // Dos posiciones pueden usar el mismo tipo sin compartir recarga ni llegadas pendientes.
        var slots = new BallBatchState[BallSpawner.MaxBatches];
        for (int index = 0; index < slots.Length; index++)
        {
            slots[index] = new BallBatchState();
            Require(slots[index].TryBegin(index + 1, 2f, true), "Each slot must have an independent lifecycle.");
            Require(!slots[index].TryBegin(1, 2f), "One press cannot launch a slot twice.");
        }
        slots[0].Advance(3f); slots[0].CompleteBall();
        Require(slots[0].Phase == BallBatchPhase.Ready, "Completed slot must recharge independently.");
        for (int index = 1; index < slots.Length; index++)
            Require(slots[index].Remaining == index + 1, "Other slots must retain their pending balls.");

        BouncyBallySettings settings = AssetDatabase.LoadAssetAtPath<BouncyBallySettings>("Assets/Resources/BouncyBallySettings.asset");
        if (settings != null)
        {
            double first = settings.tuningFirstCost, exponent = Math.Round(settings.tuningExponent, 6);
            Require(PinTuning.Cost(0, 1, first, exponent) == Math.Ceiling(first < 1e12 ? Math.Round(first, 6) : first), "Configured first tuning cost is invalid.");
            Require(PinTuning.Cost(1, 1, first, exponent) >= PinTuning.Cost(0, 1, first, exponent), "Configured tuning costs must not decrease.");
        }
        // Comprueba objetos de la escena real, sin fabricar ni reparar interfaz de prueba.
        var issues = GaltonSceneBuilder.SceneIssues();
        Require(issues.Count == 0, string.Join("\n", issues));
        Debug.Log("Bouncy Bally gameplay checks passed: tuning costs, purchase count, bounds and batch lifecycle and serialized scene UI.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
