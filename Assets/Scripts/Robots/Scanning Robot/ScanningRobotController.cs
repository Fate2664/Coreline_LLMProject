namespace Coreline.Robots
{
    [UnityEngine.RequireComponent(typeof(ScanningRobotCommandExecutor))]
    public class ScanningRobotController : BaseRobotController
    {
        public override bool CanExecuteAction(RobotCommandAction action)
        {
            return base.CanExecuteAction(action) || action == RobotCommandAction.Scan;
        }
    }
}
