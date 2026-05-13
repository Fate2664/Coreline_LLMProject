using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    public class CommandTargetRegistry : MonoBehaviour
    {
        private static CommandTargetRegistry instance;

        [SerializeField] private bool rebuildOnAwake = true;
        [SerializeField] private bool resourceNodesRequireScan = true;

        private readonly Dictionary<string, CommandTarget> targetsById = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly List<CommandTarget> targets = new();
        private readonly List<ScanningRobotController> scanners = new();

        public static CommandTargetRegistry Instance => instance;
        public IReadOnlyList<CommandTarget> Targets => targets;
        public IReadOnlyList<ScanningRobotController> Scanners => scanners;
        public bool ResourceNodesRequireScan => resourceNodesRequireScan;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(CommandTargetRegistry)} instances found. Using {name}.");
            }

            instance = this;

            if (rebuildOnAwake)
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Register(CommandTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (!targets.Contains(target))
            {
                targets.Add(target);
            }

            string id = target.TargetId;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (targetsById.TryGetValue(id, out CommandTarget existing) && existing != target)
            {
                Debug.LogWarning($"Duplicate command target id '{id}' on {target.name}. Keeping latest target.");
            }

            targetsById[id] = target;
        }

        public void Unregister(CommandTarget target)
        {
            if (target == null)
            {
                return;
            }

            targets.Remove(target);

            string id = target.TargetId;
            if (targetsById.TryGetValue(id, out CommandTarget existing) && existing == target)
            {
                targetsById.Remove(id);
            }
        }

        public void Rebuild()
        {
            targets.Clear();
            targetsById.Clear();
            scanners.RemoveAll(scanner => scanner == null);

            CommandTarget[] sceneTargets = FindObjectsByType<CommandTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (CommandTarget target in sceneTargets)
            {
                Register(target);
            }
        }

        public bool TryGetTarget(string targetId, out CommandTarget target)
        {
            return TryGetTarget(targetId, null, out target);
        }

        public bool TryGetTarget(string targetId, Vector3 viewerPosition, out CommandTarget target)
        {
            return TryGetTarget(targetId, (Vector3?)viewerPosition, out target);
        }

        private bool TryGetTarget(string targetId, Vector3? viewerPosition, out CommandTarget target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            if (targetsById.TryGetValue(targetId, out target) && IsTargetVisible(target, viewerPosition))
            {
                return true;
            }

            Rebuild();
            if (targetsById.TryGetValue(targetId, out target) && IsTargetVisible(target, viewerPosition))
            {
                return true;
            }

            target = null;
            return false;
        }

        public bool TryFindNearestResource(string resource, Vector3 origin, out CommandTarget target)
        {
            return TryFindNearest(CommandTargetType.ResourceNode, resource, origin, out target);
        }

        public bool TryFindNearestPickup(string resource, Vector3 origin, out CommandTarget target)
        {
            return TryFindNearest(CommandTargetType.PickupItem, resource, origin, out target);
        }

        public bool TryFindNearest(CommandTargetType targetType, string resource, Vector3 origin, out CommandTarget target)
        {
            target = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                CommandTarget candidate = targets[i];
                if (candidate == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                if (!IsTargetVisible(candidate, origin) ||
                    candidate.TargetType != targetType ||
                    !candidate.MatchesResource(resource))
                {
                    continue;
                }

                float distanceSqr = (candidate.DestinationPosition - origin).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                target = candidate;
            }

            return target != null;
        }

        public List<CommandTarget> FindTargets(CommandTargetType targetType, string resource, Vector3 origin)
        {
            List<CommandTarget> results = new();

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                CommandTarget candidate = targets[i];
                if (candidate == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                if (IsTargetVisible(candidate, origin) &&
                    candidate.TargetType == targetType &&
                    candidate.MatchesResource(resource))
                {
                    results.Add(candidate);
                }
            }

            results.Sort((left, right) =>
            {
                float leftDistance = (left.DestinationPosition - origin).sqrMagnitude;
                float rightDistance = (right.DestinationPosition - origin).sqrMagnitude;
                return leftDistance.CompareTo(rightDistance);
            });

            return results;
        }

        public void RegisterScanner(ScanningRobotController scanner)
        {
            if (scanner == null || scanners.Contains(scanner))
            {
                return;
            }

            scanners.Add(scanner);
        }

        public void UnregisterScanner(ScanningRobotController scanner)
        {
            if (scanner == null)
            {
                return;
            }

            scanners.Remove(scanner);
        }

        public bool IsTargetVisible(CommandTarget target)
        {
            return IsTargetVisible(target, null);
        }

        public bool IsTargetVisible(CommandTarget target, Vector3 viewerPosition)
        {
            return IsTargetVisible(target, (Vector3?)viewerPosition);
        }

        private bool IsTargetVisible(CommandTarget target, Vector3? viewerPosition)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (target.TargetType != CommandTargetType.ResourceNode)
            {
                return true;
            }

            return !resourceNodesRequireScan ||
                   viewerPosition.HasValue &&
                   IsResourceVisibleFrom(target, viewerPosition.Value);
        }

        private bool IsResourceVisibleFrom(CommandTarget target, Vector3 viewerPosition)
        {
            for (int i = scanners.Count - 1; i >= 0; i--)
            {
                ScanningRobotController scanner = scanners[i];
                if (scanner == null)
                {
                    scanners.RemoveAt(i);
                    continue;
                }

                if (!scanner.isActiveAndEnabled)
                {
                    continue;
                }

                if (scanner.IsPositionInScanRange(viewerPosition) &&
                    scanner.IsPositionInScanRange(target.DestinationPosition))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
