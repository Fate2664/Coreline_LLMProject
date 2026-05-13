using System.Collections;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(ScanningRobotController))]
    public class ScanningRobotCommandExecutor : BaseRobotCommandExecutor
    {
        protected override IEnumerator ExecuteCommandByAction(RobotCommand command)
        {
            robot.RaiseError("Scanning robots scan passively and do not execute commands.");
            yield break;
        }
    }
}
