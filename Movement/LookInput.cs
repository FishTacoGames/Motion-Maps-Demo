using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LookInput : MonoBehaviour
{
  [Header("Public Settings")]
  [Tooltip("How fast the camera rotates"), Range(0.01f, 3f)]
  public float Sensitivity = 1f; // may be set by settings
  [Header("Motion Settings")]
  [Tooltip("The player transform for horizontal rotation")]
  [SerializeField] private Transform playerTransform;
  [Tooltip("The camera (or head) transform for vertical rotation")]
  [SerializeField] private Transform cameraTransform;
  [Tooltip("Input action for mouse or gamepad input")]
  [SerializeField] private InputAction lookInput;
  [Tooltip("Input action for aiming")]
  [SerializeField] private InputAction aimInput;
  [Tooltip("How far in degrees can you move the camera down"), Range(-10f, -89f)]
  [SerializeField] private float BottomClamp = -89f;
  [Tooltip("How far in degrees can you move the camera up"), Range(10f, 89f)]
  [SerializeField] private float TopClamp = 89f;


  private float _yawRotation = 0f;
  private float _pitchRotation = 0f;
  private void Start()
  {
    dontLook = true;
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
    StartCoroutine(WaitForLoadLook());
  }
  private void OnEnable()
  {
    lookInput.Enable();
    aimInput.Enable();
  }

  private void OnDestroy()
  {
    lookInput.Disable();
    aimInput.Disable();
  }
  private IEnumerator WaitForLoadLook()
  {
    yield return new WaitForSeconds(1f);
    dontLook = false;
  }
  public float SmoothingSpeed = 5f;
  private bool dontLook;
  void Update()
  {
    if (Time.deltaTime > 0.05f) return;
    if (dontLook || Cursor.lockState != CursorLockMode.Locked) return;

    Vector2 inputLook = lookInput.ReadValue<Vector2>();
    float deltaTime = Time.deltaTime;
    _yawRotation += inputLook.x * Sensitivity * deltaTime;
    _pitchRotation += inputLook.y * Sensitivity * deltaTime;
    _pitchRotation = Mathf.Clamp(_pitchRotation, BottomClamp, TopClamp);
    float smoothedYaw = Mathf.LerpAngle(playerTransform.eulerAngles.y, _yawRotation, deltaTime * SmoothingSpeed);
    float smoothedPitch = Mathf.LerpAngle(cameraTransform.localEulerAngles.x, _pitchRotation, deltaTime * SmoothingSpeed);
    playerTransform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
    cameraTransform.localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f);
  }
}
