using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Coreline
{
    public enum ToolType
    {
        Pickaxe
    }

    public enum OreType
    {
        Coal,
        Iron,
        Gold,
        Diamond,
        Emerald
    }

    [System.Serializable]
    public class ItemDescription
    {
        public string Name;
        [TextArea(3, 10)]
        public string ToolTip;
        public Sprite Icon;
    }

    [System.Serializable]
    public class InventoryItem
    {
        public InventoryItemData item;
        public const int maxCount = 99;
        [HideInInspector]
        public int count;

        public event Action OnCountDecreased;
        public bool isEmpty => item == null;
        public bool IsTool => item is ToolItemSO;
        ToolItemSO Tool => item as ToolItemSO;

        public void IncreaseCount(int amount)
        {
            count = Mathf.Min(count + amount, maxCount);
        }

        public void DecreaseCount(int amount)
        {
            int previousCount = count;
            count = Mathf.Max(0, count - amount);

            if (count < previousCount)
            {
                OnCountDecreased?.Invoke();
            }
        }
    }

    public abstract class InventoryItemData : ScriptableObject
    {
        public ItemDescription itemDesc;
    }
    
}
