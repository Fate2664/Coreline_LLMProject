using System;
using Coreline;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Input/Game Input")]
public class GameInput : ScriptableObject, PlayerInputActions.IPlayerActions, PlayerInputActions.IUIActions 
{
    //Player Actions
    public event UnityAction<Vector2> Move =  delegate { };
    public event UnityAction<Vector2> Look =  delegate { };
    public event UnityAction<bool> PrimaryAttack  =  delegate { };
    public event UnityAction<bool> Interact  =  delegate { };
    public event UnityAction<bool> AltInteract =  delegate { };
    public event UnityAction<bool> Jump = delegate { }; 
    public event UnityAction<bool> ToggleInventory = delegate { };
    public event UnityAction<bool> Sprint = delegate { };
    public event UnityAction<bool> CrouchSlide = delegate { }; 
    
    //UI Actions
    public event UnityAction<bool> Exit  =  delegate { };
    public event UnityAction<bool> RestoreDefaults  =  delegate { };
    public event UnityAction<bool> Apply  =  delegate { };
    public event UnityAction<float> VerticalNav  =  delegate { };
    public event UnityAction<float> HorizontalNav  =  delegate { };
    public event UnityAction<float> TabNav = delegate { };


    private PlayerInputActions inputActions;
    
    
    public Vector2 Direction => inputActions.Player.Move.ReadValue<Vector2>();
    public bool IsPrimaryAttackPressed => inputActions.Player.PrimaryAttack.IsPressed();
    public bool IsInteractPressed => inputActions.Player.Interact.IsPressed();
    public bool IsAltInteractPressed => inputActions.Player.AltInteract.IsPressed();
    public bool IsSprintPressed => inputActions.Player.Sprint.IsPressed();
    
    
    
    public void EnableActions()
    {
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();
            inputActions.Player.SetCallbacks(this);
            inputActions.UI.SetCallbacks(this);
        }    
        
        inputActions.Player.Enable();
        inputActions.UI.Enable();
    }

    #region Gameplay Actions

    public void OnMove(InputAction.CallbackContext context)
    {
        Move.Invoke(context.ReadValue<Vector2>());
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        Interact.Invoke(context.ReadValueAsButton());
    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        PrimaryAttack.Invoke(context.ReadValueAsButton());
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        Look.Invoke(context.ReadValue<Vector2>());        
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        Jump.Invoke(context.ReadValueAsButton());
    }

    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        ToggleInventory.Invoke(context.ReadValueAsButton());
    }

    public void OnAltInteract(InputAction.CallbackContext context)
    {
        AltInteract.Invoke(context.ReadValueAsButton());
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        Sprint.Invoke(context.ReadValueAsButton());
    }

    public void OnCrouchSlide(InputAction.CallbackContext context)
    {
        CrouchSlide.Invoke(context.ReadValueAsButton());
    }

    #endregion

    #region UI Actions

    void PlayerInputActions.IUIActions.OnExit(InputAction.CallbackContext context)
    {
        Exit.Invoke(context.phase == InputActionPhase.Performed);
    }

    public void OnVerticalNavigation(InputAction.CallbackContext context)
    {
        VerticalNav.Invoke(context.ReadValue<float>());
    }

    public void OnHorizontalNavigation(InputAction.CallbackContext context)
    {
        HorizontalNav.Invoke(context.ReadValue<float>());
    }

    void PlayerInputActions.IUIActions.OnRestoreDefaults(InputAction.CallbackContext context)
    {
        RestoreDefaults.Invoke(context.phase == InputActionPhase.Performed);
    }

    void PlayerInputActions.IUIActions.OnApply(InputAction.CallbackContext context)
    {
        Apply.Invoke(context.phase == InputActionPhase.Performed);
    }

    public void OnTabNavigation(InputAction.CallbackContext context)
    {
        TabNav.Invoke(context.ReadValue<float>());
    }

    #endregion
}
