using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class RobotInteraction : MonoBehaviour, IInteractable, IAltInteractable
    {
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;
        [SerializeField] private RobotChatUIController chatUI;
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
            playerInteractionDetector ??= FindFirstObjectByType<PlayerInteractionDetector>();
            chatUI ??= RobotChatUIController.FindOrCreateInScene();
            collectingRobotInventoryUI ??= CollectingRobotInventoryUIController.FindOrCreateInScene();
        }

        private void FixedUpdate()
        {
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

        public void Interact(PlayerController player)
        {
            if (robotController == null)
            {
                Debug.LogWarning($"{name} has no {nameof(BaseRobotController)} to receive LLM commands.", this);
                return;
            }

            chatUI ??= RobotChatUIController.FindOrCreateInScene();
            if (chatUI == null)
            {
                Debug.LogWarning("Cannot open LLM chat because LLMChatRoot was not found.", this);
                return;
            }

            collectingRobotInventoryUI?.Close();
            chatUI.OpenForRobot(robotController, player);
        }

        public void AltInteract(PlayerController player)
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
    }
}
