// Save this file as: Assets/Editor/TerrainCellHighlighterEditor.cs

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainCellHighlighter))]
public class TerrainCellHighlighterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TerrainCellHighlighter highlighter = (TerrainCellHighlighter)target;
        BuildingPlacer placer = highlighter.GetComponent<BuildingPlacer>();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);

        if (placer == null)
        {
            EditorGUILayout.HelpBox("BuildingPlacer component not found! This component requires BuildingPlacer to function.", MessageType.Error);
            return;
        }

        if (placer.buildings == null || placer.buildings.Count == 0)
        {
            EditorGUILayout.HelpBox("No buildings configured in BuildingPlacer.", MessageType.Info);
            return;
        }

        // Show current building info
        int selectedIndex = placer.selectedBuildingIndex;
        if (selectedIndex >= 0 && selectedIndex < placer.buildings.Count)
        {
            var building = placer.buildings[selectedIndex];
            string buildingName = !string.IsNullOrEmpty(building.name) ? building.name :
                                 building.prefab != null ? building.prefab.name : $"Building {selectedIndex}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Selected Building:", buildingName);
            EditorGUILayout.LabelField("Grid Size:", $"{building.width}x{building.width} cells");
            EditorGUILayout.LabelField("Highlight Radius:", $"{building.width / 2} cells");
            EditorGUILayout.LabelField("Max Height Deviation:", $"{building.maxSlopeDeviation:F2}m");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Show click instructions
            if (highlighter.enableClickPlacement)
            {
                string mouseButton = GetMouseButtonName(highlighter.placementMouseButton);
                EditorGUILayout.HelpBox(
                    $"? Click-to-Place Enabled\n" +
                    $"• Hover over terrain to see grid\n" +
                    $"• Green = Valid placement\n" +
                    $"• Red = Invalid (too steep)\n" +
                    $"• {mouseButton} Click to place building",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Click-to-Place is disabled. Use 'Place Random Building' button in BuildingPlacer.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No building selected in BuildingPlacer.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Go to BuildingPlacer Settings"))
        {
            Selection.activeObject = placer;
        }

        // Runtime info
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
            Repaint();
        }
    }

    string GetMouseButtonName(int button)
    {
        switch (button)
        {
            case 0: return "Left";
            case 1: return "Right";
            case 2: return "Middle";
            default: return "Unknown";
        }
    }
}