using UnityEngine;

namespace Coreline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FurnaceController))]
    public sealed class FurnaceInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] private FurnaceController furnace;
        [SerializeField] private FurnaceInventoryUIController furnaceUI;
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;
        [SerializeField] private IndicatorManager indicatorManager;
        [SerializeField] private bool closeWhenPlayerLeaves = true;

        private bool wasCurrentTarget;

        private void Awake()
        {
            EnsureReferences();
        }

        private void FixedUpdate()
        {
            EnsureReferences();

            bool isCurrentTarget =
                playerInteractionDetector != null &&
                ReferenceEquals(playerInteractionDetector.CurrentTarget, this);

            if (indicatorManager != null)
            {
                if (isCurrentTarget)
                {
                    indicatorManager.ShowIndictor();
                }
                else
                {
                    indicatorManager.HideIndictor();
                }
            }

            if (closeWhenPlayerLeaves &&
                !isCurrentTarget &&
                wasCurrentTarget &&
                furnaceUI != null &&
                furnaceUI.IsOpen &&
                furnaceUI.ActiveFurnace == furnace)
            {
                furnaceUI.Close();
            }

            wasCurrentTarget = isCurrentTarget;
        }

        public void Interact(Player interactor)
        {
            EnsureReferences();
            if (furnace == null || furnaceUI == null)
            {
                return;
            }

            furnaceUI.Toggle(furnace, interactor);
        }

        private void EnsureReferences()
        {
            furnace ??= GetComponent<FurnaceController>();
            furnaceUI ??= FurnaceInventoryUIController.FindOrCreateInScene();
            playerInteractionDetector ??=
                FindFirstObjectByType<PlayerInteractionDetector>(FindObjectsInactive.Include);
        }
    }
}
