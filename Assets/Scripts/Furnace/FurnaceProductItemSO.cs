using UnityEngine;

namespace Coreline
{
    [CreateAssetMenu(menuName = "Inventory/Furnace Product")]
    public sealed class FurnaceProductItemSO : InventoryItemData
    {
        public OreType oreType;
    }
}
