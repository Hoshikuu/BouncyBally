using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cuatro tandas independientes, cada una con un tipo de bola y recarga propios.</summary>
public class BallSpawner : MonoBehaviour
{
    public const int MaxBatches = 4;
    [SerializeField] private GaltonBoard board;
    [SerializeField] private SkillTreeController skillTree;
    [SerializeField] private BallDefinition[] batchBalls = new BallDefinition[MaxBatches];
    private readonly BallBatchState[] states = new BallBatchState[MaxBatches];
    private sealed class Result { public int slot, edges, landed; public double reward; public bool manual; }
    private readonly Dictionary<int, Result> results = new Dictionary<int, Result>();
    private GameManager economy;
    private int pendingBalls, nextBatch;
    public int PendingBalls => pendingBalls;
    public IReadOnlyList<BallDefinition> Balls => skillTree.Balls;
    public event System.Action ConfigurationChanged;
    public event System.Action<double> BatchCompleted;
    public event System.Action<int> BallLanded;
    public event System.Action<bool> Launched;
    private void Awake()
    {
        if (batchBalls == null || batchBalls.Length != MaxBatches) System.Array.Resize(ref batchBalls, MaxBatches);
        for (int i = 0; i < MaxBatches; i++) states[i] = new BallBatchState();
    }
    private void Start()
    {
        economy = GameManager.Instance;
        if (economy == null || board == null || skillTree == null) { enabled = false; return; }
        BallDefinition basic = null;
        foreach (BallDefinition type in Balls) if (type != null && type.StartsUnlocked) { basic = type; break; }
        for (int i = 0; i < MaxBatches; i++)
            if (batchBalls[i] == null || !economy.IsBallUnlocked(batchBalls[i])) batchBalls[i] = basic;
        ConfigurationChanged?.Invoke();
    }
    public bool IsBatchUnlocked(int index) => index >= 0 && index < MaxBatches && GameManager.Instance != null && index < GameManager.Instance.UnlockedBatches;
    public BallDefinition GetBall(int index) => index >= 0 && index < MaxBatches ? batchBalls[index] : null;
    public BallBatchState GetState(int index) => index >= 0 && index < MaxBatches ? states[index] : null;
    /// <summary>Solo permite asignaciones del catálogo, desbloqueadas y con la tanda en reposo.</summary>
    public bool AssignBall(int index, BallDefinition type)
    {
        if (!IsBatchUnlocked(index) || type == null || !economy.IsBallUnlocked(type) || states[index].Phase != BallBatchPhase.Ready) return false;
        bool found = false;
        foreach (BallDefinition entry in Balls) if (entry == type) { found = true; break; }
        if (!found) return false;
        batchBalls[index] = type;
        // Un cambio de tipo requiere volver a activar Auto de forma explícita.
        if (states[index].Auto) states[index].ToggleAuto();
        ConfigurationChanged?.Invoke(); return true;
    }
    public bool CanLaunch(int index)
    {
        BallDefinition type = GetBall(index);
        return Time.timeScale > 0 && isActiveAndEnabled && economy != null && board != null && board.enabled &&
            IsBatchUnlocked(index) && type != null && type.Prefab != null && economy.IsBallUnlocked(type) && states[index].Phase == BallBatchPhase.Ready &&
            pendingBalls + economy.GetBallCount(type) <= economy.Settings.maxActiveBalls && (board.RequestedRows == board.Rows || pendingBalls == 0);
    }
    /// <summary>Botones, teclado y Auto usan la misma validación y el mismo lanzamiento.</summary>
    public bool TryLaunch(int index, bool manual = true)
    {
        if (!CanLaunch(index)) return false;
        if (pendingBalls == 0) board.ApplyPendingExpansion();
        BallDefinition type = batchBalls[index]; BallBatchState state = states[index];
        if (!state.TryBegin(economy.GetBallCount(type), economy.GetCooldown(type), economy.Settings.rechargeDuringFlight)) return false;
        int id = ++nextBatch;
        results.Add(id, new Result { slot = index, manual = manual }); pendingBalls += state.Total;
        if (manual) economy.Progression.NotifyManualLaunch();
        StartCoroutine(Spawn(type, state.Total, economy.GetBallValue(type), economy.GetBallSpeed(type), economy.GetPinBonus(type), economy.GetSlotBonus(type), economy.GetRewardFactor(type), id));
        Launched?.Invoke(manual); return true;
    }
    private IEnumerator Spawn(BallDefinition type, int count, double value, float speed, double pin, double slot, double reward, int id)
    {
        for (int i = 0; i < count; i++)
        {
            BallController ball = Instantiate(type.Prefab, board.GetSpawnPosition(), Quaternion.identity);
            ball.Setup(board, this, value, speed, pin, slot, type, reward, id);
            if (i + 1 < count) yield return new WaitForSeconds(economy.Settings.spawnInterval);
        }
    }
    public void RecordResult(int id, int column, double reward)
    {
        if (results.TryGetValue(id, out Result result)) { result.landed++; result.reward += reward; if (column == 0 || column == board.Rows) result.edges++; }
        BallLanded?.Invoke(column);
    }
    public void BallFinished(int id)
    {
        if (!results.TryGetValue(id, out Result result)) return;
        BallBatchState state = states[result.slot];
        if (!state.CompleteBall()) return;
        pendingBalls = Mathf.Max(0, pendingBalls - 1);
        if (state.Remaining == 0)
        {
            results.Remove(id); board.EndBatch(id); economy.ReportBatch(result.reward);
            economy.Progression.NotifyBatch(result.reward, result.landed, result.edges, result.manual);
            BatchCompleted?.Invoke(result.reward);
        }
        if (pendingBalls == 0) board.ApplyPendingExpansion();
    }
    public void ToggleAuto(int index)
    {
        if (IsBatchUnlocked(index) && economy != null && GetBall(index) != null && economy.HasAutoLaunch(GetBall(index))) states[index].ToggleAuto();
    }
    private void Update()
    {
        if (economy == null || skillTree == null) return;
        for (int i = 0; i < MaxBatches; i++)
        {
            states[i].Advance(Time.deltaTime);
            BallDefinition type = GetBall(i);
            if (type != null && states[i].Auto && economy.HasAutoLaunch(type)) TryLaunch(i, false);
        }
    }
}
