using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

namespace Coreline
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        [Header("Bindings")] [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private UIPanel panel;
        [SerializeField] private GridView grid;
        [SerializeField] private ItemView closeButtonRoot;

        [Header("Grid Layout")] [SerializeField, Min(0)]
        private int padding = 10;

        private readonly List<InventorySlot> displayedSlots = new();
        private bool inventorySubscribed;
        private bool gridRegistered;
        private bool closeButtonRegistered;
        private int boundSlotCount = -1;

        public event Action<int, InventorySlot> SlotSelected;

        public PlayerInventory Inventory => playerInventory;
        public UIPanel Panel => panel;
        public GridView Grid => grid;
        public bool IsOpen => panel != null && panel.IsOpen;

        private void OnEnable()
        {
            SubscribeToInventory();
            BuildDisplaySlots();
            RegisterGrid();
            RegisterCloseButton();
        }

        private void OnDisable()
        {
            UnsubscribeFromInventory();
            UnregisterGrid();
            UnregisterCloseButton();
        }

        public void Bind(PlayerInventory inventory)
        {
            if (playerInventory == inventory)
            {
                Refresh();
                return;
            }

            UnsubscribeFromInventory();
            playerInventory = inventory;

            if (isActiveAndEnabled)
            {
                SubscribeToInventory();
            }

            Refresh();
        }

        public void Refresh()
        {
            int previousSlotCount = displayedSlots.Count;
            BuildDisplaySlots();

            if (!gridRegistered || !grid.gameObject.activeInHierarchy) return;
            if (previousSlotCount != displayedSlots.Count || boundSlotCount != displayedSlots.Count)
            {
                SetGridDataSource();
                return;
            }

            grid.Refresh();
        }

        private void BuildDisplaySlots()
        {
            displayedSlots.Clear();

            IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                displayedSlots.Add(slots[i]);
            }
        }

        private void SetGridDataSource()
        {
            grid.SetDataSource(displayedSlots);
            boundSlotCount = displayedSlots.Count;
        }

        private void ProvideSlice(int sliceIndex, GridView gridView, ref GridSlice2D gridSlice)
        {
            gridSlice.Layout.AutoSize.Y = AutoSize.Shrink;
            gridSlice.AutoLayout.AutoSpace = true;
            gridSlice.Layout.Padding.Value = padding;
        }

        private static void BindSlot(Data.OnBind<InventorySlot> evt, InventoryItemVisuals target, int index)
        {
            target.Bind(evt.UserData);
        }

        private bool TryGetSlot(int index, out InventorySlot slot)
        {
            if (index >= 0 && index < displayedSlots.Count)
            {
                slot = displayedSlots[index];
                return slot != null;
            }

            slot = null;
            return false;
        }


        #region Subscribe/Register Methods

        private void RegisterCloseButton()
        {
            if (closeButtonRegistered) return;

            UIBlock button = closeButtonRoot.UIBlock;
            button.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            button.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            button.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            button.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleCloseButtonPressed);
            closeButtonRegistered = true;
        }
        
        private void RegisterGrid()
        {
            if (gridRegistered) return;

            grid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            grid.SetSliceProvider(ProvideSlice);
            grid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            grid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            grid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleSlotPressed);
            grid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleSlotReleased);
            gridRegistered = true;
            SetGridDataSource();
        }
        
        private void SubscribeToInventory()
        {
            if (inventorySubscribed) return;

            playerInventory.InventoryUpdated += Refresh;
            inventorySubscribed = true;
        }
        
        #endregion

        #region Unsubscribe/Unregister Methods

        private void UnregisterCloseButton()
        {
            if (!closeButtonRegistered) return;

            UIBlock button = closeButtonRoot.UIBlock;
            button.RemoveGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            button.RemoveGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            button.RemoveGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            button.RemoveGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleCloseButtonPressed);
            closeButtonRegistered = false;
        }
        
        private void UnregisterGrid()
        {
            if (!gridRegistered) return;

            grid.RemoveDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            grid.RemoveGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            grid.RemoveGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            grid.RemoveGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleSlotPressed);
            grid.RemoveGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleSlotReleased);
            gridRegistered = false;
        }
        
        private void UnsubscribeFromInventory()
        {
            if (!inventorySubscribed) return;

            playerInventory.InventoryUpdated -= Refresh;
            inventorySubscribed = false;
        }

        #endregion

        #region Handle Methods

        private static void HandleSlotPressed(Gesture.OnPress evt, InventoryItemVisuals target, int index)
        {
            target.OnPressVisualOnly();
            evt.Consume();
        }

        private void HandleSlotReleased(Gesture.OnRelease evt, InventoryItemVisuals target, int index)
        {
            target.OnRelease();

            if (TryGetSlot(index, out InventorySlot slot) && !slot.IsEmpty)
            {
                SlotSelected?.Invoke(index, slot);
            }

            evt.Consume();
        }

        private void HandleCloseButtonPressed(Gesture.OnRelease evt, InventoryButtonVisuals target)
        {
            if (target.ButtonRoot != null)
                target.ButtonRoot.Color = target.HoverColor;

            panel.Close();
            evt.Consume();
        }

        #endregion
    }
}
