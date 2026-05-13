using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Coreline.Robots
{
    [RequireComponent(typeof(BaseRobotController))]
    public class BaseRobotCommandExecutor : MonoBehaviour
    {
        [SerializeField] private CommandTargetRegistry targetRegistry;
        [SerializeField] protected float defaultStoppingDistance = 1.5f;
        [SerializeField] protected float maxTravelTime = 45f;
        [SerializeField] protected float dynamicTargetRepathDistance = 0.5f;

        protected BaseRobotController robot;
        protected Coroutine activeRoutine;
        protected RobotCommand activeCommand;

        protected CommandTargetRegistry Registry => targetRegistry != null ? targetRegistry : CommandTargetRegistry.Instance;
        protected NavMeshAgent Agent => robot.Agent;


        protected virtual void Awake()
        {
            robot = GetComponent<BaseRobotController>();
            targetRegistry ??= CommandTargetRegistry.Instance;
        }

        protected virtual void Update()
        {
            if (activeRoutine != null || robot.CommandQueue == null)
            {
                return;
            }

            if (robot.CommandQueue.TryDequeue(out RobotCommand command))
            {
                activeRoutine = StartCoroutine(ExecuteCommand(command));
            }
            else if (robot.CurrentState != RobotWorkState.Idle)
            {
                robot.SetStatus(RobotWorkState.Idle);
            }
        }

        public virtual void CancelCurrentCommand()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            activeCommand = null;
            StopAgent();
        }

        protected IEnumerator ExecuteCommand(RobotCommand command)
        {
            activeCommand = command;
            command.Normalize();

            yield return ExecuteCommandByAction(command);

            activeCommand = null;
            activeRoutine = null;

            if (robot.CommandQueue == null || !robot.CommandQueue.HasCommands)
            {
                robot.SetStatus(RobotWorkState.Idle);
            }
        }

        protected virtual IEnumerator ExecuteCommandByAction(RobotCommand command)
        {
            switch (command.ActionType)
            {
                case RobotCommandAction.Move:
                    yield return ExecuteMove(command);
                    break;
                case RobotCommandAction.Wait:
                    yield return ExecuteWait(command);
                    break;
                case RobotCommandAction.Stop:
                    robot.CommandQueue.Clear();
                    StopAgent();
                    robot.SetStatus(RobotWorkState.Idle);
                    break;
                default:
                    robot.RaiseError($"Cannot execute unsupported action '{command.action}'.");
                    break;
            }
        }

        protected virtual IEnumerator ExecuteMove(RobotCommand command)
        {
            if (!TryResolveTarget(command, out CommandTarget target))
            {
                yield break;
            }

            yield return MoveToTarget(target, Mathf.Max(defaultStoppingDistance, target.InteractionRadius), _ => { });
        }

        protected virtual IEnumerator ExecuteWait(RobotCommand command)
        {
            robot.SetStatus(RobotWorkState.Idle);
            StopAgent();
            yield return new WaitForSeconds(Mathf.Max(1, command.amount));
        }

        protected IEnumerator MoveToTarget(CommandTarget target, float stoppingDistance, Action<bool> onComplete)
        {
            onComplete ??= _ => { };

            if (target == null)
            {
                onComplete(false);
                yield break;
            }

            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            {
                robot.RaiseError("Robot NavMeshAgent is not active on a NavMesh.");
                onComplete(false);
                yield break;
            }

            robot.SetStatus(RobotWorkState.Walking);

            float previousStoppingDistance = Agent.stoppingDistance;
            Agent.stoppingDistance = Mathf.Max(0.05f, stoppingDistance);
            Agent.isStopped = false;

            Vector3 currentDestination = target.DestinationPosition;
            bool pathStarted = Agent.SetDestination(currentDestination);
            if (!pathStarted)
            {
                robot.RaiseError($"Could not path to target '{target.TargetId}'.");
                Agent.stoppingDistance = previousStoppingDistance;
                onComplete(false);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < maxTravelTime)
            {
                if (target == null)
                {
                    Agent.stoppingDistance = previousStoppingDistance;
                    onComplete(false);
                    yield break;
                }

                Vector3 targetDestination = target.DestinationPosition;
                float repathDistanceSqr = dynamicTargetRepathDistance * dynamicTargetRepathDistance;
                if (!Agent.pathPending && (targetDestination - currentDestination).sqrMagnitude >= repathDistanceSqr)
                {
                    currentDestination = targetDestination;
                    Agent.SetDestination(currentDestination);
                }

                if (!Agent.pathPending && Agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    robot.RaiseError($"No valid path to target '{target.TargetId}'.");
                    Agent.stoppingDistance = previousStoppingDistance;
                    onComplete(false);
                    yield break;
                }

                if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.05f)
                {
                    if (!Agent.hasPath || Agent.velocity.sqrMagnitude <= 0.05f)
                    {
                        Agent.stoppingDistance = previousStoppingDistance;
                        onComplete(true);
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            robot.RaiseError($"Timed out moving to target '{target.TargetId}'.");
            Agent.stoppingDistance = previousStoppingDistance;
            onComplete(false);
        }

        protected virtual bool TryResolveTarget(RobotCommand command, out CommandTarget target)
        {
            target = null;

            CommandTargetRegistry registry = Registry;
            if (registry == null)
            {
                robot.RaiseError($"No {nameof(CommandTargetRegistry)} exists in the scene.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(command.target) && registry.TryGetTarget(command.target, transform.position, out target))
            {
                return true;
            }

            if (command.ActionType == RobotCommandAction.MineResource)
            {
                return registry.TryFindNearestResource(command.resource, transform.position, out target);
            }

            if (command.ActionType == RobotCommandAction.Pickup)
            {
                return registry.TryFindNearestPickup(command.resource, transform.position, out target);
            }

            robot.RaiseError($"Unknown command target '{command.target}'.");
            return false;
        }

        protected void StopAgent()
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            {
                return;
            }

            Agent.isStopped = true;
            Agent.ResetPath();
        }
    }
}
