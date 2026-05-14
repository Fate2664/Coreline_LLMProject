using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class CollectionRobotIdleState : CollectionRobotBaseState
    {
        public CollectionRobotIdleState(BaseRobotController robot, Animator animator) : base(robot, animator) { }
        
        public override void OnEnter()
        {
            robot.SetStatus(RobotWorkState.Idle);
            animator.CrossFade(IdleHash, crossFadeDuration);            
        }

        public override void FixedUpdate()
        {
            robot.HandleMovement();
        }
    }
}