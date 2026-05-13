using System;
using UnityEngine;
using UnityEngine.AI;

namespace Coreline.Robots
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class BaseRobotController : MonoBehaviour
    {
        [SerializeField] private RobotCommandValidator commandValidator;
        [SerializeField] private RobotCommandQueue commandQueue;
        [SerializeField] private BaseRobotCommandExecutor commandExecutor;
        [SerializeField] private CommandTarget commandTarget;

        private NavMeshAgent agent;
        private RobotWorkState currentState = RobotWorkState.Idle;

        public event Action<RobotWorkState> StatusChanged;
        public event Action<string> ErrorRaised;

        public NavMeshAgent Agent => agent;
        public RobotCommandQueue CommandQueue => commandQueue;
        public BaseRobotCommandExecutor CommandExecutor => commandExecutor;
        public RobotCommandValidator CommandValidator => commandValidator;
        public CommandTarget CommandTarget => commandTarget;
        public string RobotTargetId => EnsureRobotCommandTarget().TargetId;
        public RobotWorkState CurrentState => currentState;
        protected virtual string RobotTargetIdPrefix => "Robot";

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            commandQueue = EnsureComponent(commandQueue);
            commandValidator = EnsureComponent(commandValidator);
            commandExecutor = commandExecutor != null ? commandExecutor : GetComponent<BaseRobotCommandExecutor>();

            if (commandExecutor == null)
            {
                commandExecutor = gameObject.AddComponent<BaseRobotCommandExecutor>();
            }

            EnsureRobotCommandTarget();
        }

        public CommandTarget EnsureRobotCommandTarget()
        {
            commandTarget = commandTarget != null ? commandTarget : GetComponent<CommandTarget>();
            if (commandTarget == null)
            {
                commandTarget = gameObject.AddComponent<CommandTarget>();
            }

            string id = commandTarget.ConfiguredTargetId;
            if (ShouldGenerateRobotTargetId(commandTarget, id))
            {
                id = GenerateUniqueRobotTargetId(commandTarget);
            }

            commandTarget.Configure(
                id,
                CommandTargetType.Robot,
                destination: transform);

            return commandTarget;
        }

        public virtual bool CanExecuteAction(RobotCommandAction action)
        {
            return action == RobotCommandAction.Move ||
                   action == RobotCommandAction.Wait ||
                   action == RobotCommandAction.Stop;
        }

        public bool SubmitCommands(RobotCommandSequence sequence, bool clearExisting = false, bool validate = true)
        {
            if (sequence == null || sequence.IsEmpty)
            {
                RaiseError("Cannot submit an empty robot command sequence.");
                return false;
            }

            RobotCommandSequence commandsToQueue = sequence;

            if (validate && commandValidator != null)
            {
                if (!commandValidator.TryValidate(sequence, this, out commandsToQueue, out string validationError))
                {
                    SetStatus(RobotWorkState.Idle);
                    RaiseError(validationError);
                    return false;
                }
            }

            if (clearExisting)
            {
                commandQueue.Clear();
            }

            commandQueue.EnqueueSequence(commandsToQueue);
            return true;
        }

        public bool SubmitCommand(RobotCommand command, bool clearExisting = false, bool validate = true)
        {
            return SubmitCommands(RobotCommandSequence.FromCommand(command), clearExisting, validate);
        }

        public void StopRobot()
        {
            commandQueue?.Clear();
            commandExecutor?.CancelCurrentCommand();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            SetStatus(RobotWorkState.Idle);
        }

        public void SetStatus(RobotWorkState state)
        {
            if (currentState == state)
                return;

            currentState = state;
            StatusChanged?.Invoke(currentState);
        }

        public void RaiseError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"[{name}] {error}");
                ErrorRaised?.Invoke(error);
            }
        }

        public virtual void HandleMovement()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            if (currentState == RobotWorkState.Walking)
            {
                agent.isStopped = false;
                return;
            }

            agent.isStopped = true;

            if (currentState == RobotWorkState.Idle && agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        public virtual void HandleMining()
        {
        }

        protected T EnsureComponent<T>(T existing) where T : Component
        {
            if (existing != null)
                return existing;

            if (TryGetComponent(out T found))
                return found;

            return gameObject.AddComponent<T>();
        }

        private bool ShouldGenerateRobotTargetId(CommandTarget target, string id)
        {
            return string.IsNullOrWhiteSpace(id) ||
                   IsGenericRobotTargetId(id) ||
                   IsTargetIdInUse(id, target);
        }

        private string GenerateUniqueRobotTargetId(CommandTarget self)
        {
            string prefix = RobotTargetIdPrefix;

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{prefix}_{i}";
                if (!IsTargetIdInUse(candidate, self))
                {
                    return candidate;
                }
            }

            return $"{prefix}_{Mathf.Abs(GetInstanceID())}";
        }

        private static bool IsGenericRobotTargetId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            return id.StartsWith("robot_", StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith("target_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTargetIdInUse(string id, CommandTarget self)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            CommandTarget[] targets = FindObjectsByType<CommandTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (CommandTarget target in targets)
            {
                if (target == null || target == self || !target.HasConfiguredTargetId)
                {
                    continue;
                }

                if (string.Equals(target.ConfiguredTargetId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
