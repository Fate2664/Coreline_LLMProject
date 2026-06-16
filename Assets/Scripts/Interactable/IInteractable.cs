using Coreline;
using UnityEngine;

public interface IInteractable
{
    public void Interact(Player interactor);
}

public interface IAltInteractable
{
    public void AltInteract(Player interactor);
}
