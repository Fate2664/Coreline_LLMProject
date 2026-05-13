using UnityEngine;

namespace Coreline
{
    //This is an enum for all the player locomotion states. A locomotion state is seperate from other movement states such as Jumping, Climbing and Falling
    public enum PlayerLocomotionState
    {
        Idling = 0,
        Walking = 1,
        Sprinting = 2,
    }
    
    //This is the base player state class. All player states will inherit from this.
    public abstract class PlayerBaseState : IState
    {
        protected readonly PlayerController player;
        protected readonly Animator animator;
        
        //Animation hashes
        protected static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        protected static readonly int JumpHash = Animator.StringToHash("Jump");
        protected static readonly int FallingHash = Animator.StringToHash("Fall");
        protected static readonly int HeavyLandHash = Animator.StringToHash("HeavyLand");
        protected static readonly int ClimbingHash = Animator.StringToHash("Climbing");
        protected static readonly int ClimbOverLedgeHash = Animator.StringToHash("ClimbingOverLedge");
        
        protected const float crossFadeDuration = 0.3f;

        protected PlayerBaseState(PlayerController player, Animator animator)
        {
            this.player = player;
            this.animator = animator;
        }
        
        public virtual void OnEnter() { }
        public virtual void Update() { }
        public virtual void FixedUpdate() { }
        public virtual void OnExit() { }
    }
}
