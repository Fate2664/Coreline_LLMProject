using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Coreline.Robots
{
    [RequireComponent(typeof(MiningRobotCommandExecutor))]
    public class MiningRobotController : BaseRobotController
    {
        [SerializeField] private Transform toolTransform;
        [SerializeField] private MultiAimConstraint lookAimConstraint;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private Transform defaultLookAimTarget;
        [SerializeField] private float activeLookAimWeight = 1f;

        private StateMachine stateMachine;
        private Animator animator;
        private Transform generatedDefaultLookAimTarget;
        private bool hasLookAimSource;

        public Transform ToolTransform => toolTransform != null ? toolTransform : transform;
        public bool IsMiningAnimationReady => stateMachine != null && stateMachine.IsInState<RobotMineState>();
        protected override string RobotTargetIdPrefix => "MiningRobot";

        protected override void Awake()
        {
            base.Awake();
            animator = GetComponentInChildren<Animator>();
            lookAimConstraint ??= GetComponentInChildren<MultiAimConstraint>(true);
            rigBuilder ??= GetComponentInChildren<RigBuilder>(true);
            EnsureDefaultLookAimTarget();
            CacheLookAimSourceState();
            
            stateMachine = new  StateMachine();

            var idleState = new RobotIdleState(this, animator);
            var walkState = new RobotWalkState(this, animator);
            var mining = new RobotMineState(this, animator);
            
            Any(walkState, new FuncPredicate(ShouldEnterWalkState));
            Any(mining, new FuncPredicate(ShouldEnterMiningState));
            
            At(mining, idleState, new FuncPredicate(ShouldEnterIdleState));
            At(walkState, idleState, new FuncPredicate(ShouldEnterIdleState));
            
            stateMachine.SetState(idleState);
        }

        public override bool CanExecuteAction(RobotCommandAction action)
        {
            return base.CanExecuteAction(action) || action == RobotCommandAction.MineResource;
        }

        private void Update()
        {
            stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            stateMachine?.FixedUpdate();
        }
        
        private void At(IState from, IState to, IPredicate condition) =>
            stateMachine.AddTransition(from, to, condition);
        private void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

        public override void HandleMining()
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
                return;

            Agent.isStopped = true;

            if (IsPaused)
            {
                return;
            }

            if (Agent.hasPath)
            {
                Agent.ResetPath();
            }
        }

        public void SetMiningLookTarget(Transform target)
        {
            if (lookAimConstraint == null || target == null)
            {
                return;
            }

            SetLookAimSource(target, activeLookAimWeight);
        }

        public void ClearMiningLookTarget()
        {
            if (lookAimConstraint == null)
            {
                return;
            }

            Transform fallback = defaultLookAimTarget != null ? defaultLookAimTarget : EnsureDefaultLookAimTarget();
            SetLookAimSource(fallback, hasLookAimSource ? 1f : 0f);
        }

        private Transform EnsureDefaultLookAimTarget()
        {
            if (defaultLookAimTarget != null)
            {
                return defaultLookAimTarget;
            }

            if (generatedDefaultLookAimTarget == null)
            {
                GameObject fallback = new("DefaultLookAimTarget");
                fallback.transform.SetParent(transform, false);
                fallback.transform.localPosition = Vector3.forward * 3f + Vector3.up * 1.5f;
                generatedDefaultLookAimTarget = fallback.transform;
            }

            return generatedDefaultLookAimTarget;
        }

        private void CacheLookAimSourceState()
        {
            if (lookAimConstraint == null)
            {
                return;
            }

            WeightedTransformArray sources = lookAimConstraint.data.sourceObjects;
            hasLookAimSource = sources.Count > 0 && sources[0].transform != null;
            if (hasLookAimSource && defaultLookAimTarget == null)
            {
                defaultLookAimTarget = sources[0].transform;
            }
        }

        private void SetLookAimSource(Transform target, float sourceWeight)
        {
            MultiAimConstraintData data = lookAimConstraint.data;
            WeightedTransformArray sources = data.sourceObjects;

            if (sources.Count == 0)
            {
                sources.Add(new WeightedTransform(target, sourceWeight));
            }
            else
            {
                sources.SetTransform(0, target);
                sources.SetWeight(0, sourceWeight);
            }

            data.sourceObjects = sources;
            lookAimConstraint.data = data;
            lookAimConstraint.weight = target != null ? activeLookAimWeight : 0f;
            RebuildLookAimRig();
        }

        private void RebuildLookAimRig()
        {
            rigBuilder ??= lookAimConstraint != null
                ? lookAimConstraint.GetComponentInParent<RigBuilder>()
                : GetComponentInChildren<RigBuilder>(true);

            if (rigBuilder == null || !rigBuilder.isActiveAndEnabled)
            {
                return;
            }

            // Animation Rigging binds source transform handles when the rig graph is built.
            // Rebuild after swapping the source so the runtime job follows the new node.
            if (Application.isPlaying)
            {
                if (rigBuilder.Build())
                {
                    rigBuilder.SyncLayers();
                }
            }
            else
            {
                rigBuilder.SyncLayers();
            }
        }
        
        #region State Checks

        private bool ShouldEnterWalkState()
        {
            return CurrentState == RobotWorkState.Walking &&
                   !stateMachine.IsInState<RobotWalkState>();
        }

        private bool ShouldEnterIdleState()
        {
            return CurrentState == RobotWorkState.Idle &&
                   !stateMachine.IsInState<RobotIdleState>();
        }

        private bool ShouldEnterMiningState()
        {
            return CurrentState == RobotWorkState.Mining;
        }
            
        #endregion

    }
}
