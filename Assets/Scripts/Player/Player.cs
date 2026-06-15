using System;
using UnityEngine;

namespace Coreline
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private UIStateController uiStateController;

        private PlayerCharacter playerCharacter;
        private PlayerCamera playerCamera;
        private CameraSpring[] cameraSprings;
        private CameraTilt playerCameraTilt;
        private CharacterInput characterInput;
        private CountDownTimer toggleInventoryTimer;
        
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
        }

        private void OnDisable()
        {
            input.Look -= OnLook;
            input.Move -= OnMove;
            input.Sprint -= OnSprint;
            input.Jump -= OnJump;
            input.CrouchSlide -= OnCrouch;
            input.PrimaryAttack -= OnPrimaryAttack;
            input.DisableActions();
        }

        #endregion

        #region Update Methods

        private void Update()
        {
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
                PrimaryAttack = !gameplayInputBlocked && primaryAttackInput
            };
            playerCharacter.UpdateInput(characterInput);
            crouchInput = false;
            jumpInput = false;
            playerCharacter.UpdateBody(deltaTime);
            
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
