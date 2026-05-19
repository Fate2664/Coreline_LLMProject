using System;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

namespace Coreline
{
    public struct CharacterInput
    {
        public Quaternion Rotation;
        public Vector2 Move;
    }

    public class PlayerCharacter : MonoBehaviour, ICharacterController
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float walkSpeed = 20f;


        private KinematicCharacterMotor motor;

        private Quaternion requestedRotation;
        private Vector3 requestedMovement;

        private void Awake()
        {
            motor ??= GetComponent<KinematicCharacterMotor>();
        }

        public void Initialize()
        {
            motor.CharacterController = this;
        }

        public void UpdateInput(CharacterInput input)
        {
            requestedRotation = input.Rotation;
            //Take the 2D input move vector and create a 3D movement vector on the XZ plane
            requestedMovement = new Vector3(input.Move.x, 0, input.Move.y);
            //Clamp the length to 1 to prevent moving faster diagonally
            requestedMovement = Vector3.ClampMagnitude(requestedMovement, 1f);
            //Orient the input so it's relative to the direction the player is facing
            requestedMovement = input.Rotation * requestedMovement;
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            //We don't want the character to pitch up and down, so the direction the character
            //looks should always be "flattened"
            //This is done by projecting a vector pointing in the same direction that the
            //player is looking onto a flat ground plane

            var forward = Vector3.ProjectOnPlane(requestedRotation * Vector3.forward, motor.CharacterUp);
            if (forward != Vector3.zero)
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * requestedMovement.magnitude;
            currentVelocity = groundedMovement * walkSpeed;
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        public void PostGroundingUpdate(float deltaTime)
        {
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public bool IsColliderValidForCollisions(Collider coll) => true;

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        public Transform GetCameraTarget() => cameraTarget;
    }
}