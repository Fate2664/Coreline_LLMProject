using Nova;
using System.Collections;
using Coreline;
using UnityEngine;

[System.Serializable]
public class InventoryItemVisuals : ItemVisuals
{
    public UIBlock2D ItemRoot;
    public UIBlock ContentRoot;
    public UIBlock2D Image;
    public TextBlock Count;
    public UIBlock ToolTipRoot;
    public TextBlock ToolTipText;

    [Header("Animations")] public float ToolTipDelay = 0.5f;
    public Color DefaultColor;
    public Color HoverColor;
    public Color PressedColor;

    private UIManager _uiManager;
    private InventoryItem boundItem;
    private InventorySlot boundSlot;
    private Coroutine toolTipCoroutine;
    private bool isHovered;

    public void Bind(InventoryItem data, UIManager panel)
    {
        UnbindLegacyItem();

        boundItem = data;
        boundSlot = null;
        _uiManager = panel;

        if (boundItem != null)
        {
            boundItem.OnCountDecreased += InventoryItem_OnCountDecreased;
        }

        RefreshVisuals();
    }

    public void Bind(InventorySlot slot)
    {
        UnbindLegacyItem();

        boundItem = null;
        boundSlot = slot;
        _uiManager = null;

        RefreshVisuals();
    }

    private void InventoryItem_OnCountDecreased()
    {
        RefreshCount();
    }

    private void RefreshCount()
    {
        if (Count != null)
        {
            Count.Text = BoundAmount.ToString();
        }
    }

    #region ToolTips

    private void StartToolTipDelay()
    {
        if (ToolTipRoot == null || !HasItem)
        {
            return;
        }

        CancelToolTip();
        isHovered = true;
        toolTipCoroutine = View.StartCoroutine(ShowToolTipAfterDelay());
    }

    private IEnumerator ShowToolTipAfterDelay()
    {
        yield return new WaitForSeconds(ToolTipDelay);

        if (isHovered)
        {
            ToolTipRoot.gameObject.SetActive(true);
        }

        toolTipCoroutine = null;
    }

    private void CancelToolTip()
    {
        if (ToolTipRoot == null) return;
        isHovered = false;

        if (toolTipCoroutine != null)
        {
            View.StopCoroutine(toolTipCoroutine);
            toolTipCoroutine = null;
        }

        ToolTipRoot.gameObject.SetActive(false);
    }

    #endregion

    public void EquipItem()
    {
        if (boundItem != null && !boundItem.isEmpty && _uiManager != null)
        {
            _uiManager.EquipItem(boundItem);
        }
    }

    #region Gesture Methods

    internal void OnHover()
    {
        if (ItemRoot != null)
        {
            ItemRoot.Color = HoverColor;
        }

        StartToolTipDelay();
    }

    internal void OnPress()
    {
        OnPressVisualOnly();
        EquipItem();
    }

    internal void OnPressVisualOnly()
    {
        if (ItemRoot != null)
        {
            ItemRoot.Color = PressedColor;
        }
    }

    internal void OnUnhover()
    {
        if (ItemRoot != null)
        {
            ItemRoot.Color = DefaultColor;
        }

        CancelToolTip();
    }

    internal void OnRelease()
    {
        if (ItemRoot != null)
        {
            ItemRoot.Color = HoverColor;
        }
    }

    internal void OnCancel()
    {
        OnUnhover();
    }

    private InventoryItemData BoundItemData =>
        boundSlot != null ? boundSlot.Item : boundItem?.item;

    private int BoundAmount =>
        boundSlot != null ? boundSlot.Amount : boundItem?.count ?? 0;

    private bool HasItem => BoundItemData != null && BoundAmount > 0;

    private void RefreshVisuals()
    {
        CancelToolTip();

        if (ItemRoot != null)
        {
            ItemRoot.Color = DefaultColor;
        }

        if (!HasItem)
        {
            if (ContentRoot != null)
            {
                ContentRoot.gameObject.SetActive(false);
            }

            return;
        }

        InventoryItemData item = BoundItemData;
        ItemDescription description = item.itemDesc;

        if (ContentRoot != null)
        {
            ContentRoot.gameObject.SetActive(true);
        }

        if (Image != null)
        {
            Image.SetImage(description?.Icon);
        }

        if (ToolTipText != null)
        {
            ToolTipText.Text = description?.ToolTip ?? string.Empty;
        }

        RefreshCount();
    }

    private void UnbindLegacyItem()
    {
        if (boundItem != null)
        {
            boundItem.OnCountDecreased -= InventoryItem_OnCountDecreased;
        }
    }

    internal static void HandleHover(Gesture.OnHover evt, InventoryItemVisuals target, int index)
    {
        target.OnHover();
    }

    internal static void HandleHover(Gesture.OnHover evt, InventoryItemVisuals target)
    {
        target.OnHover();
    }

    internal static void HandlePress(Gesture.OnPress evt, InventoryItemVisuals target, int index)
    {
        target.OnPress();
    }

    internal static void HandlePress(Gesture.OnPress evt, InventoryItemVisuals target)
    {
        target.OnPress();
    }

    internal static void HandleUnhover(Gesture.OnUnhover evt, InventoryItemVisuals target, int index)
    {
        target.OnUnhover();
    }

    internal static void HandleUnhover(Gesture.OnUnhover evt, InventoryItemVisuals target)
    {
        target.OnUnhover();
    }

    internal static void HandleRelease(Gesture.OnRelease evt, InventoryItemVisuals target, int index)
    {
        target.OnRelease();
    }

    internal static void HandleRelease(Gesture.OnRelease evt, InventoryItemVisuals target)
    {
        target.OnRelease();
    }

    #endregion
}
