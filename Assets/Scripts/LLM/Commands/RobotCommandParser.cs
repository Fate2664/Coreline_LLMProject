using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coreline.Robots
{
    public static class RobotCommandParser
    {
        public static bool TryParse(string llmOutput, out RobotCommandSequence sequence, out string error)
        {
            sequence = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(llmOutput))
            {
                error = "The model returned an empty response.";
                return false;
            }

            string json = ExtractJson(llmOutput);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The model response did not contain a JSON command object or array.";
                return false;
            }

            try
            {
                return TryParseJson(json, out sequence, out error);
            }
            catch (Exception exception)
            {
                if (TryWrapLooseObjectSequence(json, out string wrappedJson))
                {
                    try
                    {
                        return TryParseJson(wrappedJson, out sequence, out error);
                    }
                    catch (Exception wrappedException)
                    {
                        error = $"Failed to parse command JSON: {wrappedException.Message}";
                        return false;
                    }
                }

                error = $"Failed to parse command JSON: {exception.Message}";
                return false;
            }
        }

        public static string ToJson(RobotCommandSequence sequence)
        {
            return JsonConvert.SerializeObject(sequence, Formatting.Indented);
        }

        private static bool TryParseJson(string json, out RobotCommandSequence sequence, out string error)
        {
            JToken root = JToken.Parse(json);
            sequence = ParseRootToken(root, out error);

            if (sequence == null || sequence.IsEmpty)
            {
                error = "The JSON did not contain any commands.";
                return false;
            }

            sequence.Normalize();
            return true;
        }

        private static RobotCommandSequence ParseRootToken(JToken root, out string error)
        {
            error = string.Empty;

            if (root is JObject rootObject)
            {
                if (rootObject.TryGetValue("sequence", StringComparison.OrdinalIgnoreCase, out JToken sequenceToken))
                {
                    if (sequenceToken.Type != JTokenType.Array)
                    {
                        error = "The command sequence must be a JSON array.";
                        return null;
                    }

                    return SequenceFromArray((JArray)sequenceToken);
                }

                RobotCommand command = CommandFromObject(rootObject);
                return RobotCommandSequence.FromCommand(command);
            }

            if (root is JArray rootArray)
            {
                return SequenceFromArray(rootArray);
            }

            error = "The command JSON root must be an object or array.";
            return null;
        }

        private static RobotCommandSequence SequenceFromArray(JArray array)
        {
            List<RobotCommand> commands = new();
            foreach (JToken token in array)
            {
                if (token is JObject commandObject)
                {
                    commands.Add(CommandFromObject(commandObject));
                }
            }

            return new RobotCommandSequence(commands);
        }

        private static RobotCommand CommandFromObject(JObject commandObject)
        {
            RobotCommand command = commandObject.ToObject<RobotCommand>();
            if (command != null)
            {
                command.HasExplicitAmount = commandObject.TryGetValue("amount", StringComparison.OrdinalIgnoreCase, out _);
                if (command.nodeCount <= 0 &&
                    commandObject.TryGetValue("nodeCount", StringComparison.OrdinalIgnoreCase, out JToken nodeCountToken) &&
                    nodeCountToken.Type == JTokenType.Integer)
                {
                    command.nodeCount = nodeCountToken.Value<int>();
                }
            }

            return command;
        }

        private static string ExtractJson(string value)
        {
            string trimmed = value.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLineBreak = trimmed.IndexOf('\n');
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

                if (firstLineBreak >= 0 && lastFence > firstLineBreak)
                {
                    trimmed = trimmed.Substring(firstLineBreak + 1, lastFence - firstLineBreak - 1).Trim();
                }
            }

            int objectStart = trimmed.IndexOf('{');
            int objectEnd = trimmed.LastIndexOf('}');
            int arrayStart = trimmed.IndexOf('[');
            int arrayEnd = trimmed.LastIndexOf(']');

            bool hasObject = objectStart >= 0 && objectEnd > objectStart;
            bool hasArray = arrayStart >= 0 && arrayEnd > arrayStart;
            bool useArray = hasArray && (!hasObject || arrayStart < objectStart);

            if (!hasObject && !hasArray)
            {
                Debug.LogWarning($"Robot command parser received non-JSON output: {value}");
                return string.Empty;
            }

            int start = useArray ? arrayStart : objectStart;
            int end = useArray ? arrayEnd : objectEnd;
            return trimmed.Substring(start, end - start + 1);
        }

        private static bool TryWrapLooseObjectSequence(string json, out string wrappedJson)
        {
            wrappedJson = string.Empty;
            string trimmed = json.Trim();

            if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
                !trimmed.EndsWith("}", StringComparison.Ordinal) ||
                !HasLooseObjectSeparator(trimmed))
            {
                return false;
            }

            wrappedJson = $"[{NormalizeLooseObjectSequence(trimmed)}]";
            return true;
        }

        private static bool HasLooseObjectSeparator(string json)
        {
            for (int i = 0; i < json.Length - 1; i++)
            {
                if (json[i] != '}')
                {
                    continue;
                }

                int nextIndex = i + 1;
                while (nextIndex < json.Length && char.IsWhiteSpace(json[nextIndex]))
                {
                    nextIndex++;
                }

                if (nextIndex < json.Length && (json[nextIndex] == ',' || json[nextIndex] == '{'))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeLooseObjectSequence(string json)
        {
            StringBuilder builder = new();
            int depth = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                builder.Append(current);

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth = Mathf.Max(0, depth - 1);
                    if (depth != 0)
                    {
                        continue;
                    }

                    int nextIndex = i + 1;
                    while (nextIndex < json.Length && char.IsWhiteSpace(json[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex < json.Length && json[nextIndex] == ',')
                    {
                        builder.Append(',');
                        i = nextIndex;
                    }
                    else if (nextIndex < json.Length && json[nextIndex] == '{')
                    {
                        builder.Append(',');
                        i = nextIndex - 1;
                    }
                }
            }

            return builder.ToString();
        }
    }
}
