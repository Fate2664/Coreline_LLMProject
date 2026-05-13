using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    public class RobotCommandValidator : MonoBehaviour
    {
        [SerializeField] private CommandTargetRegistry targetRegistry;
        [SerializeField] private bool resolveResourceTargets = true;
        [SerializeField] private bool expandResourceMiningToAllVisibleNodes = true;

        private CommandTargetRegistry Registry => targetRegistry != null ? targetRegistry : CommandTargetRegistry.Instance;

        private void Awake()
        {
            targetRegistry ??= CommandTargetRegistry.Instance;
        }

        public bool TryValidate(RobotCommandSequence sequence, BaseRobotController robot, out RobotCommandSequence validatedSequence, out string error)
        {
            validatedSequence = null;
            error = string.Empty;

            if (sequence == null || sequence.IsEmpty)
            {
                error = "No robot commands were provided.";
                return false;
            }

            CommandTargetRegistry registry = Registry;
            if (registry == null)
            {
                error = $"No {nameof(CommandTargetRegistry)} exists in the scene.";
                return false;
            }

            Vector3 robotPosition = robot != null ? robot.transform.position : transform.position;
            List<RobotCommand> validated = new();

            foreach (RobotCommand sourceCommand in sequence.sequence)
            {
                RobotCommand command = sourceCommand.Clone();
                command.Normalize();

                if (!TryValidateAndAppendCommand(command, registry, robot, robotPosition, validated, out error))
                {
                    return false;
                }
            }

            validatedSequence = new RobotCommandSequence(validated);
            return true;
        }

        private bool TryValidateAndAppendCommand(RobotCommand command, CommandTargetRegistry registry, BaseRobotController robot,
            Vector3 robotPosition, List<RobotCommand> validated, out string error)
        {
            error = string.Empty;

            if (ShouldExpandMineResourceCommand(command, registry, robotPosition))
            {
                return TryExpandMineResourceCommand(command, registry, robot, robotPosition, validated, out error);
            }

            if (!TryValidateCommand(command, registry, robot, robotPosition, out error))
            {
                return false;
            }

            validated.Add(command);
            return true;
        }

        private bool TryValidateCommand(RobotCommand command, CommandTargetRegistry registry, BaseRobotController robot, Vector3 robotPosition, out string error)
        {
            error = string.Empty;

            if (robot != null && !robot.CanExecuteAction(command.ActionType))
            {
                error = $"{robot.name} cannot execute action '{command.action}'.";
                return false;
            }

            switch (command.ActionType)
            {
                case RobotCommandAction.Move:
                    return RequireTarget(command, registry, robotPosition, out error);
                case RobotCommandAction.MineResource:
                    return ValidateMineCommand(command, registry, robotPosition, out error);
                case RobotCommandAction.Scan:
                    return string.IsNullOrWhiteSpace(command.target) || RequireTarget(command, registry, robotPosition, out error);
                case RobotCommandAction.Pickup:
                    return ValidatePickupCommand(command, registry, robotPosition, out error);
                case RobotCommandAction.Deliver:
                    return ValidateDeliverCommand(command, registry, robotPosition, out error);
                case RobotCommandAction.Wait:
                case RobotCommandAction.Stop:
                    return true;
                default:
                    error = $"Unsupported robot action '{command.action}'.";
                    return false;
            }
        }

        private bool ShouldExpandMineResourceCommand(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition)
        {
            return expandResourceMiningToAllVisibleNodes &&
                   resolveResourceTargets &&
                   command.ActionType == RobotCommandAction.MineResource &&
                   !string.IsNullOrWhiteSpace(command.resource) &&
                   !TryGetValidTarget(command, registry, robotPosition, out _);
        }

        private bool TryExpandMineResourceCommand(RobotCommand command, CommandTargetRegistry registry, BaseRobotController robot,
            Vector3 robotPosition, List<RobotCommand> validated, out string error)
        {
            error = string.Empty;

            if (robot != null && !robot.CanExecuteAction(command.ActionType))
            {
                error = $"{robot.name} cannot execute action '{command.action}'.";
                return false;
            }

            List<CommandTarget> resourceTargets = registry.FindTargets(CommandTargetType.ResourceNode, command.resource, robotPosition);
            if (resourceTargets.Count == 0)
            {
                return ValidateMineCommand(command, registry, robotPosition, out error);
            }

            int requestedNodeCount = command.RequestedNodeCount;
            int targetCount = requestedNodeCount > 0
                ? Mathf.Min(requestedNodeCount, resourceTargets.Count)
                : resourceTargets.Count;

            for (int i = 0; i < targetCount; i++)
            {
                CommandTarget resourceTarget = resourceTargets[i];
                RobotCommand expandedCommand = command.Clone();
                expandedCommand.target = resourceTarget.TargetId;
                expandedCommand.amount = 1;
                expandedCommand.nodeCount = 0;
                expandedCommand.HasExplicitAmount = false;
                expandedCommand.Normalize();

                if (!ValidateMineTarget(expandedCommand, resourceTarget, out error))
                {
                    return false;
                }

                validated.Add(expandedCommand);
            }

            return true;
        }

        private bool ValidateMineCommand(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition, out string error)
        {
            error = string.Empty;

            if (TryGetValidTarget(command, registry, robotPosition, out CommandTarget target))
            {
                return ValidateMineTarget(command, target, out error);
            }

            if (resolveResourceTargets && registry.TryFindNearestResource(command.resource, robotPosition, out CommandTarget resourceTarget))
            {
                return ValidateMineTarget(command, resourceTarget, out error);
            }

            if (!string.IsNullOrWhiteSpace(command.resource))
            {
                error = $"No visible resource node found for '{command.resource}'. Move the mining robot inside a scanning robot radius first.";
                return false;
            }

            return RequireTarget(command, registry, robotPosition, out error);
        }

        private bool ValidatePickupCommand(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition, out string error)
        {
            error = string.Empty;

            if (TryGetValidTarget(command, registry, robotPosition, out CommandTarget target))
            {
                return ValidatePickupTarget(command, target, out error);
            }

            if (registry.TryFindNearestPickup(command.resource, robotPosition, out CommandTarget pickupTarget))
            {
                return true;
            }

            return RequireTarget(command, registry, robotPosition, out error);
        }

        private static bool ValidateMineTarget(RobotCommand command, CommandTarget target, out string error)
        {
            error = string.Empty;

            if (target.TargetType != CommandTargetType.ResourceNode)
            {
                error = $"Target '{command.target}' is not a resource node.";
                return false;
            }

            if (!target.TryGetMineable(out _))
            {
                error = $"Target '{command.target}' does not have an IMineable component.";
                return false;
            }

            return true;
        }

        private static bool ValidatePickupTarget(RobotCommand command, CommandTarget target, out string error)
        {
            error = string.Empty;

            if (target.TargetType != CommandTargetType.PickupItem)
            {
                error = $"Target '{command.target}' is not a pickup item.";
                return false;
            }

            return true;
        }

        private static bool TryGetValidTarget(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition, out CommandTarget target)
        {
            target = null;
            return !string.IsNullOrWhiteSpace(command.target) && registry.TryGetTarget(command.target, robotPosition, out target);
        }

        private bool ValidateDeliverCommand(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition, out string error)
        {
            if (!RequireTarget(command, registry, robotPosition, out error))
            {
                return false;
            }

            CommandTarget target = GetTarget(command, registry, robotPosition);
            if (target.TargetType != CommandTargetType.Storage && target.TargetType != CommandTargetType.Refinery)
            {
                error = $"Target '{command.target}' is not a storage or refinery target.";
                return false;
            }

            return true;
        }

        private static bool RequireTarget(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(command.target))
            {
                error = $"Action '{command.action}' requires a target.";
                return false;
            }

            if (!registry.TryGetTarget(command.target, robotPosition, out _))
            {
                error = $"Unknown command target '{command.target}'.";
                return false;
            }

            return true;
        }

        private static CommandTarget GetTarget(RobotCommand command, CommandTargetRegistry registry, Vector3 robotPosition)
        {
            registry.TryGetTarget(command.target, robotPosition, out CommandTarget target);
            return target;
        }
    }
}
