using DG.Tweening;
using Nova;
using UnityEngine;
using UnityEngine.Events;

namespace Platformer
{
    [RequireComponent(typeof(UIBlock2D))]
    public class MenuButtonVisuals : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIBlock2D background;
        [SerializeField] private TextBlock buttonLabel;

        [Header("Hover Appearance")]
        [SerializeField] private Color hoverBackgroundColor = Color.white;
        [SerializeField] private Color hoverTextColor = Color.black;
        [SerializeField] private float hoverScale = 1.05f;

        [Header("Animation")]
        [SerializeField] private float pressedScale = 0.98f;
        [SerializeField] private float animationDuration = 0.15f;

        [Header("Events")]
        [SerializeField] private UnityEvent onClicked;

        private Vector3 defaultScale;
        private Color defaultBackgroundColor;
        private Color defaultTextColor;
        private bool defaultBodyEnabled;
        private bool isHovered;
        private bool handlersRegistered;

        private void Awake()
        {
            background ??= GetComponent<UIBlock2D>();
            if (background == null)
            {
                enabled = false;
                return;
            }

            defaultScale = background.transform.localScale;
            defaultBackgroundColor = background.Color;
            defaultBodyEnabled = background.BodyEnabled;

            if (buttonLabel != null)
            {
                defaultTextColor = buttonLabel.Color;
            }
        }

        private void OnEnable()
        {
            if (background == null || handlersRegistered)
            {
                return;
            }

            background.AddGestureHandler<Gesture.OnHover>(HandleHover);
            background.AddGestureHandler<Gesture.OnUnhover>(HandleUnhover);
            background.AddGestureHandler<Gesture.OnPress>(HandlePress);
            background.AddGestureHandler<Gesture.OnRelease>(HandleRelease);
            background.AddGestureHandler<Gesture.OnCancel>(HandleCancel);
            background.AddGestureHandler<Gesture.OnClick>(HandleClick);
            handlersRegistered = true;
        }

        private void OnDisable()
        {
            if (background == null || !handlersRegistered)
            {
                return;
            }

            background.RemoveGestureHandler<Gesture.OnHover>(HandleHover);
            background.RemoveGestureHandler<Gesture.OnUnhover>(HandleUnhover);
            background.RemoveGestureHandler<Gesture.OnPress>(HandlePress);
            background.RemoveGestureHandler<Gesture.OnRelease>(HandleRelease);
            background.RemoveGestureHandler<Gesture.OnCancel>(HandleCancel);
            background.RemoveGestureHandler<Gesture.OnClick>(HandleClick);
            handlersRegistered = false;
        }

        private void HandleHover(Gesture.OnHover evt)
        {
            isHovered = true;
            background.BodyEnabled = true;
            background.Color = hoverBackgroundColor;

            if (buttonLabel != null)
            {
                buttonLabel.Color = hoverTextColor;
            }
            
            AudioManager.Instance?.Play("HoverSound");
            AnimateScale(defaultScale * hoverScale, Ease.OutBack);
        }

        private void HandleUnhover(Gesture.OnUnhover evt)
        {
            isHovered = false;
            background.BodyEnabled = defaultBodyEnabled;
            background.Color = defaultBackgroundColor;

            if (buttonLabel != null)
            {
                buttonLabel.Color = defaultTextColor;
            }
            AnimateScale(defaultScale, Ease.OutQuad);
        }

        private void HandlePress(Gesture.OnPress evt)
        {
            AudioManager.Instance?.Play("ClickSound");
            AnimateScale(defaultScale * pressedScale, Ease.OutQuad);
        }

        private void HandleRelease(Gesture.OnRelease evt)
        {
            AnimateScale(defaultScale * (isHovered ? hoverScale : 1f), Ease.OutBack);
        }

        private void HandleCancel(Gesture.OnCancel evt)
        {
            AnimateScale(defaultScale * (isHovered ? hoverScale : 1f), Ease.OutQuad);
        }

        private void HandleClick(Gesture.OnClick evt)
        {
            onClicked?.Invoke();
        }

        private void AnimateScale(Vector3 targetScale, Ease ease)
        {
            background.DOKill();
            background.transform
                .DOScale(targetScale, animationDuration)
                .SetEase(ease)
                .SetUpdate(true);
        }

    }
}
