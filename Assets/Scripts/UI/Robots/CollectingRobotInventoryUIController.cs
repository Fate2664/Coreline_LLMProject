using System.Collections.Generic;
using Coreline;
using Nova;
using UnityEngine;
using UnityEngine.SceneManagement;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Coreline.Robots
{
    public class CollectingRobotInventoryUIController : MonoBehaviour
    {
        private const string DefaultRootName = "CollectionRobotInventoryRoot";
        private const string RobotNameTextName = "RobotNameText";
        private const string CloseButtonName = "CloseButton";

        [SerializeField] private GridView grid;
        [SerializeField] private ItemView closeButtonRoot;
        [SerializeField] private TextBlock robotNameText;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private UIBlock2D visualRoot;

        [Header("Player Inventory")]
        [SerializeField] private Player player;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private InventoryPanel playerInventoryPanel;
        [SerializeField] private GameObject playerInventoryCloseButton;
        [SerializeField] private bool disablePlayerInventoryCloseButtonWhileOpen = true;
        [SerializeField] private bool closePlayerInventoryWhenRobotInventoryCloses = true;

        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;
        [SerializeField] private int slotCount = 24;
        [SerializeField] private int padding = 10;
        [SerializeField] private List<OreItemSO> oreItemDefinitions = new();
        [SerializeField, Min(0f)] private float openDuration = 0.25f;
        [SerializeField, Min(0f)] private float closeDuration = 0.2f;
        [SerializeField] private bool useUnscaledAnimationTime = true;

        [Header("Drag Preview")]
        [SerializeField] private UIBlock2D dragPreviewIcon;
        [SerializeField] private Camera dragPreviewCamera;
        [SerializeField] private Vector2 dragPreviewSize = new(56f, 56f);
        [SerializeField] private Vector2 dragPreviewOffset = new(22f, -22f);
        [SerializeField] private short dragPreviewZIndex = 2000;

        private readonly List<InventoryItem> items = new();
        private readonly List<RobotInventoryDisplaySource> itemSources = new();
        private readonly Dictionary<uint, int> pressedRobotSlotIndices = new();
        private readonly List<UIBlockHit> uiBlockHits = new();
        private bool gridInitialized;
        private bool closeButtonSubscribed;
        private bool isGameplayInputBlocked;
        private bool isOpen;
        private bool playerInventoryWasOpenOnOpen;
        private bool playerInventoryCloseButtonWasActive;
        private bool hasCachedPlayerInventoryCloseButtonState;
        private CollectingRobotController pausedRobot;
        private UIVisuals visuals;

        public static bool IsAnyOpen { get; private set; }
        public CollectingRobotController ActiveRobot { get; private set; }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            if (hideOnStart && !isOpen)
            {
                HideImmediate();
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeGrid();
            SubscribeToCloseButton();
        }

        private void OnDisable()
        {
            UnsubscribeFromActiveInventory();
            UnsubscribeFromCloseButton();
            UnregisterGameplayInputBlock();
            ReleasePausedRobot();
            visuals?.KillAnimation();

            if (isOpen)
            {
                isOpen = false;
                IsAnyOpen = false;
            }
        }

        private void LateUpdate()
        {
            if (!isOpen)
            {
                return;
            }

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            UpdateDragPreviewFromPointer();
        }

        public void ToggleForRobot(CollectingRobotController robot, Player playerContext)
        {
            if (isOpen && ActiveRobot == robot)
            {
                Close();
                return;
            }

            OpenForRobot(robot, playerContext);
        }

        public void OpenForRobot(CollectingRobotController robot, Player playerContext)
        {
            if (robot == null)
            {
                Debug.LogWarning($"{nameof(CollectingRobotInventoryUIController)} cannot open without a collecting robot.", this);
                return;
            }

            isOpen = true;
            IsAnyOpen = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureReferences();
            InitializeGrid();
            SubscribeToCloseButton();
            EnsurePlayerInventory(playerContext);
            OpenPlayerInventory();
            SetActiveRobot(robot);
            RefreshInventory();
            RefreshRobotNameText();
            RegisterGameplayInputBlock();
            ShowPanel();

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Close()
        {
            HideDragPreview();
            RestorePlayerInventoryState();
            UnsubscribeFromActiveInventory();
            UnregisterGameplayInputBlock();
            ReleasePausedRobot();
            ActiveRobot = null;
            isOpen = false;
            IsAnyOpen = false;
            HidePanel();
        }

        public void RefreshInventory()
        {
            BuildInventoryItems();

            if (grid == null || !grid.gameObject.activeInHierarchy)
            {
                return;
            }

            grid.Refresh();
        }

        private void SetActiveRobot(CollectingRobotController robot)
        {
            if (ActiveRobot != null && ActiveRobot.Inventory != null)
            {
                ActiveRobot.Inventory.InventoryChanged -= HandleInventoryChanged;
            }

            ReleasePausedRobot();
            ActiveRobot = robot;

            if (ActiveRobot != null && ActiveRobot.Inventory != null)
            {
                ActiveRobot.Inventory.InventoryChanged += HandleInventoryChanged;
            }

            SetPausedRobot(ActiveRobot);
        }

        private void SetPausedRobot(CollectingRobotController robot)
        {
            if (pausedRobot == robot)
            {
                return;
            }

            ReleasePausedRobot();
            pausedRobot = robot;
            pausedRobot?.PauseForInteraction();
        }

        private void ReleasePausedRobot()
        {
            if (pausedRobot == null)
            {
                return;
            }

            pausedRobot.ResumeFromInteraction();
            pausedRobot = null;
        }

        private void HandleInventoryChanged()
        {
            if (isOpen)
            {
                RefreshInventory();
            }
        }

        private void BuildInventoryItems()
        {
            items.Clear();
            itemSources.Clear();

            CollectingRobotInventory inventory = ActiveRobot != null ? ActiveRobot.Inventory : null;
            if (inventory != null)
            {
                AddItemStacks(inventory);
                AddResourceStacks(inventory);
            }

            int targetSlotCount = Mathf.Max(slotCount, items.Count);
            while (items.Count < targetSlotCount)
            {
                items.Add(new InventoryItem());
                itemSources.Add(RobotInventoryDisplaySource.Empty);
            }

            grid?.SetDataSource(items);
        }

        private void AddItemStacks(CollectingRobotInventory inventory)
        {
            foreach (RobotInventoryItemStack stack in inventory.ItemStacks)
            {
                if (stack?.item == null || stack.amount <= 0)
                {
                    continue;
                }

                items.Add(new InventoryItem
                {
                    item = stack.item,
                    count = stack.amount
                });
                itemSources.Add(RobotInventoryDisplaySource.FromItem(stack.item, stack.amount));
            }
        }

        private void AddResourceStacks(CollectingRobotInventory inventory)
        {
            foreach (RobotResourceStack stack in inventory.ResourceStacks)
            {
                if (stack == null || stack.amount <= 0 || !TryFindOreItemDefinition(stack.oreType, out OreItemSO itemData))
                {
                    continue;
                }

                items.Add(new InventoryItem
                {
                    item = itemData,
                    count = stack.amount
                });
                itemSources.Add(RobotInventoryDisplaySource.FromResource(stack.oreType, itemData, stack.amount));
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

        private void InitializeGrid()
        {
            if (gridInitialized || grid == null)
            {
                return;
            }

            grid.AddDataBinder<InventoryItem, InventoryItemVisuals>(BindItem);
            grid.SetSliceProvider(ProvideSlice);
            grid.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(InventoryItemVisuals.HandleHover);
            grid.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(InventoryItemVisuals.HandleUnhover);
            grid.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleRobotItemPressed);
            grid.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleRobotItemReleased);
            grid.AddGestureHandler<Gesture.OnCancel, InventoryItemVisuals>(HandleRobotItemCanceled);
            grid.SetDataSource(items);
            gridInitialized = true;
        }

        private void ProvideSlice(int sliceIndex, GridView gridView, ref GridSlice2D gridSlice)
        {
            gridSlice.Layout.AutoSize.Y = AutoSize.Shrink;
            gridSlice.AutoLayout.AutoSpace = true;
            gridSlice.Layout.Padding.Value = padding;
        }

        private static void BindItem(Data.OnBind<InventoryItem> evt, InventoryItemVisuals target, int index)
        {
            target.Bind(evt.UserData, null);
        }

        private void HandleRobotItemPressed(Gesture.OnPress evt, InventoryItemVisuals target, int index)
        {
            if (!IsValidTransferSlot(index))
            {
                return;
            }

            pressedRobotSlotIndices[evt.Interaction.ControlID] = index;

            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.PressedColor;
            }

            ShowDragPreview(items[index].item.itemDesc.Icon, evt.PointerWorldPosition, evt.Interaction.ControlID);
            evt.Consume();
        }

        private void HandleRobotItemReleased(Gesture.OnRelease evt, InventoryItemVisuals target, int index)
        {
            uint controlId = evt.Interaction.ControlID;

            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.HoverColor;
            }

            bool shouldTransfer = pressedRobotSlotIndices.TryGetValue(controlId, out int pressedIndex) &&
                                  IsPointerOverPlayerInventory(evt.Interaction.Ray);

            pressedRobotSlotIndices.Remove(controlId);

            HideDragPreview();

            if (shouldTransfer)
            {
                TransferSlotToPlayerInventory(pressedIndex);
                evt.Consume();
            }
        }

        private void HandleRobotItemCanceled(Gesture.OnCancel evt, InventoryItemVisuals target, int index)
        {
            pressedRobotSlotIndices.Remove(evt.Interaction.ControlID);
            HideDragPreview();

            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.DefaultColor;
            }
        }

        private bool IsValidTransferSlot(int index)
        {
            return index >= 0 &&
                   index < items.Count &&
                   index < itemSources.Count &&
                   !items[index].isEmpty &&
                   itemSources[index].SourceType != RobotInventoryDisplaySourceType.Empty;
        }

        private bool IsPointerOverPlayerInventory(Ray pointerRay)
        {
            if (playerInventoryPanel == null || playerInventoryPanel.Grid == null)
            {
                return false;
            }

            uiBlockHits.Clear();
            Interaction.RaycastAll(pointerRay, uiBlockHits);

            Transform playerGridTransform = playerInventoryPanel.Grid.transform;
            for (int i = 0; i < uiBlockHits.Count; i++)
            {
                UIBlock hitBlock = uiBlockHits[i].UIBlock;
                if (hitBlock != null && hitBlock.transform.IsChildOf(playerGridTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TransferSlotToPlayerInventory(int index)
        {
            if (!IsValidTransferSlot(index) || ActiveRobot == null || ActiveRobot.Inventory == null)
            {
                return false;
            }

            EnsurePlayerInventory(player);
            if (playerInventory == null)
            {
                Debug.LogWarning("Cannot transfer item because no player inventory was found.", this);
                return false;
            }

            RobotInventoryDisplaySource source = itemSources[index];
            InventoryItem item = items[index];
            if (!playerInventory.CanAcceptItem(item.item, source.Amount))
            {
                return false;
            }

            bool removedFromRobot = source.SourceType switch
            {
                RobotInventoryDisplaySourceType.ItemStack => ActiveRobot.Inventory.TryRemoveItem(source.Item, source.Amount),
                RobotInventoryDisplaySourceType.ResourceStack => ActiveRobot.Inventory.TryRemoveResource(source.OreType, source.Amount),
                _ => false
            };

            if (!removedFromRobot)
            {
                return false;
            }

            if (playerInventory.TryAddItem(item.item, source.Amount))
            {
                RefreshInventory();
                playerInventoryPanel?.Refresh();
                return true;
            }

            RestoreRobotInventorySource(source);
            RefreshInventory();
            return false;
        }

        private void ShowDragPreview(Sprite icon, Vector3 pointerWorldPosition, uint controlId)
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
            if (dragPreviewCamera == null)
            {
                dragPreviewCamera = FindFirstObjectByType<Camera>();
            }

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

        private void EnsurePlayerInventory(Player playerContext)
        {
            player ??= playerContext;
            playerInventory ??= player != null
                ? player.GetComponent<PlayerInventory>() ??
                  player.GetComponentInParent<PlayerInventory>()
                : null;
            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            playerInventoryPanel ??=
                FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);

            if (playerInventoryPanel != null && playerInventory != null)
            {
                playerInventoryPanel.Bind(playerInventory);
            }

            EnsurePlayerInventoryCloseButton();
        }

        private void OpenPlayerInventory()
        {
            EnsurePlayerInventory(player);
            playerInventoryWasOpenOnOpen =
                playerInventoryPanel != null && playerInventoryPanel.IsOpen;
            playerInventoryPanel?.Open();
            SetPlayerInventoryCloseButtonEnabled(false);
        }

        private void RestorePlayerInventoryState()
        {
            SetPlayerInventoryCloseButtonEnabled(true);

            if (closePlayerInventoryWhenRobotInventoryCloses &&
                playerInventoryPanel != null &&
                !playerInventoryWasOpenOnOpen)
            {
                playerInventoryPanel.Close();
            }

            playerInventoryWasOpenOnOpen = false;
        }

        private void SetPlayerInventoryCloseButtonEnabled(bool enabled)
        {
            EnsurePlayerInventoryCloseButton();

            if (!disablePlayerInventoryCloseButtonWhileOpen || playerInventoryCloseButton == null)
            {
                return;
            }

            if (!enabled)
            {
                playerInventoryCloseButtonWasActive = playerInventoryCloseButton.activeSelf;
                hasCachedPlayerInventoryCloseButtonState = true;
                playerInventoryCloseButton.SetActive(false);
                return;
            }

            if (hasCachedPlayerInventoryCloseButtonState)
            {
                playerInventoryCloseButton.SetActive(playerInventoryCloseButtonWasActive);
                hasCachedPlayerInventoryCloseButtonState = false;
            }
        }

        private void EnsurePlayerInventoryCloseButton()
        {
            if (playerInventoryCloseButton != null || playerInventoryPanel == null)
            {
                return;
            }

            Transform closeButton = FindChildRecursive(
                playerInventoryPanel.transform,
                CloseButtonName);
            if (closeButton != null)
            {
                playerInventoryCloseButton = closeButton.gameObject;
            }
        }

        private void SubscribeToCloseButton()
        {
            if (closeButtonSubscribed || closeButtonRoot == null)
            {
                return;
            }

            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            closeButtonRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);

            if (closeButtonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                visuals.OnClicked.AddListener(Close);
            }

            closeButtonSubscribed = true;
        }

        private void UnsubscribeFromCloseButton()
        {
            if (!closeButtonSubscribed || closeButtonRoot == null)
            {
                return;
            }

            closeButtonRoot.UIBlock.RemoveGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            closeButtonRoot.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            closeButtonRoot.UIBlock.RemoveGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            closeButtonRoot.UIBlock.RemoveGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);

            if (closeButtonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                visuals.OnClicked.RemoveListener(Close);
            }

            closeButtonSubscribed = false;
        }

        private void UnsubscribeFromActiveInventory()
        {
            if (ActiveRobot != null && ActiveRobot.Inventory != null)
            {
                ActiveRobot.Inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void RefreshRobotNameText()
        {
            if (robotNameText == null)
            {
                return;
            }

            robotNameText.Text = ActiveRobot != null ? ActiveRobot.RobotTargetId : string.Empty;
        }

        private void EnsureReferences()
        {
            visualRoot ??= GetComponent<UIBlock2D>();
            visualRoot ??= GetComponentInChildren<UIBlock2D>(true);
            visuals ??= new UIVisuals(visualRoot != null ? visualRoot.transform : transform);
            grid ??= GetComponentInChildren<GridView>(true);
            closeButtonRoot ??= FindChildItemViewWithVisuals<InventoryButtonVisuals>(CloseButtonName);
            robotNameText ??= FindChildComponentByName<TextBlock>(RobotNameTextName);
            uiStateController ??= UIStateController.Instance;
            uiStateController ??=
                FindFirstObjectByType<UIStateController>(FindObjectsInactive.Include);
        }

        private void ShowPanel()
        {
            EnsureReferences();
            visuals?.ShowUI(openDuration, useUnscaledAnimationTime);
        }

        private void HidePanel()
        {
            EnsureReferences();

            if (!gameObject.activeSelf)
            {
                return;
            }

            visuals?.HideUI(closeDuration, useUnscaledAnimationTime, () =>
            {
                if (!isOpen && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            });
        }

        private void HideImmediate()
        {
            EnsureReferences();
            visuals?.HideImmediate();

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private ItemView FindChildItemViewWithVisuals<TVisuals>(string preferredObjectName)
            where TVisuals : ItemVisuals
        {
            ItemView fallback = null;
            ItemView[] itemViews = GetComponentsInChildren<ItemView>(true);

            for (int i = 0; i < itemViews.Length; i++)
            {
                ItemView itemView = itemViews[i];
                if (itemView == null || !itemView.TryGetVisuals(out TVisuals _))
                {
                    continue;
                }

                if (itemView.name == preferredObjectName)
                {
                    return itemView;
                }

                fallback ??= itemView;
            }

            return fallback;
        }

        private T FindChildComponentByName<T>(string objectName) where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].name == objectName)
                {
                    return components[i];
                }
            }

            return null;
        }

        private void RegisterGameplayInputBlock()
        {
            if (isGameplayInputBlocked)
            {
                return;
            }

            EnsureReferences();

            if (uiStateController == null)
            {
                Debug.LogWarning(
                    $"{nameof(CollectingRobotInventoryUIController)} cannot block player input because no {nameof(UIStateController)} exists in the scene.",
                    this);
                return;
            }

            uiStateController.RegisterModalInputBlock(unlockCursorWhileOpen);
            isGameplayInputBlocked = true;
        }

        private void UnregisterGameplayInputBlock()
        {
            if (!isGameplayInputBlocked)
            {
                return;
            }

            uiStateController?.UnregisterModalInputBlock(unlockCursorWhileOpen);
            isGameplayInputBlocked = false;
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

                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObject in rootObjects)
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
            public static readonly RobotInventoryDisplaySource Empty = new(RobotInventoryDisplaySourceType.Empty, null, default, 0);

            public readonly RobotInventoryDisplaySourceType SourceType;
            public readonly InventoryItemData Item;
            public readonly OreType OreType;
            public readonly int Amount;

            private RobotInventoryDisplaySource(RobotInventoryDisplaySourceType sourceType, InventoryItemData item, OreType oreType, int amount)
            {
                SourceType = sourceType;
                Item = item;
                OreType = oreType;
                Amount = Mathf.Max(0, amount);
            }

            public static RobotInventoryDisplaySource FromItem(InventoryItemData item, int amount)
            {
                return new RobotInventoryDisplaySource(RobotInventoryDisplaySourceType.ItemStack, item, default, amount);
            }

            public static RobotInventoryDisplaySource FromResource(OreType oreType, InventoryItemData item, int amount)
            {
                return new RobotInventoryDisplaySource(RobotInventoryDisplaySourceType.ResourceStack, item, oreType, amount);
            }
        }
    }
}
