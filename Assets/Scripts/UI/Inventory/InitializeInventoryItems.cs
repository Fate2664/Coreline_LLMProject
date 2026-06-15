using UnityEngine;
using System.Collections;
using Coreline;

[System.Serializable]
public class StartingInventoryItem
{
    public InventoryItemData item;
    public int count = 1;
}

public class InitializeInventoryItems : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private StartingInventoryItem[] items;
    [SerializeField] private bool countForProgression = true;

    private IEnumerator Start()
    {
        ResolveInventory();

        if (playerInventory == null)
        {
            yield return null;
            ResolveInventory();
        }

        if (playerInventory == null)
        {
            Debug.LogWarning(
                $"{nameof(InitializeInventoryItems)} could not find a {nameof(PlayerInventory)}.",
                this);
            yield break;
        }

        foreach (var item in items)
        {
            if (item != null && item.item != null)
            {
                playerInventory.TryAddItem(
                    item.item,
                    item.count,
                    countForProgression);
            }
        }
    }

    private void ResolveInventory()
    {
        playerInventory ??= GetComponent<PlayerInventory>();
        playerInventory ??= GetComponentInParent<PlayerInventory>();
        playerInventory ??=
            FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }
}
