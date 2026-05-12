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
        [SerializeField] private int maxItemStacks = 12;
        [SerializeField] private int maxAmountPerStack = 99;
        [SerializeField] private List<RobotInventoryItemStack> itemStacks = new();
        [SerializeField] private List<RobotResourceStack> resourceStacks = new();

        public IReadOnlyList<RobotInventoryItemStack> ItemStacks => itemStacks;
        public IReadOnlyList<RobotResourceStack> ResourceStacks => resourceStacks;
        public bool HasAnyItems => itemStacks.Count > 0 || resourceStacks.Count > 0;

        public bool TryAddItem(InventoryItemData item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            RobotInventoryItemStack existing = itemStacks.Find(stack => stack.item == item);
            if (existing != null)
            {
                existing.amount = Mathf.Min(existing.amount + amount, maxAmountPerStack);
                return true;
            }

            if (itemStacks.Count >= maxItemStacks)
            {
                return false;
            }

            itemStacks.Add(new RobotInventoryItemStack
            {
                item = item,
                amount = Mathf.Min(amount, maxAmountPerStack)
            });

            return true;
        }

        public bool TryAddResource(OreType oreType, int amount = 1)
        {
            if (amount <= 0)
            {
                return false;
            }

            RobotResourceStack existing = resourceStacks.Find(stack => stack.oreType == oreType);
            if (existing != null)
            {
                existing.amount = Mathf.Min(existing.amount + amount, maxAmountPerStack);
                return true;
            }

            if (resourceStacks.Count >= maxItemStacks)
            {
                return false;
            }

            resourceStacks.Add(new RobotResourceStack
            {
                oreType = oreType,
                amount = Mathf.Min(amount, maxAmountPerStack)
            });

            return true;
        }

        public int GetResourceAmount(OreType oreType)
        {
            int amount = 0;

            RobotResourceStack resourceStack = resourceStacks.Find(stack => stack.oreType == oreType);
            if (resourceStack != null)
            {
                amount += resourceStack.amount;
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

            return true;
        }

        public void ClearAll()
        {
            itemStacks.Clear();
            resourceStacks.Clear();
        }

        public bool TryTransferTo(IRobotInventoryReceiver receiver)
        {
            return receiver != null && receiver.TryReceiveFromRobot(this);
        }
    }
}
