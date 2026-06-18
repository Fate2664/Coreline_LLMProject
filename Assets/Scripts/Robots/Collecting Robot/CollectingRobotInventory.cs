using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    public interface IRobotInventoryReceiver
    {
        bool TryReceiveFromRobot(CollectingRobotInventory sourceInventory);
    }

    [Serializable]
    public class RobotInventoryItemStack
    {
        public InventoryItemData item;
        public int amount;
    }

    [Serializable]
    public class RobotResourceStack
    {
        public OreType oreType;
        public int amount;
    }

    public class CollectingRobotInventory : MonoBehaviour
    {
        [SerializeField] private int maxItemStacks = 24;
        [SerializeField] private int maxAmountPerStack = 99;
        [SerializeField] private List<RobotInventoryItemStack> itemStacks = new();
        [SerializeField] private List<RobotResourceStack> resourceStacks = new();

        private float capacityMultiplier = 1f;

        public event Action InventoryChanged;

        public IReadOnlyList<RobotInventoryItemStack> ItemStacks => itemStacks;
        public IReadOnlyList<RobotResourceStack> ResourceStacks => resourceStacks;
        public bool HasAnyItems => itemStacks.Count > 0 || resourceStacks.Count > 0;
        public int BaseMaxItemStacks => Mathf.Max(1, maxItemStacks);
        public int MaxItemStacks => EffectiveMaxItemStacks;

        public void SetCapacityMultiplier(float multiplier)
        {
            capacityMultiplier = Mathf.Max(0.01f, multiplier);
            InventoryChanged?.Invoke();
        }

        public bool CanAcceptItem(InventoryItemData item, int amount = 1)
        {
            return item != null && amount > 0 && GetAvailableItemSpace(item) >= amount;
        }

        public bool TryAddItem(InventoryItemData item, int amount = 1)
        {
            if (!CanAcceptItem(item, amount))
            {
                return false;
            }

            int remaining = amount;
            int maxStackAmount = EffectiveMaxAmountPerStack;

            foreach (RobotInventoryItemStack stack in itemStacks)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (stack == null || stack.item != item || stack.amount >= maxStackAmount)
                {
                    continue;
                }

                int added = Mathf.Min(maxStackAmount - stack.amount, remaining);
                stack.amount += added;
                remaining -= added;
            }

            while (remaining > 0 && HasFreeStackSlot)
            {
                int added = Mathf.Min(maxStackAmount, remaining);
                itemStacks.Add(new RobotInventoryItemStack
                {
                    item = item,
                    amount = added
                });
                remaining -= added;
            }

            InventoryChanged?.Invoke();
            return remaining <= 0;
        }

        public bool CanAcceptResource(OreType oreType, int amount = 1)
        {
            return amount > 0 && GetAvailableResourceSpace(oreType) >= amount;
        }

        public bool TryAddResource(OreType oreType, int amount = 1)
        {
            if (!CanAcceptResource(oreType, amount))
            {
                return false;
            }

            int remaining = amount;
            int maxStackAmount = EffectiveMaxAmountPerStack;

            foreach (RobotResourceStack stack in resourceStacks)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (stack == null || stack.oreType != oreType || stack.amount >= maxStackAmount)
                {
                    continue;
                }

                int added = Mathf.Min(maxStackAmount - stack.amount, remaining);
                stack.amount += added;
                remaining -= added;
            }

            while (remaining > 0 && HasFreeStackSlot)
            {
                int added = Mathf.Min(maxStackAmount, remaining);
                resourceStacks.Add(new RobotResourceStack
                {
                    oreType = oreType,
                    amount = added
                });
                remaining -= added;
            }

            InventoryChanged?.Invoke();
            return remaining <= 0;
        }

        public int GetResourceAmount(OreType oreType)
        {
            int amount = 0;

            foreach (RobotResourceStack resourceStack in resourceStacks)
            {
                if (resourceStack != null && resourceStack.oreType == oreType)
                {
                    amount += resourceStack.amount;
                }
            }

            foreach (RobotInventoryItemStack itemStack in itemStacks)
            {
                if (itemStack.item is OreItemSO oreItem && oreItem.oreType == oreType)
                {
                    amount += itemStack.amount;
                }
            }

            return amount;
        }

        public bool TryRemoveResource(OreType oreType, int amount)
        {
            if (amount <= 0 || GetResourceAmount(oreType) < amount)
            {
                return false;
            }

            for (int i = itemStacks.Count - 1; i >= 0 && amount > 0; i--)
            {
                RobotInventoryItemStack itemStack = itemStacks[i];
                if (itemStack.item is not OreItemSO oreItem || oreItem.oreType != oreType)
                {
                    continue;
                }

                int removed = Mathf.Min(itemStack.amount, amount);
                itemStack.amount -= removed;
                amount -= removed;

                if (itemStack.amount <= 0)
                {
                    itemStacks.RemoveAt(i);
                }
            }

            for (int i = resourceStacks.Count - 1; i >= 0 && amount > 0; i--)
            {
                RobotResourceStack resourceStack = resourceStacks[i];
                if (resourceStack.oreType != oreType)
                {
                    continue;
                }

                int removed = Mathf.Min(resourceStack.amount, amount);
                resourceStack.amount -= removed;
                amount -= removed;

                if (resourceStack.amount <= 0)
                {
                    resourceStacks.RemoveAt(i);
                }
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryRemoveItem(InventoryItemData item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            int availableAmount = 0;
            foreach (RobotInventoryItemStack itemStack in itemStacks)
            {
                if (itemStack.item == item)
                {
                    availableAmount += itemStack.amount;
                }
            }

            if (availableAmount < amount)
            {
                return false;
            }

            for (int i = itemStacks.Count - 1; i >= 0 && amount > 0; i--)
            {
                RobotInventoryItemStack itemStack = itemStacks[i];
                if (itemStack.item != item)
                {
                    continue;
                }

                int removed = Mathf.Min(itemStack.amount, amount);
                itemStack.amount -= removed;
                amount -= removed;

                if (itemStack.amount <= 0)
                {
                    itemStacks.RemoveAt(i);
                }
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public void ClearAll()
        {
            bool hadItems = HasAnyItems;
            itemStacks.Clear();
            resourceStacks.Clear();

            if (hadItems)
            {
                InventoryChanged?.Invoke();
            }
        }

        public bool TryTransferTo(IRobotInventoryReceiver receiver)
        {
            return receiver != null && receiver.TryReceiveFromRobot(this);
        }

        private int UsedStackCount => itemStacks.Count + resourceStacks.Count;
        private int EffectiveMaxItemStacks =>
            Mathf.Max(1, Mathf.RoundToInt(BaseMaxItemStacks * capacityMultiplier));
        private int EffectiveMaxAmountPerStack => Mathf.Max(1, maxAmountPerStack);
        private int FreeStackSlotCount => Mathf.Max(0, EffectiveMaxItemStacks - UsedStackCount);
        private bool HasFreeStackSlot => FreeStackSlotCount > 0;

        private int GetAvailableItemSpace(InventoryItemData item)
        {
            int availableSpace = FreeStackSlotCount * EffectiveMaxAmountPerStack;

            foreach (RobotInventoryItemStack stack in itemStacks)
            {
                if (stack != null && stack.item == item)
                {
                    availableSpace += Mathf.Max(0, EffectiveMaxAmountPerStack - stack.amount);
                }
            }

            return availableSpace;
        }

        private int GetAvailableResourceSpace(OreType oreType)
        {
            int availableSpace = FreeStackSlotCount * EffectiveMaxAmountPerStack;

            foreach (RobotResourceStack stack in resourceStacks)
            {
                if (stack != null && stack.oreType == oreType)
                {
                    availableSpace += Mathf.Max(0, EffectiveMaxAmountPerStack - stack.amount);
                }
            }

            return availableSpace;
        }
    }
}
