using System;
using Coreline.Robots;
using Platformer;
using UnityEngine;

namespace Coreline
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private InventoryPanel inventoryPanel;
        [SerializeField] private BuildPlacer buildPlacer;
        [SerializeField] private GameUIManager gameUIManager;

        private PlayerCharacter playerCharacter;
        private PlayerCamera playerCamera;
        private CameraSpring[] cameraSprings;
        private CameraTilt playerCameraTilt;
        private CharacterInput characterInput;
        private PlayerInventory playerInventory;
        private CountDownTimer toggleInventoryTimer;
        private CountDownTimer togglePauseTimer;
        private bool wasToggleInventoryPressed;
        private bool wasTogglePausePressed;
        private const float ToggleInventoryCooldown = 0.1f;
        
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
        //Pause
        private bool pauseInput;
        private void OnPause(bool pressed) => pauseInput = pressed;
        
        #endregion

        #region Startup Methods

        private void Awake()
        {
            playerCharacter = GetComponentInChildren<PlayerCharacter>();
            playerCamera = GetComponentInChildren<PlayerCamera>();
            cameraSprings = GetComponentsInChildren<CameraSpring>();
            playerCameraTilt = GetComponentInChildren<CameraTilt>();
            playerInventory = GetComponent<PlayerInventory>();
            toggleInventoryTimer = new CountDownTimer(ToggleInventoryCooldown);
            togglePauseTimer = new CountDownTimer(ToggleInventoryCooldown);
        }

        private void Start()
        {
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
            inventoryPanel.SlotSelected += HandleItemSlotSelected;
            input.Pause += OnPause;
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
            inventoryPanel.SlotSelected -= HandleItemSlotSelected;
            input.Pause -= OnPause;
            input.DisableActions();
        }

        #endregion

        #region Update Methods

        private void Update()
        {
            togglePauseTimer.Tick(Time.deltaTime);
            
            //Pause Toggle
            bool togglePauseThisFrame = pauseInput && !wasTogglePausePressed;
            if (togglePauseThisFrame && !togglePauseTimer.IsRunning && pauseInput)
                gameUIManager.TogglePauseMenu();
            
            wasTogglePausePressed = pauseInput;
            if (gameUIManager.IsPaused)
                return;
            
            toggleInventoryTimer.Tick(Time.deltaTime);

            bool gameplayInputBlocked = uiStateController != null && uiStateController.BlocksGameplayInput;

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
            
            //Inventory Toggle
            bool togglePressedThisFrame = toggleInventoryInput && !wasToggleInventoryPressed;
            if (togglePressedThisFrame && !InventoryToggleBlocked() && !toggleInventoryTimer.IsRunning)
                ToggleInventory();
            
            wasToggleInventoryPressed = toggleInventoryInput;
            
            
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

        #region Inventory Methods

        public void ToggleInventory()
        {
            toggleInventoryTimer.Start();
            inventoryPanel.Panel.Toggle();
        }

        private void HandleItemSlotSelected(int slotIndex, InventorySlot slot)
        {
            if (InventoryToggleBlocked() || slot?.Item is not CraftingRecipe recipe)
            {
                return;
            }

            GameObject prefab = recipe switch
            {
                RobotCraftingRecipe robotRecipe => robotRecipe.RobotPrefab,
                BuildsCraftingRecipe buildRecipe => buildRecipe.BuildPrefab,
                _ => null
            };

            if (prefab == null || buildPlacer == null)
            {
                Debug.LogWarning(
                    $"Cannot place {recipe.DisplayName} because its prefab or BuildPlacer is missing.",
                    this);
                return;
            }

            buildPlacer.BeginPrefabPlacement(prefab, placedObject =>
            {
                if (placedObject != null &&
                    placedObject.TryGetComponent(out BaseRobotController robotController))
                {
                    robotController.EnsureRobotCommandTarget();
                }

                playerInventory?.TryRemoveItem(recipe, 1);
            });
            ToggleInventory();
        }
        
        private static bool InventoryToggleBlocked() =>
            RobotChatUIController.IsAnyOpen ||
            CollectingRobotInventoryUIController.IsAnyOpen ||
            WorkbenchUIController.IsAnyOpen ||
            ChestInventoryUIController.IsAnyOpen ||
            FurnaceInventoryUIController.IsAnyOpen;

       
        #endregion
       
    }
}
