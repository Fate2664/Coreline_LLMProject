using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private InventoryContainer inventory = new();

        public event Action<InventoryItemData, int> ItemAddedToInventory;

        public event Action InventoryUpdated
        {
            add => Container.Changed += value;
            remove
            {
                if (inventory != null)
                {
                    inventory.Changed -= value;
                }
            }
        }

        public InventoryContainer Container
        {
            get
            {
                inventory ??= new InventoryContainer();
                inventory.EnsureInitialized();
                return inventory;
            }
        }

        public IReadOnlyList<InventorySlot> Slots => Container.Slots;

        private void Awake()
        {
            Container.EnsureInitialized();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Container.EnsureInitialized();
        }
#endif

        public bool CanAcceptItem(InventoryItemData item, int amount = 1)
        {
            return Container.CanAdd(item, amount);
        }

        public bool TryAddItem(
            InventoryItemData item,
            int amount = 1,
            bool countForProgression = true)
        {
            if (!Container.TryAdd(item, amount))
            {
                return false;
            }

            if (countForProgression)
            {
                ItemAddedToInventory?.Invoke(item, amount);
            }

            return true;
        }

        public int GetItemCount(InventoryItemData item)
        {
            return Container.GetCount(item);
        }

        public bool TryRemoveItem(InventoryItemData item, int amount = 1)
        {
            return Container.TryRemove(item, amount);
        }

        public bool TryRemoveFromSlot(int slotIndex, int amount = 1)
        {
            return Container.TryRemoveAt(slotIndex, amount);
        }

        public int GetOreCount(OreType oreType)
        {
            return Container.GetCount(item => MatchesOreType(item, oreType));
        }

        public bool HasOre(OreType oreType, int amount = 1)
        {
            return amount > 0 && GetOreCount(oreType) >= amount;
        }

        public bool TryRemoveOre(OreType oreType, int amount = 1)
        {
            return Container.TryRemove(
                item => MatchesOreType(item, oreType),
                amount);
        }

        public InventoryItemData FindOreItem(OreType oreType)
        {
            IReadOnlyList<InventorySlot> slots = Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryItemData item = slots[i]?.Item;
                if (MatchesOreType(item, oreType))
                {
                    return item;
                }
            }

            return null;
        }

        public static bool MatchesOreType(
            InventoryItemData item,
            OreType oreType)
        {
            return item switch
            {
                OreItemSO oreItem => oreItem.oreType == oreType,
                FurnaceProductItemSO furnaceProduct =>
                    furnaceProduct.oreType == oreType,
                _ => false
            };
        }

        public void Clear()
        {
            Container.Clear();
        }
    }
}
