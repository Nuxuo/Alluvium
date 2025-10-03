using UnityEditor;
using UnityEngine;
using VoxelTerrain.Data;

[CustomEditor(typeof(BiomePreset))]
public class BiomePresetEditor : Editor
{
    private BiomePreset preset;
    private bool showTerrainRules = true;
    private bool showResourceRules = true;

    private void OnEnable()
    {
        preset = (BiomePreset)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Header
        DrawHeader();

        EditorGUILayout.Space();

        // Identity
        DrawIdentity();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Heightmap settings
        DrawHeightmapSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Erosion settings
        DrawErosionSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Terrain rules
        DrawTerrainRules();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Resource rules
        DrawResourceRules();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        var style = new GUIStyle(EditorStyles.largeLabel);
        style.fontSize = 16;
        style.fontStyle = FontStyle.Bold;
        EditorGUILayout.LabelField($"🏔️ {preset.biomeName}", style);
        EditorGUILayout.EndVertical();
    }

    private void DrawIdentity()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Biome Identity", EditorStyles.boldLabel);

        SerializedProperty biomeName = serializedObject.FindProperty("biomeName");
        EditorGUILayout.PropertyField(biomeName);

        SerializedProperty description = serializedObject.FindProperty("description");
        EditorGUILayout.PropertyField(description);

        EditorGUILayout.EndVertical();
    }

    private void DrawHeightmapSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🗻 Heightmap Generation", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("numOctaves"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("persistence"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lacunarity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("elevationScale"));

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Octaves: More = more detail\n" +
            "Persistence: Higher = rougher terrain\n" +
            "Lacunarity: Frequency multiplier\n" +
            "Elevation Scale: Maximum height in units",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawErosionSettings()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🌊 Erosion Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("erosionIterations"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("erosionBrushRadius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("erosionStrength"));

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "More iterations = more realistic but slower\n" +
            "Higher strength = more dramatic erosion",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawTerrainRules()
    {
        EditorGUILayout.BeginVertical("box");

        showTerrainRules = EditorGUILayout.Foldout(showTerrainRules, "🎨 Terrain Type Rules", true, EditorStyles.foldoutHeader);

        if (showTerrainRules)
        {
            SerializedProperty terrainRules = serializedObject.FindProperty("terrainRules");

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Define where each terrain type appears based on height and slope", MessageType.Info);

            if (terrainRules.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No terrain rules defined. Add rules to define terrain appearance.", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            // Draw each rule
            for (int i = 0; i < terrainRules.arraySize; i++)
            {
                DrawTerrainRule(terrainRules.GetArrayElementAtIndex(i), i);
            }

            EditorGUILayout.Space(5);

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Terrain Rule"))
            {
                terrainRules.InsertArrayElementAtIndex(terrainRules.arraySize);
            }
            if (terrainRules.arraySize > 0 && GUILayout.Button("➖ Remove Last"))
            {
                terrainRules.DeleteArrayElementAtIndex(terrainRules.arraySize - 1);
            }
            EditorGUILayout.EndHorizontal();

            // Visual preview
            DrawTerrainRulesPreview();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTerrainRule(SerializedProperty rule, int index)
    {
        EditorGUILayout.BeginVertical("box");

        SerializedProperty ruleName = rule.FindPropertyRelative("ruleName");
        SerializedProperty terrainType = rule.FindPropertyRelative("terrainType");
        SerializedProperty previewColor = rule.FindPropertyRelative("previewColor");
        SerializedProperty priority = rule.FindPropertyRelative("priority");

        // Header
        EditorGUILayout.BeginHorizontal();

        Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
        EditorGUI.DrawRect(colorRect, previewColor.colorValue);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y + colorRect.height - 1, colorRect.width, 1), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, 1, colorRect.height), Color.black);
        EditorGUI.DrawRect(new Rect(colorRect.x + colorRect.width - 1, colorRect.y, 1, colorRect.height), Color.black);

        rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded,
            $"{ruleName.stringValue} - {terrainType.enumDisplayNames[terrainType.enumValueIndex]} (Priority: {priority.intValue})",
            true);

        EditorGUILayout.EndHorizontal();

        if (rule.isExpanded)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(ruleName);
            EditorGUILayout.PropertyField(terrainType);
            EditorGUILayout.PropertyField(previewColor);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Height Conditions", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(rule.FindPropertyRelative("minHeight"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxHeight"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("heightBlend"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Slope Conditions", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(rule.FindPropertyRelative("minSlope"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxSlope"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("slopeBlend"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Priority & Strength", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(priority);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("strength"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Variation", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(rule.FindPropertyRelative("useNoise"));
            if (rule.FindPropertyRelative("useNoise").boolValue)
            {
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("noiseInfluence"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawTerrainRulesPreview()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Height Distribution Preview", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(0, 60);
        rect.x += 5;
        rect.width -= 10;

        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

        SerializedProperty terrainRules = serializedObject.FindProperty("terrainRules");

        float barHeight = rect.height / Mathf.Max(1, terrainRules.arraySize);

        for (int i = 0; i < terrainRules.arraySize; i++)
        {
            SerializedProperty rule = terrainRules.GetArrayElementAtIndex(i);
            SerializedProperty minHeight = rule.FindPropertyRelative("minHeight");
            SerializedProperty maxHeight = rule.FindPropertyRelative("maxHeight");
            SerializedProperty previewColor = rule.FindPropertyRelative("previewColor");
            SerializedProperty ruleName = rule.FindPropertyRelative("ruleName");

            float minX = rect.x + rect.width * minHeight.floatValue;
            float maxX = rect.x + rect.width * maxHeight.floatValue;
            float yPos = rect.y + i * barHeight;

            Color col = previewColor.colorValue;
            col.a = 0.9f;
            EditorGUI.DrawRect(new Rect(minX, yPos + 1, maxX - minX, barHeight - 2), col);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(minX + 3, yPos + 1, 150, barHeight - 2), ruleName.stringValue, labelStyle);
        }

        // Height markers
        for (float h = 0; h <= 1f; h += 0.25f)
        {
            float x = rect.x + rect.width * h;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1, rect.height), new Color(1, 1, 1, 0.5f));

            GUIStyle markerStyle = new GUIStyle(EditorStyles.miniLabel);
            markerStyle.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(x - 15, rect.y + rect.height + 2, 30, 15), h.ToString("F2"), markerStyle);
        }
    }

    private void DrawResourceRules()
    {
        EditorGUILayout.BeginVertical("box");

        showResourceRules = EditorGUILayout.Foldout(showResourceRules, "⛏️ Resource Placement Rules", true, EditorStyles.foldoutHeader);

        if (showResourceRules)
        {
            SerializedProperty resourceRules = serializedObject.FindProperty("resourceRules");

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Define where resources spawn and how abundant they are", MessageType.Info);

            if (resourceRules.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No resource rules defined. Add rules to place minable resources.", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            // Draw each rule
            for (int i = 0; i < resourceRules.arraySize; i++)
            {
                DrawResourceRule(resourceRules.GetArrayElementAtIndex(i), i);
            }

            EditorGUILayout.Space(5);

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Resource Rule"))
            {
                resourceRules.InsertArrayElementAtIndex(resourceRules.arraySize);
            }
            if (resourceRules.arraySize > 0 && GUILayout.Button("➖ Remove Last"))
            {
                resourceRules.DeleteArrayElementAtIndex(resourceRules.arraySize - 1);
            }
            EditorGUILayout.EndHorizontal();

            // Statistics
            DrawResourceStatistics();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawResourceRule(SerializedProperty rule, int index)
    {
        EditorGUILayout.BeginVertical("box");

        SerializedProperty ruleName = rule.FindPropertyRelative("ruleName");
        SerializedProperty resourceType = rule.FindPropertyRelative("resourceType");
        SerializedProperty spawnProbability = rule.FindPropertyRelative("spawnProbability");

        // Header
        rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded,
            $"{ruleName.stringValue} - {resourceType.enumDisplayNames[resourceType.enumValueIndex]} ({spawnProbability.floatValue * 100:F0}% spawn)",
            true);

        if (rule.isExpanded)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(ruleName);
            EditorGUILayout.PropertyField(resourceType);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Valid Terrain Types", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("validTerrainTypes"), true);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Placement Conditions", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(rule.FindPropertyRelative("minHeight"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxHeight"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("minSlope"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxSlope"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Density Settings", EditorStyles.miniBoldLabel);

            EditorGUILayout.Slider(spawnProbability, 0f, 1f, "Spawn Probability");
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("minDensity"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxDensity"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Clustering", EditorStyles.miniBoldLabel);

            SerializedProperty useClustering = rule.FindPropertyRelative("useClustering");
            EditorGUILayout.PropertyField(useClustering);

            if (useClustering.boolValue)
            {
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("clusterScale"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("clusterThreshold"));

                EditorGUILayout.HelpBox("Clustering makes resources appear in groups rather than randomly scattered", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawResourceStatistics()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Resource Statistics", EditorStyles.boldLabel);

        SerializedProperty resourceRules = serializedObject.FindProperty("resourceRules");

        if (resourceRules.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No resources configured", MessageType.None);
            return;
        }

        EditorGUILayout.BeginVertical("box");

        for (int i = 0; i < resourceRules.arraySize; i++)
        {
            SerializedProperty rule = resourceRules.GetArrayElementAtIndex(i);
            SerializedProperty resourceType = rule.FindPropertyRelative("resourceType");
            SerializedProperty spawnProbability = rule.FindPropertyRelative("spawnProbability");
            SerializedProperty minDensity = rule.FindPropertyRelative("minDensity");
            SerializedProperty maxDensity = rule.FindPropertyRelative("maxDensity");

            string resourceName = resourceType.enumDisplayNames[resourceType.enumValueIndex];
            float avgDensity = (minDensity.intValue + maxDensity.intValue) / 2f;

            EditorGUILayout.LabelField($"• {resourceName}: {spawnProbability.floatValue * 100:F1}% spawn, {avgDensity:F0} avg richness");
        }

        EditorGUILayout.EndVertical();
    }
}