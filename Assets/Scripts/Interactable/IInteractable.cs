using UnityEngine;

public interface IInteractable
{
    public void Interact(PlayerController interactor);
}

public interface IAltInteractable
{
    public void AltInteract(PlayerController interactor);
}
