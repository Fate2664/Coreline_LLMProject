using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Coreline.Robots
{
    [Serializable]
    public class RobotCommandSequence
    {
        [JsonProperty("sequence")] public List<RobotCommand> sequence = new();

        [JsonIgnore] public bool IsEmpty => sequence == null || sequence.Count == 0;

        public RobotCommandSequence()
        {
        }

        public RobotCommandSequence(IEnumerable<RobotCommand> commands)
        {
            sequence = commands == null ? new List<RobotCommand>() : new List<RobotCommand>(commands);
            Normalize();
        }

        public static RobotCommandSequence FromCommand(RobotCommand command)
        {
            RobotCommandSequence result = new();
            if (command != null)
            {
                result.sequence.Add(command);
            }

            result.Normalize();
            return result;
        }

        public void Normalize()
        {
            sequence ??= new List<RobotCommand>();

            for (int i = sequence.Count - 1; i >= 0; i--)
            {
                if (sequence[i] == null)
                {
                    sequence.RemoveAt(i);
                    continue;
                }

                sequence[i].Normalize();
            }
        }
    }
}
