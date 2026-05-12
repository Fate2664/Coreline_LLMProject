using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(CollectingRobotCommandExecutor))]
    public class CollectingRobotController : BaseRobotController
    {
        [SerializeField] private CollectingRobotInventory inventory;
        
        public CollectingRobotInventory Inventory => inventory;

        protected override void Awake()
        {
            base.Awake();
            inventory = EnsureComponent(inventory);
        }
        
        public override bool CanExecuteAction(RobotCommandAction action)
        {
            return base.CanExecuteAction(action) ||
                   action == RobotCommandAction.Pickup ||
                   action == RobotCommandAction.Deliver;
        }
    }
}
