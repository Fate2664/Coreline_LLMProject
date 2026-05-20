using KinematicCharacterController;
using UnityEditor.Search;
using UnityEngine;

namespace Coreline
{
    public enum CrouchInput
    {
        None,
        Toggle
    }

    public enum Stance
    {
        Stand,
        Crouch,
        Slide
    }

    public struct CharacterState
    {
        public bool Grounded;
        public Stance Stance;
    }

    public struct CharacterInput
    {
        public Quaternion Rotation;
        public Vector2 Move;
        public bool Jump;
        public CrouchInput Crouch;
    }

    public class PlayerCharacter : MonoBehaviour, ICharacterController
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform root;

        [Space(10)] [Header("Walking")] [SerializeField]
        private float walkSpeed = 20f;

        [SerializeField] private float walkAcceleration = 25f;

        [Space(10)] [Header("Jumping")] [SerializeField]
        private float jumpSpeed = 20f;

        [SerializeField] private float airSpeed = 15f;
        [SerializeField] private float airAcceleration = 70f;
        [SerializeField] private float gravity = -90f;

        [Space(10)] [Header("Crouching")] [SerializeField]
        private float crouchSpeed = 7f;

        [SerializeField] private float crouchAcceleration = 20f;
        [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchHeightTransition = 15f;
        [Range(0, 1)] [SerializeField] private float standCameraTargetHeight = .9f;
        [Range(0, 1)] [SerializeField] private float crouchCameraTargetHeight = .7f;

        [Space(10)] [Header("Sliding")] 
        [SerializeField] private float slideStartSpeed = 25f;
        [SerializeField] private float slideSteerAcceleration = 5f;
        [SerializeField] private float slideEndSpeed = 15f;
        [SerializeField] private float slideFriction = .8f;
        [SerializeField] private float slideGravity = -60f;

        private KinematicCharacterMotor _motor;
        private CharacterState _state;
        private CharacterState _lastState;
        private CharacterState _tempState;

        private Quaternion _requestedRotation;
        private Vector3 _requestedMovement;
        private bool _requestedJump;
        private bool _requestedCrouch;
        private Collider[] _uncrouchOverlapResults;

        private void Awake()
        {
            _motor ??= GetComponent<KinematicCharacterMotor>();
        }

        public void Initialize()
        {
            _state.Stance = Stance.Stand;
            _lastState = _state;
            _motor.CharacterController = this;
            _uncrouchOverlapResults = new Collider[8];
        }

        public void UpdateInput(CharacterInput input)
        {
            _requestedRotation = input.Rotation;
            //Take the 2D input move vector and create a 3D movement vector on the XZ plane
            _requestedMovement = new Vector3(input.Move.x, 0, input.Move.y);
            //Clamp the length to 1 to prevent moving faster diagonally
            _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
            //Orient the input so it's relative to the direction the player is facing
            _requestedMovement = input.Rotation * _requestedMovement;

            _requestedJump = _requestedJump || input.Jump;


            _requestedCrouch = input.Crouch switch
            {
                CrouchInput.Toggle => !_requestedCrouch,
                CrouchInput.None => _requestedCrouch,
                _ => _requestedCrouch
            };
        }

        public void UpdateBody(float deltaTime)
        {
            var currentHeight = _motor.Capsule.height;
            var normalizedHeight = currentHeight / standHeight;

            var cameraTargetHeight = currentHeight *
                                     (_state.Stance is Stance.Stand
                                         ? standCameraTargetHeight
                                         : crouchCameraTargetHeight);
            var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

            cameraTarget.localPosition = Vector3.Lerp
            (
                a: cameraTarget.localPosition,
                b: new Vector3(0f, cameraTargetHeight, 0f),
                t: 1f - Mathf.Exp(-crouchHeightTransition * deltaTime)
            );

            root.localScale = Vector3.Lerp(
                a: root.localScale,
                b: rootTargetScale,
                t: 1f - Mathf.Exp(-crouchHeightTransition * deltaTime)
            );
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            //We don't want the character to pitch up and down, so the direction the character
            //looks should always be "flattened"
            //This is done by projecting a vector pointing in the same direction that the
            //player is looking onto a flat ground plane

            var forward = Vector3.ProjectOnPlane(_requestedRotation * Vector3.forward, _motor.CharacterUp);
            if (forward != Vector3.zero)
                currentRotation = Quaternion.LookRotation(forward, _motor.CharacterUp);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            //If on the ground
            if (_motor.GroundingStatus.IsStableOnGround)
            {
                //Snap the requested movement direction to the angle of the surface
                //the character is currently walking on.
                var groundedMovement = _motor.GetDirectionTangentToSurface
                (
                    direction: _requestedMovement,
                    surfaceNormal: _motor.GroundingStatus.GroundNormal
                ) * _requestedMovement.magnitude;
                
                //Start sliding
                {
                    var isMoving = groundedMovement.sqrMagnitude > 0f;
                    var isCrouching = _state.Stance is Stance.Crouch;
                    var wasStanding = _lastState.Stance is Stance.Stand;
                    var wasInAir = !_lastState.Grounded;
                    if (isMoving && isCrouching && (wasStanding || wasInAir))
                    {
                        _state.Stance = Stance.Slide;
                        
                        var slideSpeed = Mathf.Max(slideStartSpeed, currentVelocity.magnitude);
                        currentVelocity = _motor.GetDirectionTangentToSurface(
                            direction: currentVelocity,
                            surfaceNormal: _motor.GroundingStatus.GroundNormal
                        ) * slideSpeed;
                    }
                }
                //Move
                if (_state.Stance is Stance.Stand or Stance.Crouch)
                {
                    //Calculate the speed and acceleration
                    var speed = _state.Stance is Stance.Stand ? walkSpeed : crouchSpeed;
                    var acceleration = _state.Stance is Stance.Stand ? walkAcceleration : crouchAcceleration;

                    //and move along the ground in that direction
                    var targetVelocity = groundedMovement * speed;
                    currentVelocity = Vector3.Lerp(
                        a: currentVelocity,
                        b: targetVelocity,
                        t: 1f - Mathf.Exp(-acceleration * deltaTime)
                    );
                }
                //Continue sliding
                else
                {
                    //Friction
                    currentVelocity -= currentVelocity * (slideFriction * deltaTime);
                    
                    //Slope
                    {
                        var force = Vector3.ProjectOnPlane(
                            vector: -_motor.CharacterUp,
                            planeNormal: _motor.GroundingStatus.GroundNormal
                        ) * slideGravity;
                        
                        currentVelocity -= force * deltaTime;
                    }
                    
                    //Steer
                    {
                        var currentSpeed = currentVelocity.magnitude;
                        //Target velocity is the player's movement direction at the current speed.
                        var targetVelocity = groundedMovement * currentSpeed;
                        var steerForce = (targetVelocity - currentVelocity) * (slideSteerAcceleration * deltaTime);
                        //Add steer force but clamp velocity so the slide speed doesn't increase due to direct movement input
                        currentVelocity += steerForce;
                        currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed);
                    }
                    
                    //Stop
                    if (currentVelocity.sqrMagnitude < slideEndSpeed)
                        _state.Stance = Stance.Crouch;
                }
            }
            //else in the air
            else
            {
                //In air movement
                if (_requestedMovement.sqrMagnitude > 0f)
                {
                    //Requested movement projected onto the movement plane.
                    var planarMovement = Vector3.ProjectOnPlane
                    (
                        vector: _requestedMovement,
                        planeNormal: _motor.CharacterUp
                    ) * _requestedMovement.magnitude;

                    //Current velocity on movement plane
                    var currentPlanarVelocity = Vector3.ProjectOnPlane
                    (
                        vector: currentVelocity,
                        planeNormal: _motor.CharacterUp
                    );

                    //Calculate movement force
                    var movementForce = planarMovement * (airAcceleration * deltaTime);
                    //Add force to the current planar velocity for a target velocity
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    //Limit the target velocity to air speed
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                    //Steer towards current velocity
                    currentVelocity += targetPlanarVelocity - currentPlanarVelocity;
                }

                //Gravity
                currentVelocity += _motor.CharacterUp * (gravity * deltaTime);
            }

            if (_requestedJump)
            {
                _requestedJump = false;
                _requestedCrouch = false;
                
                //Unstick the player from the ground
                _motor.ForceUnground(time: 0);

                //Set minimum vertical speed to the jump speed.
                var currentVerticalSpeed = Vector3.Dot(currentVelocity, _motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                //Add the difference in the current and the target vertical speed to the character's velocity.
                currentVelocity += _motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            _tempState = _state;

            //Crouch
            if (_requestedCrouch && _state.Stance is Stance.Stand)
            {
                _state.Stance = Stance.Crouch;
                _motor.SetCapsuleDimensions(radius: _motor.Capsule.radius, height: crouchHeight,
                    yOffset: crouchHeight * 0.5f);
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            if (!_motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
                _state.Stance = Stance.Crouch;
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            //Uncrouch
            if (!_requestedCrouch && _state.Stance is not Stance.Stand)
            {
                _motor.SetCapsuleDimensions(radius: _motor.Capsule.radius, height: standHeight,
                    yOffset: standHeight * 0.5f);

                //See if the capsule overlaps any colliders before actually
                //allowing the character to stand up
                var pos = _motor.TransientPosition;
                var rot = _motor.TransientRotation;
                var mask = _motor.CollidableLayers;
                if (_motor.CharacterOverlap(pos, rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) >
                    0)
                {
                    //Re crouch
                    _requestedCrouch = true;
                    _motor.SetCapsuleDimensions(radius: _motor.Capsule.radius, height: crouchHeight,
                        yOffset: crouchHeight * 0.5f);
                }
                else
                {
                    _state.Stance = Stance.Stand;
                }
            }

            //Update state to reflect relevant motor properties
            _state.Grounded = _motor.GroundingStatus.IsStableOnGround;
            //Update the _lastState to store the character state snapshot taken at
            //the beginning of this character update
            _lastState = _tempState;
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