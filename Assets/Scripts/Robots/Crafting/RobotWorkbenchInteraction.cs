using UnityEngine;

namespace Coreline.Robots
{
    public class RobotWorkbenchInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private RobotWorkbenchUIController workbenchUI;

        public void Interact(PlayerController interactor)
        {
            workbenchUI ??= RobotWorkbenchUIController.FindOrCreateInScene();
            if (workbenchUI == null)
            {
                return;
            }

            workbenchUI.Toggle(interactor);
        }
    }
}
