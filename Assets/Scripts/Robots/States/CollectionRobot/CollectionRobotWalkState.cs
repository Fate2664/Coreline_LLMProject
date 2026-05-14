using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class CollectionRobotWalkState : CollectionRobotBaseState
    {
        public CollectionRobotWalkState(BaseRobotController robot, Animator animator) : base(robot, animator) { }

        public override void OnEnter()
        {
            robot.SetStatus(RobotWorkState.Walking);
            animator.CrossFade(WalkingHash, crossFadeDuration);            
        }

        public override void FixedUpdate()
        {
            robot.HandleMovement();
        }
    }
}