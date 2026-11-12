using System;

public enum BallBatchPhase { Ready, Dropping, CoolingDown }

/// <summary>
/// Estado de una tanda sin dependencias de Unity. La recarga solo comienza tras el último contacto final.
/// Captura cantidad y duración al lanzar; cambiar mejoras no modifica una tanda ya iniciada.
/// </summary>
public sealed class BallBatchState
{
    public BallBatchPhase Phase { get; private set; }
    public int Remaining { get; private set; }
    public int Total { get; private set; }
    public float Cooldown { get; private set; }
    public float RemainingCooldown { get; private set; }
    private bool concurrentRecharge;
    public bool Auto { get; private set; }
    public float Progress => Phase == BallBatchPhase.Ready ? 1f : Phase == BallBatchPhase.Dropping && !concurrentRecharge ? 0f :
        Math.Max(0f, Math.Min(1f, 1f - RemainingCooldown / Math.Max(0.01f, Cooldown)));

    public bool TryBegin(int count, float cooldown, bool rechargeInFlight = false)
    {
        if (Phase != BallBatchPhase.Ready || count < 1 || count > 250 ||
            float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0.5f) return false;
        concurrentRecharge = rechargeInFlight;
        Phase = BallBatchPhase.Dropping;
        Total = Remaining = count;
        Cooldown = cooldown;
        RemainingCooldown = concurrentRecharge ? cooldown : 0f;
        return true;
    }

    /// <summary>Devuelve false si la tanda ya terminó; no permite recuentos negativos.</summary>
    public bool CompleteBall()
    {
        if (Phase != BallBatchPhase.Dropping || Remaining == 0) return false;
        Remaining--;
        if (Remaining == 0)
        {
            if (!concurrentRecharge) RemainingCooldown = Cooldown;
            Phase = RemainingCooldown > 0 ? BallBatchPhase.CoolingDown : BallBatchPhase.Ready;
        }
        return true;
    }

    public void Advance(float seconds)
    {
        if ((Phase != BallBatchPhase.CoolingDown && !(Phase == BallBatchPhase.Dropping && concurrentRecharge)) || seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        RemainingCooldown = Math.Max(0f, RemainingCooldown - seconds);
        if (RemainingCooldown == 0f && Phase != BallBatchPhase.Dropping) Phase = BallBatchPhase.Ready;
    }

    public void ToggleAuto() => Auto = !Auto;
}
