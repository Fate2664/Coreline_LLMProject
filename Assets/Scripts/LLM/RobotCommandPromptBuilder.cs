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
            "Allowed actions are move, mine_resource, scan, pickup, deliver, wait, and stop. " +
            "Use only target ids from the available target list. Never put target type names like ResourceNode, Storage, or Refinery in the target field. " +
            "If the player asks for a resource by name, set the resource field and omit target unless a specific target id is requested. " +
            "Resource-only mining means mine every visible matching resource node. If the player states a specific number of nodes, set node_count to that node count. Omit node_count and amount otherwise. " +
            "If the player asks the robot to return, come back, or return to me, add a move command with target player after the requested work. " +
            "For a single action return {\"action\":\"mine_resource\",\"target\":\"iron_node_12\",\"priority\":\"high\"}. " +
            "For multiple actions return an object with a sequence property, not a raw array: {\"sequence\":[{\"action\":\"move\",\"target\":\"storage_1\"},{\"action\":\"pickup\",\"resource\":\"iron\"},{\"action\":\"deliver\",\"target\":\"refinery\"}]}.";

        [SerializeField] private bool includeTargetsInUserPrompt = true;
        [SerializeField] private int maxTargetsInPrompt = 80;

        public string BuildSystemPrompt(CommandTargetRegistry registry)
        {
            StringBuilder builder = new();
            builder.AppendLine(baseSystemPrompt);
            builder.AppendLine();
            builder.AppendLine("Command schema:");
            builder.AppendLine("{\"action\":\"move|mine_resource|scan|pickup|deliver|wait|stop\",\"target\":\"target_id\",\"resource\":\"coal|iron|gold|diamond|emerald\",\"priority\":\"low|normal|high\",\"amount\":1,\"node_count\":2}");
            builder.AppendLine();
            builder.AppendLine("The target value must match an id from the available target list exactly. The target value must not be a target type label.");
            builder.AppendLine("For resource requests like mine coal, prefer {\"action\":\"mine_resource\",\"resource\":\"coal\",\"priority\":\"normal\"} unless the player names a specific target id.");
            builder.AppendLine("Do not include amount for vague requests like mine coal or mine some coal; that means mine all visible matching nodes.");
            builder.AppendLine("If the player gives a specific node count, set node_count to that count, for example mine two coal nodes -> {\"action\":\"mine_resource\",\"resource\":\"coal\",\"node_count\":2,\"priority\":\"normal\"}.");
            builder.AppendLine("For requests like mine coal then return to me, return {\"sequence\":[{\"action\":\"mine_resource\",\"resource\":\"coal\",\"priority\":\"normal\"},{\"action\":\"move\",\"target\":\"player\"}]}.");
            builder.AppendLine();
            builder.AppendLine("If multiple actions are needed, wrap them in an object with a sequence array. Do not return a top-level JSON array.");

            if (!includeTargetsInUserPrompt)
            {
                AppendTargets(builder, registry);
            }

            return builder.ToString();
        }

        public string BuildUserPrompt(string playerPrompt, CommandTargetRegistry registry)
        {
            if (!includeTargetsInUserPrompt)
            {
                return playerPrompt;
            }

            StringBuilder builder = new();
            builder.AppendLine("Available command targets:");
            AppendTargets(builder, registry);
            builder.AppendLine();
            builder.AppendLine("Player instruction:");
            builder.AppendLine(playerPrompt);
            return builder.ToString();
        }

        private void AppendTargets(StringBuilder builder, CommandTargetRegistry registry)
        {
            if (registry == null)
            {
                builder.AppendLine("No command targets are registered.");
                return;
            }

            int count = 0;
            foreach (CommandTarget target in registry.Targets)
            {
                if (target == null)
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
