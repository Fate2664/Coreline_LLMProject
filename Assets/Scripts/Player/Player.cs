using System;
using UnityEngine;

namespace Coreline
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private GameInput input;

        private PlayerCharacter playerCharacter;
        private PlayerCamera playerCamera;
        private CameraSpring playerCameraSpring;
        private CameraTilt playerCameraTilt;

        private Vector2 lookInput;
        private void OnLook(Vector2 value) => lookInput = value;
        private Vector2 moveInput;
        private void OnMove(Vector2 value) => moveInput = value;
        private bool sprintInput;
        private void OnSprint(bool pressed) => sprintInput = pressed;
        private bool jumpInput;
        private bool jumpInputHeld;
        private void OnJump(bool pressed)
        {
            if (pressed && !jumpInputHeld)
                jumpInput = true;
            
            jumpInputHeld = pressed;
        } 
        private bool crouchInput;
        private bool crouchInputHeld;
        private void OnCrouch(bool pressed)
        {
            if (pressed && !crouchInputHeld)
                crouchInput = true;
            
            crouchInputHeld = pressed;
        }

        #region Startup Methods

        private void Awake()
        {
            playerCharacter = GetComponentInChildren<PlayerCharacter>();
            playerCamera = GetComponentInChildren<PlayerCamera>();
            playerCameraSpring = GetComponentInChildren<CameraSpring>();
            playerCameraTilt = GetComponentInChildren<CameraTilt>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            input.EnableActions();
            playerCharacter.Initialize();
            playerCamera.Initialize(playerCharacter.GetCameraTarget());
            playerCameraSpring.Initialize();
            playerCameraTilt.Initialize();
        }

        private void OnEnable()
        {
            input.Look += OnLook;
            input.Move += OnMove;
            input.Sprint += OnSprint;
            input.Jump += OnJump;
            input.CrouchSlide += OnCrouch;
        }

        private void OnDisable()
        {
            input.Look -= OnLook;
            input.Move -= OnMove;
            input.Sprint -= OnSprint;
            input.Jump -= OnJump;
            input.CrouchSlide -= OnCrouch;
            input.DisableActions();
        }

        #endregion

        private void Update()
        {
            //Get camera input and update its rotation
            var cameraInput = new CameraInput { Look = lookInput };
            var deltaTime = Time.deltaTime;
                
            playerCamera.UpdateRotation(cameraInput);

            //Get character input and update it.
            var characterInput = new CharacterInput
            {
                Rotation = playerCamera.transform.rotation,
                Move = moveInput,
                Sprint = sprintInput,
                Jump = jumpInput,
                Crouch = crouchInput ? CrouchInput.Pressed : CrouchInput.None,
                CrouchHeld = crouchInputHeld
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
            playerCameraSpring.UpdateSpring(deltaTime, cameraTarget.up);
            playerCameraTilt.UpdateTilt(deltaTime, state.Stance is Stance.Slide, state.Acceleration ,cameraTarget.up);
        }
    }
}