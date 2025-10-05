#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TerrainErosion
{
    // This attribute links this editor script to the TerrainErosionSystem component.
    [CustomEditor(typeof(TerrainErosionSystem))]
    public class TerrainErosionSystemEditor : Editor
    {
        // This method draws the custom UI in the Inspector.
        public override void OnInspectorGUI()
        {
            // Draw the default fields (width, height, erosionIterations, etc.)
            DrawDefaultInspector();

            // Get a reference to the script we are inspecting.
            TerrainErosionSystem system = (TerrainErosionSystem)target;

            EditorGUILayout.Space(10); // Add some vertical space
            EditorGUILayout.LabelField("Manual Controls", EditorStyles.boldLabel);

            // --- Control Buttons ---

            if (GUILayout.Button("Initialize / Reset"))
            {
                // A confirmation dialog is good practice for destructive actions.
                if (EditorUtility.DisplayDialog("Initialize Terrain",
                    "Are you sure you want to discard current terrain data and re-initialize?", "Yes", "Cancel"))
                {
                    system.Initialize();
                }
            }

            if (GUILayout.Button("Run Erosion"))
            {
                // Tell the system to run the full erosion process.
                system.RunErosion();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Data Export", EditorStyles.boldLabel);

            // --- Export Buttons ---

            if (GUILayout.Button("Export Height Texture"))
            {
                Texture2D tex = system.ExportHeightTexture();
                string path = EditorUtility.SaveFilePanel("Save Height Texture",
                    "", "height.png", "png");

                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.Refresh(); // Tell Unity to import the new file
                }
            }

            if (GUILayout.Button("Export Material Texture"))
            {
                Texture2D tex = system.ExportMaterialTexture();
                string path = EditorUtility.SaveFilePanel("Save Material Texture",
                    "", "material.png", "png");

                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.Refresh();
                }
            }
        }
    }
}
#endif