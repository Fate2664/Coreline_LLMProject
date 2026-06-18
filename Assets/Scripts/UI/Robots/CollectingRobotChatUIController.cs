namespace Coreline.Robots
{
    public class CollectingRobotChatUIController : RobotChatUIController
    {
        private const string InventoryBodyName = "InventoryBody";

        [UnityEngine.SerializeField]
        private CollectingRobotInventoryUIController inventoryUI;

        protected override string RobotTypeLabel => "Collection Robot";
        protected override bool ShowsMiningRobotDropdown => true;

        protected override bool CanOpenForRobot(BaseRobotController robot)
        {
            return robot is CollectingRobotController;
        }

        protected override void OnOpenedForRobot(BaseRobotController robot, Coreline.Player player)
        {
            inventoryUI ??= GetComponent<CollectingRobotInventoryUIController>();
            inventoryUI ??= GetComponentInChildren<CollectingRobotInventoryUIController>(true);
            inventoryUI?.Bind(robot as CollectingRobotController, player);
        }

        protected override void OnClosing()
        {
            inventoryUI?.Unbind();
        }

        protected override void OnTabSelected(string bodyName)
        {
            if (bodyName == InventoryBodyName)
            {
                inventoryUI?.RefreshAll();
            }
        }

        public new static CollectingRobotChatUIController FindOrCreateInScene()
        {
            return FindOrCreateInScene<CollectingRobotChatUIController>();
        }
    }
}
