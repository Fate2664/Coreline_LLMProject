using Nova;
using UnityEngine;
using UnityEngine.Events;

namespace Coreline
{
    [System.Serializable]
    public class InventoryButtonVisuals : ItemVisuals
    {
        public UIBlock2D ButtonRoot;
        public UnityEvent OnClicked = null;
        
        [Header("Animations")] 
        public Color DefaultColor;
        public Color HoverColor;
        public Color PressedColor;

        internal static void HandleHover(Gesture.OnHover evt, InventoryButtonVisuals target)
        {
            target.ButtonRoot.Color = target.HoverColor;
        }

        internal static void HandlePress(Gesture.OnPress evt, InventoryButtonVisuals target)
        {
            target.ButtonRoot.Color = target.PressedColor;
        }

        internal static void HandleUnhover(Gesture.OnUnhover evt, InventoryButtonVisuals target)
        {
            target.ButtonRoot.Color = target.DefaultColor;
        }

        internal static void HandleRelease(Gesture.OnRelease evt, InventoryButtonVisuals target)
        {
            target.ButtonRoot.Color = target.HoverColor;
            target.OnClicked?.Invoke();
        }
    }
}
