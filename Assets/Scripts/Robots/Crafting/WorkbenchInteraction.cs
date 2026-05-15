using UnityEngine;

namespace Coreline.Robots
{
    public class WorkbenchInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private WorkbenchUIController workbenchUI;
        [SerializeField] IndicatorManager indicatorManager;
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;

        private bool wasCurrentTarget;

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

            if (!isCurrentTarget && wasCurrentTarget && workbenchUI != null)
            {
                workbenchUI.Close();
            }

            wasCurrentTarget = isCurrentTarget;
        }

        public void Interact(PlayerController interactor)
        {
            workbenchUI ??= WorkbenchUIController.FindOrCreateInScene();
            if (workbenchUI == null)
            {
                return;
            }

            workbenchUI.Toggle(interactor);
        }
    }
}
