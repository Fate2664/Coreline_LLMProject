using UnityEngine;

namespace Coreline.Robots
{
    public class WorkbenchInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private WorkbenchUIController workbenchUI;
        [SerializeField] IndicatorManager indicatorManager;
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;

        private bool wasCurrentTarget;

        private void Awake()
        {
            EnsureReferences();
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
            }
            else
            {
                indicatorManager.HideIndictor();
            }

            if (!isCurrentTarget && wasCurrentTarget && workbenchUI != null)
            {
                workbenchUI.Close();
            }

            wasCurrentTarget = isCurrentTarget;
        }

        public void Interact(Player interactor)
        {
            workbenchUI ??= WorkbenchUIController.FindOrCreateInScene();
            if (workbenchUI == null)
            {
                return;
            }

            workbenchUI.Toggle(interactor);
        }

        private void EnsureReferences()
        {
            playerInteractionDetector ??= FindFirstObjectByType<PlayerInteractionDetector>();
            workbenchUI ??= WorkbenchUIController.FindOrCreateInScene();
        }
    }
}
