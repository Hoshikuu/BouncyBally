using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Retira un asset, sus referencias y sus vistas de las escenas abiertas.</summary>
public static class GaltonAssetDeletion
{
    public static bool Delete(Object target)
    {
        if (target == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        string path = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
        // Solo se elimina el prefab de bola si tiene el mismo nombre y no es compartido.
        string generatedPrefab = null;
        if (target is BallDefinition ball && ball.Prefab != null)
        {
            string candidate = AssetDatabase.GetAssetPath(ball.Prefab);
            if (!string.IsNullOrEmpty(candidate) && candidate == Path.ChangeExtension(path, ".prefab"))
            {
                bool shared = AssetDatabase.GetAllAssetPaths().Any(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                    p != path && p != candidate && !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                    AssetDatabase.GetDependencies(p, false).Contains(candidate));
                if (!shared) generatedPrefab = candidate;
            }
        }
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(s => s.isLoaded).ToArray();
        var loadedPaths = new HashSet<string>(scenes.Select(s => s.path));
        var blockers = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != path &&
            ((p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !loadedPaths.Contains(p)) ||
             p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) &&
            AssetDatabase.GetDependencies(p, false).Contains(path)).ToArray();
        if (blockers.Length > 0)
        {
            EditorUtility.DisplayDialog("Cannot delete yet", "These closed scenes or prefabs reference this asset. Open the scenes and remove prefab references before deleting:\n\n" + string.Join("\n", blockers), "OK");
            return false;
        }
        if (!EditorUtility.DisplayDialog("Delete " + target.name + "?", "Remove the asset, its references and its scene views? This moves the asset to the system trash. Save affected scenes after checking the result.", "Delete", "Cancel")) return false;

        // Primero se identifican vistas antes de limpiar referencias serializadas.
        var sceneObjects = scenes.SelectMany(s => s.GetRootGameObjects())
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(c => c != null).ToArray();
        var views = new HashSet<GameObject>();
        if (target is SkillDefinition skill)
            foreach (var node in sceneObjects.OfType<SkillNodeUI>())
                if (node.Skill == skill) views.Add(node.gameObject);
        foreach (var component in sceneObjects)
        {
            var data = new SerializedObject(component);
            if (target is BallDefinition) RemoveRows(data, "choices", "ball", target, views);
            if (target is PinModifierDefinition) RemoveRows(data, "moduleRows", "modifier", target, views);
            if (target is ChallengeDefinition) RemoveRows(data, "rows", "goal", target, views);
            if (target is GachaDefinition) RemoveRows(data, "boxes", "box", target, views);
            data.ApplyModifiedProperties();
        }
        // Las listas del controlador no deben conservar nodos destruidos.
        foreach (var tree in sceneObjects.OfType<SkillTreeController>())
        {
            var data = new SerializedObject(tree);
            var nodes = data.FindProperty("nodeViews");
            if (nodes != null) for (int i = nodes.arraySize - 1; i >= 0; --i)
            {
                var node = nodes.GetArrayElementAtIndex(i).objectReferenceValue as SkillNodeUI;
                if (node != null && views.Contains(node.gameObject)) RemoveArrayElement(nodes, i);
            }
            data.ApplyModifiedProperties();
        }
        foreach (GameObject view in views) if (view != null) Undo.DestroyObjectImmediate(view);

        // Incluye subassets: requisitos, catálogos, destinos y listas de recompensas.
        foreach (string assetPath in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && p != path))
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset is ScriptableObject) ClearReferences(asset, target);
        foreach (var component in sceneObjects)
            if (component != null) ClearReferences(component, target);
        foreach (var scene in scenes) if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.MoveAssetToTrash(path))
        {
            EditorUtility.DisplayDialog("Delete failed", "The asset could not be moved to the trash. Review the unsaved scene changes and restore links if necessary.", "OK");
            return false;
        }
        if (generatedPrefab != null && !AssetDatabase.MoveAssetToTrash(generatedPrefab))
            Debug.LogWarning("Deleted the ball asset, but could not remove its generated prefab: " + generatedPrefab);
        AssetDatabase.SaveAssets();
        return true;
    }

    // Las filas serializadas son estructuras; retirar toda la fila evita dejar referencias incompletas.
    private static void RemoveRows(SerializedObject data, string field, string reference, Object target, HashSet<GameObject> views)
    {
        var array = data.FindProperty(field);
        if (array == null || !array.isArray) return;
        for (int i = array.arraySize - 1; i >= 0; --i)
        {
            var row = array.GetArrayElementAtIndex(i);
            var item = row.FindPropertyRelative(reference);
            if (item == null || item.objectReferenceValue != target) continue;
            var button = row.FindPropertyRelative("button") ?? row.FindPropertyRelative("claim") ?? row.FindPropertyRelative("open");
            if (button != null && button.objectReferenceValue is Component control)
            {
                // Solo se retira el GameObject si las demás vistas de la fila son descendientes.
                bool owned = true;
                foreach (string name in new[] { "label", "identity", "preview", "claimLabel", "openLabel", "odds" })
                {
                    var property = row.FindPropertyRelative(name);
                    if (property != null && property.objectReferenceValue is Component child && !child.transform.IsChildOf(control.transform)) { owned = false; break; }
                }
                if (owned) views.Add(control.gameObject);
                else Undo.RecordObject(control.gameObject, "Hide deleted item view");
                if (!owned) control.gameObject.SetActive(false);
            }
            RemoveArrayElement(array, i);
        }
    }

    private static void RemoveArrayElement(SerializedProperty array, int index)
    {
        int size = array.arraySize;
        array.DeleteArrayElementAtIndex(index);
        if (array.arraySize == size) array.DeleteArrayElementAtIndex(index);
    }

    private static void ClearReferences(Object owner, Object target)
    {
        var data = new SerializedObject(owner);
        var it = data.GetIterator();
        var arrayPaths = new List<string>();
        while (it.Next(true))
        {
            if (it.isArray && it.propertyType != SerializedPropertyType.String && it.arraySize > 0 &&
                it.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ObjectReference)
                arrayPaths.Add(it.propertyPath);
        }
        foreach (string path in arrayPaths)
        {
            var array = data.FindProperty(path);
            if (array == null) continue;
            for (int i = array.arraySize - 1; i >= 0; --i)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) RemoveArrayElement(array, i);
        }
        it = data.GetIterator();
        while (it.Next(true))
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == target)
                it.objectReferenceValue = null;
        if (data.ApplyModifiedProperties()) EditorUtility.SetDirty(owner);
    }
}
