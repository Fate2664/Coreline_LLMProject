using UnityEngine;

public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private GameInput input;
    private PlayerController player;
    private IInteractable currentTarget;
    private Collider currentIteractableObject;
    private bool wasInteractPressed;

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

        if (isInteractPressed && !wasInteractPressed && currentTarget != null)
        {
            currentTarget.Interact(player);
        }

        wasInteractPressed = isInteractPressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null && other.CompareTag("Interaction Trigger"))
        {
            currentTarget = interactable;
            currentIteractableObject = other;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null && interactable == currentTarget && other.CompareTag("Interaction Trigger"))
        {
            currentTarget = null;
            currentIteractableObject = null;
        }
    }
}
