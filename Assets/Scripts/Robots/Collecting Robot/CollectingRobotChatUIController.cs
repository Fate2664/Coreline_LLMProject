namespace Coreline.Robots
{
    public class CollectingRobotChatUIController : RobotChatUIController
    {
        protected override string RobotTypeLabel => "Collection Robot";
        protected override bool ShowsMiningRobotDropdown => true;

        protected override bool CanOpenForRobot(BaseRobotController robot)
        {
            return robot is CollectingRobotController;
        }

        public new static CollectingRobotChatUIController FindOrCreateInScene()
        {
            return FindOrCreateInScene<CollectingRobotChatUIController>();
        }
    }
}
