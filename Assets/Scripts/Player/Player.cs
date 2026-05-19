using System;
using UnityEngine;

namespace Coreline
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        
        private PlayerCharacter playerCharacter;
        private PlayerCamera playerCamera;

        private Vector2 lookInput; 
        private void OnLook(Vector2 value) => lookInput = value;
        private Vector2 moveInput;
        private void OnMove(Vector2 value) => moveInput = value;

        private void Awake()
        {
            playerCharacter = GetComponentInChildren<PlayerCharacter>();
            playerCamera = GetComponentInChildren<PlayerCamera>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            input.EnableActions();
            playerCharacter.Initialize();
            playerCamera.Initialize(playerCharacter.GetCameraTarget());
        }

        private void OnEnable()
        {
            input.Look += OnLook;
            input.Move += OnMove;
        }

        private void OnDisable()
        {
            input.Look -= OnLook;
            input.Move -= OnMove;
            input.DisableActions(); 
        }

        private void Update()
        {
            //Get camera input and update its rotation
            var cameraInput = new CameraInput { Look = lookInput };
            playerCamera.UpdateRotation(cameraInput);
            
            //Get character input and update it.
            var characterInput = new CharacterInput
            {
                Rotation = playerCamera.transform.rotation,
                Move = moveInput
            };
            playerCharacter.UpdateInput(characterInput);
        }   

        private void LateUpdate()
        {
            playerCamera.UpdatePosition(playerCharacter.GetCameraTarget());
        }
        
    }
}
