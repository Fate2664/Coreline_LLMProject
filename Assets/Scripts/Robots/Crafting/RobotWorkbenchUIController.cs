using System.Collections.Generic;
using System.Text;
using Coreline;
using Nova;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Coreline.Robots
{
    public class RobotWorkbenchUIController : MonoBehaviour
    {
        private const string DefaultRootName = "WorkbenchRoot";
        private const string OptionsRootName = "OptionsRoot";
        private const string RequirementsRootName = "RequirmentsRoot";
        private const string RequirementsRootCorrectName = "RequirementsRoot";
        private const string CraftButtonName = "CraftButton";
        private const string CloseButtonName = "CloseButton";
        private const string RobotTypeTextName = "RobotTypeText";
        private const string CraftInfoTextName = "CraftItemInfo";
        private const string CraftButtonTextName = "CraftText";

        [Header("Recipes")]
        [SerializeField] private List<RobotCraftingRecipe> recipes = new();
        [SerializeField] private List<OreItemSO> oreItemDefinitions = new();

        [Header("UI Roots")]
        [SerializeField] private Transform optionsRoot;
        [SerializeField] private Transform requirementsRoot;
        [SerializeField] private List<ItemView> optionSlots = new();
        [SerializeField] private List<ItemView> requirementSlots = new();
        [SerializeField] private ItemView requirementSlotPrefab;

        [Header("UI Text")]
        [SerializeField] private TextBlock robotTypeText;
        [SerializeField] private TextBlock craftInfoText;
        [SerializeField] private TextBlock craftButtonText;
        [SerializeField] private TextBlock statusText;

        [Header("Buttons")]
        [SerializeField] private ItemView craftButtonRoot;
        [SerializeField] private ItemView closeButtonRoot;

        [Header("Crafting")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private UIManager playerInventory;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float spawnForwardOffset = 2.5f;
        [SerializeField] private float navMeshSampleRadius = 2f;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;

        [Header("Slot Colours")]
        [SerializeField] private Color selectedOptionColor = new(0.9f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color unavailableOptionColor = new(0.2f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color requirementMetColor = new(0.2f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color requirementMissingColor = new(0.35f, 0.15f, 0.15f, 1f);

        private readonly Dictionary<InventoryItemVisuals, int> optionVisualIndices = new();
        private readonly HashSet<ItemView> registeredOptionSlots = new();
        private readonly List<RobotCraftingRequirement> spentRequirements = new();
        private int selectedRecipeIndex;
        private bool buttonsSubscribed;
        private bool isOpen;

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
                gameObject.SetActive(false);
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

            if (isOpen)
            {
                isOpen = false;
                IsAnyOpen = false;
            }
        }

        private void LateUpdate()
        {
            if (!isOpen || !unlockCursorWhileOpen)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Toggle(PlayerController player)
        {
            if (isOpen)
            {
                Close();
                return;
            }

            Open(player);
        }

        public void Open(PlayerController player)
        {
            playerController ??= player;
            isOpen = true;
            IsAnyOpen = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureReferences();
            EnsurePlayerInventory(player);
            ClampSelectedRecipeIndex();
            RefreshAll();

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Close()
        {
            isOpen = false;
            IsAnyOpen = false;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
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

            EnsurePlayerInventory(playerController);
            if (playerInventory == null)
            {
                SetStatus("No player inventory found.");
                return;
            }

            if (recipe.RobotPrefab == null)
            {
                SetStatus($"{recipe.DisplayName} has no robot prefab assigned.");
                return;
            }

            if (!CanAfford(recipe))
            {
                SetStatus(BuildMissingRequirementsMessage(recipe));
                RefreshAll();
                return;
            }

            if (!TrySpendRequirements(recipe))
            {
                SetStatus("Could not spend the required ores.");
                RefreshAll();
                return;
            }

            GameObject craftedRobot = Instantiate(recipe.RobotPrefab, GetSpawnPosition(), GetSpawnRotation());
            BaseRobotController robotController = craftedRobot.GetComponent<BaseRobotController>();
            if (robotController != null)
            {
                robotController.EnsureRobotCommandTarget();
            }

            SetStatus($"Crafted {recipe.DisplayName}.");
            RefreshAll();
        }

        public void RefreshAll()
        {
            EnsureReferences();
            EnsurePlayerInventory(playerController);
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

        private bool CanAfford(RobotCraftingRecipe recipe)
        {
            if (playerInventory == null || recipe == null)
            {
                return false;
            }

            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement))
                {
                    continue;
                }

                if (playerInventory.GetOreCount(requirement.oreType) < requirement.amount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TrySpendRequirements(RobotCraftingRecipe recipe)
        {
            spentRequirements.Clear();

            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement))
                {
                    continue;
                }

                if (!playerInventory.TryRemoveOre(requirement.oreType, requirement.amount))
                {
                    RestoreSpentRequirements();
                    return false;
                }

                spentRequirements.Add(requirement);
            }

            spentRequirements.Clear();
            return true;
        }

        private void RestoreSpentRequirements()
        {
            foreach (RobotCraftingRequirement requirement in spentRequirements)
            {
                if (TryFindOreItemDefinition(requirement.oreType, out OreItemSO itemData))
                {
                    playerInventory.TryAddItemToInventory(itemData, requirement.amount);
                }
            }

            spentRequirements.Clear();
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 position;
            if (spawnPoint != null)
            {
                position = spawnPoint.position;
            }
            else if (playerController != null)
            {
                position = playerController.transform.position + playerController.transform.forward * spawnForwardOffset;
            }
            else
            {
                position = transform.position + transform.forward * spawnForwardOffset;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
        }

        private Quaternion GetSpawnRotation()
        {
            if (spawnPoint != null)
            {
                return spawnPoint.rotation;
            }

            if (playerController != null)
            {
                return Quaternion.Euler(0f, playerController.transform.eulerAngles.y, 0f);
            }

            return Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
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
                BindOptionSlot(visuals, recipes[i], i == selectedRecipeIndex, CanAfford(recipes[i]));
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
            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
            {
                if (IsValidRequirement(requirement))
                {
                    visibleRequirementCount++;
                }
            }

            CreateMissingRequirementSlots(visibleRequirementCount);

            int slotIndex = 0;
            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
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

        private void BindRequirementSlot(InventoryItemVisuals visuals, RobotCraftingRequirement requirement)
        {
            int owned = playerInventory != null ? playerInventory.GetOreCount(requirement.oreType) : 0;
            bool isMet = owned >= requirement.amount;

            if (visuals.ContentRoot != null)
            {
                visuals.ContentRoot.gameObject.SetActive(true);
            }

            if (visuals.Image != null && TryFindOreItemDefinition(requirement.oreType, out OreItemSO itemData))
            {
                visuals.Image.SetImage(itemData.itemDesc.Icon);
            }

            if (visuals.Count != null)
            {
                visuals.Count.Text = $"{owned}/{requirement.amount}";
            }

            if (visuals.ToolTipRoot != null)
            {
                visuals.ToolTipRoot.gameObject.SetActive(false);
            }

            if (visuals.ToolTipText != null)
            {
                visuals.ToolTipText.Text = $"{requirement.oreType}: {owned}/{requirement.amount}";
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
                if (robotTypeText != null)
                {
                    robotTypeText.Text = "No robot selected";
                }

                if (craftInfoText != null)
                {
                    craftInfoText.Text = "Create robot crafting recipes and assign them to this workbench.";
                }

                return;
            }

            if (robotTypeText != null)
            {
                robotTypeText.Text = recipe.DisplayName;
            }

            if (craftInfoText != null)
            {
                craftInfoText.Text = BuildInfoText(recipe);
            }
        }

        private void RefreshCraftButton()
        {
            if (craftButtonText == null)
            {
                return;
            }

            if (!TryGetSelectedRecipe(out RobotCraftingRecipe recipe))
            {
                craftButtonText.Text = "SELECT";
                return;
            }

            craftButtonText.Text = CanAfford(recipe) && recipe.RobotPrefab != null ? "CRAFT" : "MISSING";
        }

        private string BuildInfoText(RobotCraftingRecipe recipe)
        {
            StringBuilder builder = new();

            if (!string.IsNullOrWhiteSpace(recipe.Description))
            {
                builder.AppendLine(recipe.Description);
                builder.AppendLine();
            }

            builder.AppendLine("Requirements:");

            bool hasRequirements = false;
            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement))
                {
                    continue;
                }

                hasRequirements = true;
                int owned = playerInventory != null ? playerInventory.GetOreCount(requirement.oreType) : 0;
                builder.AppendLine($"{requirement.oreType}: {owned}/{requirement.amount}");
            }

            if (!hasRequirements)
            {
                builder.AppendLine("No ore cost configured.");
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildMissingRequirementsMessage(RobotCraftingRecipe recipe)
        {
            foreach (RobotCraftingRequirement requirement in recipe.Requirements)
            {
                if (!IsValidRequirement(requirement))
                {
                    continue;
                }

                int owned = playerInventory != null ? playerInventory.GetOreCount(requirement.oreType) : 0;
                if (owned < requirement.amount)
                {
                    return $"Need {requirement.amount - owned} more {requirement.oreType}.";
                }
            }

            return "Missing required ores.";
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.Text = message;
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

        private static bool IsValidRequirement(RobotCraftingRequirement requirement)
        {
            return requirement != null && requirement.amount > 0;
        }

        private void EnsurePlayerInventory(PlayerController player)
        {
            playerController ??= player;
            playerInventory ??= playerController != null
                ? playerController.InventoryUIManager
                : null;
            playerInventory ??= FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        }

        private void EnsureReferences()
        {
            optionsRoot ??= FindChildTransform(OptionsRootName);
            requirementsRoot ??= FindChildTransform(RequirementsRootName) ?? FindChildTransform(RequirementsRootCorrectName);
            craftButtonRoot ??= FindChildItemViewWithVisuals<InventoryButtonVisuals>(CraftButtonName);
            closeButtonRoot ??= FindChildItemViewWithVisuals<InventoryButtonVisuals>(CloseButtonName);
            robotTypeText ??= FindChildComponentByName<TextBlock>(RobotTypeTextName);
            craftInfoText ??= FindChildComponentByName<TextBlock>(CraftInfoTextName);
            craftButtonText ??= FindChildComponentByName<TextBlock>(CraftButtonTextName);
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

        private static void SubscribeButton(ItemView buttonRoot, UnityEngine.Events.UnityAction action)
        {
            if (buttonRoot == null)
            {
                return;
            }

            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            buttonRoot.UIBlock.AddGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);

            if (buttonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                visuals.OnClicked.AddListener(action);
            }
        }

        private static void UnsubscribeButton(ItemView buttonRoot, UnityEngine.Events.UnityAction action)
        {
            if (buttonRoot == null)
            {
                return;
            }

            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnHover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleHover);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, InventoryButtonVisuals>(InventoryButtonVisuals.HandleUnhover);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnPress, InventoryButtonVisuals>(InventoryButtonVisuals.HandlePress);
            buttonRoot.UIBlock.RemoveGestureHandler<Gesture.OnRelease, InventoryButtonVisuals>(InventoryButtonVisuals.HandleRelease);

            if (buttonRoot.TryGetVisuals(out InventoryButtonVisuals visuals))
            {
                visuals.OnClicked.RemoveListener(action);
            }
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

        public static RobotWorkbenchUIController FindOrCreateInScene()
        {
            RobotWorkbenchUIController existing =
                FindFirstObjectByType<RobotWorkbenchUIController>(FindObjectsInactive.Include);
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

            return root.AddComponent<RobotWorkbenchUIController>();
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
