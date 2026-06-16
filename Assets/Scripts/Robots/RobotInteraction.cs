using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class RobotInteraction : MonoBehaviour, IInteractable, IAltInteractable
    {
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;
        [SerializeField] private RobotChatUIController chatUI;
        [SerializeField] private MiningRobotChatUIController miningRobotChatUI;
        [SerializeField] private CollectingRobotChatUIController collectingRobotChatUI;
        [SerializeField] private CollectingRobotInventoryUIController collectingRobotInventoryUI;
        [SerializeField] private BaseRobotController robotController;
        [SerializeField] private bool closeChatWhenPlayerLeaves = true;
        [SerializeField] private bool closeInventoryWhenPlayerLeaves = true;
        [SerializeField] IndicatorManager indicatorManager;
        [SerializeField] IndicatorManager altIndicatorManager;
        
        private void Awake()
        {
            robotController ??= GetComponentInParent<BaseRobotController>();
            robotController ??= GetComponentInChildren<BaseRobotController>();
        }

        private void Start()
        {
            EnsureReferences();
        }

        private void FixedUpdate()
        {
            EnsureReferences();
            if (indicatorManager == null || playerInteractionDetector == null) return;

            bool isCurrentTarget = ReferenceEquals(playerInteractionDetector.CurrentTarget, this);
            if (isCurrentTarget)
            {
                indicatorManager.ShowIndictor();
                if (altIndicatorManager != null)
                {
                    altIndicatorManager.ShowIndictor();
                }
            }
            else
            {
                indicatorManager.HideIndictor();
                if (altIndicatorManager != null)
                {
                    altIndicatorManager.HideIndictor();
                }
            }

            if (closeChatWhenPlayerLeaves &&
                !isCurrentTarget &&
                chatUI != null &&
                ReferenceEquals(chatUI.ActiveRobot, robotController))
            {
                chatUI.Close();
            }

            if (closeInventoryWhenPlayerLeaves &&
                !isCurrentTarget &&
                collectingRobotInventoryUI != null &&
                ReferenceEquals(collectingRobotInventoryUI.ActiveRobot, robotController))
            {
                collectingRobotInventoryUI.Close();
            }
        }

        public void Interact(Player player)
        {
            if (robotController == null)
            {
                Debug.LogWarning($"{name} has no {nameof(BaseRobotController)} to receive LLM commands.", this);
                return;
            }

            chatUI = ResolveChatUIForRobot();
            if (chatUI == null)
            {
                Debug.LogWarning(
                    $"Cannot open {robotController.GetType().Name} chat because the expected UI root " +
                    $"{GetExpectedChatRootName()} was not found in the scene.",
                    this);
                return;
            }

            collectingRobotInventoryUI?.Close();
            CloseOtherChatControllers(chatUI);
            chatUI.OpenForRobot(robotController, player);
        }

        public void AltInteract(Player player)
        {
            if (robotController is not CollectingRobotController collectingRobot)
            {
                return;
            }

            collectingRobotInventoryUI ??= CollectingRobotInventoryUIController.FindOrCreateInScene();
            if (collectingRobotInventoryUI == null)
            {
                Debug.LogWarning("Cannot open collection robot inventory because CollectionRobotInventoryRoot was not found.", this);
                return;
            }

            chatUI?.Close();
            collectingRobotInventoryUI.ToggleForRobot(collectingRobot, player);
        }

        private RobotChatUIController ResolveChatUIForRobot()
        {
            if (robotController is MiningRobotController)
            {
                miningRobotChatUI ??= chatUI as MiningRobotChatUIController;
                miningRobotChatUI ??= MiningRobotChatUIController.FindOrCreateInScene();
                return miningRobotChatUI;
            }

            if (robotController is CollectingRobotController)
            {
                collectingRobotChatUI ??= chatUI as CollectingRobotChatUIController;
                collectingRobotChatUI ??= CollectingRobotChatUIController.FindOrCreateInScene();
                return collectingRobotChatUI;
            }

            return chatUI != null ? chatUI : RobotChatUIController.FindOrCreateInScene();
        }

        private void EnsureReferences()
        {
            playerInteractionDetector ??= FindFirstObjectByType<PlayerInteractionDetector>();
            chatUI ??= ResolveChatUIForRobot();
            collectingRobotInventoryUI ??= CollectingRobotInventoryUIController.FindOrCreateInScene();
        }

        private string GetExpectedChatRootName()
        {
            if (robotController is MiningRobotController)
            {
                return RobotChatUIController.GetExpectedRootNameForController<MiningRobotChatUIController>();
            }

            if (robotController is CollectingRobotController)
            {
                return RobotChatUIController.GetExpectedRootNameForController<CollectingRobotChatUIController>();
            }

            return RobotChatUIController.GetExpectedRootNameForController<RobotChatUIController>();
        }

        private static void CloseOtherChatControllers(RobotChatUIController activeController)
        {
            RobotChatUIController[] controllers =
                FindObjectsByType<RobotChatUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (RobotChatUIController controller in controllers)
            {
                if (controller != null && controller != activeController)
                {
                    controller.Close();
                }
            }
        }
    }
}
