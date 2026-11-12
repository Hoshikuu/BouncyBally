using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Plantillas y enlace posterior a la compilación; nunca sobrescribe scripts existentes.</summary>
[InitializeOnLoad]
public static class GaltonScriptTemplates
{
    private static string PendingKey => "Galton.Editor.PendingScript.v2." + Application.dataPath;
    [Serializable] private class Pending
    {
        public string className;
        public string targetPath;
        public bool ball;
    }

    static GaltonScriptTemplates() => EditorApplication.delayCall += CompletePending;
    public static bool HasPending => EditorPrefs.HasKey(PendingKey);

    public static void Generate(Object target, bool ball)
    {
        if (target == null || EditorApplication.isPlayingOrWillChangePlaymode || HasPending) return;
        string path = EditorUtility.SaveFilePanelInProject("Create " + (ball ? "ball" : "skill") + " script",
            ball ? "MyBall" : "MySkillAction", "cs", "Choose a new PascalCase class name.");
        if (string.IsNullOrEmpty(path)) return;
        string className = Path.GetFileNameWithoutExtension(path);
        if (!Regex.IsMatch(className, "^[A-Z][A-Za-z0-9_]*$") || File.Exists(path) ||
            TypeCache.GetTypesDerivedFrom<Object>().Any(x => x.Name == className))
        {
            EditorUtility.DisplayDialog("Invalid name", "Use a unique PascalCase class name and a new file.", "OK");
            return;
        }
        string source = ball
            ? "using UnityEngine;\n\n/// <summary>Comportamiento propio de esta bola; movimiento y pago permanecen en BallController.</summary>\npublic class " + className + " : BallBase\n{\n    public override double AddPinBonus(double amount)\n    {\n        // Modificar aquí el bonus si esta bola necesita una regla especial.\n        return base.AddPinBonus(amount);\n    }\n}\n"
            : "using UnityEngine;\n\n/// <summary>Efecto personalizado; el controlador realiza el cobro antes de Apply.</summary>\n[CreateAssetMenu(menuName = \"Bouncy Bally/Actions/" + className + "\")]\npublic class " + className + " : SkillAction\n{\n    // Cambiar a una condición real después de implementar Apply. Evita cobrar una plantilla vacía.\n    public override bool CanApply(GameManager game, SkillDefinition skill) => false;\n\n    public override void Apply(GameManager game, SkillDefinition skill)\n    {\n        // Aplicar exactamente un nivel. No modificar este asset durante Play.\n    }\n}\n";
        File.WriteAllText(path, source);
        EditorPrefs.SetString(PendingKey, JsonUtility.ToJson(new Pending {
            className = className, targetPath = AssetDatabase.GetAssetPath(target), ball = ball }));
        AssetDatabase.ImportAsset(path);
    }

    /// <summary>Reintenta tras recargar dominio. Si la compilación falla, conserva la solicitud.</summary>
    public static void CompletePending()
    {
        if (!HasPending || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CompletePending;
            return;
        }
        Pending pending = JsonUtility.FromJson<Pending>(EditorPrefs.GetString(PendingKey));
        Type type = (pending.ball ? TypeCache.GetTypesDerivedFrom<BallBase>().AsEnumerable() :
            TypeCache.GetTypesDerivedFrom<SkillAction>().AsEnumerable()).FirstOrDefault(x => x.Name == pending.className);
        if (type == null) return;
        if (pending.ball)
        {
            BallDefinition definition = AssetDatabase.LoadAssetAtPath<BallDefinition>(pending.targetPath);
            if (definition == null || definition.Prefab == null) return;
            string prefabPath = AssetDatabase.GetAssetPath(definition.Prefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                BallController movement = contents.GetComponent<BallController>();
                string movementData = EditorJsonUtility.ToJson(movement);
                // Se retira primero el controlador que requiere BallBase para sustituir su variante.
                Object.DestroyImmediate(movement);
                foreach (BallBase old in contents.GetComponents<BallBase>()) Object.DestroyImmediate(old);
                contents.AddComponent(type);
                BallController replacement = contents.AddComponent<BallController>();
                EditorJsonUtility.FromJsonOverwrite(movementData, replacement);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            // Recrear el componente cambia su fileID: se actualiza explícitamente la referencia del asset.
            GaltonSceneBuilder.Assign(definition, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<BallController>());
        }
        else
        {
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(pending.targetPath);
            if (skill == null) return;
            SkillAction action = ScriptableObject.CreateInstance(type) as SkillAction;
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(pending.targetPath).Replace('\\', '/') + "/" + pending.className + ".asset");
            AssetDatabase.CreateAsset(action, path);
            SerializedObject serialized = new SerializedObject(skill);
            serialized.FindProperty("customAction").objectReferenceValue = action;
            serialized.FindProperty("effect").enumValueIndex = (int)SkillEffect.Custom;
            serialized.ApplyModifiedProperties();
        }
        EditorPrefs.DeleteKey(PendingKey);
        AssetDatabase.SaveAssets();
    }

    public static void CancelPending() => EditorPrefs.DeleteKey(PendingKey);
}
