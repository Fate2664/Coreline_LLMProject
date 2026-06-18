using Neocortex;
using Nova;
using NovaSamples.UIControls;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coreline.Robots
{
    public class RobotChatUIController : MonoBehaviour
    {
        protected const string DefaultChatRootName = "LLMChatRoot";
        private const string MiningChatRootName = "MiningRobotChat";
        private const string CollectingChatRootName = "CollectionRobotChat";
        private const string PlayerTargetId = "player";
        private const string MiningRobotDropdownName = "DropDownSetting";
        private const string MiningRobotDropdownRootName = "RobotToFollowRoot";
        private const string NoMiningRobotSelection = "None";
        private const string RobotNameTextName = "RobotNameText";
        private const string RobotTypeTextName = "RobotTypeText";
        private const string TabRootName = "TabRoot";
        private const string BodyRootName = "Body";
        private const string TabSuffix = "Tab";
        private const string BodySuffix = "Body";

        [SerializeField] private NeocortexTextChatInput chatInput;
        [SerializeField] private NeocortexChatPanel chatPanel;
        [SerializeField] private ItemView miningRobotDropdownView;
        [SerializeField] private TextBlock robotTypeText;
        [SerializeField] private TextBlock robotNameText;
        [SerializeField] private TextField robotNameField;
        [SerializeField] private OllamaRobotCommandClient commandClient;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;
        [SerializeField] private bool closeAfterSubmit;
        [SerializeField] private bool clearMessagesOnOpen;
        [SerializeField] private bool hideMiningRobotDropdownWhenInactive = true;
        [SerializeField] private float playerReturnStoppingDistance = 2f;

        private bool isSubscribed;
        private bool isCommandClientSubscribed;
        private bool isMiningRobotDropdownSubscribed;
        private bool isGameplayInputBlocked;
        private bool isOpen;
        private CommandTarget playerTarget;
        private CommandTarget activeRobotTarget;
        private BaseRobotController pausedRobot;
        private string lastAppliedRobotName = string.Empty;
        private DropDownVisuals miningRobotDropdownVisuals;
        private RobotUpgradeUIController upgradeUI;
        private readonly List<UIBlock2D> tabButtons = new();
        private readonly List<GameObject> tabBodies = new();
        private bool tabHandlersRegistered;
        private int selectedTabIndex = -1;
        private readonly MultiOptionSetting miningRobotDropdownSetting = new()
        {
            Key = "collecting_robot_selected_mining_robot",
            Name = "Mining Robot"
        };
        private readonly List<MiningRobotController> miningRobotDropdownRobots = new();

        public static bool IsAnyOpen { get; private set; }
        public BaseRobotController ActiveRobot { get; private set; }
        protected virtual string RobotTypeLabel => "Robot";
        protected virtual bool ShowsMiningRobotDropdown => true;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            if (hideOnStart && !isOpen && !IsAnyOpen)
            {
                isOpen = false;
                IsAnyOpen = false;

                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeTabs();
        }

        private void OnDisable()
        {
            UnregisterTabHandlers();
            UnsubscribeFromInput();
            UnsubscribeFromCommandClient();
            UnsubscribeFromMiningRobotDropdown();
            UnregisterGameplayInputBlock();
            ReleasePausedRobot();

            if (isOpen)
            {
                isOpen = false;
                IsAnyOpen = AnyChatControllerOpen();
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

            SyncRobotNameFromEditableField();
        }

        public void OpenForRobot(BaseRobotController robot)
        {
            OpenForRobot(robot, FindFirstObjectByType<Coreline.Player>());
        }

        public void OpenForRobot(BaseRobotController robot, Coreline.Player player)
        {
            if (robot == null)
            {
                Debug.LogWarning($"{GetType().Name} cannot open without a robot target.", this);
                return;
            }

            if (!CanOpenForRobot(robot))
            {
                Debug.LogWarning($"{GetType().Name} cannot open for {robot.GetType().Name}.", this);
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureReferences();
            SelectTab(0);
            SubscribeToInput();
            SubscribeToCommandClient();
            SubscribeToMiningRobotDropdown();
            EnsurePlayerTarget(player);

            SetPausedRobot(robot);
            ActiveRobot = robot;
            activeRobotTarget = EnsureRobotTarget(robot);
            RefreshRobotTypeText();
            RefreshRobotNameText(activeRobotTarget);
            commandClient.SetTargetRobot(robot);
            RefreshMiningRobotDropdown(robot);
            isOpen = true;
            IsAnyOpen = true;
            ClosePlayerInventory();
            upgradeUI?.Bind(robot, player);
            OnOpenedForRobot(robot, player);
            RegisterGameplayInputBlock();

            if (clearMessagesOnOpen)
            {
                chatPanel?.ClearMessages();
            }

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Close()
        {
            upgradeUI?.Unbind();
            OnClosing();
            ReleasePausedRobot();
            ActiveRobot = null;
            activeRobotTarget = null;
            lastAppliedRobotName = string.Empty;
            UnregisterGameplayInputBlock();
            isOpen = false;
            IsAnyOpen = AnyChatControllerOpen();
            miningRobotDropdownVisuals?.Collapse();

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetPausedRobot(BaseRobotController robot)
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

        private void SubmitPrompt(string prompt)
        {
            if (!isOpen || ActiveRobot == null)
            {
                Debug.LogWarning("Cannot submit an LLM prompt because no robot is selected.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            EnsureReferences();
            chatPanel?.AddMessage(prompt, isUser: true);
            commandClient.SetTargetRobot(ActiveRobot);
            commandClient.SubmitPrompt(prompt);

            if (closeAfterSubmit && chatPanel == null)
            {
                Close();
            }
        }

        private void EnsureReferences()
        {
            chatInput ??= GetComponentInChildren<NeocortexTextChatInput>(true);
            chatPanel ??= GetComponentInChildren<NeocortexChatPanel>(true);
            Transform miningRobotDropdownRoot =
                FindChildRecursive(transform, MiningRobotDropdownRootName);
            miningRobotDropdownView ??=
                FindChildItemViewWithVisuals<DropDownVisuals>(
                    MiningRobotDropdownName,
                    miningRobotDropdownRoot);
            robotTypeText ??= FindChildComponentByName<TextBlock>(RobotTypeTextName);
            robotNameField ??= FindChildComponentByName<TextField>(RobotNameTextName);
            robotNameText ??= FindChildComponentByName<TextBlock>(RobotNameTextName);
            uiStateController ??= UIStateController.Instance;
            uiStateController ??=
                FindFirstObjectByType<UIStateController>(FindObjectsInactive.Include);

            if (miningRobotDropdownView != null)
            {
                miningRobotDropdownView.TryGetVisuals(out miningRobotDropdownVisuals);
            }

            if (robotNameField == null && robotNameText != null)
            {
                robotNameField = robotNameText.GetComponent<TextField>();
            }

            if (robotNameText == null && robotNameField != null)
            {
                robotNameText = robotNameField.TextBlock;
            }

            if (GetComponent<RobotCommandPromptBuilder>() == null)
            {
                gameObject.AddComponent<RobotCommandPromptBuilder>();
            }

            if (GetComponent<RobotCommandValidator>() == null)
            {
                gameObject.AddComponent<RobotCommandValidator>();
            }

            commandClient ??= GetComponent<OllamaRobotCommandClient>();
            if (commandClient == null)
            {
                commandClient = gameObject.AddComponent<OllamaRobotCommandClient>();
            }

            if (FindChildRecursive(transform, "UpgradesBody") != null)
            {
                upgradeUI ??= GetComponent<RobotUpgradeUIController>();
                upgradeUI ??= gameObject.AddComponent<RobotUpgradeUIController>();
            }
        }

        protected virtual bool CanOpenForRobot(BaseRobotController robot)
        {
            return robot != null;
        }

        protected virtual void OnOpenedForRobot(BaseRobotController robot, Coreline.Player player)
        {
        }

        protected virtual void OnClosing()
        {
        }

        protected virtual void OnTabSelected(string bodyName)
        {
        }

        private void EnsurePlayerTarget(Coreline.Player player)
        {
            if (player == null)
            {
                return;
            }

            playerTarget = player.GetComponent<CommandTarget>();
            if (playerTarget == null)
            {
                playerTarget = player.gameObject.AddComponent<CommandTarget>();
            }

            Transform playerDestination = player.PlayerCharacter != null
                ? player.PlayerCharacter.transform
                : player.transform;

            playerTarget.Configure(
                PlayerTargetId,
                CommandTargetType.Waypoint,
                destination: playerDestination,
                radius: playerReturnStoppingDistance);
        }

        private CommandTarget EnsureRobotTarget(BaseRobotController robot)
        {
            return robot != null ? robot.EnsureRobotCommandTarget() : null;
        }

        private void RefreshRobotNameText(CommandTarget robotTarget)
        {
            lastAppliedRobotName = robotTarget != null ? robotTarget.TargetId : string.Empty;
            SetRobotNameField(lastAppliedRobotName);
        }

        private void RefreshRobotTypeText()
        {
            if (robotTypeText != null)
            {
                robotTypeText.Text = RobotTypeLabel;
            }
        }

        private void SyncRobotNameFromEditableField()
        {
            if (!isOpen || ActiveRobot == null || activeRobotTarget == null)
            {
                return;
            }

            string currentName = GetRobotNameFieldValue();
            if (string.IsNullOrWhiteSpace(currentName) ||
                string.Equals(SanitizeRobotTargetId(currentName), lastAppliedRobotName, System.StringComparison.Ordinal))
            {
                return;
            }

            ApplyActiveRobotName(currentName);
        }

        private bool ApplyActiveRobotName(string robotName)
        {
            if (ActiveRobot == null || activeRobotTarget == null)
            {
                return false;
            }

            string targetId = SanitizeRobotTargetId(robotName);
            if (string.IsNullOrWhiteSpace(targetId))
            {
                SetRobotNameField(lastAppliedRobotName);
                return false;
            }

            if (CommandTargetRegistry.Instance != null &&
                CommandTargetRegistry.Instance.TryGetTarget(targetId, out CommandTarget existingTarget) &&
                existingTarget != activeRobotTarget)
            {
                SetRobotNameField(lastAppliedRobotName);
                return false;
            }

            activeRobotTarget.Configure(
                targetId,
                CommandTargetType.Robot,
                destination: ActiveRobot.transform);

            lastAppliedRobotName = activeRobotTarget.TargetId;
            SetRobotNameField(lastAppliedRobotName);
            return true;
        }

        private void SetRobotNameField(string value)
        {
            if (robotNameField != null)
            {
                robotNameField.Text = value;
            }

            if (robotNameText != null && (robotNameField == null || robotNameField.TextBlock != robotNameText))
            {
                robotNameText.Text = value;
            }
        }

        private string GetRobotNameFieldValue()
        {
            if (robotNameField != null)
            {
                return robotNameField.Text;
            }

            return robotNameText != null ? robotNameText.Text : string.Empty;
        }

        private static string SanitizeRobotTargetId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("\"", string.Empty).Replace("\\", string.Empty);
        }

        private void RefreshMiningRobotDropdown(BaseRobotController robot)
        {
            EnsureReferences();

            if (miningRobotDropdownView == null || miningRobotDropdownVisuals == null)
            {
                return;
            }

            if (!ShowsMiningRobotDropdown || robot is not CollectingRobotController collectingRobot)
            {
                miningRobotDropdownVisuals.Collapse();

                if (hideMiningRobotDropdownWhenInactive)
                {
                    miningRobotDropdownView.gameObject.SetActive(false);
                }

                return;
            }

            if (hideMiningRobotDropdownWhenInactive && !miningRobotDropdownView.gameObject.activeSelf)
            {
                miningRobotDropdownView.gameObject.SetActive(true);
            }

            RefreshMiningRobotDropdownOptions(collectingRobot);
        }

        private void RefreshMiningRobotDropdownOptions(CollectingRobotController collectingRobot)
        {
            miningRobotDropdownRobots.Clear();
            miningRobotDropdownRobots.Add(null);

            MiningRobotController[] miningRobots =
                FindObjectsByType<MiningRobotController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            List<MiningRobotController> sortedRobots = new(miningRobots);
            sortedRobots.RemoveAll(robot => robot == null || !robot.isActiveAndEnabled);
            sortedRobots.Sort((left, right) =>
                string.Compare(GetMiningRobotDropdownLabel(left), GetMiningRobotDropdownLabel(right), System.StringComparison.OrdinalIgnoreCase));

            int selectedIndex = 0;
            if (collectingRobot.SelectedMiningRobot != null)
            {
                int existingIndex = sortedRobots.IndexOf(collectingRobot.SelectedMiningRobot);
                selectedIndex = existingIndex >= 0 ? existingIndex + 1 : 0;
            }

            string[] options = new string[sortedRobots.Count + 1];
            options[0] = NoMiningRobotSelection;

            for (int i = 0; i < sortedRobots.Count; i++)
            {
                MiningRobotController miningRobot = sortedRobots[i];
                miningRobotDropdownRobots.Add(miningRobot);
                options[i + 1] = GetMiningRobotDropdownLabel(miningRobot);
            }

            miningRobotDropdownSetting.SetOptions(options, selectedIndex);
            collectingRobot.SetSelectedMiningRobot(miningRobotDropdownRobots[miningRobotDropdownSetting.SelectedIndex]);
            miningRobotDropdownVisuals.Refresh(miningRobotDropdownSetting);
        }

        private static string GetMiningRobotDropdownLabel(MiningRobotController miningRobot)
        {
            if (miningRobot == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(miningRobot.RobotTargetId)
                ? miningRobot.RobotTargetId
                : miningRobot.name;
        }

        private void SubscribeToInput()
        {
            if (isSubscribed)
            {
                return;
            }

            EnsureReferences();

            if (chatInput == null)
            {
                Debug.LogWarning($"{GetType().Name} could not find a {nameof(NeocortexTextChatInput)}.", this);
                return;
            }

            chatInput.OnSendButtonClicked.AddListener(SubmitPrompt);
            isSubscribed = true;
        }

        private void UnsubscribeFromInput()
        {
            if (!isSubscribed || chatInput == null)
            {
                return;
            }

            chatInput.OnSendButtonClicked.RemoveListener(SubmitPrompt);
            isSubscribed = false;
        }

        private void SubscribeToCommandClient()
        {
            if (isCommandClientSubscribed)
            {
                return;
            }

            EnsureReferences();

            if (commandClient == null)
            {
                return;
            }

            commandClient.OnCommandAccepted.AddListener(HandleCommandAccepted);
            commandClient.OnCommandRejected.AddListener(HandleCommandRejected);
            isCommandClientSubscribed = true;
        }

        private void UnsubscribeFromCommandClient()
        {
            if (!isCommandClientSubscribed || commandClient == null)
            {
                return;
            }

            commandClient.OnCommandAccepted.RemoveListener(HandleCommandAccepted);
            commandClient.OnCommandRejected.RemoveListener(HandleCommandRejected);
            isCommandClientSubscribed = false;
        }

        private void HandleCommandAccepted(string acceptedCommandJson)
        {
            AddRobotMessage("Message understood.");
        }

        private void HandleCommandRejected(string reason)
        {
            AddRobotMessage(BuildRejectedResponse(reason));
        }

        private void AddRobotMessage(string message)
        {
            if (chatPanel == null)
            {
                Debug.Log($"[{GetType().Name}] {message}", this);
                return;
            }

            chatPanel.AddMessage(message, isUser: false);
        }

        private static string BuildRejectedResponse(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason) &&
                reason.Contains("No chest is selected", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Please select a chest from the Chest to Deliver dropdown first.";
            }

            string resource = ExtractResourceFromMissingResourceError(reason);
            if (!string.IsNullOrWhiteSpace(resource))
            {
                return $"I cannot see any {resource}. Please place a scanning robot for me to see some {resource}.";
            }

            return "I didn't understand your instruction. Please try again.";
        }

        private static string ExtractResourceFromMissingResourceError(string reason)
        {
            const string prefix = "No visible resource node found for '";
            if (string.IsNullOrWhiteSpace(reason) || !reason.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            int start = prefix.Length;
            int end = reason.IndexOf('\'', start);
            return end > start ? reason.Substring(start, end - start) : string.Empty;
        }

        private void SubscribeToMiningRobotDropdown()
        {
            if (isMiningRobotDropdownSubscribed)
            {
                return;
            }

            EnsureReferences();

            if (miningRobotDropdownView == null || miningRobotDropdownVisuals == null)
            {
                return;
            }

            miningRobotDropdownView.UIBlock.AddGestureHandler<Gesture.OnHover, DropDownVisuals>(DropDownVisuals.HandleHover);
            miningRobotDropdownView.UIBlock.AddGestureHandler<Gesture.OnUnhover, DropDownVisuals>(DropDownVisuals.HandleUnhover);
            miningRobotDropdownView.UIBlock.AddGestureHandler<Gesture.OnPress, DropDownVisuals>(DropDownVisuals.HandlePress);
            miningRobotDropdownView.UIBlock.AddGestureHandler<Gesture.OnRelease, DropDownVisuals>(DropDownVisuals.HandleRelease);
            miningRobotDropdownView.UIBlock.AddGestureHandler<Gesture.OnClick, DropDownVisuals>(HandleMiningRobotDropdownClicked);

            miningRobotDropdownVisuals.OnSelectionChanged += HandleMiningRobotDropdownSelectionChanged;
            InputManager.OnPostClick += HandleMiningRobotDropdownPostClick;
            isMiningRobotDropdownSubscribed = true;
        }

        private void UnsubscribeFromMiningRobotDropdown()
        {
            if (!isMiningRobotDropdownSubscribed || miningRobotDropdownView == null)
            {
                return;
            }

            miningRobotDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnHover, DropDownVisuals>(DropDownVisuals.HandleHover);
            miningRobotDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnUnhover, DropDownVisuals>(DropDownVisuals.HandleUnhover);
            miningRobotDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnPress, DropDownVisuals>(DropDownVisuals.HandlePress);
            miningRobotDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnRelease, DropDownVisuals>(DropDownVisuals.HandleRelease);
            miningRobotDropdownView.UIBlock.RemoveGestureHandler<Gesture.OnClick, DropDownVisuals>(HandleMiningRobotDropdownClicked);

            if (miningRobotDropdownVisuals != null)
            {
                miningRobotDropdownVisuals.OnSelectionChanged -= HandleMiningRobotDropdownSelectionChanged;
            }

            InputManager.OnPostClick -= HandleMiningRobotDropdownPostClick;
            isMiningRobotDropdownSubscribed = false;
        }

        private void HandleMiningRobotDropdownClicked(Gesture.OnClick evt, DropDownVisuals target)
        {
            if (target.ExpandedRoot != null &&
                evt.Receiver != null &&
                evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
            {
                return;
            }

            if (ActiveRobot is not CollectingRobotController collectingRobot)
            {
                return;
            }

            RefreshMiningRobotDropdownOptions(collectingRobot);

            if (target.isExpanded)
            {
                target.Collapse();
            }
            else
            {
                target.Expand(miningRobotDropdownSetting);
            }

            evt.Consume();
        }

        private void HandleMiningRobotDropdownSelectionChanged(int selectedIndex, string selectedLabel)
        {
            if (ActiveRobot is not CollectingRobotController collectingRobot)
            {
                return;
            }

            MiningRobotController selectedRobot =
                selectedIndex >= 0 && selectedIndex < miningRobotDropdownRobots.Count
                    ? miningRobotDropdownRobots[selectedIndex]
                    : null;

            collectingRobot.SetSelectedMiningRobot(selectedRobot);
        }

        private void HandleMiningRobotDropdownPostClick(UIBlock clickedUIBlock)
        {
            if (miningRobotDropdownVisuals == null || !miningRobotDropdownVisuals.isExpanded)
            {
                return;
            }

            if (clickedUIBlock == null ||
                miningRobotDropdownView == null ||
                !clickedUIBlock.transform.IsChildOf(miningRobotDropdownView.transform))
            {
                miningRobotDropdownVisuals.Collapse();
            }
        }

        private ItemView FindChildItemViewWithVisuals<TVisuals>(
            string preferredObjectName,
            Transform searchRoot = null)
            where TVisuals : ItemVisuals
        {
            ItemView fallback = null;
            ItemView[] itemViews = searchRoot != null
                ? searchRoot.GetComponentsInChildren<ItemView>(true)
                : GetComponentsInChildren<ItemView>(true);

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

        public static RobotChatUIController FindOrCreateInScene()
        {
            return FindOrCreateInScene<RobotChatUIController>();
        }

        public static string GetExpectedRootNameForController<T>() where T : RobotChatUIController
        {
            return GetExpectedRootNameForController(typeof(T));
        }

        public static string GetExpectedRootNameForController(System.Type controllerType)
        {
            if (controllerType == typeof(MiningRobotChatUIController))
            {
                return MiningChatRootName;
            }

            if (controllerType == typeof(CollectingRobotChatUIController))
            {
                return CollectingChatRootName;
            }

            return DefaultChatRootName;
        }

        protected static T FindOrCreateInScene<T>() where T : RobotChatUIController
        {
            T existing = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            string expectedRootName = GetExpectedRootNameForController<T>();
            GameObject root = FindSceneObject(expectedRootName);
            if (root == null && !string.Equals(expectedRootName, DefaultChatRootName, System.StringComparison.Ordinal))
            {
                root = FindSceneObject(DefaultChatRootName);
            }

            if (root == null)
            {
                Debug.LogWarning(
                    $"Could not find a scene object named {expectedRootName} for {typeof(T).Name}. " +
                    $"Add the matching robot chat UI prefab to the scene.");
                return null;
            }

            RobotChatUIController existingController = root.GetComponent<RobotChatUIController>();
            if (existingController != null && existingController is not T)
            {
                Debug.LogWarning(
                    $"Found {root.name}, but it already has {existingController.GetType().Name}. " +
                    $"Expected {typeof(T).Name} on a separate {expectedRootName} UI root.",
                    existingController);
                return null;
            }

            return root.GetComponent<T>() ?? root.AddComponent<T>();
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
                    $"{GetType().Name} cannot block player input because no {nameof(UIStateController)} exists in the scene.",
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

        private void InitializeTabs()
        {
            ResolveTabBindings();
            RegisterTabHandlers();

            if (tabBodies.Count == 0)
            {
                return;
            }

            int initialTabIndex = selectedTabIndex;
            if (initialTabIndex < 0 || initialTabIndex >= tabBodies.Count)
            {
                initialTabIndex = FindActiveTabIndex();
            }

            SelectTab(initialTabIndex >= 0 ? initialTabIndex : 0);
        }

        private void ResolveTabBindings()
        {
            tabButtons.Clear();
            tabBodies.Clear();

            Transform tabRoot = FindChildRecursive(transform, TabRootName);
            Transform bodyRoot = FindChildRecursive(transform, BodyRootName);
            if (tabRoot == null || bodyRoot == null)
            {
                return;
            }

            for (int i = 0; i < tabRoot.childCount; i++)
            {
                Transform tab = tabRoot.GetChild(i);
                UIBlock2D tabButton = tab.GetComponent<UIBlock2D>();
                if (tabButton == null)
                {
                    Debug.LogWarning(
                        $"{GetType().Name} cannot register tab {tab.name} because it has no {nameof(UIBlock2D)}.",
                        tab);
                    continue;
                }

                string bodyName = GetMatchingBodyName(tab.name);
                Transform body = FindDirectChild(bodyRoot, bodyName);
                if (body == null)
                {
                    Debug.LogWarning(
                        $"{GetType().Name} could not find {bodyName} under {BodyRootName} for tab {tab.name}.",
                        this);
                    continue;
                }

                tabButtons.Add(tabButton);
                tabBodies.Add(body.gameObject);
            }
        }

        private void RegisterTabHandlers()
        {
            if (tabHandlersRegistered)
            {
                return;
            }

            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].AddGestureHandler<Gesture.OnClick>(HandleTabClicked);
            }

            tabHandlersRegistered = tabButtons.Count > 0;
        }

        private void UnregisterTabHandlers()
        {
            if (!tabHandlersRegistered)
            {
                return;
            }

            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] != null)
                {
                    tabButtons[i].RemoveGestureHandler<Gesture.OnClick>(HandleTabClicked);
                }
            }

            tabHandlersRegistered = false;
        }

        private void HandleTabClicked(Gesture.OnClick evt)
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] == evt.Receiver)
                {
                    SelectTab(i);
                    return;
                }
            }
        }

        private void SelectTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabBodies.Count)
            {
                return;
            }

            selectedTabIndex = tabIndex;

            for (int i = 0; i < tabBodies.Count; i++)
            {
                GameObject body = tabBodies[i];
                if (body != null && body.activeSelf != (i == selectedTabIndex))
                {
                    body.SetActive(i == selectedTabIndex);
                }
            }

            OnTabSelected(tabBodies[selectedTabIndex].name);

            if (string.Equals(
                    tabBodies[selectedTabIndex].name,
                    RobotUpgradeUIController.UpgradesBodyName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                upgradeUI?.RefreshAll();
            }
        }

        private int FindActiveTabIndex()
        {
            for (int i = 0; i < tabBodies.Count; i++)
            {
                if (tabBodies[i] != null && tabBodies[i].activeSelf)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetMatchingBodyName(string tabName)
        {
            if (tabName.EndsWith(TabSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                return tabName.Substring(0, tabName.Length - TabSuffix.Length) + BodySuffix;
            }

            return tabName + BodySuffix;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static void ClosePlayerInventory()
        {
            InventoryPanel inventoryPanel =
                FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);

            if (inventoryPanel != null && inventoryPanel.IsOpen)
            {
                inventoryPanel.Panel.Close();
            }
        }

        private static bool AnyChatControllerOpen()
        {
            RobotChatUIController[] controllers =
                FindObjectsByType<RobotChatUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].isOpen)
                {
                    return true;
                }
            }

            return false;
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
