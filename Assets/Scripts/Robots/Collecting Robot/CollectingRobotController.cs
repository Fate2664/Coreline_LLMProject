using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(CollectingRobotCommandExecutor))]
    public class CollectingRobotController : BaseRobotController
    {
        [SerializeField] private CollectingRobotInventory inventory;
        [SerializeField] private MiningRobotController selectedMiningRobot;
        [SerializeField] private SphereCollider visionTrigger;
        [SerializeField] private LayerMask visionLayers = ~0;
        [SerializeField] private float fallbackVisionRadius = 8f;
        [SerializeField] private bool refreshVisionWithOverlap = true;
        [SerializeField] private bool orePickupsRequireScan = true;

        private readonly HashSet<CommandTarget> visiblePickupTargets = new();
        private Animator animator;
        private StateMachine stateMachine;
        
        public CollectingRobotInventory Inventory => inventory;
        public MiningRobotController SelectedMiningRobot => selectedMiningRobot;
        protected override string RobotTargetIdPrefix => "CollectionRobot";

        protected override void Awake()
        {
            base.Awake();
            animator = GetComponentInChildren<Animator>();

            if (visionTrigger != null)
            {
                visionTrigger.isTrigger = true;
            }
            
            stateMachine = new StateMachine();

            var idleState = new CollectionRobotIdleState(this, animator);
            var walkState = new CollectionRobotWalkState(this, animator);
            
            Any(walkState, new FuncPredicate(ShouldEnterWalkState));
            At(walkState, idleState, new FuncPredicate(ShouldEnterIdleState));
            
            stateMachine.SetState(idleState);
        }
        
        private void At(IState from, IState to, IPredicate condition) =>
            stateMachine.AddTransition(from, to, condition);
        private void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

        private void Update()
        {
            stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            stateMachine?.FixedUpdate();
        }

        public void SetSelectedMiningRobot(MiningRobotController miningRobot)
        {
            selectedMiningRobot = miningRobot;
        }

        public bool TryGetNearestVisiblePickup(string resource, Vector3 origin, out CommandTarget target)
        {
            target = null;
            float bestDistanceSqr = float.MaxValue;

            RefreshVisiblePickupTargets();

            foreach (CommandTarget candidate in visiblePickupTargets)
            {
                if (!IsVisiblePickupTarget(candidate) || !candidate.MatchesResource(resource))
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

        public List<CommandTarget> GetVisiblePickupTargets(string resource, Vector3 origin)
        {
            RefreshVisiblePickupTargets();

            List<CommandTarget> results = new();
            foreach (CommandTarget target in visiblePickupTargets)
            {
                if (IsVisiblePickupTarget(target) && target.MatchesResource(resource))
                {
                    results.Add(target);
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

        public void ForgetVisiblePickup(CommandTarget target)
        {
            if (target != null)
            {
                visiblePickupTargets.Remove(target);
            }
        }

        public bool CanSeePickupTarget(CommandTarget target)
        {
            return IsVisiblePickupTarget(target);
        }
        
        public override bool CanExecuteAction(RobotCommandAction action)
        {
            return base.CanExecuteAction(action) ||
                   action == RobotCommandAction.Pickup ||
                   action == RobotCommandAction.Deliver;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (TryGetPickupTarget(other, out CommandTarget target))
            {
                visiblePickupTargets.Add(target);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (TryGetPickupTarget(other, out CommandTarget target))
            {
                visiblePickupTargets.Remove(target);
            }
        }

        private void RefreshVisiblePickupTargets()
        {
            visiblePickupTargets.RemoveWhere(target => !IsVisiblePickupTarget(target));

            if (!refreshVisionWithOverlap)
            {
                return;
            }

            Vector3 center;
            float radius;

            if (visionTrigger != null)
            {
                center = visionTrigger.transform.TransformPoint(visionTrigger.center);
                radius = GetWorldSphereRadius(visionTrigger);
            }
            else
            {
                center = transform.position;
                radius = Mathf.Max(0.1f, fallbackVisionRadius);
            }

            Collider[] hits = Physics.OverlapSphere(center, radius, visionLayers, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                if (TryGetPickupTarget(hit, out CommandTarget target) && IsVisiblePickupTarget(target))
                {
                    visiblePickupTargets.Add(target);
                }
            }
        }

        private static bool TryGetPickupTarget(Collider collider, out CommandTarget target)
        {
            target = collider != null ? collider.GetComponentInParent<CommandTarget>() : null;
            return IsUsablePickupTarget(target);
        }

        private static bool IsUsablePickupTarget(CommandTarget target)
        {
            return target != null &&
                   target.gameObject.activeInHierarchy &&
                   target.TargetType == CommandTargetType.PickupItem;
        }

        private bool IsVisiblePickupTarget(CommandTarget target)
        {
            if (!IsUsablePickupTarget(target))
            {
                return false;
            }

            if (!orePickupsRequireScan || !IsOrePickupTarget(target))
            {
                return true;
            }

            CommandTargetRegistry registry = CommandTargetRegistry.Instance;
            return registry != null && registry.IsTargetVisible(target, transform.position);
        }

        private static bool IsOrePickupTarget(CommandTarget target)
        {
            return target != null &&
                   (target.HasOreType || target.InventoryItemData is OreItemSO);
        }

        private static float GetWorldSphereRadius(SphereCollider sphereCollider)
        {
            if (sphereCollider == null)
            {
                return 0f;
            }

            Vector3 scale = sphereCollider.transform.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0.1f, sphereCollider.radius * largestAxis);
        }

        #region State Checks

        private bool ShouldEnterWalkState()
        {
            return CurrentState == RobotWorkState.Walking && !stateMachine.IsInState<CollectionRobotWalkState>();
        }

        private bool ShouldEnterIdleState()
        {
            return CurrentState == RobotWorkState.Idle && !stateMachine.IsInState<CollectionRobotIdleState>();
        }

        #endregion
        
    }
}
