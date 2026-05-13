using Nova;
using System;
using UnityEngine;

[System.Serializable]
public class DropDownItemVisuals : ItemVisuals
{
    public TextBlock label = null;
    public UIBlock2D Background = null;
    public UIBlock2D SelectedIndicator = null;

}


[System.Serializable]
public class DropDownVisuals : ItemVisuals
{
    public TextBlock label = null;
    public TextBlock SelectedLabel = null;
    public UIBlock2D Background = null;
    public UIBlock ExpandedRoot = null;
    public ListView OptionsList = null;

    public Color DefaultColor;
    public Color HoveredColor;
    public Color PressedColor;

    public Color PrimaryRowColor;
    public Color SecondaryRowColor;

    private MultiOptionSetting dataSource = null;
    private bool eventHandlersRegistered = false;
    public bool isExpanded => ExpandedRoot != null && ExpandedRoot.gameObject.activeSelf;
    public MultiOptionSetting DataSource => dataSource;
    public event Action<int, string> OnSelectionChanged;

    internal static void HandleHover(Gesture.OnHover evt, DropDownVisuals target)
    {
        if (target.ExpandedRoot != null && evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
        {
            return;
        }
        target.Background.Color = target.HoveredColor;
    }

    internal static void HandlePress(Gesture.OnPress evt, DropDownVisuals target)
    {
        if (target.ExpandedRoot != null && evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
        {
            return;
        }
        target.Background.Color = target.PressedColor;
    }

    internal static void HandleRelease(Gesture.OnRelease evt, DropDownVisuals target)
    {
        if (target.ExpandedRoot != null && evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
        {
            return;
        }
        target.Background.Color = target.HoveredColor;
    }

    internal static void HandleUnhover(Gesture.OnUnhover evt, DropDownVisuals target)
    {
        if (target.ExpandedRoot != null && evt.Receiver.transform.IsChildOf(target.ExpandedRoot.transform))
        {
            return;
        }
        target.Background.Color = target.DefaultColor;
    }

    public void Collapse()
    {
        if (ExpandedRoot != null)
        {
            ExpandedRoot.gameObject.SetActive(false);
        }
    }

    public void Refresh(MultiOptionSetting dataSource)
    {
        this.dataSource = dataSource;

        EnsureEventHandlers();
        RefreshSelectedLabel();

        if (OptionsList != null && dataSource != null)
        {
            OptionsList.SetDataSource(dataSource.Options);
            if (OptionsList.gameObject.activeInHierarchy)
            {
                OptionsList.Refresh();
            }
        }
    }

    public void Expand(MultiOptionSetting dataSource)
    {
        this.dataSource = dataSource;
        EnsureEventHandlers();
        RefreshSelectedLabel();

        if (ExpandedRoot != null)
        {
            ExpandedRoot.gameObject.SetActive(true);
        }

        if (OptionsList != null && dataSource != null)
        {
            OptionsList.SetDataSource(dataSource.Options);
            OptionsList.Refresh();

            if (dataSource.Options.Length > 0)
            {
                OptionsList.JumpToIndex(Mathf.Clamp(dataSource.SelectedIndex, 0, dataSource.Options.Length - 1));
            }
        }
    }

    // This method is called when the visuals are initialized.
    private void EnsureEventHandlers()
    {
        if (eventHandlersRegistered || OptionsList == null)
        {
            return;
        }
        eventHandlersRegistered = true;

        OptionsList.AddGestureHandler<Gesture.OnHover, DropDownItemVisuals>(HandleItemHovered);
        OptionsList.AddGestureHandler<Gesture.OnUnhover, DropDownItemVisuals>(HandleItemUnHovered);
        OptionsList.AddGestureHandler<Gesture.OnPress, DropDownItemVisuals>(HandleItemPressed);
        OptionsList.AddGestureHandler<Gesture.OnRelease, DropDownItemVisuals>(HandleItemReleased);
        OptionsList.AddGestureHandler<Gesture.OnClick, DropDownItemVisuals>(HandleItemClicked);

        OptionsList.AddDataBinder<string, DropDownItemVisuals>(BindItem);
    }

    private void BindItem(Data.OnBind<string> evt, DropDownItemVisuals target, int index)
    {
        target.label.Text = evt.UserData;
        target.SelectedIndicator.gameObject.SetActive(dataSource != null && index == dataSource.SelectedIndex);
        target.Background.Color = index % 2 == 0 ? PrimaryRowColor : SecondaryRowColor;
    }

    private void HandleItemClicked(Gesture.OnClick evt, DropDownItemVisuals target, int index)
    {
        if (dataSource == null)
        {
            return;
        }

        dataSource.SelectedIndex = index;
        RefreshSelectedLabel();
        OnSelectionChanged?.Invoke(dataSource.SelectedIndex, dataSource.CurrentSelection);
        evt.Consume();
        Collapse();
    }

    private void RefreshSelectedLabel()
    {
        if (SelectedLabel != null)
        {
            SelectedLabel.Text = dataSource != null ? dataSource.CurrentSelection : string.Empty;
        }
    }

    private void HandleItemReleased(Gesture.OnRelease evt, DropDownItemVisuals target, int index)
    {
        target.Background.Color = HoveredColor;
    }

    private void HandleItemPressed(Gesture.OnPress evt, DropDownItemVisuals target, int index)
    {
        target.Background.Color = PressedColor;
    }

    private void HandleItemUnHovered(Gesture.OnUnhover evt, DropDownItemVisuals target, int index)
    {
        target.Background.Color = index % 2 == 0 ? PrimaryRowColor : SecondaryRowColor;
    }

    private void HandleItemHovered(Gesture.OnHover evt, DropDownItemVisuals target, int index)
    {
        target.Background.Color = HoveredColor;
    }
}


