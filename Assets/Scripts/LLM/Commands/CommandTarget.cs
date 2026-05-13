using System;
using UnityEngine;

namespace Coreline.Robots
{
    public enum CommandTargetType
    {
        Waypoint,
        ResourceNode,
        PickupItem,
        Storage,
        Refinery,
        Robot,
        Other
    }

    [DisallowMultipleComponent]
    public class CommandTarget : MonoBehaviour
    {
        [SerializeField] private string targetId;
        [SerializeField] private CommandTargetType targetType = CommandTargetType.Other;
        [SerializeField] private OreType oreType;
        [SerializeField] private bool hasOreType;
        [SerializeField] private Transform destinationOverride;
        [SerializeField] private float interactionRadius = 1.5f;

        [Header("Pickup Data")]
        [SerializeField] private InventoryItemData inventoryItemData;
        [SerializeField] private int pickupAmount = 1;
        [SerializeField] private bool destroyOnPickup = true;

        public string TargetId => GetOrCreateTargetId();
        public CommandTargetType TargetType => targetType;
        public bool HasOreType => hasOreType;
        public OreType OreType => oreType;
        public Vector3 DestinationPosition => destinationOverride != null ? destinationOverride.position : transform.position;
        public float InteractionRadius => Mathf.Max(0.05f, interactionRadius);
        public InventoryItemData InventoryItemData => inventoryItemData;
        public int PickupAmount => Mathf.Max(1, pickupAmount);
        public bool DestroyOnPickup => destroyOnPickup;

        private void Awake()
        {
            GetOrCreateTargetId();
        }

        private void OnEnable()
        {
            CommandTargetRegistry.Instance?.Register(this);
        }

        private void OnDisable()
        {
            CommandTargetRegistry.Instance?.Unregister(this);
        }

        public bool TryGetMineable(out global::IMineable mineable)
        {
            mineable = GetComponentInParent<global::IMineable>();
            return mineable != null;
        }

        public bool TryGetOre(out Ore ore)
        {
            ore = GetComponentInParent<Ore>();
            return ore != null;
        }

        public void Configure(string id, CommandTargetType type, bool hasResource = false, OreType resourceType = default,
            Transform destination = null, float radius = 1.5f)
        {
            CommandTargetRegistry.Instance?.Unregister(this);

            targetId = id;
            targetType = type;
            hasOreType = hasResource;
            oreType = resourceType;
            destinationOverride = destination;
            interactionRadius = Mathf.Max(0.05f, radius);

            CommandTargetRegistry.Instance?.Register(this);
        }

        public bool MatchesResource(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                return true;
            }

            if (!hasOreType)
            {
                return false;
            }

            return string.Equals(oreType.ToString(), resource, StringComparison.OrdinalIgnoreCase);
        }

        public string BuildPromptDescription()
        {
            string resourceText = hasOreType ? oreType.ToString().ToLowerInvariant() : "none";
            return $"{TargetId} type={targetType} resource={resourceText}";
        }

        private string BuildDefaultTargetId()
        {
            string prefix = targetType switch
            {
                CommandTargetType.ResourceNode when hasOreType => $"{oreType.ToString().ToLowerInvariant()}_node",
                CommandTargetType.PickupItem when hasOreType => $"{oreType.ToString().ToLowerInvariant()}_ore",
                CommandTargetType.Storage => "storage",
                CommandTargetType.Refinery => "refinery",
                CommandTargetType.Robot => "robot",
                _ => "target"
            };

            return $"{prefix}_{Mathf.Abs(GetInstanceID())}";
        }

        private string GetOrCreateTargetId()
        {
            if (string.IsNullOrWhiteSpace(targetId) || ShouldGenerateUniqueResourceId())
            {
                targetId = BuildDefaultTargetId();
            }

            return targetId;
        }

        private bool ShouldGenerateUniqueResourceId()
        {
            if (targetType != CommandTargetType.ResourceNode || !hasOreType || string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string resourceName = oreType.ToString().ToLowerInvariant();
            string normalizedId = targetId.Trim().ToLowerInvariant();
            return normalizedId == resourceName || normalizedId == $"{resourceName}_node";
        }
    }
}
