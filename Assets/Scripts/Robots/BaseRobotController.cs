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

        private NavMeshAgent agent;
        private RobotWorkState currentState = RobotWorkState.Idle;

        public event Action<RobotWorkState> StatusChanged;
        public event Action<string> ErrorRaised;

        public NavMeshAgent Agent => agent;
        public RobotCommandQueue CommandQueue => commandQueue;
        public BaseRobotCommandExecutor CommandExecutor => commandExecutor;
        public RobotCommandValidator CommandValidator => commandValidator;
        public RobotWorkState CurrentState => currentState;

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
    }
}
