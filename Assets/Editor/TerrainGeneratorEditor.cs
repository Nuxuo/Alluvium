using UnityEditor;
using UnityEngine;
using VoxelTerrain.Data;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    private TerrainGenerator generator;
    private bool showAdvancedSettings = false;

    private void OnEnable()
    {
        generator = (TerrainGenerator)target;
        Tools.hidden = true;
    }

    private void OnDisable()
    {
        Tools.hidden = false;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header
        EditorGUILayout.Space();
        DrawHeader();

        // Main generation controls
        EditorGUILayout.Space();
        DrawMainControls();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Biome selection
        DrawBiomeSelection();

        // Settings sections
        DrawMapSettings();
        DrawVoxelSettings();
        DrawWaterSettings();

        // Advanced/Debug
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "⚙️ Advanced Settings", true);
        if (showAdvancedSettings)
        {
            DrawAdvancedSettings();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        var style = new GUIStyle(EditorStyles.largeLabel);
        style.fontSize = 16;
        style.fontStyle = FontStyle.Bold;
        EditorGUILayout.LabelField("🌍 Voxel Terrain Generator", style);
        EditorGUILayout.LabelField("Biome-based procedural terrain with resources", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawMainControls()
    {
        EditorGUILayout.BeginVertical("box");

        // Big generation button
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("🚀 Generate Complete World", GUILayout.Height(40)))
        {
            generator.GenerateCompleteWorld();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Generates heightmap → erosion → terrain → resources → mesh", MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawBiomeSelection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🏔️ Biome Selection", EditorStyles.boldLabel);

        SerializedProperty useQuickPreset = serializedObject.FindProperty("useQuickPreset");
        EditorGUILayout.PropertyField(useQuickPreset, new GUIContent("Use Quick Preset"));

        if (useQuickPreset.boolValue)
        {
            // Quick preset dropdown
            SerializedProperty quickPreset = serializedObject.FindProperty("quickPreset");
            EditorGUILayout.PropertyField(quickPreset, new GUIContent("Biome Type"));

            // Show description based on selected preset
            EditorGUILayout.Space(5);
            string description = GetPresetDescription((TerrainGenerator.QuickPresetType)quickPreset.enumValueIndex);
            EditorGUILayout.HelpBox(description, MessageType.None);
        }
        else
        {
            // Custom biome preset asset
            SerializedProperty biomePreset = serializedObject.FindProperty("biomePreset");
            EditorGUILayout.PropertyField(biomePreset, new GUIContent("Custom Biome Preset"));

            if (biomePreset.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a BiomePreset asset or use Quick Preset mode", MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private string GetPresetDescription(TerrainGenerator.QuickPresetType preset)
    {
        switch (preset)
        {
            case TerrainGenerator.QuickPresetType.Mountains:
                return "⛰️ Mountains: High peaks, rocky cliffs, snow caps. Rich in Iron, Gold, and Stone. Great for mining operations.";
            case TerrainGenerator.QuickPresetType.Desert:
                return "🏜️ Desert: Flat sandy terrain with rock formations. Contains Sand and Copper deposits. Low water availability.";
            case TerrainGenerator.QuickPresetType.Plains:
                return "🌾 Plains: Gentle rolling hills, perfect for building. Contains Coal and Clay. Ideal for agriculture and construction.";
            case TerrainGenerator.QuickPresetType.Highlands:
                return "🗻 Highlands: Elevated plateaus with steep cliffs. Rich in Iron, Copper, and Granite. Strategic defensive positions.";
            default:
                return "";
        }
    }

    private void DrawMapSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🗺️ Map Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapSize"), new GUIContent("Map Size", "Grid resolution (cells per side)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"), new GUIContent("World Scale", "Size in world units"));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🎲 Random Seed", EditorStyles.miniBoldLabel);

        SerializedProperty randomizeSeed = serializedObject.FindProperty("randomizeSeed");
        EditorGUILayout.PropertyField(randomizeSeed, new GUIContent("Randomize Seed"));

        EditorGUI.BeginDisabledGroup(randomizeSeed.boolValue);
        SerializedProperty seed = serializedObject.FindProperty("seed");
        EditorGUILayout.PropertyField(seed, new GUIContent("Seed"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    private void DrawVoxelSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📦 Voxel Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        SerializedProperty voxelSize = serializedObject.FindProperty("voxelSize");
        EditorGUILayout.PropertyField(voxelSize, new GUIContent("Voxel Size", "Size of individual voxels"));

        SerializedProperty generateSkirt = serializedObject.FindProperty("generateVoxelSkirt");
        EditorGUILayout.PropertyField(generateSkirt, new GUIContent("Generate Skirt", "Add voxels around edges"));

        SerializedProperty voxelMaterial = serializedObject.FindProperty("voxelMaterial");
        EditorGUILayout.PropertyField(voxelMaterial, new GUIContent("Voxel Material"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWaterSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("💧 Water Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        SerializedProperty enableWater = serializedObject.FindProperty("enableWater");
        EditorGUILayout.PropertyField(enableWater, new GUIContent("Enable Water"));

        if (enableWater.boolValue)
        {
            SerializedProperty waterLevel = serializedObject.FindProperty("waterLevel");
            EditorGUILayout.Slider(waterLevel, 0f, 1f, "Water Level");

            SerializedProperty waterMaterial = serializedObject.FindProperty("waterMaterial");
            EditorGUILayout.PropertyField(waterMaterial);

            SerializedProperty waterScale = serializedObject.FindProperty("waterScale");
            EditorGUILayout.PropertyField(waterScale);

            SerializedProperty waterSkirtHeight = serializedObject.FindProperty("waterSkirtHeight");
            EditorGUILayout.PropertyField(waterSkirtHeight);
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚙️ Advanced Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("heightMapComputeShader"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("erosionShader"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapComputeShader"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainAssignmentShader"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("resourcePlacementShader"));

        EditorGUILayout.EndVertical();
    }

    // Custom scene view overlay
    private void OnSceneGUI()
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 250, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Terrain Generator", EditorStyles.boldLabel);
        if (GUILayout.Button("Generate World"))
        {
            generator.GenerateCompleteWorld();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();

        Handles.EndGUI();
    }
}