using System;
using Nova;
using UnityEngine;
using System.Collections.Generic;
using Coreline;


public class UIManager : MonoBehaviour, ITimeTracker
{
    #region Class Variables

    [Header("References")]
    [SerializeField] private PlayerController playerController;
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

    public void AddItemToInventory(InventoryItemData item, int count = 1)
    {
        TryAddItemToInventory(item, count);
    }

    public bool TryAddItemToInventory(InventoryItemData item, int count = 1)
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

        if (equippedItem != null && equippedItem.item == item)
        {
            RefreshEquippedItem();
        }

        return remaining <= 0;
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
        equippedItem = item != null && !item.isEmpty ? item : null;
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

    public bool TryUseEquippedItem(int amount = 1)
    {
        if (equippedItem.isEmpty || equippedItem.count < amount) return false;
        
        equippedItem.DecreaseCount(amount);

        if (equippedItem.count <= 0)
        {
            int index = Items.IndexOf(equippedItem);
            if (index >= 0) Items[index] = new InventoryItem();
            equippedItem = null;
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

    private void HoeEquipped()
    {
        playerController.ToggleInventory();
        buildPlacer.enabled = true;
        buildPlacer.SetDestroyMode(false);
        buildPlacer.placementPieceType = BuildPieceType.Floor;
    }

    public void FenceEquipped()
    {
        playerController.ToggleInventory();
        buildPlacer.enabled = true;
        buildPlacer.SetDestroyMode(false);
        buildPlacer.placementPieceType = BuildPieceType.Wall;
    }

    public void DestroyEquipped()
    {
        playerController.ToggleInventory();
        buildPlacer.enabled = true;
        buildPlacer.SetDestroyMode(true);
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
