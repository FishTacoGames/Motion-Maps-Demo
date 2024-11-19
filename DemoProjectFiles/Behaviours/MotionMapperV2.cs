using Grid_Parameters;
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grid_Parameters
{
  [System.Serializable]
  public struct MotionValues
  {
    [Range(1f, 25f)] public float width;
    [Range(1f, 25f)] public float length;
    [Range(0.1f, 1f)] public float redStrength;
    public readonly Vector4 Vector { get { return new Vector4(redStrength, 0f, length, width); } } // second value is not used yet
  }
}
public class MotionMapperV2 : MonoBehaviour
{
  private GridLoaderV2 loader;
  [SerializeField]
  private ComputeShader positionComputeShader;
  private RenderTexture textureArray;
  public ShaderColliderLink[] links;
  private Vector4[] motionValues;
  private Vector4[] positionValues;
  private Vector4[] sliceOrigins;
  private int threadGroupsX;
  private int threadGroupsY;

  private int textureSize;
  private int worldYBounds = 0;
  private int ysize = 0;
  private int lowerYBound;
  private int upperYBound;
  private int Yboundsofset = 0;
  private float halfHeight = 1024f;
  private ComputeBuffer posBuffer;
  private ComputeBuffer motionBuffer;
  private ComputeBuffer originBuffer;
  public ExpansionLevel expansionLevel = ExpansionLevel.Bottom;
  public RenderTextureFormat renderTextureFormat;

  private void Start()
  {
    sliceOrigins = new Vector4[4];
    loader = FindFirstObjectByType<GridLoaderV2>();
    if (loader == null)
    {
      Debug.LogError("No GridLoaderV2 found in scene. Aborting.");
      return;
    }
    loader.OnMainCellChange += UpdateMotionMap;
    worldYBounds = (int)loader.Gridcellsizey * loader.Gridcelllimitsy;
    ysize = (int)loader.Gridcellsizey;
    halfHeight = (float)ysize / 2f;
    lowerYBound = (int)GetLowerBounds(expansionLevel);
    upperYBound = (int)GetUpperBounds(expansionLevel);
    Yboundsofset = (int)GetNormalizedOffset(expansionLevel);
    textureSize = (int)loader.Gridcellsizexz;
    motionValues = new Vector4[links.Length];

    // motion map setup
    Vector3 baseOrigin = new(loader.ActiveCell.Item1.x, 0, loader.ActiveCell.Item1.z);
    Vector3 activeCellPosition = CalculateCellCenter(new(loader.ActiveCell.Item1.x, 0, loader.ActiveCell.Item1.z));
    Vector3 lastActiveCellPosition = CalculateCellCenter(new(loader.LastActiveCell.x, 0, loader.LastActiveCell.z));
    Vector3 origin3 = CalculateCellCenter(baseOrigin + new Vector3(1, 0, 0));
    Vector3 origin4 = CalculateCellCenter(baseOrigin + new Vector3(2, 0, 0));
    sliceOrigins[0] = new Vector4(activeCellPosition.x, 0, activeCellPosition.z, 0);
    sliceOrigins[1] = new Vector4(lastActiveCellPosition.x, 0, lastActiveCellPosition.z, 0);
    sliceOrigins[2] = new Vector4(origin3.x, 0, origin3.z, 0);
    sliceOrigins[3] = new Vector4(origin4.x, 0, origin4.z, 0);
    Vector4 neworigin = new(activeCellPosition.x, 0, activeCellPosition.z, 0);
    for (int i = 0; i < links.Length; i++)
    {
      motionValues[i] = links[i].motionValues.Vector;
    }
    positionValues = new Vector4[links.Length];
    threadGroupsX = Mathf.CeilToInt(textureSize / 8.0f);
    threadGroupsY = Mathf.CeilToInt(textureSize / 8.0f);

    textureArray = new(textureSize, textureSize, 0)
    {
      dimension = TextureDimension.Tex2DArray,
      volumeDepth = 9,
      format = RenderTextureFormat.ARGBFloat,
      enableRandomWrite = true,
      //filterMode = FilterMode.Point,
      wrapMode = TextureWrapMode.Clamp
    };
    textureArray.Create();
    // Set up compute shader parameters
    positionComputeShader.SetInt("indexToClear", farthestSliceIndex);
    positionComputeShader.SetInt("texSize", textureSize);
    positionComputeShader.SetTexture(0, "ResultArray", textureArray);
    Shader.SetGlobalTexture("_MotionMaps", textureArray, RenderTextureSubElement.Default);
    Shader.SetGlobalVector("_WorldXYZMapping", new Vector4(textureSize, (float)worldYBounds, textureSize / 2f, halfHeight)); // textureArray info to reduce division operations in shader.
    // Initialize and set buffer
    posBuffer = new ComputeBuffer(links.Length, 16);
    motionBuffer = new ComputeBuffer(links.Length, 16);
    originBuffer = new ComputeBuffer(sliceOrigins.Length, 16);
    posBuffer.SetData(positionValues);
    motionBuffer.SetData(motionValues);
    originBuffer.SetData(sliceOrigins);
    positionComputeShader.SetBuffer(0, "positionValues", posBuffer);
    positionComputeShader.SetBuffer(0, "motionValues", motionBuffer);
    positionComputeShader.SetBuffer(0, "sliceOrigins", originBuffer);
  }

  private void Update()
  {
    for (int i = 0; i < links.Length; i++)
    {
      motionValues[i] = links[i].motionValues.Vector;
      // 0 to 1 Height, world height of 0 = 0.500, or world height of 1024 = 1.000, or world height of 5 = 0.529 
      float clampedY = Mathf.Clamp(links[i].transform.position.y, lowerYBound, upperYBound);
      float normalizedY = (clampedY + Yboundsofset) / worldYBounds; // floating point conversion for example from -1024 to 1024 to 0 to 1 if center. 0 to 2048 to 0 to 1 if bottom
      float direction = MapDirectionToFloat(links[i].transform.forward);
      Vector4 newPos = new(links[i].transform.position.x + textureSize / 2f, normalizedY, links[i].transform.position.z + textureSize / 2f, direction);
      positionValues[i] = newPos;
    }

    Shader.SetGlobalVector("_Center1", new Vector2(sliceOrigins[0].x, sliceOrigins[0].z));
    Shader.SetGlobalVector("_Center2", new Vector2(sliceOrigins[1].x, sliceOrigins[1].z));
    Shader.SetGlobalVector("_Center3", new Vector2(sliceOrigins[2].x, sliceOrigins[2].z));
    Shader.SetGlobalVector("_Center4", new Vector2(sliceOrigins[3].x, sliceOrigins[3].z));
    positionComputeShader.SetVectorArray("sliceOrigins", sliceOrigins);
    posBuffer.SetData(positionValues);
    motionBuffer.SetData(motionValues);
    originBuffer.SetData(sliceOrigins);
    positionComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 9);
  }
  // Index for 4 slices
  private int farthestSliceIndex = 10;
  private Coroutine EndSliceClear;

  public void UpdateMotionMap(Vector3 lastcell, Vector3 currentcell)
  {
    Vector3 activeCellPosition = CalculateCellCenter(currentcell);
    Vector4 neworigin = new(activeCellPosition.x, 0, activeCellPosition.z, 0);
    if (sliceOrigins.Contains(neworigin)) return;

    float maxDistance = 0f;
    for (int i = 0; i < 4; i++)
    {
      float distance = Vector3.Distance(sliceOrigins[i], loader.transform.position);
      if (distance > maxDistance)
      {
        maxDistance = distance;
        farthestSliceIndex = i;
      }
    }
    
    // clear slice of the farthest origin and set to new origin
    positionComputeShader.SetInt("indexToClear", farthestSliceIndex);
    sliceOrigins[farthestSliceIndex] = neworigin;
    // small delay for gpu to clear slice
    if (EndSliceClear != null) StopCoroutine(EndSliceClear);
    EndSliceClear = StartCoroutine(EndClearSlice());
  }
  private IEnumerator EndClearSlice()
  {
    yield return new WaitForSeconds(0.15f);
    positionComputeShader.SetInt("indexToClear", 10);
  }

  public float MapDirectionToFloat(Vector3 forward)
  {
    Vector3 projectedForward = new(forward.x, 0, forward.z);
    if (projectedForward.magnitude == 0) return 0;
    projectedForward.Normalize();
    float angle = Mathf.Atan2(projectedForward.z, projectedForward.x) * Mathf.Rad2Deg;
    if (angle < 0) angle += 360;
    float direction = angle / 45f;
    return direction;
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
    if (loader != null)
    {
      loader.OnMainCellChange -= UpdateMotionMap;
    }
  }
  float GetLowerBounds(ExpansionLevel level)
  {
    return level switch
    {
      ExpansionLevel.Top => (float)-ysize,
      ExpansionLevel.Center => (float)-ysize / 2,
      // ExpansionLevel.Bottom:
      _ => 0f,
    };
  }
  float GetUpperBounds(ExpansionLevel level)
  {
    return level switch
    {
      ExpansionLevel.Center => (float)ysize / 2,
      ExpansionLevel.Bottom => (float)ysize,
      // ExpansionLevel.Top:
      _ => 0f
    };
  }
  float GetNormalizedOffset(ExpansionLevel level)
  {
    return level switch
    {
      ExpansionLevel.Center => (float)ysize / 2,
      ExpansionLevel.Top => (float)ysize,
      _ => 0f
    };
  }
  private Vector3 CalculateCellCenter(Vector3 cell) =>
  loader.Origin + new Vector3(cell.x * textureSize + textureSize / 2, 0, cell.z * textureSize + textureSize / 2);

#if UNITY_EDITOR

  private void OnDrawGizmos()
  {
    if (sliceOrigins == null || sliceOrigins.Length == 0) return;
    Gizmos.color = Color.red;
    foreach (var origin in sliceOrigins)
    {
      Gizmos.DrawWireCube(origin, new Vector3(textureSize, ysize, textureSize));
      Handles.Label(origin, origin.ToString());
    }
  }
#endif
}
