using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    [Serializable]
    public sealed class InventoryContainer
    {
        [SerializeField, Min(1)] private int slotCount = 24;
        [SerializeField, Min(1)] private int maxStackSize = InventorySlot.DefaultMaxStackSize;
        [SerializeField] private List<InventorySlot> slots = new();

        public event Action Changed;

        public IReadOnlyList<InventorySlot> Slots
        {
            get
            {
                EnsureInitialized();
                return slots;
            }
        }

        public int SlotCount
        {
            get
            {
                EnsureInitialized();
                return slots.Count;
            }
        }

        public int MaxStackSize => Mathf.Max(1, maxStackSize);

        public void EnsureInitialized()
        {
            slotCount = Mathf.Max(1, slotCount);
            maxStackSize = Mathf.Max(1, maxStackSize);
            slots ??= new List<InventorySlot>(slotCount);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i] ??= new InventorySlot();
                slots[i].Normalize();
            }

            while (slots.Count < slotCount)
            {
                slots.Add(new InventorySlot());
            }

            // Do not silently delete inventory data if slotCount was reduced in the Inspector.
            if (slots.Count > slotCount)
            {
                slotCount = slots.Count;
            }
        }

        public bool TryResize(int newSlotCount)
        {
            EnsureInitialized();
            newSlotCount = Mathf.Max(1, newSlotCount);

            if (newSlotCount == slots.Count)
            {
                return true;
            }

            if (newSlotCount < slots.Count)
            {
                for (int i = newSlotCount; i < slots.Count; i++)
                {
                    if (!slots[i].IsEmpty)
                    {
                        return false;
                    }
                }

                slots.RemoveRange(newSlotCount, slots.Count - newSlotCount);
            }
            else
            {
                while (slots.Count < newSlotCount)
                {
                    slots.Add(new InventorySlot());
                }
            }

            slotCount = newSlotCount;
            Changed?.Invoke();
            return true;
        }

        public bool CanAdd(InventoryItemData item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            EnsureInitialized();

            int availableSpace = 0;
            foreach (InventorySlot slot in slots)
            {
                availableSpace += slot.GetAvailableSpace(item, maxStackSize);
                if (availableSpace >= amount)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAdd(InventoryItemData item, int amount = 1)
        {
            if (!CanAdd(item, amount))
            {
                return false;
            }

            int remaining = amount;

            // Fill matching stacks before using empty slots.
            foreach (InventorySlot slot in slots)
            {
                if (slot.IsEmpty || slot.Item != item)
                {
                    continue;
                }

                remaining -= slot.Add(item, remaining, maxStackSize);
                if (remaining <= 0)
                {
                    Changed?.Invoke();
                    return true;
                }
            }

            foreach (InventorySlot slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    continue;
                }

                remaining -= slot.Add(item, remaining, maxStackSize);
                if (remaining <= 0)
                {
                    Changed?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public int GetCount(InventoryItemData item)
        {
            return item == null ? 0 : GetCount(slotItem => slotItem == item);
        }

        public int GetCount(Func<InventoryItemData, bool> matchesItem)
        {
            if (matchesItem == null)
            {
                return 0;
            }

            EnsureInitialized();

            int total = 0;
            foreach (InventorySlot slot in slots)
            {
                if (!slot.IsEmpty && matchesItem(slot.Item))
                {
                    total += slot.Amount;
                }
            }

            return total;
        }

        public bool TryRemove(InventoryItemData item, int amount = 1)
        {
            return item != null && TryRemove(slotItem => slotItem == item, amount);
        }

        public bool TryRemove(Func<InventoryItemData, bool> matchesItem, int amount = 1)
        {
            if (matchesItem == null || amount <= 0 || GetCount(matchesItem) < amount)
            {
                return false;
            }

            int remaining = amount;
            foreach (InventorySlot slot in slots)
            {
                if (slot.IsEmpty || !matchesItem(slot.Item))
                {
                    continue;
                }

                remaining -= slot.Remove(remaining);
                if (remaining <= 0)
                {
                    Changed?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public bool TryRemoveAt(int slotIndex, int amount = 1)
        {
            EnsureInitialized();

            if (slotIndex < 0 ||
                slotIndex >= slots.Count ||
                amount <= 0 ||
                slots[slotIndex].Amount < amount)
            {
                return false;
            }

            slots[slotIndex].Remove(amount);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            EnsureInitialized();

            bool hadItems = false;
            foreach (InventorySlot slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    hadItems = true;
                    slot.Clear();
                }
            }

            if (hadItems)
            {
                Changed?.Invoke();
            }
        }
    }
}
