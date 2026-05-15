namespace Coreline.Robots
{
    public class MiningRobotChatUIController : RobotChatUIController
    {
        protected override string RobotTypeLabel => "Mining Robot";
        protected override bool ShowsMiningRobotDropdown => false;

        protected override bool CanOpenForRobot(BaseRobotController robot)
        {
            return robot is MiningRobotController;
        }

        public new static MiningRobotChatUIController FindOrCreateInScene()
        {
            return FindOrCreateInScene<MiningRobotChatUIController>();
        }
    }
}
