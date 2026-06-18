using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(CollectingRobotController))]
    public class CollectingRobotCommandExecutor : BaseRobotCommandExecutor
    {
        [SerializeField] private float pickupRadius = 2f;
        [SerializeField] private LayerMask pickupLayers = ~0;
        [SerializeField] private float deliveryDuration = 0.5f;
        [SerializeField] private bool clearInventoryWhenNoReceiver = true;
        [SerializeField] private bool followSelectedMiningRobotWhenPickupHasNoTarget;
        [SerializeField] private float followDistance = 3f;
        [SerializeField] private float followCollectDuration = 120f;
        [SerializeField] private float visiblePickupRetryInterval = 0.25f;
        [SerializeField] private float noVisiblePickupStopDelay = 3f;
        [SerializeField] private float repeatCollectRetryInterval = 1f;

        private CollectingRobotController collectingRobot;

        protected override void Awake()
        {
            base.Awake();
            collectingRobot = GetComponent<CollectingRobotController>();
        }

        protected override IEnumerator ExecuteCommandByAction(RobotCommand command)
        {
            switch (command.ActionType)
            {
                case RobotCommandAction.Pickup:
                    yield return ExecutePickup(command);
                    break;
                case RobotCommandAction.Deliver:
                    yield return ExecuteDeliver(command);
                    break;
                default:
                    yield return base.ExecuteCommandByAction(command);
                    break;
            }
        }

        private IEnumerator ExecutePickup(RobotCommand command)
        {
            if (TryGetExplicitTarget(command, out CommandTarget explicitTarget))
            {
                if (explicitTarget.TargetType == CommandTargetType.Robot)
                {
                    yield return ExecuteFollowAndCollect(command, explicitTarget);
                    yield break;
                }

                if (explicitTarget.TargetType == CommandTargetType.PickupItem)
                {
                    yield return MoveAndPickup(explicitTarget);
                    yield break;
                }

                robot.RaiseError($"Target '{explicitTarget.TargetId}' is not a pickup item or robot.");
                yield break;
            }

            if (TryGetSelectedMiningRobotFollowTarget(command, out CommandTarget followTarget))
            {
                yield return ExecuteFollowAndCollect(command, followTarget);
                yield break;
            }

            if (command.IsRepeating)
            {
                yield return ExecuteRepeatingCollectVisible(command);
                yield break;
            }

            yield return ExecuteCollectVisible(command);
        }

        private IEnumerator ExecuteCollectVisible(RobotCommand command)
        {
            bool collectedAny = false;
            bool inventoryBlocked = false;

            while (TryGetNearestCollectablePickup(command.resource, out CommandTarget visiblePickup, out inventoryBlocked))
            {
                bool pickedUp = false;
                yield return MoveAndPickup(visiblePickup, value => pickedUp = value);
                collectedAny |= pickedUp;

                if (!pickedUp)
                {
                    collectingRobot.ForgetVisiblePickup(visiblePickup);
                }

                yield return null;
            }

            if (inventoryBlocked)
            {
                StopForFullInventory();
                yield break;
            }

            if (!collectedAny)
            {
                if (PickupNearby(command.resource))
                {
                    yield break;
                }
            }
        }

        private IEnumerator ExecuteRepeatingCollectVisible(RobotCommand command)
        {
            while (true)
            {
                yield return WaitWhileRobotPaused();

                bool collectedAny = false;
                bool inventoryBlocked = false;

                while (TryGetNearestCollectablePickup(command.resource, out CommandTarget visiblePickup, out inventoryBlocked))
                {
                    bool pickedUp = false;
                    yield return MoveAndPickup(visiblePickup, value => pickedUp = value);
                    collectedAny |= pickedUp;

                    if (!pickedUp)
                    {
                        collectingRobot.ForgetVisiblePickup(visiblePickup);
                    }

                    yield return null;
                }

                if (inventoryBlocked)
                {
                    StopForFullInventory();
                    yield break;
                }

                if (!collectedAny)
                {
                    if (PickupNearby(command.resource))
                    {
                        yield break;
                    }
                }

                StopAgent();
                robot.SetStatus(RobotWorkState.Idle);
                yield return WaitForSecondsPausable(repeatCollectRetryInterval);
            }
        }

        private IEnumerator ExecuteFollowAndCollect(RobotCommand command, CommandTarget followTarget)
        {
            if (followTarget == null)
            {
                yield break;
            }

            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            {
                robot.RaiseError("Robot NavMeshAgent is not active on a NavMesh.");
                yield break;
            }

            float previousStoppingDistance = Agent.stoppingDistance;
            float elapsed = 0f;
            float timeWithoutVisiblePickup = 0f;
            float commandDuration = command.IsRepeating ? float.PositiveInfinity : Mathf.Max(0.1f, followCollectDuration);

            Agent.stoppingDistance = Mathf.Max(0.05f, followDistance);
            Agent.isStopped = false;

            while (elapsed < commandDuration)
            {
                if (IsRobotPaused)
                {
                    PauseAgent();
                    yield return null;
                    continue;
                }

                if (followTarget == null || !followTarget.gameObject.activeInHierarchy)
                {
                    break;
                }

                if (TryGetNearestCollectablePickup(command.resource, out CommandTarget visiblePickup, out bool inventoryBlocked))
                {
                    timeWithoutVisiblePickup = 0f;

                    bool pickedUp = false;
                    yield return MoveAndPickup(visiblePickup, value => pickedUp = value);

                    if (!pickedUp)
                    {
                        collectingRobot.ForgetVisiblePickup(visiblePickup);
                    }

                    Agent.stoppingDistance = Mathf.Max(0.05f, followDistance);
                    Agent.isStopped = false;
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                if (inventoryBlocked)
                {
                    StopForFullInventory();
                    break;
                }

                robot.SetStatus(RobotWorkState.Walking);
                Agent.isStopped = false;

                if (!Agent.pathPending)
                {
                    Agent.SetDestination(followTarget.DestinationPosition);
                }

                if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.05f)
                {
                    timeWithoutVisiblePickup += visiblePickupRetryInterval;
                    if (!command.IsRepeating &&
                        timeWithoutVisiblePickup >= noVisiblePickupStopDelay &&
                        collectingRobot.SelectedMiningRobot == null)
                    {
                        break;
                    }
                }
                else
                {
                    timeWithoutVisiblePickup = 0f;
                }

                elapsed += visiblePickupRetryInterval;
                yield return WaitForSecondsPausable(visiblePickupRetryInterval);
            }

            Agent.stoppingDistance = previousStoppingDistance;
            StopAgent();
            robot.SetStatus(RobotWorkState.Idle);
        }

        private IEnumerator MoveAndPickup(CommandTarget target)
        {
            yield return MoveAndPickup(target, null);
        }

        private IEnumerator MoveAndPickup(CommandTarget target, System.Action<bool> onComplete)
        {
            onComplete ??= _ => { };

            if (target == null)
            {
                onComplete(false);
                yield break;
            }

            if (!CanAcceptPickupTarget(target))
            {
                robot.RaiseError($"No robot inventory space for pickup target '{target.TargetId}'.");
                StopAgent();
                robot.SetStatus(RobotWorkState.Idle);
                onComplete(false);
                yield break;
            }

            bool reached = false;
            yield return MoveToTarget(target, target.InteractionRadius, value => reached = value);

            bool pickedUp = reached && TryPickupTarget(target);
            onComplete(pickedUp);
        }

        private IEnumerator ExecuteDeliver(RobotCommand command)
        {
            if (RobotCommand.IsSelectedChestReference(command.target) &&
                collectingRobot.SelectedDeliveryChest != null &&
                collectingRobot.SelectedDeliveryChest.CommandTarget != null)
            {
                command.target = collectingRobot.SelectedDeliveryChest.CommandTarget.TargetId;
            }

            if (!TryResolveTarget(command, out CommandTarget target))
            {
                yield break;
            }

            bool reached = false;
            yield return MoveToTarget(target, target.InteractionRadius, value => reached = value);

            if (!reached)
            {
                yield break;
            }

            robot.SetStatus(RobotWorkState.Idle);

            IRobotInventoryReceiver receiver = target.GetComponentInParent<IRobotInventoryReceiver>();
            if (receiver != null)
            {
                if (!collectingRobot.Inventory.TryTransferTo(receiver))
                {
                    robot.RaiseError($"Target '{target.TargetId}' did not accept the robot inventory.");
                }
            }
            else if (clearInventoryWhenNoReceiver)
            {
                collectingRobot.Inventory.ClearAll();
            }

            yield return WaitForSecondsPausable(deliveryDuration);
        }

        private bool PickupNearby(string resource)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius, pickupLayers, QueryTriggerInteraction.Collide);
            HashSet<CommandTarget> pickedTargets = new();

            foreach (Collider hit in hits)
            {
                CommandTarget target = hit.GetComponentInParent<CommandTarget>();
                if (target == null ||
                    target.TargetType != CommandTargetType.PickupItem ||
                    !target.MatchesResource(resource) ||
                    !collectingRobot.CanSeePickupTarget(target))
                {
                    continue;
                }

                if (pickedTargets.Add(target))
                {
                    if (!TryPickupTarget(target) && !CanAcceptPickupTarget(target))
                    {
                        StopForFullInventory();
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryPickupTarget(CommandTarget target)
        {
            if (target == null)
            {
                return false;
            }

            collectingRobot.ForgetVisiblePickup(target);
            robot.SetStatus(RobotWorkState.Idle);

            bool added = false;
            if (target.InventoryItemData != null)
            {
                added = collectingRobot.Inventory.TryAddItem(target.InventoryItemData, target.PickupAmount);
            }
            else if (target.HasOreType)
            {
                added = collectingRobot.Inventory.TryAddResource(target.OreType, target.PickupAmount);
            }

            if (!added)
            {
                robot.RaiseError($"Could not add pickup target '{target.TargetId}' to robot inventory.");
                return false;
            }

            if (target.DestroyOnPickup)
            {
                Destroy(target.gameObject);
            }
            else
            {
                target.gameObject.SetActive(false);
            }

            return true;
        }

        private bool TryGetNearestCollectablePickup(string resource, out CommandTarget target, out bool inventoryBlocked)
        {
            target = null;
            inventoryBlocked = false;

            List<CommandTarget> visiblePickups = collectingRobot.GetVisiblePickupTargets(resource, transform.position);
            for (int i = 0; i < visiblePickups.Count; i++)
            {
                CommandTarget candidate = visiblePickups[i];
                if (CanAcceptPickupTarget(candidate))
                {
                    target = candidate;
                    return true;
                }

                inventoryBlocked = true;
                collectingRobot.ForgetVisiblePickup(candidate);
            }

            return false;
        }

        private bool CanAcceptPickupTarget(CommandTarget target)
        {
            if (target == null || collectingRobot == null || collectingRobot.Inventory == null)
            {
                return false;
            }

            if (target.InventoryItemData != null)
            {
                return collectingRobot.Inventory.CanAcceptItem(target.InventoryItemData, target.PickupAmount);
            }

            return target.HasOreType && collectingRobot.Inventory.CanAcceptResource(target.OreType, target.PickupAmount);
        }

        private void StopForFullInventory()
        {
            StopAgent();
            robot.SetStatus(RobotWorkState.Idle);
            robot.RaiseError("Collection robot inventory is full.");
        }

        private bool TryGetExplicitTarget(RobotCommand command, out CommandTarget target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(command.target) || IsSelectedMiningRobotReference(command.target))
            {
                return false;
            }

            CommandTargetRegistry registry = Registry;
            return registry != null && registry.TryGetTarget(command.target, transform.position, out target);
        }

        private bool TryGetSelectedMiningRobotFollowTarget(RobotCommand command, out CommandTarget target)
        {
            target = null;

            MiningRobotController selectedMiningRobot = collectingRobot.SelectedMiningRobot;
            bool requestedSelectedRobot = IsSelectedMiningRobotReference(command.target);
            bool shouldUseSelectedByDefault = followSelectedMiningRobotWhenPickupHasNoTarget &&
                                              string.IsNullOrWhiteSpace(command.target) &&
                                              selectedMiningRobot != null;
            bool actionRequestsFollow = IsFollowAndCollectAction(command.action) &&
                                              string.IsNullOrWhiteSpace(command.target) &&
                                              selectedMiningRobot != null;

            if (!requestedSelectedRobot && !shouldUseSelectedByDefault && !actionRequestsFollow)
            {
                return false;
            }

            if (selectedMiningRobot == null)
            {
                robot.RaiseError("No mining robot is selected for this collecting robot.");
                return false;
            }

            target = selectedMiningRobot.CommandTarget;
            return target != null;
        }

        private static bool IsSelectedMiningRobotReference(string target)
        {
            switch (RobotCommand.NormalizeToken(target))
            {
                case "this_robot":
                case "this robot":
                case "selected_robot":
                case "selected robot":
                case "assigned_robot":
                case "assigned robot":
                case "selected_mining_robot":
                case "selected mining robot":
                case "assigned_mining_robot":
                case "assigned mining robot":
                case "miner":
                case "mining_robot":
                case "mining robot":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsFollowAndCollectAction(string action)
        {
            switch (RobotCommand.NormalizeToken(action))
            {
                case "follow_and_collect":
                case "follow_collect":
                case "follow_and_pickup":
                case "follow_pickup":
                    return true;
                default:
                    return false;
            }
        }

    }
}
