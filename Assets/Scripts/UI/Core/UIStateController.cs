using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    [DefaultExecutionOrder(-1000)]
    public sealed class UIStateController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private GameInput input;

        [Header("Gameplay Cursor")]
        [SerializeField] private CursorLockMode gameplayCursorLockMode = CursorLockMode.Locked;
        [SerializeField] private bool gameplayCursorVisible;

        [Header("UI Cursor")]
        [SerializeField] private CursorLockMode uiCursorLockMode = CursorLockMode.None;
        [SerializeField] private bool uiCursorVisible = true;

        private readonly List<UIPanel> openPanels = new();
        private int modalInputBlockCount;
        private int modalCursorRequestCount;

        public static UIStateController Instance { get; private set; }

        public event Action StateChanged;

        public IReadOnlyList<UIPanel> OpenPanels => openPanels;
        public bool HasOpenPanels => openPanels.Count > 0;
        public bool BlocksGameplayInput =>
            modalInputBlockCount > 0 ||
            HasPanelMatching(panel => panel.BlocksGameplayInput);
        public bool RequiresCursor =>
            modalCursorRequestCount > 0 ||
            HasPanelMatching(panel => panel.RequiresCursor);
        public UIPanel TopPanel => openPanels.Count > 0 ? openPanels[^1] : null;
        public static bool GameplayInputBlocked =>
            Instance != null && Instance.BlocksGameplayInput;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"Only one {nameof(UIStateController)} should exist in a scene.",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            RemoveInvalidPanels();
            ApplyCursorState();
        }

        private void OnEnable()
        {
            if (Instance != this)
            {
                return;
            }

            if (input != null)
            {
                input.Exit += HandleExitInput;
            }

            ApplyCursorState();
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Exit -= HandleExitInput;
            }

            if (Instance == this)
            {
                ApplyGameplayCursorState();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && Instance == this)
            {
                ApplyCursorState();
            }
        }

        public bool IsOpen(UIPanel panel)
        {
            return panel != null && openPanels.Contains(panel);
        }

        public void RegisterOpenPanel(UIPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            RemoveInvalidPanels();
            openPanels.Remove(panel);
            openPanels.Add(panel);
            RefreshState();
        }

        public void UnregisterOpenPanel(UIPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            if (openPanels.Remove(panel))
            {
                RefreshState();
            }
        }

        public void BringToFront(UIPanel panel)
        {
            if (panel == null || !openPanels.Remove(panel))
            {
                return;
            }

            openPanels.Add(panel);
            RefreshState();
        }

        public void RegisterModalInputBlock(bool requiresCursor = true)
        {
            modalInputBlockCount++;

            if (requiresCursor)
            {
                modalCursorRequestCount++;
            }

            RefreshState();
        }

        public void UnregisterModalInputBlock(bool requiredCursor = true)
        {
            modalInputBlockCount = Mathf.Max(0, modalInputBlockCount - 1);

            if (requiredCursor)
            {
                modalCursorRequestCount = Mathf.Max(0, modalCursorRequestCount - 1);
            }

            RefreshState();
        }

        public bool CloseTopPanel()
        {
            RemoveInvalidPanels();

            for (int i = openPanels.Count - 1; i >= 0; i--)
            {
                UIPanel panel = openPanels[i];
                if (panel != null && panel.CloseOnExit)
                {
                    panel.Close();
                    return true;
                }
            }

            return false;
        }

        public void CloseAllPanels()
        {
            UIPanel[] panels = openPanels.ToArray();
            for (int i = panels.Length - 1; i >= 0; i--)
            {
                panels[i]?.Close();
            }
        }

        private void HandleExitInput(bool pressed)
        {
            if (pressed)
            {
                CloseTopPanel();
            }
        }

        private bool HasPanelMatching(Predicate<UIPanel> matches)
        {
            RemoveInvalidPanels();

            foreach (UIPanel panel in openPanels)
            {
                if (matches(panel))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveInvalidPanels()
        {
            openPanels.RemoveAll(panel => panel == null);
        }

        private void RefreshState()
        {
            ApplyCursorState();
            StateChanged?.Invoke();
        }

        private void ApplyCursorState()
        {
            if (RequiresCursor)
            {
                Cursor.lockState = uiCursorLockMode;
                Cursor.visible = uiCursorVisible;
                return;
            }

            ApplyGameplayCursorState();
        }

        private void ApplyGameplayCursorState()
        {
            Cursor.lockState = gameplayCursorLockMode;
            Cursor.visible = gameplayCursorVisible;
        }
    }
}
