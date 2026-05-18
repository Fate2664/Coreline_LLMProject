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
    //This script gives items to the player at the start
    
    [SerializeField] private StartingInventoryItem[] items;
    [SerializeField] private bool equipStartingPickaxe = true;
    
    private IEnumerator Start()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            yield break;
        }

        yield return null;

        foreach (var item in items)
        {
            if (item != null && item.item != null)
            {
                uiManager.AddItemToInventory(item.item, item.count);
            }
        }

        if (equipStartingPickaxe)
        {
            uiManager.EquipFirstPickaxe();
        }
    }
}
