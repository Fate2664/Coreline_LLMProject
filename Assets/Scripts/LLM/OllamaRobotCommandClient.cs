using Neocortex;
using Neocortex.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Coreline.Robots
{
    public class OllamaRobotCommandClient : MonoBehaviour
    {
        [SerializeField] private string modelName = "llama3.2";
        [SerializeField] private BaseRobotController targetRobot;
        [SerializeField] private RobotCommandPromptBuilder promptBuilder;
        [SerializeField] private RobotCommandValidator commandValidator;
        [SerializeField] private CommandTargetRegistry targetRegistry;
        [SerializeField] private bool clearRobotQueueOnSubmit;
        [SerializeField] private bool validateBeforeSubmit = true;
        [SerializeField] private bool logRawResponses = true;
        [SerializeField] private bool logAcceptedCommands = true;

        public UnityEvent<string> OnPromptSubmitted = new();
        public UnityEvent<string> OnRawResponseReceived = new();
        public UnityEvent<string> OnCommandAccepted = new();
        public UnityEvent<string> OnCommandRejected = new();

        private OllamaRequest request;
        private string activePlayerPrompt = string.Empty;

        private CommandTargetRegistry Registry => targetRegistry != null ? targetRegistry : CommandTargetRegistry.Instance;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void Start()
        {
            InitializeRequest();
        }

        private void OnDestroy()
        {
            if (request != null)
            {
                request.OnChatResponseReceived -= OnChatResponseReceived;
            }
        }

        public void SetModelName(string value)
        {
            modelName = value;

            if (request != null)
            {
                request.ModelName = modelName;
            }
        }

        public void SetTargetRobot(BaseRobotController robot)
        {
            targetRobot = robot;
        }

        public void SubmitPrompt(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                Reject("Cannot submit an empty robot prompt.");
                return;
            }

            EnsureDependencies();

            if (request == null)
            {
                InitializeRequest();
            }

            if (request == null || string.IsNullOrWhiteSpace(modelName))
            {
                Reject("Ollama model name is not set.");
                return;
            }

            request.ModelName = modelName;
            string prompt = promptBuilder != null
                ? promptBuilder.BuildUserPrompt(playerPrompt, Registry, targetRobot)
                : playerPrompt;
            activePlayerPrompt = playerPrompt;
            OnPromptSubmitted.Invoke(playerPrompt);
            request.Send(prompt);
        }

        private void InitializeRequest()
        {
            if (request != null)
            {
                return;
            }

            EnsureDependencies();

            request = new OllamaRequest();
            request.ModelName = modelName;
            request.OnChatResponseReceived += OnChatResponseReceived;

            string systemPrompt = promptBuilder != null
                ? promptBuilder.BuildSystemPrompt(Registry, targetRobot)
                : "Return only robot command JSON for a Unity mining game.";

            request.AddSystemMessage(systemPrompt);
        }

        private void OnChatResponseReceived(ChatResponse response)
        {
            string rawResponse = response?.message ?? string.Empty;
            OnRawResponseReceived.Invoke(rawResponse);

            if (logRawResponses)
            {
                Debug.Log($"[{name}] Ollama robot command response: {rawResponse}");
            }

            if (!RobotCommandParser.TryParse(rawResponse, out RobotCommandSequence parsedSequence, out string parseError))
            {
                Reject(parseError);
                return;
            }

            ApplyPromptBasedNormalizations(parsedSequence, activePlayerPrompt, targetRobot);
            RobotCommandSequence sequenceToSubmit = parsedSequence;

            if (validateBeforeSubmit)
            {
                if (commandValidator == null)
                {
                    Reject($"No {nameof(RobotCommandValidator)} is assigned.");
                    return;
                }

                if (!commandValidator.TryValidate(parsedSequence, targetRobot, out sequenceToSubmit, out string validationError))
                {
                    Reject(validationError);
                    return;
                }
            }

            if (targetRobot == null)
            {
                Reject("No target robot is assigned.");
                return;
            }

            if (TryHandleImmediateStop(sequenceToSubmit))
            {
                return;
            }

            bool submitted = targetRobot.SubmitCommands(sequenceToSubmit, clearRobotQueueOnSubmit, validate: false);
            if (!submitted)
            {
                Reject("The target robot rejected the command sequence.");
                return;
            }

            string acceptedCommandJson = RobotCommandParser.ToJson(sequenceToSubmit);
            if (logAcceptedCommands)
            {
                Debug.Log($"[{name}] Robot command accepted: {acceptedCommandJson}");
            }

            OnCommandAccepted.Invoke(acceptedCommandJson);
        }

        private void Reject(string reason)
        {
            Debug.LogWarning($"[{name}] Robot command rejected: {reason}");
            OnCommandRejected.Invoke(reason);
        }

        private bool TryHandleImmediateStop(RobotCommandSequence sequence)
        {
            if (targetRobot == null ||
                sequence == null ||
                sequence.sequence == null ||
                sequence.sequence.Count != 1 ||
                sequence.sequence[0].ActionType != RobotCommandAction.Stop)
            {
                return false;
            }

            targetRobot.StopRobot();

            string acceptedCommandJson = RobotCommandParser.ToJson(sequence);
            if (logAcceptedCommands)
            {
                Debug.Log($"[{name}] Robot command accepted: {acceptedCommandJson}");
            }

            OnCommandAccepted.Invoke(acceptedCommandJson);
            return true;
        }

        private static void ApplyPromptBasedNormalizations(RobotCommandSequence sequence, string playerPrompt, BaseRobotController robot)
        {
            if (sequence == null || sequence.sequence == null)
            {
                return;
            }

            bool promptRequestsFollowPlayer = PromptRequestsFollowPlayer(playerPrompt);
            bool promptRequestsImmediateStop = PromptRequestsImmediateStop(playerPrompt);
            bool promptRequestsRepeat = PromptRequestsRepeat(playerPrompt);
            for (int i = 0; i < sequence.sequence.Count; i++)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null)
                {
                    continue;
                }

                command.Normalize();

                if (promptRequestsImmediateStop)
                {
                    command.action = "stop";
                    command.target = string.Empty;
                    command.resource = string.Empty;
                    command.amount = 1;
                    command.nodeCount = 0;
                    command.HasExplicitAmount = false;
                    command.Normalize();
                    continue;
                }

                if (promptRequestsFollowPlayer && command.ActionType == RobotCommandAction.Unknown)
                {
                    command.action = "follow";
                }
                else if (promptRequestsRepeat && command.ActionType == RobotCommandAction.Unknown)
                {
                    if (robot is MiningRobotController || PromptMentionsMining(playerPrompt))
                    {
                        command.action = "mine_resource";
                    }
                    else if (robot is CollectingRobotController || PromptMentionsCollecting(playerPrompt))
                    {
                        command.action = "pickup";
                    }
                }

                if (promptRequestsFollowPlayer &&
                    command.ActionType == RobotCommandAction.Move &&
                    IsPlayerTarget(command.target))
                {
                    command.action = "follow";
                }

                if (promptRequestsFollowPlayer &&
                    command.ActionType == RobotCommandAction.Follow &&
                    string.IsNullOrWhiteSpace(command.target))
                {
                    command.target = "player";
                }

                if (promptRequestsRepeat &&
                    (command.ActionType == RobotCommandAction.MineResource ||
                     command.ActionType == RobotCommandAction.Pickup))
                {
                    command.SetRepeating(true);
                }

                if (string.IsNullOrWhiteSpace(command.resource) && TryExtractResourceFromPrompt(playerPrompt, out string resource))
                {
                    command.resource = resource;
                }

                command.Normalize();
            }
        }

        private static bool PromptRequestsFollowPlayer(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.Trim().ToLowerInvariant();
            return normalizedPrompt == "follow" ||
                   normalizedPrompt == "follow me" ||
                   normalizedPrompt.Contains("follow me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("follow the player", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("follow player", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("stay with me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("stay near me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("stay by me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("come with me", System.StringComparison.Ordinal);
        }

        private static bool PromptRequestsImmediateStop(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.Trim().ToLowerInvariant();
            return normalizedPrompt == "stop" ||
                   normalizedPrompt == "halt" ||
                   normalizedPrompt == "wait there" ||
                   normalizedPrompt == "stay there" ||
                   normalizedPrompt.Contains("stop following", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("stop moving", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("stop what", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("cancel command", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("cancel current", System.StringComparison.Ordinal);
        }

        private static bool PromptRequestsRepeat(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.Trim().ToLowerInvariant();
            return normalizedPrompt.Contains("repeatingly", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("repeatedly", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("continously", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("continuously", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("continually", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("continue to", System.StringComparison.Ordinal) ||
                   normalizedPrompt.StartsWith("continue ", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains(" keep ", System.StringComparison.Ordinal) ||
                   normalizedPrompt.StartsWith("keep ", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("always ", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("whenever", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("as they respawn", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("when they respawn", System.StringComparison.Ordinal);
        }

        private static bool PromptMentionsMining(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("mine", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("mining", System.StringComparison.Ordinal);
        }

        private static bool PromptMentionsCollecting(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("collect", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("pickup", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("pick up", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("gather", System.StringComparison.Ordinal);
        }

        private static bool TryExtractResourceFromPrompt(string playerPrompt, out string resource)
        {
            resource = string.Empty;
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            foreach (string oreName in System.Enum.GetNames(typeof(OreType)))
            {
                string normalizedOreName = oreName.ToLowerInvariant();
                if (normalizedPrompt.Contains(normalizedOreName, System.StringComparison.Ordinal))
                {
                    resource = normalizedOreName;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlayerTarget(string target)
        {
            return string.Equals(RobotCommand.NormalizeToken(target), "player", System.StringComparison.Ordinal);
        }

        private void EnsureDependencies()
        {
            promptBuilder ??= GetComponent<RobotCommandPromptBuilder>();
            commandValidator ??= GetComponent<RobotCommandValidator>();
            targetRobot ??= FindFirstObjectByType<MiningRobotController>();
            targetRobot ??= FindFirstObjectByType<BaseRobotController>();
            targetRegistry ??= CommandTargetRegistry.Instance;
        }
    }
}
