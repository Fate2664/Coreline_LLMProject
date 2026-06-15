using System;
using Nova;
using UnityEngine;
using System.Collections.Generic;
using Coreline;
using Coreline.Robots;


public class UIManager : MonoBehaviour, ITimeTracker
{
    #region Class Variables

    [Header("References")]
    [SerializeField] private UIPanel inventoryPanel;
    [SerializeField] private BuildPlacer  buildPlacer;
    [Header("Inventory")]
    [SerializeField] private ItemDatabase ItemDatabase = null;
    [SerializeField] private ItemView EquipItemRoot = null;
    [SerializeField] private ItemView closeButtonRoot = null;
    [SerializeField] private ItemView destructionButtonRoot = null;
    [Space(10)]
    [Header("Grid Layout")]
    public GridView Grid = null;
    public int Count = 24;
    [Space(10)]
    [Header("Row Styling")] [SerializeField]
    private int padding = 10;
    
    [Header("Date & Time")]
    [SerializeField] private TextBlock TimeText = null;
    [SerializeField] private TextBlock TimePrefix = null;
    [SerializeField] private TextBlock DayText = null;

    public InventoryItem EquippedItem => equippedItem;
    public event Action<InventoryItemData, int> ItemAddedToInventory;

    private List<InventoryItem> Items;
    private readonly InventoryItem emptyEquippedItem = new ();
    private InventoryItem equippedItem;
    private bool inventoryNeedsRefresh;

    #endregion

    private void Start()
    {
        EnsureInventoryItems();
        InitGrid(Grid, Items);
        RegisterStandaloneGestureHandlers();
        RefreshEquippedItem();
        TimeManager.Instance.RegisterTracker(this);
    }

    public void AddItemToInventory(InventoryItemData item, int count = 1, bool countForProgression = true)
    {
        TryAddItemToInventory(item, count, countForProgression);
    }

    public bool TryAddItemToInventory(InventoryItemData item, int count = 1, bool countForProgression = true)
    {
        if (!CanAcceptItem(item, count))
        {
            return false;
        }

        int remaining = count;

        foreach (InventoryItem existing in Items)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (existing.isEmpty || existing.item != item || existing.count >= InventoryItem.maxCount)
            {
                continue;
            }

            int added = Mathf.Min(InventoryItem.maxCount - existing.count, remaining);
            existing.IncreaseCount(added);
            remaining -= added;
        }

        while (remaining > 0)
        {
            int emptyIndex = Items.FindIndex(x => x.isEmpty);
            if (emptyIndex < 0)
            {
                break;
            }

            int added = Mathf.Min(InventoryItem.maxCount, remaining);
            Items[emptyIndex] = new InventoryItem
            {
                item = item,
                count = added
            };
            remaining -= added;
        }

        inventoryNeedsRefresh = true;
        RefreshInventory();

        if (equippedItem == null && IsPickaxeTool(item))
        {
            EquipFirstPickaxe();
        }
        else if (equippedItem != null && equippedItem.item == item)
        {
            RefreshEquippedItem();
        }

        bool addedAllItems = remaining <= 0;
        if (addedAllItems && countForProgression)
        {
            ItemAddedToInventory?.Invoke(item, count);
        }

        return addedAllItems;
    }

    public bool CanAcceptItem(InventoryItemData item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        EnsureInventoryItems();

        int availableSpace = 0;
        foreach (InventoryItem inventoryItem in Items)
        {
            if (inventoryItem.isEmpty)
            {
                availableSpace += InventoryItem.maxCount;
            }
            else if (inventoryItem.item == item)
            {
                availableSpace += Mathf.Max(0, InventoryItem.maxCount - inventoryItem.count);
            }
        }

        return availableSpace >= count;
    }

    public int GetItemCount(InventoryItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        EnsureInventoryItems();

        int total = 0;
        foreach (InventoryItem inventoryItem in Items)
        {
            if (inventoryItem != null && !inventoryItem.isEmpty && inventoryItem.item == item)
            {
                total += inventoryItem.count;
            }
        }

        return total;
    }

    public bool TryRemoveItem(InventoryItemData item, int count = 1)
    {
        if (item == null || count <= 0 || GetItemCount(item) < count)
        {
            return false;
        }

        EnsureInventoryItems();

        int remaining = count;
        for (int i = 0; i < Items.Count && remaining > 0; i++)
        {
            InventoryItem inventoryItem = Items[i];
            if (inventoryItem == null || inventoryItem.isEmpty || inventoryItem.item != item)
            {
                continue;
            }

            int removed = Mathf.Min(inventoryItem.count, remaining);
            inventoryItem.DecreaseCount(removed);
            remaining -= removed;

            if (inventoryItem.count <= 0)
            {
                if (equippedItem == inventoryItem)
                {
                    equippedItem = null;
                }

                Items[i] = new InventoryItem();
            }
        }

        inventoryNeedsRefresh = true;
        RefreshInventory();
        RefreshEquippedItem();
        return remaining <= 0;
    }

    public int GetOreCount(OreType oreType)
    {
        EnsureInventoryItems();

        int total = 0;
        foreach (InventoryItem inventoryItem in Items)
        {
            if (inventoryItem == null || inventoryItem.isEmpty)
            {
                continue;
            }

            OreItemSO oreItem = inventoryItem.item as OreItemSO;
            if (oreItem != null && oreItem.oreType == oreType)
            {
                total += inventoryItem.count;
            }
        }

        return total;
    }

    public bool HasOre(OreType oreType, int count)
    {
        return count > 0 && GetOreCount(oreType) >= count;
    }

    public bool TryRemoveOre(OreType oreType, int count)
    {
        if (count <= 0 || GetOreCount(oreType) < count)
        {
            return false;
        }

        EnsureInventoryItems();

        int remaining = count;
        for (int i = 0; i < Items.Count && remaining > 0; i++)
        {
            InventoryItem inventoryItem = Items[i];
            if (inventoryItem == null || inventoryItem.isEmpty)
            {
                continue;
            }

            OreItemSO oreItem = inventoryItem.item as OreItemSO;
            if (oreItem == null || oreItem.oreType != oreType)
            {
                continue;
            }

            int removed = Mathf.Min(inventoryItem.count, remaining);
            inventoryItem.DecreaseCount(removed);
            remaining -= removed;

            if (inventoryItem.count <= 0)
            {
                if (equippedItem == inventoryItem)
                {
                    equippedItem = null;
                }

                Items[i] = new InventoryItem();
            }
        }

        inventoryNeedsRefresh = true;
        RefreshInventory();
        RefreshEquippedItem();
        return remaining <= 0;
    }

    private void EnsureInventoryItems()
    {
        if (Items != null)
        {
            return;
        }

        if (ItemDatabase != null)
        {
            Items = ItemDatabase.GetEmptyItems(Count);
            return;
        }

        Items = new List<InventoryItem>(Count);
        for (int i = 0; i < Count; i++)
        {
            Items.Add(new InventoryItem());
        }
    }

    #region Register Methods

    private void InitGrid(GridView grid, List<InventoryItem> datasource)
    {
        if (grid == null)
        {
            return;
        }

        grid.AddDataBinder<InventoryItem, InventoryItemVisuals>(BindItem);

        grid.SetSliceProvider(ProvideSlice);

        grid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
        grid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
        grid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(InventoryItemVisuals.HandlePress);
        grid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(InventoryItemVisuals.HandleRelease);

        grid.SetDataSource(datasource);
    }

    private void ProvideSlice(int sliceIndex, GridView gridview, ref GridSlice2D gridslice)
    {
        gridslice.Layout.AutoSize.Y = AutoSize.Shrink;
        gridslice.AutoLayout.AutoSpace = true;
        gridslice.Layout.Padding.Value = padding;
    }
    
    private void BindItem(Data.OnBind<InventoryItem> evt, InventoryItemVisuals target, int index) => target.Bind(evt.UserData, this);

    private void RegisterStandaloneGestureHandlers()
    {
        if (EquipItemRoot != null)
        {
            
            EquipItemRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            EquipItemRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            EquipItemRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(InventoryItemVisuals.HandlePress);
            EquipItemRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(InventoryItemVisuals.HandleRelease);
        }

        if (closeButtonRoot != null)
        {
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);
        }

        if (destructionButtonRoot != null)
        {
            destructionButtonRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            destructionButtonRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            destructionButtonRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            destructionButtonRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);
        }
    }

    #endregion

    public void RefreshInventory()
    {
        if (Grid == null || !Grid.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!inventoryNeedsRefresh) return;

        Grid.Refresh();
        inventoryNeedsRefresh = false;
    }

    #region Equip Item Methods

    public void EquipItem(InventoryItem item)
    {
        if (item == null || item.isEmpty)
        {
            return;
        }

        if (item.item is RobotCraftingRecipe robotRecipe)
        {
            BeginRobotPlacement(item, robotRecipe);
            return;
        }

        if (!CanEquipItem(item))
        {
            return;
        }

        equippedItem = item;
        if (equippedItem.IsTool)
        {
            ToolItemSO tool = equippedItem.item as ToolItemSO;
            switch (tool.toolType)
            {
                // case ToolType.Hoe:
                //     HoeEquipped();
                //     break;
                // case ToolType.WateringCan:
                //     break;
            }
        }
        RefreshEquippedItem();
    }

    public bool EquipFirstPickaxe()
    {
        EnsureInventoryItems();

        foreach (InventoryItem inventoryItem in Items)
        {
            if (!CanEquipItem(inventoryItem))
            {
                continue;
            }

            equippedItem = inventoryItem;
            RefreshEquippedItem();
            return true;
        }

        return false;
    }

    private static bool CanEquipItem(InventoryItem item)
    {
        return item != null && !item.isEmpty && IsPickaxeTool(item.item);
    }

    private static bool IsPickaxeTool(InventoryItemData item)
    {
        ToolItemSO tool = item as ToolItemSO;
        return tool != null && tool.toolType == ToolType.Pickaxe;
    }

    private void BeginRobotPlacement(InventoryItem inventorySlot, RobotCraftingRecipe recipe)
    {
        if (buildPlacer == null)
        {
            Debug.LogWarning("Cannot place robot because no BuildPlacer is assigned.", this);
            RefreshEquippedItem();
            return;
        }

        if (recipe == null || recipe.RobotPrefab == null)
        {
            Debug.LogWarning("Cannot place robot because the selected robot item has no prefab assigned.", this);
            RefreshEquippedItem();
            return;
        }

        buildPlacer.BeginPrefabPlacement(recipe.RobotPrefab, placedObject =>
        {
            if (placedObject != null && placedObject.TryGetComponent(out BaseRobotController robotController))
            {
                robotController.EnsureRobotCommandTarget();
            }

            TryConsumeInventorySlot(inventorySlot, 1);
        });

        equippedItem = null;
        RefreshEquippedItem();
        inventoryPanel ??= GetComponentInParent<UIPanel>();
        inventoryPanel?.Close();
    }

    private bool TryConsumeInventorySlot(InventoryItem inventorySlot, int amount)
    {
        if (inventorySlot == null || inventorySlot.isEmpty || amount <= 0 || inventorySlot.count < amount)
        {
            return false;
        }

        EnsureInventoryItems();
        inventorySlot.DecreaseCount(amount);

        if (inventorySlot.count <= 0)
        {
            int index = Items.IndexOf(inventorySlot);
            if (index >= 0)
            {
                Items[index] = new InventoryItem();
            }

            if (equippedItem == inventorySlot)
            {
                equippedItem = null;
            }
        }

        inventoryNeedsRefresh = true;
        RefreshInventory();
        RefreshEquippedItem();
        return true;
    }

    public void UnEquipItem()
    {
        equippedItem = null;
        RefreshEquippedItem();
    }

    private void RefreshEquippedItem()
    {
        if (EquipItemRoot == null || !EquipItemRoot.TryGetVisuals(out InventoryItemVisuals visuals)) return;
        
        visuals.Bind(equippedItem ?? emptyEquippedItem, this);
    }

    #endregion

    public void ClockUpdate(GameTimestamp timestamp)
    {
        int hours = timestamp.hour;
        int minutes = timestamp.minute;
        string prefix = "AM";
        
        if (hours > 12)
        {
            prefix = "PM";
            hours -= 12;
        }

        TimePrefix.Text = prefix;
        TimeText.Text = hours.ToString("00") +  ":" + minutes.ToString("00");
        DayText.Text = timestamp.day.ToString();
    }

    private void OnDisable() => TimeManager.Instance?.UnregisterTracker(this);
}
