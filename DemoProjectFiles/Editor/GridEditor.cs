using UnityEditor;
using UnityEngine;
using Grid_Parameters;
using Grid_Visualization;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
#if UNITY_EDITOR
/// <summary>
/// Provides an interface for building a grid
/// a visual component and its editor helper allow the user to preview the grid in active size
/// the user can set cells to invalid in the 3D viewport which will prevent it from being saved
/// parameters are saved to a seperate folder from complex data, they are also loaded first for file checks
/// a tiny master file is created for runtime math aswell
/// </summary>
public class GridEditor : EditorWindow
{
  private static GridVisualizer gridManager;

  // editor prefs
  [Range(1f, 10f)] private static float sceneButtonSize = 5;
  [Range(250f, 5000f)] private static float sceneButtonDrawDistance = 1000;
  private static bool partialLoad = true;
  [Range(0f, 3000f)] private static float loadRadius = 100f;
  private static bool preview = true; //move to editor prefs?
  private static bool keeploaded = true;
  //feedback
  private static float totalArea = 0;
  private static int totalCells = 0;
  private static string feedbackMessage = "";
  private static bool dismissedWarning = false;
  //savables
  [Range(2, 25)] public static int worldSizeX = 2;
  [Range(2, 25)] public static int worldSizeZ = 2;
  [Range(1, 4)] public static int worldSizeY = 2;
  private static Vector3 origin = Vector3.zero;
  private static CellSize XZSize = CellSize._256;
  private static CellSize YSize = CellSize._256;
  private static ExpansionMode expansionMode = ExpansionMode.Center;
  private static ExpansionLevel expansionLevel = ExpansionLevel.Bottom;

  private static int cellXZSize;
  private static int cellYSize;
  private static Vector3 baseOffset;
  private static int yOffset;
  private static (Vector3, Vector3,bool,string)[] Cells;
  private static bool[] validCells;

  private readonly string SaveMessage = "Are you sure you want to save? This will overwrite any existing grid data for this scene.";
  private readonly string WarningMessage = "Your grid has over 1000 cells, consider using a larger cell size.";
  private readonly string SizeMessage = "Large grids may take some time to save and load.";

  private static string scenePath;
  private static string sceneName;

  #region BehaviorManagement

  [MenuItem("Tools/Grid Builder")] 
  public static void ShowWindow()
  {
    dismissedWarning = false;
    GridEditor window = GetWindow<GridEditor>("Grid Builder");
    window.Show();
    if (SceneManager.GetActiveScene().name.EndsWith("_SubScene"))
    {
      EditorSceneManager.SetActiveScene(SceneManager.GetSceneAt(0));
    }
    scenePath = SceneManager.GetActiveScene().path;
    sceneName = SceneManager.GetActiveScene().name;

    GetVisualizer();
    LoadMasterGridData();
    LoadGridData();
    LoadGridComplexFiles();
  }
  void OnDisable()
  {
    if (!keeploaded)
      UnloadAllScenes();
    if (gridManager != null)
    {
      validCells = null;
      gridManager.buttonPress -= OnPress;
      gridManager.Cells = Cells;
      gridManager.Preview = false;
      DestroyImmediate(gridManager.gameObject);
    }
  }
  private static void GetVisualizer()
  {
    var gridManagers = FindObjectsByType<GridVisualizer>(FindObjectsSortMode.None);
    if (gridManagers.Length == 0 || gridManagers == null)
    {
      gridManager = new GameObject("GridManager").AddComponent<GridVisualizer>();
    }
    else
    {
      gridManager = gridManagers[0];
    }
    if (gridManagers.Length > 1)
    {
      for (int i = 1; i < gridManagers.Length; i++)
      {
        DestroyImmediate(gridManagers[i].gameObject);
      }
    }
    Selection.activeObject = gridManager;
    gridManager.buttonPress += OnPress;
  }
  // called from 3D viewport button
  private static void OnPress(int index, bool valid)
  {
    validCells[index] = valid;
    CalculateGrid(partialLoad, loadRadius);
    gridManager.Cells = Cells;
    gridManager.Preview = preview;
  }
  void UnloadAllScenes()
  {
    int sceneCount = EditorSceneManager.sceneCount;
    List<Scene> scenesToClose = new List<Scene>();

    // Collect scenes that need to be closed
    for (int i = 0; i < sceneCount; i++)
    {
      var scene = EditorSceneManager.GetSceneAt(i);
      if (scene.name.EndsWith("_SubScene"))
      {
        scenesToClose.Add(scene);
      }
    }
    for (int i = 0; i < totalCells; i++)
    {
      complexDataLoaded[i] = false;
    }
    foreach (var scene in scenesToClose)
    {
      EditorSceneManager.CloseScene(scene, true);
    }
  }


  #endregion BehaviorManagement

  private void OnGUI()
  {
    if (EditorApplication.isPlayingOrWillChangePlaymode)
    {
      GridEditor window = GetWindow<GridEditor>("Grid Builder");
      window.Close();
      return;
    }
    if (EditorApplication.isCompiling)
      return;
    int cellThreshold = 1024;
    if (totalCells > cellThreshold && !dismissedWarning)
    {
      dismissedWarning = true;
      EditorUtility.DisplayDialog("Cell Count Warning" , WarningMessage, "OK");
    }
    var prevswitch = preview;
    preview = GUILayout.Toggle(preview, "Preview?");
    partialLoad = GUILayout.Toggle(partialLoad,"Partial Load? (Complex Files Only)");
    if (partialLoad)
    {
      loadRadius = EditorGUILayout.Slider("Load Radius", loadRadius,0, 3000);
    }
    keeploaded = GUILayout.Toggle(keeploaded, "Keep Loaded?");
    sceneButtonSize = EditorGUILayout.Slider("Scene Button Size", sceneButtonSize, 1, 10);
    sceneButtonDrawDistance = EditorGUILayout.Slider("Scene Button Draw Distance", sceneButtonDrawDistance, 250, 5000);

    if (prevswitch != preview)
    {
      Selection.activeObject = gridManager;
    }
    EditorGUILayout.BeginVertical();
    EditorGUILayout.LabelField(feedbackMessage, EditorStyles.boldLabel, GUILayout.Height(50));
    EditorGUILayout.LabelField("X Count");
    worldSizeX = EditorGUILayout.IntSlider(worldSizeX, 2,25);
    EditorGUILayout.LabelField("Z Count");
    worldSizeZ = EditorGUILayout.IntSlider(worldSizeZ, 2, 25);
    EditorGUILayout.LabelField("Y Count");
    worldSizeY = EditorGUILayout.IntSlider(worldSizeY, 1, 4);

    origin = EditorGUILayout.Vector3Field("Origin", origin);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("XZ Cell Size");
    XZSize = (CellSize)EditorGUILayout.EnumPopup(XZSize);
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Y Cell Size");
    YSize = (CellSize)EditorGUILayout.EnumPopup(YSize);
    EditorGUILayout.EndHorizontal();
    expansionMode = (ExpansionMode)EditorGUILayout.EnumPopup(expansionMode);
    expansionLevel = (ExpansionLevel)EditorGUILayout.EnumPopup(expansionLevel);
    EditorGUILayout.EndVertical();

    if (GUILayout.Button("Save All"))
    {
      bool confirm = EditorUtility.DisplayDialog("Save All", "Are you sure you want to save all grid data?", "Yes", "No");
      if (confirm)
      {
        SafeSave();
      }
    }
    if (GUILayout.Button("Overwrite All"))
    {
      ConfirmOverwrite();
    }
    CalculateGrid(partialLoad, loadRadius);
    if (gridManager == null)
    {
      GetVisualizer();
    }
    else
    {
      gridManager.sceneButtonSize = sceneButtonSize;
      gridManager.sceneButtonDrawDistance = sceneButtonDrawDistance;
      gridManager.Cells = Cells;
      gridManager.Preview = preview;
    }
    if (Selection.activeGameObject != gridManager.gameObject)
    {
      EditorGUILayout.LabelField("Select the grid manager in the scene to preview the grid in the scene");
    }
  }
  #region Load
  private static void LoadMasterGridData()
  {
    string gridDataFolder = GridFolder();
    string parameterSubfolder = Path.Combine(gridDataFolder, "Grid_Parameters");
    string masterGridDataFilePath = Path.Combine(parameterSubfolder, sceneName + "_MasterGridData.txt");

    if (File.Exists(masterGridDataFilePath))
    {
      using (FileStream fs = new(masterGridDataFilePath, FileMode.Open))
      using (BinaryReader reader = new(fs))
      {
        // Read each property in the same order
        worldSizeX = reader.ReadInt32();
        worldSizeZ = reader.ReadInt32();
        worldSizeY = reader.ReadInt32();
        origin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        XZSize = (CellSize)reader.ReadInt32();
        YSize = (CellSize)reader.ReadInt32();
        expansionMode = (ExpansionMode)reader.ReadInt32();
        expansionLevel = (ExpansionLevel)reader.ReadInt32();
      }
    }
  }
  private static void LoadGridData()
  {
    string gridDataFolder = GridFolder();
    string parameterSubfolder = Path.Combine(gridDataFolder, "Grid_Parameters");
    Cells = new (Vector3, Vector3, bool, string)[worldSizeX * worldSizeY * worldSizeZ];

    if (Directory.Exists(parameterSubfolder))
    {
      string[] savedFiles = Directory.GetFiles(parameterSubfolder, "*.txt");
      foreach (string filePath in savedFiles)
      {
        // Extract the coordinate info from the filename
        string filename = Path.GetFileNameWithoutExtension(filePath);
        string[] parts = filename.Split('_');
        string[] coordParts = parts[^1].Split(',');

        if (coordParts.Length == 4 &&
            int.TryParse(coordParts[0], out int x) &&
            int.TryParse(coordParts[1], out int z) &&
            int.TryParse(coordParts[2], out int y) &&
            int.TryParse(coordParts[3], out int arrayIndex))
        {
          if (arrayIndex >= 0 && arrayIndex < Cells.Length)
          {
            // Load and set data for the valid cell
            var data = LoadData(filePath);
            Bounds loadedBounds = data;
            Cells[arrayIndex] = (loadedBounds.center, loadedBounds.size, true, $"{x},{z},{y},{arrayIndex}");
          }
        }
      }
    }
  }

  private static void LoadGridComplexFiles()
  {
    string gridDataFolder = GridFolder();
    int index = 0;
    for (int x = 0; x < worldSizeX; x++)
    {
      for (int y = 0; y < worldSizeY; y++)
      {
        for (int z = 0; z < worldSizeZ; z++)
        {
          var cell = Cells[index];
          string gridCoordinate = $"{x},{z},{y},{index}";
          string complexDataSubfolder = Path.Combine(gridDataFolder, sceneName + "_ComplexData_" + gridCoordinate);
          if (cell.Item3)
          {
            if (!Directory.Exists(complexDataSubfolder))
            {
              Debug.Log("file not found!" + complexDataSubfolder + "for cell: " + gridCoordinate);
              AssetDatabase.CreateFolder(gridDataFolder, sceneName + "_ComplexData_" + gridCoordinate);
            }
            else
            {
              // we can access complex info here

            }
          }
          index++;
        }
      }
    }
  }

  private static Bounds LoadData(string filePath)
  {
    using FileStream fs = new(filePath, FileMode.Open);
    BinaryReader reader = new(fs);
    Vector3 center = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    Vector3 size = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    reader.Close();
    return new Bounds(center, size);
  }
  #endregion Load

  #region Files
  public string FormatSize(long bytes)
  {
    if (bytes >= 1_073_741_824)  // GB
      return $"{bytes / 1_073_741_824.0f:0.##} GB";
    if (bytes >= 1_048_576)      // MB
      return $"{bytes / 1_048_576.0f:0.##} MB";
    if (bytes >= 1_024)          // KB
      return $"{bytes / 1_024.0f:0.##} KB";
    return $"{bytes} Bytes";     // Bytes
  }
  private static string GridFolder()
  {
    string sceneDirectory = Path.GetDirectoryName(scenePath);
    string path = Path.Combine(sceneDirectory, "GridData_" + sceneName);
    if (!Directory.Exists(path))
      Directory.CreateDirectory(path);
    return Path.Combine(sceneDirectory, "GridData_" + sceneName);
  }
  #endregion Files

  #region Save
  private void ConfirmOverwrite()
  {
    bool confirmSave = EditorUtility.DisplayDialog("Confirm Overwrite", SaveMessage, "Yes", "No");
    if (confirmSave)
    {
      if (totalArea > 10000000)
      {
        bool confirmLongWait = EditorUtility.DisplayDialog("Size Warning", SizeMessage, "OK", "Cancel");
        if (confirmLongWait)
        {
          UnloadAllScenes();
          DeleteComplexDataFolders();
          OverwriteGridData();
        }
      }
      else
      {
        UnloadAllScenes();
        DeleteComplexDataFolders();
        OverwriteGridData();
      }
    }
  }
  private void DeleteComplexDataFolders()
  {
    string gridDataFolder = GridFolder();
    string complexDataPrefix = Path.Combine(gridDataFolder, sceneName + "_ComplexData_");
    // Get all matching directories
    string[] complexDataSubfolders = Directory.GetDirectories(gridDataFolder);
    foreach (string file in complexDataSubfolders)
    {
      if (file.StartsWith(complexDataPrefix))
      {
        AssetDatabase.DeleteAsset(file);
      }
    }
  }
  GameObject GetParent()
  {
    GameObject parent = GameObject.Find("Grid_Terrains");
    if (parent == null)
    {
      return new GameObject("Grid_Terrains");
    }
    return parent;
  }
  private void OverwriteGridData()
  {
    var mainScene = SceneManager.GetActiveScene();
    Vector3 storedOrigin = origin + baseOffset + new Vector3(0,yOffset,0);
    Vector2 storedCellSize = new(cellXZSize, cellYSize);
    string gridDataFolder = GridFolder();
    // find and destroy terrain objects in the scene. As we are overwriting fully in this method. this is at the users discretion
    Terrain[] terrainObjects = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
    foreach (Terrain terrainObject in terrainObjects)
      DestroyImmediate(terrainObject.gameObject);

    // create/delete folders/files
    string parameterSubfolder = Path.Combine(gridDataFolder, "Grid_Parameters");
    if (!Directory.Exists(parameterSubfolder))
      Directory.CreateDirectory(parameterSubfolder);
    
    string[] parameterFilePaths = Directory.GetFiles(parameterSubfolder);
    foreach (string filePath in parameterFilePaths)
      if (filePath.StartsWith(Path.Combine(parameterSubfolder, "Grid_Cell_")) || filePath == Path.Combine(parameterSubfolder, sceneName + "_MasterGridData"))
        File.Delete(filePath);
     
    // create a master grid data file for world variables and put in parameters file
    string masterGridDataFilePath = Path.Combine(parameterSubfolder, sceneName + "_MasterGridData.txt");
    using (FileStream fs = new(masterGridDataFilePath, FileMode.Create))
    using (BinaryWriter writer = new(fs))
    {
      writer.Write(worldSizeX);
      writer.Write(worldSizeZ);
      writer.Write(worldSizeY);
      writer.Write(origin.x);
      writer.Write(origin.y);
      writer.Write(origin.z);
      writer.Write((int)XZSize);
      writer.Write((int)YSize);
      writer.Write((int)expansionMode);
      writer.Write((int)expansionLevel);
      writer.Write(storedOrigin.x);
      writer.Write(storedOrigin.y);
      writer.Write(storedOrigin.z);
      writer.Write(storedCellSize.x);
      writer.Write(storedCellSize.y);
    }
    long totalFileSize = new FileInfo(masterGridDataFilePath).Length;
    //loop through all grid data and save them
    int arrayIndex = 0;
    for (int x = 0; x < worldSizeX; x++)
    {
      for (int y = 0; y < worldSizeY; y++)
      {    
        for (int z = 0; z < worldSizeZ; z++)
        {
          var cell = Cells[arrayIndex];
          if (cell.Item3)
          {
            string gridCoordinate = $"{x},{z},{y},{arrayIndex}";
            Bounds b = new(cell.Item1, cell.Item2);
            string savename = sceneName + "_Cell_" + gridCoordinate;
            string filePath = Path.Combine(parameterSubfolder, savename + ".txt");
            SaveData(filePath, b);
            string complexDataSubfolder = Path.Combine(gridDataFolder, sceneName + "_ComplexData_" + gridCoordinate);
            // recreate all the complex folders
            if (!Directory.Exists(complexDataSubfolder))
              AssetDatabase.CreateFolder(gridDataFolder, sceneName + "_ComplexData_" + gridCoordinate);
            // Do complex storage here
            Scene subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            subScene.name = sceneName + "_SubScene_" + gridCoordinate;
            // search for terrain data
            if (!Directory.Exists(Path.Combine(complexDataSubfolder, savename + "_Terrain")) && y == 0)
            {
              TerrainData terrainData = new()
              {
               
                heightmapResolution = cellXZSize + 1,
                size = new Vector3(cellXZSize, cellYSize, cellXZSize),
              };
              terrainData.SetDetailResolution(128, 32);
              Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
              terrain.transform.position = origin + baseOffset  +
                               new Vector3(x * cellXZSize,
                                           (expansionLevel != ExpansionLevel.Top ? 0 :  -cellYSize / 2),
                                           z * cellXZSize); 
              terrain.name = savename + "_Terrain";
              AssetDatabase.CreateAsset(terrainData, Path.Combine(complexDataSubfolder, savename + "_Terrain" + ".asset"));
              //add the terrain to the subscene
              EditorSceneManager.MoveGameObjectToScene(terrain.gameObject, subScene);
              //terrain.transform.SetParent(parent.transform); // must happen in the subscene if needed
            }
            var scenceDir = Path.Combine(complexDataSubfolder, savename + "_SubScene" + ".unity");
            if (Directory.Exists(scenceDir))
            {
              Directory.Delete(scenceDir);
            }
            subScene.isSubScene = true;
            bool saveResult = EditorSceneManager.SaveScene(subScene, scenceDir);
            if (!saveResult)
            {
              Debug.LogError("Failed to save the scene at: " + savename + "_SubScene" + ".unity");
            }
            else
            {
              EditorSceneManager.UnloadSceneAsync(subScene);
             // EditorSceneManager.OpenScene(scenceDir, OpenSceneMode.AdditiveWithoutLoading);
            }
            // we can accumulate the complex size here aswell later
            totalFileSize += new FileInfo(filePath).Length;
          }
          arrayIndex++;
        }
      }
    }
    EditorSceneManager.SetActiveScene(mainScene);
    AssetDatabase.Refresh();
    partialLoad = true;
    Debug.Log($"Total grid data file size: {FormatSize(totalFileSize)}");
  }
  private void SafeSave()
  {
    string parameterSubfolder = Path.Combine(GridFolder(), "Grid_Parameters");
    string masterGridDataFilePath = Path.Combine(parameterSubfolder, sceneName + "_MasterGridData.txt");
    if (!File.Exists(masterGridDataFilePath))
    {
      Debug.LogError("Master grid data file does not exist. Cannot proceed with safe saving.");
      return;
    }

    // Load the master grid data from the file
    using FileStream fs = new(masterGridDataFilePath, FileMode.Open);
    using BinaryReader reader = new(fs);
    float storedWorldSizeX = reader.ReadInt32();
    float storedWorldSizeZ = reader.ReadInt32();
    float storedWorldSizeY = reader.ReadInt32();
    float storedOriginX = reader.ReadSingle();
    float storedOriginY = reader.ReadSingle();
    float storedOriginZ = reader.ReadSingle();
    int storedCellXZSize = reader.ReadInt32();
    int storedCellYSize = reader.ReadInt32();
    int storedExpansionMode = reader.ReadInt32();
    int storedExpansionLevel = reader.ReadInt32();
    float storedOffsetX = reader.ReadSingle();
    float storedOffsetY = reader.ReadSingle();
    float storedOffsetZ = reader.ReadSingle();
    float storedCellSizeX = reader.ReadSingle();
    float storedCellSizeY = reader.ReadSingle();

    // Compare the current cell sizes with the stored data
    if (storedCellXZSize != cellXZSize || storedCellYSize != cellYSize)
    {
      Debug.LogError("Cell sizes have changed since the last save. Cannot proceed with safe saving."); // would require terrain resizing which is not recommended
      return;
    }
    if (storedOriginX != origin.x || storedOriginY != origin.y || storedOriginZ != origin.z)
    {
      Debug.LogError("Origin has changed since the last save. Cannot proceed with safe saving.");// we could try to reposition the grid in this case
      return;
    }
    if (storedExpansionLevel != (int)expansionLevel)
    {
      Debug.LogError("Expansion level has changed since the last save. Cannot proceed with safe saving.");// we could try to reposition the grid in this case
      return;
    }
    if (storedExpansionMode != (int)expansionMode)
    {
      Debug.LogError("Expansion mode has changed since the last save. Cannot proceed with safe saving.");// we could try to reposition the grid in this case
      return;
    }
    if (storedWorldSizeX != worldSizeX || storedWorldSizeY != worldSizeY || storedWorldSizeZ != worldSizeZ)
    {
      Debug.LogError("World size has changed since the last save. Cannot proceed with safe saving. To add cells to the grid use the Grid Editor. Stored World size: " + storedWorldSizeX + ", " + storedWorldSizeY + ", " + storedWorldSizeZ + ". Current World size: " + worldSizeX + ", " + worldSizeY + ", " + worldSizeZ + ".");
      return;
    }
    if (validCells.Length > 0) // TODO check valid cells agains existing parameter files if they all match we can skip this step, if greater display different dialog
    {
      bool confirmLoss = EditorUtility.DisplayDialog("Possible data loss", "Your Valid cells have changed, some data may be deleted", "OK", "Cancel");
      if (confirmLoss)
        ValidateCellsAndDeleteInvalidOnes(parameterSubfolder);
    }
  }
  private void ValidateCellsAndDeleteInvalidOnes(string parameterSubfolder)
  {
    // Logic to validate all the cells. If the cell complex folder exists but is invalid
    // delete the corresponding folder/data and notify the user.
    int index = 0;
    for (int x = 0; x < worldSizeX; x++)
    {
      for (int y = 0; y < worldSizeY; y++)
      {
        for (int z = 0; z < worldSizeZ; z++)
        {
          var cell = Cells[index];
          var scene = EditorSceneManager.GetSceneByName(sceneName + "_Cell_" + $"{x},{z},{y},{index}_SubScene");
          if (!cell.Item3) // If the cell is invalid
          {
            string parameterFilePath = Path.Combine(parameterSubfolder, sceneName + "_Cell_" + $"{x},{z},{y},{index}.txt");
            string complexDataSubfolder = Path.Combine(GridFolder(), sceneName + "_ComplexData_" + $"{x},{z},{y},{index}");
            if (scene.IsValid())
            {
              EditorSceneManager.CloseScene(scene, true);
            }
            if (File.Exists(parameterFilePath) && Directory.Exists(complexDataSubfolder))
            {
              File.Delete(parameterFilePath);
              File.Delete(parameterFilePath + ".meta");
              Directory.Delete(complexDataSubfolder, true);
              File.Delete(complexDataSubfolder + ".meta");
            }
          }
          else
          {
            string parameterFilePath = Path.Combine(parameterSubfolder, sceneName + "_Cell_" + $"{x},{z},{y},{index}.txt");
            if (!File.Exists(parameterFilePath))
            {
              SaveData(parameterFilePath, new(cell.Item1, cell.Item2));
            }
            string complexDataSubfolder = Path.Combine(GridFolder(), sceneName + "_ComplexData_" + $"{x},{z},{y},{index}");
            if (!Directory.Exists(complexDataSubfolder))
            {
              Directory.CreateDirectory(complexDataSubfolder);
              string savename = sceneName + "_Cell_" + $"{x},{z},{y},{index}";
              Scene subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
              subScene.name = sceneName + "_SubScene_" + $"{x},{z},{y},{index}";
              if (y == 0)
              {
                TerrainData terrainData = new()
                {
                  heightmapResolution = cellXZSize + 1,
                  size = new Vector3(cellXZSize, cellYSize, cellXZSize),
                };
                Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
                terrain.transform.position = origin + baseOffset +
                                 new Vector3(x * cellXZSize,
                                             (expansionLevel != ExpansionLevel.Top ? 0 : -cellYSize / 2),
                                             z * cellXZSize);
                terrain.name = savename + "_Terrain";
                AssetDatabase.CreateAsset(terrainData, Path.Combine(complexDataSubfolder, savename + "_Terrain" + ".asset"));
                EditorSceneManager.MoveGameObjectToScene(terrain.gameObject, subScene);
              }
              subScene.isSubScene = true;
              var scenceDir = Path.Combine(complexDataSubfolder, savename + "_SubScene" + ".unity");
              EditorSceneManager.SaveScene(subScene, scenceDir);
              string normalizedScPath = scenceDir.Replace("\\", "/");
              foreach (var sc in EditorBuildSettings.scenes)
              {
                if (sc.path == normalizedScPath)
                {
                  sc.enabled = true; // not working
                }
              }
            }
          }        
          index++;
        }
      }
    }
    AssetDatabase.Refresh();
  }
  private void SaveData(string filePath, Bounds bounds)
  {
    using FileStream fs = new(filePath, FileMode.Create);
    using BinaryWriter writer = new(fs);
    writer.Write(bounds.min.x);
    writer.Write(bounds.min.y);
    writer.Write(bounds.min.z);
    writer.Write(bounds.max.x);
    writer.Write(bounds.max.y);
    writer.Write(bounds.max.z);
  }
  #endregion Save

  #region GridMath
  static bool[] complexDataLoaded;
  private static void CalculateGrid(bool partialLoad, float radius)
  {
    string gridDataFolder = GridFolder();
    string parameterSubfolder = Path.Combine(gridDataFolder, "Grid_Parameters");
    Vector3 cellSize = new(cellXZSize, cellYSize, cellXZSize);
    totalArea = 0;
    totalCells = 0;
    int invalidCells = 0;
    cellXZSize = (int)XZSize;
    cellYSize = (int)YSize;
    baseOffset = CalculateBaseOffset();
    yOffset = CalculateVerticalOffset();
    if (Cells == null || Cells.Length != worldSizeX * worldSizeZ * worldSizeY)
      Cells = new (Vector3, Vector3,bool,string)[worldSizeX * worldSizeZ * worldSizeY];
    if (validCells == null)
    {
      validCells = new bool[worldSizeX * worldSizeZ * worldSizeY];
      for (int i = 0; i < worldSizeX * worldSizeZ * worldSizeY; i++)
      {
        string searchPattern = $"*,{i}.txt";
        if (Directory.Exists(parameterSubfolder))
        {
          string[] matchingFiles = Directory.GetFiles(parameterSubfolder, searchPattern);
          if (matchingFiles.Length > 0)
          {
            validCells[i] = true;
          }
          else
          {
            validCells[i] = false;
          }
        }
        else
        {
          validCells[i] = true;
        }
      }
    }
    else if (validCells.Length != worldSizeX * worldSizeZ * worldSizeY)
    {
      validCells = new bool[worldSizeX * worldSizeZ * worldSizeY];
      for (int i = 0; i < worldSizeX * worldSizeZ * worldSizeY; i++)
      {
        validCells[i] = true;
      }
    }
    if (complexDataLoaded == null || complexDataLoaded.Length != worldSizeX * worldSizeZ * worldSizeY)// can get stuck in a state where we should have recalculated but the array is not null or same size
    {
      complexDataLoaded = new bool[Cells.Length];
      for (int i = 0; i < Cells.Length; i++)
      {
        complexDataLoaded[i] = false;
      }
    }
    Vector3 cameraPosition = SceneView.lastActiveSceneView.camera.transform.position;
    for (int x = 0; x < worldSizeX; x++)
    {
      for (int y = 0; y < worldSizeY; y++)
      {
        for (int z = 0; z < worldSizeZ; z++)
        {         
          string gridCoordinate = $"{x},{z},{y},{totalCells}";
          Vector3 cellCenter = origin + baseOffset +
                               new Vector3(x * cellXZSize + cellXZSize / 2,
                                           y * cellYSize + cellYSize / 2 + yOffset,
                                           z * cellXZSize + cellXZSize / 2);
          Cells[totalCells] = (cellCenter, cellSize, validCells[totalCells], gridCoordinate);
          if (validCells[totalCells] && y == 0)
          {
            totalArea += cellXZSize * cellXZSize;
          }
          // allow complex data to be loaded per distance
          bool withinRadius = !partialLoad || Vector3.Distance(cameraPosition, cellCenter) <= radius;
          if (withinRadius)
          {
            if (!complexDataLoaded[totalCells])
            {
              LoadScene(gridCoordinate);
            }
            complexDataLoaded[totalCells] = true;
          }
          else
          {
            if (complexDataLoaded[totalCells])
            {
              UnLoadscene(gridCoordinate);
            }
            complexDataLoaded[totalCells] = false; 
          }
          if (!validCells[totalCells])
            invalidCells++;

          totalCells++;
        }
      }
    }
    var km = (totalArea / 1_000_000);
    feedbackMessage = $"Total 2D area: {totalArea:F0} m² or {km:F2} km² / {(km / 1.609):F2} miles². \nTotal Cells: {totalCells}, Disabled Cells: {invalidCells}. " +
      $"\nY height is {worldSizeY * cellYSize}m, X distance is {worldSizeX * cellXZSize}m, Z distance is {worldSizeZ * cellXZSize}m.";
  }
  static void LoadScene(string gridCoordinate)
  {
    string complexDataSubfolder = Path.Combine(GridFolder(), sceneName + "_ComplexData_" + gridCoordinate);
    string savename = sceneName + "_Cell_" + gridCoordinate;
    var scenceDir = Path.Combine(complexDataSubfolder, savename + "_SubScene" + ".unity");
    if (Directory.Exists(complexDataSubfolder))
      EditorSceneManager.OpenScene(scenceDir, OpenSceneMode.Additive);
  }
  static void UnLoadscene(string gridCoordinate)
  {
    // if a scene was modified and not saved, we should notify the user and allow them to save before we close the scene
    string savename = sceneName + "_Cell_" + gridCoordinate;
    var subsceneInScene = SceneManager.GetSceneByName(savename + "_SubScene");
    if (subsceneInScene.isDirty)
    {
      bool saveSubscene = EditorUtility.DisplayDialog("SubScene Save", "a scene was modified and not saved,", "save", "Discard Changes");
      if (saveSubscene)
      {
        string complexDataSubfolder = Path.Combine(GridFolder(), sceneName + "_ComplexData_" + gridCoordinate);
        var scenceDir = Path.Combine(complexDataSubfolder, savename + "_SubScene" + ".unity");
        EditorSceneManager.SaveScene(subsceneInScene, scenceDir);
      }
    }
    if (subsceneInScene == null || subsceneInScene.name == "")
    {
      Debug.Log("no scene " + savename);
      return;
    }
    EditorSceneManager.CloseScene(subsceneInScene,true);
  }
  private static Vector3 CalculateBaseOffset() => expansionMode switch
  {
    ExpansionMode.Center => new Vector3(-cellXZSize * worldSizeX / 2, 0, -cellXZSize * worldSizeZ / 2),
    ExpansionMode.NegativeXZCorner => new Vector3(-cellXZSize * worldSizeX, 0, 0),
    ExpansionMode.PositiveZXCorner => new Vector3(0, 0, -cellXZSize * worldSizeX),
    ExpansionMode.NegativeZXCorner => new Vector3(-cellXZSize * worldSizeX, 0, -cellXZSize * worldSizeX),
    _ => Vector3.zero,
  };
  private static int CalculateVerticalOffset()
  {
    int totalHeight = cellYSize * worldSizeY;
    return expansionLevel switch
    {
      ExpansionLevel.Center => -totalHeight / 2,
      ExpansionLevel.Top => -totalHeight,
      _ => 0,
    };
  }
  #endregion Gridmath
}
#endif
