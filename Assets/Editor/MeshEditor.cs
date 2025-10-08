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