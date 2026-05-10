using UnityEngine;

namespace Coreline
{
    [CreateAssetMenu(menuName = "Inventory/Tool")]
    public class ToolItemSO : InventoryItemData
    {
        public ToolType toolType;
    }
}
