using Nova;
using System;
using System.Collections;
using System.Collections.Generic;
using Coreline;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

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
    private Coroutine toolTipCoroutine;
    private bool isHovered = false;

    public void Bind(InventoryItem data, UIManager panel)
    {
        if (boundItem != null)
        {
            boundItem.OnCountDecreased -= InventoryItem_OnCountDecreased;
        }

        boundItem = data;
        _uiManager = panel;
        boundItem.OnCountDecreased += InventoryItem_OnCountDecreased;

        if (ItemRoot != null)
        {
            ItemRoot.Color = DefaultColor;
        }

        if (data.isEmpty)
        {
            if (ContentRoot != null) ContentRoot.gameObject.SetActive(false);
            if (ToolTipRoot != null) ToolTipRoot.gameObject.SetActive(false);
        }
        else
        {
            if (ContentRoot != null) ContentRoot.gameObject.SetActive(true);
            if (ToolTipRoot != null) ToolTipRoot.gameObject.SetActive(false);
            Image.SetImage(data.item.itemDesc.Icon);
            RefreshCount();
            if (ToolTipText != null) ToolTipText.Text = data.item.itemDesc.ToolTip;
        }
    }

    private void InventoryItem_OnCountDecreased()
    {
        RefreshCount();
    }

    private void RefreshCount()
    {
        if (Count != null && boundItem != null)
        {
            Count.Text = boundItem.count.ToString();
        }
    }

    #region ToolTips

    private void StartToolTipDelay()
    {
        if (ToolTipRoot == null) return;
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
        if (!boundItem.isEmpty)
        {
            _uiManager.EquipItem(boundItem);
        }
    }

    #region Gesture Methods

    internal void OnHover()
    {
        ItemRoot.Color = HoverColor;
        StartToolTipDelay();
    }

    internal void OnPress()
    {
        ItemRoot.Color = PressedColor;
        EquipItem();
    }

    internal void OnUnhover()
    {
        ItemRoot.Color = DefaultColor;
        CancelToolTip();
    }

    internal void OnRelease()
    {
        ItemRoot.Color = HoverColor;
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