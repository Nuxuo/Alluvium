using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimpleHeightmapGenerator))]
public class SimpleHeightmapGeneratorEditor : Editor
{
    private SimpleHeightmapGenerator generator;

    private void OnEnable()
    {
        generator = (SimpleHeightmapGenerator)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header
        DrawHeader();

        EditorGUILayout.Space(10);

        // Main Controls
        DrawMainControls();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        // Settings
        DrawHeightmapSettings();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        DrawNoiseSettings();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        DrawLayerSettings();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        DrawVisualizationSettings();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        DrawComputeSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        var style = new GUIStyle(EditorStyles.largeLabel);
        style.fontSize = 16;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("🗻 Simple Heightmap Generator", style);
        EditorGUILayout.LabelField("Voxel-based terrain with layers (1x1x0.1)", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawMainControls()
    {
        EditorGUILayout.BeginVertical("box");

        // Big generate button
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("🚀 Generate Heightmap", GUILayout.Height(40)))
        {
            generator.GenerateAndVisualize();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // Quick actions
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🎲 Random Seed"))
        {
            SerializedProperty seedProp = serializedObject.FindProperty("seed");
            seedProp.intValue = Random.Range(-10000, 10000);
            serializedObject.ApplyModifiedProperties();
            generator.GenerateAndVisualize();
        }

        if (GUILayout.Button("🔄 Regenerate"))
        {
            generator.GenerateAndVisualize();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHeightmapSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🗺️ Map Settings", EditorStyles.boldLabel);

        SerializedProperty mapSize = serializedObject.FindProperty("mapSize");
        EditorGUILayout.PropertyField(mapSize, new GUIContent("Map Size", "Resolution of the heightmap (voxels per side)"));

        // Show total voxels count
        int totalVoxels = mapSize.intValue * mapSize.intValue;
        string voxelInfo = totalVoxels > 50000 ? " ⚠️ Very high!" : totalVoxels > 25000 ? " (High)" : "";
        EditorGUILayout.HelpBox($"Total voxels: {totalVoxels:N0}{voxelInfo}",
            totalVoxels > 50000 ? MessageType.Warning : MessageType.Info);

        SerializedProperty seed = serializedObject.FindProperty("seed");
        EditorGUILayout.PropertyField(seed, new GUIContent("Seed", "Random seed for generation"));

        EditorGUILayout.EndVertical();
    }

    private void DrawNoiseSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🌊 Noise Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("octaves"),
            new GUIContent("Octaves", "Layers of detail (more = more detail)"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("persistence"),
            new GUIContent("Persistence", "Height contribution per octave (higher = rougher)"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("lacunarity"),
            new GUIContent("Lacunarity", "Frequency multiplier per octave"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"),
            new GUIContent("Scale", "Overall noise scale (lower = larger features)"));

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Octaves: More layers = more detail\n" +
            "Persistence: How much each layer affects height\n" +
            "Lacunarity: How quickly detail increases\n" +
            "Scale: Size of terrain features",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawLayerSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🪨 Layer Settings", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Each voxel is 1x1x0.1 units (width x depth x height)\n" +
            "Layers are stacked from bottom to top:\n" +
            "• Rock (base terrain from heightmap)\n" +
            "• Dirt (middle layer)\n" +
            "• Sand (top layer)",
            MessageType.Info);

        EditorGUILayout.Space(3);

        // Dirt layer count
        SerializedProperty dirtLayers = serializedObject.FindProperty("dirtLayers");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(dirtLayers, new GUIContent("Dirt Layers", "Number of 0.1 unit layers"));

        // Show world height
        float dirtHeight = dirtLayers.intValue * 0.1f;
        GUI.enabled = false;
        EditorGUILayout.LabelField($"= {dirtHeight:F1}u", GUILayout.Width(60));
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // Sand layer count
        SerializedProperty sandLayers = serializedObject.FindProperty("sandLayers");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(sandLayers, new GUIContent("Sand Layers", "Number of 0.1 unit layers"));

        // Show world height
        float sandHeight = sandLayers.intValue * 0.1f;
        GUI.enabled = false;
        EditorGUILayout.LabelField($"= {sandHeight:F1}u", GUILayout.Width(60));
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Total additional height info
        float totalAdditionalHeight = dirtHeight + sandHeight;
        int totalAdditionalLayers = dirtLayers.intValue + sandLayers.intValue;
        EditorGUILayout.HelpBox(
            $"Additional layers: {totalAdditionalLayers} (adds {totalAdditionalHeight:F1} units on top of base terrain)",
            MessageType.None);

        // Color legend
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Layer Colors:", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        DrawColorBox(new Color(0.4f, 0.4f, 0.45f), "Rock");
        DrawColorBox(new Color(0.4f, 0.25f, 0.15f), "Dirt");
        DrawColorBox(new Color(0.9f, 0.85f, 0.6f), "Sand");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawColorBox(Color color, string label)
    {
        EditorGUILayout.BeginVertical();

        Rect rect = GUILayoutUtility.GetRect(40, 20);
        EditorGUI.DrawRect(rect, color);

        EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawVisualizationSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📦 Voxel Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("heightScale"),
            new GUIContent("Height Scale", "Maximum terrain height (for base rock layer)"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainScale"),
            new GUIContent("Terrain Scale", "Width/depth of terrain in world units"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("voxelMaterial"),
            new GUIContent("Voxel Material", "Material for rendering voxels"));

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Voxels are 1x1x0.1 units (width x depth x height)\n" +
            "Each column contains multiple 0.1-unit layers\n" +
            "Layers are colored by soil type\n" +
            "Edge walls show visible strata",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawComputeSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚙️ Compute Shader", EditorStyles.boldLabel);

        SerializedProperty computeShader = serializedObject.FindProperty("heightMapShader");
        EditorGUILayout.PropertyField(computeShader, new GUIContent("HeightMap Compute Shader"));

        if (computeShader.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign the HeightMap.compute shader!", MessageType.Error);
        }

        EditorGUILayout.EndVertical();
    }

    // Scene view overlay
    private void OnSceneGUI()
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 200, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Heightmap Generator", EditorStyles.boldLabel);

        if (GUILayout.Button("🚀 Generate"))
        {
            generator.GenerateAndVisualize();
        }

        if (GUILayout.Button("🎲 Random Seed"))
        {
            SerializedProperty seedProp = serializedObject.FindProperty("seed");
            seedProp.intValue = Random.Range(-10000, 10000);
            serializedObject.ApplyModifiedProperties();
            generator.GenerateAndVisualize();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();

        Handles.EndGUI();
    }
}