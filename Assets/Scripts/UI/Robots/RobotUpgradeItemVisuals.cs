using System;
using Nova;
using UnityEngine;

namespace Coreline.Robots
{
    public sealed class RobotUpgradeOptionViewData
    {
        public RobotUpgradeDefinition Definition { get; }
        public Sprite Icon { get; }
        public int CurrentLevel { get; }
        public bool IsSelected { get; }

        public RobotUpgradeOptionViewData(
            RobotUpgradeDefinition definition,
            Sprite icon,
            int currentLevel,
            bool isSelected)
        {
            Definition = definition;
            Icon = icon;
            CurrentLevel = currentLevel;
            IsSelected = isSelected;
        }
    }

    public sealed class RobotUpgradeCostViewData
    {
        public OreItemSO Item { get; }
        public int OwnedAmount { get; }
        public int RequiredAmount { get; }

        public bool HasEnough => OwnedAmount >= RequiredAmount;

        public RobotUpgradeCostViewData(OreItemSO item, int ownedAmount, int requiredAmount)
        {
            Item = item;
            OwnedAmount = Mathf.Max(0, ownedAmount);
            RequiredAmount = Mathf.Max(1, requiredAmount);
        }
    }

    [Serializable]
    public sealed class UpgradeOptionVisuals : ItemVisuals
    {
        public UIBlock2D Root;
        public UIBlock2D Icon;
        public TextBlock Title;
        public Color DefaultColor = new(0.215f, 0.215f, 0.215f, 1f);
        public Color HoverColor = new(0.3f, 0.3f, 0.3f, 1f);
        public Color PressedColor = new(0.15f, 0.15f, 0.15f, 1f);
        public Color SelectedColor = new(0.32f, 0.42f, 0.52f, 1f);

        private bool isSelected;

        public void Bind(RobotUpgradeOptionViewData data)
        {
            if (data == null || data.Definition == null)
            {
                return;
            }

            isSelected = data.IsSelected;

            if (Title != null)
            {
                Title.Text =
                    $"{data.Definition.Title}  {data.CurrentLevel}/{data.Definition.MaxLevel}";
            }

            if (Icon != null && data.Icon != null)
            {
                Icon.SetImage(data.Icon);
            }

            ApplyDefaultColor();
        }

        public void ApplyDefaultColor()
        {
            if (Root != null)
            {
                Root.Color = isSelected ? SelectedColor : DefaultColor;
            }
        }

        internal static void HandleHover(
            Gesture.OnHover evt,
            UpgradeOptionVisuals target,
            int index)
        {
            if (target.Root != null)
            {
                target.Root.Color = target.HoverColor;
            }
        }

        internal static void HandleUnhover(
            Gesture.OnUnhover evt,
            UpgradeOptionVisuals target,
            int index)
        {
            target.ApplyDefaultColor();
        }

        internal static void HandlePress(
            Gesture.OnPress evt,
            UpgradeOptionVisuals target,
            int index)
        {
            if (target.Root != null)
            {
                target.Root.Color = target.PressedColor;
            }
        }

        internal static void HandleRelease(
            Gesture.OnRelease evt,
            UpgradeOptionVisuals target,
            int index)
        {
            if (target.Root != null)
            {
                target.Root.Color = target.HoverColor;
            }
        }
    }

    [Serializable]
    public sealed class CostItemVisuals : ItemVisuals
    {
        public UIBlock2D Icon;
        public TextBlock PlayerAmountText;
        public TextBlock CostAmountText;
        public Color EnoughColor = Color.white;
        public Color MissingColor = new(0.764f, 0.22f, 0.22f, 1f);

        public void Bind(RobotUpgradeCostViewData data)
        {
            if (data == null)
            {
                return;
            }

            if (Icon != null && data.Item != null && data.Item.itemDesc?.Icon != null)
            {
                Icon.SetImage(data.Item.itemDesc.Icon);
            }

            if (PlayerAmountText != null)
            {
                PlayerAmountText.Text = data.OwnedAmount.ToString();
                PlayerAmountText.Color = data.HasEnough ? EnoughColor : MissingColor;
            }

            if (CostAmountText != null)
            {
                CostAmountText.Text = data.RequiredAmount.ToString();
            }
        }
    }
}
