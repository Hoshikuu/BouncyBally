using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Sincroniza catálogos y vistas de habilidades durante la edición.</summary>
public static class GaltonSceneBuilder
{
    public static T FindInScene<T>() where T:Component=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    public static T[] Load<T>() where T:Object=>AssetDatabase.FindAssets("t:"+typeof(T).Name,new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x,System.StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x=>x!=null).Distinct().ToArray();
    public static void Sync()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        BouncyBallySettings config=AssetDatabase.LoadAssetAtPath<BouncyBallySettings>("Assets/Resources/BouncyBallySettings.asset");
        if(config==null){EditorUtility.DisplayDialog("Bouncy Bally","Missing settings asset.","OK");return;}
        BallDefinition[] balls=Load<BallDefinition>();
        CreateMissingBallChoices(balls);
        Undo.RecordObject(config,"Sync Bouncy Bally catalogs");
        config.balls=balls;config.skills=Load<SkillDefinition>();config.modifiers=Load<PinModifierDefinition>();config.challenges=Load<ChallengeDefinition>();config.gachas=Load<GachaDefinition>();
        EditorUtility.SetDirty(config);
        SkillTreeController tree=FindInScene<SkillTreeController>();
        if(tree!=null)
        {
            SerializedObject data=new SerializedObject(tree);
            SetArray(data,"balls",config.balls);SetArray(data,"skills",config.skills);
            RectTransform graph=data.FindProperty("graph").objectReferenceValue as RectTransform;
            SerializedProperty registered=data.FindProperty("nodeViews");
            var nodes=new List<SkillNodeUI>();
            for(int i=0;i<registered.arraySize;i++)
            {
                var node=registered.GetArrayElementAtIndex(i).objectReferenceValue as SkillNodeUI;
                if(node!=null && node.gameObject.activeSelf && node.HasSceneReferences && node.Rect!=null &&
                    graph!=null && node.transform.IsChildOf(graph) && config.skills.Contains(node.Skill) && !nodes.Contains(node))
                    nodes.Add(node);
            }
            SkillNodeUI template=nodes.FirstOrDefault();
            if(template!=null)
            {
                foreach(SkillDefinition skill in config.skills)
                    if(skill!=null && !nodes.Any(n=>n.Skill==skill))
                    {
                        GameObject copy=Object.Instantiate(template.gameObject,template.transform.parent);
                        copy.name=skill.name;
                        Undo.RegisterCreatedObjectUndo(copy,"Create skill node");
                        SkillNodeUI node=copy.GetComponent<SkillNodeUI>();
                        Assign(node,"skill",skill);
                        nodes.Add(node);
                    }
            }
            else if(config.skills.Length>0)
                Debug.LogWarning("Cannot create skill views: nodeViews needs an active, complete registered template under the graph.",tree);
            SetArray(data,"nodeViews",nodes.ToArray());data.ApplyModifiedProperties();
            foreach(SkillNodeUI node in nodes)
            {
                if(node.Skill==null || node.Rect==null)continue;
                Undo.RecordObject(node.Rect,"Move existing skill node");
                node.Rect.anchorMin=node.Rect.anchorMax=node.Skill.Position;
                node.Rect.anchoredPosition=Vector2.zero;
                node.Rect.sizeDelta=Vector2.one*config.nodeSize;
            }
            if(graph!=null){Undo.RecordObject(graph,"Resize existing tree");graph.sizeDelta=config.graphSize;}
            EditorSceneManager.MarkSceneDirty(tree.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
    }
    /// <summary>Duplica una opción existente en edición para cada bola nueva.</summary>
    public static void CreateMissingBallChoices(BallDefinition[] balls)
    {
        var configuration=FindInScene<BatchConfigurationUI>();
        if(configuration==null) return;
        var data=new SerializedObject(configuration);
        var choices=data.FindProperty("choices");
        if(choices==null || choices.arraySize==0) return;
        var first=choices.GetArrayElementAtIndex(0);
        var template=first.FindPropertyRelative("button").objectReferenceValue as Button;
        var label=first.FindPropertyRelative("label").objectReferenceValue as TMPro.TMP_Text;
        var identity=first.FindPropertyRelative("identity").objectReferenceValue as Image;
        if(template==null || label==null || identity==null || !label.transform.IsChildOf(template.transform) || !identity.transform.IsChildOf(template.transform)) return;
        string labelPath=AnimationUtility.CalculateTransformPath(label.transform,template.transform);
        string identityPath=AnimationUtility.CalculateTransformPath(identity.transform,template.transform);
        var existing=new HashSet<BallDefinition>();
        for(int i=0;i<choices.arraySize;i++)
        {
            var item=choices.GetArrayElementAtIndex(i).FindPropertyRelative("ball").objectReferenceValue as BallDefinition;
            if(item!=null) existing.Add(item);
        }
        foreach(var ball in balls)
        {
            if(ball==null || ball.RandomOnly || existing.Contains(ball)) continue;
            var clone=Object.Instantiate(template.gameObject,template.transform.parent);
            clone.name=ball.name+" Choice";
            Undo.RegisterCreatedObjectUndo(clone,"Create ball choice");
            var clonedLabel=(string.IsNullOrEmpty(labelPath)?clone.transform:clone.transform.Find(labelPath));
            var clonedIdentity=(string.IsNullOrEmpty(identityPath)?clone.transform:clone.transform.Find(identityPath));
            int index=choices.arraySize;
            choices.InsertArrayElementAtIndex(index);
            var row=choices.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("ball").objectReferenceValue=ball;
            row.FindPropertyRelative("button").objectReferenceValue=clone.GetComponent<Button>();
            row.FindPropertyRelative("label").objectReferenceValue=clonedLabel!=null?clonedLabel.GetComponent<TMPro.TMP_Text>():null;
            row.FindPropertyRelative("identity").objectReferenceValue=clonedIdentity!=null?clonedIdentity.GetComponent<Image>():null;
            var rect=clone.transform as RectTransform;
            if(rect!=null && template.transform is RectTransform original)
                rect.anchoredPosition=original.anchoredPosition+new Vector2((rect.rect.width+14)*(index%4),-(rect.rect.height+10)*(index/4));
            existing.Add(ball);
        }
        data.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(configuration.gameObject.scene);
    }
    /// <summary>Evita cambiar el catálogo si la escena no dispone de una opción completa por bola.</summary>
    public static void CreateSkillNode(SkillDefinition skill)
    {
        if (skill == null) return;
        SkillTreeController tree = FindInScene<SkillTreeController>();
        if (tree == null) { Debug.LogWarning("Skill created. Open the scene with its SkillTreeController to add its node."); return; }
        Sync();

    }
    /// <summary>Valida componentes y referencias ya existentes; no modifica la escena.</summary>
    private static void SetArray<T>(SerializedObject data,string field,T[] values)where T:Object
    {SerializedProperty list=data.FindProperty(field);list.arraySize=values.Length;for(int i=0;i<values.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    public static void Assign(Object target,params object[] fields)
    {
        Undo.RecordObject(target,"Link Bouncy Bally references");SerializedObject data=new SerializedObject(target);
        for(int i=0;i<fields.Length;i+=2){SerializedProperty field=data.FindProperty((string)fields[i]);if(field!=null)field.objectReferenceValue=fields[i+1]as Object;}
        data.ApplyModifiedProperties();
    }
}
