using UnityEngine;

namespace Coreline.Robots
{
    public class RobotIdleState : RobotBaseState
    {
        public RobotIdleState(BaseRobotController robot, Animator animator) : base(robot, animator) { }

        public override void OnEnter()
        {
            robot.SetStatus(RobotWorkState.Idle);

            if (animator != null)
            {
                animator.CrossFade(IdleHash, crossFadeDuration);
            }
        }

        public override void FixedUpdate()
        {
            robot.HandleMovement();
        }
       
    }
}
