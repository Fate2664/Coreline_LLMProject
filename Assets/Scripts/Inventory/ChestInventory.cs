using System;
using System.Collections.Generic;
using Coreline.Robots;
using UnityEngine;

namespace Coreline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CommandTarget))]
    public sealed class ChestInventory : MonoBehaviour, IRobotInventoryReceiver
    {
        [Header("Identity")]
        [SerializeField] private string chestName = "Chest_1";
        [SerializeField] private Transform deliveryPoint;
        [SerializeField, Min(0.05f)] private float deliveryRadius = 1.5f;

        [Header("Inventory")]
        [SerializeField] private InventoryContainer inventory = new();

        private CommandTarget commandTarget;

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

        public string ChestName => string.IsNullOrWhiteSpace(chestName) ? name : chestName.Trim();
        public CommandTarget CommandTarget => commandTarget ??= GetComponent<CommandTarget>();
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
            EnsureUniqueChestName();
            Container.EnsureInitialized();
            ConfigureCommandTarget();
        }

#if UNITY_EDITOR
        
#endif

        public bool CanAcceptItem(InventoryItemData item, int amount = 1)
        {
            return Container.CanAdd(item, amount);
        }

        public bool TryAddItem(InventoryItemData item, int amount = 1)
        {
            return Container.TryAdd(item, amount);
        }

        public bool TryRemoveFromSlot(int slotIndex, int amount = 1)
        {
            return Container.TryRemoveAt(slotIndex, amount);
        }

        public bool TryReceiveFromRobot(CollectingRobotInventory sourceInventory)
        {
            if (sourceInventory == null || !sourceInventory.HasAnyItems)
            {
                return false;
            }

            Dictionary<InventoryItemData, int> itemsToTransfer = new();

            foreach (RobotInventoryItemStack stack in sourceInventory.ItemStacks)
            {
                if (stack?.item == null || stack.amount <= 0)
                {
                    continue;
                }

                AddTransferAmount(itemsToTransfer, stack.item, stack.amount);
            }

            foreach (RobotResourceStack stack in sourceInventory.ResourceStacks)
            {
                if (stack == null || stack.amount <= 0 ||
                    !TryFindOreItemDefinition(stack.oreType, out OreItemSO oreItem))
                {
                    return false;
                }

                AddTransferAmount(itemsToTransfer, oreItem, stack.amount);
            }

            if (itemsToTransfer.Count == 0 || !CanAcceptAll(itemsToTransfer))
            {
                return false;
            }

            foreach (KeyValuePair<InventoryItemData, int> transfer in itemsToTransfer)
            {
                if (!Container.TryAdd(transfer.Key, transfer.Value))
                {
                    Debug.LogError(
                        $"{ChestName} failed to receive a robot inventory after capacity validation.",
                        this);
                    return false;
                }
            }

            sourceInventory.ClearAll();
            return true;
        }

        private bool CanAcceptAll(IReadOnlyDictionary<InventoryItemData, int> itemsToTransfer)
        {
            IReadOnlyList<InventorySlot> slots = Container.Slots;
            int emptySlotCount = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty)
                {
                    emptySlotCount++;
                }
            }

            int requiredEmptySlots = 0;
            int maxStackSize = Container.MaxStackSize;

            foreach (KeyValuePair<InventoryItemData, int> transfer in itemsToTransfer)
            {
                int remaining = transfer.Value;

                for (int i = 0; i < slots.Count && remaining > 0; i++)
                {
                    InventorySlot slot = slots[i];
                    if (slot == null || slot.IsEmpty || slot.Item != transfer.Key)
                    {
                        continue;
                    }

                    remaining -= Mathf.Max(0, maxStackSize - slot.Amount);
                }

                if (remaining > 0)
                {
                    requiredEmptySlots += Mathf.CeilToInt(remaining / (float)maxStackSize);
                    if (requiredEmptySlots > emptySlotCount)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ConfigureCommandTarget()
        {
            commandTarget ??= GetComponent<CommandTarget>();
            commandTarget ??= gameObject.AddComponent<CommandTarget>();
            commandTarget.Configure(
                ChestName,
                CommandTargetType.Storage,
                destination: deliveryPoint != null ? deliveryPoint : transform,
                radius: deliveryRadius);
        }

        private void EnsureUniqueChestName()
        {
            string requestedName = string.IsNullOrWhiteSpace(chestName)
                ? string.Empty
                : chestName.Trim();

            if (string.IsNullOrWhiteSpace(requestedName) ||
                string.Equals(requestedName, "Chest", StringComparison.OrdinalIgnoreCase) ||
                IsChestNameInUse(requestedName, this))
            {
                chestName = GenerateUniqueChestName(this);
                return;
            }

            chestName = requestedName;
        }

        private static string GenerateUniqueChestName(ChestInventory self)
        {
            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"Chest_{i}";
                if (!IsChestNameInUse(candidate, self))
                {
                    return candidate;
                }
            }

            return $"Chest_{Mathf.Abs(self.GetInstanceID())}";
        }

        private static bool IsChestNameInUse(
            string candidate,
            ChestInventory self)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            ChestInventory[] chests = FindObjectsByType<ChestInventory>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < chests.Length; i++)
            {
                ChestInventory chest = chests[i];
                if (chest == null ||
                    chest == self ||
                    !chest.isActiveAndEnabled ||
                    (chest.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                if (string.Equals(
                        chest.ChestName,
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            CommandTarget selfTarget = self != null
                ? self.GetComponent<CommandTarget>()
                : null;
            CommandTarget[] targets = FindObjectsByType<CommandTarget>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < targets.Length; i++)
            {
                CommandTarget target = targets[i];
                if (target == null ||
                    target == selfTarget ||
                    !target.isActiveAndEnabled ||
                    target.TargetType != CommandTargetType.Storage ||
                    !target.HasConfiguredTargetId ||
                    (target.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                if (string.Equals(
                        target.ConfiguredTargetId,
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddTransferAmount(
            IDictionary<InventoryItemData, int> transfer,
            InventoryItemData item,
            int amount)
        {
            transfer.TryGetValue(item, out int currentAmount);
            transfer[item] = currentAmount + amount;
        }

        private static bool TryFindOreItemDefinition(OreType oreType, out OreItemSO itemData)
        {
            OreItemSO[] loadedDefinitions = Resources.FindObjectsOfTypeAll<OreItemSO>();
            for (int i = 0; i < loadedDefinitions.Length; i++)
            {
                OreItemSO definition = loadedDefinitions[i];
                if (definition != null && definition.oreType == oreType)
                {
                    itemData = definition;
                    return true;
                }
            }

            itemData = null;
            return false;
        }
    }
}
