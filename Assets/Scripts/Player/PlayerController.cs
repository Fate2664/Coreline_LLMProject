using System;
using Coreline;
using Nova;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Serialized Variables

    [Header("Movement Settings")] 
    [Space(10)] 
    [Range(1f, 10f)] 
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    
    [Space(10)]
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 1.0f;
    [SerializeField] private float jumpCooldown = 0.5f;
    [SerializeField] private float inAirDrag = 2.0f;
    
    [Space(10)]
    [Header("Camera Settings")] 
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform orientation;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 75.0f;

    [Space(10)] 
    [Header("Connections")] 
    [SerializeField] private GameInput gameInput;
    [SerializeField] private UIBlock2D InventoryRoot;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private BuildPlacer buildPlacer;
    
    [HideInInspector]
    public bool IsInventoryOpen => InventoryRoot.gameObject.activeSelf;
    #endregion

    #region Private Variables

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;

    private float lookSensMultiplier = 0.01f;
    private float cameraPitch;
    private float cameraYaw;
    private CountDownTimer jumpCooldownTimer;

    private void OnMove(Vector2 dir) => moveInput = dir;
    private void OnLook(Vector2 dir) => lookInput = dir;
    private void OnJump(bool pressed) => jumpPressed = pressed;
    

    #endregion
    
    #region Startup Methods

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerCamera.localEulerAngles.x > 180f)
        {
            cameraPitch = playerCamera.localEulerAngles.x - 360f;
        }else cameraPitch = playerCamera.localEulerAngles.x;
        
        jumpCooldownTimer = new CountDownTimer(jumpCooldown);
    }

    private void Start()
    {
        gameInput.Move += OnMove;
        gameInput.Look += OnLook;
        gameInput.Jump += OnJump;
        gameInput.EnableActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void OnDestroy()
    {
        gameInput.Move -= OnMove;
        gameInput.Look -= OnLook;
        gameInput.Jump -= OnJump;
    }

    #endregion

    #region Update Methods

    private void Update()
    {
        IsGrounded();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
        jumpCooldownTimer.Tick(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        HandleLook();
    }

    #endregion

    #region Movement & Camera & Toggle Methods

    private void HandleMovement()
    {
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 moveDirection = (orientation.right * moveInput.x + orientation.forward * moveInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        
        if (!IsGrounded() && moveInput.sqrMagnitude <= 0.01f)
            return;

        rb.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
    }

    private void HandleLook()
    {
        cameraYaw += lookInput.x * lookSensitivity * lookSensMultiplier;
        cameraPitch -= lookInput.y * lookSensitivity * lookSensMultiplier;

        playerCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        orientation.transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
    }

    private void HandleJump()
    {
        if (!jumpPressed || !IsGrounded() || jumpCooldownTimer.IsRunning)
            return;
        
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpCooldownTimer.Start();
        
        if (IsGrounded() || jumpCooldownTimer.IsFinished)
            jumpCooldownTimer.Reset();
    }

    public void ToggleInventory()
    {
        if (InventoryRoot.gameObject.activeSelf)
        {
            InventoryRoot.gameObject.SetActive(false);
        }
        else
        {
            InventoryRoot.gameObject.SetActive(true);
            uiManager?.RefreshInventory();

            if (buildPlacer.enabled)
            {
                buildPlacer.enabled = false;
                uiManager?.UnEquipItem();
            }
        }
    }

    #endregion

    #region State Checks

    private bool IsGrounded() => Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

    #endregion
}
