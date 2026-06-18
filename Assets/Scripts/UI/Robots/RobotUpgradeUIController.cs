using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

namespace Coreline.Robots
{
    [DisallowMultipleComponent]
    public sealed class RobotUpgradeUIController : MonoBehaviour
    {
        public const string UpgradesBodyName = "UpgradesBody";

        private const string UpgradeOptionsName = "UpgradeOptions";
        private const string UpgradeCostItemsRootName = "UpgradeCostItemsRoot";
        private const string HeadingName = "Heading";
        private const string DescriptionName = "Description";
        private const string UpgradeEffectTextName = "UpgradeEffectText";
        private const string UpgradeEffectPercentageName = "UpgradeEffectPercentage";
        private const string CostTextName = "CostText";
        private const string UpgradeButtonName = "UpgradeButton";
        private const string UpgradeButtonTextName = "UpgradeText";

        [SerializeField] private ListView upgradeOptions;
        [SerializeField] private ListView costItems;
        [SerializeField] private TextBlock heading;
        [SerializeField] private TextBlock description;
        [SerializeField] private TextBlock effectText;
        [SerializeField] private TextBlock effectPercentage;
        [SerializeField] private TextBlock costText;
        [SerializeField] private UIBlock2D upgradeButton;
        [SerializeField] private TextBlock upgradeButtonText;

        private readonly List<RobotUpgradeOptionViewData> optionData = new();
        private readonly List<RobotUpgradeCostViewData> costData = new();

        private BaseRobotController robot;
        private RobotUpgradeController upgradeController;
        private PlayerInventory playerInventory;
        private RobotUpgradeType? selectedUpgradeType;
        private bool handlersRegistered;
        private string purchaseStatus = string.Empty;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            RegisterHandlers();
        }

        private void OnDisable()
        {
            UnregisterHandlers();
            UnsubscribeFromData();
        }

        public void Bind(BaseRobotController robotContext, Player player)
        {
            UnsubscribeFromData();

            robot = robotContext;
            upgradeController = robot != null ? robot.Upgrades : null;
            playerInventory = player != null
                ? player.GetComponent<PlayerInventory>() ?? player.GetComponentInParent<PlayerInventory>()
                : null;
            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);

            if (upgradeController != null)
            {
                upgradeController.UpgradesChanged += HandleUpgradesChanged;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated += HandleInventoryUpdated;
            }

            IReadOnlyList<RobotUpgradeDefinition> definitions = upgradeController?.Definitions;
            if (definitions != null && definitions.Count > 0 &&
                (!selectedUpgradeType.HasValue || FindSelectedDefinition() == null))
            {
                selectedUpgradeType = definitions[0].Type;
            }

            purchaseStatus = string.Empty;
            RefreshAll();
        }

        public void Unbind()
        {
            UnsubscribeFromData();
            robot = null;
            upgradeController = null;
            playerInventory = null;
            selectedUpgradeType = null;
            purchaseStatus = string.Empty;
            optionData.Clear();
            costData.Clear();
        }

        public void RefreshAll()
        {
            EnsureReferences();
            RegisterHandlers();
            RefreshUpgradeOptions();
            RefreshDescription();
        }

        private void EnsureReferences()
        {
            Transform upgradesBody = FindChildRecursive(transform, UpgradesBodyName);
            if (upgradesBody == null)
            {
                return;
            }

            upgradeOptions ??= FindChildComponentByName<ListView>(upgradesBody, UpgradeOptionsName);
            costItems ??= FindChildComponentByName<ListView>(upgradesBody, UpgradeCostItemsRootName);
            heading ??= FindChildComponentByName<TextBlock>(upgradesBody, HeadingName);
            description ??= FindChildComponentByName<TextBlock>(upgradesBody, DescriptionName);
            effectText ??= FindChildComponentByName<TextBlock>(upgradesBody, UpgradeEffectTextName);
            effectPercentage ??=
                FindChildComponentByName<TextBlock>(upgradesBody, UpgradeEffectPercentageName);
            costText ??= FindChildComponentByName<TextBlock>(upgradesBody, CostTextName);
            upgradeButton ??= FindChildComponentByName<UIBlock2D>(upgradesBody, UpgradeButtonName);
            upgradeButtonText ??=
                FindChildComponentByName<TextBlock>(upgradesBody, UpgradeButtonTextName);
        }

        private void RegisterHandlers()
        {
            if (handlersRegistered || upgradeOptions == null || costItems == null)
            {
                return;
            }

            upgradeOptions.AddDataBinder<RobotUpgradeOptionViewData, UpgradeOptionVisuals>(
                BindUpgradeOption);
            upgradeOptions.AddGestureHandler<Gesture.OnHover, UpgradeOptionVisuals>(
                UpgradeOptionVisuals.HandleHover);
            upgradeOptions.AddGestureHandler<Gesture.OnUnhover, UpgradeOptionVisuals>(
                UpgradeOptionVisuals.HandleUnhover);
            upgradeOptions.AddGestureHandler<Gesture.OnPress, UpgradeOptionVisuals>(
                UpgradeOptionVisuals.HandlePress);
            upgradeOptions.AddGestureHandler<Gesture.OnRelease, UpgradeOptionVisuals>(
                UpgradeOptionVisuals.HandleRelease);
            upgradeOptions.AddGestureHandler<Gesture.OnClick, UpgradeOptionVisuals>(
                HandleUpgradeOptionClicked);

            costItems.AddDataBinder<RobotUpgradeCostViewData, CostItemVisuals>(BindCostItem);

            if (upgradeButton != null)
            {
                upgradeButton.AddGestureHandler<Gesture.OnClick>(HandleUpgradeButtonClicked);
            }

            handlersRegistered = true;
        }

        private void UnregisterHandlers()
        {
            if (!handlersRegistered)
            {
                return;
            }

            if (upgradeOptions != null)
            {
                upgradeOptions.RemoveDataBinder<RobotUpgradeOptionViewData, UpgradeOptionVisuals>(
                    BindUpgradeOption);
                upgradeOptions.RemoveGestureHandler<Gesture.OnHover, UpgradeOptionVisuals>(
                    UpgradeOptionVisuals.HandleHover);
                upgradeOptions.RemoveGestureHandler<Gesture.OnUnhover, UpgradeOptionVisuals>(
                    UpgradeOptionVisuals.HandleUnhover);
                upgradeOptions.RemoveGestureHandler<Gesture.OnPress, UpgradeOptionVisuals>(
                    UpgradeOptionVisuals.HandlePress);
                upgradeOptions.RemoveGestureHandler<Gesture.OnRelease, UpgradeOptionVisuals>(
                    UpgradeOptionVisuals.HandleRelease);
                upgradeOptions.RemoveGestureHandler<Gesture.OnClick, UpgradeOptionVisuals>(
                    HandleUpgradeOptionClicked);
            }

            if (costItems != null)
            {
                costItems.RemoveDataBinder<RobotUpgradeCostViewData, CostItemVisuals>(BindCostItem);
            }

            if (upgradeButton != null)
            {
                upgradeButton.RemoveGestureHandler<Gesture.OnClick>(HandleUpgradeButtonClicked);
            }

            handlersRegistered = false;
        }

        private void RefreshUpgradeOptions()
        {
            optionData.Clear();

            IReadOnlyList<RobotUpgradeDefinition> definitions = upgradeController?.Definitions;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    RobotUpgradeDefinition definition = definitions[i];
                    Sprite icon = definition.Icon ?? ResolveFallbackIcon(definition);
                    optionData.Add(new RobotUpgradeOptionViewData(
                        definition,
                        icon,
                        upgradeController.GetLevel(definition.Type),
                        selectedUpgradeType == definition.Type));
                }
            }

            if (upgradeOptions != null)
            {
                upgradeOptions.SetDataSource(optionData);
                if (upgradeOptions.gameObject.activeInHierarchy)
                {
                    upgradeOptions.Refresh();
                }
            }
        }

        private void RefreshDescription()
        {
            RobotUpgradeDefinition selected = FindSelectedDefinition();
            if (selected == null)
            {
                SetEmptyDescription();
                return;
            }

            int currentLevel = upgradeController.GetLevel(selected.Type);
            bool maxLevel = upgradeController.IsMaxLevel(selected);

            if (heading != null)
            {
                heading.Text = $"{selected.Title}  Level {currentLevel}/{selected.MaxLevel}";
            }

            if (description != null)
            {
                description.Text = selected.Description;
            }

            if (effectText != null)
            {
                effectText.Text = selected.EffectLabel;
            }

            if (effectPercentage != null)
            {
                effectPercentage.Text = $"+{selected.PercentageIncrease:0.#}%";
            }

            RefreshCosts(selected, currentLevel, maxLevel);
            RefreshUpgradeButton(selected, maxLevel);
        }

        private void RefreshCosts(
            RobotUpgradeDefinition selected,
            int currentLevel,
            bool maxLevel)
        {
            costData.Clear();

            if (!maxLevel)
            {
                for (int i = 0; i < selected.Costs.Count; i++)
                {
                    RobotUpgradeCost cost = selected.Costs[i];
                    if (cost == null)
                    {
                        continue;
                    }

                    int required =
                        cost.GetAmountForLevel(currentLevel, selected.CostMultiplierPerLevel);
                    int owned = playerInventory != null
                        ? playerInventory.GetOreCount(cost.OreType)
                        : 0;
                    OreItemSO item = upgradeController.ResolveCostItem(cost.OreType, playerInventory);
                    costData.Add(new RobotUpgradeCostViewData(item, owned, required));
                }
            }

            if (costText != null)
            {
                costText.Text = maxLevel ? "Maximum level reached" : "Cost";
            }

            if (costItems != null)
            {
                costItems.SetDataSource(costData);
                if (costItems.gameObject.activeInHierarchy)
                {
                    costItems.Refresh();
                }
            }
        }

        private void RefreshUpgradeButton(RobotUpgradeDefinition selected, bool maxLevel)
        {
            if (upgradeButtonText == null)
            {
                return;
            }

            if (maxLevel)
            {
                upgradeButtonText.Text = "Max Level";
                return;
            }

            if (!string.IsNullOrWhiteSpace(purchaseStatus))
            {
                upgradeButtonText.Text = purchaseStatus;
                return;
            }

            upgradeButtonText.Text =
                upgradeController.CanPurchase(selected, playerInventory)
                    ? "Upgrade"
                    : "Missing Materials";
        }

        private void SetEmptyDescription()
        {
            if (heading != null)
            {
                heading.Text = "No upgrade selected";
            }

            if (description != null)
            {
                description.Text = string.Empty;
            }

            if (effectText != null)
            {
                effectText.Text = string.Empty;
            }

            if (effectPercentage != null)
            {
                effectPercentage.Text = string.Empty;
            }

            if (costText != null)
            {
                costText.Text = string.Empty;
            }

            if (upgradeButtonText != null)
            {
                upgradeButtonText.Text = "Upgrade";
            }

            costData.Clear();
            costItems?.SetDataSource(costData);
        }

        private void BindUpgradeOption(
            Data.OnBind<RobotUpgradeOptionViewData> evt,
            UpgradeOptionVisuals target,
            int index)
        {
            target.Bind(evt.UserData);
        }

        private void BindCostItem(
            Data.OnBind<RobotUpgradeCostViewData> evt,
            CostItemVisuals target,
            int index)
        {
            target.Bind(evt.UserData);
        }

        private void HandleUpgradeOptionClicked(
            Gesture.OnClick evt,
            UpgradeOptionVisuals target,
            int index)
        {
            if (index < 0 || index >= optionData.Count)
            {
                return;
            }

            selectedUpgradeType = optionData[index].Definition.Type;
            purchaseStatus = string.Empty;
            RefreshAll();
            evt.Consume();
        }

        private void HandleUpgradeButtonClicked(Gesture.OnClick evt)
        {
            RobotUpgradeDefinition selected = FindSelectedDefinition();
            if (selected == null || upgradeController == null)
            {
                return;
            }

            if (upgradeController.TryPurchase(selected, playerInventory, out string failureReason))
            {
                purchaseStatus = "Upgraded";
            }
            else
            {
                purchaseStatus = upgradeController.IsMaxLevel(selected)
                    ? "Max Level"
                    : "Missing Materials";

                if (!string.IsNullOrWhiteSpace(failureReason))
                {
                    Debug.LogWarning($"[{robot?.name}] {failureReason}", robot);
                }
            }

            RefreshAll();
            evt.Consume();
        }

        private void HandleUpgradesChanged()
        {
            purchaseStatus = string.Empty;
            RefreshAll();
        }

        private void HandleInventoryUpdated()
        {
            purchaseStatus = string.Empty;
            RefreshDescription();
        }

        private RobotUpgradeDefinition FindSelectedDefinition()
        {
            if (!selectedUpgradeType.HasValue || upgradeController == null)
            {
                return null;
            }

            IReadOnlyList<RobotUpgradeDefinition> definitions = upgradeController.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Type == selectedUpgradeType.Value)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private Sprite ResolveFallbackIcon(RobotUpgradeDefinition definition)
        {
            if (definition == null || definition.Costs.Count == 0 || upgradeController == null)
            {
                return null;
            }

            RobotUpgradeCost firstCost = definition.Costs[0];
            OreItemSO item = firstCost != null
                ? upgradeController.ResolveCostItem(firstCost.OreType, playerInventory)
                : null;
            return item != null ? item.itemDesc?.Icon : null;
        }

        private void UnsubscribeFromData()
        {
            if (upgradeController != null)
            {
                upgradeController.UpgradesChanged -= HandleUpgradesChanged;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated -= HandleInventoryUpdated;
            }
        }

        private static T FindChildComponentByName<T>(Transform root, string objectName)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (string.Equals(
                        components[i].name,
                        objectName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return components[i];
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
