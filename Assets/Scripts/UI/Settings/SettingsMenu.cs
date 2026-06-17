using System;
using UnityEngine;
using Nova;
using NovaSamples.UIControls;
using System.Collections.Generic;
using DG.Tweening;
using Platformer;

[System.Serializable]
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance;
    public MenuManager MenuManager;
    public UIBlock Root = null;
    public GameInput gameInput = null;
    public PopupManager popup = null;
    public List<SettingsCollection> SettingsCollection = null;
    public ListView TabBar = null;
    public ListView SettingsList = null;

    private int selectedTabIndex = -1;
    private List<Setting> CurrentSettings => SettingsCollection[selectedTabIndex].Settings;
    private List<Setting> currentSortedSettings;
    private int currentIndex;
    private float inputCooldown = 0.15f;
    private float inputTimer;
    private float verticalNav;
    private float horizontalNav;
    private float tabNav;

    private void OnVerticalNav(float dir) => verticalNav = dir;
    private void OnHorizontalNav(float dir) => horizontalNav = dir;
    private void OnTabNav(float dir) => tabNav = dir;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        SettingsManager.Instance.Menu = this;
        
        //Visual
        Root.AddGestureHandler<Gesture.OnHover, StepperSettingVisuals>(StepperSettingVisuals.HandleHover);
        Root.AddGestureHandler<Gesture.OnUnhover, StepperSettingVisuals>(StepperSettingVisuals.HandleUnHover);
        Root.AddGestureHandler<Gesture.OnPress, StepperSettingVisuals>(StepperSettingVisuals.HandlePress);
        Root.AddGestureHandler<Gesture.OnHover, ToggleSettingVisuals>(ToggleSettingVisuals.HandleHover);
        Root.AddGestureHandler<Gesture.OnUnhover, ToggleSettingVisuals>(ToggleSettingVisuals.HandleUnHover);
        Root.AddGestureHandler<Gesture.OnPress, ToggleSettingVisuals>(ToggleSettingVisuals.HandlePress);
        Root.AddGestureHandler<Gesture.OnHover, SliderSettingVisuals>(SliderSettingVisuals.HandleHover);
        Root.AddGestureHandler<Gesture.OnUnhover, SliderSettingVisuals>(SliderSettingVisuals.HandleUnHover);

        //State Changing
        SettingsList.AddGestureHandler<Gesture.OnClick, ToggleSettingVisuals>(HandleToggleClick);
        SettingsList.AddGestureHandler<Gesture.OnDrag, SliderSettingVisuals>(HandleSliderDragged);

        //Data Binding
        SettingsList.AddDataBinder<StepperSetting, StepperSettingVisuals>(BindStepperSetting);
        SettingsList.AddDataUnbinder<StepperSetting, StepperSettingVisuals>(UnbindStepperSetting);
        SettingsList.AddDataBinder<BoolSetting, ToggleSettingVisuals>(BindToggleSetting);
        SettingsList.AddDataBinder<FloatSetting, SliderSettingVisuals>(BindSliderSetting);

        //Tabs
        TabBar.AddDataBinder<SettingsCollection, TabButtonVisuals>(BindTab);
        TabBar.AddGestureHandler<Gesture.OnHover, TabButtonVisuals>(TabButtonVisuals.HandleHover);
        TabBar.AddGestureHandler<Gesture.OnPress, TabButtonVisuals>(TabButtonVisuals.HandlePress);
        TabBar.AddGestureHandler<Gesture.OnUnhover, TabButtonVisuals>(TabButtonVisuals.HandleUnHover);
        TabBar.AddGestureHandler<Gesture.OnClick, TabButtonVisuals>(HandleTabClicked);

        TabBar.SetDataSource(SettingsCollection);

        if (TabBar.TryGetItemView(0, out ItemView firstTab))
        {
            SelectTab(firstTab.Visuals as TabButtonVisuals, 0);
        }

        gameInput.VerticalNav += OnVerticalNav;
        gameInput.HorizontalNav += OnHorizontalNav;
        gameInput.TabNav += OnTabNav;
        gameInput.RestoreDefaults += OnRestoreDefaults;
        gameInput.Apply += OnApply;
        gameInput.Exit += OnExit;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (SettingsManager.Instance != null && SettingsManager.Instance.Menu == this)
            SettingsManager.Instance.Menu = null;
    }

    private void FixedUpdate()
    {
        if (!popup.IsOpen)
        {
            HandleVerticalNavigation();
            HandleHorizontalNavigation();
            HandleTabNavigation();
        }
    }

    private ItemVisuals GetSettingVisuals(int index)
    {
        if (!SettingsList.TryGetItemView(index, out ItemView itemView))
        {
            return null;
        }

        switch (itemView.Visuals)
        {
            case StepperSettingVisuals stepper:
                return stepper;
            case ToggleSettingVisuals toggle:
                return toggle;
            case SliderSettingVisuals slider:
                return slider;
        }

        return null;
    }

    #region Popups

    private void OnApply(bool pressed)
    {
        if (!pressed) return;

        if (popup.IsOpen)
        {
            popup.Confirm();
            return;
        }

        //Show Popup
        PopupData popupData = new PopupData(PopupType.ApplySettings, "Apply Settings?", new List<PopupButtonData>
        {
            new("Confirm", OnConfirmPressed),
            new("Cancel", OnCancelPressed)
        });

        popup.Show(popupData);
    }

    private void OnExit(bool pressed)
    {
        if (!pressed) return;
        
        if (popup.IsOpen)
        {
            popup.Cancel();
            return;
        }
        
        //Check if settings are saved
        //IF they are not then show popup
        //IF they are then return to main menu 
        if (!HasUnsavedSettings())
        {
            MenuManager.HideSettings();
            return;
        }

        PopupData popupData = new PopupData(
            PopupType.ExitSettings,
            "Discard unsaved settings?",
            new List<PopupButtonData>
            {
                new("Confirm", OnConfirmPressed),
                new("Cancel", OnCancelPressed)
            });

        popup.Show(popupData);
    }

    private void OnRestoreDefaults(bool pressed)
    {
        if (!pressed || popup.IsOpen) return;

        if (popup.IsOpen)
        {
            popup.Confirm();
        }

        //Show Popup
        PopupData popupData = new PopupData(PopupType.RestoreDefaults, "Restore Settings to Defaults?",
            new List<PopupButtonData>
            {
                new("Confirm", OnConfirmPressed),
                new("Cancel", OnCancelPressed)
            });

        popup.Show(popupData);
    }

    private void OnConfirmPressed(PopupType popupType)
    {
        switch (popupType)
        {
            case PopupType.ApplySettings:
                SettingsManager.Instance.SaveAllSettings();
                break;
            case PopupType.RestoreDefaults:
                SettingsManager.Instance.ResetAllSettings();
                break;
            case PopupType.ExitSettings:
                SettingsManager.Instance.LoadAllSettings();
                MenuManager.HideSettings();
                break;
        }
    }

    private void OnCancelPressed(PopupType popupType)
    {
    }

    private bool HasUnsavedSettings()
    {
        foreach (SettingsCollection collection in SettingsCollection)
        {
            foreach (Setting setting in collection.Settings)
            {
                if (setting.HasUnsavedChanges)
                    return true;
            }
        }
        return false;
    }
    #endregion

    #region Navigation

    private void HandleTabNavigation()
    {
        float nav = tabNav;
        if (Time.unscaledTime < inputTimer) return;

        if (nav > 0.5f)
        {
            MoveTabSelection(1);
        }
        else if (nav < -0.5f)
        {
            MoveTabSelection(-1);
        }
    }

    private void HandleVerticalNavigation()
    {
        float nav = verticalNav;
        if (Time.unscaledTime < inputTimer) return;

        if (nav > 0.5f)
        {
            MoveVerticalSelection(-1);
        }
        else if (nav < -0.5f)
        {
            MoveVerticalSelection(1);
        }
    }

    private void HandleHorizontalNavigation()
    {
        float nav = horizontalNav;
        if (Time.unscaledTime < inputTimer) return;

        if (nav > 0.5f)
        {
            MoveHorizontalSelection(1);
        }
        else if (nav < -0.5f)
        {
            MoveHorizontalSelection(-1);
        }
    }

    private void MoveTabSelection(int direction)
    {
        if (SettingsCollection == null || SettingsCollection.Count == 0)
            return;

        int tabCount = SettingsCollection.Count;
        int newIndex = selectedTabIndex + direction;

        // Wrap around
        if (newIndex < 0)
        {
            newIndex = tabCount - 1;
        }
        else if (newIndex >= tabCount)
        {
            newIndex = 0;
        }

        if (newIndex == selectedTabIndex)
            return;

        if (TabBar.TryGetItemView(newIndex, out ItemView itemView))
        {
            SelectTab(itemView.Visuals as TabButtonVisuals, newIndex);
        }

        inputTimer = Time.unscaledTime + inputCooldown;
    }

    private void MoveHorizontalSelection(int direction)
    {
        if (currentSortedSettings == null || currentSortedSettings.Count == 0) return;

        ApplySettingInput(currentIndex, direction);
        inputTimer = Time.unscaledTime + inputCooldown;
    }

    private void MoveVerticalSelection(int direction)
    {
        int newIndex = Mathf.Clamp(currentIndex + direction, 0, currentSortedSettings.Count - 1);
        if (newIndex == currentIndex) return;

        currentIndex = newIndex;
        HighlightCurrentSetting();
        SettingsList.JumpToIndex(currentIndex);

        inputTimer = Time.unscaledTime + inputCooldown;
    }

    private void HighlightCurrentSetting()
    {
        for (int i = 0; i < currentSortedSettings.Count; i++)
        {
            if (GetSettingVisuals(i) == null) continue;
            ItemVisuals visuals = GetSettingVisuals(i);
            bool selected = i == currentIndex;

            switch (visuals)
            {
                case StepperSettingVisuals stepper:
                    stepper.isSelected = selected;
                    AnimateHighlight(stepper.Background, selected);
                    break;
                case ToggleSettingVisuals toggle:
                    toggle.isSelected = selected;
                    AnimateHighlight(toggle.Background, selected);
                    break;
                case SliderSettingVisuals slider:
                    slider.isSelected = selected;
                    AnimateHighlight(slider.MainBackground, selected);
                    break;
            }
        }
    }

    private void AnimateHighlight(UIBlock2D background, bool selected)
    {
        background.DOKill();
        if (selected)
        {
            background.transform.DOScale(Vector3.one * 1.05f, 0.15f).SetEase(Ease.OutBack);
        }
        else
        {
            background.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
        }
    }

    #endregion

    #region HandleData

    private void SelectTab(TabButtonVisuals visuals, int index)
    {
        if (index == selectedTabIndex)
        {
            return;
        }

        if (selectedTabIndex >= 0 && TabBar.TryGetItemView(selectedTabIndex, out ItemView currentItemView))
        {
            (currentItemView.Visuals as TabButtonVisuals).isSelected = false;
        }

        selectedTabIndex = index;
        visuals.isSelected = true;
        currentSortedSettings = new List<Setting>(CurrentSettings);
        currentSortedSettings.Sort((a, b) => a.Order.CompareTo(b.Order));

        SettingsList.SetDataSource(currentSortedSettings);
        currentIndex = 0;
        HighlightCurrentSetting();
    }

    private void HandleToggleClick(Gesture.OnClick evt, ToggleSettingVisuals target, int index)
    {
        ApplySettingInput(index, 1);
    }
    
    private void HandleTabClicked(Gesture.OnClick evt, TabButtonVisuals target, int index)
    {
        SelectTab(target, index);
    }

    private void HandleSliderDragged(Gesture.OnDrag evt, SliderSettingVisuals target, int index)
    {
        FloatSetting setting = currentSortedSettings[index] as FloatSetting;
        Vector3 localPointerPos = target.SliderBackground.transform.InverseTransformPoint(evt.PointerPositions.Current);
        float sliderWidth = target.SliderBackground.CalculatedSize.X.Value;

        float startX = target.MinVisualOffset;
        float endX = sliderWidth;
        float currentX = Mathf.Clamp(localPointerPos.x + sliderWidth  * .5f, startX, endX);
        float normalized = Mathf.InverseLerp(startX, endX, currentX);
        float visualPercent = Mathf.Lerp(startX / sliderWidth, 1f , normalized);
        
        setting.Value = Mathf.Lerp(setting.Min, setting.Max, normalized);
        target.FillBar.Size.X.Percent = visualPercent;
        target.ValueLabel.Text = setting.DisplayValue;
    }

    private void CheckUnsavedChanges(bool hasUnsavedChanges, TextBlock settingLabel)
    {
        if (hasUnsavedChanges && settingLabel.Text[settingLabel.Text.Length - 1] != '*')
        {
            settingLabel.Text += '*';
        }
        else if (!hasUnsavedChanges && settingLabel.Text[settingLabel.Text.Length - 1] == '*')
        {
            settingLabel.Text = settingLabel.Text.Remove(settingLabel.Text.Length - 1);
        }
    }

    public void ApplySettingInput(int index, int direction)
    {
        Setting setting = currentSortedSettings[index];
        ItemVisuals visuals = GetSettingVisuals(index);

        switch (setting)
        {
            case StepperSetting stepper:
                stepper.MoveSelection(direction);
                if (visuals is StepperSettingVisuals stepperVisuals)
                    CheckUnsavedChanges(stepper.HasUnsavedChanges, stepperVisuals.SettingLabel);
                break;
            case BoolSetting toggle:
                toggle.IsChecked = !toggle.IsChecked;
                if (visuals is ToggleSettingVisuals toggleVisuals)
                {
                    toggleVisuals.isCheckedVisual = toggle.IsChecked;
                    CheckUnsavedChanges(toggle.HasUnsavedChanges, toggleVisuals.SettingLabel);
                }
                break;
            case FloatSetting slider:
                slider.InputMove(direction);
                if (visuals is SliderSettingVisuals sliderVisuals)
                {
                    sliderVisuals.ValueLabel.Text = slider.DisplayValue;
                    //float normalized = Mathf.InverseLerp(sliderVisuals.MinVisualOffset, sliderVisuals.SliderBackground.CalculatedSize.X.Value, )
                    //float visualPercent = Mathf.Lerp(sliderVisuals.MinVisualOffset / sliderVisuals.SliderBackground.CalculatedSize.X.Value, 1f , normalized);
                    //sliderVisuals.FillBar.Size.X.Percent = visualPercent;
                    CheckUnsavedChanges(slider.HasUnsavedChanges, sliderVisuals.SettingLabel);
                }
                break;
        }
    }

    #endregion

    #region BindData

    private void BindTab(Data.OnBind<SettingsCollection> evt, TabButtonVisuals target, int index)
    {
        target.label.Text = evt.UserData.Category;
    }

    private void BindToggleSetting(Data.OnBind<BoolSetting> evt, ToggleSettingVisuals target, int index)
    {
        target.SettingLabel.Text = evt.UserData.Name;
        target.isCheckedVisual = evt.UserData.IsChecked;
    }

    private void BindStepperSetting(Data.OnBind<StepperSetting> evt, StepperSettingVisuals target, int index)
    {
        StepperSetting setting = evt.UserData;
        target.SettingLabel.Text = evt.UserData.Name;
        target.Initialize(evt.UserData, index);
    }

    private void UnbindStepperSetting(Data.OnUnbind<StepperSetting> evt, StepperSettingVisuals target, int index)
    {
        target.Unbind(evt.UserData);
    }

    private void BindSliderSetting(Data.OnBind<FloatSetting> evt, SliderSettingVisuals visuals, int index)
    {
        FloatSetting setting = evt.UserData;
        visuals.SettingLabel.Text = setting.Name;
        visuals.ValueLabel.Text = setting.DisplayValue;
        
        float normalized = Mathf.InverseLerp(setting.Min, setting.Max, setting.Value);
        visuals.FillBar.Size.X.Percent = normalized;
    }

    #endregion

    private void OnDisable()
    {
        gameInput.VerticalNav -= OnVerticalNav;
        gameInput.HorizontalNav -= OnHorizontalNav;
        gameInput.TabNav -= OnTabNav;
        gameInput.RestoreDefaults -= OnRestoreDefaults;
        gameInput.Apply -= OnApply;
        gameInput.Exit -= OnExit;
    }
}
