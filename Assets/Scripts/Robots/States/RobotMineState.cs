using UnityEngine;

namespace Coreline.Robots
{
    public class RobotMineState : RobotBaseState
    {
        public RobotMineState(BaseRobotController robot, Animator animator) : base(robot, animator) { }

        public override void OnEnter()
        {
            robot.SetStatus(RobotWorkState.Mining);

            if (animator != null)
            {
                animator.CrossFade(MiningHash, crossFadeDuration);
            }
        }

        public override void FixedUpdate()
        {
            robot.HandleMining();
        }

    }
}
