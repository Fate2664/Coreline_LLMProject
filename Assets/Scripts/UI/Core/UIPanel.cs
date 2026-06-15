using UnityEngine;
using UnityEngine.Events;
using Nova;

namespace Coreline
{
    [DisallowMultipleComponent]
    public sealed class UIPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIStateController stateController;
        [SerializeField] private UIBlock2D visualRoot;

        [Header("State")]
        [SerializeField] private bool startOpen;
        [SerializeField] private bool registerWhenEnabled = true;
        [SerializeField] private bool deactivateWhenClosed = true;
        [SerializeField] private bool blocksGameplayInput = true;
        [SerializeField] private bool requiresCursor = true;
        [SerializeField] private bool closeOnExit = true;

        [Header("Animation")]
        [SerializeField] private bool animateOnStart;
        [SerializeField, Min(0f)] private float openDuration = 0.25f;
        [SerializeField, Min(0f)] private float closeDuration = 0.2f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Events")]
        [SerializeField] private UnityEvent opened;
        [SerializeField] private UnityEvent closed;

        private UIVisuals visuals;
        private bool hasStarted;
        private bool isOpen;
        private bool isClosing;
        private bool isRegistered;

        public bool IsOpen => isOpen;
        public bool BlocksGameplayInput => blocksGameplayInput;
        public bool RequiresCursor => requiresCursor;
        public bool CloseOnExit => closeOnExit;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            hasStarted = true;

            if (startOpen)
            {
                Open(animateOnStart);
                return;
            }

            visuals.HideImmediate();
            FinishClose(invokeEvent: false);
        }

        private void OnEnable()
        {
            Initialize();

            if (hasStarted && registerWhenEnabled && !isOpen && !isClosing)
            {
                Open(animate: false);
            }
        }

        private void OnDisable()
        {
            visuals?.KillAnimation();
            isOpen = false;
            isClosing = false;
            Unregister();
        }

        public void Open()
        {
            Open(animate: true);
        }

        public void Open(bool animate)
        {
            Initialize();

            if (isOpen && !isClosing)
            {
                BringToFront();
                return;
            }

            isClosing = false;
            isOpen = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            Register();

            if (animate)
            {
                visuals.ShowUI(openDuration, useUnscaledTime, () => opened?.Invoke());
            }
            else
            {
                visuals.ShowImmediate();
                opened?.Invoke();
            }
        }

        public void Close()
        {
            Close(animate: true);
        }

        public void Close(bool animate)
        {
            if (!isOpen && !isClosing)
            {
                return;
            }

            isOpen = false;
            isClosing = true;

            if (animate)
            {
                visuals.HideUI(
                    closeDuration,
                    useUnscaledTime,
                    () => FinishClose(invokeEvent: true));
            }
            else
            {
                visuals.HideImmediate();
                FinishClose(invokeEvent: true);
            }
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void BringToFront()
        {
            ResolveStateController();
            stateController?.BringToFront(this);
        }

        private void Initialize()
        {
            visualRoot ??= GetComponent<UIBlock2D>();
            visualRoot ??= GetComponentInChildren<UIBlock2D>(true);
            visuals ??= new UIVisuals(visualRoot != null ? visualRoot.transform : transform);
            ResolveStateController();
        }

        private void ResolveStateController()
        {
            stateController ??= UIStateController.Instance;
            stateController ??=
                FindFirstObjectByType<UIStateController>(FindObjectsInactive.Include);
        }

        private void Register()
        {
            ResolveStateController();

            if (stateController == null)
            {
                Debug.LogWarning(
                    $"{name} cannot register because no {nameof(UIStateController)} exists in the scene.",
                    this);
                return;
            }

            stateController.RegisterOpenPanel(this);
            isRegistered = true;
        }

        private void Unregister()
        {
            if (!isRegistered)
            {
                return;
            }

            stateController?.UnregisterOpenPanel(this);
            isRegistered = false;
        }

        private void FinishClose(bool invokeEvent)
        {
            isClosing = false;
            Unregister();

            if (invokeEvent)
            {
                closed?.Invoke();
            }

            if (deactivateWhenClosed && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
