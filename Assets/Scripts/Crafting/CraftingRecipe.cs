using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    [Serializable]
    public sealed class CraftingIngredient
    {
        [SerializeField] private InventoryItemData item;
        [SerializeField, Min(1)] private int amount = 1;

        public InventoryItemData Item => item;
        public int Amount => Mathf.Max(1, amount);
        public bool IsValid => item != null && amount > 0;
    }

    public abstract class CraftingRecipe : InventoryItemData
    {
        [Header("Recipe")]
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private List<CraftingIngredient> requirements = new();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public IReadOnlyList<CraftingIngredient> Requirements => requirements;

        public virtual InventoryItemData OutputItem => this;
        public virtual int OutputAmount => 1;

        public InventoryItemData CraftedItem => OutputItem;
        public int CraftedAmount => OutputAmount;

        protected virtual void OnEnable()
        {
            SyncInventoryDescription();
        }

        private void SyncInventoryDescription()
        {
            itemDesc ??= new ItemDescription();
            itemDesc.Name = DisplayName;
            itemDesc.ToolTip = description;
            itemDesc.Icon = icon;
        }
    }

    [CreateAssetMenu(menuName = "Crafting/Item Recipe")]
    public sealed class ItemCraftingRecipe : CraftingRecipe
    {
        [Header("Output")]
        [SerializeField] private InventoryItemData craftedItem;
        [SerializeField, Min(1)] private int craftedAmount = 1;

        public override InventoryItemData OutputItem => craftedItem;
        public override int OutputAmount => Mathf.Max(1, craftedAmount);
    }
}
