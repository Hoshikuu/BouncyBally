using UnityEngine;

/// <summary>Acceso a las variantes sin añadir nodos ni modificar el catálogo de habilidades.</summary>
public class BallTypeSelector : MonoBehaviour
{
    private BallSpawner spawner;
    private bool open;
    private int batch;
    private Vector2 scroll;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (var spawner in Object.FindObjectsByType<BallSpawner>(FindObjectsSortMode.None))
            if (spawner.GetComponent<BallTypeSelector>() == null) spawner.gameObject.AddComponent<BallTypeSelector>();
    }
    private void Awake() { spawner = GetComponent<BallSpawner>(); }
    private void OnGUI()
    {
        if (spawner == null || GameManager.Instance == null || Time.timeScale <= 0) return;
        if (GUI.Button(new Rect(12, Screen.height - 44, 180, 32), "Tipos de bolas")) open = !open;
        if (!open) return;
        GUILayout.BeginArea(new Rect(12, Mathf.Max(12, Screen.height - 500), 290, 440), GUI.skin.box);
        GUILayout.Label("Selecciona una tanda y un tipo");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < BallSpawner.MaxBatches; i++)
        { GUI.enabled = spawner.IsBatchUnlocked(i); if (GUILayout.Button((batch == i ? "• " : "") + (i + 1))) batch = i; }
        GUILayout.EndHorizontal(); GUI.enabled = true;
        BallTrick.FirstBounceRight = GUILayout.Toggle(BallTrick.FirstBounceRight, "Trick: primer rebote a la derecha");
        GUILayout.Label("Planos: " + BallPlans.Count);
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var ball in spawner.Balls)
        {
            GUI.enabled = GameManager.Instance.IsBallUnlocked(ball) && spawner.GetState(batch).Phase == BallBatchPhase.Ready;
            if (GUILayout.Button(ball.Title)) spawner.AssignBall(batch, ball);
        }
        GUI.enabled = true;
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }
}
