using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GridLoaderV2 : MonoBehaviour
{
  //Does not change at runtime
  private int cellSize;
  [Tooltip("a radius of 1 = 3x3,2 = 5x5, 3 = 7x7, any radius above 3 may cause performance issues with large cell sizes") ,Range(1, 10)] public int loadingRadius;
  [Range(1, 4)] public int verticalLoadingRadius;
  [Range(1f, 1000)] public int mSTimeThreshold = 100;
  float timeElapsed = 0;
  private Vector3 currentCell;
  private HashSet<Vector3> loadedCells = new();
  public List<Vector3> SurroundCells { get { return loadedCells.Where (x => x.y == 0).ToList (); } } 
  private Vector3 origin;
  public Vector3 Origin { get { return origin; } }
  private int XZSize, YSize, xlimits, zlimits, ylimits;
  public int Gridcellsizexz { get { return XZSize; } }
  public int Gridcelllimitsx { get { return xlimits; } }
  public int Gridcelllimitsz { get { return zlimits; } }
  public int Gridcellsizey { get { return YSize; } }
  public int Gridcelllimitsy { get { return ylimits; } }
  public (Vector3, Vector3) ActiveCell { get { return new(currentCell, new Vector3(XZSize, YSize, XZSize)); } }
  public Vector3 LastActiveCell { get; private set; } = new();
  private string sceneName;
  private Dictionary<Vector3,(string, string)> sceneData; // cell, (name, path)
  private readonly HashSet<Vector3> newLoadedCells = new();
  public Action<Vector3, Vector3> OnMainCellChange;

  public MoveInput MoveInput;
  private void Awake()
  {
    sceneName = SceneManager.GetActiveScene().name;
    loaderstartpos = player.transform.position;
    //for (int i = 0; i < SceneManager.sceneCount; i++)
    //{
    //  if (!SceneManager.GetSceneAt(i).name.EndsWith("_SubScene"))
    //    continue;
    //  var scene = SceneManager.GetSceneAt(i);
    //  scene.isSubScene = true;
    //  SceneManager.UnloadSceneAsync(scene);
    //}
    Physics.simulationMode = SimulationMode.Script;
    InitializeGridData();
    cellSize = XZSize;
    currentCell = GetPlayerCell();
  }
  public Transform player;
  private Vector3 loaderstartpos;
  void Start()
  {
    StartCoroutine(WaitForTerrainAndEnablePhysicsMainScene());
    // find any adjacent cell to initialize
    LastActiveCell = ClampToGridBounds(currentCell + new Vector3(1, 0, 0));
    StartLoadingCells(currentCell);
  }
  private IEnumerator WaitForTerrainAndEnablePhysicsMainScene()
  {
    float maxLoadTime = 3f;
    float timeElapsed = 0f;
    while (timeElapsed < maxLoadTime)
    {
      player.transform.position = loaderstartpos;
      timeElapsed += Time.deltaTime;
      yield return null;
    }
    MoveInput.canMove = true;
    Physics.simulationMode = SimulationMode.FixedUpdate;
  }
  void Update()
  {
    timeElapsed++;
    if (timeElapsed < mSTimeThreshold) return;
    Vector3 newCell = GetPlayerCell();
    if (newCell != currentCell)
    {
      LastActiveCell = currentCell;
      timeElapsed = 0;
      currentCell = newCell;
      OnMainCellChange?.Invoke(LastActiveCell, currentCell);
      StartLoadingCells(currentCell);
    }
  }
  private void InitializeGridData()
  {
    sceneData = new Dictionary<Vector3, (string, string)>();

    // Resources folder structure: Resources/GridData_<sceneName>
    string gridDataPath = $"GridData_MotionDemo/Grid_Parameters";
    string masterGridDataFileName = $"MotionDemo_MasterGridData";

    // Load MasterGridData file
    TextAsset masterGridDataFile = Resources.Load<TextAsset>($"{gridDataPath}/{masterGridDataFileName}");

    if (masterGridDataFile != null)
    {
      using MemoryStream ms = new(masterGridDataFile.bytes);
      using BinaryReader reader = new(ms);

      xlimits = reader.ReadInt32();
      zlimits = reader.ReadInt32();
      ylimits = reader.ReadInt32();
      _ = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
      _ = reader.ReadInt32();
      _ = reader.ReadInt32();
      _ = reader.ReadInt32();
      _ = reader.ReadInt32();
      int originX = (int)reader.ReadSingle();
      int originY = (int)reader.ReadSingle();
      int originZ = (int)reader.ReadSingle();
      XZSize = (int)reader.ReadSingle();
      YSize = (int)reader.ReadSingle();
      origin = new(originX, originY, originZ);
    }
    else
    {
      Debug.LogWarning($"Master grid data file not found in Resources at {gridDataPath}/{masterGridDataFileName}, using defaults.");
      xlimits = 4;
      zlimits = 4;
      ylimits = 2;
      origin = new(0, 0, 0);
      XZSize = 256;
      YSize = 128;
    }

    // Load all parameter files in Resources/GridData_<sceneName>/Grid_Parameters
    TextAsset[] parameterFiles = Resources.LoadAll<TextAsset>(gridDataPath);
    if (parameterFiles == null || parameterFiles.Length == 0)
    {
      Debug.LogWarning($"No parameter files found in Resources at {gridDataPath}");
    }
    foreach (TextAsset file in parameterFiles)
    {
      string filename = Path.GetFileNameWithoutExtension(file.name);
      string[] parts = filename.Split('_');
      string[] coordParts = parts[^1].Split(',');

      if (coordParts.Length == 4 &&
          int.TryParse(coordParts[0], out int x) &&
          int.TryParse(coordParts[1], out int z) &&
          int.TryParse(coordParts[2], out int y) &&
          int.TryParse(coordParts[3], out int arrayIndex))
      {
        var coordinates = $"{x},{z},{y},{arrayIndex}";
        string sceneAssetName = $"MotionDemo_Cell_{coordinates}_SubScene";
        string complexDataPath = $"GridData_MotionDemo/{filename.Replace("_Cell_", "_ComplexData_")}";
        string complexName = $"{complexDataPath}/{sceneAssetName}";
        // Resources paths don't need extensions or folder references
        if (Resources.Load(complexName) != null)
        {
          sceneData.Add(new Vector3(x, y, z), (sceneAssetName, complexName));
        }
        else
        {
          Debug.LogWarning($"Complex data file not found in Resources at {complexName}");
        }
      }
    }
  }

  private Vector3 GetPlayerCell()
  {
    Vector3 playerPos = transform.position;
    Vector3 cell = new(
        Mathf.FloorToInt((playerPos.x - origin.x) / cellSize),
        Mathf.FloorToInt((playerPos.y - origin.y) / YSize),
        Mathf.FloorToInt((playerPos.z - origin.z) / cellSize)
    );
    cell = ClampToGridBounds(cell);
    if (IsOutOfBounds(cell)) Debug.Log("Player is out of bounds");
    return cell;
  }

  private Vector3 ClampToGridBounds(Vector3 cell) =>
      new(
          Mathf.Clamp(cell.x, 0, xlimits - 1),
          Mathf.Clamp(cell.y, 0, ylimits - 1),
          Mathf.Clamp(cell.z, 0, zlimits - 1)
      );

  private bool IsOutOfBounds(Vector3 cell) =>
      cell.x >= xlimits || cell.y >= ylimits || cell.z >= zlimits;
  private Coroutine cellManagementCoroutine;
  private void StartLoadingCells(Vector3 centerCell)
  {
    if (cellManagementCoroutine != null) StopCoroutine(cellManagementCoroutine);
    cellManagementCoroutine = StartCoroutine(ManageCellsAround(centerCell));
  }

  private IEnumerator ManageCellsAround(Vector3 centerCell)
  {
    newLoadedCells.Clear();

    // Load cells spread over frames
    for (int x = -loadingRadius; x <= loadingRadius; x++)
    {
      for (int y = -verticalLoadingRadius; y <= verticalLoadingRadius; y++)
      {
        for (int z = -loadingRadius; z <= loadingRadius; z++)
        {
          Vector3 cell = centerCell + new Vector3(x, y, z);

          if (IsCellInBounds(cell))
          {
            newLoadedCells.Add(cell);
            if (!loadedCells.Contains(cell))
            {
              if (sceneData != null)
              {
                //LoadCell(cell);
              }
              yield return null;
            }
          }
        }
      }
    }
    yield return StartCoroutine(UnloadCellsNotInRange(newLoadedCells));
    loadedCells = new HashSet<Vector3>(newLoadedCells);
  }

  private IEnumerator UnloadCellsNotInRange(HashSet<Vector3> newLoadedCells)
  {
    List<Vector3> cellsToUnload = new List<Vector3>();
    foreach (var cell in loadedCells)
    {
      if (!newLoadedCells.Contains(cell))
      {
        cellsToUnload.Add(cell);
      }
    }
    foreach (var cell in cellsToUnload)
    {
      if (sceneData != null)
      {
       // UnloadCell(cell);
      }
      yield return null; // spread unloads
    }
  }
  private void UnloadCell(Vector3 cell)
  {
    sceneData.TryGetValue(cell, out (string scenename, string scenepath) sceneInfo);
    var scene = SceneManager.GetSceneByName(sceneInfo.scenename);
    if (scene.isLoaded)
      SceneManager.UnloadSceneAsync(sceneInfo.scenename);
  }
  private void LoadCell(Vector3 cell)
  {
    sceneData.TryGetValue(cell, out (string scenename, string scenepath) sceneInfo);
    var scene = SceneManager.GetSceneByName(sceneInfo.scenename);
    if (!scene.isLoaded && sceneInfo.scenename != null)
      SceneManager.LoadSceneAsync(sceneInfo.scenename, LoadSceneMode.Additive);
  }
  private bool IsCellInBounds(Vector3 cell) =>
      cell.x >= 0 && cell.x < xlimits &&
      cell.y >= 0 && cell.y < ylimits &&
      cell.z >= 0 && cell.z < zlimits;

  //private IEnumerator WaitForUnload(Scene scene)
  //{
  //  float timeout = 10f;
  //  float timeElapsed = 0f;

  //  while (scene.isLoaded && timeElapsed < timeout)
  //  {
  //    timeElapsed += Time.deltaTime;
  //    yield return null;
  //  }
  //  if (!scene.isLoaded)
  //  {
  //    SceneManager.LoadSceneAsync(scene.name, LoadSceneMode.Additive);
  //  }
  //  else
  //  {
  //    // Debug.LogWarning($"Scene {scene.name} not found for loading.");
  //  }
  //}
  //private IEnumerator WaitForLoad(Scene scene)
  //{
  //  float timeout = 10f;
  //  float timeElapsed = 0f;

  //  while (!scene.isLoaded && timeElapsed < timeout)
  //  {
  //    timeElapsed += Time.deltaTime;
  //    yield return null;
  //  }
  //  if (scene.isLoaded)
  //  {
  //    SceneManager.UnloadSceneAsync(scene);
  //  }
  //  else
  //  {
  //    //  Debug.LogWarning($"Scene {scene.name} not found for unloading.");
  //  }
  //}
}
