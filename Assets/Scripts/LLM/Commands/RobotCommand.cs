using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Coreline.Robots
{
    public enum RobotCommandAction
    {
        Unknown,
        Move,
        MineResource,
        Scan,
        Pickup,
        Deliver,
        Wait,
        Stop
    }

    public enum RobotCommandPriority
    {
        Low,
        Normal,
        High
    }

    [Serializable]
    public class RobotCommand
    {
        [JsonProperty("action")] public string action;
        [JsonProperty("target")] public string target;
        [JsonProperty("resource")] public string resource;
        [JsonProperty("priority")] public string priority;
        [JsonProperty("amount")] public int amount = 1;
        [JsonProperty("node_count", DefaultValueHandling = DefaultValueHandling.Ignore)] public int nodeCount;

        [JsonIgnore] public RobotCommandAction ActionType => ParseAction(action);
        [JsonIgnore] public RobotCommandPriority PriorityType => ParsePriority(priority);
        [JsonIgnore] public bool HasExplicitAmount { get; set; }
        [JsonIgnore] public int RequestedNodeCount => nodeCount > 0 ? nodeCount : HasExplicitAmount && amount > 1 ? amount : 0;

        public RobotCommand Clone()
        {
            return new RobotCommand
            {
                action = action,
                target = target,
                resource = resource,
                priority = priority,
                amount = amount,
                nodeCount = nodeCount,
                HasExplicitAmount = HasExplicitAmount
            };
        }

        public void Normalize()
        {
            action = NormalizeToken(action);
            target = NormalizeTargetAlias(NormalizeToken(target));
            resource = NormalizeToken(resource);
            priority = NormalizeToken(priority);

            if (amount <= 0)
            {
                amount = 1;
            }

            if (nodeCount < 0)
            {
                nodeCount = 0;
            }

            if (string.IsNullOrWhiteSpace(priority))
            {
                priority = "normal";
            }
        }

        public override string ToString()
        {
            string targetText = string.IsNullOrWhiteSpace(target) ? "none" : target;
            string resourceText = string.IsNullOrWhiteSpace(resource) ? "none" : resource;
            return $"{action} target={targetText} resource={resourceText} priority={priority}";
        }

        public static RobotCommandAction ParseAction(string value)
        {
            switch (NormalizeToken(value))
            {
                case "move":
                case "go_to":
                case "goto":
                case "return":
                case "return_to_player":
                case "come_back":
                case "come_back_to_player":
                    return RobotCommandAction.Move;
                case "mine":
                case "mine_resource":
                case "mine_node":
                    return RobotCommandAction.MineResource;
                case "scan":
                case "scan_area":
                case "survey":
                    return RobotCommandAction.Scan;
                case "pickup":
                case "pick_up":
                case "collect":
                    return RobotCommandAction.Pickup;
                case "deliver":
                case "dropoff":
                case "drop_off":
                case "deposit":
                    return RobotCommandAction.Deliver;
                case "wait":
                    return RobotCommandAction.Wait;
                case "stop":
                case "cancel":
                    return RobotCommandAction.Stop;
                default:
                    return RobotCommandAction.Unknown;
            }
        }

        public static RobotCommandPriority ParsePriority(string value)
        {
            switch (NormalizeToken(value))
            {
                case "low":
                    return RobotCommandPriority.Low;
                case "high":
                case "urgent":
                    return RobotCommandPriority.High;
                default:
                    return RobotCommandPriority.Normal;
            }
        }

        public static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeTargetAlias(string value)
        {
            switch (value)
            {
                case "me":
                case "player":
                case "user":
                case "player_character":
                case "player_position":
                case "current_player":
                    return "player";
                default:
                    return value;
            }
        }
    }
}
