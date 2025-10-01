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
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
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
        showVoxelSettings = EditorGUILayout.Foldout(showVoxelSettings, "📦 Voxel & Block Settings", true, EditorStyles.foldoutHeader);

        if (showVoxelSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Voxel generation creates blocky terrain. Configure block types dynamically with custom rules for height, slope, and blending!", MessageType.Info);

            // === BLOCK TYPE COMPUTE SHADER ===
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("⚡ GPU Acceleration", EditorStyles.boldLabel);

            SerializedProperty blockTypeShader = serializedObject.FindProperty("blockTypeComputeShader");
            EditorGUILayout.PropertyField(blockTypeShader, new GUIContent("Block Type Shader", "GPU compute shader for block generation"));

            if (blockTypeShader.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ Block Type Compute Shader is required!", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // === VOXEL SETTINGS ===
            EditorGUI.BeginChangeCheck();

            SerializedProperty voxelSize = serializedObject.FindProperty("voxelSize");
            EditorGUILayout.PropertyField(voxelSize, new GUIContent("Voxel Size", "Size multiplier for individual voxels"));

            SerializedProperty generateVoxelSkirt = serializedObject.FindProperty("generateVoxelSkirt");
            EditorGUILayout.PropertyField(generateVoxelSkirt, new GUIContent("Generate Skirt", "Add voxel skirt around edges"));

            SerializedProperty voxelMaterial = serializedObject.FindProperty("voxelMaterial");
            EditorGUILayout.PropertyField(voxelMaterial, new GUIContent("Voxel Material", "Material for voxel rendering"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (!Application.isPlaying)
                {
                    terrainGenerator.ConstructMesh();
                }
            }

            // === DYNAMIC BLOCK TYPE CONFIGURATION ===
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🎨 Dynamic Block Type Rules", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Each block type has its own rules. They can overlap - higher priority wins!", MessageType.Info);
            if (GUILayout.Button("Reset to Defaults", GUILayout.Width(130)))
            {
                terrainGenerator.SetupDefaultBlockConfigs();
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            SerializedProperty blockConfigs = serializedObject.FindProperty("blockTypeConfigs");

            if (blockConfigs.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No block types configured. Click 'Reset to Defaults' to create starter configurations.", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();

            // Draw each block type config
            for (int i = 0; i < blockConfigs.arraySize; i++)
            {
                SerializedProperty config = blockConfigs.GetArrayElementAtIndex(i);
                DrawBlockTypeConfig(config, i);
            }

            // Add/Remove buttons
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("➕ Add Block Type"))
            {
                blockConfigs.InsertArrayElementAtIndex(blockConfigs.arraySize);
            }

            if (blockConfigs.arraySize > 0 && GUILayout.Button("➖ Remove Last"))
            {
                blockConfigs.DeleteArrayElementAtIndex(blockConfigs.arraySize - 1);
            }

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (!Application.isPlaying)
                {
                    terrainGenerator.ConstructMesh();
                }
            }

            // === VISUAL HEIGHT DISTRIBUTION ===
            DrawHeightDistributionPreview();

            // === STATISTICS ===
            DrawVoxelStatistics();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBlockTypeConfig(SerializedProperty config, int index)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");

        SerializedProperty displayName = config.FindPropertyRelative("displayName");
        SerializedProperty blockType = config.FindPropertyRelative("blockType");
        SerializedProperty previewColor = config.FindPropertyRelative("previewColor");
        SerializedProperty priority = config.FindPropertyRelative("priority");

        // Header with color preview
        EditorGUILayout.BeginHorizontal();

        Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
        EditorGUI.DrawRect(colorRect, previewColor.colorValue);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y + colorRect.height - 1, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, 1, colorRect.height), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x + colorRect.width - 1, colorRect.y, 1, colorRect.height), Color.black);

        bool foldout = config.isExpanded;
        string headerText = $"{displayName.stringValue} (Priority: {priority.intValue})";
        config.isExpanded = EditorGUILayout.Foldout(foldout, headerText, true, EditorStyles.foldoutHeader);

        EditorGUILayout.EndHorizontal();

        if (config.isExpanded)
        {
            EditorGUI.indentLevel++;

            // Identity
            EditorGUILayout.PropertyField(displayName);
            EditorGUILayout.PropertyField(blockType);
            EditorGUILayout.PropertyField(previewColor);

            EditorGUILayout.Space(3);

            // Height Mode Selection
            SerializedProperty heightMode = config.FindPropertyRelative("heightMode");
            EditorGUILayout.PropertyField(heightMode, new GUIContent("Height Mode"));

            EditorGUILayout.Space(3);

            bool isNormalizedMode = heightMode.enumValueIndex == 0;

            if (isNormalizedMode)
            {
                // NORMALIZED HEIGHT MODE (0-1)
                EditorGUILayout.LabelField("Height Rules - Normalized (0-1)", EditorStyles.miniBoldLabel);

                SerializedProperty minHeight = config.FindPropertyRelative("minHeight");
                SerializedProperty maxHeight = config.FindPropertyRelative("maxHeight");
                SerializedProperty heightBlend = config.FindPropertyRelative("heightBlendAmount");

                EditorGUILayout.Slider(minHeight, 0f, 1f, "Min Height");
                EditorGUILayout.Slider(maxHeight, 0f, 1f, "Max Height");
                EditorGUILayout.Slider(heightBlend, 0.01f, 0.5f, "Height Blend");

                // Visual height range
                Rect rangeRect = GUILayoutUtility.GetRect(0, 15);
                rangeRect.x += EditorGUIUtility.labelWidth + 4;
                rangeRect.width -= EditorGUIUtility.labelWidth + 4;

                float minX = rangeRect.x + rangeRect.width * minHeight.floatValue;
                float maxX = rangeRect.x + rangeRect.width * maxHeight.floatValue;

                EditorGUI.DrawRect(new Rect(rangeRect.x, rangeRect.y, rangeRect.width, rangeRect.height), new Color(0.2f, 0.2f, 0.2f));
                EditorGUI.DrawRect(new Rect(minX, rangeRect.y, maxX - minX, rangeRect.height), previewColor.colorValue * 0.7f);
            }
            else
            {
                // VOXEL LAYER MODE (Absolute)
                EditorGUILayout.LabelField("Height Rules - Voxel Layers", EditorStyles.miniBoldLabel);

                // Calculate max possible voxel layers for reference
                float cellSize = (terrainGenerator.scale * 2f) / (terrainGenerator.mapSize - 1);
                float actualVoxelSize = cellSize * terrainGenerator.voxelSize;
                int maxPossibleLayers = Mathf.Max(1, Mathf.RoundToInt(terrainGenerator.elevationScale / actualVoxelSize));

                SerializedProperty minVoxelLayer = config.FindPropertyRelative("minVoxelLayer");
                SerializedProperty maxVoxelLayer = config.FindPropertyRelative("maxVoxelLayer");
                SerializedProperty voxelLayerBlend = config.FindPropertyRelative("voxelLayerBlend");

                EditorGUILayout.IntSlider(minVoxelLayer, 0, maxPossibleLayers, "Min Voxel Layer");
                EditorGUILayout.IntSlider(maxVoxelLayer, 0, maxPossibleLayers, "Max Voxel Layer");
                EditorGUILayout.IntSlider(voxelLayerBlend, 1, 20, "Layer Blend Range");

                EditorGUILayout.HelpBox($"Terrain has ~{maxPossibleLayers} voxel layers total\n" +
                    $"Layer 0 = ground, Layer {maxPossibleLayers} = peak", MessageType.Info);

                // Visual voxel layer range
                Rect rangeRect = GUILayoutUtility.GetRect(0, 20);
                rangeRect.x += EditorGUIUtility.labelWidth + 4;
                rangeRect.width -= EditorGUIUtility.labelWidth + 4;

                float minX = rangeRect.x + rangeRect.width * (minVoxelLayer.intValue / (float)maxPossibleLayers);
                float maxX = rangeRect.x + rangeRect.width * (maxVoxelLayer.intValue / (float)maxPossibleLayers);

                EditorGUI.DrawRect(new Rect(rangeRect.x, rangeRect.y, rangeRect.width, rangeRect.height), new Color(0.2f, 0.2f, 0.2f));
                EditorGUI.DrawRect(new Rect(minX, rangeRect.y, maxX - minX, rangeRect.height), previewColor.colorValue * 0.7f);

                // Draw layer markers
                GUIStyle layerStyle = new GUIStyle(EditorStyles.miniLabel);
                layerStyle.alignment = TextAnchor.UpperCenter;
                layerStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(minX - 10, rangeRect.y + rangeRect.height + 2, 20, 15), minVoxelLayer.intValue.ToString(), layerStyle);
                GUI.Label(new Rect(maxX - 10, rangeRect.y + rangeRect.height + 2, 20, 15), maxVoxelLayer.intValue.ToString(), layerStyle);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Slope Rules", EditorStyles.miniBoldLabel);

            SerializedProperty minSlope = config.FindPropertyRelative("minSlope");
            SerializedProperty maxSlope = config.FindPropertyRelative("maxSlope");
            SerializedProperty slopeBlend = config.FindPropertyRelative("slopeBlendAmount");

            EditorGUILayout.Slider(minSlope, 0f, 2f, "Min Slope");
            EditorGUILayout.Slider(maxSlope, 0f, 2f, "Max Slope");
            EditorGUILayout.Slider(slopeBlend, 0.01f, 0.5f, "Slope Blend");

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Priority & Influence", EditorStyles.miniBoldLabel);

            EditorGUILayout.IntSlider(priority, 0, 10, "Priority");

            SerializedProperty strength = config.FindPropertyRelative("strength");
            EditorGUILayout.Slider(strength, 0f, 1f, "Strength");

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Noise Variation", EditorStyles.miniBoldLabel);

            SerializedProperty useNoise = config.FindPropertyRelative("useNoiseVariation");
            SerializedProperty noiseInfluence = config.FindPropertyRelative("noiseInfluence");

            EditorGUILayout.PropertyField(useNoise, new GUIContent("Use Noise"));
            if (useNoise.boolValue)
            {
                EditorGUILayout.Slider(noiseInfluence, 0f, 1f, "Noise Influence");
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHeightDistributionPreview()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("📊 Height Distribution Preview", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(0, 40);
        rect.x += EditorGUIUtility.labelWidth;
        rect.width -= EditorGUIUtility.labelWidth;

        // Background
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

        // Draw each block type's range
        SerializedProperty blockConfigs = serializedObject.FindProperty("blockTypeConfigs");
        for (int i = 0; i < blockConfigs.arraySize; i++)
        {
            SerializedProperty config = blockConfigs.GetArrayElementAtIndex(i);
            SerializedProperty minHeight = config.FindPropertyRelative("minHeight");
            SerializedProperty maxHeight = config.FindPropertyRelative("maxHeight");
            SerializedProperty previewColor = config.FindPropertyRelative("previewColor");
            SerializedProperty displayName = config.FindPropertyRelative("displayName");

            float minX = rect.x + rect.width * minHeight.floatValue;
            float maxX = rect.x + rect.width * maxHeight.floatValue;
            float barHeight = rect.height / blockConfigs.arraySize;
            float yPos = rect.y + i * barHeight;

            // Draw range bar
            Color col = previewColor.colorValue;
            col.a = 0.8f;
            EditorGUI.DrawRect(new Rect(minX, yPos + 2, maxX - minX, barHeight - 4), col);

            // Draw label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(minX + 4, yPos + 2, 100, barHeight - 4), displayName.stringValue, labelStyle);
        }

        // Draw height markers
        for (float h = 0; h <= 1f; h += 0.25f)
        {
            float x = rect.x + rect.width * h;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1, rect.height), new Color(1, 1, 1, 0.3f));

            GUIStyle markerStyle = new GUIStyle(EditorStyles.miniLabel);
            markerStyle.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(x - 20, rect.y + rect.height + 2, 40, 15), h.ToString("F2"), markerStyle);
        }
    }

    private void DrawVoxelStatistics()
    {
        float cellSize = (terrainGenerator.scale * 2f) / (terrainGenerator.mapSize - 1);
        float actualVoxelSize = cellSize * terrainGenerator.voxelSize;
        int maxVoxelLayers = Mathf.RoundToInt(terrainGenerator.elevationScale / actualVoxelSize);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📊 Statistics", EditorStyles.boldLabel);

        SerializedProperty blockConfigs = serializedObject.FindProperty("blockTypeConfigs");
        int numBlockTypes = blockConfigs.arraySize;

        Transform meshHolder = terrainGenerator.transform.Find("Mesh Holder");
        if (meshHolder != null && meshHolder.GetComponent<MeshFilter>() != null &&
            meshHolder.GetComponent<MeshFilter>().sharedMesh != null)
        {
            var mesh = meshHolder.GetComponent<MeshFilter>().sharedMesh;
            EditorGUILayout.HelpBox(
                $"Voxel size: {actualVoxelSize:F2} units\n" +
                $"Max layers: ~{maxVoxelLayers}\n" +
                $"Block types: {numBlockTypes} (dynamic)\n" +
                $"Generation: GPU compute shader\n" +
                $"Mesh: {mesh.vertexCount:N0} verts, {mesh.triangles.Length / 3:N0} tris",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Voxel size: {actualVoxelSize:F2} units\n" +
                $"Max layers: ~{maxVoxelLayers}\n" +
                $"Block types: {numBlockTypes} (dynamic)\n" +
                $"Generation: GPU compute shader",
                MessageType.None);
        }
    }

    private void DrawHeightmapSettings()
    {
        EditorGUILayout.BeginVertical("box");
        showHeightmapSettings = EditorGUILayout.Foldout(showHeightmapSettings, "🗻 Heightmap Generation", true, EditorStyles.foldoutHeader);

        if (showHeightmapSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox("Fractal noise settings for the initial terrain shape.", MessageType.Info);

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

            EditorGUILayout.PropertyField(serializedObject.FindProperty("erosion"), new GUIContent("Erosion Shader"));

            // *** NEW SECTION FOR EROSION RESISTANCE MAP ***
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🪨 Erosion Resistance Map", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Uses a noise map to simulate soil hardness. Whiter areas (hard rock) will erode slower than darker areas (soft soil).", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapComputeShader"), new GUIContent("Noise Map Shader"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapScale"), new GUIContent("Resistance Scale", "Scale of the soil hardness patterns."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapOctaves"), new GUIContent("Resistance Octaves", "Detail in the hardness patterns."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapPersistence"), new GUIContent("Resistance Persistence"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("noiseMapLacunarity"), new GUIContent("Resistance Lacunarity"));

            // Performance settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("💧 Droplet Simulation", EditorStyles.boldLabel);
            SerializedProperty numIterations = serializedObject.FindProperty("numErosionIterations");
            SerializedProperty brushRadius = serializedObject.FindProperty("erosionBrushRadius");

            EditorGUILayout.PropertyField(numIterations, new GUIContent("Erosion Iterations", "Number of water droplets to simulate"));
            EditorGUILayout.PropertyField(brushRadius, new GUIContent("Erosion Radius", "Size of erosion brush"));

            // Droplet physics
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physics", EditorStyles.miniBoldLabel);

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
            EditorGUILayout.LabelField("Sediment", EditorStyles.miniBoldLabel);

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