using System;
using System.Collections.Generic;

namespace Coreline
{
    public enum CraftingFailureReason
    {
        None,
        InvalidRecipe,
        InvalidInventory,
        MissingIngredients,
        InventoryFull,
        TransactionFailed
    }

    public readonly struct CraftingResult
    {
        private CraftingResult(bool succeeded, CraftingFailureReason failureReason, InventoryItemData relevantItem, int requiredAmount, int availableAmount)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            RelevantItem = relevantItem;
            RequiredAmount = requiredAmount;
            AvailableAmount = availableAmount;
        }

        public bool Succeeded { get; }
        public CraftingFailureReason FailureReason { get; }
        public InventoryItemData RelevantItem { get; }
        public int RequiredAmount { get; }
        public int AvailableAmount { get; }
        public int MissingAmount => Math.Max(0, RequiredAmount - AvailableAmount);

        public static CraftingResult Success()
        {
            return new CraftingResult(true, CraftingFailureReason.None, null, 0, 0);
        }

        public static CraftingResult Failure(
            CraftingFailureReason reason,
            InventoryItemData relevantItem = null,
            int requiredAmount = 0,
            int availableAmount = 0)
        {
            return new CraftingResult(
                false,
                reason,
                relevantItem,
                requiredAmount,
                availableAmount);
        }
    }

    public sealed class CraftingService
    {
        public CraftingResult Evaluate(CraftingRecipe recipe, PlayerInventory inventory, int quantity = 1)
        {
            return inventory == null
                ? CraftingResult.Failure(CraftingFailureReason.InvalidInventory)
                : Evaluate(recipe, inventory.Container, quantity);
        }

        public CraftingResult Evaluate(CraftingRecipe recipe, InventoryContainer inventory, int quantity = 1)
        {
            if (!TryBuildTransaction(recipe, inventory, quantity, out Dictionary<InventoryItemData, int> requiredItems, out InventoryItemData craftedItem, out int craftedAmount, out CraftingResult failure))
            {
                return failure;
            }

            foreach (KeyValuePair<InventoryItemData, int> requirement in requiredItems)
            {
                int availableAmount = inventory.GetCount(requirement.Key);
                if (availableAmount < requirement.Value)
                {
                    return CraftingResult.Failure(
                        CraftingFailureReason.MissingIngredients,
                        requirement.Key,
                        requirement.Value,
                        availableAmount);
                }
            }

            return CanFitOutputAfterSpending(
                inventory,
                requiredItems,
                craftedItem,
                craftedAmount)
                ? CraftingResult.Success()
                : CraftingResult.Failure(
                    CraftingFailureReason.InventoryFull,
                    craftedItem,
                    craftedAmount);
        }

        public CraftingResult TryCraft(
            CraftingRecipe recipe,
            PlayerInventory inventory,
            int quantity = 1,
            bool countForProgression = true)
        {
            if (inventory == null)
            {
                return CraftingResult.Failure(CraftingFailureReason.InvalidInventory);
            }

            CraftingResult evaluation = Evaluate(recipe, inventory, quantity);
            if (!evaluation.Succeeded)
            {
                return evaluation;
            }

            if (!TryBuildTransaction(
                    recipe,
                    inventory.Container,
                    quantity,
                    out Dictionary<InventoryItemData, int> requiredItems,
                    out InventoryItemData craftedItem,
                    out int craftedAmount,
                    out CraftingResult failure))
            {
                return failure;
            }

            List<KeyValuePair<InventoryItemData, int>> spentItems = new();

            foreach (KeyValuePair<InventoryItemData, int> requirement in requiredItems)
            {
                if (!inventory.TryRemoveItem(requirement.Key, requirement.Value))
                {
                    RestoreItems(inventory, spentItems);
                    return CraftingResult.Failure(CraftingFailureReason.TransactionFailed);
                }

                spentItems.Add(requirement);
            }

            if (inventory.TryAddItem(craftedItem, craftedAmount, countForProgression))
            {
                return CraftingResult.Success();
            }

            RestoreItems(inventory, spentItems);
            return CraftingResult.Failure(
                CraftingFailureReason.TransactionFailed,
                craftedItem,
                craftedAmount);
        }

        public CraftingResult TryCraft(
            CraftingRecipe recipe,
            InventoryContainer inventory,
            int quantity = 1)
        {
            CraftingResult evaluation = Evaluate(recipe, inventory, quantity);
            if (!evaluation.Succeeded)
            {
                return evaluation;
            }

            if (!TryBuildTransaction(
                    recipe,
                    inventory,
                    quantity,
                    out Dictionary<InventoryItemData, int> requiredItems,
                    out InventoryItemData craftedItem,
                    out int craftedAmount,
                    out CraftingResult failure))
            {
                return failure;
            }

            List<KeyValuePair<InventoryItemData, int>> spentItems = new();

            foreach (KeyValuePair<InventoryItemData, int> requirement in requiredItems)
            {
                if (!inventory.TryRemove(requirement.Key, requirement.Value))
                {
                    RestoreItems(inventory, spentItems);
                    return CraftingResult.Failure(CraftingFailureReason.TransactionFailed);
                }

                spentItems.Add(requirement);
            }

            if (inventory.TryAdd(craftedItem, craftedAmount))
            {
                return CraftingResult.Success();
            }

            RestoreItems(inventory, spentItems);
            return CraftingResult.Failure(
                CraftingFailureReason.TransactionFailed,
                craftedItem,
                craftedAmount);
        }

        private static bool TryBuildTransaction(
            CraftingRecipe recipe,
            InventoryContainer inventory,
            int quantity,
            out Dictionary<InventoryItemData, int> requiredItems,
            out InventoryItemData craftedItem,
            out int craftedAmount,
            out CraftingResult failure)
        {
            requiredItems = new Dictionary<InventoryItemData, int>();
            craftedItem = null;
            craftedAmount = 0;

            if (inventory == null)
            {
                failure = CraftingResult.Failure(CraftingFailureReason.InvalidInventory);
                return false;
            }

            if (recipe == null ||
                recipe.CraftedItem == null ||
                quantity <= 0 ||
                recipe.CraftedAmount <= 0 ||
                recipe.Requirements == null)
            {
                failure = CraftingResult.Failure(CraftingFailureReason.InvalidRecipe);
                return false;
            }

            foreach (CraftingIngredient ingredient in recipe.Requirements)
            {
                if (ingredient == null || !ingredient.IsValid ||
                    !TryMultiply(ingredient.Amount, quantity, out int requiredAmount))
                {
                    failure = CraftingResult.Failure(CraftingFailureReason.InvalidRecipe);
                    return false;
                }

                requiredItems.TryGetValue(ingredient.Item, out int currentAmount);
                if (!TryAdd(currentAmount, requiredAmount, out int combinedAmount))
                {
                    failure = CraftingResult.Failure(CraftingFailureReason.InvalidRecipe);
                    return false;
                }

                requiredItems[ingredient.Item] = combinedAmount;
            }

            if (!TryMultiply(recipe.CraftedAmount, quantity, out craftedAmount))
            {
                failure = CraftingResult.Failure(CraftingFailureReason.InvalidRecipe);
                return false;
            }

            craftedItem = recipe.CraftedItem;
            failure = default;
            return true;
        }

        private static bool CanFitOutputAfterSpending(
            InventoryContainer inventory,
            IReadOnlyDictionary<InventoryItemData, int> requiredItems,
            InventoryItemData craftedItem,
            int craftedAmount)
        {
            List<SimulatedSlot> simulatedSlots = new(inventory.SlotCount);

            foreach (InventorySlot slot in inventory.Slots)
            {
                simulatedSlots.Add(new SimulatedSlot(slot.Item, slot.Amount));
            }

            foreach (KeyValuePair<InventoryItemData, int> requirement in requiredItems)
            {
                int remaining = requirement.Value;

                for (int i = 0; i < simulatedSlots.Count && remaining > 0; i++)
                {
                    SimulatedSlot slot = simulatedSlots[i];
                    if (slot.Item != requirement.Key || slot.Amount <= 0)
                    {
                        continue;
                    }

                    int removedAmount = Math.Min(slot.Amount, remaining);
                    slot.Amount -= removedAmount;
                    remaining -= removedAmount;

                    if (slot.Amount <= 0)
                    {
                        slot.Item = null;
                    }

                    simulatedSlots[i] = slot;
                }
            }

            int outputRemaining = craftedAmount;

            for (int i = 0; i < simulatedSlots.Count && outputRemaining > 0; i++)
            {
                SimulatedSlot slot = simulatedSlots[i];
                if (slot.Item != craftedItem)
                {
                    continue;
                }

                int addedAmount = Math.Min(
                    inventory.MaxStackSize - slot.Amount,
                    outputRemaining);

                slot.Amount += addedAmount;
                outputRemaining -= addedAmount;
                simulatedSlots[i] = slot;
            }

            for (int i = 0; i < simulatedSlots.Count && outputRemaining > 0; i++)
            {
                SimulatedSlot slot = simulatedSlots[i];
                if (slot.Item != null && slot.Amount > 0)
                {
                    continue;
                }

                int addedAmount = Math.Min(inventory.MaxStackSize, outputRemaining);
                simulatedSlots[i] = new SimulatedSlot(craftedItem, addedAmount);
                outputRemaining -= addedAmount;
            }

            return outputRemaining <= 0;
        }

        private static void RestoreItems(
            PlayerInventory inventory,
            IReadOnlyList<KeyValuePair<InventoryItemData, int>> spentItems)
        {
            for (int i = spentItems.Count - 1; i >= 0; i--)
            {
                KeyValuePair<InventoryItemData, int> spentItem = spentItems[i];
                inventory.TryAddItem(
                    spentItem.Key,
                    spentItem.Value,
                    countForProgression: false);
            }
        }

        private static void RestoreItems(
            InventoryContainer inventory,
            IReadOnlyList<KeyValuePair<InventoryItemData, int>> spentItems)
        {
            for (int i = spentItems.Count - 1; i >= 0; i--)
            {
                KeyValuePair<InventoryItemData, int> spentItem = spentItems[i];
                inventory.TryAdd(spentItem.Key, spentItem.Value);
            }
        }

        private static bool TryMultiply(int left, int right, out int result)
        {
            long value = (long)left * right;
            result = value > 0 && value <= int.MaxValue ? (int)value : 0;
            return result > 0;
        }

        private static bool TryAdd(int left, int right, out int result)
        {
            long value = (long)left + right;
            result = value > 0 && value <= int.MaxValue ? (int)value : 0;
            return result > 0;
        }

        private struct SimulatedSlot
        {
            public SimulatedSlot(InventoryItemData item, int amount)
            {
                Item = item;
                Amount = amount;
            }

            public InventoryItemData Item;
            public int Amount;
        }
    }
}
