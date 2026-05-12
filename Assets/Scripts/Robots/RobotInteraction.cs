using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    public class RobotInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;
        [SerializeField] private RobotChatUIController chatUI;
        [SerializeField] private BaseRobotController robotController;
        [SerializeField] private bool closeChatWhenPlayerLeaves = true;

        private IndicatorManager indicatorManager;
        
        private void Awake()
        {
            indicatorManager = GetComponentInChildren<IndicatorManager>();
            robotController ??= GetComponentInParent<BaseRobotController>();
            robotController ??= GetComponentInChildren<BaseRobotController>();
        }

        private void Start()
        {
            playerInteractionDetector ??= FindFirstObjectByType<PlayerInteractionDetector>();
            chatUI ??= RobotChatUIController.FindOrCreateInScene();
        }

        private void FixedUpdate()
        {
            if (indicatorManager == null || playerInteractionDetector == null) return;

            bool isCurrentTarget = ReferenceEquals(playerInteractionDetector.CurrentTarget, this);
            if (isCurrentTarget)
            {
                indicatorManager.ShowIndictor();
            }
            else
            {
                indicatorManager.HideIndictor();
            }

            if (closeChatWhenPlayerLeaves &&
                !isCurrentTarget &&
                chatUI != null &&
                ReferenceEquals(chatUI.ActiveRobot, robotController))
            {
                chatUI.Close();
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

            chatUI.OpenForRobot(robotController, player);
        }
    }
}
