using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    public class RobotCommandQueue : MonoBehaviour
    {
        [SerializeField] private List<RobotCommand> pendingCommands = new();

        public event Action QueueChanged;

        public int Count => pendingCommands.Count;
        public bool HasCommands => pendingCommands.Count > 0;
        public IReadOnlyList<RobotCommand> PendingCommands => pendingCommands;

        public void Enqueue(RobotCommand command)
        {
            if (command == null)
            {
                return;
            }

            command.Normalize();

            if (command.PriorityType == RobotCommandPriority.High)
            {
                int insertIndex = pendingCommands.FindLastIndex(existing => existing.PriorityType == RobotCommandPriority.High) + 1;
                pendingCommands.Insert(insertIndex, command);
            }
            else
            {
                pendingCommands.Add(command);
            }

            QueueChanged?.Invoke();
        }

        public void EnqueueSequence(RobotCommandSequence sequence)
        {
            if (sequence == null || sequence.IsEmpty)
            {
                return;
            }

            foreach (RobotCommand command in sequence.sequence)
            {
                Enqueue(command);
            }
        }

        public bool TryDequeue(out RobotCommand command)
        {
            command = null;

            if (pendingCommands.Count == 0)
            {
                return false;
            }

            command = pendingCommands[0];
            pendingCommands.RemoveAt(0);
            QueueChanged?.Invoke();
            return true;
        }

        public bool TryPeek(out RobotCommand command)
        {
            command = pendingCommands.Count > 0 ? pendingCommands[0] : null;
            return command != null;
        }

        public void Clear()
        {
            if (pendingCommands.Count == 0)
            {
                return;
            }

            pendingCommands.Clear();
            QueueChanged?.Invoke();
        }
    }
}
