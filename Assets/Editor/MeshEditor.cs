
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class MeshEditor : Editor
{
    TerrainGenerator terrainGenerator;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Terrain"))
        {
            terrainGenerator.GenerateTerrain();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Building Mode", EditorStyles.boldLabel);

        BuildingModeController buildingModeController = terrainGenerator.GetComponent<BuildingModeController>();
        if (buildingModeController == null)
        {
            if (GUILayout.Button("Add Building Mode Controller"))
            {
                buildingModeController = terrainGenerator.gameObject.AddComponent<BuildingModeController>();
                Debug.Log("BuildingModeController component was added.");
            }
        }
        else
        {
            // Building mode toggle
            bool buildingMode = buildingModeController.buildingModeEnabled;
            bool newBuildingMode = EditorGUILayout.Toggle("Building Mode Enabled", buildingMode);

            if (newBuildingMode != buildingMode)
            {
                Undo.RecordObject(buildingModeController, "Toggle Building Mode");
                buildingModeController.buildingModeEnabled = newBuildingMode;
                EditorUtility.SetDirty(buildingModeController);
            }

            if (buildingModeController.buildingModeEnabled)
            {
                EditorGUILayout.HelpBox("Building Mode is ACTIVE - Grid is visible", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Building Mode is OFF - Grid is hidden", MessageType.None);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Building Placement", EditorStyles.boldLabel);

        BuildingPlacer buildingPlacer = terrainGenerator.GetComponent<BuildingPlacer>();
        if (buildingPlacer == null)
        {
            if (GUILayout.Button("Add BuildingPlacer Component"))
            {
                buildingPlacer = terrainGenerator.gameObject.AddComponent<BuildingPlacer>();
                Debug.Log("BuildingPlacer component was added.");
            }
        }
        else
        {
            // Building selection dropdown
            if (buildingPlacer.buildings != null && buildingPlacer.buildings.Count > 0)
            {
                string[] buildingNames = new string[buildingPlacer.buildings.Count];
                for (int i = 0; i < buildingPlacer.buildings.Count; i++)
                {
                    var building = buildingPlacer.buildings[i];
                    string name = !string.IsNullOrEmpty(building.name) ? building.name :
                                  building.prefab != null ? building.prefab.name : $"Building {i}";
                    buildingNames[i] = $"{i}: {name} (Width: {building.width})";
                }

                buildingPlacer.selectedBuildingIndex = EditorGUILayout.Popup(
                    "Selected Building",
                    buildingPlacer.selectedBuildingIndex,
                    buildingNames
                );
            }
            else
            {
                EditorGUILayout.HelpBox("Add buildings to the BuildingPlacer component first!", MessageType.Info);
            }

            if (GUILayout.Button("Place Random Building"))
            {
                buildingPlacer.PlaceRandomBuilding();
            }

            if (GUILayout.Button("Clear All Buildings"))
            {
                if (EditorUtility.DisplayDialog("Clear Buildings",
                    "Remove all placed buildings?", "Yes", "Cancel"))
                {
                    buildingPlacer.ClearAllBuildings();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pathfinding", EditorStyles.boldLabel);

        if (GUILayout.Button("Find and Draw Path"))
        {
            Pathfinder pathfinder = terrainGenerator.GetComponent<Pathfinder>();
            if (pathfinder == null)
            {
                pathfinder = terrainGenerator.gameObject.AddComponent<Pathfinder>();
                Debug.Log("Pathfinder component was added.");
            }
            pathfinder.FindAndDrawPath();
        }
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
