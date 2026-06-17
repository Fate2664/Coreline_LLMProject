using System;
using System.Collections.Generic;
using DG.Tweening;
using Nova;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class PopupButtonVisuals : ItemVisuals
{
    public UIBlock2D Background = null;
    public TextBlock ButtonLabel = null;
}


[System.Serializable]
public class PopupBoxVisuals : ItemVisuals  
{
    public UIBlock2D Background = null;
    public TextBlock PopupText = null;
    public ListView ButtonList = null;
    
    public float PopinDuration = 0.35f;

    public event Action<int> OnButtonClicked;
    
    private bool EventsRegistered;
    private List<PopupButtonData> currentButtonData;

    public void Bind(PopupData data)
    {
        EnsureEventHandlers();
                
        PopupText.Text = data.message;
        currentButtonData = data.buttons;
        
        ButtonList.SetDataSource(currentButtonData);
    }
    
    public void Show()
    {
        PopupText.Visible = true;
        Background.Visible = true;
        Background.transform.DOScale(Vector3.one, PopinDuration).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        Background.transform.DOScale(Vector3.zero, PopinDuration).SetEase(Ease.InBack).OnComplete(() =>
        {
            PopupText.Visible = false;
            Background.Visible = false;
            ButtonList.SetDataSource<PopupButtonData>(null);
        });
    }

    public void EnsureEventHandlers()
    {
        if (!EventsRegistered)
        {
            EventsRegistered = true;
        }
        else return;
        
        ButtonList.AddGestureHandler<Gesture.OnHover, PopupButtonVisuals>(HandleButtonHover);
        ButtonList.AddGestureHandler<Gesture.OnUnhover, PopupButtonVisuals>(HandleButtonUnHover);
        ButtonList.AddGestureHandler<Gesture.OnPress, PopupButtonVisuals>(HandleButtonPress);
        ButtonList.AddDataBinder<PopupButtonData, PopupButtonVisuals>(BindButtonData);
        
        ButtonList.AddGestureHandler<Gesture.OnClick, PopupButtonVisuals>(HandleButtonClick);
    }

    private void HandleButtonPress(Gesture.OnPress evt, PopupButtonVisuals target, int index)
    {
        AnimateScale(Vector3.one * 0.95f, Ease.OutBack, target.Background);
    }

    private void BindButtonData(Data.OnBind<PopupButtonData> evt, PopupButtonVisuals target, int index)
    {
        target.ButtonLabel.Text = evt.UserData.label;
    }

    internal static void HandleButtonHover(Gesture.OnHover evt, PopupButtonVisuals target, int index)
    {
        AnimateScale(Vector3.one * 1.05f, Ease.OutBack, target.Background);
    }

    internal static void HandleButtonUnHover(Gesture.OnUnhover evt, PopupButtonVisuals target, int index)
    {
        AnimateScale(Vector3.one, Ease.OutQuad, target.Background);
    }

    private void HandleButtonClick(Gesture.OnClick evt, PopupButtonVisuals target, int index)
    {
        OnButtonClicked?.Invoke(index);
    }
    
    private static void AnimateScale(Vector3 targetScale, Ease ease, UIBlock2D background)
    {
        background.DOKill();
        background.transform
            .DOScale(targetScale, .15f)
            .SetEase(ease)
            .SetUpdate(true);
    }
}
