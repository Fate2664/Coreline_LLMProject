using System;
using Coreline;
using Coreline.Robots;
using Nova;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Serialized Variables

    [Header("Movement Settings")] [Space(10)] [Range(1f, 10f)] [SerializeField]
    private float moveSpeed = 5f;

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;

    [Space(10)] [Header("Jump Settings")] [SerializeField]
    private float jumpForce = 1.0f;

    [SerializeField] private float jumpCooldown = 0.5f;
    [SerializeField] private float inAirDrag = 2.0f;

    [Space(10)] [Header("Camera Settings")] [SerializeField]
    private Transform playerCamera;

    [SerializeField] private Transform orientation;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 75.0f;

    [Space(10)] [Header("Connections")] [SerializeField]
    private GameInput gameInput;

    [SerializeField] private UIBlock2D InventoryRoot;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private BuildPlacer buildPlacer;

    [HideInInspector] public bool IsInventoryOpen => InventoryRoot != null && InventoryRoot.gameObject.activeSelf;
    public UIBlock2D InventoryRootBlock => InventoryRoot;
    public UIManager InventoryUIManager => uiManager;
    private bool IsUiInputBlocked => IsInventoryOpen ||
                                     RobotChatUIController.IsAnyOpen ||
                                     CollectingRobotInventoryUIController.IsAnyOpen ||
                                     WorkbenchUIController.IsAnyOpen;

    #endregion

    #region Private Variables

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private MiningController miningController;
    private bool jumpPressed;
    private bool toggleInventoryPressed;
    private bool wasToggleInventoryPressed;
    private bool primaryAttackPressed;

    private float toggleInventoryCooldown = 0.1f;
    private float lookSensMultiplier = 0.01f;
    private float cameraPitch;
    private float cameraYaw;
    private CountDownTimer jumpCooldownTimer;
    private CountDownTimer toggleInventoryTimer;

    private void OnMove(Vector2 dir) => moveInput = dir;
    private void OnLook(Vector2 dir) => lookInput = dir;
    private void OnJump(bool pressed) => jumpPressed = pressed;
    private void OnPrimaryAttack(bool pressed)
    {
        if (IsUiInputBlocked) return;
        miningController.OnPrimaryAttack(pressed);
    }
    private void OnToggleInventory(bool pressed) => toggleInventoryPressed = pressed;

    #endregion

    #region Startup Methods

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        miningController = GetComponent<MiningController>();

        if (playerCamera.localEulerAngles.x > 180f)
        {
            cameraPitch = playerCamera.localEulerAngles.x - 360f;
        }
        else cameraPitch = playerCamera.localEulerAngles.x;

        jumpCooldownTimer = new CountDownTimer(jumpCooldown);
        toggleInventoryTimer = new CountDownTimer(toggleInventoryCooldown);
        ToggleInventory();
    }

    private void Start()
    {
        gameInput.Move += OnMove;
        gameInput.Look += OnLook;
        gameInput.Jump += OnJump;
        gameInput.PrimaryAttack += OnPrimaryAttack;
        gameInput.ToggleInventory += OnToggleInventory;
        gameInput.EnableActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        gameInput.Move -= OnMove;
        gameInput.Look -= OnLook;
        gameInput.Jump -= OnJump;
        gameInput.PrimaryAttack -= OnPrimaryAttack;
        gameInput.ToggleInventory -= OnToggleInventory;
    }

    #endregion

    #region Update Methods

    private void Update()
    {
        IsGrounded();
    }

    private void FixedUpdate()
    {
        if (toggleInventoryPressed &&
            !wasToggleInventoryPressed &&
            !CollectingRobotInventoryUIController.IsAnyOpen &&
            !WorkbenchUIController.IsAnyOpen)
        {
            ToggleInventory();
        }

        wasToggleInventoryPressed = toggleInventoryPressed;
        
        if (IsUiInputBlocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleMovement();
        HandleJump();
        jumpCooldownTimer.Tick(Time.fixedDeltaTime);
        toggleInventoryTimer.Tick(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        HandleLook();
    }

    #endregion

    #region Movement & Camera & Toggle Methods

    private void HandleMovement()
    {
        if (IsUiInputBlocked) return;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 moveDirection = (orientation.right * moveInput.x + orientation.forward * moveInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;

        if (!IsGrounded() && moveInput.sqrMagnitude <= 0.01f)
            return;

        rb.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
    }

    private void HandleLook()
    {
        if (IsUiInputBlocked) return;

        cameraYaw += lookInput.x * lookSensitivity * lookSensMultiplier;
        cameraPitch -= lookInput.y * lookSensitivity * lookSensMultiplier;

        playerCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        orientation.transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
    }

    private void HandleJump()
    {
        if (!jumpPressed || !IsGrounded() || jumpCooldownTimer.IsRunning || IsUiInputBlocked)
            return;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpCooldownTimer.Start();

        if (IsGrounded() || jumpCooldownTimer.IsFinished)
            jumpCooldownTimer.Reset();
    }

    public void ToggleInventory()
    {
        if (toggleInventoryTimer.IsRunning)
            return;
        toggleInventoryTimer.Start();

        SetInventoryVisible(!IsInventoryOpen);
    }

    public void OpenInventory()
    {
        SetInventoryVisible(true);
    }

    public void CloseInventory()
    {
        SetInventoryVisible(false);
    }

    private void SetInventoryVisible(bool visible)
    {
        if (InventoryRoot == null)
        {
            return;
        }

        if (InventoryRoot.gameObject.activeSelf == visible)
        {
            if (visible)
            {
                uiManager?.RefreshInventory();
            }

            return;
        }

        InventoryRoot.gameObject.SetActive(visible);

        if (!visible)
        {
            return;
        }

        uiManager?.RefreshInventory();

        if (buildPlacer != null && buildPlacer.enabled)
        {
            buildPlacer.enabled = false;
            uiManager?.UnEquipItem();
        }
    }

    #endregion

    #region State Checks

    private bool IsGrounded() => Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer,
        QueryTriggerInteraction.Ignore);

    #endregion
}
