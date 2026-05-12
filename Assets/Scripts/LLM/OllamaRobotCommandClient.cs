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
            string prompt = promptBuilder != null ? promptBuilder.BuildUserPrompt(playerPrompt, Registry) : playerPrompt;
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
                ? promptBuilder.BuildSystemPrompt(Registry)
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

        private void EnsureDependencies()
        {
            promptBuilder ??= GetComponent<RobotCommandPromptBuilder>();
            commandValidator ??= GetComponent<RobotCommandValidator>();
            targetRobot ??= FindFirstObjectByType<BaseRobotController>();
            targetRegistry ??= CommandTargetRegistry.Instance;
        }
    }
}
