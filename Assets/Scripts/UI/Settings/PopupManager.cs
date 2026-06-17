using UnityEngine;

public class PopupManager : MonoBehaviour
{
    [SerializeField] private PopupBoxVisuals popup;

    public bool IsOpen { get; private set; }
    
    private PopupData currentData;

    public void Show(PopupData data)
    {
        currentData = data;
        IsOpen = true;
        
        popup.Bind(data);
        popup.OnButtonClicked += HandleButtonClicked;
        
        popup.Show();
    }

    private void HandleButtonClicked(int index)
    {
        currentData.buttons[index].Callback?.Invoke(currentData.type);
        Hide();
    }

    public void Hide()
    {
        popup.OnButtonClicked -= HandleButtonClicked;
        popup.Hide();
        IsOpen = false;
    }

    public void Confirm()
    {
        if (!IsOpen || currentData == null || currentData.buttons == null) return;
        HandleButtonClicked(0);
    }

    public void Cancel()
    {
        if (!IsOpen || currentData == null || currentData.buttons == null) return;
        HandleButtonClicked(1);
    }
}
