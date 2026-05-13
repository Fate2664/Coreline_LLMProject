using Neocortex;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coreline.Robots
{
    public class RobotChatUIController : MonoBehaviour
    {
        private const string DefaultChatRootName = "LLMChatRoot";
        private const string PlayerTargetId = "player";

        [SerializeField] private NeocortexTextChatInput chatInput;
        [SerializeField] private NeocortexChatPanel chatPanel;
        [SerializeField] private OllamaRobotCommandClient commandClient;
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool unlockCursorWhileOpen = true;
        [SerializeField] private bool closeAfterSubmit;
        [SerializeField] private bool clearMessagesOnOpen;
        [SerializeField] private float playerReturnStoppingDistance = 2f;

        private bool isSubscribed;
        private bool isCommandClientSubscribed;
        private bool isOpen;
        private CommandTarget playerTarget;

        public static bool IsAnyOpen { get; private set; }
        public BaseRobotController ActiveRobot { get; private set; }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            if (hideOnStart)
            {
                Close();
            }
        }

        private void OnEnable()
        {
            SubscribeToInput();
            SubscribeToCommandClient();
        }

        private void OnDisable()
        {
            UnsubscribeFromInput();
            UnsubscribeFromCommandClient();

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

        public void OpenForRobot(BaseRobotController robot)
        {
            OpenForRobot(robot, FindFirstObjectByType<global::PlayerController>());
        }

        public void OpenForRobot(BaseRobotController robot, global::PlayerController player)
        {
            if (robot == null)
            {
                Debug.LogWarning($"{nameof(RobotChatUIController)} cannot open without a robot target.", this);
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureReferences();
            SubscribeToInput();
            SubscribeToCommandClient();
            EnsurePlayerTarget(player);

            ActiveRobot = robot;
            commandClient.SetTargetRobot(robot);
            isOpen = true;
            IsAnyOpen = true;

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
            ActiveRobot = null;
            isOpen = false;
            IsAnyOpen = false;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
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
        }

        private void EnsurePlayerTarget(global::PlayerController player)
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

            playerTarget.Configure(
                PlayerTargetId,
                CommandTargetType.Waypoint,
                destination: player.transform,
                radius: playerReturnStoppingDistance);
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
                Debug.LogWarning($"{nameof(RobotChatUIController)} could not find a {nameof(NeocortexTextChatInput)}.", this);
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
                Debug.Log($"[{nameof(RobotChatUIController)}] {message}", this);
                return;
            }

            chatPanel.AddMessage(message, isUser: false);
        }

        private static string BuildRejectedResponse(string reason)
        {
            string resource = ExtractResourceFromMissingResourceError(reason);
            if (!string.IsNullOrWhiteSpace(resource))
            {
                return $"I can't see any {resource} nodes in the area.";
            }

            if (IsInstructionUnderstandingError(reason))
            {
                return "I didn't understand your instruction. Please try again.";
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

        private static bool IsInstructionUnderstandingError(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return true;
            }

            return reason.Contains("parse", System.StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("JSON", System.StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("Unsupported robot action", System.StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("Unknown command target", System.StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("cannot execute action", System.StringComparison.OrdinalIgnoreCase);
        }

        public static RobotChatUIController FindOrCreateInScene()
        {
            RobotChatUIController existing = FindFirstObjectByType<RobotChatUIController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = FindSceneObject(DefaultChatRootName);
            if (root == null)
            {
                Debug.LogWarning($"Could not find a scene object named {DefaultChatRootName}.");
                return null;
            }

            return root.AddComponent<RobotChatUIController>();
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
