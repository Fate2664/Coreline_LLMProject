using Coreline;
using Coreline.Robots;
using UnityEngine;

public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private GameInput input;
    [SerializeField, Min(0f)] private float detectionRadius = 0.75f;
    [SerializeField, Min(0f)] private float detectionCenterHeight = 0.8f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField, Min(1)] private int maxDetectedColliders = 16;

    private Player player;
    private IInteractable currentTarget;
    private Collider currentIteractableObject;
    private Collider[] detectedColliders;
    private bool wasInteractPressed;
    private bool wasAltInteractPressed;

    public IInteractable CurrentTarget => currentTarget;
    public Collider CurrentIteractableObject => currentIteractableObject;

    private void Awake()
    {
        player = GetComponent<Player>() ?? GetComponentInParent<Player>();
        input ??= GetComponent<GameInput>();
        detectedColliders = new Collider[Mathf.Max(1, maxDetectedColliders)];
    }

    private void Update()
    {
        RefreshCurrentTarget();

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
        TrySetCurrentTarget(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (currentTarget == null)
        {
            TrySetCurrentTarget(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (TryGetInteractable(other, out IInteractable interactable) &&
            interactable == currentTarget &&
            IsInteractionCollider(other))
        {
            currentTarget = null;
            currentIteractableObject = null;
        }
    }

    private static bool IsInteractionCollider(Collider other)
    {
        return other.isTrigger || other.CompareTag("Interaction Trigger") || other.CompareTag("House");
    }

    private void TrySetCurrentTarget(Collider other)
    {
        if (!TryGetInteractable(other, out IInteractable interactable) || !IsInteractionCollider(other))
        {
            return;
        }

        currentTarget = interactable;
        currentIteractableObject = other;
    }

    private void RefreshCurrentTarget()
    {
        if (detectedColliders == null || detectedColliders.Length != Mathf.Max(1, maxDetectedColliders))
        {
            detectedColliders = new Collider[Mathf.Max(1, maxDetectedColliders)];
        }

        Vector3 center = GetDetectionCenter();
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            detectionRadius,
            detectedColliders,
            interactionMask,
            QueryTriggerInteraction.Collide);

        IInteractable bestTarget = null;
        Collider bestCollider = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = detectedColliders[i];
            if (candidate == null ||
                candidate.transform.IsChildOf(transform) ||
                !TryGetInteractable(candidate, out IInteractable interactable) ||
                !IsInteractionCollider(candidate))
            {
                continue;
            }

            float distance = (candidate.ClosestPoint(center) - center).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = interactable;
            bestCollider = candidate;
        }

        currentTarget = bestTarget;
        currentIteractableObject = bestCollider;
    }

    private Vector3 GetDetectionCenter()
    {
        Transform source = player != null && player.PlayerCharacter != null
            ? player.PlayerCharacter.transform
            : transform;

        return source.position + Vector3.up * detectionCenterHeight;
    }

    private static bool TryGetInteractable(Collider collider, out IInteractable interactable)
    {
        interactable = collider != null ? collider.GetComponentInParent<IInteractable>() : null;
        return interactable != null;
    }
}
