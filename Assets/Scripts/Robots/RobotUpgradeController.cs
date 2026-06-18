using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    public enum RobotUpgradeType
    {
        WalkingSpeed,
        MiningSpeed,
        CarryingCapacity
    }

    public enum RobotUpgradeEffectValueType
    {
        Percentage,
        Fixed
    }

    [Serializable]
    public sealed class RobotUpgradeCost
    {
        [SerializeField] private OreType oreType = OreType.Iron;
        [SerializeField, Min(1)] private int baseAmount = 5;

        public OreType OreType => oreType;
        public int BaseAmount => Mathf.Max(1, baseAmount);

        public RobotUpgradeCost()
        {
        }

        public RobotUpgradeCost(OreType oreType, int baseAmount)
        {
            this.oreType = oreType;
            this.baseAmount = Mathf.Max(1, baseAmount);
        }

        public int GetAmountForLevel(int currentLevel, float costMultiplierPerLevel)
        {
            float scaledAmount = BaseAmount *
                                 Mathf.Pow(Mathf.Max(1f, costMultiplierPerLevel), Mathf.Max(0, currentLevel));
            return Mathf.Max(1, Mathf.CeilToInt(scaledAmount));
        }
    }

    public sealed class RobotUpgradeDefinition
    {
        public RobotUpgradeType Type { get; }
        public string Title { get; }
        public string Description { get; }
        public string EffectLabel { get; }
        public Sprite Icon { get; }
        public RobotUpgradeEffectValueType EffectValueType { get; }
        public float EffectIncrease { get; }
        public string EffectUnit { get; }
        public int MaxLevel { get; }
        public float CostMultiplierPerLevel { get; }
        public IReadOnlyList<RobotUpgradeCost> Costs { get; }

        public RobotUpgradeDefinition(
            RobotUpgradeType type,
            string title,
            string description,
            string effectLabel,
            Sprite icon,
            RobotUpgradeEffectValueType effectValueType,
            float effectIncrease,
            string effectUnit,
            int maxLevel,
            float costMultiplierPerLevel,
            IReadOnlyList<RobotUpgradeCost> costs)
        {
            Type = type;
            Title = title;
            Description = description;
            EffectLabel = effectLabel;
            Icon = icon;
            EffectValueType = effectValueType;
            EffectIncrease = Mathf.Max(0f, effectIncrease);
            EffectUnit = effectUnit ?? string.Empty;
            MaxLevel = Mathf.Max(1, maxLevel);
            CostMultiplierPerLevel = Mathf.Max(1f, costMultiplierPerLevel);
            Costs = costs ?? Array.Empty<RobotUpgradeCost>();
        }

        public string GetFormattedEffectIncrease()
        {
            return EffectValueType == RobotUpgradeEffectValueType.Percentage
                ? $"+{EffectIncrease:0.#}%"
                : $"+{EffectIncrease:0.#}{(string.IsNullOrWhiteSpace(EffectUnit) ? string.Empty : $" {EffectUnit}")}";
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BaseRobotController))]
    public sealed class RobotUpgradeController : MonoBehaviour
    {
        [Header("Walking Speed")]
        [SerializeField] private Sprite walkingSpeedIcon;
        [SerializeField, Min(0f)] private float walkingSpeedIncreasePercent = 10f;
        [SerializeField, Min(1)] private int walkingSpeedMaxLevel = 5;
        [SerializeField, Min(1f)] private float walkingSpeedCostMultiplier = 1.5f;
        [SerializeField] private List<RobotUpgradeCost> walkingSpeedCosts = new()
        {
            new RobotUpgradeCost(OreType.Iron, 5)
        };

        [Header("Mining Speed")]
        [SerializeField] private Sprite miningSpeedIcon;
        [SerializeField, Min(0f)] private float miningSpeedIncreasePercent = 10f;
        [SerializeField, Min(1)] private int miningSpeedMaxLevel = 5;
        [SerializeField, Min(1f)] private float miningSpeedCostMultiplier = 1.5f;
        [SerializeField] private List<RobotUpgradeCost> miningSpeedCosts = new()
        {
            new RobotUpgradeCost(OreType.Coal, 10),
            new RobotUpgradeCost(OreType.Iron, 5)
        };

        [Header("Carrying Capacity")]
        [SerializeField] private Sprite carryingCapacityIcon;
        [SerializeField, Min(0)] private int carryingCapacityIncreaseSlots = 2;
        [SerializeField, Min(1)] private int carryingCapacityMaxLevel = 5;
        [SerializeField, Min(1f)] private float carryingCapacityCostMultiplier = 1.5f;
        [SerializeField] private List<RobotUpgradeCost> carryingCapacityCosts = new()
        {
            new RobotUpgradeCost(OreType.Iron, 10)
        };

        [Header("Current Levels")]
        [SerializeField, Min(0)] private int walkingSpeedLevel;
        [SerializeField, Min(0)] private int miningSpeedLevel;
        [SerializeField, Min(0)] private int carryingCapacityLevel;

        private readonly List<RobotUpgradeDefinition> definitions = new();
        private BaseRobotController robot;
        private MiningRobotCommandExecutor miningExecutor;
        private CollectingRobotInventory collectingInventory;

        public event Action UpgradesChanged;

        public IReadOnlyList<RobotUpgradeDefinition> Definitions
        {
            get
            {
                EnsureInitialized();
                return definitions;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            ApplyAllUpgrades();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            walkingSpeedMaxLevel = Mathf.Max(1, walkingSpeedMaxLevel);
            miningSpeedMaxLevel = Mathf.Max(1, miningSpeedMaxLevel);
            carryingCapacityMaxLevel = Mathf.Max(1, carryingCapacityMaxLevel);
            carryingCapacityIncreaseSlots = Mathf.Max(0, carryingCapacityIncreaseSlots);

            walkingSpeedLevel = Mathf.Clamp(walkingSpeedLevel, 0, walkingSpeedMaxLevel);
            miningSpeedLevel = Mathf.Clamp(miningSpeedLevel, 0, miningSpeedMaxLevel);
            carryingCapacityLevel = Mathf.Clamp(carryingCapacityLevel, 0, carryingCapacityMaxLevel);

            definitions.Clear();
        }
#endif

        public int GetLevel(RobotUpgradeType type)
        {
            return type switch
            {
                RobotUpgradeType.WalkingSpeed => walkingSpeedLevel,
                RobotUpgradeType.MiningSpeed => miningSpeedLevel,
                RobotUpgradeType.CarryingCapacity => carryingCapacityLevel,
                _ => 0
            };
        }

        public bool IsMaxLevel(RobotUpgradeDefinition definition)
        {
            return definition == null || GetLevel(definition.Type) >= definition.MaxLevel;
        }

        public bool CanPurchase(RobotUpgradeDefinition definition, PlayerInventory inventory)
        {
            if (definition == null || inventory == null || IsMaxLevel(definition))
            {
                return false;
            }

            int currentLevel = GetLevel(definition.Type);
            foreach (RobotUpgradeCost cost in definition.Costs)
            {
                if (cost == null)
                {
                    continue;
                }

                int requiredAmount = cost.GetAmountForLevel(currentLevel, definition.CostMultiplierPerLevel);
                if (!inventory.HasOre(cost.OreType, requiredAmount))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryPurchase(
            RobotUpgradeDefinition definition,
            PlayerInventory inventory,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (definition == null)
            {
                failureReason = "No upgrade is selected.";
                return false;
            }

            if (inventory == null)
            {
                failureReason = "No player inventory was found.";
                return false;
            }

            if (IsMaxLevel(definition))
            {
                failureReason = "This upgrade is already at its maximum level.";
                return false;
            }

            if (!CanPurchase(definition, inventory))
            {
                failureReason = "The player does not have the required materials.";
                return false;
            }

            int currentLevel = GetLevel(definition.Type);
            foreach (RobotUpgradeCost cost in definition.Costs)
            {
                if (cost == null)
                {
                    continue;
                }

                int requiredAmount = cost.GetAmountForLevel(currentLevel, definition.CostMultiplierPerLevel);
                if (!inventory.TryRemoveOre(cost.OreType, requiredAmount))
                {
                    failureReason = "The upgrade materials could not be removed.";
                    return false;
                }
            }

            SetLevel(definition.Type, currentLevel + 1);
            ApplyUpgrade(definition);
            UpgradesChanged?.Invoke();
            return true;
        }

        public OreItemSO ResolveCostItem(OreType oreType, PlayerInventory inventory = null)
        {
            if (inventory != null)
            {
                IReadOnlyList<InventorySlot> slots = inventory.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i]?.Item is OreItemSO inventoryOre && inventoryOre.oreType == oreType)
                    {
                        return inventoryOre;
                    }
                }
            }

            OreItemSO[] loadedItems = Resources.FindObjectsOfTypeAll<OreItemSO>();
            for (int i = 0; i < loadedItems.Length; i++)
            {
                if (loadedItems[i] != null && loadedItems[i].oreType == oreType)
                {
                    return loadedItems[i];
                }
            }

            return null;
        }

        private void EnsureInitialized()
        {
            robot ??= GetComponent<BaseRobotController>();
            miningExecutor ??= GetComponent<MiningRobotCommandExecutor>();
            collectingInventory ??= GetComponent<CollectingRobotInventory>();

            if (definitions.Count > 0 || robot == null)
            {
                return;
            }

            definitions.Add(new RobotUpgradeDefinition(
                RobotUpgradeType.WalkingSpeed,
                "Walking Speed",
                "Increases this robot's movement speed.",
                "Walking Speed: ",
                walkingSpeedIcon,
                RobotUpgradeEffectValueType.Percentage,
                walkingSpeedIncreasePercent,
                string.Empty,
                walkingSpeedMaxLevel,
                walkingSpeedCostMultiplier,
                walkingSpeedCosts));

            if (robot is MiningRobotController)
            {
                definitions.Add(new RobotUpgradeDefinition(
                    RobotUpgradeType.MiningSpeed,
                    "Mining Speed",
                    "Reduces the delay between mining hits.",
                    "Mining Speed: ",
                    miningSpeedIcon,
                    RobotUpgradeEffectValueType.Percentage,
                    miningSpeedIncreasePercent,
                    string.Empty,
                    miningSpeedMaxLevel,
                    miningSpeedCostMultiplier,
                    miningSpeedCosts));
            }
            else if (robot is CollectingRobotController)
            {
                definitions.Add(new RobotUpgradeDefinition(
                    RobotUpgradeType.CarryingCapacity,
                    "Carrying Capacity",
                    "Adds inventory stack slots to this robot.",
                    "Carrying Capacity: ",
                    carryingCapacityIcon,
                    RobotUpgradeEffectValueType.Fixed,
                    carryingCapacityIncreaseSlots,
                    "slots",
                    carryingCapacityMaxLevel,
                    carryingCapacityCostMultiplier,
                    carryingCapacityCosts));
            }
        }

        private void SetLevel(RobotUpgradeType type, int level)
        {
            RobotUpgradeDefinition definition = FindDefinition(type);
            int clampedLevel = Mathf.Clamp(level, 0, definition?.MaxLevel ?? 0);

            switch (type)
            {
                case RobotUpgradeType.WalkingSpeed:
                    walkingSpeedLevel = clampedLevel;
                    break;
                case RobotUpgradeType.MiningSpeed:
                    miningSpeedLevel = clampedLevel;
                    break;
                case RobotUpgradeType.CarryingCapacity:
                    carryingCapacityLevel = clampedLevel;
                    break;
            }
        }

        private void ApplyAllUpgrades()
        {
            EnsureInitialized();
            for (int i = 0; i < definitions.Count; i++)
            {
                ApplyUpgrade(definitions[i]);
            }
        }

        private void ApplyUpgrade(RobotUpgradeDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            int level = GetLevel(definition.Type);
            switch (definition.Type)
            {
                case RobotUpgradeType.WalkingSpeed:
                    float walkingMultiplier =
                        1f + level * definition.EffectIncrease / 100f;
                    robot?.SetWalkingSpeedMultiplier(walkingMultiplier);
                    break;
                case RobotUpgradeType.MiningSpeed:
                    float miningMultiplier =
                        1f + level * definition.EffectIncrease / 100f;
                    miningExecutor?.SetMiningSpeedMultiplier(miningMultiplier);
                    break;
                case RobotUpgradeType.CarryingCapacity:
                    int additionalSlots =
                        Mathf.RoundToInt(level * definition.EffectIncrease);
                    collectingInventory?.SetCapacityBonusSlots(additionalSlots);
                    break;
            }
        }

        private RobotUpgradeDefinition FindDefinition(RobotUpgradeType type)
        {
            EnsureInitialized();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Type == type)
                {
                    return definitions[i];
                }
            }

            return null;
        }
    }
}
