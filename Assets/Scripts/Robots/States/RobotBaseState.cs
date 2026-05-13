using UnityEngine;

namespace Coreline.Robots
{
    public enum RobotWorkState
    {
        Idle,
        Walking,
        Mining,
        Scanning,
        Collecting
    }
    
    public abstract class RobotBaseState : IState
    {
        protected readonly BaseRobotController robot;
        protected readonly Animator animator;
        
        protected static readonly int IdleHash = Animator.StringToHash("Idle");
        protected static readonly int WalkHash = Animator.StringToHash("Walk");
        protected static readonly int MiningHash = Animator.StringToHash("Mine");

        protected const float crossFadeDuration = 0.3f;

        protected RobotBaseState(BaseRobotController robot, Animator animator)
        {
            this.robot = robot;
            this.animator = animator;
        }
        
        public virtual void OnEnter(){}
        public virtual void OnExit(){}
        public virtual void Update() {}
        public virtual void FixedUpdate() {}
    }
}
