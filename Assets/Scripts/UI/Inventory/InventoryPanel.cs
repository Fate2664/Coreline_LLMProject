using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

namespace Coreline
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private UIPanel panel;
        [SerializeField] private GridView grid;
        [SerializeField] private ItemView closeButtonRoot;

        [Header("Grid Layout")]
        [SerializeField, Min(0)] private int padding = 10;

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

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToInventory();
            RebuildDisplayedSlots();
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

        public void Open()
        {
            ResolveReferences();
            Refresh();
            panel?.Open();
        }

        public void Close()
        {
            ResolveReferences();
            panel?.Close();
        }

        public void Toggle()
        {
            ResolveReferences();
            panel?.Toggle();
        }

        public void Refresh()
        {
            ResolveReferences();
            int previousSlotCount = displayedSlots.Count;
            RebuildDisplayedSlots();

            if (!gridRegistered || grid == null || !grid.gameObject.activeInHierarchy)
            {
                return;
            }

            if (previousSlotCount != displayedSlots.Count ||
                boundSlotCount != displayedSlots.Count)
            {
                SetGridDataSource();
                return;
            }

            grid.Refresh();
        }

        private void ResolveReferences()
        {
            panel ??= GetComponent<UIPanel>();
            grid ??= GetComponentInChildren<GridView>(true);
        }

        private void SubscribeToInventory()
        {
            if (inventorySubscribed || playerInventory == null)
            {
                return;
            }

            playerInventory.InventoryChanged += HandleInventoryChanged;
            inventorySubscribed = true;
        }

        private void UnsubscribeFromInventory()
        {
            if (!inventorySubscribed || playerInventory == null)
            {
                inventorySubscribed = false;
                return;
            }

            playerInventory.InventoryChanged -= HandleInventoryChanged;
            inventorySubscribed = false;
        }

        private void HandleInventoryChanged()
        {
            Refresh();
        }

        private void RebuildDisplayedSlots()
        {
            displayedSlots.Clear();

            if (playerInventory == null)
            {
                return;
            }

            IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                displayedSlots.Add(slots[i]);
            }
        }

        private void RegisterGrid()
        {
            if (gridRegistered || grid == null)
            {
                return;
            }

            grid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            grid.SetSliceProvider(ProvideSlice);
            grid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(HandleSlotHovered);
            grid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(HandleSlotUnhovered);
            grid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleSlotPressed);
            grid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleSlotReleased);
            grid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandleSlotCanceled);

            gridRegistered = true;
            SetGridDataSource();
        }

        private void UnregisterGrid()
        {
            if (!gridRegistered || grid == null)
            {
                gridRegistered = false;
                return;
            }

            grid.RemoveDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            grid.RemoveGestureHandler<Gesture.OnHover, InventoryItemVisuals>(HandleSlotHovered);
            grid.RemoveGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(HandleSlotUnhovered);
            grid.RemoveGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleSlotPressed);
            grid.RemoveGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleSlotReleased);
            grid.RemoveGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandleSlotCanceled);
            gridRegistered = false;
        }

        private void SetGridDataSource()
        {
            grid.SetDataSource(displayedSlots);
            boundSlotCount = displayedSlots.Count;
        }

        private void ProvideSlice(
            int sliceIndex,
            GridView gridView,
            ref GridSlice2D gridSlice)
        {
            gridSlice.Layout.AutoSize.Y = AutoSize.Shrink;
            gridSlice.AutoLayout.AutoSpace = true;
            gridSlice.Layout.Padding.Value = padding;
        }

        private static void BindSlot(
            Data.OnBind<InventorySlot> evt,
            InventoryItemVisuals target,
            int index)
        {
            target.Bind(evt.UserData);
        }

        private static void HandleSlotHovered(
            Gesture.OnHover evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnHover();
        }

        private static void HandleSlotUnhovered(
            Gesture.OnUnhover evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnUnhover();
        }

        private static void HandleSlotPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnPressVisualOnly();
            evt.Consume();
        }

        private void HandleSlotReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnRelease();

            if (TryGetSlot(index, out InventorySlot slot) && !slot.IsEmpty)
            {
                SlotSelected?.Invoke(index, slot);
            }

            evt.Consume();
        }

        private static void HandleSlotCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnCancel();
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

        private void RegisterCloseButton()
        {
            if (closeButtonRegistered || closeButtonRoot == null)
            {
                return;
            }

            UIBlock button = closeButtonRoot.UIBlock;
            button.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            button.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            button.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            button.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleCloseButtonReleased);
            closeButtonRegistered = true;
        }

        private void UnregisterCloseButton()
        {
            if (!closeButtonRegistered || closeButtonRoot == null)
            {
                closeButtonRegistered = false;
                return;
            }

            UIBlock button = closeButtonRoot.UIBlock;
            button.RemoveGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            button.RemoveGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            button.RemoveGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            button.RemoveGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleCloseButtonReleased);
            closeButtonRegistered = false;
        }

        private void HandleCloseButtonReleased(
            Gesture.OnRelease evt,
            InventoryButtonVisuals target)
        {
            if (target.ButtonRoot != null)
            {
                target.ButtonRoot.Color = target.HoverColor;
            }

            Close();
            evt.Consume();
        }
    }
}
