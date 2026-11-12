using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Autoría de catálogos y balance; el gráfico no depende de vistas de escena.</summary>
public class GaltonEditorWindow : EditorWindow
{
    private const string SettingsPath = "Assets/Resources/BouncyBallySettings.asset";
    private static readonly string[] Tabs = { "Settings", "Balls", "Skills", "Tree", "Pins", "Goals", "Gachas", "Rewards", "Scene" };
    private static readonly Type[] SceneTypes = { typeof(GameManager), typeof(GaltonBoard), typeof(BallSpawner), typeof(GaltonEffects) };

    private sealed class Catalog
    {
        public readonly Type Type;
        public readonly string Field;
        public Catalog(Type type, string field) { Type = type; Field = field; }
    }

    private static readonly Catalog[] Catalogs = {
        new Catalog(typeof(BallDefinition), "balls"), new Catalog(typeof(SkillDefinition), "skills"),
        new Catalog(typeof(PinModifierDefinition), "modifiers"), new Catalog(typeof(ChallengeDefinition), "challenges"),
        new Catalog(typeof(GachaDefinition), "gachas"), new Catalog(typeof(RewardDefinition), "rewards")
    };

    private sealed class Node
    {
        public SkillDefinition Asset, Prerequisite;
        public BallDefinition[] Targets;
        public string Title, Key, TargetKey, RootKey, Branch;
        public int Levels, Depth;
        public Vector2 Position;
    }


    [SerializeField] private int tab;
    [SerializeField] private Object selected;
    [SerializeField] private Vector2 graphScroll;
    [SerializeField] private float zoom = .75f;
    [SerializeField] private bool snap;
    private Vector2 listScroll, inspectorScroll;
    private string newName = "New Ball", search = "";
    private BouncyBallySettings settings;
    private UnityEditor.Editor sceneInspector;
    private readonly Dictionary<Type, Object[]> assetCache = new Dictionary<Type, Object[]>();
    private SkillDefinition dragging, connectChild;
    private Vector2 dragOffset;
    private int dragUndoGroup = -1, dragControl;
    private GUIStyle nodeStyle;

    [MenuItem("Tools/Bouncy Bally/Editor")]
    public static void Open()
    {
        var window = GetWindow<GaltonEditorWindow>("Bouncy Bally Editor");
        window.minSize = new Vector2(900, 560);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Bouncy Bally Editor");
        minSize = new Vector2(900, 560);
        Undo.undoRedoPerformed += Refresh;
        EditorApplication.projectChanged += Refresh;
        EditorApplication.hierarchyChanged += Repaint;
        Refresh();
    }

    private void OnDisable()
    {
        FinishDrag();
        Undo.undoRedoPerformed -= Refresh;
        EditorApplication.projectChanged -= Refresh;
        EditorApplication.hierarchyChanged -= Repaint;
        if (sceneInspector != null) DestroyImmediate(sceneInspector);
    }

    private void Refresh()
    {
        assetCache.Clear();
        settings = AssetDatabase.LoadAssetAtPath<BouncyBallySettings>(SettingsPath);
        Repaint();
    }

    private Object[] FindAssets(Type type)
    {
        if (!assetCache.TryGetValue(type, out Object[] assets))
        {
            assets = AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets" })
                .Select(guid => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type))
                .Where(asset => asset != null).Distinct()
                .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal).ToArray();
            assetCache[type] = assets;
        }
        return assets;
    }

    private static Catalog TabCatalog(int index)
    {
        switch (index)
        {
            case 1: return Catalogs[0];
            case 2: return Catalogs[1];
            case 4: return Catalogs[2];
            case 5: return Catalogs[3];
            case 6: return Catalogs[4];
            case 7: return Catalogs[5];
            default: return null;
        }
    }

    private void OnGUI()
    {
        bool locked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
        if (locked) FinishDrag();
        using (new EditorGUI.DisabledScope(locked))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Bouncy Bally Editor", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Sync catalogs", EditorStyles.toolbarButton)) SyncCatalogs();
                if (GUILayout.Button("Save assets", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
            }
            int next = GUILayout.SelectionGrid(tab, Tabs, position.width < 1200 ? 5 : 10, EditorStyles.toolbarButton);
            if (next != tab)
            {
                FinishDrag();
                connectChild = null;
                tab = next;
                listScroll = inspectorScroll = Vector2.zero;
                if (TabCatalog(tab) != null) newName = "New " + Singular(Tabs[tab]);
            }
            if (GaltonScriptTemplates.HasPending)
            {
                EditorGUILayout.HelpBox("An optional script is waiting for compilation and linking.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Retry linking")) GaltonScriptTemplates.CompletePending();
                    if (GUILayout.Button("Cancel pending link")) GaltonScriptTemplates.CancelPending();
                }
            }
            if (tab == 0) DrawSettings();
            else if (tab == 3) DrawTree();
            else if (tab == 8) DrawScene();
            else DrawAssets(TabCatalog(tab));
        }
        if (locked) EditorGUILayout.HelpBox("Stop Play mode and wait for compilation to edit.", MessageType.Info);
    }

    private static string Singular(string text)
    {
        if (text == "Goals") return "Goal";
        return text.EndsWith("s", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("Resources/BouncyBallySettings.asset", EditorStyles.boldLabel);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Create the runtime settings asset, then sync the catalogs.", MessageType.Info);
            if (GUILayout.Button("Create settings and sync catalogs")) SyncCatalogs();
            return;
        }
        using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
        {
            inspectorScroll = scroll.scrollPosition;
            DrawSerializedInspector(settings);
        }
        EditorGUILayout.HelpBox("Edit balance here. Rewards are assigned inside gachas.", MessageType.Info);
    }

    private void DrawAssets(Catalog catalog)
    {
        if (catalog == null) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            newName = EditorGUILayout.TextField("New name", newName);
            if (GUILayout.Button("Create " + Singular(Tabs[tab]), GUILayout.Width(145))) CreateAsset(catalog.Type, false);
            using (new EditorGUI.DisabledScope(selected == null || selected.GetType() != catalog.Type))
            {
                if (GUILayout.Button("Duplicate", GUILayout.Width(85))) CreateAsset(catalog.Type, true);
                if (GUILayout.Button("Delete", GUILayout.Width(70))) DeleteSelected();
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(235)))
            {
                search = EditorGUILayout.TextField("Search", search);
                using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll))
                {
                    listScroll = scroll.scrollPosition;
                    Object[] assets = FindAssets(catalog.Type);
                    foreach (Object asset in assets)
                    {
                        string label = AssetTitle(asset);
                        if (!string.IsNullOrEmpty(search) && (label + " " + asset.name).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (GUILayout.Toggle(selected == asset, new GUIContent(label, AssetDatabase.GetAssetPath(asset)), "Button")) Select(asset);
                    }
                    if (assets.Length == 0) EditorGUILayout.HelpBox("No assets yet. Use Create.", MessageType.Info);
                }
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
            {
                inspectorScroll = scroll.scrollPosition;
                if (selected == null || selected.GetType() != catalog.Type)
                    EditorGUILayout.HelpBox("Select an asset to edit all its fields.", MessageType.Info);
                else
                {
                    DrawAssetHeader(selected);
                    DrawSerializedInspector(selected);
                    DrawOptionalScript(selected);
                }
            }
        }
        EditorGUILayout.HelpBox("Sync catalogs after adding assets. Built-in effects and balance need no scripts.", MessageType.Info);
    }

    private void DeleteSelected()
    {
        FinishDrag();
        Object target = selected;
        if (!GaltonAssetDeletion.Delete(target)) return;
        selected = null;
        connectChild = null;
        Refresh();
        ShowNotification(new GUIContent("Asset and scene links removed"));
    }

    private static string AssetTitle(Object asset)
    {
        if (asset == null) return "Missing";
        SerializedProperty title = new SerializedObject(asset).FindProperty("title");
        return title != null && !string.IsNullOrWhiteSpace(title.stringValue) ? title.stringValue : asset.name;
    }

    private void Select(Object asset)
    {
        if (selected == asset) return;
        selected = asset;
        inspectorScroll = Vector2.zero;
        Selection.activeObject = asset;
        Repaint();
    }

    private static void DrawAssetHeader(Object asset)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(AssetTitle(asset), EditorStyles.boldLabel);
            if (GUILayout.Button("Ping", GUILayout.Width(50))) EditorGUIUtility.PingObject(asset);
        }
        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset), EditorStyles.miniLabel);
    }

    // SerializedObject expone también los campos privados y conserva Undo del inspector.
    private void DrawSerializedInspector(Object asset)
    {
        var data = new SerializedObject(asset);
        data.Update();
        SerializedProperty property = data.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            using (new EditorGUI.DisabledScope(property.name == "m_Script" || property.name == "id"))
            {
                if (asset is SkillDefinition skill && property.name == "prerequisite")
                {
                    SkillDefinition previous = property.objectReferenceValue as SkillDefinition;
                    EditorGUILayout.PropertyField(property, true);
                    SkillDefinition candidate = property.objectReferenceValue as SkillDefinition;
                    if (candidate != previous)
                    {
                        data.FindProperty("prerequisiteLevel").intValue = candidate == null ? 1 :
                            Mathf.Clamp(data.FindProperty("prerequisiteLevel").intValue, 1, candidate.MaxLevel);
                    }
                }
                else EditorGUILayout.PropertyField(property, true);
            }
        }
        if (data.ApplyModifiedProperties()) Repaint();
        SerializedProperty id = data.FindProperty("id");
        if (id != null && id.propertyType == SerializedPropertyType.String)
        {
            EditorGUILayout.LabelField("Permanent ID. Duplicate creates a new ID.", EditorStyles.miniLabel);
            bool invalid = string.IsNullOrWhiteSpace(id.stringValue) || Catalogs.Any(catalog => FindAssets(catalog.Type).Any(other => {
                if (other == asset) return false;
                SerializedProperty otherId = new SerializedObject(other).FindProperty("id");
                return otherId != null && otherId.propertyType == SerializedPropertyType.String && otherId.stringValue == id.stringValue;
            }));
            if (invalid && GUILayout.Button("Repair missing or duplicate ID"))
            {
                data.Update();
                data.FindProperty("id").stringValue = NewUniqueId();
                data.ApplyModifiedProperties();
            }
        }
    }

    private static void DrawOptionalScript(Object asset)
    {
        if (!(asset is BallDefinition) && !(asset is SkillDefinition)) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional custom behavior", EditorStyles.boldLabel);
        if (asset is BallDefinition ball && ball.CustomBehavior != null)
        {
            var editor = UnityEditor.Editor.CreateEditor(ball.CustomBehavior);
            editor.OnInspectorGUI();
            Object.DestroyImmediate(editor);
        }

        using (new EditorGUI.DisabledScope(GaltonScriptTemplates.HasPending))
            if (GUILayout.Button(asset is BallDefinition ? "Create custom ball script" : "Create custom skill script"))
                GaltonScriptTemplates.Generate(asset, asset is BallDefinition);
    }

    private void CreateAsset(Type type, bool duplicate)
    {
        Object source = duplicate ? selected : null;
        if (duplicate && (source == null || source.GetType() != type)) return;
        string title = duplicate ? AssetTitle(source) + " Copy" : (string.IsNullOrWhiteSpace(newName) ? "New " + type.Name : newName.Trim());
        string safeName = string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '/' || c == '\\' ? '_' : c));
        string path = EditorUtility.SaveFilePanelInProject(duplicate ? "Duplicate asset" : "Create asset", safeName, "asset", "Choose a new asset path.");
        if (string.IsNullOrEmpty(path)) return;
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        BallController template = null;
        if (type == typeof(BallDefinition))
        {
            template = duplicate ? (source as BallDefinition).Prefab : FindBasicPrefab();
            if (template == null || template.GetComponent<BallBase>() == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(template)))
            {
                EditorUtility.DisplayDialog("Missing Basic prefab", "A ball needs a saved Basic prefab with BallController and BallBase. Repair Basic Ball first.", "OK");
                return;
            }
        }
        ScriptableObject asset = null;
        string prefabPath = null;
        bool assetCreated = false;
        try
        {
            asset = duplicate ? Instantiate((ScriptableObject)source) : ScriptableObject.CreateInstance(type);
            asset.name = Path.GetFileNameWithoutExtension(path);
            var data = new SerializedObject(asset);
            SerializedProperty titleField = data.FindProperty("title");
            if (titleField != null) titleField.stringValue = title;
            SerializedProperty id = data.FindProperty("id");
            if (id != null && id.propertyType == SerializedPropertyType.String) id.stringValue = NewUniqueId();
            if (template != null)
            {
                prefabPath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, "prefab"));
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), prefabPath)) throw new InvalidOperationException("Could not clone the ball prefab.");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                BallController controller = prefab != null ? prefab.GetComponent<BallController>() : null;
                if (controller == null || controller.GetComponent<BallBase>() == null) throw new InvalidOperationException("The cloned prefab is missing BallController or BallBase.");
                data.FindProperty("prefab").objectReferenceValue = controller;
                if (!duplicate)
                {
                    data.FindProperty("symbol").stringValue = title.Substring(0, 1).ToUpperInvariant();
                    data.FindProperty("description").stringValue = "Configure this ball's statistics and unlock rule.";
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            assetCreated = true;
            AssetDatabase.SaveAssets();
            Refresh();
            Select(asset);
            if (!SyncCatalogs())
            {
                ShowNotification(new GUIContent("Asset created; catalog sync blocked until scene choices are complete"));
                return;
            }
            if (asset is SkillDefinition skill) GaltonSceneBuilder.CreateSkillNode(skill);
            if (asset is ChallengeDefinition || asset is GachaDefinition)
                EditorUtility.DisplayDialog("Scene view required", "The asset was added to the catalog. Create and assign a complete ProgressionUI " +
                    (asset is ChallengeDefinition ? "goal row" : "gacha row") + " in the scene; catalog registration alone does not create a view.", "OK");
        }
        catch (Exception exception)
        {
            if (assetCreated) AssetDatabase.DeleteAsset(path);
            else if (asset != null) DestroyImmediate(asset);
            if (!string.IsNullOrEmpty(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Asset creation failed", exception.Message, "OK");
        }
    }

    private BallController FindBasicPrefab()
    {
        // Basic tiene prioridad explícita; una bola nueva no copia la selección actual.
        BallDefinition basic = FindAssets(typeof(BallDefinition)).Cast<BallDefinition>().FirstOrDefault(ball =>
            ball.Prefab != null && (string.Equals(ball.name, "Basic Ball", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AssetTitle(ball), "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AssetTitle(ball), "Basic Ball", StringComparison.OrdinalIgnoreCase)));
        if (basic != null) return basic.Prefab;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).OrderBy(value => value, StringComparer.Ordinal))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && (prefab.name == "Basic" || prefab.name == "Basic Ball" || prefab.name == "Ball") &&
                prefab.GetComponent<BallBase>() != null && prefab.GetComponent<BallController>() != null) return prefab.GetComponent<BallController>();
        }
        return null;
    }

    private string NewUniqueId()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Catalog catalog in Catalogs)
            foreach (Object asset in FindAssets(catalog.Type))
            {
                SerializedProperty id = new SerializedObject(asset).FindProperty("id");
                if (id != null && id.propertyType == SerializedPropertyType.String) ids.Add(id.stringValue);
            }
        string value;
        do { value = Guid.NewGuid().ToString("N"); } while (ids.Contains(value));
        return value;
    }

    private bool EnsureSettings()
    {
        if (settings != null) return true;
        if (AssetDatabase.LoadMainAssetAtPath(SettingsPath) != null)
        {
            EditorUtility.DisplayDialog("Invalid settings asset", SettingsPath + " is occupied by another asset type.", "OK");
            return false;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        settings = ScriptableObject.CreateInstance<BouncyBallySettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return true;
    }

    private bool SyncCatalogs()
    {
        Refresh();
        if (!EnsureSettings()) return false;
        BallDefinition[] balls = FindAssets(typeof(BallDefinition)).Cast<BallDefinition>().ToArray();
        GaltonSceneBuilder.CreateMissingBallChoices(balls);

        var data = new SerializedObject(settings);
        data.Update();
        // Todos los arrays de referencias, incluidos futuros catálogos del modelo.
        SerializedProperty property = data.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            if (!property.isArray || property.propertyType == SerializedPropertyType.String) continue;
            Type type = CatalogType(property);
            if (type == null) continue;
            Object[] assets = FindAssets(type);
            property.arraySize = assets.Length;
            for (int i = 0; i < assets.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
        }
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Catalogs synced"));
        return true;
    }

    private static Type CatalogType(SerializedProperty array)
    {
        Catalog known = Catalogs.FirstOrDefault(catalog => catalog.Field == array.name);
        if (known != null) return known.Type;
        return TypeCache.GetTypesDerivedFrom<ScriptableObject>().FirstOrDefault(type =>
            array.arrayElementType == "PPtr<$" + type.Name + ">" || array.arrayElementType == "PPtr<" + type.Name + ">");
    }

    private Object[] Registered(Type type)
    {
        Catalog catalog = Catalogs.FirstOrDefault(item => item.Type == type);
        if (settings == null || catalog == null) return Array.Empty<Object>();
        SerializedProperty array = new SerializedObject(settings).FindProperty(catalog.Field);
        if (array == null || !array.isArray) return Array.Empty<Object>();
        return Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }

    private List<Node> ReadNodes()
    {
        var nodes = new List<Node>();
        foreach (SkillDefinition skill in Registered(typeof(SkillDefinition)).OfType<SkillDefinition>().Distinct())
        {
            var data = new SerializedObject(skill);
            SerializedProperty targets = data.FindProperty("targetBalls");
            BallDefinition[] balls = Enumerable.Range(0, targets.arraySize).Select(i => targets.GetArrayElementAtIndex(i).objectReferenceValue as BallDefinition).ToArray();
            nodes.Add(new Node {
                Asset = skill, Key = StableKey(skill), Title = data.FindProperty("title").stringValue,
                Position = data.FindProperty("position").vector2Value,
                Prerequisite = data.FindProperty("prerequisite").objectReferenceValue as SkillDefinition,
                Targets = balls,
                TargetKey = balls.Length == 0 ? "0:Global" : "1:" + string.Join("|", balls.Select(ball => StableKey(ball)).Distinct().OrderBy(key => key, StringComparer.Ordinal)),
                Levels = skill.MaxLevel
            });
        }
        return nodes.OrderBy(node => node.Key, StringComparer.Ordinal).ToList();
    }

    private static string StableKey(Object asset)
    {
        if (asset == null) return "Missing";
        SerializedProperty id = new SerializedObject(asset).FindProperty("id");
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        return id != null && !string.IsNullOrWhiteSpace(id.stringValue) ? id.stringValue + ":" + guid : guid;
    }

    private Vector2 GraphSize()
    {
        Vector2 size = settings != null ? new SerializedObject(settings).FindProperty("graphSize").vector2Value : new Vector2(4000, 2800);
        return new Vector2(Finite(size.x) ? Mathf.Max(400, size.x) : 4000, Finite(size.y) ? Mathf.Max(300, size.y) : 2800);
    }

    private float NodeSize()
    {
        float size = settings != null ? new SerializedObject(settings).FindProperty("nodeSize").floatValue : 170;
        return Finite(size) ? Mathf.Clamp(size, 80, 500) : 170;
    }

    private void ZoomLimits(out float minimum, out float maximum)
    {
        var data = settings != null ? new SerializedObject(settings) : null;
        float low = data != null ? data.FindProperty("minimumZoom").floatValue : .22f;
        float high = data != null ? data.FindProperty("maximumZoom").floatValue : 2f;
        minimum = Finite(low) ? Mathf.Clamp(low, .05f, 1f) : .22f;
        maximum = Finite(high) ? Mathf.Clamp(high, Mathf.Max(1f, minimum), 4f) : 2f;
    }

    private void DrawTree()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Sync catalogs to create settings and load all skills.", MessageType.Info);
            return;
        }
        List<Node> nodes = ReadNodes();
        ZoomLimits(out float minimum, out float maximum);
        zoom = Mathf.Clamp(Finite(zoom) ? zoom : .75f, minimum, maximum);
        bool fit = false;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(nodes.Count + " skills", GUILayout.Width(70));
            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                FinishDrag();
                AutoLayout(nodes);
                nodes = ReadNodes();
                fit = true;
            }
            if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40))) fit = true;
            zoom = EditorGUILayout.Slider("Zoom", zoom, minimum, maximum, GUILayout.MinWidth(190), GUILayout.MaxWidth(320));
            snap = GUILayout.Toggle(snap, "Snap 5%", GUILayout.Width(85));
            GUILayout.FlexibleSpace();
        }
        if (connectChild != null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("Click a prerequisite for " + AssetTitle(connectChild) + ". Escape cancels.", MessageType.Info);
                if (GUILayout.Button("Cancel", GUILayout.Width(60))) connectChild = null;
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                Rect view = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawGraph(view, nodes, minimum, maximum, fit);
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
            {
                inspectorScroll = scroll.scrollPosition;
                if (selected is SkillDefinition skill && nodes.Any(node => node.Asset == skill))
                {
                    DrawAssetHeader(skill);
                    if (GUILayout.Button("Pick prerequisite in graph")) connectChild = skill;
                    if (GUILayout.Button("Clear prerequisite")) SetPrerequisite(skill, null);
                    if (GUILayout.Button("Delete skill")) { DeleteSelected(); GUIUtility.ExitGUI(); }
                    DrawSerializedInspector(skill);
                    DrawOptionalScript(skill);
                }
                else EditorGUILayout.HelpBox("Select a skill. Drag to move; scroll to zoom. All targets share this graph.", MessageType.Info);
            }
        }
        if (nodes.Count == 0) EditorGUILayout.HelpBox("Create skills, then sync catalogs.", MessageType.Info);
    }

    private void DrawGraph(Rect view, List<Node> nodes, float minimum, float maximum, bool fit)
    {
        Vector2 size = GraphSize();
        float nodeSize = NodeSize(), padding = nodeSize * .6f + 24;
        Vector2 extent = size + Vector2.one * padding * 2;
        Event e = Event.current;
        bool pointerInView = view.Contains(e.mousePosition) && e.mousePosition.x < view.xMax - 16 && e.mousePosition.y < view.yMax - 16;
        if (fit)
        {
            zoom = Mathf.Clamp(Mathf.Min(Mathf.Max(1, view.width - 20) / extent.x, Mathf.Max(1, view.height - 20) / extent.y), minimum, maximum);
            graphScroll = Vector2.zero;
        }
        if (GUI.enabled && e.type == EventType.ScrollWheel && pointerInView)
        {
            Vector2 anchor = e.mousePosition - view.position;
            float oldZoom = zoom;
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .06f), minimum, maximum);
            graphScroll = Vector2.Max(Vector2.zero, (graphScroll + anchor) * (zoom / oldZoom) - anchor);
            e.Use();
            Repaint();
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            connectChild = null;
            FinishDrag();
            e.Use();
        }
        Rect content = new Rect(Vector2.zero, extent * zoom);
        int control = GUIUtility.GetControlID(FocusType.Passive);
        graphScroll = GUI.BeginScrollView(view, graphScroll, content);
        EditorGUI.DrawRect(content, new Color(.075f, .085f, .105f));
        var lookup = nodes.ToDictionary(node => node.Asset);
        Handles.BeginGUI();
        foreach (Node node in nodes)
        {
            if (node.Prerequisite == null || !lookup.TryGetValue(node.Prerequisite, out Node parent)) continue;
            Vector2 a = GraphPoint(parent.Position, size, padding) + Vector2.up * nodeSize * .23f * zoom;
            Vector2 b = GraphPoint(node.Position, size, padding) - Vector2.up * nodeSize * .23f * zoom;
            Color color = HasCycle(node.Asset, node.Prerequisite) ? new Color(1, .3f, .3f) : new Color(.4f, .7f, .6f);
            float bend = Mathf.Max(30 * zoom, Mathf.Abs(b.y - a.y) * .45f);
            Handles.DrawBezier(a, b, a + Vector2.up * bend, b - Vector2.up * bend, color, null, 2);
            Vector3 tip = b;
            Handles.color = color;
            Handles.DrawAAPolyLine(2, tip + new Vector3(-5, -8) * zoom, tip, tip + new Vector3(5, -8) * zoom);
        }
        Handles.EndGUI();
        if (nodeStyle == null) nodeStyle = new GUIStyle(EditorStyles.whiteLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        nodeStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(12 * zoom), 7, 24);
        foreach (Node node in nodes)
        {
            Rect rect = NodeRect(node, size, padding, nodeSize);
            EditorGUI.DrawRect(rect, selected == node.Asset ? new Color(.22f, .44f, .36f) : new Color(.16f, .20f, .25f));
            string targets = node.Targets.Length == 0 ? "Global" : string.Join(", ", node.Targets.Select(ball => AssetTitle(ball)));
            string label = node.Title + "\n" + targets + " · " + node.Levels + " levels";
            GUI.Label(rect, new GUIContent(label, label + "\n" + AssetDatabase.GetAssetPath(node.Asset)), nodeStyle);
        }
        if (GUI.enabled && pointerInView && e.type == EventType.MouseDown && e.button == 0 && GUIUtility.hotControl == 0)
        {
            Node hit = nodes.LastOrDefault(node => NodeRect(node, size, padding, nodeSize).Contains(e.mousePosition));
            if (hit != null)
            {
                if (connectChild != null)
                {
                    if (SetPrerequisite(connectChild, hit.Asset)) connectChild = null;
                }
                else
                {
                    Select(hit.Asset);
                    dragging = hit.Asset;
                    dragOffset = e.mousePosition - GraphPoint(hit.Position, size, padding);
                    dragControl = control;
                    GUIUtility.hotControl = control;
                    GUIUtility.keyboardControl = 0;
                }
                e.Use();
            }
        }
        if (GUI.enabled && dragging != null && GUIUtility.hotControl == dragControl && e.type == EventType.MouseDrag && e.button == 0)
        {
            if (dragUndoGroup < 0)
            {
                Undo.IncrementCurrentGroup();
                dragUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Move Bouncy Bally skill");
                Undo.RegisterCompleteObjectUndo(dragging, "Move Bouncy Bally skill");
            }
            Vector2 point = (e.mousePosition - dragOffset) / zoom - Vector2.one * padding;
            Vector2 normalized = new Vector2(Mathf.Clamp01(point.x / size.x), Mathf.Clamp01(1 - point.y / size.y));
            if (snap) normalized = new Vector2(Mathf.Round(normalized.x * 20) / 20, Mathf.Round(normalized.y * 20) / 20);
            SetPosition(dragging, normalized);
            e.Use();
            Repaint();
        }
        if (dragging != null && (e.rawType == EventType.MouseUp || e.type == EventType.Ignore)) FinishDrag();
        GUI.EndScrollView();
    }

    private Vector2 GraphPoint(Vector2 normalized, Vector2 size, float padding)
    {
        // Se sanea solo el dibujo; abrir el árbol nunca reescribe posiciones inválidas.
        float x = Finite(normalized.x) ? Mathf.Clamp01(normalized.x) : .5f;
        float y = Finite(normalized.y) ? Mathf.Clamp01(normalized.y) : .5f;
        return new Vector2(padding + x * size.x, padding + (1 - y) * size.y) * zoom;
    }

    private Rect NodeRect(Node node, Vector2 size, float padding, float nodeSize)
    {
        Vector2 point = GraphPoint(node.Position, size, padding), dimensions = new Vector2(nodeSize, nodeSize * .46f) * zoom;
        return new Rect(point - dimensions * .5f, dimensions);
    }

    private void FinishDrag()
    {
        if (dragUndoGroup >= 0) Undo.CollapseUndoOperations(dragUndoGroup);
        if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
        dragging = null;
        dragUndoGroup = -1;
        dragControl = 0;
    }

    private static void SetPosition(SkillDefinition skill, Vector2 value)
    {
        var data = new SerializedObject(skill);
        data.FindProperty("position").vector2Value = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static SkillDefinition Prerequisite(SkillDefinition skill)
    {
        return skill == null ? null : new SerializedObject(skill).FindProperty("prerequisite").objectReferenceValue as SkillDefinition;
    }

    private static bool HasCycle(SkillDefinition skill, SkillDefinition candidate)
    {
        var visited = new HashSet<SkillDefinition> { skill };
        while (candidate != null)
        {
            if (!visited.Add(candidate)) return true;
            candidate = Prerequisite(candidate);
        }
        return false;
    }

    private bool SetPrerequisite(SkillDefinition skill, SkillDefinition candidate)
    {

        var data = new SerializedObject(skill);
        data.FindProperty("prerequisite").objectReferenceValue = candidate;
        var level = data.FindProperty("prerequisiteLevel");
        level.intValue = candidate == null ? 1 : Mathf.Clamp(level.intValue, 1, candidate.MaxLevel);
        data.ApplyModifiedProperties();
        Repaint();
        return true;
    }

    private void AutoLayout(List<Node> nodes)
    {
        if (nodes.Count == 0) return;
        var lookup = nodes.ToDictionary(node => node.Asset);
        if (nodes.Any(node => node.Prerequisite != null && !lookup.ContainsKey(node.Prerequisite)))
        {
            ShowNotification(new GUIContent("Sync catalogs or fix missing prerequisites before Auto Layout."));
            return;
        }
        var children = nodes.ToDictionary(node => node.Asset, node => new List<Node>());
        foreach (Node node in nodes) if (node.Prerequisite != null) children[node.Prerequisite].Add(node);
        var queue = new Queue<Node>(nodes.Where(node => node.Prerequisite == null).OrderBy(node => node.Key, StringComparer.Ordinal));
        var ordered = new List<Node>();
        // Kahn detecta ciclos antes de modificar assets, sin recursión ni reparaciones silenciosas.
        while (queue.Count > 0)
        {
            Node node = queue.Dequeue();
            if (node.Prerequisite == null) { node.Depth = 0; node.RootKey = node.Key; node.Branch = node.Key; }
            else
            {
                Node parent = lookup[node.Prerequisite];
                node.Depth = parent.Depth + 1;
                node.RootKey = parent.RootKey;
                node.Branch = parent.Branch + "/" + node.Key;
            }
            ordered.Add(node);
            foreach (Node child in children[node.Asset].OrderBy(child => child.TargetKey, StringComparer.Ordinal).ThenBy(child => child.Key, StringComparer.Ordinal)) queue.Enqueue(child);
        }
        if (ordered.Count != nodes.Count)
        {
            ShowNotification(new GUIContent("Fix prerequisite cycles before Auto Layout. No positions changed."));
            return;
        }
        // Carriles por conjunto de destinos y raíz; el orden de requisitos separa las subramas.
        var lanes = ordered.GroupBy(node => node.TargetKey + ":root:" + node.RootKey).OrderBy(lane => lane.Key, StringComparer.Ordinal).ToList();
        float cell = NodeSize() + 70, gap = NodeSize() * .7f;
        float[] widths = lanes.Select(lane => lane.GroupBy(node => node.Depth).Max(row => row.Count()) * cell).ToArray();
        float totalWidth = widths.Sum() + gap * Mathf.Max(0, lanes.Count - 1);
        int maxDepth = ordered.Max(node => node.Depth);
        Vector2 size = GraphSize();
        size.x = Mathf.Max(size.x, totalWidth + cell);
        size.y = Mathf.Max(size.y, (maxDepth + 2) * (NodeSize() * .46f + 90));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Auto Layout Bouncy Bally skills");
        Undo.RegisterCompleteObjectUndo(nodes.Select(node => (Object)node.Asset).Concat(new Object[] { settings }).ToArray(), "Auto Layout Bouncy Bally skills");
        var data = new SerializedObject(settings);
        data.FindProperty("graphSize").vector2Value = size;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        float left = (size.x - totalWidth) * .5f;
        for (int index = 0; index < lanes.Count; index++)
        {
            foreach (var row in lanes[index].GroupBy(node => node.Depth).OrderBy(row => row.Key))
            {
                Node[] members = row.OrderBy(node => node.Branch, StringComparer.Ordinal).ToArray();
                for (int column = 0; column < members.Length; column++)
                {
                    float x = left + widths[index] * .5f + (column - (members.Length - 1) * .5f) * cell;
                    float y = 1 - (row.Key + 1f) / (maxDepth + 2f);
                    SetPosition(members[column].Asset, new Vector2(x / size.x, y));
                }
            }
            left += widths[index] + gap;
        }
        Undo.CollapseUndoOperations(group);
        Repaint();
    }

    private void DrawScene()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync catalogs")) SyncCatalogs();
            if (GUILayout.Button("Sync scene")) SyncScene();
            if (GUILayout.Button("Save scene and assets")) { AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes(); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.Width(255)))
            {
                listScroll = scroll.scrollPosition;
                foreach (Type type in SceneTypes)
                {
                    EditorGUILayout.LabelField(type.Name, EditorStyles.boldLabel);
                    Component[] components = SceneComponents(type);
                    foreach (Component component in components)
                    {
                        string label = component.gameObject.scene.name + "/" + component.gameObject.name;
                        if (GUILayout.Toggle(selected == component, new GUIContent(label, HierarchyPath(component.transform)), "Button")) Select(component);
                    }
                    if (components.Length == 0) EditorGUILayout.LabelField("Not found in loaded scenes", EditorStyles.miniLabel);
                }
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
            {
                inspectorScroll = scroll.scrollPosition;
                Component target = selected as Component;
                if (target == null || !SceneTypes.Contains(target.GetType()) || !IsSceneComponent(target))
                    EditorGUILayout.HelpBox("Select an existing component to edit its configuration.", MessageType.Info);
                else
                {
                    EditorGUILayout.ObjectField("Component", target, target.GetType(), true);
                    UnityEditor.Editor.CreateCachedEditor(target, null, ref sceneInspector);
                    EditorGUI.BeginChangeCheck();
                    sceneInspector.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                    }
                }
            }
        }
        EditorGUILayout.HelpBox("Sync scene calls GaltonSceneBuilder.Sync. Graph edits are saved to assets and do not trigger a scene rebuild.", MessageType.Info);
    }

    private static Component[] SceneComponents(Type type)
    {
        return Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<Component>().Where(IsSceneComponent)
            .OrderBy(component => component.gameObject.scene.path, StringComparer.Ordinal)
            .ThenBy(component => HierarchyPath(component.transform), StringComparer.Ordinal).ToArray();
    }

    private static bool IsSceneComponent(Component component)
    {
        return component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(component.gameObject.scene);
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }

    private void SyncScene()
    {
        FinishDrag();
        if (!SyncCatalogs()) return;
        if (settings == null) return;
        // Protege las posiciones mientras el builder externo migra al catálogo.
        var positions = FindAssets(typeof(SkillDefinition)).Cast<SkillDefinition>()
            .ToDictionary(skill => skill, skill => new SerializedObject(skill).FindProperty("position").vector2Value);
        try { GaltonSceneBuilder.Sync(); }
        finally
        {
            foreach (var pair in positions)
                if (pair.Key != null && new SerializedObject(pair.Key).FindProperty("position").vector2Value != pair.Value) SetPosition(pair.Key, pair.Value);
            AssetDatabase.SaveAssets();
            Refresh();
        }
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
