using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
namespace Grid_Visualization
{
#if UNITY_EDITOR
  [CustomEditor(typeof(GridVisualizer))]
  public class GridHelper : Editor
  {
    private GridVisualizer gridManager;
    private void OnEnable()
    {
      if (!Application.isPlaying && target != null)
        gridManager = target != null ? target.GetComponent<GridVisualizer>() : null; 
    }
    private float BaseSize => gridManager.sceneButtonSize;
    private float DrawDist => gridManager.sceneButtonDrawDistance;
    private void OnSceneGUI()
    {
      if (gridManager == null || !gridManager.Preview) return;
      if (gridManager.Cells == null || Application.isPlaying) return;
      foreach ((Vector3 cellCenter, Vector3 cellSize, bool valid, string coord) in gridManager.Cells)
      {
        float distanceToCamera = Vector3.Distance(cellCenter, SceneView.lastActiveSceneView.camera.transform.position);
        float scale = Mathf.Clamp(distanceToCamera * 0.05f, 0.1f, 5f);
        float scaledSize = BaseSize * scale;
        if (distanceToCamera < DrawDist)
        {
          Handles.Label(cellCenter, coord, new GUIStyle { normal = new GUIStyleState { textColor = gridManager.validColor } });
          if (Handles.Button(cellCenter, SceneView.currentDrawingSceneView.camera.transform.rotation, scaledSize, scaledSize, Handles.RectangleHandleCap))
          {
            string[] parts = coord.Split(',');
            int index = int.Parse(parts[3]);
            //Debug.Log($"Cell clicked: {coord} at index {index}");
            gridManager.TriggerButtonPress(index, !valid);
          }
        }
      }
    }
  }
#endif
}
