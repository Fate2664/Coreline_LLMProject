using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class CollectionRobotBaseState : IState
    {
        protected readonly BaseRobotController robot;
        protected readonly Animator animator;
        
        protected static readonly int WalkingHash = Animator.StringToHash("MoveOpen");
        protected static readonly int IdleHash = Animator.StringToHash("MoveClosed");

        protected const float crossFadeDuration = 0.3f;

        protected CollectionRobotBaseState(BaseRobotController robot, Animator animator)
        {
            this.robot = robot;
            this.animator = animator;
        }

        public virtual void OnEnter(){}
        public virtual void OnExit(){}
        public virtual void Update(){}
        public virtual void FixedUpdate(){}
    }
}
