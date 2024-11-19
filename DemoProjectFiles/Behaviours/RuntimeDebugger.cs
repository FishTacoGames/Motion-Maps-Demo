using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RuntimeDebugger : MonoBehaviour
{
  public Slider linkwidth;
  public Slider linklength;
  public Slider Sensitivity;
  public ShaderColliderLink mainloaderLink;
  public GameObject[] planes;
  public Text text;
  public Button toggle;
  public Button quitGame;
  private bool visualize = false;
  private bool enableCursor = false;
  [SerializeField] private InputAction focusInput;
  [SerializeField] private InputAction clickInput;

  public LookInput lookInput;
  private void Start()
  {
    focusInput.Enable();
    focusInput.performed += ToggleFocus;
    foreach (var plane in planes)
    {
      plane.SetActive(false);
    }
    Shader.SetGlobalFloat("_Debug", 0);
    clickInput.Enable();
    clickInput.performed += _ => enableCursor = false;
    toggle.onClick.AddListener(OnToggle);
    linkwidth.onValueChanged.AddListener(LinkSizeX);
    linklength.onValueChanged.AddListener(LinkSizeY);
    Sensitivity.onValueChanged.AddListener(SetCamSens);
    quitGame.onClick.AddListener(QuitGame);
  }
  void Update()
  {
    if (enableCursor)
    {
      Cursor.visible = true;
      Cursor.lockState = CursorLockMode.None;
    }
    else
    {
      Cursor.visible = false;
      Cursor.lockState = CursorLockMode.Locked;
    }
  }
  void QuitGame()
  {
#if UNITY_EDITOR
    if (Application.isEditor)
    {
      UnityEditor.EditorApplication.isPlaying = false;
      return;
    }
#endif
    Application.Quit();
  }
  void SetCamSens(float value)
  {
    lookInput.Sensitivity = value;
  }
  void OnToggle()
  {
    visualize = !visualize;
    if (visualize)
    {
      foreach (var plane in planes)
      {
        plane.SetActive(true);
      }
      text.text = "ON";
      Shader.SetGlobalFloat("_Debug", 1);
    }
    else
    {
      foreach (var plane in planes)
      {
        plane.SetActive(false);
      }
      text.text = "OFF";
      Shader.SetGlobalFloat("_Debug", 0);
    }
  }
  void LinkSizeX(float valuex)
  {
    mainloaderLink.motionValues.width = valuex;  
  }
  void LinkSizeY(float valuey)
  {
    mainloaderLink.motionValues.length = valuey;
  }
 
  void OnDestroy()
  {
    //Shader.SetGlobalFloat("_Debug", 0);
    toggle.onClick.RemoveListener(OnToggle);
    linklength.onValueChanged.RemoveListener(LinkSizeX);
    linklength.onValueChanged.RemoveListener(LinkSizeY);
    Sensitivity.onValueChanged.RemoveListener(SetCamSens);
    focusInput.Disable();
    focusInput.performed -= ToggleFocus;
    quitGame.onClick.RemoveListener(QuitGame);
  }
  void ToggleFocus(InputAction.CallbackContext context)
  {
    enableCursor = !enableCursor;
  }
}
