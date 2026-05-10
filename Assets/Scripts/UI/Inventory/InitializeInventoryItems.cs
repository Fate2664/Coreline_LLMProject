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
    
    private IEnumerator Start()
    {
        UIManager uiManager = FindObjectOfType<UIManager>();
        // Wait for one frame to ensure UpgradesPanel.Start() has run
        yield return null;

        foreach (var item in items)
        {
            if (item != null && item.item != null)
            {
                uiManager.AddItemToInventory(item.item, item.count);
            }
        }
    }
}
