using System;
using System.Collections;
using UnityEngine;
namespace Grid_Visualization
{
#if UNITY_EDITOR
  public class GridVisualizer : MonoBehaviour
  {
    [HideInInspector, Range(1f, 10f)] public float sceneButtonSize = 5;
    [HideInInspector, Range(250f, 10000f)] public float sceneButtonDrawDistance = 1000;
    [HideInInspector] public bool Preview = false;
    [HideInInspector] public (Vector3, Vector3, bool, string)[] Cells;
    [HideInInspector] public Color validColor = Color.green;
    [HideInInspector] public Color nonValidColor = Color.red;
    public Action<int, bool> buttonPress;

    public void TriggerButtonPress(int index, bool valid) => buttonPress?.Invoke(index, valid);
    private void Start()
    {
      if (Application.isPlaying)
      {
        Destroy(this);
      }
    }
    private void OnDrawGizmos()
    {
      if (!Preview || Cells == null) return;
      Gizmos.color = validColor;
      foreach ((Vector3 cellCenter, Vector3 cellSize, bool valid, _) in Cells)
      {
        var color = valid ? validColor : nonValidColor;
        Gizmos.color = color;
        Gizmos.DrawWireCube(cellCenter, cellSize);
      }
      Gizmos.color = default;
    }
  }
#endif
}
