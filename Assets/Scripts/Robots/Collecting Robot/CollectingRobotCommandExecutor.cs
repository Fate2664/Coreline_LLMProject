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
            if (TryResolveTarget(command, out CommandTarget target))
            {
                bool reached = false;
                yield return MoveToTarget(target, target.InteractionRadius, value => reached = value);

                if (reached)
                {
                    TryPickupTarget(target);
                }

                yield break;
            }

            if (Registry != null && Registry.TryFindNearestPickup(command.resource, transform.position, out CommandTarget nearestPickup))
            {
                bool reached = false;
                yield return MoveToTarget(nearestPickup, nearestPickup.InteractionRadius, value => reached = value);

                if (reached)
                {
                    TryPickupTarget(nearestPickup);
                }

                yield break;
            }

            PickupNearby(command.resource);
        }

        private IEnumerator ExecuteDeliver(RobotCommand command)
        {
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

            yield return new WaitForSeconds(deliveryDuration);
        }

        private void PickupNearby(string resource)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius, pickupLayers, QueryTriggerInteraction.Collide);
            HashSet<CommandTarget> pickedTargets = new();

            foreach (Collider hit in hits)
            {
                CommandTarget target = hit.GetComponentInParent<CommandTarget>();
                if (target == null || target.TargetType != CommandTargetType.PickupItem || !target.MatchesResource(resource))
                {
                    continue;
                }

                if (pickedTargets.Add(target))
                {
                    TryPickupTarget(target);
                }
            }
        }

        private bool TryPickupTarget(CommandTarget target)
        {
            if (target == null)
            {
                return false;
            }

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
    }
}
