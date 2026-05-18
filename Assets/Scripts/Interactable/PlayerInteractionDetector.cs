using Coreline.Robots;
using UnityEngine;

public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private GameInput input;
    private PlayerController player;
    private IInteractable currentTarget;
    private Collider currentIteractableObject;
    private bool wasInteractPressed;
    private bool wasAltInteractPressed;

    public IInteractable CurrentTarget => currentTarget;
    public Collider CurrentIteractableObject => currentIteractableObject;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        input ??= GetComponent<GameInput>();
    }

    private void Update()
    {
        if (input == null)
        {
            return;
        }

        bool isInteractPressed = input.IsInteractPressed;
        bool isAltInteractPressed = input.IsAltInteractPressed;

        if (isInteractPressed && !wasInteractPressed && currentTarget != null)
        {
            currentTarget.Interact(player);
        }

        if (!RobotChatUIController.IsAnyOpen &&
            !WorkbenchUIController.IsAnyOpen &&
            isAltInteractPressed &&
            !wasAltInteractPressed &&
            currentTarget is IAltInteractable altInteractable)
        {
            altInteractable.AltInteract(player);
        }

        wasInteractPressed = isInteractPressed;
        wasAltInteractPressed = isAltInteractPressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null && IsInteractionCollider(other))
        {
            currentTarget = interactable;
            currentIteractableObject = other;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null && interactable == currentTarget && IsInteractionCollider(other))
        {
            currentTarget = null;
            currentIteractableObject = null;
        }
    }

    private static bool IsInteractionCollider(Collider other)
    {
        return other.CompareTag("Interaction Trigger") || other.CompareTag("House");
    }
}
