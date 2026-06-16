using System;
using DG.Tweening;
using Nova;
using UnityEngine;

namespace Coreline
{
    public sealed class UIVisuals
    {
        private readonly Transform target;
        private readonly Vector3 visibleScale;
        private Tween activeTween;

        public UIVisuals(UIBlock2D background) : this(background != null ? background.transform : null)
        {
        }

        public UIVisuals(Transform target)
        {
            this.target = target;
            visibleScale = target != null && target.localScale != Vector3.zero
                ? target.localScale
                : Vector3.one;
        }

        public void ShowUI(
            float duration = 0.25f,
            bool useUnscaledTime = true,
            Action onComplete = null)
        {
            AnimateTo(visibleScale, duration, Ease.OutBack, useUnscaledTime, onComplete);
        }

        public void HideUI(
            float duration = 0.2f,
            bool useUnscaledTime = true,
            Action onComplete = null)
        {
            AnimateTo(Vector3.zero, duration, Ease.OutQuad, useUnscaledTime, onComplete);
        }

        public void ShowImmediate()
        {
            KillAnimation();
            if (target != null)
            {
                target.localScale = visibleScale;
            }
        }

        public void HideImmediate()
        {
            KillAnimation();
            if (target != null)
            {
                target.localScale = Vector3.zero;
            }
        }

        public void KillAnimation()
        {
            if (activeTween == null)
            {
                return;
            }

            activeTween.Kill();
            activeTween = null;
        }

        private void AnimateTo(Vector3 scale, float duration, Ease ease, bool useUnscaledTime, Action onComplete)
        {
            KillAnimation();

            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (duration <= 0f)
            {
                target.localScale = scale;
                onComplete?.Invoke();
                return;
            }

            activeTween = target
                .DOScale(scale, duration)
                .SetEase(ease)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() =>
                {
                    activeTween = null;
                    onComplete?.Invoke();
                });
        }
    }
}
