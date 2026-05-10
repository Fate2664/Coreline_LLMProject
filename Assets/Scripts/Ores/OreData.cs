using UnityEngine;

namespace Coreline
{
    [CreateAssetMenu(menuName = "Entity/OreData")]
    public class OreData : EntityData
    {
        public InventoryItemData inventoryItemData;
        public int pickupAmount = 1;
    }
}