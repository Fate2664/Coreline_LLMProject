using KinematicCharacterController;
using UnityEditor.Search;
using UnityEngine;

namespace Coreline
{
    public enum CrouchInput
    {
        None,
        Pressed
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
        public Vector3 Velocity;
        public Vector3 Acceleration;
    }

    public struct CharacterInput
    {
        public Quaternion Rotation;
        public Vector2 Move;
        public bool Sprint;
        public bool Jump;
        public CrouchInput Crouch;
        public bool CrouchHeld;
    }

    public class PlayerCharacter : MonoBehaviour, ICharacterController
    {
        #region Serialized Fields

        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform root;
        [Space(10)] 
        [Header("Walking")] 
        [SerializeField] private float walkSpeed = 20f;
        [SerializeField] private float walkAcceleration = 25f;
        [Space(10)]
        [Header("Sprinting")]
        [SerializeField] private float sprintSpeed = 20f;
        [SerializeField] private float sprintAcceleration = 25f;
        [Space(10)] 
        [Header("Jumping")] 
        [SerializeField] private float jumpSpeed = 20f;
        [SerializeField] private float airSpeed = 15f;
        [SerializeField] private float airAcceleration = 70f;
        [SerializeField] private float gravity = -90f;
        [SerializeField] private float coyoteTime = 0.2f;
        [Space(10)] 
        [Header("Crouching")] 
        [SerializeField] private float crouchSpeed = 7f;
        [SerializeField] private float crouchAcceleration = 20f;
        [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchHeightTransition = 15f;
        [Range(0, 1)] [SerializeField] private float standCameraTargetHeight = .9f;
        [Range(0, 1)] [SerializeField] private float crouchCameraTargetHeight = .7f;
        [Space(10)] 
        [Header("Sliding")] 
        [SerializeField] private float slideStartSpeed = 25f;
        [SerializeField] private float slideSteerAcceleration = 5f;
        [SerializeField] private float slideEndSpeed = 15f;
        [SerializeField] private float slideFriction = .8f;
        [SerializeField] private float slideGravity = -60f;

        #endregion

        #region Private Fields

        private KinematicCharacterMotor motor;
        private CharacterState state;
        private CharacterState lastState;
        private CharacterState tempState;

        private Quaternion requestedRotation;
        private Vector3 requestedMovement;
        private bool requestedSprint;
        private bool requestedJump;
        private bool requestedCrouch;
        private bool requestedCrouchHeld;
        private bool requestedCrouchInAir;
        private Collider[] uncrouchOverlapResults;

        private float timeSinceUngrounded;
        private float timeSinceJumpRequested;
        private bool ungroundedDueToJump;

        #endregion
        
        public Transform GetCameraTarget() => cameraTarget;
        public CharacterState GetState() => state;
        public CharacterState GetLastState() => lastState;

        private void Awake()
        {
            motor ??= GetComponent<KinematicCharacterMotor>();
        }

        public void Initialize()
        {
            state.Stance = Stance.Stand;
            lastState = state;
            motor.CharacterController = this;
            uncrouchOverlapResults = new Collider[8];
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
            
            //Sprinting
            requestedSprint = input.Sprint;
            
            //Jumping
            var wasRequestingJump = requestedJump;
            requestedJump = requestedJump || input.Jump;
            if (requestedJump && !wasRequestingJump)
                timeSinceJumpRequested = 0f;
            
            //Crouching
            var wasRequestingCrouch = requestedCrouch;
            requestedCrouch = input.Crouch switch
            {
                CrouchInput.Pressed => !requestedCrouch,
                CrouchInput.None => requestedCrouch,
                _ => requestedCrouch
            };
            requestedCrouchHeld = input.CrouchHeld;
            if (requestedCrouch && !wasRequestingCrouch)
                requestedCrouchInAir = !state.Grounded;
            else if (!requestedCrouch && wasRequestingCrouch)
                requestedCrouchInAir = false;
        }

        public void UpdateBody(float deltaTime)
        {
            var currentHeight = motor.Capsule.height;
            var normalizedHeight = currentHeight / standHeight;

            var cameraTargetHeight = currentHeight *
                                     (state.Stance is Stance.Stand
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

            var forward = Vector3.ProjectOnPlane(requestedRotation * Vector3.forward, motor.CharacterUp);
            if (forward != Vector3.zero)
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            state.Acceleration = Vector3.zero;
            
            //If on the ground
            if (motor.GroundingStatus.IsStableOnGround)
            {
                timeSinceUngrounded = 0f;
                ungroundedDueToJump = false;
                //Snap the requested movement direction to the angle of the surface
                //the character is currently walking on.
                var groundedMovement = motor.GetDirectionTangentToSurface
                (
                    direction: requestedMovement,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ) * requestedMovement.magnitude;
                
                //Start sliding
                {
                    var isMoving = groundedMovement.sqrMagnitude > 0f;
                    var isCrouching = state.Stance is Stance.Crouch;
                    var wasStanding = lastState.Stance is Stance.Stand;
                    var wasInAir = !lastState.Grounded;
                    //Player needs to be moving + trying to sprint + in crouch mode + holding down crouch + was standing last state
                    //or was in the air last state to start sliding
                    if (isMoving && requestedSprint && isCrouching && requestedCrouchHeld && (wasStanding || wasInAir))
                    {
                        state.Stance = Stance.Slide;
                        
                        //When landing on stable ground the character motor projects the velocity onto a flat plane.
                        //However, in this case, we want the player to slide with the slope.
                        //Reproject the last frame's (falling) velocity onto the ground normal to slide.
                        if (wasInAir)
                        {
                            currentVelocity = Vector3.ProjectOnPlane
                            (
                                vector: lastState.Velocity,
                                planeNormal: motor.GroundingStatus.GroundNormal
                            );
                        }

                        var effectiveSlideStartSpeed = slideStartSpeed;
                        if (!lastState.Grounded && !requestedCrouchInAir)
                        {
                            effectiveSlideStartSpeed = 0f;
                            requestedCrouchInAir = false;
                        }
                        
                        var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                        currentVelocity = motor.GetDirectionTangentToSurface(
                            direction: currentVelocity,
                            surfaceNormal: motor.GroundingStatus.GroundNormal
                        ) * slideSpeed;
                    }
                }
                //Move
                if (state.Stance is Stance.Stand or Stance.Crouch)
                {
                    //Calculate the speed and acceleration
                    var speed = state.Stance switch
                    {
                        Stance.Stand when requestedSprint => sprintSpeed,
                        Stance.Stand => walkSpeed,
                        Stance.Crouch => crouchSpeed,
                        _ => walkSpeed
                    };
                    var acceleration = state.Stance switch
                    {
                        Stance.Stand when requestedSprint => sprintAcceleration,
                        Stance.Stand => walkAcceleration,
                        Stance.Crouch => crouchAcceleration,
                        _ => walkAcceleration
                    };

                    //and move along the ground in that direction
                    var targetVelocity = groundedMovement * speed;
                    var moveVelocity = Vector3.Lerp(
                        a: currentVelocity,
                        b: targetVelocity,
                        t: 1f - Mathf.Exp(-acceleration * deltaTime)
                    );
                    state.Acceleration = (moveVelocity - currentVelocity) / deltaTime;
                    currentVelocity = moveVelocity;
                }
                //Continue sliding
                else
                {
                    //Hold crouch check
                    if (!requestedCrouchHeld)
                    {
                        requestedCrouch = false;
                        state.Stance = Stance.Crouch;
                    }
                    else
                    {
                        //Friction
                        currentVelocity -= currentVelocity * (slideFriction * deltaTime);
                        
                        //Slope
                        {
                            var force = Vector3.ProjectOnPlane(
                                vector: -motor.CharacterUp,
                                planeNormal: motor.GroundingStatus.GroundNormal
                            ) * slideGravity;
                            
                            currentVelocity -= force * deltaTime;
                        }
                        
                        //Steer
                        {
                            var currentSpeed = currentVelocity.magnitude;
                            //Target velocity is the player's movement direction at the current speed.
                            var targetVelocity = groundedMovement * currentSpeed;
                            var steerVelocity = currentVelocity;
                            var steerForce = (targetVelocity - steerVelocity) * (slideSteerAcceleration * deltaTime);
                            //Add steer force but clamp velocity so the slide speed doesn't increase due to direct movement input
                            steerVelocity += steerForce;
                            steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);
                            
                            state.Acceleration = (steerVelocity - currentVelocity) / deltaTime;
                            currentVelocity = steerVelocity;
                        }
                        
                        //Stop
                        if (currentVelocity.sqrMagnitude < slideEndSpeed)
                            state.Stance = Stance.Crouch;
                    }
                }
            }
            //else in the air
            else
            {
                timeSinceUngrounded += deltaTime;
                //In air movement
                if (requestedMovement.sqrMagnitude > 0f)
                {
                    //Requested movement projected onto the movement plane.
                    var planarMovement = Vector3.ProjectOnPlane
                    (
                        vector: requestedMovement,
                        planeNormal: motor.CharacterUp
                    ) * requestedMovement.magnitude;

                    //Current velocity on movement plane
                    var currentPlanarVelocity = Vector3.ProjectOnPlane
                    (
                        vector: currentVelocity,
                        planeNormal: motor.CharacterUp
                    );

                    //Calculate movement force
                    var movementForce = planarMovement * (airAcceleration * deltaTime);
                    
                    //If moving slower than the max air speed, treat movementForce as a simple steering force
                    if (currentPlanarVelocity.sqrMagnitude < airSpeed)
                    {
                        //Add force to the current planar velocity for a target velocity
                        var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                        //Limit the target velocity to air speed
                        targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                        //Steer towards target velocity
                        movementForce = targetPlanarVelocity - currentPlanarVelocity;
                    }
                    //Otherwise, nerf the movement force when it is in the direction fo the current planar velocity
                    //to prevent accelerating further beyond the max air speed.
                    else if (Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                    {
                        //Project movement force onto the plane whose normal is the current planar velocity
                        var constrainedMovementForce = Vector3.ProjectOnPlane
                        (
                            vector: movementForce,
                            planeNormal: currentPlanarVelocity.normalized
                        );
                        
                        movementForce = constrainedMovementForce;
                    }
                    
                    //Prevent air-climbing on steep slopes
                    if (motor.GroundingStatus.IsStableOnGround)
                    {
                        //If moving in the same direction as the resultant velocity
                        if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                        {
                            //Calculate obstruction normal
                            var obstructionNormal = Vector3.Cross
                            (
                                motor.CharacterUp,
                                motor.GroundingStatus.GroundNormal
                            ).normalized;
                            
                            //Project movement force onto obstruction plane
                            movementForce = Vector3.ProjectOnPlane(movementForce, obstructionNormal);
                        }
                    }
                    
                    currentVelocity += movementForce;
                }

                //Gravity
                currentVelocity += motor.CharacterUp * (gravity * deltaTime);
            }

            if (requestedJump)
            {
                var grounded = motor.GroundingStatus.IsStableOnGround;
                var canCoyoteJump = timeSinceUngrounded < coyoteTime && !ungroundedDueToJump;
                if (grounded || canCoyoteJump)
                {
                    requestedJump = false;
                    requestedCrouch = false;
                    requestedCrouchInAir = false;
                    
                    //Unstick the player from the ground
                    motor.ForceUnground(time: 0);
                    ungroundedDueToJump = true;
                    
                    //Set minimum vertical speed to the jump speed.
                    var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                    var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                    //Add the difference in the current and the target vertical speed to the character's velocity.
                    currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
                }
                else
                {
                    timeSinceJumpRequested += deltaTime;
                    //Defer the jump request until coyote time has passed
                    var canJumpLater = timeSinceJumpRequested < coyoteTime;
                    requestedJump = canJumpLater;
                }
            }
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            tempState = state;

            //Crouch
            if (requestedCrouch && state.Stance is Stance.Stand)
            {
                state.Stance = Stance.Crouch;
                motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: crouchHeight,
                    yOffset: crouchHeight * 0.5f);
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            if (!motor.GroundingStatus.IsStableOnGround && state.Stance is Stance.Slide)
                state.Stance = Stance.Crouch;
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            //Uncrouch
            if (!requestedCrouch && state.Stance is not Stance.Stand)
            {
                motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: standHeight,
                    yOffset: standHeight * 0.5f);

                //See if the capsule overlaps any colliders before actually
                //allowing the character to stand up
                var pos = motor.TransientPosition;
                var rot = motor.TransientRotation;
                var mask = motor.CollidableLayers;
                if (motor.CharacterOverlap(pos, rot, uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) >
                    0)
                {
                    //Re crouch
                    requestedCrouch = true;
                    motor.SetCapsuleDimensions(radius: motor.Capsule.radius, height: crouchHeight,
                        yOffset: crouchHeight * 0.5f);
                }
                else
                {
                    state.Stance = Stance.Stand;
                }
            }

            //Update state to reflect relevant motor properties
            state.Grounded = motor.GroundingStatus.IsStableOnGround;
            state.Velocity = motor.Velocity;
            //Update the _lastState to store the character state snapshot taken at
            //the beginning of this character update
            lastState = tempState;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public bool IsColliderValidForCollisions(Collider coll) => true;
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
        
    }
}