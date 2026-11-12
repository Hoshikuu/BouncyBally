using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>Sorteo ponderado sin reemplazo; todas las gachas comparten los IDs obtenidos.</summary>
public class RewardCollection : MonoBehaviour
{
    private GameManager game;
    private PinModuleInventory inventory;
    private ProgressionSystem progress;
    private BouncyBallySettings config;
    private readonly HashSet<string> claimed = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> claimedOrder = new List<string>();
    private ReadOnlyCollection<string> claimedView;
    private bool opening;

    public event Action<RewardDefinition> Opened;
    public event Action Changed;
    public IReadOnlyCollection<string> ClaimedIds => claimedView ?? (claimedView = claimedOrder.AsReadOnly());

    public void Initialize(GameManager game, PinModuleInventory inventory, ProgressionSystem progress,
        BouncyBallySettings config)
    {
        if (opening) return;
        this.game = game;
        this.inventory = inventory;
        this.progress = progress;
        this.config = config;
    }

    private bool Ready => game != null && inventory != null && progress != null && config != null &&
        progress.IsInitializedFor(game, inventory, config);

    public int Remaining(GachaDefinition gacha)
    {
        return Pool(gacha).Count;
    }

    public float Chance(GachaDefinition gacha, RewardDefinition reward)
    {
        List<RewardDefinition> pool = Pool(gacha);
        double total = 0d;
        double weight = 0d;
        foreach (RewardDefinition item in pool)
        {
            total += item.weight;
            if (item == reward) weight = item.weight;
        }
        // Probabilidad normalizada 0..1 para el mismo conjunto que usa TryOpen.
        return total > 0d ? (float)(weight / total) : 0f;
    }

    public bool CanOpen(GachaDefinition gacha)
    {
        // La consulta refleja el estado final para el HUD; TryOpen bloquea la reentrada.
        return CanPay(gacha) && Pool(gacha).Count > 0;
    }

    public bool TryOpen(GachaDefinition gacha, out RewardDefinition reward)
    {
        reward = null;
        if (opening) return false;
        opening = true;
        try
        {
            if (!CanPay(gacha)) return false;
            List<RewardDefinition> pool = Pool(gacha);
            if (pool.Count == 0) return false;
            double total = 0d;
            foreach (RewardDefinition item in pool) total += item.weight;
            double roll = UnityEngine.Random.value * total;
            RewardDefinition selected = pool[pool.Count - 1];
            foreach (RewardDefinition item in pool)
            {
                roll -= item.weight;
                if (roll < 0d) { selected = item; break; }
            }

            // Reserva antes de gastar: los callbacks ya ven el premio retirado del pool.
            string id = selected.id;
            int cost = gacha.ticketCost;
            claimed.Add(id);
            claimedOrder.Add(id);
            if (!progress.TrySpendTickets(cost))
            {
                claimed.Remove(id);
                claimedOrder.Remove(id);
                return false;
            }
            // GameManager es el único propietario de la concesión, incluido inventory.Grant.
            game.ApplyCollectionReward(selected);
            reward = selected;
            RaiseOpened(selected);
            RaiseChanged();
            return true;
        }
        finally { opening = false; }
    }

    private bool CanPay(GachaDefinition gacha)
    {
        return ValidGacha(gacha) && game.WorkshopUnlocked &&
            ProgressionCatalog.HasSkill(game, config, gacha.requiredSkill) &&
            gacha.ticketCost > 0 && progress.Tickets >= gacha.ticketCost;
    }

    private bool ValidGacha(GachaDefinition gacha)
    {
        return Ready && ProgressionCatalog.Unique(gacha, config.gachas, item => item.id);
    }

    private List<RewardDefinition> Pool(GachaDefinition gacha)
    {
        var pool = new List<RewardDefinition>();
        if (!ValidGacha(gacha) || gacha.rewards == null) return pool;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (RewardDefinition item in gacha.rewards)
        {
            if (ValidReward(item) && !claimed.Contains(item.id) && seen.Add(item.id) &&
                game.CanGrantCollectionReward(item)) pool.Add(item);
        }
        return pool;
    }

    private bool ValidReward(RewardDefinition reward)
    {
        if (reward == null || string.IsNullOrWhiteSpace(reward.id) ||
            !ProgressionCatalog.Finite(reward.weight) || reward.weight <= 0f ||
            !ProgressionCatalog.Finite(reward.amount) || reward.amount <= 0d) return false;

        // Compartir el mismo asset es válido; dos assets con un ID igual son ambiguos.
        foreach (GachaDefinition gacha in config.gachas)
        {
            if (gacha == null || gacha.rewards == null) continue;
            foreach (RewardDefinition item in gacha.rewards)
                if (item != null && item != reward &&
                    string.Equals(item.id, reward.id, StringComparison.Ordinal)) return false;
        }
        switch (reward.effect)
        {
            case CollectionRewardEffect.Modifier:
                return ProgressionCatalog.Unique(reward.modifier, config.modifiers, item => item.id);
            case CollectionRewardEffect.UnlockBall:
                return ProgressionCatalog.Contains(reward.ball, config.balls);
            case CollectionRewardEffect.BallValue:
            case CollectionRewardEffect.RewardFactor:
            case CollectionRewardEffect.PinBonus:
            case CollectionRewardEffect.SlotBonus:
                return ReferenceEquals(reward.ball, null) || ProgressionCatalog.Contains(reward.ball, config.balls);
            default: return false;
        }
    }

    private void RaiseOpened(RewardDefinition reward)
    {
        if (Opened == null) return;
        foreach (Action<RewardDefinition> listener in Opened.GetInvocationList())
        {
            try { listener(reward); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private void RaiseChanged()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
