using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline.Robots
{
    [RequireComponent(typeof(SphereCollider))]
    public class ScanningRobotController : BaseRobotController
    {
        [SerializeField] private SphereCollider scanTrigger;
        [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField] private bool showIndicatorsOnScannedTargets = true;

        private readonly List<CommandTarget> lastScannedTargets = new();
        private readonly HashSet<CommandTarget> indicatedTargets = new();
        private CommandTargetRegistry registeredRegistry;
        private float nextRefreshTime;

        public event Action<float> ScanStarted;
        public event Action<IReadOnlyList<CommandTarget>> ScanCompleted;

        public float ScanRadius => GetWorldScanRadius();
        public IReadOnlyList<CommandTarget> LastScannedTargets => lastScannedTargets;

        protected override void Awake()
        {
            base.Awake();
            scanTrigger ??= GetComponent<SphereCollider>();

            if (scanTrigger != null)
            {
                scanTrigger.isTrigger = true;
            }

            if (CommandExecutor != null)
            {
                CommandExecutor.enabled = false;
            }
        }

        private void OnEnable()
        {
            RegisterScanner();
            RefreshScannedTargets();
        }

        private void Start()
        {
            RegisterScanner();
            RefreshScannedTargets();
        }

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + Mathf.Max(0.1f, refreshInterval);
            RefreshScannedTargets();
        }

        private void OnDisable()
        {
            registeredRegistry?.UnregisterScanner(this);
            registeredRegistry = null;
        }

        public override bool CanExecuteAction(RobotCommandAction action)
        {
            return false;
        }

        public bool IsPositionInScanRange(Vector3 position)
        {
            if (scanTrigger == null)
            {
                return false;
            }

            Vector3 localPosition = scanTrigger.transform.InverseTransformPoint(position);
            return (localPosition - scanTrigger.center).sqrMagnitude <= scanTrigger.radius * scanTrigger.radius;
        }

        public void RefreshScannedTargets()
        {
            RegisterScanner();
            ScanStarted?.Invoke(ScanRadius);

            lastScannedTargets.Clear();
            indicatedTargets.RemoveWhere(target => target == null);

            CommandTargetRegistry registry = registeredRegistry != null ? registeredRegistry : CommandTargetRegistry.Instance;
            if (registry != null)
            {
                foreach (CommandTarget target in registry.Targets)
                {
                    if (target == null ||
                        target.TargetType != CommandTargetType.ResourceNode ||
                        !target.gameObject.activeInHierarchy ||
                        !IsPositionInScanRange(target.DestinationPosition))
                    {
                        continue;
                    }

                    lastScannedTargets.Add(target);
                    ShowTargetIndicator(target);
                }
            }

            ScanCompleted?.Invoke(lastScannedTargets);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetResourceTarget(other, out CommandTarget target) ||
                !IsPositionInScanRange(target.DestinationPosition))
            {
                return;
            }

            if (!lastScannedTargets.Contains(target))
            {
                lastScannedTargets.Add(target);
                ShowTargetIndicator(target);
                ScanCompleted?.Invoke(lastScannedTargets);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetResourceTarget(other, out CommandTarget target))
            {
                return;
            }

            if (lastScannedTargets.Remove(target))
            {
                ScanCompleted?.Invoke(lastScannedTargets);
            }
        }

        private void RegisterScanner()
        {
            CommandTargetRegistry registry = CommandTargetRegistry.Instance;
            if (registry == null || registeredRegistry == registry)
            {
                return;
            }

            registeredRegistry?.UnregisterScanner(this);
            registeredRegistry = registry;
            registeredRegistry.RegisterScanner(this);
        }

        private float GetWorldScanRadius()
        {
            if (scanTrigger == null)
            {
                return 0f;
            }

            Vector3 scale = scanTrigger.transform.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return Mathf.Max(0f, scanTrigger.radius * largestAxis);
        }

        private static bool TryGetResourceTarget(Collider other, out CommandTarget target)
        {
            target = other != null ? other.GetComponentInParent<CommandTarget>() : null;
            return target != null && target.TargetType == CommandTargetType.ResourceNode;
        }

        private void ShowTargetIndicator(CommandTarget target)
        {
            if (!showIndicatorsOnScannedTargets)
            {
                return;
            }

            if (!indicatedTargets.Add(target))
            {
                return;
            }

            IndicatorManager indicator = target.GetComponentInChildren<IndicatorManager>();
            indicator?.ShowIndictor();
        }
    }
}
