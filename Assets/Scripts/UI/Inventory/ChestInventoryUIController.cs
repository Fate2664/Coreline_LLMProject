using System.Collections.Generic;
using Nova;
using UnityEngine;
using UnityEngine.SceneManagement;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Coreline
{
    [DisallowMultipleComponent]
    public sealed class ChestInventoryUIController : MonoBehaviour
    {
        private const string DefaultRootName = "ChestInventory";
        private const string ChestGridName = "ChestInventory";
        private const string PlayerGridName = "PlayerInventory";
        private const string ChestNameTextName = "RobotTypeText";
        private const string CloseButtonName = "CloseButton";

        [Header("Panel")]
        [SerializeField] private UIPanel panel;
        [SerializeField] private TextBlock chestNameText;
        [SerializeField] private UIBlock2D closeButton;

        [Header("Inventory Grids")]
        [SerializeField] private GridView chestGrid;
        [SerializeField] private GridView playerGrid;
        [SerializeField, Min(0)] private int padding = 10;

        [Header("Drag Preview")]
        [SerializeField] private UIBlock2D dragPreviewIcon;
        [SerializeField] private Camera dragPreviewCamera;
        [SerializeField] private Vector2 dragPreviewSize = new(56f, 56f);
        [SerializeField] private Vector2 dragPreviewOffset = new(22f, -22f);
        [SerializeField] private short dragPreviewZIndex = 2000;

        private readonly List<InventorySlot> chestSlots = new();
        private readonly List<InventorySlot> pendingChestSlots = new();
        private readonly List<InventorySlot> playerSlots = new();
        private readonly List<InventorySlot> pendingPlayerSlots = new();
        private readonly Dictionary<uint, int> pressedChestSlotIndices = new();
        private readonly Dictionary<uint, int> pressedPlayerSlotIndices = new();
        private readonly List<UIBlockHit> uiBlockHits = new();

        private ChestInventory activeChest;
        private PlayerInventory playerInventory;
        private bool gridsInitialized;
        private bool closeButtonRegistered;
        private bool inventoriesSubscribed;
        private bool isTransferring;
        private int boundChestSlotCount = -1;
        private int boundPlayerSlotCount = -1;

        public static bool IsAnyOpen { get; private set; }
        public ChestInventory ActiveChest => activeChest;
        public bool IsOpen => panel != null && panel.IsOpen;

        private void Awake()
        {
            EnsureReferences();
            InitializeGrids();
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeGrids();
            RegisterCloseButton();

            if (activeChest != null)
            {
                SubscribeToInventories();
                RefreshAll();
            }
        }

        private void OnDisable()
        {
            HideDragPreview();
            UnsubscribeFromInventories();
            UnregisterCloseButton();
            pressedChestSlotIndices.Clear();
            pressedPlayerSlotIndices.Clear();
            IsAnyOpen = false;
        }

        private void LateUpdate()
        {
            if (IsOpen)
            {
                UpdateDragPreviewFromPointer();
            }
        }

        public void Toggle(ChestInventory chest, Player playerContext)
        {
            if (IsOpen && activeChest == chest)
            {
                Close();
                return;
            }

            Open(chest, playerContext);
        }

        public void Open(ChestInventory chest, Player playerContext)
        {
            if (chest == null)
            {
                Debug.LogWarning($"{nameof(ChestInventoryUIController)} cannot open without a chest.", this);
                return;
            }

            UnsubscribeFromInventories();
            activeChest = chest;
            ResolvePlayerInventory(playerContext);
            EnsureReferences();
            InitializeGrids();

            if (panel == null)
            {
                Debug.LogError($"{nameof(ChestInventoryUIController)} requires a {nameof(UIPanel)}.", this);
                return;
            }

            CloseStandalonePlayerInventory();
            IsAnyOpen = true;
            panel.Open();

            SubscribeToInventories();
            UpdateChestName();
            RefreshAll();
        }

        public void Close()
        {
            HideDragPreview();
            IsAnyOpen = false;
            pressedChestSlotIndices.Clear();
            pressedPlayerSlotIndices.Clear();
            UnsubscribeFromInventories();
            panel?.Close();
        }

        public void RefreshAll()
        {
            UpdateChestName();
            RefreshChestInventory();
            RefreshPlayerInventory();
        }

        private void RefreshChestInventory()
        {
            pendingChestSlots.Clear();
            if (activeChest != null)
            {
                IReadOnlyList<InventorySlot> slots = activeChest.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    pendingChestSlots.Add(slots[i]);
                }
            }

            if (chestGrid == null || !chestGrid.gameObject.activeInHierarchy)
            {
                return;
            }

            ApplySlots(chestGrid, chestSlots, pendingChestSlots, ref boundChestSlotCount);
        }

        private void RefreshPlayerInventory()
        {
            pendingPlayerSlots.Clear();
            if (playerInventory != null)
            {
                IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    pendingPlayerSlots.Add(slots[i]);
                }
            }

            if (playerGrid == null || !playerGrid.gameObject.activeInHierarchy)
            {
                return;
            }

            ApplySlots(playerGrid, playerSlots, pendingPlayerSlots, ref boundPlayerSlotCount);
        }

        private static void ApplySlots(
            GridView grid,
            List<InventorySlot> displayed,
            List<InventorySlot> pending,
            ref int boundSlotCount)
        {
            if (boundSlotCount != pending.Count || displayed.Count != pending.Count)
            {
                displayed.Clear();
                displayed.AddRange(pending);
                grid.SetDataSource(displayed);
                boundSlotCount = displayed.Count;
                return;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                displayed[i] = pending[i];
            }

            grid.Refresh();
        }

        private void InitializeGrids()
        {
            if (gridsInitialized || chestGrid == null || playerGrid == null)
            {
                return;
            }

            chestGrid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            chestGrid.SetSliceProvider(ProvideSlice);
            chestGrid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            chestGrid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            chestGrid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleChestSlotPressed);
            chestGrid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleChestSlotReleased);
            chestGrid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandleChestSlotCanceled);

            playerGrid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindSlot);
            playerGrid.SetSliceProvider(ProvideSlice);
            playerGrid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            playerGrid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            playerGrid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandlePlayerSlotPressed);
            playerGrid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandlePlayerSlotReleased);
            playerGrid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandlePlayerSlotCanceled);

            gridsInitialized = true;
        }

        private void ProvideSlice(int sliceIndex, GridView gridView, ref GridSlice2D gridSlice)
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

        private void HandleChestSlotPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target,
            int index)
        {
            if (!IsValidSlot(chestSlots, index))
            {
                return;
            }

            pressedChestSlotIndices[evt.Interaction.ControlID] = index;
            target.OnPressVisualOnly();
            ShowDragPreview(
                chestSlots[index].Item?.itemDesc?.Icon,
                evt.PointerWorldPosition);
            evt.Consume();
        }

        private void HandleChestSlotReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnRelease();
            uint controlId = evt.Interaction.ControlID;
            bool shouldTransfer =
                pressedChestSlotIndices.TryGetValue(controlId, out int pressedIndex) &&
                IsPointerOverGrid(evt.Interaction.Ray, playerGrid);

            pressedChestSlotIndices.Remove(controlId);
            HideDragPreview();
            if (shouldTransfer)
            {
                TransferChestSlotToPlayer(pressedIndex);
                evt.Consume();
            }
        }

        private void HandleChestSlotCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target,
            int index)
        {
            pressedChestSlotIndices.Remove(evt.Interaction.ControlID);
            ResetSlotVisual(target);
            HideDragPreview();
        }

        private void HandlePlayerSlotPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target,
            int index)
        {
            if (!IsValidSlot(playerSlots, index))
            {
                return;
            }

            pressedPlayerSlotIndices[evt.Interaction.ControlID] = index;
            target.OnPressVisualOnly();
            ShowDragPreview(
                playerSlots[index].Item?.itemDesc?.Icon,
                evt.PointerWorldPosition);
            evt.Consume();
        }

        private void HandlePlayerSlotReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target,
            int index)
        {
            target.OnRelease();
            uint controlId = evt.Interaction.ControlID;
            bool shouldTransfer =
                pressedPlayerSlotIndices.TryGetValue(controlId, out int pressedIndex) &&
                IsPointerOverGrid(evt.Interaction.Ray, chestGrid);

            pressedPlayerSlotIndices.Remove(controlId);
            HideDragPreview();
            if (shouldTransfer)
            {
                TransferPlayerSlotToChest(pressedIndex);
                evt.Consume();
            }
        }

        private void HandlePlayerSlotCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target,
            int index)
        {
            pressedPlayerSlotIndices.Remove(evt.Interaction.ControlID);
            ResetSlotVisual(target);
            HideDragPreview();
        }

        private bool TransferChestSlotToPlayer(int index)
        {
            if (!IsValidSlot(chestSlots, index) ||
                activeChest == null ||
                playerInventory == null)
            {
                return false;
            }

            InventorySlot slot = chestSlots[index];
            InventoryItemData item = slot.Item;
            int amount = slot.Amount;
            if (!playerInventory.CanAcceptItem(item, amount))
            {
                return false;
            }

            isTransferring = true;
            bool removed = activeChest.TryRemoveFromSlot(index, amount);
            bool added = removed && playerInventory.TryAddItem(item, amount);

            if (removed && !added)
            {
                activeChest.TryAddItem(item, amount);
            }

            isTransferring = false;
            RefreshAll();
            return added;
        }

        private bool TransferPlayerSlotToChest(int index)
        {
            if (!IsValidSlot(playerSlots, index) ||
                activeChest == null ||
                playerInventory == null)
            {
                return false;
            }

            InventorySlot slot = playerSlots[index];
            InventoryItemData item = slot.Item;
            int amount = slot.Amount;
            if (!activeChest.CanAcceptItem(item, amount))
            {
                return false;
            }

            isTransferring = true;
            bool removed = playerInventory.TryRemoveFromSlot(index, amount);
            bool added = removed && activeChest.TryAddItem(item, amount);

            if (removed && !added)
            {
                playerInventory.TryAddItem(item, amount, countForProgression: false);
            }

            isTransferring = false;
            RefreshAll();
            return added;
        }

        private bool IsPointerOverGrid(Ray pointerRay, GridView targetGrid)
        {
            if (targetGrid == null)
            {
                return false;
            }

            uiBlockHits.Clear();
            Interaction.RaycastAll(pointerRay, uiBlockHits);
            Transform gridTransform = targetGrid.transform;

            for (int i = 0; i < uiBlockHits.Count; i++)
            {
                UIBlock hitBlock = uiBlockHits[i].UIBlock;
                if (hitBlock != null && hitBlock.transform.IsChildOf(gridTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidSlot(IReadOnlyList<InventorySlot> slots, int index)
        {
            return index >= 0 &&
                   index < slots.Count &&
                   slots[index] != null &&
                   !slots[index].IsEmpty;
        }

        private static void ResetSlotVisual(InventoryItemVisuals target)
        {
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.DefaultColor;
            }
        }

        private void SubscribeToInventories()
        {
            if (inventoriesSubscribed)
            {
                return;
            }

            if (activeChest != null)
            {
                activeChest.InventoryUpdated += HandleChestInventoryChanged;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated += HandlePlayerInventoryChanged;
            }

            inventoriesSubscribed = true;
        }

        private void UnsubscribeFromInventories()
        {
            if (!inventoriesSubscribed)
            {
                return;
            }

            if (activeChest != null)
            {
                activeChest.InventoryUpdated -= HandleChestInventoryChanged;
            }

            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated -= HandlePlayerInventoryChanged;
            }

            inventoriesSubscribed = false;
        }

        private void HandleChestInventoryChanged()
        {
            if (!isTransferring)
            {
                RefreshChestInventory();
            }
        }

        private void HandlePlayerInventoryChanged()
        {
            if (!isTransferring)
            {
                RefreshPlayerInventory();
            }
        }

        private void RegisterCloseButton()
        {
            if (closeButtonRegistered || closeButton == null)
            {
                return;
            }

            closeButton.AddGestureHandler<Gesture.OnClick>(HandleCloseButtonClicked);
            closeButtonRegistered = true;
        }

        private void UnregisterCloseButton()
        {
            if (!closeButtonRegistered || closeButton == null)
            {
                return;
            }

            closeButton.RemoveGestureHandler<Gesture.OnClick>(HandleCloseButtonClicked);
            closeButtonRegistered = false;
        }

        private void HandleCloseButtonClicked(Gesture.OnClick evt)
        {
            Close();
            evt.Consume();
        }

        private void UpdateChestName()
        {
            if (chestNameText != null)
            {
                chestNameText.Text = activeChest != null
                    ? $"Chest Name: {activeChest.ChestName}"
                    : "Chest Name:";
            }
        }

        private void ResolvePlayerInventory(Player playerContext)
        {
            playerInventory = playerContext != null
                ? playerContext.GetComponent<PlayerInventory>() ??
                  playerContext.GetComponentInParent<PlayerInventory>()
                : playerInventory;
            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        }

        private void CloseStandalonePlayerInventory()
        {
            InventoryPanel inventoryPanel =
                FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            if (inventoryPanel != null && inventoryPanel.IsOpen)
            {
                inventoryPanel.Panel.Close();
            }
        }

        private void ShowDragPreview(Sprite icon, Vector3 pointerWorldPosition)
        {
            if (icon == null)
            {
                return;
            }

            EnsureDragPreviewIcon();
            if (dragPreviewIcon == null)
            {
                return;
            }

            dragPreviewIcon.SetImage(icon);
            dragPreviewIcon.gameObject.SetActive(true);
            UpdateDragPreviewPosition(pointerWorldPosition);
        }

        private void HideDragPreview()
        {
            if (dragPreviewIcon != null)
            {
                dragPreviewIcon.gameObject.SetActive(false);
            }
        }

        private void UpdateDragPreviewFromPointer()
        {
            if (dragPreviewIcon == null || !dragPreviewIcon.gameObject.activeSelf)
            {
                return;
            }

            Camera camera = GetDragPreviewCamera();
            if (camera == null || !TryGetPointerScreenPosition(out Vector2 pointerPosition))
            {
                return;
            }

            Ray pointerRay = camera.ScreenPointToRay(pointerPosition);
            Transform previewParent = dragPreviewIcon.transform.parent != null
                ? dragPreviewIcon.transform.parent
                : transform;
            Plane uiPlane = new(previewParent.forward, previewParent.position);

            if (uiPlane.Raycast(pointerRay, out float distance))
            {
                UpdateDragPreviewPosition(pointerRay.GetPoint(distance));
            }
        }

        private void UpdateDragPreviewPosition(Vector3 pointerWorldPosition)
        {
            if (dragPreviewIcon == null)
            {
                return;
            }

            Transform previewParent = dragPreviewIcon.transform.parent;
            Vector3 localPosition = previewParent != null
                ? previewParent.InverseTransformPoint(pointerWorldPosition)
                : pointerWorldPosition;
            localPosition += (Vector3)dragPreviewOffset;
            localPosition.z = -0.25f;
            dragPreviewIcon.TrySetLocalPosition(localPosition);
        }

        private void EnsureDragPreviewIcon()
        {
            if (dragPreviewIcon != null)
            {
                return;
            }

            GameObject previewObject = new("ChestInventoryDragPreview");
            previewObject.layer = gameObject.layer;
            previewObject.transform.SetParent(transform, false);
            dragPreviewIcon = previewObject.AddComponent<UIBlock2D>();
            dragPreviewIcon.Color = Color.white;
            dragPreviewIcon.Size.XY.Value = dragPreviewSize;
            dragPreviewIcon.ZIndex = dragPreviewZIndex;

            SortGroup sortGroup = previewObject.AddComponent<SortGroup>();
            sortGroup.RenderQueue = 4001;
            sortGroup.RenderOverOpaqueGeometry = true;
            dragPreviewIcon.gameObject.SetActive(false);
        }

        private Camera GetDragPreviewCamera()
        {
            if (dragPreviewCamera != null)
            {
                return dragPreviewCamera;
            }

            dragPreviewCamera = Camera.main;
            dragPreviewCamera ??= FindFirstObjectByType<Camera>();
            return dragPreviewCamera;
        }

        private static bool TryGetPointerScreenPosition(out Vector2 pointerPosition)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.mousePresent)
            {
                pointerPosition = default;
                return false;
            }

            pointerPosition = Input.mousePosition;
            return true;
#else
            if (Mouse.current == null)
            {
                pointerPosition = default;
                return false;
            }

            pointerPosition = Mouse.current.position.ReadValue();
            return true;
#endif
        }

        private void EnsureReferences()
        {
            panel ??= GetComponent<UIPanel>();
            panel ??= GetComponentInParent<UIPanel>();
            chestGrid ??= FindGridByName(ChestGridName);
            playerGrid ??= FindGridByName(PlayerGridName);
            chestNameText ??= FindChildComponentByName<TextBlock>(ChestNameTextName);
            closeButton ??= FindChildComponentByName<UIBlock2D>(CloseButtonName);
            ResolvePlayerInventory(null);
        }

        private GridView FindGridByName(string objectName)
        {
            GridView[] grids = GetComponentsInChildren<GridView>(true);
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null && grids[i].name == objectName)
                {
                    return grids[i];
                }
            }

            return null;
        }

        private T FindChildComponentByName<T>(string objectName) where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].name == objectName)
                {
                    return components[i];
                }
            }

            return null;
        }

        public static ChestInventoryUIController FindOrCreateInScene()
        {
            ChestInventoryUIController existing =
                FindFirstObjectByType<ChestInventoryUIController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = FindSceneObject(DefaultRootName);
            if (root == null)
            {
                Debug.LogWarning($"Could not find a scene object named {DefaultRootName}.");
                return null;
            }

            return root.AddComponent<ChestInventoryUIController>();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    Transform found = FindChildRecursive(rootObject.transform, objectName);
                    if (found != null)
                    {
                        return found.gameObject;
                    }
                }
            }

            return null;
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
    }
}
