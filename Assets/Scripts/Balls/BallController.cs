using System.Collections;
using UnityEngine;

/// <summary>
/// Recorre el tablero con curvas, sin Rigidbody. Coordina contactos, efectos y pago.
/// Las estadísticas y las reglas especiales pertenecen a BallBase y sus variantes.
/// </summary>
[RequireComponent(typeof(BallBase))]
public class BallController : MonoBehaviour
{
    [Header("Movimiento")]
    [Min(0.01f)] [SerializeField] private float segmentDuration = 0.3f;
    [Min(0f)] [SerializeField] private float bounceHeight = 0.55f;
    [SerializeField] private float pinContactOffset = 0.25f;
    private GaltonBoard board;
    private BallSpawner spawner;
    internal BallSpawner Owner => spawner;
    private BallDefinition type;
    private double rewardFactor;
    private int batchId;
    private bool finished;
    private BallBase ball;
    private BallVisualEffects visuals;
    private GaltonEffects effects;
    private float speed;
    private double pinBonus;
    private double slotBonus;

    /// <summary>Recibe las mejoras de esta tanda e inicia el recorrido.</summary>
    public void Setup(GaltonBoard targetBoard, BallSpawner owner, double value,
                      float movementSpeed, double extraPinBonus, double extraSlotBonus,
                      BallDefinition definition, double payoutFactor, int id = 0)
    {
        batchId = id;
        if (BouncyBallySettings.Current != null)
        { segmentDuration = BouncyBallySettings.Current.segmentDuration; transform.localScale = Vector3.one * BouncyBallySettings.Current.ballScale; }
        board = targetBoard;
        spawner = owner;
        type = definition;
        // El ajuste global de economía se aplica una sola vez a cada pago.
        rewardFactor = payoutFactor * (BouncyBallySettings.Current != null ? BouncyBallySettings.Current.payoutMultiplier : 1f);
        speed = Mathf.Max(0.01f, movementSpeed);
        pinBonus = extraPinBonus;
        slotBonus = extraSlotBonus;
        ball = GetComponent<BallBase>();
        visuals = GetComponent<BallVisualEffects>();
        effects = board.GetComponent<GaltonEffects>();
        ball.SetIdentityColor(definition != null ? definition.DisplayColor : Color.white);
        ball.Setup(value, board.Colors);
        StartCoroutine(Traverse());
    }

    /// <summary>Visita un pin por fila. Cada giro a la derecha incrementa la columna.</summary>
    private IEnumerator Traverse()
    {
        int column = 0, alternations = 0, chain = 0;
        bool? lastRight = null;
        yield return MoveTo(PinContact(0, 0), false);
        for (int row = 0; row < board.Rows; row++)
        {
            Pin pin = board.GetPin(row, column);
            bool right = Random.value < pin.RightChance;
            if (lastRight.HasValue && lastRight.Value != right) alternations++;
            lastRight = right;
            chain = pin.Modifier != null ? chain + 1 : 0;
            double bonus = pin.MultiplierBonus + pinBonus;
            if (type.Rule == BallDefinition.BallRule.Alternating) bonus += alternations * type.RuleAmount;
            double appliedBonus = ball.AddPinBonus(pin.ResolveContactBonus(bonus,batchId,alternations,chain));
            if (type.Rule == BallDefinition.BallRule.Pulse && pin.Modifier != null) GameManager.Instance.TriggerPulse(type);
            pin.PlayImpact();
            if (effects != null)
                effects.PlayPin(pin.transform.position, pin.CurrentColor, appliedBonus);
            if (visuals != null)
                visuals.PlayBounce();
            if (right)
                column++;
            Vector3 target = row == board.Rows - 1
                ? board.GetSlotPosition(column)
                : PinContact(row + 1, column);
            yield return MoveTo(target, true);
        }

        // Cada recorrido calcula y entrega su recompensa una sola vez.
        double slotMultiplier = board.GetMultiplier(column);
        double reward = ball.ComputeReward(slotMultiplier + slotBonus) * rewardFactor * GameManager.Instance.PulseFactor(type);
        GameManager.Instance.AddMoney(reward);
        spawner.RecordResult(batchId, column, reward);
        if (effects != null)
        {
            Color color = board.Colors != null ? board.Colors.ForSlot(slotMultiplier) : Color.white;
            effects.PlaySlot(board.GetSlotPosition(column), color, reward);
        }
        yield return new WaitForSeconds(0.15f);
        Finish();
        Destroy(gameObject);
    }

    // Cuenta también la destrucción externa para que ninguna tanda quede bloqueada.
    private void OnDestroy() => Finish();

    /// <summary>Descarta la bola sin registrar una llegada al reiniciar la partida.</summary>
    internal void CancelForReset()
    {
        finished = true;
        StopAllCoroutines();
        Destroy(gameObject);
    }

    private void Finish()
    {
        if (finished || spawner == null || type == null) return;
        finished = true;
        spawner.BallFinished(batchId);
    }

    private Vector3 PinContact(int row, int column)
    {
        return board.GetPinPosition(row, column) + Vector3.up * pinContactOffset;
    }

    /// <summary>Interpola en línea recta o en una curva de Bézier con el centro elevado.</summary>
    private IEnumerator MoveTo(Vector3 target, bool bounce)
    {
        Vector3 start = transform.position;
        Vector3 control = (start + target) * 0.5f + Vector3.up * bounceHeight;
        float duration = Mathf.Max(0.01f, segmentDuration) / speed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = bounce
                ? Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, target, t), t)
                : Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.position = target;
    }
}
