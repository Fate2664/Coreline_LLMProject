using System.Collections.Generic;
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

            ApplyPromptBasedNormalizations(parsedSequence, activePlayerPrompt, targetRobot, Registry);
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

            bool submitted = targetRobot.SubmitCommands(sequenceToSubmit, clearExisting: true, validate: false);
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

        private static void ApplyPromptBasedNormalizations(RobotCommandSequence sequence, string playerPrompt, BaseRobotController robot,
            CommandTargetRegistry registry)
        {
            if (sequence == null || sequence.sequence == null)
            {
                return;
            }

            bool promptRequestsFollowPlayer = PromptRequestsFollowPlayer(playerPrompt);
            bool promptRequestsImmediateStop = PromptRequestsImmediateStop(playerPrompt);
            bool promptRequestsRepeat = PromptRequestsRepeat(playerPrompt);
            List<string> promptResources = RobotCommand.ExtractResourceNames(playerPrompt);
            HashSet<string> prioritizedResources = ExtractPrioritizedResources(playerPrompt);
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

                if (string.IsNullOrWhiteSpace(command.resource) && promptResources.Count == 1)
                {
                    command.resource = promptResources[0];
                }

                command.Normalize();
                ApplyResourcePriority(command, prioritizedResources);
            }

            if (!promptRequestsImmediateStop)
            {
                NormalizeCollectingRobotFollowAndCollectCommands(sequence, playerPrompt, robot, promptResources);
                NormalizeCollectingRobotSelectedChestDelivery(sequence, playerPrompt, robot, promptResources);
                ExpandMiningResourceCommands(sequence, promptResources, prioritizedResources, robot, playerPrompt);
                ReorderPrioritizedMiningCommands(sequence);
                RemoveUnrequestedScannerMovementCommands(sequence, playerPrompt, robot, registry);
                RemoveUnrequestedMiningMovementCommands(sequence, playerPrompt, robot, registry);
            }
        }

        private static void NormalizeCollectingRobotFollowAndCollectCommands(RobotCommandSequence sequence, string playerPrompt,
            BaseRobotController robot, List<string> promptResources)
        {
            if (sequence == null ||
                sequence.sequence == null ||
                robot is not CollectingRobotController ||
                !PromptMentionsCollecting(playerPrompt) ||
                !PromptReferencesSelectedRobot(playerPrompt))
            {
                return;
            }

            RobotCommand pickupCommand = null;
            bool shouldRepeat = PromptRequestsRepeat(playerPrompt);

            for (int i = sequence.sequence.Count - 1; i >= 0; i--)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null)
                {
                    continue;
                }

                command.Normalize();

                if (command.ActionType == RobotCommandAction.Pickup)
                {
                    pickupCommand ??= command;
                    shouldRepeat |= command.IsRepeating;
                    continue;
                }

                if (command.ActionType == RobotCommandAction.Follow ||
                    command.ActionType == RobotCommandAction.Move && IsSelectedRobotOrPlayerTarget(command.target))
                {
                    sequence.sequence.RemoveAt(i);
                }
            }

            if (pickupCommand == null)
            {
                pickupCommand = new RobotCommand
                {
                    action = "pickup",
                    priority = "normal"
                };
                sequence.sequence.Insert(0, pickupCommand);
            }

            pickupCommand.action = "pickup";
            pickupCommand.target = "selected_mining_robot";
            if (string.IsNullOrWhiteSpace(pickupCommand.resource) && promptResources.Count == 1)
            {
                pickupCommand.resource = promptResources[0];
            }

            if (shouldRepeat)
            {
                pickupCommand.SetRepeating(true);
            }

            pickupCommand.Normalize();
        }

        private static void NormalizeCollectingRobotSelectedChestDelivery(
            RobotCommandSequence sequence,
            string playerPrompt,
            BaseRobotController robot,
            List<string> promptResources)
        {
            if (sequence == null ||
                sequence.sequence == null ||
                robot is not CollectingRobotController ||
                !PromptReferencesSelectedChest(playerPrompt) ||
                !PromptMentionsDelivery(playerPrompt))
            {
                return;
            }

            RobotCommand pickupCommand = null;
            RobotCommand deliveryCommand = null;

            for (int i = sequence.sequence.Count - 1; i >= 0; i--)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null)
                {
                    continue;
                }

                command.Normalize();
                if (command.ActionType == RobotCommandAction.Pickup)
                {
                    pickupCommand ??= command;
                    continue;
                }

                if (command.ActionType == RobotCommandAction.Deliver)
                {
                    deliveryCommand ??= command;
                    sequence.sequence.RemoveAt(i);
                }
            }

            if (pickupCommand == null && PromptMentionsCollecting(playerPrompt))
            {
                pickupCommand = new RobotCommand
                {
                    action = "pickup",
                    priority = "normal"
                };

                if (promptResources.Count == 1)
                {
                    pickupCommand.resource = promptResources[0];
                }

                pickupCommand.Normalize();
                sequence.sequence.Insert(0, pickupCommand);
            }

            deliveryCommand ??= new RobotCommand
            {
                action = "deliver",
                priority = "normal"
            };
            deliveryCommand.action = "deliver";
            deliveryCommand.target = "selected_chest";
            deliveryCommand.resource = string.Empty;
            deliveryCommand.Normalize();

            int pickupIndex = pickupCommand != null
                ? sequence.sequence.IndexOf(pickupCommand)
                : -1;
            int deliveryIndex = pickupIndex >= 0
                ? pickupIndex + 1
                : sequence.sequence.Count;
            sequence.sequence.Insert(deliveryIndex, deliveryCommand);
        }

        private static bool PromptReferencesSelectedRobot(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("this robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("selected robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("assigned robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("selected mining robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("assigned mining robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("mining robot", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("miner", System.StringComparison.Ordinal);
        }

        private static bool PromptReferencesSelectedChest(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("this chest", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("selected chest", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("chosen chest", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("assigned chest", System.StringComparison.Ordinal);
        }

        private static bool PromptMentionsDelivery(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("deliver", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("deposit", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("drop off", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("dropoff", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("put it in", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("put them in", System.StringComparison.Ordinal);
        }

        private static bool IsSelectedRobotOrPlayerTarget(string target)
        {
            string normalizedTarget = RobotCommand.NormalizeToken(target);
            return IsPlayerTarget(normalizedTarget) ||
                   normalizedTarget == "selected_mining_robot" ||
                   normalizedTarget == "selected_robot" ||
                   normalizedTarget == "this_robot" ||
                   normalizedTarget == "assigned_robot" ||
                   normalizedTarget == "miner" ||
                   normalizedTarget == "mining_robot";
        }

        private static void ExpandMiningResourceCommands(RobotCommandSequence sequence, List<string> promptResources,
            HashSet<string> prioritizedResources, BaseRobotController robot, string playerPrompt)
        {
            if (sequence == null || sequence.sequence == null)
            {
                return;
            }

            HashSet<string> existingMiningResources = new(System.StringComparer.OrdinalIgnoreCase);
            RobotCommand miningTemplate = null;
            int lastMiningCommandIndex = -1;

            for (int i = 0; i < sequence.sequence.Count; i++)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null)
                {
                    continue;
                }

                command.Normalize();
                if (command.ActionType != RobotCommandAction.MineResource)
                {
                    continue;
                }

                miningTemplate ??= command;
                lastMiningCommandIndex = i;

                List<string> commandResources = RobotCommand.ExtractResourceNames(command.resource);
                if (commandResources.Count == 0 &&
                    string.IsNullOrWhiteSpace(command.target) &&
                    promptResources.Count > 1)
                {
                    commandResources = promptResources;
                }

                if (commandResources.Count <= 1)
                {
                    if (commandResources.Count == 1)
                    {
                        existingMiningResources.Add(commandResources[0]);
                    }

                    ApplyResourcePriority(command, prioritizedResources);
                    continue;
                }

                sequence.sequence.RemoveAt(i);
                for (int j = commandResources.Count - 1; j >= 0; j--)
                {
                    RobotCommand splitCommand = command.Clone();
                    splitCommand.resource = commandResources[j];
                    splitCommand.target = string.Empty;
                    splitCommand.Normalize();
                    ApplyResourcePriority(splitCommand, prioritizedResources);
                    sequence.sequence.Insert(i, splitCommand);
                    existingMiningResources.Add(commandResources[j]);
                }

                i += commandResources.Count - 1;
                lastMiningCommandIndex = i;
            }

            bool shouldInferMissingMiningResources = promptResources.Count > 0 &&
                                                     (miningTemplate != null ||
                                                      robot is MiningRobotController ||
                                                      PromptMentionsMining(playerPrompt));
            if (!shouldInferMissingMiningResources)
            {
                return;
            }

            int insertIndex = lastMiningCommandIndex >= 0 ? lastMiningCommandIndex + 1 : 0;
            foreach (string resource in promptResources)
            {
                if (existingMiningResources.Contains(resource))
                {
                    continue;
                }

                RobotCommand inferredCommand = miningTemplate != null
                    ? miningTemplate.Clone()
                    : new RobotCommand { action = "mine_resource", priority = "normal" };

                inferredCommand.resource = resource;
                inferredCommand.target = string.Empty;
                inferredCommand.Normalize();
                ApplyResourcePriority(inferredCommand, prioritizedResources);
                sequence.sequence.Insert(insertIndex, inferredCommand);
                insertIndex++;
                existingMiningResources.Add(resource);
            }
        }

        private static void ApplyResourcePriority(RobotCommand command, HashSet<string> prioritizedResources)
        {
            if (command == null ||
                prioritizedResources == null ||
                prioritizedResources.Count == 0 ||
                command.ActionType != RobotCommandAction.MineResource ||
                !RobotCommand.TryNormalizeResourceName(command.resource, out string resource))
            {
                return;
            }

            if (prioritizedResources.Contains(resource))
            {
                command.priority = "high";
            }
            else if (command.PriorityType == RobotCommandPriority.High)
            {
                command.priority = "normal";
            }
        }

        private static void ReorderPrioritizedMiningCommands(RobotCommandSequence sequence)
        {
            if (sequence == null || sequence.sequence == null)
            {
                return;
            }

            List<int> miningIndexes = new();
            List<RobotCommand> highPriorityMiningCommands = new();
            List<RobotCommand> otherMiningCommands = new();

            for (int i = 0; i < sequence.sequence.Count; i++)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null || command.ActionType != RobotCommandAction.MineResource)
                {
                    continue;
                }

                miningIndexes.Add(i);
                if (command.PriorityType == RobotCommandPriority.High)
                {
                    highPriorityMiningCommands.Add(command);
                }
                else
                {
                    otherMiningCommands.Add(command);
                }
            }

            if (highPriorityMiningCommands.Count == 0 || otherMiningCommands.Count == 0)
            {
                return;
            }

            List<RobotCommand> reorderedMiningCommands = new(highPriorityMiningCommands.Count + otherMiningCommands.Count);
            reorderedMiningCommands.AddRange(highPriorityMiningCommands);
            reorderedMiningCommands.AddRange(otherMiningCommands);

            for (int i = 0; i < miningIndexes.Count; i++)
            {
                sequence.sequence[miningIndexes[i]] = reorderedMiningCommands[i];
            }
        }

        private static void RemoveUnrequestedScannerMovementCommands(RobotCommandSequence sequence, string playerPrompt,
            BaseRobotController robot, CommandTargetRegistry registry)
        {
            if (sequence == null ||
                sequence.sequence == null ||
                robot is not MiningRobotController ||
                PromptRequestsScannerMovement(playerPrompt) ||
                !SequenceContainsMiningCommand(sequence))
            {
                return;
            }

            for (int i = sequence.sequence.Count - 1; i >= 0; i--)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null ||
                    command.ActionType != RobotCommandAction.Move &&
                    command.ActionType != RobotCommandAction.Follow)
                {
                    continue;
                }

                if (IsScannerTarget(command.target, registry))
                {
                    sequence.sequence.RemoveAt(i);
                }
            }
        }

        private static bool SequenceContainsMiningCommand(RobotCommandSequence sequence)
        {
            foreach (RobotCommand command in sequence.sequence)
            {
                if (command != null && command.ActionType == RobotCommandAction.MineResource)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PromptRequestsScannerMovement(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            bool mentionsScanner = normalizedPrompt.Contains("scanner", System.StringComparison.Ordinal) ||
                                   normalizedPrompt.Contains("scanning robot", System.StringComparison.Ordinal);
            if (!mentionsScanner)
            {
                return false;
            }

            return normalizedPrompt.Contains("go to", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("move to", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("follow", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("return to", System.StringComparison.Ordinal);
        }

        private static bool IsScannerTarget(string targetId, CommandTargetRegistry registry)
        {
            string normalizedTarget = RobotCommand.NormalizeToken(targetId);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return false;
            }

            if (normalizedTarget == "scanner" ||
                normalizedTarget == "scanning_robot" ||
                normalizedTarget == "scanning robot" ||
                normalizedTarget.StartsWith("scanningrobot_", System.StringComparison.Ordinal))
            {
                return true;
            }

            return registry != null &&
                   registry.TryGetTarget(targetId, out CommandTarget target) &&
                   target != null &&
                   target.GetComponentInParent<ScanningRobotController>() != null;
        }

        private static void RemoveUnrequestedMiningMovementCommands(RobotCommandSequence sequence, string playerPrompt,
            BaseRobotController robot, CommandTargetRegistry registry)
        {
            if (sequence == null ||
                sequence.sequence == null ||
                robot is not MiningRobotController ||
                !SequenceContainsMiningCommand(sequence))
            {
                return;
            }

            for (int i = sequence.sequence.Count - 1; i >= 0; i--)
            {
                RobotCommand command = sequence.sequence[i];
                if (command == null ||
                    command.ActionType != RobotCommandAction.Move &&
                    command.ActionType != RobotCommandAction.Follow)
                {
                    continue;
                }

                if (!ShouldKeepMovementCommandInMiningSequence(command, playerPrompt, registry))
                {
                    sequence.sequence.RemoveAt(i);
                }
            }
        }

        private static bool ShouldKeepMovementCommandInMiningSequence(RobotCommand command, string playerPrompt, CommandTargetRegistry registry)
        {
            if (IsPlayerTarget(command.target))
            {
                return PromptRequestsFollowPlayer(playerPrompt) || PromptRequestsReturnToPlayer(playerPrompt);
            }

            if (registry != null && registry.TryGetTarget(command.target, out CommandTarget target))
            {
                if (target.TargetType == CommandTargetType.ResourceNode)
                {
                    return true;
                }

                return PromptMentionsTarget(playerPrompt, command.target);
            }

            return PromptMentionsTarget(playerPrompt, command.target);
        }

        private static bool PromptRequestsReturnToPlayer(string playerPrompt)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            return normalizedPrompt.Contains("return to me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("come back", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("come back to me", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("return to player", System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains("return to the player", System.StringComparison.Ordinal);
        }

        private static bool PromptMentionsTarget(string playerPrompt, string target)
        {
            if (string.IsNullOrWhiteSpace(playerPrompt) || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            string normalizedTarget = RobotCommand.NormalizeToken(target);
            string targetAsWords = normalizedTarget.Replace("_", " ");

            return normalizedPrompt.Contains(normalizedTarget, System.StringComparison.Ordinal) ||
                   normalizedPrompt.Contains(targetAsWords, System.StringComparison.Ordinal);
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

        private static HashSet<string> ExtractPrioritizedResources(string playerPrompt)
        {
            HashSet<string> result = new(System.StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(playerPrompt))
            {
                return result;
            }

            string normalizedPrompt = playerPrompt.ToLowerInvariant();
            foreach (string resource in RobotCommand.ExtractResourceNames(playerPrompt))
            {
                if (PromptPrioritizesResource(normalizedPrompt, resource))
                {
                    result.Add(resource);
                }
            }

            return result;
        }

        private static bool PromptPrioritizesResource(string normalizedPrompt, string resource)
        {
            foreach (string resourceAlias in ResourceAliases(resource))
            {
                if (normalizedPrompt.Contains($"prioritize {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"prioritize the {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"prioritise {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"prioritise the {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"priority {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"priority to {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"priority to the {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"prefer {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"prefer the {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"focus on {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"focus on the {resourceAlias}", System.StringComparison.Ordinal) ||
                    normalizedPrompt.Contains($"{resourceAlias} first", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ResourceAliases(string resource)
        {
            yield return resource;
            yield return $"{resource}s";
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
