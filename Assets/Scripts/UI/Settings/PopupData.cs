using System;
using System.Collections.Generic;
using DG.Tweening;
using Nova;
using UnityEngine;
using UnityEngine.Events;

public enum PopupType
{
    RestoreDefaults,
    ApplySettings,
    ExitSettings,
}


[System.Serializable]
public class PopupData
{
    public PopupType type;
    public string message;
    public List<PopupButtonData> buttons;
    
    public PopupData(PopupType type, string message, List<PopupButtonData> buttons)
    {
        this.type = type;
        this.message = message;
        this.buttons =  buttons;
    }
}

[System.Serializable]
public class PopupButtonData
{
    public string label;
    public UnityAction<PopupType> Callback;
    
    public PopupButtonData(string label, UnityAction<PopupType> callback)
    {
        this.label = label;
        this.Callback = callback;
    }
}

