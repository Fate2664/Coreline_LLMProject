using System.Collections.Generic;
using Coreline;
using Nova;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Coreline.Robots
{
    public class CollectingRobotInventoryUIController : MonoBehaviour
    {
        private const string DefaultRootName = "CollectionRobotChat";
        private const string RobotGridName = "CollectionRobotInventory";
        private const string PlayerGridName = "PlayerInventory";

        [Header("Embedded Grids")]
        [FormerlySerializedAs("grid")]
        [SerializeField] private GridView robotGrid;
        [SerializeField] private GridView playerGrid;
        [SerializeField, Min(0)] private int padding = 10;
        [FormerlySerializedAs("slotCount")]
        [SerializeField, Min(1)] private int robotSlotCount = 14;
        [SerializeField] private List<OreItemSO> oreItemDefinitions = new();

        [Header("Player")]
        [SerializeField] private Player player;
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Drag Preview")]
        [SerializeField] private UIBlock2D dragPreviewIcon;
        [SerializeField] private Camera dragPreviewCamera;
        [SerializeField] private Vector2 dragPreviewSize = new(56f, 56f);
        [SerializeField] private Vector2 dragPreviewOffset = new(22f, -22f);
        [SerializeField] private short dragPreviewZIndex = 2000;

        private readonly List<InventoryItem> robotItems = new();
        private readonly List<RobotInventoryDisplaySource> robotItemSources = new();
        private readonly List<InventorySlot> playerSlots = new();
        private readonly Dictionary<uint, int> pressedRobotSlotIndices = new();
        private readonly Dictionary<uint, int> pressedPlayerSlotIndices = new();
        private readonly List<UIBlockHit> uiBlockHits = new();

        private bool robotGridInitialized;
        private bool playerGridInitialized;
        private bool isBound;

        public static bool IsAnyOpen { get; private set; }
        public CollectingRobotController ActiveRobot { get; private set; }

        private void Awake()
        {
            EnsureReferences();
            InitializeGrids();
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeGrids();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            if (isBound)
            {
                UpdateDragPreviewFromPointer();
            }
        }

        public void Bind(CollectingRobotController robot, Player playerContext)
        {
            if (robot == null)
            {
                Debug.LogWarning(
                    $"{nameof(CollectingRobotInventoryUIController)} cannot bind without a collecting robot.",
                    this);
                return;
            }

            EnsureReferences();
            InitializeGrids();
            EnsurePlayerInventory(playerContext);

            if (ActiveRobot != robot)
            {
                UnsubscribeFromRobotInventory();
                ActiveRobot = robot;
                SubscribeToRobotInventory();
            }

            SubscribeToPlayerInventory();
            isBound = true;
            IsAnyOpen = true;
            RefreshAll();
        }

        public void Unbind()
        {
            HideDragPreview();
            pressedRobotSlotIndices.Clear();
            pressedPlayerSlotIndices.Clear();
            UnsubscribeFromRobotInventory();
            UnsubscribeFromPlayerInventory();
            ActiveRobot = null;
            isBound = false;
            IsAnyOpen = false;
        }

        // Compatibility entry points now bind the embedded inventory instead of opening a second window.
        public void OpenForRobot(CollectingRobotController robot, Player playerContext)
        {
            Bind(robot, playerContext);
        }

        public void ToggleForRobot(CollectingRobotController robot, Player playerContext)
        {
            if (isBound && ActiveRobot == robot)
            {
                Unbind();
                return;
            }

            Bind(robot, playerContext);
        }

        public void Close()
        {
            CollectingRobotChatUIController chatController =
                GetComponent<CollectingRobotChatUIController>() ??
                GetComponentInParent<CollectingRobotChatUIController>();

            if (chatController != null)
            {
                chatController.Close();
                return;
            }

            Unbind();
        }

        public void RefreshAll()
        {
            RefreshRobotInventory();
            RefreshPlayerInventory();
        }

        public void RefreshInventory()
        {
            RefreshAll();
        }

        private void RefreshRobotInventory()
        {
            BuildRobotInventoryItems();

            if (robotGridInitialized && robotGrid != null && robotGrid.gameObject.activeInHierarchy)
            {
                robotGrid.Refresh();
            }
        }

        private void RefreshPlayerInventory()
        {
            BuildPlayerInventorySlots();

            if (playerGridInitialized && playerGrid != null && playerGrid.gameObject.activeInHierarchy)
            {
                playerGrid.Refresh();
            }
        }

        private void BuildRobotInventoryItems()
        {
            robotItems.Clear();
            robotItemSources.Clear();

            CollectingRobotInventory inventory = ActiveRobot != null ? ActiveRobot.Inventory : null;
            if (inventory != null)
            {
                AddRobotItemStacks(inventory);
                AddRobotResourceStacks(inventory);
            }

            int targetSlotCount = Mathf.Max(robotSlotCount, robotItems.Count);
            while (robotItems.Count < targetSlotCount)
            {
                robotItems.Add(new InventoryItem());
                robotItemSources.Add(RobotInventoryDisplaySource.Empty);
            }

            robotGrid?.SetDataSource(robotItems);
        }

        private void BuildPlayerInventorySlots()
        {
            playerSlots.Clear();

            if (playerInventory != null)
            {
                IReadOnlyList<InventorySlot> slots = playerInventory.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    playerSlots.Add(slots[i]);
                }
            }

            playerGrid?.SetDataSource(playerSlots);
        }

        private void AddRobotItemStacks(CollectingRobotInventory inventory)
        {
            foreach (RobotInventoryItemStack stack in inventory.ItemStacks)
            {
                if (stack?.item == null || stack.amount <= 0)
                {
                    continue;
                }

                robotItems.Add(new InventoryItem
                {
                    item = stack.item,
                    count = stack.amount
                });
                robotItemSources.Add(RobotInventoryDisplaySource.FromItem(stack.item, stack.amount));
            }
        }

        private void AddRobotResourceStacks(CollectingRobotInventory inventory)
        {
            foreach (RobotResourceStack stack in inventory.ResourceStacks)
            {
                if (stack == null ||
                    stack.amount <= 0 ||
                    !TryFindOreItemDefinition(stack.oreType, out OreItemSO itemData))
                {
                    continue;
                }

                robotItems.Add(new InventoryItem
                {
                    item = itemData,
                    count = stack.amount
                });
                robotItemSources.Add(
                    RobotInventoryDisplaySource.FromResource(stack.oreType, itemData, stack.amount));
            }
        }

        private bool TryFindOreItemDefinition(OreType oreType, out OreItemSO itemData)
        {
            foreach (OreItemSO definition in oreItemDefinitions)
            {
                if (definition != null && definition.oreType == oreType)
                {
                    itemData = definition;
                    return true;
                }
            }

            OreItemSO[] loadedDefinitions = Resources.FindObjectsOfTypeAll<OreItemSO>();
            foreach (OreItemSO definition in loadedDefinitions)
            {
                if (definition != null && definition.oreType == oreType)
                {
                    itemData = definition;
                    return true;
                }
            }

            itemData = null;
            return false;
        }

        private void InitializeGrids()
        {
            InitializeRobotGrid();
            InitializePlayerGrid();
        }

        private void InitializeRobotGrid()
        {
            if (robotGridInitialized || robotGrid == null)
            {
                return;
            }

            robotGrid.AddDataBinder<InventoryItem, InventoryItemVisuals>(BindRobotItem);
            robotGrid.SetSliceProvider(ProvideSlice);
            robotGrid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            robotGrid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            robotGrid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleRobotItemPressed);
            robotGrid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleRobotItemReleased);
            robotGrid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandleRobotItemCanceled);
            robotGrid.SetDataSource(robotItems);
            robotGridInitialized = true;
        }

        private void InitializePlayerGrid()
        {
            if (playerGridInitialized || playerGrid == null)
            {
                return;
            }

            playerGrid.AddDataBinder<InventorySlot, InventoryItemVisuals>(BindPlayerItem);
            playerGrid.SetSliceProvider(ProvideSlice);
            playerGrid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            playerGrid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            playerGrid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandlePlayerItemPressed);
            playerGrid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandlePlayerItemReleased);
            playerGrid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandlePlayerItemCanceled);
            playerGrid.SetDataSource(playerSlots);
            playerGridInitialized = true;
        }

        private void ProvideSlice(int sliceIndex, GridView gridView, ref GridSlice2D gridSlice)
        {
            gridSlice.Layout.AutoSize.Y = AutoSize.Shrink;
            gridSlice.AutoLayout.AutoSpace = true;
            gridSlice.Layout.Padding.Value = padding;
        }

        private static void BindRobotItem(
            Data.OnBind<InventoryItem> evt,
            InventoryItemVisuals target,
            int index)
        {
            InventorySlot slot = new();
            slot.Add(evt.UserData.item, evt.UserData.count, InventorySlot.DefaultMaxStackSize);
            target.Bind(slot);
        }

        private static void BindPlayerItem(
            Data.OnBind<InventorySlot> evt,
            InventoryItemVisuals target,
            int index)
        {
            target.Bind(evt.UserData);
        }

        private void HandleRobotItemPressed(
            Gesture.OnPress evt,
            InventoryItemVisuals target,
            int index)
        {
            if (!IsValidRobotSlot(index))
            {
                return;
            }

            pressedRobotSlotIndices[evt.Interaction.ControlID] = index;
            target.OnPressVisualOnly();
            ShowDragPreview(robotItems[index].item.itemDesc.Icon, evt.PointerWorldPosition);
            evt.Consume();
        }

        private void HandleRobotItemReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target,
            int index)
        {
            uint controlId = evt.Interaction.ControlID;
            target.OnRelease();

            bool shouldTransfer =
                pressedRobotSlotIndices.TryGetValue(controlId, out int pressedIndex) &&
                IsPointerOverGrid(evt.Interaction.Ray, playerGrid);

            pressedRobotSlotIndices.Remove(controlId);
            HideDragPreview();

            if (shouldTransfer)
            {
                TransferRobotSlotToPlayer(pressedIndex);
                evt.Consume();
            }
        }

        private void HandleRobotItemCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target,
            int index)
        {
            pressedRobotSlotIndices.Remove(evt.Interaction.ControlID);
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.DefaultColor;
            }
            HideDragPreview();
        }

        private void HandlePlayerItemPressed(
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
            ShowDragPreview(playerSlots[index].Item.itemDesc.Icon, evt.PointerWorldPosition);
            evt.Consume();
        }

        private void HandlePlayerItemReleased(
            Gesture.OnRelease evt,
            InventoryItemVisuals target,
            int index)
        {
            uint controlId = evt.Interaction.ControlID;
            target.OnRelease();

            bool shouldTransfer =
                pressedPlayerSlotIndices.TryGetValue(controlId, out int pressedIndex) &&
                IsPointerOverGrid(evt.Interaction.Ray, robotGrid);

            pressedPlayerSlotIndices.Remove(controlId);
            HideDragPreview();

            if (shouldTransfer)
            {
                TransferPlayerSlotToRobot(pressedIndex);
                evt.Consume();
            }
        }

        private void HandlePlayerItemCanceled(
            Gesture.OnCancel evt,
            InventoryItemVisuals target,
            int index)
        {
            pressedPlayerSlotIndices.Remove(evt.Interaction.ControlID);
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.DefaultColor;
            }
            HideDragPreview();
        }

        private bool IsValidRobotSlot(int index)
        {
            return index >= 0 &&
                   index < robotItems.Count &&
                   index < robotItemSources.Count &&
                   !robotItems[index].isEmpty &&
                   robotItemSources[index].SourceType != RobotInventoryDisplaySourceType.Empty;
        }

        private bool IsValidPlayerSlot(int index)
        {
            return index >= 0 &&
                   index < playerSlots.Count &&
                   playerSlots[index] != null &&
                   !playerSlots[index].IsEmpty;
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

        private bool TransferRobotSlotToPlayer(int index)
        {
            if (!IsValidRobotSlot(index) ||
                ActiveRobot == null ||
                ActiveRobot.Inventory == null ||
                playerInventory == null)
            {
                return false;
            }

            RobotInventoryDisplaySource source = robotItemSources[index];
            InventoryItem item = robotItems[index];
            if (!playerInventory.CanAcceptItem(item.item, source.Amount))
            {
                return false;
            }

            bool removedFromRobot = source.SourceType switch
            {
                RobotInventoryDisplaySourceType.ItemStack =>
                    ActiveRobot.Inventory.TryRemoveItem(source.Item, source.Amount),
                RobotInventoryDisplaySourceType.ResourceStack =>
                    ActiveRobot.Inventory.TryRemoveResource(source.OreType, source.Amount),
                _ => false
            };

            if (!removedFromRobot)
            {
                return false;
            }

            if (playerInventory.TryAddItem(item.item, source.Amount))
            {
                RefreshAll();
                return true;
            }

            RestoreRobotInventorySource(source);
            RefreshAll();
            return false;
        }

        private bool TransferPlayerSlotToRobot(int index)
        {
            if (!IsValidPlayerSlot(index) ||
                ActiveRobot == null ||
                ActiveRobot.Inventory == null ||
                playerInventory == null)
            {
                return false;
            }

            InventorySlot slot = playerSlots[index];
            InventoryItemData item = slot.Item;
            int amount = slot.Amount;
            bool canAccept = item is OreItemSO oreItem
                ? ActiveRobot.Inventory.CanAcceptResource(oreItem.oreType, amount)
                : ActiveRobot.Inventory.CanAcceptItem(item, amount);

            if (!canAccept || !playerInventory.TryRemoveFromSlot(index, amount))
            {
                return false;
            }

            bool addedToRobot = item is OreItemSO ore
                ? ActiveRobot.Inventory.TryAddResource(ore.oreType, amount)
                : ActiveRobot.Inventory.TryAddItem(item, amount);

            if (addedToRobot)
            {
                RefreshAll();
                return true;
            }

            playerInventory.TryAddItem(item, amount, countForProgression: false);
            RefreshAll();
            return false;
        }

        private void RestoreRobotInventorySource(RobotInventoryDisplaySource source)
        {
            if (ActiveRobot == null || ActiveRobot.Inventory == null)
            {
                return;
            }

            switch (source.SourceType)
            {
                case RobotInventoryDisplaySourceType.ItemStack:
                    ActiveRobot.Inventory.TryAddItem(source.Item, source.Amount);
                    break;
                case RobotInventoryDisplaySourceType.ResourceStack:
                    ActiveRobot.Inventory.TryAddResource(source.OreType, source.Amount);
                    break;
            }
        }

        private void SubscribeToRobotInventory()
        {
            if (ActiveRobot?.Inventory != null)
            {
                ActiveRobot.Inventory.InventoryChanged -= HandleRobotInventoryChanged;
                ActiveRobot.Inventory.InventoryChanged += HandleRobotInventoryChanged;
            }
        }

        private void UnsubscribeFromRobotInventory()
        {
            if (ActiveRobot?.Inventory != null)
            {
                ActiveRobot.Inventory.InventoryChanged -= HandleRobotInventoryChanged;
            }
        }

        private void SubscribeToPlayerInventory()
        {
            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated -= HandlePlayerInventoryChanged;
                playerInventory.InventoryUpdated += HandlePlayerInventoryChanged;
            }
        }

        private void UnsubscribeFromPlayerInventory()
        {
            if (playerInventory != null)
            {
                playerInventory.InventoryUpdated -= HandlePlayerInventoryChanged;
            }
        }

        private void HandleRobotInventoryChanged()
        {
            if (isBound)
            {
                RefreshRobotInventory();
            }
        }

        private void HandlePlayerInventoryChanged()
        {
            if (isBound)
            {
                RefreshPlayerInventory();
            }
        }

        private void EnsurePlayerInventory(Player playerContext)
        {
            player = playerContext != null ? playerContext : player;
            playerInventory = player != null
                ? player.GetComponent<PlayerInventory>() ??
                  player.GetComponentInParent<PlayerInventory>()
                : playerInventory;
            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        }

        private void EnsureReferences()
        {
            robotGrid ??= FindGridByName(RobotGridName);
            playerGrid ??= FindGridByName(PlayerGridName);
            EnsurePlayerInventory(player);
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

            GameObject previewObject = new("CollectionRobotInventoryDragPreview");
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

        public static CollectingRobotInventoryUIController FindOrCreateInScene()
        {
            CollectingRobotInventoryUIController existing =
                FindFirstObjectByType<CollectingRobotInventoryUIController>(FindObjectsInactive.Include);
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

            return root.AddComponent<CollectingRobotInventoryUIController>();
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

        private enum RobotInventoryDisplaySourceType
        {
            Empty,
            ItemStack,
            ResourceStack
        }

        private readonly struct RobotInventoryDisplaySource
        {
            public static readonly RobotInventoryDisplaySource Empty =
                new(RobotInventoryDisplaySourceType.Empty, null, default, 0);

            public readonly RobotInventoryDisplaySourceType SourceType;
            public readonly InventoryItemData Item;
            public readonly OreType OreType;
            public readonly int Amount;

            private RobotInventoryDisplaySource(
                RobotInventoryDisplaySourceType sourceType,
                InventoryItemData item,
                OreType oreType,
                int amount)
            {
                SourceType = sourceType;
                Item = item;
                OreType = oreType;
                Amount = Mathf.Max(0, amount);
            }

            public static RobotInventoryDisplaySource FromItem(InventoryItemData item, int amount)
            {
                return new RobotInventoryDisplaySource(
                    RobotInventoryDisplaySourceType.ItemStack,
                    item,
                    default,
                    amount);
            }

            public static RobotInventoryDisplaySource FromResource(
                OreType oreType,
                InventoryItemData item,
                int amount)
            {
                return new RobotInventoryDisplaySource(
                    RobotInventoryDisplaySourceType.ResourceStack,
                    item,
                    oreType,
                    amount);
            }
        }
    }
}
