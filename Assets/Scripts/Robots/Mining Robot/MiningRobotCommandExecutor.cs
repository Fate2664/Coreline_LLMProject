using System.Collections;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(MiningRobotController))]
    public class MiningRobotCommandExecutor : BaseRobotCommandExecutor
    {
        [SerializeField] private float miningInterval = 0.35f;
        [SerializeField] private float miningDamage = 1f;
        [SerializeField] private int maxMiningHits = 200;
        [SerializeField] private float faceTargetTurnSpeed = 540f;
        [SerializeField] private float faceTargetAngleTolerance = 2f;
        [SerializeField] private float maxFaceTargetTime = 1.5f;
        [SerializeField] private float repeatMineRetryInterval = 1f;

        private MiningRobotController miningRobot;

        protected override void Awake()
        {
            base.Awake();
            miningRobot = GetComponent<MiningRobotController>();
        }

        public override void CancelCurrentCommand()
        {
            base.CancelCurrentCommand();
            miningRobot?.ClearMiningLookTarget();
        }

        protected override IEnumerator ExecuteCommandByAction(RobotCommand command)
        {
            if (command.ActionType == RobotCommandAction.MineResource)
            {
                yield return ExecuteMine(command);
                yield break;
            }

            yield return base.ExecuteCommandByAction(command);
        }

        private IEnumerator ExecuteMine(RobotCommand command)
        {
            if (command.IsRepeating)
            {
                yield return ExecuteRepeatingMine(command);
                yield break;
            }

            if (!TryResolveTarget(command, out CommandTarget target))
            {
                yield break;
            }

            yield return MineTarget(command, target);
        }

        private IEnumerator ExecuteRepeatingMine(RobotCommand command)
        {
            if (Registry == null)
            {
                robot.RaiseError($"No {nameof(CommandTargetRegistry)} exists in the scene.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(command.target) && string.IsNullOrWhiteSpace(command.resource))
            {
                robot.RaiseError("A repeating mine command requires a resource or target.");
                yield break;
            }

            while (true)
            {
                yield return WaitWhileRobotPaused();

                if (!TryGetRepeatingMineTarget(command, out CommandTarget target))
                {
                    StopAgent();
                    robot.SetStatus(RobotWorkState.Idle);
                    yield return WaitForSecondsPausable(repeatMineRetryInterval);
                    continue;
                }

                yield return MineTarget(command, target);
                yield return WaitForSecondsPausable(repeatMineRetryInterval);
            }
        }

        private bool TryGetRepeatingMineTarget(RobotCommand command, out CommandTarget target)
        {
            target = null;
            CommandTargetRegistry registry = Registry;
            if (registry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(command.target))
            {
                return registry.TryGetTarget(command.target, transform.position, out target);
            }

            return !string.IsNullOrWhiteSpace(command.resource) &&
                   registry.TryFindNearestResource(command.resource, transform.position, out target);
        }

        private IEnumerator MineTarget(RobotCommand command, CommandTarget target)
        {
            bool reached = false;
            yield return MoveToTarget(target, target.InteractionRadius, value => reached = value);

            if (!reached)
            {
                yield break;
            }

            StopAgent();
            yield return WaitWhileRobotPaused();
            yield return FaceTarget(target);

            if (!target.TryGetMineable(out global::IMineable mineable))
            {
                robot.RaiseError($"Target '{target.TargetId}' is not mineable.");
                yield break;
            }

            miningRobot.SetMiningLookTarget(target.transform);
            robot.SetStatus(RobotWorkState.Mining);
            try
            {
                yield return WaitForMiningAnimationReady();

                if (!miningRobot.IsMiningAnimationReady)
                {
                    yield break;
                }

                int hitCount = Mathf.Max(1, command.amount > 1 ? command.amount : maxMiningHits);
                for (int i = 0; i < hitCount; i++)
                {
                    yield return WaitWhileRobotPaused();

                    if (target == null || mineable == null)
                    {
                        yield break;
                    }

                    BuildMiningHit(target, out Vector3 point, out Vector3 normal);

                    bool depleted = mineable.Mine(new global::MiningHit(miningRobot.ToolTransform, point, normal, miningDamage));
                    if (depleted)
                    {
                        robot.SetStatus(RobotWorkState.Idle);
                        yield break;
                    }

                    yield return WaitForSecondsPausable(miningInterval);
                }
            }
            finally
            {
                miningRobot.ClearMiningLookTarget();
            }
        }

        private IEnumerator FaceTarget(CommandTarget target)
        {
            if (target == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < maxFaceTargetTime)
            {
                if (IsRobotPaused)
                {
                    PauseAgent();
                    yield return null;
                    continue;
                }

                if (!TryGetFlatDirectionToTarget(target, out Vector3 direction))
                {
                    yield break;
                }

                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    faceTargetTurnSpeed * Time.deltaTime);

                if (Quaternion.Angle(transform.rotation, targetRotation) <= faceTargetAngleTolerance)
                {
                    transform.rotation = targetRotation;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForMiningAnimationReady()
        {
            float elapsed = 0f;
            const float timeout = 5f;

            while (!miningRobot.IsMiningAnimationReady &&
                   (miningRobot.CurrentState == RobotWorkState.Mining || IsRobotPaused) &&
                   elapsed < timeout)
            {
                if (IsRobotPaused)
                {
                    PauseAgent();
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void BuildMiningHit(CommandTarget target, out Vector3 point, out Vector3 normal)
        {
            Collider targetCollider = target.GetComponentInChildren<Collider>();
            Vector3 sourcePosition = miningRobot.ToolTransform.position;
            point = targetCollider != null ? targetCollider.ClosestPoint(sourcePosition) : target.transform.position;
            normal = sourcePosition - point;

            if (normal.sqrMagnitude <= 0.0001f)
            {
                normal = -miningRobot.transform.forward;
            }
            else
            {
                normal.Normalize();
            }
        }

        private bool TryGetFlatDirectionToTarget(CommandTarget target, out Vector3 direction)
        {
            Collider targetCollider = target.GetComponentInChildren<Collider>();
            Vector3 targetPosition = targetCollider != null ? targetCollider.bounds.center : target.DestinationPosition;

            direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }
    }
}
