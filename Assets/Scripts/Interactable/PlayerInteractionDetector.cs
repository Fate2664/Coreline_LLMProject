using System;
using System.Collections.Generic;
using System.Numerics;
using Nova;
using UnityEngine;
using UnityEngine.Events;
using Vector3 = UnityEngine.Vector3;

public class PlayerInteractionDetector : MonoBehaviour
{
    private GameInput input;
    private PlayerController player;
    private IInteractable currentTarget;
    private Collider currentIteractableObject;
    public IInteractable CurrentTarget => currentTarget;
    public Collider CurrentIteractableObject => currentIteractableObject;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        input = GetComponent<GameInput>();
    }

    private void Update()
    {
        if (input.IsInteractPressed)
        {
            if (currentTarget != null)
            {
                currentTarget.Interact(player);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<IInteractable>(out IInteractable interactable))
        {
            currentTarget = interactable;
            currentIteractableObject = other;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<IInteractable>(out IInteractable interactable) && interactable == currentTarget)
            currentTarget = null;
        currentIteractableObject = null;
    }
}