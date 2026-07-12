using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    [Serializable]
    public sealed class FurnaceRecipe
    {
        [SerializeField] private InventoryItemData input;
        [SerializeField] private InventoryItemData output;
        [SerializeField, Min(1)] private int inputAmount = 1;
        [SerializeField, Min(1)] private int outputAmount = 1;

        public InventoryItemData Input => input;
        public InventoryItemData Output => output;
        public int InputAmount => Mathf.Max(1, inputAmount);
        public int OutputAmount => Mathf.Max(1, outputAmount);
        public bool IsValid => input != null && output != null;

        public bool Matches(InventoryItemData item)
        {
            return IsValid && item == input;
        }
    }

    [DisallowMultipleComponent]
    public sealed class FurnaceController : MonoBehaviour
    {
        [Header("Items and Recipes")]
        [SerializeField] private InventoryItemData coalItem;
        [SerializeField] private List<FurnaceRecipe> recipes = new();

        [Header("Slots")]
        [SerializeField] private InventoryContainer fuelInventory = new();
        [SerializeField] private InventoryContainer inputInventory = new();
        [SerializeField] private InventoryContainer outputInventory = new();

        [Header("Processing")]
        [SerializeField, Min(0.1f)] private float processingDuration = 5f;
        [SerializeField] private GameObject particleEffects;

        private float processingTime;
        private bool isProcessing;

        public event Action StateChanged;
        public event Action<float> ProgressChanged;

        public InventorySlot FuelSlot => GetFirstSlot(FuelContainer);
        public InventorySlot InputSlot => GetFirstSlot(InputContainer);
        public InventorySlot OutputSlot => GetFirstSlot(OutputContainer);
        public float Progress01 => Mathf.Clamp01(processingTime / Mathf.Max(0.1f, processingDuration));
        public bool IsProcessing => isProcessing;
        public InventoryItemData CoalItem => coalItem;

        private InventoryContainer FuelContainer => EnsureSingleSlotContainer(ref fuelInventory);
        private InventoryContainer InputContainer => EnsureSingleSlotContainer(ref inputInventory);
        private InventoryContainer OutputContainer => EnsureSingleSlotContainer(ref outputInventory);

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToContainers();
            RefreshProcessingState();
        }

        private void OnDisable()
        {
            UnsubscribeFromContainers();
            SetProcessing(false);
        }

        private void Update()
        {
            if (!TryGetRunnableRecipe(out FurnaceRecipe recipe))
            {
                SetProcessing(false);
                return;
            }

            SetProcessing(true);
            float duration = Mathf.Max(0.1f, processingDuration);
            processingTime += Time.deltaTime;
            NotifyProgressChanged();

            while (processingTime >= duration && TryGetRunnableRecipe(out recipe))
            {
                processingTime -= duration;
                if (!CompleteCycle(recipe))
                {
                    processingTime = Mathf.Min(processingTime, duration);
                    break;
                }
            }

            RefreshProcessingState();
        }

        public bool CanAcceptFuel(InventoryItemData item, int amount = 1)
        {
            return item != null &&
                   item == coalItem &&
                   amount > 0 &&
                   FuelContainer.CanAdd(item, amount);
        }

        public bool TryAddFuel(InventoryItemData item, int amount = 1)
        {
            return CanAcceptFuel(item, amount) && FuelContainer.TryAdd(item, amount);
        }

        public bool CanAcceptInput(InventoryItemData item, int amount = 1)
        {
            return FindRecipe(item) != null &&
                   amount > 0 &&
                   InputContainer.CanAdd(item, amount);
        }

        public bool TryAddInput(InventoryItemData item, int amount = 1)
        {
            return CanAcceptInput(item, amount) && InputContainer.TryAdd(item, amount);
        }

        public bool TryRemoveFuel(int amount)
        {
            return FuelContainer.TryRemoveAt(0, amount);
        }

        public bool TryRemoveInput(int amount)
        {
            return InputContainer.TryRemoveAt(0, amount);
        }

        public bool TryRemoveOutput(int amount)
        {
            return OutputContainer.TryRemoveAt(0, amount);
        }

        private void Initialize()
        {
            FuelContainer.EnsureInitialized();
            InputContainer.EnsureInitialized();
            OutputContainer.EnsureInitialized();
            SetParticleEffects(false);
            NotifyProgressChanged();
        }

        private bool TryGetRunnableRecipe(out FurnaceRecipe recipe)
        {
            recipe = FindRecipe(InputSlot.Item);
            return recipe != null &&
                   FuelSlot.Item == coalItem &&
                   FuelSlot.Amount >= 1 &&
                   InputSlot.Amount >= recipe.InputAmount &&
                   OutputContainer.CanAdd(recipe.Output, recipe.OutputAmount);
        }

        private FurnaceRecipe FindRecipe(InventoryItemData input)
        {
            if (input == null)
            {
                return null;
            }

            for (int i = 0; i < recipes.Count; i++)
            {
                FurnaceRecipe recipe = recipes[i];
                if (recipe != null && recipe.Matches(input))
                {
                    return recipe;
                }
            }

            return null;
        }

        private bool CompleteCycle(FurnaceRecipe recipe)
        {
            if (recipe == null || !TryGetRunnableRecipe(out FurnaceRecipe runnable) || runnable != recipe)
            {
                return false;
            }

            if (!FuelContainer.TryRemoveAt(0, 1))
            {
                return false;
            }

            if (!InputContainer.TryRemoveAt(0, recipe.InputAmount))
            {
                FuelContainer.TryAdd(coalItem, 1);
                return false;
            }

            if (!OutputContainer.TryAdd(recipe.Output, recipe.OutputAmount))
            {
                FuelContainer.TryAdd(coalItem, 1);
                InputContainer.TryAdd(recipe.Input, recipe.InputAmount);
                return false;
            }

            StateChanged?.Invoke();
            return true;
        }

        private void HandleContainerChanged()
        {
            if (InputSlot.IsEmpty || FindRecipe(InputSlot.Item) == null)
            {
                ResetProgress();
            }

            RefreshProcessingState();
            StateChanged?.Invoke();
        }

        private void RefreshProcessingState()
        {
            bool canProcess = TryGetRunnableRecipe(out _);
            SetProcessing(canProcess);
        }

        private void SetProcessing(bool value)
        {
            if (isProcessing == value)
            {
                SetParticleEffects(value);
                return;
            }

            isProcessing = value;
            SetParticleEffects(value);
            StateChanged?.Invoke();
        }

        private void ResetProgress()
        {
            if (processingTime <= 0f)
            {
                return;
            }

            processingTime = 0f;
            NotifyProgressChanged();
        }

        private void NotifyProgressChanged()
        {
            ProgressChanged?.Invoke(Progress01);
        }

        private void SetParticleEffects(bool active)
        {
            if (particleEffects != null && particleEffects.activeSelf != active)
            {
                particleEffects.SetActive(active);
            }
        }

        private void SubscribeToContainers()
        {
            UnsubscribeFromContainers();
            FuelContainer.Changed += HandleContainerChanged;
            InputContainer.Changed += HandleContainerChanged;
            OutputContainer.Changed += HandleContainerChanged;
        }

        private void UnsubscribeFromContainers()
        {
            if (fuelInventory != null)
            {
                fuelInventory.Changed -= HandleContainerChanged;
            }

            if (inputInventory != null)
            {
                inputInventory.Changed -= HandleContainerChanged;
            }

            if (outputInventory != null)
            {
                outputInventory.Changed -= HandleContainerChanged;
            }
        }

        private static InventoryContainer EnsureSingleSlotContainer(ref InventoryContainer container)
        {
            container ??= new InventoryContainer();
            container.EnsureInitialized();
            container.TryResize(1);
            return container;
        }

        private static InventorySlot GetFirstSlot(InventoryContainer container)
        {
            IReadOnlyList<InventorySlot> slots = container.Slots;
            return slots[0];
        }
    }
}
