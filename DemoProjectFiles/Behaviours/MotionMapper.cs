using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public enum MotionMapResolution
{
  Low_256px_9mb = 256,
  Medium_512px_36mb = 512,
  MediumHigh_1k_144mb = 1024,
  High_2k_576mb = 2048,
  VeryHigh_4k_2300mb = 4096
}

public enum AxisSize
{
  Lowest_16 = 16,
  ExtremeLow_32 = 32,
  VeryLow_64 = 64,
  Low_128 = 128,
  MediumLow_256 = 256,
  Medium_512 = 512,
  MediumHigh_1024 = 1024,
  High_2048 = 2048,
  VeryHigh_4096 = 4096
}


public class MotionMapper : MonoBehaviour
{
  [Tooltip ("3x3 RenderTexture grid"),SerializeField] private MotionMapResolution motionResolution = MotionMapResolution.MediumHigh_1k_144mb;
  [SerializeField] private AxisSize CellXZAxisSize = AxisSize.Low_128;
  [SerializeField] private AxisSize CellYAxisSize = AxisSize.MediumLow_256;
  [SerializeField] private Transform player;
  [SerializeField] private ComputeShader motionMap;
  [SerializeField] private ShaderColliderLink[] links;
  private ComputeBuffer posBuffer;
  private ComputeBuffer motionBuffer;
  private ComputeBuffer originBuffer;
  private RenderTexture textureArray;
  private Queue<int> cellUpdateQueue;
  private Vector4[] motionValues;
  private Vector4[] positionValues;
  private Vector3Int currentCell;
  private Vector4[] sliceOrigins; // World positions for each cell
  private Vector3Int[] cellOffsets;
  private int textureSliceResolution;
  private int cellSize;
  private int halfCellSize;
  private int ysize;
  private float halfYSize;
  private int threadGroupsX;
  private int threadGroupsY;
  private bool isUpdating = false;
  private void Start()
  {
    InitializeGrid();
  }
  private void OnDestroy()
  {
    if (textureArray != null)
    {
      textureArray.Release();
      textureArray = null;
    }
    if (posBuffer != null)
    {
      posBuffer.Release();
      posBuffer = null;
    }
    if (motionBuffer != null)
    {
      motionBuffer.Release();
      motionBuffer = null;
    }
    if (originBuffer != null)
    {
      originBuffer.Release();
      originBuffer = null;
    }
  }
  void InitializeGrid()
  {
    cellSize = (int)CellXZAxisSize;
    halfCellSize = cellSize / 2;
    ysize = (int)CellYAxisSize;
    halfYSize = ysize / 2;
    textureSliceResolution = (int)motionResolution;
    motionValues = new Vector4[links.Length];
    for (int i = 0; i < links.Length; i++)
    {
      motionValues[i] = links[i].motionValues.Vector;
    }
    positionValues = new Vector4[links.Length];
    threadGroupsX = Mathf.CeilToInt(textureSliceResolution / 8.0f);
    threadGroupsY = Mathf.CeilToInt(textureSliceResolution / 8.0f);

    textureArray = new(textureSliceResolution, textureSliceResolution, 0)
    {
      dimension = TextureDimension.Tex2DArray,
      volumeDepth = 9,
      format = RenderTextureFormat.ARGBFloat,
      enableRandomWrite = true,
      //filterMode = FilterMode.Point,
      wrapMode = TextureWrapMode.Clamp
    };
    textureArray.Create();
    cellUpdateQueue = new Queue<int>();

    sliceOrigins = new Vector4[9];
    cellOffsets = new Vector3Int[9];
    for (int i = 0; i < 9; i++)
    {
      // Offsets for a 3x3 grid (centered on the middle cell)
      cellOffsets[i] = new Vector3Int(i % 3 - 1, 0, i / 3 - 1);
      sliceOrigins[i] = CalculateCellOrigin(currentCell + cellOffsets[i]);
    }
    // Set up compute shader parameters
    motionMap.SetInt("indexToClear", 10);
    motionMap.SetInt("cellSize", cellSize);
    motionMap.SetInt("textureSize", textureSliceResolution);
    motionMap.SetTexture(0, "ResultArray", textureArray);
    Shader.SetGlobalFloat("_textureSize", textureSliceResolution);
    Shader.SetGlobalTexture("_MotionMaps", textureArray, RenderTextureSubElement.Default);
    Shader.SetGlobalVector("_WorldXYZMapping", new Vector4(cellSize, (float)ysize, cellSize / 2f, halfYSize)); // info to reduce division operations in shader.

    // Initialize and set buffer
    posBuffer = new ComputeBuffer(links.Length, 16);
    motionBuffer = new ComputeBuffer(links.Length, 16);
    originBuffer = new ComputeBuffer(sliceOrigins.Length, 16);
    posBuffer.SetData(positionValues);
    motionBuffer.SetData(motionValues);
    originBuffer.SetData(sliceOrigins);
    motionMap.SetBuffer(0, "positionValues", posBuffer);
    motionMap.SetBuffer(0, "motionValues", motionBuffer);
    motionMap.SetBuffer(0, "sliceOrigins", originBuffer);
    EnqueueCellUpdates(currentCell);
  }

  private void Update()
  {
    Vector3Int newCell = GetPlayerCell();
    if (newCell != currentCell)
    {
      EnqueueCellUpdates(newCell);
      currentCell = newCell;
    }

    if (!isUpdating && cellUpdateQueue.Count > 0)
    {
      StartCoroutine(ProcessCellUpdateQueue());
    }
    for (int i = 0; i < links.Length; i++)
    {
      motionValues[i] = links[i].motionValues.Vector;
      motionValues[i].y /= ysize;
      float clampedY = Mathf.Clamp(links[i].transform.position.y, -halfYSize, halfYSize);
      float normalizedY = (clampedY + halfYSize) / ysize; // floating point conversion. for example: ysize of 1024; world height of 5 = 0.529
      float direction;
      direction = MapDirectionToFloat(links[i].transform.forward);

      Vector4 newPos = new(
      (links[i].transform.position.x),
      normalizedY,
      (links[i].transform.position.z),
      direction
      );
      positionValues[i] = newPos;
    }
    posBuffer.SetData(positionValues);
    motionBuffer.SetData(motionValues);
    motionMap.Dispatch(0, threadGroupsX, threadGroupsY, 9);
  }
  private IEnumerator ProcessCellUpdateQueue()
  {
    isUpdating = true;

    while (cellUpdateQueue.Count > 0)
    {
      int i = cellUpdateQueue.Dequeue();
      Vector4 newOrigin = CalculateCellOrigin(currentCell + cellOffsets[i]);
      motionMap.SetInt("indexToClear", i);
      yield return null;
      sliceOrigins[i] = newOrigin;
      motionMap.SetInt("indexToClear", 10);
    }
    UpdateShaderVariables();
    isUpdating = false;
  }
  private void UpdateShaderVariables()
  {
    Shader.SetGlobalVector("_Center0", new Vector2(sliceOrigins[0].x, sliceOrigins[0].z));
    Shader.SetGlobalVector("_Center1", new Vector2(sliceOrigins[1].x, sliceOrigins[1].z));
    Shader.SetGlobalVector("_Center2", new Vector2(sliceOrigins[2].x, sliceOrigins[2].z));
    Shader.SetGlobalVector("_Center3", new Vector2(sliceOrigins[3].x, sliceOrigins[3].z));
    Shader.SetGlobalVector("_Center4", new Vector2(sliceOrigins[4].x, sliceOrigins[4].z));
    Shader.SetGlobalVector("_Center5", new Vector2(sliceOrigins[5].x, sliceOrigins[5].z));
    Shader.SetGlobalVector("_Center6", new Vector2(sliceOrigins[6].x, sliceOrigins[6].z));
    Shader.SetGlobalVector("_Center7", new Vector2(sliceOrigins[7].x, sliceOrigins[7].z));
    Shader.SetGlobalVector("_Center8", new Vector2(sliceOrigins[8].x, sliceOrigins[8].z));
    motionMap.SetVectorArray("sliceOrigins", sliceOrigins);
    originBuffer.SetData(sliceOrigins);
  }
  private Vector3Int GetPlayerCell()
  {
    Vector3 playerPos = player.position;
    return new Vector3Int(
        Mathf.FloorToInt(playerPos.x / cellSize),
        Mathf.FloorToInt(playerPos.y / ysize),
        Mathf.FloorToInt(playerPos.z / cellSize)
    );
  }
  private void EnqueueCellUpdates(Vector3Int newCell)
  {
    Vector3Int delta = newCell - currentCell;

    for (int i = 0; i < 9; i++)
    {
      Vector4 oldOrigin = sliceOrigins[i];
      cellOffsets[i] -= delta;
      cellOffsets[i].x = WrapCoordinate(cellOffsets[i].x);
      cellOffsets[i].z = WrapCoordinate(cellOffsets[i].z);

      Vector4 newOrigin = CalculateCellOrigin(newCell + cellOffsets[i]);
      if (newOrigin != oldOrigin)
      {
        cellUpdateQueue.Enqueue(i);
      }
    }
  }

  private int WrapCoordinate(int coord) => ((coord + 1 + 3) % 3) - 1;

  private Vector4 CalculateCellOrigin(Vector3Int cellPos) =>
    // world space
    new(
        cellPos.x * cellSize + halfCellSize,
        0,
        cellPos.z * cellSize + halfCellSize,
        0
    );
  public float MapDirectionToFloat(Vector3 forward)
  {
    Vector3 projectedForward = new(forward.x, 0, forward.z);
    if (projectedForward.magnitude == 0) return 0;
    projectedForward.Normalize();
    float angle = Mathf.Atan2(projectedForward.z, projectedForward.x);
    if (angle < 0) angle += 2 * Mathf.PI;
    return angle;//angle in radians
  }
  public float MapDirectionOfTravelToFloat(Vector3 velocity, Transform rbTransform)
  {
    Vector3 projectedVelocity = new (velocity.x, 0, velocity.z);
    if (projectedVelocity.magnitude == 0) return 0;
    projectedVelocity.Normalize();
    Vector3 forward = new Vector3(rbTransform.forward.x, 0, rbTransform.forward.z).normalized;
    float angle = Mathf.Atan2(projectedVelocity.z, projectedVelocity.x) - Mathf.Atan2(forward.z, forward.x);
    if (angle < 0) angle += 2 * Mathf.PI;
    return angle; // Angle in radians
  }
#if UNITY_EDITOR
  private void OnDrawGizmos()
  {
    if (sliceOrigins == null || sliceOrigins.Length == 0) return;
    Gizmos.color = Color.white;
    for (int i = 0; i < sliceOrigins.Length; i++)
    {
      Vector3 cellCenter = new(sliceOrigins[i].x, 0, sliceOrigins[i].z);
      Gizmos.DrawWireCube(cellCenter, new Vector3(cellSize, ysize, cellSize));
      UnityEditor.Handles.Label(cellCenter, $"Cell {i}\n{cellOffsets[i]}");
    }
    // Draw links
    Gizmos.color = new Color(0.0f, 0.0f, 0.5f, 0.5f);
    Gizmos.DrawSphere(player.position, 0.03f);
    Gizmos.color = Color.cyan;
    for (int i = 0; i < links.Length; i++)
    {
      if (links[i].motionValues.UseRb)
      {
        Vector3 velocity = links[i].motionValues.collider.attachedRigidbody.linearVelocity;
        Vector3 projectedVelocity = new(velocity.x, 0, velocity.z);
        Gizmos.DrawLine(player.position, player.position + projectedVelocity.normalized * 3f);
        UnityEditor.Handles.Label(player.position + projectedVelocity.normalized * 3f, $"Speed: {projectedVelocity.magnitude:F2}");
      }
      else
      {
        Vector3 projectedforward = new(player.forward.x, 0, player.forward.z);
        Gizmos.DrawLine(links[i].transform.position, links[i].transform.position + projectedforward * 3f);
      }
    }
    // Draw current cell
    Gizmos.color = Color.yellow;
    Vector3 currentCellCenter = CalculateCellOrigin(currentCell);
    Gizmos.DrawWireCube(new Vector3(currentCellCenter.x, 0, currentCellCenter.z), new Vector3(cellSize, ysize, cellSize));
  }
#endif
}
