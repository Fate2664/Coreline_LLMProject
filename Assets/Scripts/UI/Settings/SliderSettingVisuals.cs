using DG.Tweening;
using Nova;
using Platformer;
using UnityEngine;

public class SliderSettingVisuals : ItemVisuals
{
    public UIBlock2D MainBackground = null;
    public UIBlock2D SliderBackground = null;
    public TextBlock SettingLabel = null;
    public UIBlock2D FillBar = null;
    public TextBlock ValueLabel = null;
    public float MinVisualOffset = 2.63f;
    public static float HoverScale = 1.05f;

    public bool isSelected
    {
        get => MainBackground.BodyEnabled;
        set
        {
            MainBackground.BodyEnabled = value;
            SettingLabel.Color = value ? Color.black : Color.white;
            ValueLabel.Color = value ? Color.black : Color.white;
            SliderBackground.Color = value ? Color.black : Color.white;
        }
    }
    
    public static void HandleHover(Gesture.OnHover evt, SliderSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen) return;
        
        AudioManager.Instance.Play("HoverSound");
        target.MainBackground.DOKill();
        target.MainBackground.transform.DOScale(target.SettingLabel.transform.localScale * HoverScale, 0.15f).SetEase(Ease.OutBack);
        target.isSelected = true;
    }

    public static void HandleUnHover(Gesture.OnUnhover evt, SliderSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen)  return;
        
        target.MainBackground.DOKill();
        target.MainBackground.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
        target.isSelected = false;
    }

}
