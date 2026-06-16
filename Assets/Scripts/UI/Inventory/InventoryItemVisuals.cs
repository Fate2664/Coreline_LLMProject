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

    private InventoryItem boundItem;
    private InventorySlot boundSlot;
    private Coroutine toolTipCoroutine;
    private bool isHovered;

    public void Bind(InventorySlot slot)
    {
        UnbindLegacyItem();

        boundItem = null;
        boundSlot = slot;

        //Refresh slot
        CancelToolTip();
        if (!HasItem)
        {
            ContentRoot.gameObject.SetActive(false);
            return;
        }

        ContentRoot.gameObject.SetActive(true);
        Image.SetImage(BoundItemData.itemDesc.Icon);
        Count.Text = BoundAmount > 1 ? BoundAmount.ToString() : string.Empty;
        ItemRoot.Color = DefaultColor;
    }

    private void InventoryItem_OnCountDecreased()
    {
        Count.Text = BoundAmount.ToString();
    }

    #region Tooltips

    private void StartToolTipDelay()
    {
        if (!HasItem) return;

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

    #region Gesture Methods

    private InventoryItemData BoundItemData => boundSlot != null ? boundSlot.Item : boundItem?.item;
    private int BoundAmount => boundSlot != null ? boundSlot.Amount : boundItem?.count ?? 0;
    private bool HasItem => BoundItemData != null && BoundAmount > 0;

    private void UnbindLegacyItem()
    {
        if (boundItem != null)
            boundItem.OnCountDecreased -= InventoryItem_OnCountDecreased;
    }

    internal void OnPressVisualOnly()
    {
        ItemRoot.Color = PressedColor;
    }

    internal void OnRelease()
    {
        ItemRoot.Color = HoverColor;
    }

    internal static void HandleHover(Gesture.OnHover evt, InventoryItemVisuals target, int index)
    {
        target.ItemRoot.Color = target.HoverColor;
        target.StartToolTipDelay();
    }

    internal static void HandleHover(Gesture.OnHover evt, InventoryItemVisuals target)
    {
        target.ItemRoot.Color = target.HoverColor;
        target.StartToolTipDelay();
    }

    internal static void HandlePress(Gesture.OnPress evt, InventoryItemVisuals target, int index)
    {
        target.ItemRoot.Color = target.PressedColor;
    }

    internal static void HandlePress(Gesture.OnPress evt, InventoryItemVisuals target)
    {
        target.ItemRoot.Color = target.PressedColor;
    }

    internal static void HandleUnhover(Gesture.OnUnhover evt, InventoryItemVisuals target, int index)
    {
        target.ItemRoot.Color = target.DefaultColor;
        target.CancelToolTip();
    }

    internal static void HandleUnhover(Gesture.OnUnhover evt, InventoryItemVisuals target)
    {
        target.ItemRoot.Color = target.DefaultColor;
        target.CancelToolTip();
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