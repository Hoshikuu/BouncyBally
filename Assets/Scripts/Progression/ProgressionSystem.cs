using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Métricas de la partida y retos únicos que conceden núcleos.</summary>
public class ProgressionSystem : MonoBehaviour
{
    private GameManager game;
    private PinModuleInventory inventory;
    private BouncyBallySettings config;
    private readonly HashSet<string> claimed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> usedModules = new HashSet<string>(StringComparer.Ordinal);
    private double earned;
    private double bestBatch;
    private long completedBatches, manualBatches, arrivals, edgeArrivals;
    private bool changing;

    public int Tickets { get; private set; }
    public int TotalClaimed => claimed.Count;
    public event Action Changed;

    public void Initialize(GameManager game, PinModuleInventory inventory, BouncyBallySettings config)
    {
        if (changing) return;
        this.game = game;
        this.inventory = inventory;
        this.config = config;
    }

    internal bool IsInitializedFor(GameManager owner, PinModuleInventory stock, BouncyBallySettings settings)
    {
        return Ready && game == owner && inventory == stock && config == settings;
    }

    private bool Ready => game != null && inventory != null && config != null;

    public void NotifyBatch(double reward, int count, int edgeCount, bool manual)
    {
        if (changing || !Ready || !ProgressionCatalog.Finite(reward) || reward < 0d ||
            count <= 0 || edgeCount < 0 || edgeCount > count) return;
        bestBatch = Math.Max(bestBatch, reward);
        completedBatches = AddCount(completedBatches, 1);
        if (manual) manualBatches = AddCount(manualBatches, 1);
        arrivals = AddCount(arrivals, count);
        edgeArrivals = AddCount(edgeArrivals, edgeCount);
        // Los ingresos se notifican aparte: una tanda no vuelve a sumarlos.
        RaiseChanged();
    }

    public void NotifyManualLaunch()
    {
        if (changing || !Ready) return;
        // Lanzar no equivale a completar: ManualBatches avanza al llegar la tanda.
        RaiseChanged();
    }

    public void NotifyModuleUse(PinModifierDefinition modifier)
    {
        if (changing || !Ready || !ProgressionCatalog.Unique(modifier, config.modifiers, item => item.id)) return;
        // Se llama desde el contacto real, nunca desde equipar o comprar.
        if (usedModules.Add(modifier.id)) RaiseChanged();
    }

    public void NotifyEarned(double amount)
    {
        if (changing || !Ready || !ProgressionCatalog.Finite(amount) || amount <= 0d) return;
        earned = amount > double.MaxValue - earned ? double.MaxValue : earned + amount;
        RaiseChanged();
    }

    public double Value(ChallengeDefinition challenge)
    {
        if (!ValidChallenge(challenge)) return 0d;
        switch (challenge.metric)
        {
            case ChallengeMetric.Earned: return earned;
            case ChallengeMetric.BatchValue: return bestBatch;
            case ChallengeMetric.ManualBatches: return manualBatches;
            case ChallengeMetric.EdgeRatio: return arrivals > 0 ? (double)edgeArrivals / arrivals : 0d;
            case ChallengeMetric.CompletedBatches: return completedBatches;
            case ChallengeMetric.DistinctModules: return usedModules.Count;
            default: return 0d;
        }
    }

    public bool IsClaimed(ChallengeDefinition challenge)
    {
        return ValidChallenge(challenge) && claimed.Contains(challenge.id);
    }

    public bool CanClaim(ChallengeDefinition challenge)
    {
        return ValidChallenge(challenge) && !claimed.Contains(challenge.id) &&
            ProgressionCatalog.HasSkill(game, config, challenge.requiredSkill) &&
            ProgressionCatalog.Finite(challenge.target) && challenge.target > 0d &&
            challenge.minimumSamples >= 0 && challenge.tickets > 0 &&
            Tickets <= int.MaxValue - challenge.tickets &&
            (challenge.metric != ChallengeMetric.EdgeRatio ||
                (challenge.target <= 1d && arrivals > 0 && arrivals >= challenge.minimumSamples)) &&
            Value(challenge) >= challenge.target;
    }

    public bool Claim(ChallengeDefinition challenge)
    {
        if (changing || !CanClaim(challenge)) return false;
        claimed.Add(challenge.id);
        Tickets += challenge.tickets;
        RaiseChanged();
        return true;
    }

    public bool TrySpendTickets(int amount)
    {
        if (changing || !Ready || amount <= 0 || Tickets < amount) return false;
        Tickets -= amount;
        RaiseChanged();
        return true;
    }

    public void ResetRunMetrics()
    {
        if (changing) return;
        earned = bestBatch = 0d;
        completedBatches = manualBatches = arrivals = edgeArrivals = 0;
        usedModules.Clear();
        RaiseChanged();
    }

    private bool ValidChallenge(ChallengeDefinition challenge)
    {
        return Ready && ProgressionCatalog.Unique(challenge, config.challenges, item => item.id) &&
            Enum.IsDefined(typeof(ChallengeMetric), challenge.metric);
    }

    private static long AddCount(long current, int amount)
    {
        return current > long.MaxValue - amount ? long.MaxValue : current + amount;
    }

    private void RaiseChanged()
    {
        changing = true;
        try
        {
            // Un panel defectuoso no debe interrumpir un pago ya confirmado.
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
            {
                try { listener(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
        finally { changing = false; }
    }
}

// Validaciones compartidas sin búsquedas de escena ni dependencia de Resources.
internal static class ProgressionCatalog
{
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    internal static bool Unique<T>(T item, T[] catalog, Func<T, string> id) where T : UnityEngine.Object
    {
        if (item == null || catalog == null || string.IsNullOrWhiteSpace(id(item))) return false;
        int matches = 0;
        bool member = false;
        foreach (T entry in catalog)
        {
            if (entry == null || !string.Equals(id(entry), id(item), StringComparison.Ordinal)) continue;
            matches++;
            if (entry == item) member = true;
        }
        return member && matches == 1;
    }

    internal static bool Contains<T>(T item, T[] catalog) where T : UnityEngine.Object
    {
        if (item == null || catalog == null) return false;
        foreach (T entry in catalog) if (entry == item) return true;
        return false;
    }

    internal static bool HasSkill(GameManager game, BouncyBallySettings config, SkillDefinition skill)
    {
        if (ReferenceEquals(skill, null)) return true;
        return game != null && config != null && skill != null &&
            Contains(skill, config.skills) && game.Tree != null && game.Tree.GetLevel(skill) > 0;
    }
}
