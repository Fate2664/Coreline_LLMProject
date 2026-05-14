using UnityEngine;

namespace Coreline.Robots
{
    public class RobotWalkState : RobotBaseState
    {
        public RobotWalkState(BaseRobotController robot, Animator animator) : base(robot, animator) { }

        public override void OnEnter()
        {
            robot.SetStatus(RobotWorkState.Walking);

            if (animator != null)
            {
                animator.CrossFade(WalkHash, crossFadeDuration);
            }
        }

        public override void FixedUpdate()
        {
            robot.HandleMovement();
        }

    }
}
