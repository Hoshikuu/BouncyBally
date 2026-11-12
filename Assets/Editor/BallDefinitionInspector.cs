using UnityEditor;

/// <summary>Muestra los parámetros del script personalizado junto a la definición de la bola.</summary>
[CustomEditor(typeof(BallDefinition))]
public class BallDefinitionInspector : Editor
{
    private Editor behaviorEditor;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var ball = (BallDefinition)target;
        if (ball.CustomBehavior == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom behavior", EditorStyles.boldLabel);
        CreateCachedEditor(ball.CustomBehavior, null, ref behaviorEditor);
        behaviorEditor.OnInspectorGUI();
    }
    private void OnDisable()
    {
        if (behaviorEditor != null) DestroyImmediate(behaviorEditor);
    }
}
