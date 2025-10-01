using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for the TerrainGenerator component
/// Provides an enhanced UI for procedural voxel terrain generation
/// </summary>
[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    TerrainGenerator terrainGenerator;

    // Foldout states
    private bool showHeightmapSettings = false;
    private bool showErosionSettings = false;
    private bool showWaterSystem = false;
    private bool showVoxelSettings = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header
        EditorGUILayout.Space();
        DrawEditorHeader();

        // Main Generation Button
        EditorGUILayout.Space();
        DrawMainControls();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Organized Sections
        DrawVoxelSettings();
        DrawHeightmapSettings();
        DrawErosionSettings();
        DrawWaterSystem();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEditorHeader()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🏔️ Voxel Terrain Generator", EditorStyles.largeLabel);
        EditorGUILayout.LabelField("Procedural voxel terrain with hydraulic erosion", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawMainControls()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🚀 World Generation", EditorStyles.boldLabel);

        // Main generation button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("🌍 Generate Complete World", GUILayout.Height(35)))
        {
            GenerateCompleteWorld();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        // Quick controls
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🗻 Heightmap Only"))
        {
            terrainGenerator.GenerateHeightMap();
            terrainGenerator.ConstructMesh();
        }

        if (GUILayout.Button("🌊 Add Erosion"))
        {
            terrainGenerator.Erode();
            terrainGenerator.ConstructMesh();
        }

        if (GUILayout.Button("🔄 Refresh Mesh"))
        {
            terrainGenerator.ConstructMesh();
        }

        EditorGUILayout.EndHorizontal();

        // Basic settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Basic Settings", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mapSize"), new GUIContent("Map Size", "Resolution of the terrain grid"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"), new GUIContent("World Scale", "Size in world units"));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("elevationScale"), new GUIContent("Height Scale", "Maximum terrain height"));

        // Seed settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Random Seed", EditorStyles.miniBoldLabel);

        SerializedProperty randomizeSeed = serializedObject.FindProperty("randomizeSeed");
        EditorGUILayout.PropertyField(randomizeSeed, new GUIContent("Randomize Seed", "Generate a new random seed each time"));

        EditorGUI.BeginDisabledGroup(terrainGenerator.randomizeSeed);
        SerializedProperty seed = serializedObject.FindProperty("seed");
        EditorGUILayout.PropertyField(seed, new GUIContent("Seed", "Random seed for noise and erosion generation"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    private void DrawVoxelSettings()
    {
        EditorGUILayout.BeginVertical("box");
        showVoxelSettings = EditorGUILayout.Foldout(showVoxelSettings, "📦 Voxel Settings", true, EditorStyles.foldoutHeader);

        if (showVoxelSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Voxel generation creates a blocky, minecraft-style terrain using the heightmap data. Only visible faces are generated for optimal performance.", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            SerializedProperty voxelSize = serializedObject.FindProperty("voxelSize");
            EditorGUILayout.PropertyField(voxelSize, new GUIContent("Voxel Size", "Size multiplier for individual voxels"));

            SerializedProperty generateVoxelSkirt = serializedObject.FindProperty("generateVoxelSkirt");
            EditorGUILayout.PropertyField(generateVoxelSkirt, new GUIContent("Generate Skirt", "Add voxel skirt around edges"));

            // Voxel material
            SerializedProperty voxelMaterial = serializedObject.FindProperty("voxelMaterial");
            EditorGUILayout.PropertyField(voxelMaterial, new GUIContent("Voxel Material", "Material used for voxel rendering (use Custom/Voxel shader)"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (!Application.isPlaying)
                {
                    terrainGenerator.ConstructMesh();
                }
            }

            // Block Type Settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🎨 Block Types", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("Configure which block types appear at different heights:\n• Sand: Below threshold\n• Dirt: Between thresholds\n• Snow: Above threshold", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            SerializedProperty sandThreshold = serializedObject.FindProperty("sandHeightThreshold");
            EditorGUILayout.Slider(sandThreshold, 0f, 1f, new GUIContent("Sand Height Threshold", "Heights below this become sand (beach/shore)"));

            SerializedProperty snowThreshold = serializedObject.FindProperty("snowHeightThreshold");
            EditorGUILayout.Slider(snowThreshold, 0f, 1f, new GUIContent("Snow Height Threshold", "Heights above this become snow (peaks)"));

            // Visual indicator of ranges
            EditorGUILayout.Space(5);
            Rect rect = GUILayoutUtility.GetRect(0, 20);
            rect.x += EditorGUIUtility.labelWidth;
            rect.width -= EditorGUIUtility.labelWidth;

            float sandPoint = rect.x + rect.width * terrainGenerator.sandHeightThreshold;
            float snowPoint = rect.x + rect.width * terrainGenerator.snowHeightThreshold;

            // Draw gradient background
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, sandPoint - rect.x, rect.height), new Color(0.85f, 0.75f, 0.55f)); // Sand
            EditorGUI.DrawRect(new Rect(sandPoint, rect.y, snowPoint - sandPoint, rect.height), new Color(0.4f, 0.3f, 0.2f)); // Dirt
            EditorGUI.DrawRect(new Rect(snowPoint, rect.y, rect.x + rect.width - snowPoint, rect.height), new Color(0.95f, 0.95f, 1f)); // Snow

            // Draw threshold lines
            EditorGUI.DrawRect(new Rect(sandPoint - 1, rect.y, 2, rect.height), Color.black);
            EditorGUI.DrawRect(new Rect(snowPoint - 1, rect.y, 2, rect.height), Color.black);

            // Labels
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(rect.x, rect.y + rect.height + 2, sandPoint - rect.x, 15), "Sand", labelStyle);
            GUI.Label(new Rect(sandPoint, rect.y + rect.height + 2, snowPoint - sandPoint, 15), "Dirt", labelStyle);
            GUI.Label(new Rect(snowPoint, rect.y + rect.height + 2, rect.x + rect.width - snowPoint, 15), "Snow", labelStyle);

            EditorGUILayout.Space(20);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (!Application.isPlaying)
                {
                    terrainGenerator.ConstructMesh();
                }
            }

            // Display voxel stats
            float cellSize = (terrainGenerator.scale * 2f) / (terrainGenerator.mapSize - 1);
            float actualVoxelSize = cellSize * terrainGenerator.voxelSize;
            int maxVoxelLayers = Mathf.RoundToInt(terrainGenerator.elevationScale / actualVoxelSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("📊 Voxel Statistics", EditorStyles.boldLabel);

            // Show mesh stats if available
            Transform meshHolder = terrainGenerator.transform.Find("Mesh Holder");
            if (meshHolder != null && meshHolder.GetComponent<MeshFilter>() != null &&
                meshHolder.GetComponent<MeshFilter>().sharedMesh != null)
            {
                var mesh = meshHolder.GetComponent<MeshFilter>().sharedMesh;
                EditorGUILayout.HelpBox(
                    $"Actual voxel size: {actualVoxelSize:F2} units\n" +
                    $"Max voxel layers: ~{maxVoxelLayers}\n" +
                    $"Optimization: Only visible faces rendered\n" +
                    $"Current mesh: {mesh.vertexCount:N0} vertices, {mesh.triangles.Length / 3:N0} triangles",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox($"Actual voxel size: {actualVoxelSize:F2} units\nMax voxel layers: ~{maxVoxelLayers}\nOptimization: Only visible faces rendered", MessageType.None);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHeightmapSettings()
    {
        EditorGUILayout.BeginVertical("box");
        showHeightmapSettings = EditorGUILayout.Foldout(showHeightmapSettings, "🗻 Heightmap Generation", true, EditorStyles.foldoutHeader);

        if (showHeightmapSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Noise-based terrain height generation using Perlin noise layers.", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("heightMapComputeShader"), new GUIContent("Height Map Shader"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);

            SerializedProperty numOctaves = serializedObject.FindProperty("numOctaves");
            SerializedProperty persistence = serializedObject.FindProperty("persistence");
            SerializedProperty lacunarity = serializedObject.FindProperty("lacunarity");
            SerializedProperty initialScale = serializedObject.FindProperty("initialScale");

            EditorGUILayout.PropertyField(numOctaves, new GUIContent("Octaves", "Number of noise layers"));
            EditorGUILayout.PropertyField(persistence, new GUIContent("Persistence", "Amplitude decrease per octave"));
            EditorGUILayout.PropertyField(lacunarity, new GUIContent("Lacunarity", "Frequency increase per octave"));
            EditorGUILayout.PropertyField(initialScale, new GUIContent("Initial Scale", "Starting noise scale"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawErosionSettings()
    {
        EditorGUILayout.BeginVertical("box");
        showErosionSettings = EditorGUILayout.Foldout(showErosionSettings, "🌊 Erosion & Weathering", true, EditorStyles.foldoutHeader);

        if (showErosionSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Hydraulic erosion simulation using water droplets.", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("erosion"), new GUIContent("Erosion Shader"));

            // Performance settings
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            SerializedProperty numIterations = serializedObject.FindProperty("numErosionIterations");
            SerializedProperty brushRadius = serializedObject.FindProperty("erosionBrushRadius");

            EditorGUILayout.PropertyField(numIterations, new GUIContent("Erosion Iterations", "Number of water droplets to simulate"));
            EditorGUILayout.PropertyField(brushRadius, new GUIContent("Erosion Radius", "Size of erosion brush"));

            // Droplet physics
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Droplet Physics", EditorStyles.boldLabel);

            SerializedProperty maxLifetime = serializedObject.FindProperty("maxLifetime");
            SerializedProperty gravity = serializedObject.FindProperty("gravity");
            SerializedProperty startSpeed = serializedObject.FindProperty("startSpeed");
            SerializedProperty startWater = serializedObject.FindProperty("startWater");
            SerializedProperty inertia = serializedObject.FindProperty("inertia");
            SerializedProperty evaporateSpeed = serializedObject.FindProperty("evaporateSpeed");

            EditorGUILayout.PropertyField(maxLifetime, new GUIContent("Max Lifetime", "Steps before droplet dies"));
            EditorGUILayout.PropertyField(gravity, new GUIContent("Gravity", "Acceleration factor"));
            EditorGUILayout.PropertyField(startSpeed, new GUIContent("Initial Speed"));
            EditorGUILayout.PropertyField(startWater, new GUIContent("Initial Volume"));
            EditorGUILayout.PropertyField(inertia, new GUIContent("Inertia", "Resistance to direction change"));
            EditorGUILayout.PropertyField(evaporateSpeed, new GUIContent("Evaporation Rate"));

            // Erosion & deposition
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Erosion & Deposition", EditorStyles.boldLabel);

            SerializedProperty sedimentCapacity = serializedObject.FindProperty("sedimentCapacityFactor");
            SerializedProperty minSedimentCapacity = serializedObject.FindProperty("minSedimentCapacity");
            SerializedProperty depositSpeed = serializedObject.FindProperty("depositSpeed");
            SerializedProperty erodeSpeed = serializedObject.FindProperty("erodeSpeed");

            EditorGUILayout.PropertyField(sedimentCapacity, new GUIContent("Sediment Capacity", "How much sediment droplets can carry"));
            EditorGUILayout.PropertyField(minSedimentCapacity, new GUIContent("Min Capacity", "Minimum carrying capacity"));
            EditorGUILayout.PropertyField(depositSpeed, new GUIContent("Deposition Rate"));
            EditorGUILayout.PropertyField(erodeSpeed, new GUIContent("Erosion Rate"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWaterSystem()
    {
        EditorGUILayout.BeginVertical("box");
        showWaterSystem = EditorGUILayout.Foldout(showWaterSystem, "💧 Water System", true, EditorStyles.foldoutHeader);

        if (showWaterSystem)
        {
            EditorGUI.indentLevel++;

            // Water enable/disable
            EditorGUI.BeginChangeCheck();
            bool enableWater = EditorGUILayout.Toggle("Enable Water", terrainGenerator.enableWater);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(terrainGenerator, "Toggle Water");
                terrainGenerator.enableWater = enableWater;
                if (!Application.isPlaying)
                {
                    terrainGenerator.ConstructMesh();
                }
            }

            if (terrainGenerator.enableWater)
            {
                // Water level slider
                EditorGUI.BeginChangeCheck();
                float waterLevel = EditorGUILayout.Slider("Water Level", terrainGenerator.waterLevel, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(terrainGenerator, "Change Water Level");
                    terrainGenerator.waterLevel = waterLevel;
                    if (!Application.isPlaying)
                    {
                        terrainGenerator.ConstructMesh();
                    }
                }

                // Water info
                float actualHeight = terrainGenerator.waterLevel * terrainGenerator.elevationScale;
                EditorGUILayout.LabelField($"Actual Height: {actualHeight:F2} units");

                // Water material and scale
                SerializedProperty waterMaterial = serializedObject.FindProperty("waterMaterial");
                SerializedProperty waterScale = serializedObject.FindProperty("waterScale");
                SerializedProperty waterSkirtHeight = serializedObject.FindProperty("waterSkirtHeight");

                EditorGUILayout.PropertyField(waterMaterial);
                EditorGUILayout.PropertyField(waterScale, new GUIContent("Water Plane Scale"));
                EditorGUILayout.PropertyField(waterSkirtHeight, new GUIContent("Water Skirt Height"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void GenerateCompleteWorld()
    {
        terrainGenerator.GenerateHeightMap();
        terrainGenerator.Erode();
        terrainGenerator.ConstructMesh();
    }

    void OnEnable()
    {
        terrainGenerator = (TerrainGenerator)target;
        Tools.hidden = true;
    }

    void OnDisable()
    {
        Tools.hidden = false;
    }
}