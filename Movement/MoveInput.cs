using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class MoveInput : MonoBehaviour
{
  public event Action<bool> OnMove;

  [Header("Movement Settings")]
  [Tooltip("Select whether the character will use analog magnitude or digital movement")]
  [SerializeField] private bool analogMovement;
  [Tooltip("How fast the character can move while airborne")]
  [SerializeField] private float airControlFactor = 0.5f;
  [Tooltip("Move speed of the character in m/s")]
  [SerializeField] private float MoveSpeed = 4.0f;
  [Tooltip("Sprint speed of the character in m/s")]
  [SerializeField] private float SprintSpeed = 6.0f;
  [Tooltip("Acceleration and deceleration")]
  [SerializeField] private float SpeedChangeRate = 10.0f;
  [Tooltip("The height the player can jump")]
  [SerializeField] private float JumpHeight = 1.2f;
  [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
  [SerializeField] private float Gravity = -15.0f;
  [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
  [SerializeField] private float JumpTimeout = 0.1f;
  [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
  [SerializeField] private float FallTimeout = 0.15f;

  [Header("Ground Settings")]
  [Tooltip("If the character is grounded or not. Not part of the CharacterController built-in grounded check")]
  [SerializeField] private bool Grounded = true;
  [Tooltip("Useful for rough ground")]
  [SerializeField] private float GroundedOffset = -0.14f;
  [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
  [SerializeField] private float GroundedRadius = 0.5f;
  [Tooltip("What layers the character uses as ground")]
  [SerializeField] private LayerMask GroundLayers;

  [Header("Input Actions")]
  [SerializeField] private InputAction moveAction;
  [SerializeField] private InputAction sprintAction;
  [SerializeField] private InputAction jumpAction;

  [Header("References")]
  private CharacterController _controller;

  [Header("Input Values")]
  private Vector2 move;
  private bool jumpPressed;
  private bool sprint;
  private float _speed;
  private float _verticalVelocity;
  private readonly float _terminalVelocity = 53.0f;
  private readonly float speedOffset = 0.1f;
  private Vector3 _lastGroundedVelocity;
  private float _jumpTimeoutDelta;
  private float _fallTimeoutDelta;
  public bool canMove = false;
  private void Start()
  {
    _controller = GetComponent<CharacterController>();
    moveAction.Enable();
    sprintAction.Enable();
    jumpAction.Enable();
    _jumpTimeoutDelta = JumpTimeout;
    _fallTimeoutDelta = FallTimeout;

    sprintAction.performed += context => sprint = true;
    sprintAction.canceled += context => sprint = false;
    jumpAction.performed += context => jumpPressed = true;
  }

  private void OnDestroy()
  {
    moveAction.Disable();
    sprintAction.Disable();
    jumpAction.Disable();
  }

  private void Update()
  {
    this.enabled = true;
    if (!canMove) return;
    move = moveAction.ReadValue<Vector2>();
    if (moveAction.enabled == false || sprintAction.enabled == false || jumpAction.enabled == false)
    {
      moveAction.Enable(); // weird sleeper bug , have to reenable
      sprintAction.Enable();
      jumpAction.Enable();
    }
    JumpAndGravity();
    GroundedCheck();
    Move();
  }
  
  private void GroundedCheck()
  {
    // Set sphere position, with offset
    Vector3 spherePosition = new(transform.position.x, transform.position.y + Vector3.up.y - GroundedOffset, transform.position.z);
    Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
  }
  private void Move()
  {
    float targetSpeed = sprint ? SprintSpeed : MoveSpeed;

    if (move == Vector2.zero) targetSpeed = 0.0f;

    float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
    float inputMagnitude = analogMovement ? move.magnitude : 1f;
    // Normalize input direction and make relative to the player's forward direction
    Vector3 inputDirection = transform.right * move.x + transform.forward * move.y;
    if (Grounded)
    {
      // Capture horizontal velocity when grounded
      _lastGroundedVelocity = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z);
      // Accelerate or decelerate to lookAtPoint speed
      if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
      {
        _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
        _speed = Mathf.Round(_speed * 1000f) / 1000f;
      }
      else
      {
        _speed = targetSpeed;
      }
    }
    else 
    {
      Vector3 airMovement = inputDirection * (_speed * Time.deltaTime * airControlFactor);
      _controller.Move(airMovement + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }
    // Move the player
    if (_controller.enabled)
    {
      Vector3 moveDirection = Grounded ? inputDirection * (_speed * Time.deltaTime) : _lastGroundedVelocity * Time.deltaTime;
      _controller.Move(moveDirection + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }
    OnMove?.Invoke(sprint); //TODO: impliment global variables for (aiming,reloading,equipping ect .) so we can prevent spint offset when not needed
    // also use velocity for more fine motion
  }
  private void JumpAndGravity()
  {

    if (Grounded)
    {
      _fallTimeoutDelta = FallTimeout;
      if (_verticalVelocity >= _terminalVelocity)
      {
        _verticalVelocity = _terminalVelocity;
      }
      if (jumpPressed && _jumpTimeoutDelta <= 0.0f)
      {
        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
        jumpPressed = false;
      }
      if (_jumpTimeoutDelta >= 0.0f)
      {
        _jumpTimeoutDelta -= Time.deltaTime;
      }
    }
    else
    {
      _jumpTimeoutDelta = JumpTimeout;

      if (_fallTimeoutDelta >= 0.0f)
      {
        _fallTimeoutDelta -= Time.deltaTime;
      }
      jumpPressed = false;
    }
    // Apply gravity over time if under terminal velocity
    if (_verticalVelocity < _terminalVelocity && !Grounded)
    {
      _verticalVelocity += Gravity * Time.deltaTime;
    }
  }
  //private float GetSurfaceAngle()
  //{
  //  if (Physics.Raycast(transform.position, transform.forward + Vector3.down, out RaycastHit hit, 1.0f))
  //  {
  //    return Vector3.Angle(hit.normal, Vector3.up);
  //  }
  //  return 0f;
  //}
}
