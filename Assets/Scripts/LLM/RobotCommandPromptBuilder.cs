using System.Text;
using UnityEngine;

namespace Coreline.Robots
{
    public class RobotCommandPromptBuilder : MonoBehaviour
    {
        [SerializeField, TextArea(8, 20)]
        private string baseSystemPrompt =
            "You translate player instructions into robot command JSON for a Unity mining game. " +
            "Return only valid JSON. Do not include markdown, explanations, or extra text. " +
            "Allowed actions are move, follow, mine_resource, pickup, deliver, wait, and stop. " +
            "Use only target ids from the available target list. Never put target type names like ResourceNode, Storage, or Refinery in the target field. " +
            "Resource node targets are only available when the target robot is inside a scanning robot radius and the node is inside that same radius. " +
            "Do not issue scan commands; scanning robots reveal nearby resources passively. " +
            "If the player asks for a resource by name, set the resource field and omit target unless a specific target id is requested. " +
            "Resource-only mining means mine every visible matching resource node. If the player states a specific number of nodes, set node_count to that node count. Omit node_count and amount otherwise. " +
            "If the player asks to repeat, continue, continuously work, continously work, or keep doing a task, set repeat to true on that command. " +
            "For collecting robots, pickup with no target means collect every visible ore or pickup item. If a selected mining robot is provided and the player says this robot, selected robot, or assigned robot, target that selected mining robot id. " +
            "If the player asks the robot to follow them, return a follow command with target player. " +
            "If the player asks the robot to return, come back, or return to me, add a move command with target player after the requested work. " +
            "For a single action return {\"action\":\"mine_resource\",\"target\":\"iron_node_12\",\"priority\":\"high\"}. " +
            "For multiple actions return an object with a sequence property, not a raw array: {\"sequence\":[{\"action\":\"move\",\"target\":\"storage_1\"},{\"action\":\"pickup\",\"resource\":\"iron\"},{\"action\":\"deliver\",\"target\":\"refinery\"}]}.";

        [SerializeField] private bool includeTargetsInUserPrompt = true;
        [SerializeField] private int maxTargetsInPrompt = 80;

        public string BuildSystemPrompt(CommandTargetRegistry registry)
        {
            return BuildSystemPrompt(registry, null);
        }

        public string BuildSystemPrompt(CommandTargetRegistry registry, BaseRobotController viewerRobot)
        {
            StringBuilder builder = new();
            builder.AppendLine(baseSystemPrompt);
            builder.AppendLine();
            builder.AppendLine("Command normalization overrides:");
            builder.AppendLine("- The final allowed action set is move, follow, mine_resource, pickup, deliver, wait, and stop.");
            builder.AppendLine("- For prompts like follow me, follow the player, stay with me, or come with me, return {\"action\":\"follow\",\"target\":\"player\",\"priority\":\"normal\"}.");
            builder.AppendLine("- A follow command continues until the player later tells that same robot to stop.");
            builder.AppendLine("- For prompts like stop, stop following, or wait there, return {\"action\":\"stop\",\"priority\":\"normal\"}.");
            builder.AppendLine("- Do not use move for follow me. Move to player is only for return-to-player commands after a task is complete.");
            builder.AppendLine("- For prompts with repeatingly, repeatedly, continously, continuously, continually, continue to, keep, always, or whenever visible, set \"repeat\":true.");
            builder.AppendLine("- A repeat command continues to look for newly visible work until the player later tells that same robot to stop.");
            builder.AppendLine();
            builder.AppendLine("Command schema:");
            builder.AppendLine("{\"action\":\"move|follow|mine_resource|pickup|deliver|wait|stop\",\"target\":\"target_id\",\"resource\":\"coal|iron|gold|diamond|emerald\",\"priority\":\"low|normal|high\",\"amount\":1,\"node_count\":2,\"repeat\":true}");
            builder.AppendLine();
            builder.AppendLine("The target value must match an id from the available target list exactly. The target value must not be a target type label.");
            builder.AppendLine("Resource nodes are available only when the target robot and the resource node are both inside a scanning robot radius.");
            builder.AppendLine("Scanning robots reveal resources passively. Do not move or follow a scanning robot before or after mining unless the player explicitly asks to move to or follow that scanner.");
            builder.AppendLine("Do not create scan commands. If no matching resource is visible, do not invent a target id.");
            builder.AppendLine("For resource requests like mine coal, prefer {\"action\":\"mine_resource\",\"resource\":\"coal\",\"priority\":\"normal\"} unless the player names a specific target id.");
            builder.AppendLine("For requests naming multiple resources, return one mine_resource command per resource in a sequence, for example mine iron and coal -> {\"sequence\":[{\"action\":\"mine_resource\",\"resource\":\"iron\",\"priority\":\"normal\"},{\"action\":\"mine_resource\",\"resource\":\"coal\",\"priority\":\"normal\"}]}.");
            builder.AppendLine("If the player says to prioritize a resource, set that resource command to priority high and keep the other resource commands normal, for example prioritize iron -> iron high, coal normal.");
            builder.AppendLine("Do not include amount for vague requests like mine coal or mine some coal; that means mine all visible matching nodes.");
            builder.AppendLine("If the player gives a specific node count, set node_count to that count, for example mine two coal nodes -> {\"action\":\"mine_resource\",\"resource\":\"coal\",\"node_count\":2,\"priority\":\"normal\"}.");
            builder.AppendLine("For requests like continuously mine coal that you can see, return {\"action\":\"mine_resource\",\"resource\":\"coal\",\"repeat\":true,\"priority\":\"normal\"}.");
            builder.AppendLine("For requests like mine coal then return to me, return {\"sequence\":[{\"action\":\"mine_resource\",\"resource\":\"coal\",\"priority\":\"normal\"},{\"action\":\"move\",\"target\":\"player\"}]}.");
            builder.AppendLine("For requests like follow me, return {\"action\":\"follow\",\"target\":\"player\",\"priority\":\"normal\"}.");
            builder.AppendLine("For collection requests like collect all ores you can see, return {\"action\":\"pickup\",\"priority\":\"normal\"} with no target.");
            builder.AppendLine("For collection requests like continue to collect any ores that you see, return {\"action\":\"pickup\",\"repeat\":true,\"priority\":\"normal\"} with no target.");
            builder.AppendLine("For collection requests like follow this robot and collect nearby ores, return {\"action\":\"pickup\",\"target\":\"selected_mining_robot_id\",\"priority\":\"normal\"} using the selected mining robot id from the user prompt context.");
            builder.AppendLine("For collection requests like collect ores and then deliver them to this chest, return a pickup command followed by a deliver command whose target is the exact id stored in selected_chest in the user prompt context.");
            builder.AppendLine("When the player says this chest, selected chest, chosen chest, or assigned chest, use the value of selected_chest exactly. Never substitute another Storage target.");
            builder.AppendLine();
            builder.AppendLine("If multiple actions are needed, wrap them in an object with a sequence array. Do not return a top-level JSON array.");

            if (!includeTargetsInUserPrompt)
            {
                AppendTargets(builder, registry, viewerRobot);
            }

            return builder.ToString();
        }

        public string BuildUserPrompt(string playerPrompt, CommandTargetRegistry registry)
        {
            return BuildUserPrompt(playerPrompt, registry, null);
        }

        public string BuildUserPrompt(string playerPrompt, CommandTargetRegistry registry, BaseRobotController viewerRobot)
        {
            if (!includeTargetsInUserPrompt)
            {
                return playerPrompt;
            }

            StringBuilder builder = new();
            builder.AppendLine("Available command targets:");
            AppendTargets(builder, registry, viewerRobot);
            builder.AppendLine();
            AppendRobotContext(builder, viewerRobot);
            builder.AppendLine();
            builder.AppendLine("Player instruction:");
            builder.AppendLine(playerPrompt);
            return builder.ToString();
        }

        private static void AppendRobotContext(StringBuilder builder, BaseRobotController viewerRobot)
        {
            if (viewerRobot is not CollectingRobotController collectingRobot)
            {
                return;
            }

            string selectedMiningRobotId = collectingRobot.SelectedMiningRobot != null
                ? collectingRobot.SelectedMiningRobot.RobotTargetId
                : "None";
            string selectedChestId = collectingRobot.SelectedDeliveryChest != null &&
                                     collectingRobot.SelectedDeliveryChest.CommandTarget != null
                ? collectingRobot.SelectedDeliveryChest.CommandTarget.TargetId
                : "None";

            builder.AppendLine("Collecting robot context:");
            builder.AppendLine($"- selected_mining_robot=\"{selectedMiningRobotId}\"");
            builder.AppendLine($"- selected_chest=\"{selectedChestId}\"");
            builder.AppendLine("- If selected_mining_robot is None, collect visible ores with {\"action\":\"pickup\",\"priority\":\"normal\"} and no target.");
            builder.AppendLine("- If the player says this robot, selected robot, assigned robot, or follow this robot, use selected_mining_robot as the pickup target.");
            builder.AppendLine("- A pickup command targeting a robot means follow that robot and collect visible nearby pickup items.");
            builder.AppendLine("- If the player says this chest, selected chest, chosen chest, or assigned chest, use the id stored in selected_chest as the deliver target.");
            builder.AppendLine("- If selected_chest is None, do not invent a storage target.");
        }

        private void AppendTargets(StringBuilder builder, CommandTargetRegistry registry, BaseRobotController viewerRobot)
        {
            if (registry == null)
            {
                builder.AppendLine("No command targets are registered.");
                return;
            }

            int count = 0;
            bool hasViewerPosition = viewerRobot != null;
            Vector3 viewerPosition = hasViewerPosition ? viewerRobot.transform.position : Vector3.zero;

            foreach (CommandTarget target in registry.Targets)
            {
                bool visible = hasViewerPosition
                    ? registry.IsTargetVisible(target, viewerPosition)
                    : registry.IsTargetVisible(target);

                if (target == null || !visible)
                {
                    continue;
                }

                if (viewerRobot is MiningRobotController &&
                    target.GetComponentInParent<ScanningRobotController>() != null)
                {
                    continue;
                }

                if (count >= maxTargetsInPrompt)
                {
                    builder.AppendLine("Additional targets omitted.");
                    break;
                }

                string resourceText = target.HasOreType ? target.OreType.ToString().ToLowerInvariant() : "none";
                builder.AppendLine($"- id=\"{target.TargetId}\" type=\"{target.TargetType}\" resource=\"{resourceText}\"");
                count++;
            }

            if (count == 0)
            {
                builder.AppendLine("No command targets are registered.");
            }
        }
    }
}
