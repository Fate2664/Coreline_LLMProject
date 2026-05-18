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
        Follow,
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
        [JsonProperty("repeat", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool repeat;
        [JsonProperty("continuous", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool continuous;
        [JsonProperty("repeating", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool repeating;

        [JsonIgnore] public RobotCommandAction ActionType => ParseAction(action);
        [JsonIgnore] public RobotCommandPriority PriorityType => ParsePriority(priority);
        [JsonIgnore] public bool HasExplicitAmount { get; set; }
        [JsonIgnore] public bool IsRepeating => repeat || continuous || repeating;
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
                repeat = repeat,
                continuous = continuous,
                repeating = repeating,
                HasExplicitAmount = HasExplicitAmount
            };
        }

        public void SetRepeating(bool value)
        {
            repeat = value;
            continuous = false;
            repeating = false;
        }

        public void Normalize()
        {
            action = NormalizeToken(action);
            target = NormalizeTargetAlias(NormalizeToken(target));
            resource = NormalizeResourceAlias(NormalizeToken(resource));
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

            if (ActionType == RobotCommandAction.Follow && string.IsNullOrWhiteSpace(target))
            {
                target = "player";
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
                case "repeat_mine":
                case "repeat_mining":
                case "repeatedly_mine":
                case "continous_mine":
                case "continously_mine":
                case "continuous_mine":
                case "continuously_mine":
                case "mine_continously":
                case "mine_continuously":
                case "keep_mining":
                    return RobotCommandAction.MineResource;
                case "scan":
                case "scan_area":
                case "survey":
                    return RobotCommandAction.Scan;
                case "pickup":
                case "pick_up":
                case "pickup_ore":
                case "pickup_ores":
                case "collect":
                case "collect_ore":
                case "collect_ores":
                case "collect_all":
                case "collect_all_ores":
                case "collect_nearby":
                case "collect_nearby_ores":
                case "gather":
                case "gather_ore":
                case "gather_ores":
                case "follow_and_collect":
                case "follow_collect":
                case "follow_and_pickup":
                case "follow_pickup":
                case "repeat_pickup":
                case "repeat_collect":
                case "repeatedly_collect":
                case "continous_pickup":
                case "continous_collect":
                case "continously_collect":
                case "continuous_pickup":
                case "continuous_collect":
                case "continuously_collect":
                case "collect_continously":
                case "collect_continuously":
                case "continue_collecting":
                case "keep_collecting":
                    return RobotCommandAction.Pickup;
                case "deliver":
                case "dropoff":
                case "drop_off":
                case "deposit":
                    return RobotCommandAction.Deliver;
                case "follow":
                case "follow me":
                case "follow_me":
                case "follow player":
                case "follow_player":
                case "follow the player":
                case "follow_the_player":
                case "follow user":
                case "follow_user":
                case "follow target":
                case "follow_target":
                case "stay with me":
                case "stay_with_me":
                case "stay near me":
                case "stay_near_me":
                case "stay by me":
                case "stay_by_me":
                case "come with me":
                case "come_with_me":
                    return RobotCommandAction.Follow;
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
                case "this_robot":
                case "selected_robot":
                case "assigned_robot":
                case "selected_mining_robot":
                case "assigned_mining_robot":
                case "miner":
                case "mining_robot":
                    return "selected_mining_robot";
                default:
                    return value;
            }
        }

        private static string NormalizeResourceAlias(string value)
        {
            switch (value)
            {
                case "ore":
                case "ores":
                case "all_ores":
                case "nearby_ores":
                case "visible_ores":
                case "resource":
                case "resources":
                case "any":
                case "any_ore":
                case "any_ores":
                    return string.Empty;
                default:
                    return value.EndsWith("_ore", StringComparison.Ordinal)
                        ? value.Substring(0, value.Length - "_ore".Length)
                        : value;
            }
        }
    }
}
