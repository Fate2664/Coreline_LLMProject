using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    [CreateAssetMenu(menuName = "Inventory/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public List<InventoryItem> items = new ();

        public List<InventoryItem> GetEmptyItems(int count)
        {
            List<InventoryItem> toRet = new List<InventoryItem>(count);
            for (int i = 0; i < count; i++)
            {
                toRet.Add(new InventoryItem());
            }
            return toRet;
        }        
    }
}