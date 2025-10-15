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