using System;
using DG.Tweening;
using Nova;
using Platformer;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class ToggleSettingVisuals : ItemVisuals
{
    public UIBlock2D Background = null;
    public TextBlock SettingLabel = null;
    public UIBlock2D CheckBox = null;
    public UIBlock2D CheckMark = null;
    public Texture2D WhiteCheckMark = null;
    public Texture2D BlackCheckMark = null;
    public static float HoverScale = 1.05f;

    public bool isSelected
    {
        get => Background.BodyEnabled;
        set
        {
            Background.BodyEnabled = value;
            SettingLabel.Color = value ? Color.black : Color.white;
            CheckBox.Color = value ? Color.white : Color.black;
            CheckBox.Border.Color = value ? Color.black : Color.white;
            CheckBox.Border.Width = value ? 6f : 4f;
            CheckMark.SetImage(value ? BlackCheckMark : WhiteCheckMark);
        }
    }

    public bool isCheckedVisual
    {
        get => CheckMark.BodyEnabled;
        set => CheckMark.BodyEnabled = value;
    }

    public static void HandleHover(Gesture.OnHover evt, ToggleSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen) return;
        
        AudioManager.Instance.Play("HoverSound");
        target.Background.DOKill();
        target.Background.transform.DOScale(target.SettingLabel.transform.localScale * HoverScale, 0.15f).SetEase(Ease.OutBack);
        target.isSelected = true;
    }

    public static void HandleUnHover(Gesture.OnUnhover evt, ToggleSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen)  return;
        
        target.Background.DOKill();
        target.Background.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
        target.isSelected = false;
    }

    public static void HandlePress(Gesture.OnPress evt, ToggleSettingVisuals target)
    {
        if  (SettingsMenu.Instance.popup.IsOpen) return;
        
        //Play SFX
        AudioManager.Instance.Play("ClickSound");
    }

}
