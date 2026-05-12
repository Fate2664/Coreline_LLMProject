using System.Collections;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(ScanningRobotController))]
    public class ScanningRobotCommandExecutor : BaseRobotCommandExecutor
    {
        [SerializeField] private float scanDuration = 2f;

        protected override IEnumerator ExecuteCommandByAction(RobotCommand command)
        {
            if (command.ActionType == RobotCommandAction.Scan)
            {
                yield return ExecuteScan(command);
                yield break;
            }

            yield return base.ExecuteCommandByAction(command);
        }

        private IEnumerator ExecuteScan(RobotCommand command)
        {
            if (!TryResolveTarget(command, out CommandTarget target))
            {
                yield break;
            }

            bool reached = false;
            yield return MoveToTarget(target, Mathf.Max(defaultStoppingDistance, target.InteractionRadius), value => reached = value);

            if (!reached)
            {
                yield break;
            }

            robot.SetStatus(RobotWorkState.Scanning);
            StopAgent();
            yield return new WaitForSeconds(Mathf.Max(0.1f, scanDuration));
            robot.SetStatus(RobotWorkState.Idle);
        }
    }
}
