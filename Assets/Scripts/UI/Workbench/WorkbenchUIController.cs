using System.Collections.Generic;
using Coreline;
using Nova;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coreline.Robots
{
    public class WorkbenchUIController : MonoBehaviour
    {
        private const string DefaultRootName = "WorkbenchRoot";
        private const string OptionsRootName = "OptionsRoot";
        private const string RequirementsRootName = "RequirmentsRoot";
        private const string RequirementsRootCorrectName = "RequirementsRoot";
        private const string CraftButtonName = "CraftButton";
        private const string CloseButtonName = "CloseButton";
        private const string CraftInfoTextName = "CraftItemInfo";
        private const string CraftButtonTextName = "CraftText";

        [Header("Recipes")]
        [SerializeField] private List<RobotCraftingRecipe> recipes = new();

        [Header("UI Roots")]
        [SerializeField] private UIBlock2D visualRoot;
        [SerializeField] private Transform optionsRoot;
        [SerializeField] private Transform requirementsRoot;
        [SerializeField] private List<ItemView> optionSlots = new();
        [SerializeField] private List<ItemView> requirementSlots = new();
        [SerializeField] private ItemView requirementSlotPrefab;

        [Header("UI Text")]
        [SerializeField] private TextBlock craftInfoText;
        [SerializeField] private TextBlock craftButtonText;
        [SerializeField] private TextBlock statusText;

        [Header("Buttons")]
        [SerializeField] private ItemView craftButtonRoot;
        [SerializeField] private ItemView closeButtonRoot;

        [Header("Crafting")]
        [SerializeField] private Player player;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private InventoryPanel playerInventoryPanel;

        [Header("Player Inventory")]
        [SerializeField] private GameObject playerInventoryCloseButton;
        [SerializeField] private bool disablePlayerInventoryCloseButtonWhileOpen = true;
        [SerializeField] private bool closePlayerInventoryWhenWorkbenchCloses = true;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;
        [SerializeField, Min(0f)] private float openDuration = 0.25f;
        [SerializeField, Min(0f)] private float closeDuration = 0.2f;
        [SerializeField] private bool useUnscaledAnimationTime = true;

        [Header("Slot Colours")]
        [SerializeField] private Color selectedOptionColor = new(0.9f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color unavailableOptionColor = new(0.2f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color requirementMetColor = new(0.2f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color requirementMissingColor = new(0.35f, 0.15f, 0.15f, 1f);

        [Header("Craft Button Colours")]
        [SerializeField] private Color craftButtonDefaultColor = new(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color craftButtonRequirementsMetColor = new(0.2f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color craftButtonRequirementsNotMetColor = new(0.45f, 0.15f, 0.15f, 1f);

        private readonly Dictionary<InventoryItemVisuals, int> optionVisualIndices = new();
        private readonly Dictionary<InventoryButtonVisuals, UnityEngine.Events.UnityAction> buttonActions = new();
        private readonly HashSet<ItemView> registeredOptionSlots = new();
        private readonly CraftingService craftingService = new();
        private int selectedRecipeIndex;
        private bool buttonsSubscribed;
        private bool isOpen;
        private bool hasAppliedCraftButtonColor;
        private Color appliedCraftButtonColor;
        private bool playerInventoryWasOpenOnOpen;
        private bool playerInventoryCloseButtonWasActive;
        private bool hasCachedPlayerInventoryCloseButtonState;
        private UIVisuals visuals;

        public static bool IsAnyOpen { get; private set; }
        public RobotCraftingRecipe SelectedRecipe => TryGetSelectedRecipe(out RobotCraftingRecipe recipe) ? recipe : null;

        private void Awake()
        {
            EnsureReferences();
            ClampSelectedRecipeIndex();
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
            SubscribeButtons();
            RegisterOptionSlotHandlers();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeButtons();
            UnregisterOptionSlotHandlers();
            visuals?.KillAnimation();

            if (isOpen)
            {
                RestorePlayerInventoryState();
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

            RefreshOptionSlots();
            RefreshRequirementSlots();
            RefreshCraftButton();
        }

        public void Toggle(Player playerContext)
        {
            if (isOpen)
            {
                Close();
                return;
            }

            Open(playerContext);
        }

        public void Open(Player playerContext)
        {
            player ??= playerContext;
            isOpen = true;
            IsAnyOpen = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureReferences();
            EnsurePlayerInventory(playerContext);
            OpenPlayerInventory();
            ClampSelectedRecipeIndex();
            hasAppliedCraftButtonColor = false;
            RefreshAll();
            ShowPanel();

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            RestorePlayerInventoryState();
            isOpen = false;
            IsAnyOpen = false;
            hasAppliedCraftButtonColor = false;
            HidePanel();
        }

        public void SelectRecipe(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= recipes.Count || recipes[recipeIndex] == null)
            {
                return;
            }

            selectedRecipeIndex = recipeIndex;
            SetStatus(string.Empty);
            RefreshAll();
        }

        public void TryCraftSelected()
        {
            if (!TryGetSelectedRecipe(out RobotCraftingRecipe recipe))
            {
                SetStatus("Select a robot to craft.");
                return;
            }

            EnsurePlayerInventory(player);
            if (playerInventory == null)
            {
                SetStatus("No player inventory found.");
                return;
            }

            CraftingResult result = craftingService.TryCraft(recipe, playerInventory);
            if (!result.Succeeded)
            {
                SetStatus(BuildCraftingFailureMessage(recipe, result));
                RefreshAll();
                return;
            }

            SetStatus($"Crafted {recipe.DisplayName}.");
            playerInventoryPanel?.Refresh();
            RefreshAll();
        }

        public void RefreshAll()
        {
            EnsureReferences();
            EnsurePlayerInventory(player);
            ClampSelectedRecipeIndex();
            RefreshOptionSlots();
            RefreshRequirementSlots();
            RefreshInfoPanel();
            RefreshCraftButton();
        }

        private bool TryGetSelectedRecipe(out RobotCraftingRecipe recipe)
        {
            recipe = null;
            if (selectedRecipeIndex < 0 || selectedRecipeIndex >= recipes.Count)
            {
                return false;
            }

            recipe = recipes[selectedRecipeIndex];
            return recipe != null;
        }

        private void ClampSelectedRecipeIndex()
        {
            if (recipes.Count <= 0)
            {
                selectedRecipeIndex = -1;
                return;
            }

            if (selectedRecipeIndex >= 0 &&
                selectedRecipeIndex < recipes.Count &&
                recipes[selectedRecipeIndex] != null)
            {
                return;
            }

            selectedRecipeIndex = -1;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i] != null)
                {
                    selectedRecipeIndex = i;
                    return;
                }
            }
        }

        private bool CanCraft(RobotCraftingRecipe recipe)
        {
            return craftingService.Evaluate(recipe, playerInventory).Succeeded;
        }

        private void RefreshOptionSlots()
        {
            EnsureOptionSlots();
            optionVisualIndices.Clear();

            for (int i = 0; i < optionSlots.Count; i++)
            {
                ItemView slot = optionSlots[i];
                if (slot == null)
                {
                    continue;
                }

                bool hasRecipe = i < recipes.Count && recipes[i] != null;
                slot.gameObject.SetActive(hasRecipe);

                if (!hasRecipe || !slot.TryGetVisuals(out InventoryItemVisuals visuals))
                {
                    continue;
                }

                optionVisualIndices[visuals] = i;
                BindOptionSlot(visuals, recipes[i], i == selectedRecipeIndex, CanCraft(recipes[i]));
            }
        }

        private void BindOptionSlot(InventoryItemVisuals visuals, RobotCraftingRecipe recipe, bool isSelected, bool canCraft)
        {
            if (visuals.ContentRoot != null)
            {
                visuals.ContentRoot.gameObject.SetActive(true);
            }

            if (visuals.Image != null && recipe.Icon != null)
            {
                visuals.Image.SetImage(recipe.Icon);
            }

            if (visuals.Count != null)
            {
                visuals.Count.Text = string.Empty;
            }

            if (visuals.ToolTipRoot != null)
            {
                visuals.ToolTipRoot.gameObject.SetActive(false);
            }

            if (visuals.ToolTipText != null)
            {
                visuals.ToolTipText.Text = recipe.DisplayName;
            }

            if (visuals.ItemRoot != null)
            {
                visuals.ItemRoot.Color = isSelected
                    ? selectedOptionColor
                    : canCraft
                        ? visuals.DefaultColor
                        : unavailableOptionColor;
            }
        }

        private void RefreshRequirementSlots()
        {
            EnsureRequirementSlots();

            if (!TryGetSelectedRecipe(out RobotCraftingRecipe recipe))
            {
                HideRequirementSlots();
                return;
            }

            int visibleRequirementCount = 0;
            foreach (CraftingIngredient requirement in recipe.Requirements)
            {
                if (IsValidRequirement(requirement))
                {
                    visibleRequirementCount++;
                }
            }

            CreateMissingRequirementSlots(visibleRequirementCount);

            int slotIndex = 0;
            foreach (CraftingIngredient requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement) || slotIndex >= requirementSlots.Count)
                {
                    continue;
                }

                ItemView slot = requirementSlots[slotIndex];
                slot.gameObject.SetActive(true);

                if (slot.TryGetVisuals(out InventoryItemVisuals visuals))
                {
                    BindRequirementSlot(visuals, requirement);
                }

                slotIndex++;
            }

            for (int i = slotIndex; i < requirementSlots.Count; i++)
            {
                if (requirementSlots[i] != null)
                {
                    requirementSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void BindRequirementSlot(InventoryItemVisuals visuals, CraftingIngredient requirement)
        {
            int owned = playerInventory != null ? playerInventory.GetItemCount(requirement.Item) : 0;
            bool isMet = owned >= requirement.Amount;

            if (visuals.ContentRoot != null)
            {
                visuals.ContentRoot.gameObject.SetActive(true);
            }

            if (visuals.Image != null && requirement.Item?.itemDesc?.Icon != null)
            {
                visuals.Image.SetImage(requirement.Item.itemDesc.Icon);
            }

            if (visuals.Count != null)
            {
                visuals.Count.Text = $"{owned}/{requirement.Amount}";
            }

            if (visuals.ToolTipRoot != null)
            {
                visuals.ToolTipRoot.gameObject.SetActive(false);
            }

            if (visuals.ToolTipText != null)
            {
                string itemName = requirement.Item?.itemDesc?.Name ?? requirement.Item?.name ?? "Item";
                visuals.ToolTipText.Text = $"{itemName}: {owned}/{requirement.Amount}";
            }

            if (visuals.ItemRoot != null)
            {
                visuals.ItemRoot.Color = isMet ? requirementMetColor : requirementMissingColor;
            }
        }

        private void HideRequirementSlots()
        {
            foreach (ItemView slot in requirementSlots)
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        private void CreateMissingRequirementSlots(int requiredCount)
        {
            if (requirementSlotPrefab == null || requirementsRoot == null)
            {
                return;
            }

            while (requirementSlots.Count < requiredCount)
            {
                ItemView slot = Instantiate(requirementSlotPrefab, requirementsRoot, false);
                requirementSlots.Add(slot);
            }
        }

        private void RefreshInfoPanel()
        {
            if (!TryGetSelectedRecipe(out RobotCraftingRecipe recipe))
            {
                if (craftInfoText != null)
                {
                    craftInfoText.Text = string.Empty;
                }

                return;
            }

            if (craftInfoText != null)
            {
                craftInfoText.Text = recipe.Description;
            }
        }

        private void RefreshCraftButton()
        {
            if (!TryGetSelectedRecipe(out RobotCraftingRecipe recipe))
            {
                SetCraftButtonColor(craftButtonDefaultColor);
                if (craftButtonText != null)
                {
                    craftButtonText.Text = "SELECT";
                }

                return;
            }

            bool canCraft = CanCraft(recipe);
            SetCraftButtonColor(canCraft ? craftButtonRequirementsMetColor : craftButtonRequirementsNotMetColor);

            if (craftButtonText != null)
            {
                craftButtonText.Text = "CRAFT";
            }
        }

        private void SetCraftButtonColor(Color color)
        {
            if (craftButtonRoot == null || !craftButtonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                return;
            }

            visuals.DefaultColor = color;
            if (hasAppliedCraftButtonColor && appliedCraftButtonColor == color)
            {
                return;
            }

            hasAppliedCraftButtonColor = true;
            appliedCraftButtonColor = color;

            if (visuals.ButtonRoot != null)
            {
                visuals.ButtonRoot.Color = color;
            }
        }

        private string BuildMissingRequirementsMessage(RobotCraftingRecipe recipe)
        {
            foreach (CraftingIngredient requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement))
                {
                    continue;
                }

                int owned = playerInventory != null ? playerInventory.GetItemCount(requirement.Item) : 0;
                if (owned < requirement.Amount)
                {
                    string itemName = requirement.Item?.itemDesc?.Name ?? requirement.Item?.name ?? "item";
                    return $"Need {requirement.Amount - owned} more {itemName}.";
                }
            }

            return "Missing required items.";
        }

        private string BuildCraftingFailureMessage(RobotCraftingRecipe recipe, CraftingResult result)
        {
            switch (result.FailureReason)
            {
                case CraftingFailureReason.MissingIngredients:
                    return BuildMissingRequirementsMessage(recipe);
                case CraftingFailureReason.InventoryFull:
                    return $"No inventory space for {recipe.DisplayName}.";
                case CraftingFailureReason.InvalidInventory:
                    return "No player inventory found.";
                case CraftingFailureReason.InvalidRecipe:
                    return "Recipe is not set up correctly.";
                case CraftingFailureReason.TransactionFailed:
                    return "Could not complete crafting.";
                default:
                    return "Could not craft item.";
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.Text = message;
            }
        }

        private static bool IsValidRequirement(CraftingIngredient requirement)
        {
            return requirement != null && requirement.IsValid;
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

            if (closePlayerInventoryWhenWorkbenchCloses &&
                playerInventoryPanel != null &&
                !playerInventoryWasOpenOnOpen)
            {
                playerInventoryPanel.Close();
            }

            playerInventoryWasOpenOnOpen = false;
        }

        private void SetPlayerInventoryCloseButtonEnabled(bool enabled)
        {
            if (!disablePlayerInventoryCloseButtonWhileOpen)
            {
                return;
            }

            EnsurePlayerInventoryCloseButton();
            if (playerInventoryCloseButton == null)
            {
                return;
            }

            if (!enabled)
            {
                if (!hasCachedPlayerInventoryCloseButtonState)
                {
                    playerInventoryCloseButtonWasActive = playerInventoryCloseButton.activeSelf;
                    hasCachedPlayerInventoryCloseButtonState = true;
                }

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
            if (playerInventoryCloseButton != null)
            {
                return;
            }

            Transform inventoryRoot = null;
            if (playerInventoryPanel != null)
            {
                inventoryRoot = playerInventoryPanel.transform;
            }

            if (inventoryRoot == null)
            {
                return;
            }

            Transform closeButton = FindChildRecursive(inventoryRoot, CloseButtonName);
            if (closeButton != null)
            {
                playerInventoryCloseButton = closeButton.gameObject;
            }
        }

        private void EnsureReferences()
        {
            visualRoot ??= GetComponent<UIBlock2D>();
            visualRoot ??= GetComponentInChildren<UIBlock2D>(true);
            visuals ??= new UIVisuals(visualRoot != null ? visualRoot.transform : transform);
            optionsRoot ??= FindChildTransform(OptionsRootName);
            requirementsRoot ??= FindChildTransform(RequirementsRootName) ?? FindChildTransform(RequirementsRootCorrectName);
            craftButtonRoot ??= FindChildItemViewWithVisuals<InventoryButtonVisuals>(CraftButtonName);
            closeButtonRoot ??= FindChildItemViewWithVisuals<InventoryButtonVisuals>(CloseButtonName);
            craftInfoText ??= FindChildComponentByName<TextBlock>(CraftInfoTextName);
            craftButtonText ??= FindChildComponentByName<TextBlock>(CraftButtonTextName);
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

        private void EnsureOptionSlots()
        {
            optionSlots.RemoveAll(slot => slot == null);

            if (optionSlots.Count > 0 || optionsRoot == null)
            {
                return;
            }

            ItemView[] views = optionsRoot.GetComponentsInChildren<ItemView>(true);
            foreach (ItemView view in views)
            {
                if (view != null && view.TryGetVisuals(out InventoryItemVisuals _))
                {
                    optionSlots.Add(view);
                }
            }
        }

        private void EnsureRequirementSlots()
        {
            requirementSlots.RemoveAll(slot => slot == null);

            if (requirementSlots.Count > 0 || requirementsRoot == null)
            {
                return;
            }

            ItemView[] views = requirementsRoot.GetComponentsInChildren<ItemView>(true);
            foreach (ItemView view in views)
            {
                if (view != null && view.TryGetVisuals(out InventoryItemVisuals _))
                {
                    requirementSlots.Add(view);
                }
            }
        }

        private void SubscribeButtons()
        {
            if (buttonsSubscribed)
            {
                return;
            }

            SubscribeButton(craftButtonRoot, TryCraftSelected);
            SubscribeButton(closeButtonRoot, Close);
            buttonsSubscribed = true;
        }

        private void UnsubscribeButtons()
        {
            if (!buttonsSubscribed)
            {
                return;
            }

            UnsubscribeButton(craftButtonRoot, TryCraftSelected);
            UnsubscribeButton(closeButtonRoot, Close);
            buttonsSubscribed = false;
        }

        private void SubscribeButton(ItemView buttonRoot, UnityEngine.Events.UnityAction action)
        {
            if (buttonRoot == null)
            {
                return;
            }

            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleButtonRelease);

            if (buttonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                buttonActions[visuals] = action;
            }
        }

        private void UnsubscribeButton(ItemView buttonRoot, UnityEngine.Events.UnityAction action)
        {
            if (buttonRoot == null)
            {
                return;
            }

            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(HandleButtonRelease);

            if (buttonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                buttonActions.Remove(visuals);
            }
        }

        private void HandleButtonRelease(Gesture.OnRelease evt, InventoryButtonVisuals target)
        {
            if (target.ButtonRoot != null)
            {
                target.ButtonRoot.Color = target.HoverColor;
            }

            if (buttonActions.TryGetValue(target, out UnityEngine.Events.UnityAction action))
            {
                action?.Invoke();
            }

            evt.Consume();
        }

        private void RegisterOptionSlotHandlers()
        {
            EnsureOptionSlots();

            foreach (ItemView slot in optionSlots)
            {
                if (slot == null || registeredOptionSlots.Contains(slot))
                {
                    continue;
                }

                slot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryItemVisuals>(HandleOptionHover);
                slot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(HandleOptionUnhover);
                slot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleOptionPress);
                slot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleOptionRelease);
                registeredOptionSlots.Add(slot);
            }
        }

        private void UnregisterOptionSlotHandlers()
        {
            foreach (ItemView slot in registeredOptionSlots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.UIBlock.RemoveGestureHandler<Gesture.OnHover, InventoryItemVisuals>(HandleOptionHover);
                slot.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, InventoryItemVisuals>(HandleOptionUnhover);
                slot.UIBlock.RemoveGestureHandler<Gesture.OnPress, InventoryItemVisuals>(HandleOptionPress);
                slot.UIBlock.RemoveGestureHandler<Gesture.OnRelease, InventoryItemVisuals>(HandleOptionRelease);
            }

            registeredOptionSlots.Clear();
        }

        private void HandleOptionHover(Gesture.OnHover evt, InventoryItemVisuals target)
        {
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.HoverColor;
            }
        }

        private void HandleOptionUnhover(Gesture.OnUnhover evt, InventoryItemVisuals target)
        {
            RefreshOptionSlots();
        }

        private void HandleOptionPress(Gesture.OnPress evt, InventoryItemVisuals target)
        {
            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = target.PressedColor;
            }

            evt.Consume();
        }

        private void HandleOptionRelease(Gesture.OnRelease evt, InventoryItemVisuals target)
        {
            if (optionVisualIndices.TryGetValue(target, out int recipeIndex))
            {
                SelectRecipe(recipeIndex);
            }

            evt.Consume();
        }

        private Transform FindChildTransform(string objectName)
        {
            Transform found = FindChildRecursive(transform, objectName);
            return found;
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

        public static WorkbenchUIController FindOrCreateInScene()
        {
            WorkbenchUIController existing =
                FindFirstObjectByType<WorkbenchUIController>(FindObjectsInactive.Include);
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

            return root.AddComponent<WorkbenchUIController>();
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
    }
}
