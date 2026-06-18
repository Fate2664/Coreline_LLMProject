using System;
using System.Collections.Generic;
using Nova;
using NovaSamples.UIControls;
using UnityEngine;

namespace Coreline.Robots
{
    public class CollectingRobotChatUIController : RobotChatUIController
    {
        private const string InventoryBodyName = "InventoryBody";
        private const string ChestDropdownRootName = "ChestToDeliverRoot";
        private const string ChestDropdownName = "DropDownSetting";
        private const string NoChestSelection = "None";

        [SerializeField]
        private CollectingRobotInventoryUIController inventoryUI;
        [SerializeField] private ItemView chestDropdownView;

        private readonly MultiOptionSetting chestDropdownSetting = new()
        {
            Key = "collecting_robot_selected_chest",
            Name = "Chest"
        };

        private readonly List<global::Coreline.ChestInventory> chestDropdownChests = new();
        private DropDownVisuals chestDropdownVisuals;
        private bool isChestDropdownSubscribed;

        protected override string RobotTypeLabel => "Collection Robot";
        protected override bool ShowsMiningRobotDropdown => true;

        protected override bool CanOpenForRobot(BaseRobotController robot)
        {
            return robot is CollectingRobotController;
        }

        protected override void OnOpenedForRobot(BaseRobotController robot, Coreline.Player player)
        {
            inventoryUI ??= GetComponent<CollectingRobotInventoryUIController>();
            inventoryUI ??= GetComponentInChildren<CollectingRobotInventoryUIController>(true);
            inventoryUI?.Bind(robot as CollectingRobotController, player);

            EnsureChestDropdownReferences();
            SubscribeToChestDropdown();
            RefreshChestDropdown(robot as CollectingRobotController);
        }

        protected override void OnClosing()
        {
            UnsubscribeFromChestDropdown();
            chestDropdownVisuals?.Collapse();
            inventoryUI?.Unbind();
        }

        protected override void OnTabSelected(string bodyName)
        {
            if (bodyName == InventoryBodyName)
            {
                inventoryUI?.RefreshAll();
            }
        }

        private void EnsureChestDropdownReferences()
        {
            if (chestDropdownView == null)
            {
                Transform dropdownRoot = FindChildRecursive(transform, ChestDropdownRootName);
                if (dropdownRoot != null)
                {
                    ItemView[] views = dropdownRoot.GetComponentsInChildren<ItemView>(true);
                    for (int i = 0; i < views.Length; i++)
                    {
                        ItemView view = views[i];
                        if (view == null || !view.TryGetVisuals(out DropDownVisuals _))
                        {
                            continue;
                        }

                        if (view.name == ChestDropdownName)
                        {
                            chestDropdownView = view;
                            break;
                        }

                        chestDropdownView ??= view;
                    }
                }
            }

            if (chestDropdownView != null)
            {
                chestDropdownView.TryGetVisuals(out chestDropdownVisuals);
            }
        }

        private void RefreshChestDropdown(CollectingRobotController collectingRobot)
        {
            EnsureChestDropdownReferences();
            if (collectingRobot == null ||
                chestDropdownView == null ||
                chestDropdownVisuals == null)
            {
                return;
            }

            chestDropdownChests.Clear();
            chestDropdownChests.Add(null);

            global::Coreline.ChestInventory[] sceneChests =
                FindObjectsByType<global::Coreline.ChestInventory>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            List<global::Coreline.ChestInventory> sortedChests = new(sceneChests);
            sortedChests.RemoveAll(chest =>
                chest == null ||
                !chest.isActiveAndEnabled ||
                chest.CommandTarget == null ||
                chest.CommandTarget.TargetType != CommandTargetType.Storage);
            sortedChests.Sort((left, right) =>
                string.Compare(
                    GetChestDropdownLabel(left),
                    GetChestDropdownLabel(right),
                    StringComparison.OrdinalIgnoreCase));

            int selectedIndex = 0;
            if (collectingRobot.SelectedDeliveryChest != null)
            {
                int existingIndex = sortedChests.IndexOf(collectingRobot.SelectedDeliveryChest);
                selectedIndex = existingIndex >= 0 ? existingIndex + 1 : 0;
            }

            string[] options = new string[sortedChests.Count + 1];
            options[0] = NoChestSelection;

            for (int i = 0; i < sortedChests.Count; i++)
            {
                global::Coreline.ChestInventory chest = sortedChests[i];
                chestDropdownChests.Add(chest);
                options[i + 1] = GetChestDropdownLabel(chest);
            }

            chestDropdownSetting.SetOptions(options, selectedIndex);
            collectingRobot.SetSelectedDeliveryChest(
                chestDropdownChests[chestDropdownSetting.SelectedIndex]);
            chestDropdownVisuals.Refresh(chestDropdownSetting);
        }

        private static string GetChestDropdownLabel(global::Coreline.ChestInventory chest)
        {
            if (chest == null)
            {
                return string.Empty;
            }

            return chest.CommandTarget != null &&
                   !string.IsNullOrWhiteSpace(chest.CommandTarget.TargetId)
                ? chest.CommandTarget.TargetId
                : chest.ChestName;
        }

        private void SubscribeToChestDropdown()
        {
            if (isChestDropdownSubscribed)
            {
                return;
            }

            EnsureChestDropdownReferences();
            if (chestDropdownView == null || chestDropdownVisuals == null)
            {
                return;
            }

            chestDropdownView.UIBlock.AddGestureHandler<Gesture.OnHover, DropDownVisuals>(
                DropDownVisuals.HandleHover);
            chestDropdownView.UIBlock.AddGestureHandler<Gesture.OnUnhover, DropDownVisuals>(
                DropDownVisuals.HandleUnhover);
            chestDropdownView.UIBlock.AddGestureHandler<Gesture.OnPress, DropDownVisuals>(
                DropDownVisuals.HandlePress);
            chestDropdownView.UIBlock.AddGestureHandler<Gesture.OnRelease, DropDownVisuals>(
                DropDownVisuals.HandleRelease);
            chestDropdownView.UIBlock.AddGestureHandler<Gesture.OnClick, DropDownVisuals>(
                HandleChestDropdownClicked);

            chestDropdownVisuals.OnSelectionChanged += HandleChestDropdownSelectionChanged;
            InputManager.OnPostClick += HandleChestDropdownPostClick;
            isChestDropdownSubscribed = true;
        }

        private void UnsubscribeFromChestDropdown()
        {
            if (!isChestDropdownSubscribed || chestDropdownView == null)
            {
                return;
            }

            chestDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnHover, DropDownVisuals>(
                DropDownVisuals.HandleHover);
            chestDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, DropDownVisuals>(
                DropDownVisuals.HandleUnhover);
            chestDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnPress, DropDownVisuals>(
                DropDownVisuals.HandlePress);
            chestDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnRelease, DropDownVisuals>(
                DropDownVisuals.HandleRelease);
            chestDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnClick, DropDownVisuals>(
                HandleChestDropdownClicked);

            if (chestDropdownVisuals != null)
            {
                chestDropdownVisuals.OnSelectionChanged -= HandleChestDropdownSelectionChanged;
            }

            InputManager.OnPostClick -= HandleChestDropdownPostClick;
            isChestDropdownSubscribed = false;
        }

        private void HandleChestDropdownClicked(Gesture.OnClick evt, DropDownVisuals target)
        {
            if (target.ExpandedRoot != null &&
                evt.Receiver != null &&
                evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
            {
                return;
            }

            if (ActiveRobot is not CollectingRobotController collectingRobot)
            {
                return;
            }

            RefreshChestDropdown(collectingRobot);

            if (target.isExpanded)
            {
                target.Collapse();
            }
            else
            {
                target.Expand(chestDropdownSetting);
            }

            evt.Consume();
        }

        private void HandleChestDropdownSelectionChanged(int selectedIndex, string selectedLabel)
        {
            if (ActiveRobot is not CollectingRobotController collectingRobot)
            {
                return;
            }

            global::Coreline.ChestInventory selectedChest =
                selectedIndex >= 0 && selectedIndex < chestDropdownChests.Count
                    ? chestDropdownChests[selectedIndex]
                    : null;

            collectingRobot.SetSelectedDeliveryChest(selectedChest);
        }

        private void HandleChestDropdownPostClick(UIBlock clickedUIBlock)
        {
            if (chestDropdownVisuals == null || !chestDropdownVisuals.isExpanded)
            {
                return;
            }

            if (clickedUIBlock == null ||
                chestDropdownView == null ||
                !clickedUIBlock.transform.IsChildOf(chestDropdownView.transform))
            {
                chestDropdownVisuals.Collapse();
            }
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
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

        public new static CollectingRobotChatUIController FindOrCreateInScene()
        {
            return FindOrCreateInScene<CollectingRobotChatUIController>();
        }
    }
}
