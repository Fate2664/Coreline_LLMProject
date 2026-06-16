using System;
using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private InventoryPanel inventoryPanel;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private BuildPlacer buildPlacer;
        [SerializeField, Min(0f)] private float toggleInventoryCooldown = 0.1f;

        private PlayerCharacter playerCharacter;
        private PlayerCamera playerCamera;
        private CameraSpring[] cameraSprings;
        private CameraTilt playerCameraTilt;
        private CharacterInput characterInput;
        private CountDownTimer toggleInventoryTimer;
        private InventoryPanel subscribedInventoryPanel;
        private bool wasToggleInventoryPressed;
        
        public CharacterInput CharacterInput => characterInput;
        public PlayerCharacter PlayerCharacter => playerCharacter;
        public PlayerCamera PlayerCamera => playerCamera;
        
        #region Inputs

        //Look
        private Vector2 lookInput;
        private void OnLook(Vector2 value) => lookInput = value;
        //Move
        private Vector2 moveInput;
        private void OnMove(Vector2 value) => moveInput = value;
        //Sprint
        private bool sprintInput;
        private void OnSprint(bool pressed) => sprintInput = pressed;
        //Jump
        private bool jumpInput;
        private bool jumpInputHeld;
        private void OnJump(bool pressed)
        {
            if (pressed && !jumpInputHeld)
                jumpInput = true;
            
            jumpInputHeld = pressed;
        } 
        //Crouch
        private bool crouchInput;
        private bool crouchInputHeld;
        private void OnCrouch(bool pressed)
        {
            if (pressed && !crouchInputHeld)
                crouchInput = true;
            
            crouchInputHeld = pressed;
        }
        //Primary Attack
        private bool primaryAttackInput;
        private void OnPrimaryAttack(bool pressed) => primaryAttackInput = pressed;
        //Inventory
        private bool toggleInventoryInput;
        private void OnToggleInventory(bool pressed) => toggleInventoryInput = pressed;
        //Interact
        private bool interactInput;
        private void OnInteract(bool pressed) => interactInput = pressed;
        
        #endregion

        #region Startup Methods

        private void Awake()
        {
            playerCharacter = GetComponentInChildren<PlayerCharacter>();
            playerCamera = GetComponentInChildren<PlayerCamera>();
            cameraSprings = GetComponentsInChildren<CameraSpring>();
            playerCameraTilt = GetComponentInChildren<CameraTilt>();
            uiStateController ??=
                FindFirstObjectByType<UIStateController>(FindObjectsInactive.Include);
            playerInventory ??= GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
            buildPlacer ??= FindFirstObjectByType<BuildPlacer>(FindObjectsInactive.Include);
            inventoryPanel ??=
                FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            toggleInventoryTimer = new CountDownTimer(toggleInventoryCooldown);
        }

        private void Start()
        {
            if (uiStateController == null)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            input.EnableActions();
            playerCharacter.Initialize();
            playerCamera.Initialize(playerCharacter.GetCameraTarget());
            
            foreach (var spring in cameraSprings)
                spring.Initialize();
            
            playerCameraTilt.Initialize();
        }

        private void OnEnable()
        {
            input.Look += OnLook;
            input.Move += OnMove;
            input.Sprint += OnSprint;
            input.Jump += OnJump;
            input.CrouchSlide += OnCrouch;
            input.PrimaryAttack += OnPrimaryAttack;
            input.ToggleInventory += OnToggleInventory;
            input.Interact += OnInteract;
            SubscribeToInventoryPanel();
        }

        private void OnDisable()
        {
            input.Look -= OnLook;
            input.Move -= OnMove;
            input.Sprint -= OnSprint;
            input.Jump -= OnJump;
            input.CrouchSlide -= OnCrouch;
            input.PrimaryAttack -= OnPrimaryAttack;
            input.ToggleInventory -= OnToggleInventory;
            input.Interact -=  OnInteract;
            toggleInventoryInput = false;
            wasToggleInventoryPressed = false;
            UnsubscribeFromInventoryPanel();
            input.DisableActions();
        }

        #endregion

        #region Update Methods

        private void Update()
        {
            toggleInventoryTimer?.Tick(Time.deltaTime);
            HandleInventoryToggleInput();

            uiStateController ??= UIStateController.Instance;
            bool gameplayInputBlocked =
                uiStateController != null && uiStateController.BlocksGameplayInput;

            //Get camera input and update its rotation
            var cameraInput = new CameraInput
            {
                Look = gameplayInputBlocked ? Vector2.zero : lookInput
            };
            var deltaTime = Time.deltaTime;
                
            playerCamera.UpdateRotation(cameraInput);

            //Get character input and update it.
            characterInput = new CharacterInput
            {
                Rotation = playerCamera.transform.rotation,
                Move = gameplayInputBlocked ? Vector2.zero : moveInput,
                Sprint = !gameplayInputBlocked && sprintInput,
                Jump = !gameplayInputBlocked && jumpInput,
                Crouch = !gameplayInputBlocked && crouchInput
                    ? CrouchInput.Pressed
                    : CrouchInput.None,
                CrouchHeld = !gameplayInputBlocked && crouchInputHeld,
                PrimaryAttack = !gameplayInputBlocked && primaryAttackInput,
                Interact = interactInput
            };
            playerCharacter.UpdateInput(characterInput);
            crouchInput = false;
            jumpInput = false;
            playerCharacter.UpdateBody(deltaTime);
            
        }

        public void ToggleInventory()
        {
            if (IsInventoryToggleBlockedByContext())
            {
                return;
            }

            if (toggleInventoryTimer != null && toggleInventoryTimer.IsRunning)
            {
                return;
            }

            InventoryPanel panel = ResolveInventoryPanel();
            if (panel == null)
            {
                return;
            }

            toggleInventoryTimer?.Start();
            panel.Toggle();
        }

        public void OpenInventory()
        {
            ResolveInventoryPanel()?.Open();
        }

        public void CloseInventory()
        {
            ResolveInventoryPanel()?.Close();
        }

        private void HandleInventoryToggleInput()
        {
            if (toggleInventoryInput && !wasToggleInventoryPressed)
            {
                ToggleInventory();
            }

            wasToggleInventoryPressed = toggleInventoryInput;
        }

        private InventoryPanel ResolveInventoryPanel()
        {
            inventoryPanel ??=
                FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            SubscribeToInventoryPanel();
            return inventoryPanel;
        }

        private static bool IsInventoryToggleBlockedByContext()
        {
            return RobotChatUIController.IsAnyOpen ||
                   CollectingRobotInventoryUIController.IsAnyOpen ||
                   WorkbenchUIController.IsAnyOpen;
        }

        private void SubscribeToInventoryPanel()
        {
            if (inventoryPanel == null || subscribedInventoryPanel == inventoryPanel)
            {
                return;
            }

            UnsubscribeFromInventoryPanel();
            subscribedInventoryPanel = inventoryPanel;
            subscribedInventoryPanel.SlotSelected += HandleInventorySlotSelected;
        }

        private void UnsubscribeFromInventoryPanel()
        {
            if (subscribedInventoryPanel == null)
            {
                return;
            }

            subscribedInventoryPanel.SlotSelected -= HandleInventorySlotSelected;
            subscribedInventoryPanel = null;
        }

        private void HandleInventorySlotSelected(int slotIndex, InventorySlot slot)
        {
            if (IsInventoryToggleBlockedByContext())
            {
                return;
            }

            if (slot == null || slot.Item is not RobotCraftingRecipe recipe)
            {
                return;
            }

            BeginRobotPlacement(recipe);
        }

        private void BeginRobotPlacement(RobotCraftingRecipe recipe)
        {
            playerInventory ??= GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
            buildPlacer ??= FindFirstObjectByType<BuildPlacer>(FindObjectsInactive.Include);

            if (buildPlacer == null)
            {
                Debug.LogWarning("Cannot place robot because no BuildPlacer exists in the scene.", this);
                return;
            }

            if (recipe == null || recipe.RobotPrefab == null)
            {
                Debug.LogWarning("Cannot place robot because the selected robot item has no prefab assigned.", this);
                return;
            }

            buildPlacer.BeginPrefabPlacement(recipe.RobotPrefab, placedObject =>
            {
                EnsurePlacedRobotTarget(placedObject);
                playerInventory?.TryRemoveItem(recipe, 1);
            });

            CloseInventory();
        }

        private static void EnsurePlacedRobotTarget(GameObject placedObject)
        {
            if (placedObject == null)
            {
                return;
            }

            BaseRobotController robotController = placedObject.GetComponent<BaseRobotController>();
            robotController ??= placedObject.GetComponentInChildren<BaseRobotController>();
            robotController?.EnsureRobotCommandTarget();
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            var cameraTarget = playerCharacter.GetCameraTarget();
            var state = playerCharacter.GetState();
            
            playerCamera.UpdatePosition(cameraTarget);
            
            foreach (var spring in  cameraSprings)
                spring.UpdateSpring(deltaTime, cameraTarget.up);
            
            playerCameraTilt.UpdateTilt(deltaTime, state.Stance is Stance.Slide, state.Acceleration ,cameraTarget.up);
        }

        #endregion
        
       
    }
}
