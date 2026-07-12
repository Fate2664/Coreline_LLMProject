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
    public sealed class FurnaceInventoryUIController : MonoBehaviour
    {
        private const string DefaultRootName = "FurnaceInventory";
        private const string PlayerGridName = "PlayerInventory";
        private const string FuelItemName = "FuelItem";
        private const string InputItemName = "IngredientItem";
        private const string OutputItemName = "ProduceItem";
        private const string SliderName = "Slider";
        private const string CloseButtonName = "CloseButton";

        private enum FurnaceSlotType
        {
            Fuel,
            Input,
            Output
        }

        [Header("Panel")]
        [SerializeField] private UIPanel panel;
        [SerializeField] private UIBlock2D closeButton;

        [Header("Furnace Slots")]
        [SerializeField] private ItemView fuelItemView;
        [SerializeField] private ItemView inputItemView;
        [SerializeField] private ItemView outputItemView;

        [Header("Player Inventory")]
        [SerializeField] private GridView playerGrid;
        [SerializeField, Min(0)] private int padding = 10;

        [Header("Processing")]
        [SerializeField] private UIBlock2D processingSlider;
        [SerializeField, Range(0f, 1f)] private float emptySliderPercent = 0.17f;
        [SerializeField, Range(0f, 1f)] private float fullSliderPercent = 0.84f;

        [Header("Drag Preview")]
        [SerializeField] private UIBlock2D dragPreviewIcon;
        [SerializeField] private Camera dragPreviewCamera;
        [SerializeField] private Vector2 dragPreviewSize = new(56f, 56f);
        [SerializeField] private Vector2 dragPreviewOffset = new(22f, -22f);
        [SerializeField] private short dragPreviewZIndex = 2000;

        private readonly List<InventorySlot> playerSlots = new();
        private readonly List<InventorySlot> pendingPlayerSlots = new();
        private readonly Dictionary<InventoryItemVisuals, FurnaceSlotType> slotTypes = new();
        private readonly Dictionary<uint, int> pressedPlayerSlotIndices = new();
        private readonly Dictionary<uint, FurnaceSlotType> pressedFurnaceSlots = new();
        private readonly HashSet<ItemView> registeredFurnaceSlots = new();
        private readonly List<UIBlockHit> uiBlockHits = new();

        private FurnaceController activeFurnace;
        private PlayerInventory playerInventory;
        private bool playerGridInitialized;
        private bool closeButtonRegistered;
        private bool furnaceSubscribed;
        private bool playerInventorySubscribed;
        private bool isTransferring;
        private int boundPlayerSlotCount = -1;

        public static bool IsAnyOpen { get; private set; }
        public FurnaceController ActiveFurnace => activeFurnace;
        public bool IsOpen => panel != null && panel.IsOpen;

        private void Awake()
        {
            EnsureReferences();
            InitializePlayerGrid();
            SetProgress(0f);
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializePlayerGrid();
            RegisterCloseButton();
            RegisterFurnaceSlotHandlers();
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            HideDragPreview();
            Unsubscribe();
            UnregisterFurnaceSlotHandlers();
            UnregisterCloseButton();
            pressedPlayerSlotIndices.Clear();
            pressedFurnaceSlots.Clear();
            IsAnyOpen = false;
        }

        private void LateUpdate()
        {
            if (IsOpen)
            {
                UpdateDragPreviewFromPointer();
            }
        }

        public void Toggle(FurnaceController furnace, Player playerContext)
        {
            if (IsOpen && activeFurnace == furnace)
            {
                Close();
                return;
            }

            Open(furnace, playerContext);
        }

        public void Open(FurnaceController furnace, Player playerContext)
        {
            if (furnace == null)
            {
                Debug.LogWarning(
                    $"{nameof(FurnaceInventoryUIController)} cannot open without a furnace.",
                    this);
                return;
            }

            Unsubscribe();
            activeFurnace = furnace;
            ResolvePlayerInventory(playerContext);
            EnsureReferences();
            InitializePlayerGrid();

            if (panel == null)
            {
                Debug.LogError(
                    $"{nameof(FurnaceInventoryUIController)} requires a {nameof(UIPanel)}.",
                    this);
                return;
            }

            CloseStandalonePlayerInventory();
            IsAnyOpen = true;
            panel.Open();
            Subscribe();
            RefreshAll();
        }

        public void Close()
        {
            HideDragPreview();
            IsAnyOpen = false;
            pressedPlayerSlotIndices.Clear();
            pressedFurnaceSlots.Clear();
            Unsubscribe();
            panel?.Close();
        }

        public void RefreshAll()
        {
            RefreshFurnaceSlots();
            RefreshPlayerInventory();
            SetProgress(activeFurnace != null ? activeFurnace.Progress01 : 0f);
        }

        private void RefreshFurnaceSlots()
        {
            BindSlot(fuelItemView, activeFurnace?.FuelSlot);
            BindSlot(inputItemView, activeFurnace?.InputSlot);
            BindSlot(outputItemView, activeFurnace?.OutputSlot);
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

            if (boundPlayerSlotCount != pendingPlayerSlots.Count ||
                playerSlots.Count != pendingPlayerSlots.Count)
            {
                playerSlots.Clear();
                playerSlots.AddRange(pendingPlayerSlots);
                playerGrid.SetDataSource(playerSlots);
                boundPlayerSlotCount = playerSlots.Count;
                return;
            }

            for (int i = 0; i < pendingPlayerSlots.Count; i++)
            {
                playerSlots[i] = pendingPlayerSlots[i];
            }

            playerGrid.Refresh();
        }

        private void InitializePlayerGrid()
        {
            if (playerGridInitialized || playerGrid == null)
            {
                return;
            }

            playerGrid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindPlayerSlot);
            playerGrid.SetSliceProvider(ProvideSlice);
            playerGrid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(
                InventoryItemVisuals.HandleHover);
            playerGrid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(
                InventoryItemVisuals.HandleUnhover);
            playerGrid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(
                HandlePlayerSlotPressed);
            playerGrid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(
                HandlePlayerSlotReleased);
            playerGrid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(
                HandlePlayerSlotCanceled);
            playerGridInitialized = true;
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

        private static void BindPlayerSlot(
            Data.OnBind<InventorySlot> evt,
            InventoryItemVisuals target,
            int index)
        {
            target.Bind(evt.UserData);
        }

        private void HandlePlayerSlotPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target,
            int index)
        {
            if (!IsValidPlayerSlot(index))
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

            bool hadPressedSlot =
                pressedPlayerSlotIndices.TryGetValue(controlId, out int pressedIndex);
            bool hasDestination =
                TryGetFurnaceDropTarget(evt.Interaction.Ray, out FurnaceSlotType destination);

            pressedPlayerSlotIndices.Remove(controlId);
            HideDragPreview();

            if (hadPressedSlot &&
                hasDestination &&
                TransferPlayerSlotToFurnace(pressedIndex, destination))
            {
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

        private void HandleFurnaceSlotPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target)
        {
            if (!slotTypes.TryGetValue(target, out FurnaceSlotType slotType))
            {
                return;
            }

            InventorySlot slot = GetFurnaceSlot(slotType);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            pressedFurnaceSlots[evt.Interaction.ControlID] = slotType;
            target.OnPressVisualOnly();
            ShowDragPreview(slot.Item?.itemDesc?.Icon, evt.PointerWorldPosition);
            evt.Consume();
        }

        private void HandleFurnaceSlotReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target)
        {
            target.OnRelease();
            uint controlId = evt.Interaction.ControlID;
            bool shouldTransfer =
                pressedFurnaceSlots.TryGetValue(controlId, out FurnaceSlotType slotType) &&
                IsPointerOverTarget(evt.Interaction.Ray, playerGrid?.transform);

            pressedFurnaceSlots.Remove(controlId);
            HideDragPreview();

            if (shouldTransfer && TransferFurnaceSlotToPlayer(slotType))
            {
                evt.Consume();
            }
        }

        private void HandleFurnaceSlotCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target)
        {
            pressedFurnaceSlots.Remove(evt.Interaction.ControlID);
            ResetSlotVisual(target);
            HideDragPreview();
        }

        private bool TransferPlayerSlotToFurnace(
            int index,
            FurnaceSlotType destination)
        {
            if (isTransferring ||
                !IsValidPlayerSlot(index) ||
                activeFurnace == null ||
                playerInventory == null)
            {
                return false;
            }

            InventorySlot slot = playerSlots[index];
            InventoryItemData item = slot.Item;
            int amount = slot.Amount;

            bool canAccept = destination switch
            {
                FurnaceSlotType.Fuel => activeFurnace.CanAcceptFuel(item, amount),
                FurnaceSlotType.Input => activeFurnace.CanAcceptInput(item, amount),
                _ => false
            };

            if (!canAccept)
            {
                return false;
            }

            isTransferring = true;
            bool removed = playerInventory.TryRemoveFromSlot(index, amount);
            bool added = removed && destination switch
            {
                FurnaceSlotType.Fuel => activeFurnace.TryAddFuel(item, amount),
                FurnaceSlotType.Input => activeFurnace.TryAddInput(item, amount),
                _ => false
            };

            if (removed && !added)
            {
                playerInventory.TryAddItem(item, amount, countForProgression: false);
            }

            isTransferring = false;
            RefreshAll();
            return added;
        }

        private bool TransferFurnaceSlotToPlayer(FurnaceSlotType slotType)
        {
            if (isTransferring || activeFurnace == null || playerInventory == null)
            {
                return false;
            }

            InventorySlot slot = GetFurnaceSlot(slotType);
            if (slot == null ||
                slot.IsEmpty ||
                !playerInventory.CanAcceptItem(slot.Item, slot.Amount))
            {
                return false;
            }

            InventoryItemData item = slot.Item;
            int amount = slot.Amount;
            isTransferring = true;

            bool removed = slotType switch
            {
                FurnaceSlotType.Fuel => activeFurnace.TryRemoveFuel(amount),
                FurnaceSlotType.Input => activeFurnace.TryRemoveInput(amount),
                FurnaceSlotType.Output => activeFurnace.TryRemoveOutput(amount),
                _ => false
            };
            bool added = removed &&
                         playerInventory.TryAddItem(
                             item,
                             amount,
                             countForProgression: false);

            if (removed && !added)
            {
                RestoreFurnaceSlot(slotType, item, amount);
            }

            isTransferring = false;
            RefreshAll();
            return added;
        }

        private InventorySlot GetFurnaceSlot(FurnaceSlotType slotType)
        {
            if (activeFurnace == null)
            {
                return null;
            }

            return slotType switch
            {
                FurnaceSlotType.Fuel => activeFurnace.FuelSlot,
                FurnaceSlotType.Input => activeFurnace.InputSlot,
                FurnaceSlotType.Output => activeFurnace.OutputSlot,
                _ => null
            };
        }

        private void RestoreFurnaceSlot(
            FurnaceSlotType slotType,
            InventoryItemData item,
            int amount)
        {
            if (slotType == FurnaceSlotType.Fuel)
            {
                activeFurnace.TryAddFuel(item, amount);
            }
            else if (slotType == FurnaceSlotType.Input)
            {
                activeFurnace.TryAddInput(item, amount);
            }
        }

        private bool TryGetFurnaceDropTarget(
            Ray pointerRay,
            out FurnaceSlotType slotType)
        {
            if (IsPointerOverTarget(pointerRay, fuelItemView?.transform))
            {
                slotType = FurnaceSlotType.Fuel;
                return true;
            }

            if (IsPointerOverTarget(pointerRay, inputItemView?.transform))
            {
                slotType = FurnaceSlotType.Input;
                return true;
            }

            slotType = default;
            return false;
        }

        private bool IsPointerOverTarget(Ray pointerRay, Transform target)
        {
            if (target == null)
            {
                return false;
            }

            uiBlockHits.Clear();
            Interaction.RaycastAll(pointerRay, uiBlockHits);

            for (int i = 0; i < uiBlockHits.Count; i++)
            {
                UIBlock hitBlock = uiBlockHits[i].UIBlock;
                if (hitBlock != null && hitBlock.transform.IsChildOf(target))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidPlayerSlot(int index)
        {
            return index >= 0 &&
                   index < playerSlots.Count &&
                   playerSlots[index] != null &&
                   !playerSlots[index].IsEmpty;
        }

        private static void ResetSlotVisual(InventoryItemVisuals target)
        {
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.DefaultColor;
            }
        }

        private void SetProgress(float normalizedProgress)
        {
            if (processingSlider == null)
            {
                return;
            }

            float minimum = Mathf.Min(emptySliderPercent, fullSliderPercent);
            float maximum = Mathf.Max(emptySliderPercent, fullSliderPercent);
            processingSlider.Size.X.Percent =
                Mathf.Lerp(minimum, maximum, Mathf.Clamp01(normalizedProgress));
        }

        private static void BindSlot(ItemView itemView, InventorySlot slot)
        {
            if (itemView != null &&
                itemView.TryGetVisuals(out InventoryItemVisuals visuals))
            {
                visuals.Bind(slot);
            }
        }

        private void Subscribe()
        {
            if (activeFurnace != null && !furnaceSubscribed)
            {
                activeFurnace.StateChanged += HandleFurnaceStateChanged;
                activeFurnace.ProgressChanged += SetProgress;
                furnaceSubscribed = true;
            }

            if (playerInventory != null && !playerInventorySubscribed)
            {
                playerInventory.InventoryUpdated += HandlePlayerInventoryChanged;
                playerInventorySubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (activeFurnace != null && furnaceSubscribed)
            {
                activeFurnace.StateChanged -= HandleFurnaceStateChanged;
                activeFurnace.ProgressChanged -= SetProgress;
            }

            if (playerInventory != null && playerInventorySubscribed)
            {
                playerInventory.InventoryUpdated -= HandlePlayerInventoryChanged;
            }

            furnaceSubscribed = false;
            playerInventorySubscribed = false;
        }

        private void HandleFurnaceStateChanged()
        {
            if (!isTransferring)
            {
                RefreshFurnaceSlots();
            }
        }

        private void HandlePlayerInventoryChanged()
        {
            if (!isTransferring)
            {
                RefreshPlayerInventory();
            }
        }

        private void RegisterFurnaceSlotHandlers()
        {
            slotTypes.Clear();
            RegisterFurnaceSlot(fuelItemView, FurnaceSlotType.Fuel);
            RegisterFurnaceSlot(inputItemView, FurnaceSlotType.Input);
            RegisterFurnaceSlot(outputItemView, FurnaceSlotType.Output);
        }

        private void RegisterFurnaceSlot(
            ItemView itemView,
            FurnaceSlotType slotType)
        {
            if (itemView == null ||
                registeredFurnaceSlots.Contains(itemView) ||
                !itemView.TryGetVisuals(out InventoryItemVisuals visuals))
            {
                return;
            }

            slotTypes[visuals] = slotType;
            itemView.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(
                InventoryItemVisuals.HandleHover);
            itemView.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(
                InventoryItemVisuals.HandleUnhover);
            itemView.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(
                HandleFurnaceSlotPressed);
            itemView.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(
                HandleFurnaceSlotReleased);
            itemView.UIBlock.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(
                HandleFurnaceSlotCanceled);
            registeredFurnaceSlots.Add(itemView);
        }

        private void UnregisterFurnaceSlotHandlers()
        {
            foreach (ItemView itemView in registeredFurnaceSlots)
            {
                if (itemView == null)
                {
                    continue;
                }

                itemView.UIBlock.RemoveGestureHandler<Gesture.OnHover, InventoryItemVisuals>(
                    InventoryItemVisuals.HandleHover);
                itemView.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(
                    InventoryItemVisuals.HandleUnhover);
                itemView.UIBlock.RemoveGestureHandler<Gesture.OnPress, InventoryItemVisuals>(
                    HandleFurnaceSlotPressed);
                itemView.UIBlock.RemoveGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(
                    HandleFurnaceSlotReleased);
                itemView.UIBlock.RemoveGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(
                    HandleFurnaceSlotCanceled);
            }

            registeredFurnaceSlots.Clear();
            slotTypes.Clear();
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
            if (camera == null ||
                !TryGetPointerScreenPosition(out Vector2 pointerPosition))
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

            GameObject previewObject = new("FurnaceInventoryDragPreview");
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
            fuelItemView ??= FindItemViewByName(FuelItemName);
            inputItemView ??= FindItemViewByName(InputItemName);
            outputItemView ??= FindItemViewByName(OutputItemName);
            playerGrid ??= FindGridByName(PlayerGridName);
            processingSlider ??= FindChildComponentByName<UIBlock2D>(SliderName);
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

        private ItemView FindItemViewByName(string objectName)
        {
            ItemView[] itemViews = GetComponentsInChildren<ItemView>(true);
            for (int i = 0; i < itemViews.Length; i++)
            {
                if (itemViews[i] != null && itemViews[i].name == objectName)
                {
                    return itemViews[i];
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

        public static FurnaceInventoryUIController FindOrCreateInScene()
        {
            FurnaceInventoryUIController existing =
                FindFirstObjectByType<FurnaceInventoryUIController>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = FindSceneObject(DefaultRootName);
            if (root == null)
            {
                Debug.LogWarning(
                    $"Could not find a scene object named {DefaultRootName}.");
                return null;
            }

            return root.AddComponent<FurnaceInventoryUIController>();
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
                    Transform found =
                        FindChildRecursive(rootObject.transform, objectName);
                    if (found != null)
                    {
                        return found.gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(
            Transform root,
            string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found =
                    FindChildRecursive(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
