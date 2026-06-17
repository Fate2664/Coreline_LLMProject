using System.Numerics;
using DG.Tweening;
using Nova;
using Platformer;
using Unity.VisualScripting;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

[System.Serializable]
public class TabButtonVisuals : ItemVisuals
{
    public TextBlock label = null;
    public UIBlock2D Background = null;
    public static float HoverScale = 1.1f;

    public bool isSelected
    {
        get => Background.BodyEnabled;
        set
        {
            Background.BodyEnabled = value;
            label.Color = value ? Color.black : Color.white;
            
        }
    }

    internal static void HandlePress(Gesture.OnPress evt, TabButtonVisuals target, int index)
    {
        //Play Audio SFX
        AudioManager.Instance.Play("ClickSound");
    }

    internal static void HandleHover(Gesture.OnHover evt, TabButtonVisuals target, int index)
    {
        target.label.DOKill();
        target.label.transform.DOScale(target.label.transform.localScale * HoverScale, 0.2f).SetEase(Ease.OutBack);
        //Play Audio SFX
        AudioManager.Instance.Play("HoverSound");
    }

    internal static void HandleUnHover(Gesture.OnUnhover evt, TabButtonVisuals target, int index)
    {
        target.label.DOKill();
        target.label.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
    }
}