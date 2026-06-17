using DG.Tweening;
using Nova;
using Platformer;
using UnityEngine;
using UnityEngine.PlayerLoop;

[System.Serializable]
public class StepperSettingVisuals : ItemVisuals
{
    public UIBlock2D Background = null;
    public TextBlock SettingLabel = null;
    public TextBlock ValueLabel = null;
    public UIBlock2D LeftArrow = null;
    public UIBlock2D RightArrow = null;
    public Texture2D WhiteArrow = null;
    public Texture2D BlackArrow = null;
    public static float HoverScale = 1.05f;

    public bool isSelected
    {
        get => Background.BodyEnabled;
        set
        {
            Background.BodyEnabled = value;
            SettingLabel.Color = value ? Color.black : Color.white;
            ValueLabel.Color = value ? Color.black : Color.white;
            LeftArrow.SetImage(value ? BlackArrow : WhiteArrow);
            RightArrow.SetImage(value ? BlackArrow : WhiteArrow);
        }
    }

    private StepperSetting DataSource;
    private bool EventHandlersRegistered = false;
    private int boundIndex = -1;

    public void Initialize(StepperSetting dataSource, int index)
    {
        if (DataSource != null)
        {
            DataSource.OnIndexChanged -= HandleIndexChanged;
        }

        DataSource = dataSource;
        boundIndex = index;
        
        if (DataSource != null)
        {
            DataSource.OnIndexChanged += HandleIndexChanged;
        }

        //When OnIndexChanged also call this code -> UpdateValue()
        //We don't care about the paramenter
        //ataSource.OnIndexChanged += _ => UpdateValue();

        if (!EventHandlersRegistered)
        {
            LeftArrow.AddGestureHandler<Gesture.OnClick>(HandleLeftArrowClicked);
            RightArrow.AddGestureHandler<Gesture.OnClick>(HandleRightArrowClicked);
            EventHandlersRegistered = true;
        }
        
        UpdateValue();
    }

    public void Unbind(StepperSetting dataSource)
    {
        if (DataSource != dataSource)
        {
            return;
        }

        DataSource.OnIndexChanged -= HandleIndexChanged;
        DataSource = null;
        boundIndex = -1;
    }

    private void HandleIndexChanged(Setting setting)
    {
        if (ValueLabel == null)
        {
            if (DataSource != null)
            {
                DataSource.OnIndexChanged -= HandleIndexChanged;
                DataSource = null;
            }

            return;
        }

        UpdateValue();
    }

    #region HandleData

    internal static void HandleHover(Gesture.OnHover evt, StepperSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen) return;
        
        AudioManager.Instance.Play("HoverSound");
        target.Background.DOKill();
        target.Background.transform.DOScale(target.SettingLabel.transform.localScale * HoverScale, 0.15f)
            .SetEase(Ease.OutBack);
        target.isSelected = true;
    }

    internal static void HandleUnHover(Gesture.OnUnhover evt, StepperSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen) return;

        target.Background.DOKill();
        target.Background.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
        target.isSelected = false;
    }

    internal static void HandlePress(Gesture.OnPress evt, StepperSettingVisuals target)
    {
        if (SettingsMenu.Instance.popup.IsOpen) return;

        //Play SFX
        AudioManager.Instance.Play("ClickSound");
    }

    private void UpdateValue()
    {
        if (ValueLabel == null || DataSource == null ||
            DataSource.Options == null || DataSource.Options.Length == 0)
        {
            return;
        }

        ValueLabel.Text = DataSource.Options[DataSource.SelectedIndex];
    }
    
    private void HandleLeftArrowClicked(Gesture.OnClick evt)
    {
        SettingsMenu.Instance.ApplySettingInput(boundIndex, -1);
    }

    private void HandleRightArrowClicked(Gesture.OnClick evt)
    {
        SettingsMenu.Instance.ApplySettingInput(boundIndex, 1);
    }

    #endregion

    
}
