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
        if(!CanSyncBallCatalog(config,balls,out string reason))
        { EditorUtility.DisplayDialog("Ball catalog not synced",reason,"OK"); return; }
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
            if(ball==null || existing.Contains(ball)) continue;
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
    public static bool CanSyncBallCatalog(BouncyBallySettings config, BallDefinition[] balls, out string reason)
    {
        reason = null;
        var configuration=FindInScene<BatchConfigurationUI>();
        if(configuration==null)
        {
            if(config!=null && new HashSet<BallDefinition>(config.balls ?? new BallDefinition[0]).SetEquals(balls)) return true;
            reason="Open the game scene with BatchConfigurationUI before changing the ball catalog. Create and link a choice for each ball, then sync again.";
            return false;
        }
        var choices=new SerializedObject(configuration).FindProperty("choices");
        var seen=new HashSet<BallDefinition>();
        if(choices!=null && choices.arraySize==balls.Length)
        {
            for(int i=0;i<choices.arraySize;i++)
            {
                var row=choices.GetArrayElementAtIndex(i);
                var ball=row.FindPropertyRelative("ball").objectReferenceValue as BallDefinition;
                if(ball==null || !balls.Contains(ball) || seen.Contains(ball) ||
                    row.FindPropertyRelative("button").objectReferenceValue==null ||
                    row.FindPropertyRelative("label").objectReferenceValue==null ||
                    row.FindPropertyRelative("identity").objectReferenceValue==null) break;
                seen.Add(ball);
            }
            if(seen.Count==balls.Length) return true;
        }
        reason="The ball catalog was not changed: BatchConfigurationUI needs one complete choice per catalog ball. Create and link the missing scene choices, then sync again.";
        return false;
    }
    /// <summary>Crea en edición una vista a partir de un nodo existente y sincroniza sus referencias.</summary>
    public static void CreateSkillNode(SkillDefinition skill)
    {
        if (skill == null) return;
        SkillTreeController tree = FindInScene<SkillTreeController>();
        if (tree == null) { Debug.LogWarning("Skill created. Open the scene with its SkillTreeController to add its node."); return; }
        Sync();

    }
    /// <summary>Valida componentes y referencias ya existentes; no modifica la escena.</summary>
    public static List<string> SceneIssues()
    {
        var issues=new List<string>();
        var scene=SceneManager.GetActiveScene();
        Canvas canvas=FindInScene<Canvas>();
        if(canvas==null){issues.Add("Missing scene Canvas.");return issues;}
        foreach(Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            if(graphic.GetComponent<CanvasRenderer>()==null)issues.Add(graphic.name+": missing CanvasRenderer.");
        var required=new Dictionary<System.Type,string[]> {
            {typeof(GameManager),new[]{"tree","inventory","progression","collection","board","moneyText"}},
            {typeof(GamePresentation),new[]{"game","spawner","tree","workshop","progression","batchConfiguration","menu","settings","batchContent","gameTitle","motionLabel","playLabel","pinButton","skillsButton","settingsButton","playButton","menuSettingsButton","goalsButton","landingsButton","motionButton","backSettingsButton","mainMenuButton"}},
            {typeof(SkillTreeController),new[]{"game","window","graph","viewport","drag","connections","closeButton","fitButton","buyButton","title","description","price"}},
            {typeof(SkillNodeUI),new[]{"skill","button","background","icon","symbolText","statusText"}},
            {typeof(BallBatchCard),new[]{"spawner","presentation","fill","identity","launch","auto","label","autoLabel","title","details"}},
            {typeof(PinWorkshopUI),new[]{"game","presentation","inventory","root","detail","leftText","rightText","upgradeText","left","right","upgrade","closeButton","gachaButton","removeButton","cancelButton","emptyText"}},
            {typeof(BatchConfigurationUI),new[]{"game","spawner","root","openButton","closeButton","hint"}},
            {typeof(ProgressionUI),new[]{"game","presentation","spawner","stats","statistics","chart"}}
        };
        foreach(var pair in required)
        {
            Component[] components=Object.FindObjectsByType(pair.Key,FindObjectsInactive.Include,FindObjectsSortMode.None)
                .OfType<Component>().Where(x=>x.gameObject.scene==scene).ToArray();
            if(pair.Key==typeof(SkillNodeUI) || pair.Key==typeof(BallBatchCard)) continue;
            if(components.Length==0){issues.Add("Missing "+pair.Key.Name+" scene component.");continue;}
            foreach(Component component in components)
            {
                var data=new SerializedObject(component);
                foreach(string field in pair.Value)
                {
                    SerializedProperty property=data.FindProperty(field);
                    if(property==null||property.objectReferenceValue==null)issues.Add(component.name+": missing "+field+".");
                }
            }
        }
        BouncyBallySettings config=AssetDatabase.LoadAssetAtPath<BouncyBallySettings>("Assets/Resources/BouncyBallySettings.asset");
        SkillTreeController tree=FindInScene<SkillTreeController>();
        if(tree!=null)
        {
            var treeData=new SerializedObject(tree);
            var views=treeData.FindProperty("nodeViews");
            if(views==null) issues.Add("Skill tree: missing nodeViews field.");
            else for(int i=0;i<views.arraySize;i++)
            {
                var node=views.GetArrayElementAtIndex(i).objectReferenceValue as SkillNodeUI;
                if(node==null || !node.HasSceneReferences) issues.Add("Skill tree: incomplete nodeViews["+i+"].");
                else if(node.Rect==null) issues.Add("Skill tree: nodeViews["+i+"] needs a RectTransform.");
            }
        }
        GamePresentation presentation=FindInScene<GamePresentation>();
        if(presentation!=null)
        {
            var cards=new SerializedObject(presentation).FindProperty("cards");
            if(cards==null) issues.Add("Game presentation: missing cards field.");
            else for(int i=0;i<cards.arraySize;i++)
            {
                var card=cards.GetArrayElementAtIndex(i).objectReferenceValue as BallBatchCard;
                if(card==null){issues.Add("Game presentation: missing cards["+i+"].");continue;}
                if(card.Rect==null || card.BatchIndex<0 || card.BatchIndex>=BallSpawner.MaxBatches)
                    issues.Add(card.name+": invalid batch index or RectTransform.");
                var cardData=new SerializedObject(card);
                foreach(string field in required[typeof(BallBatchCard)])
                    if(cardData.FindProperty(field).objectReferenceValue==null) issues.Add(card.name+": missing "+field+".");
            }
        }
        BatchConfigurationUI configuration=FindInScene<BatchConfigurationUI>();
        if(configuration!=null)
        {
            var data=new SerializedObject(configuration);
            foreach(string field in new[]{"slots","choices"})
            {
                SerializedProperty array=data.FindProperty(field);
                int expected=field=="slots" ? BallSpawner.MaxBatches : (config!=null && config.balls!=null ? config.balls.Length : -1);
                if(array==null){issues.Add("Batch configuration: missing "+field+".");continue;}
                if(expected>=0 && array.arraySize!=expected) issues.Add("Batch configuration: "+field+" must match "+(field=="slots" ? "batch capacity" : "the ball catalog")+".");
                var seen=new HashSet<Object>();
                for(int index=0;index<array.arraySize;index++)
                {
                    var row=array.GetArrayElementAtIndex(index);
                    foreach(string reference in field=="slots"?new[]{"button","label","identity"}:new[]{"ball","button","label","identity"})
                    {
                        var property=row.FindPropertyRelative(reference);
                        if(property==null || property.objectReferenceValue==null) issues.Add("Batch configuration: missing "+field+"["+index+"]."+reference);
                    }
                    if(field=="choices")
                    {
                        var ball=row.FindPropertyRelative("ball").objectReferenceValue;
                        if(ball!=null && (!seen.Add(ball) || (config!=null && config.balls!=null && !config.balls.Contains(ball))))
                            issues.Add("Batch configuration: duplicate or unregistered ball in choices["+index+"].");
                    }
                }
            }
        }
        return issues;
    }
    private static void SetArray<T>(SerializedObject data,string field,T[] values)where T:Object
    {SerializedProperty list=data.FindProperty(field);list.arraySize=values.Length;for(int i=0;i<values.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    public static void Assign(Object target,params object[] fields)
    {
        Undo.RecordObject(target,"Link Bouncy Bally references");SerializedObject data=new SerializedObject(target);
        for(int i=0;i<fields.Length;i+=2){SerializedProperty field=data.FindProperty((string)fields[i]);if(field!=null)field.objectReferenceValue=fields[i+1]as Object;}
        data.ApplyModifiedProperties();
    }
}
