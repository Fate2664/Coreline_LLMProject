using System;
using UnityEngine;

namespace Coreline
{
    [Serializable]
    public sealed class InventorySlot
    {
        public const int DefaultMaxStackSize = 99;

        [SerializeField] private InventoryItemData item;
        [SerializeField, Min(0)] private int amount;

        public InventoryItemData Item => item;
        public int Amount => amount;
        public bool IsEmpty => item == null || amount <= 0;

        public bool CanStack(InventoryItemData itemToAdd)
        {
            return itemToAdd != null && (IsEmpty || item == itemToAdd);
        }

        public int GetAvailableSpace(InventoryItemData itemToAdd, int maxStackSize)
        {
            if (!CanStack(itemToAdd))
            {
                return 0;
            }

            return Mathf.Max(0, maxStackSize - amount);
        }

        internal int Add(InventoryItemData itemToAdd, int amountToAdd, int maxStackSize)
        {
            if (amountToAdd <= 0 || !CanStack(itemToAdd))
            {
                return 0;
            }

            if (IsEmpty)
            {
                item = itemToAdd;
                amount = 0;
            }

            int addedAmount = Mathf.Min(amountToAdd, Mathf.Max(0, maxStackSize - amount));
            amount += addedAmount;
            return addedAmount;
        }

        internal int Remove(int amountToRemove)
        {
            if (IsEmpty || amountToRemove <= 0)
            {
                return 0;
            }

            int removedAmount = Mathf.Min(amount, amountToRemove);
            amount -= removedAmount;

            if (amount <= 0)
            {
                Clear();
            }

            return removedAmount;
        }

        internal void Clear()
        {
            item = null;
            amount = 0;
        }

        internal void Normalize()
        {
            if (item == null || amount <= 0)
            {
                Clear();
                return;
            }

            amount = Mathf.Max(1, amount);
        }
    }
}
