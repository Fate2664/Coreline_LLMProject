using Coreline;
using UnityEngine;

public sealed class ChestInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private ChestInventory chestInventory;
    [SerializeField] private ChestInventoryUIController chestInventoryUI;
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
            chestInventoryUI != null &&
            chestInventoryUI.IsOpen &&
            chestInventoryUI.ActiveChest == chestInventory)
        {
            chestInventoryUI.Close();
        }

        wasCurrentTarget = isCurrentTarget;
    }

    public void Interact(Player interactor)
    {
        EnsureReferences();
        if (chestInventory == null || chestInventoryUI == null)
        {
            return;
        }

        chestInventoryUI.Toggle(chestInventory, interactor);
    }

    private void EnsureReferences()
    {
        chestInventory ??= GetComponent<ChestInventory>();
        chestInventoryUI ??= ChestInventoryUIController.FindOrCreateInScene();
        playerInteractionDetector ??=
            FindFirstObjectByType<PlayerInteractionDetector>(FindObjectsInactive.Include);
    }
}
