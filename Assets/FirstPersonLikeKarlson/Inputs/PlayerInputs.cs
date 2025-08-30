// from StarterAssetsInputs from Starter Assets package

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInputs : MonoBehaviour
{
    [Header("Character Input Values")] public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool sprint;
    public bool shoot;
    public bool autoShoot;

    public bool pause;
    // Crouch関連の変数を追加・整理
    public bool crouch; // Hold: 押している間 true
    public bool crouchPressed; // Press: 押されたフレームでのみ true
    public bool crouchReleased; // Release: 離されたフレームでのみ true

    [Header("Movement Settings")] public bool analogMovement;

    [Header("Mouse Cursor Settings")] public bool cursorLocked = true;
    public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        if (cursorInputForLook)
        {
            LookInput(value.Get<Vector2>());
        }
    }

    public void OnJump(InputValue value)
    {
        JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }

    public void OnShoot(InputValue value)
    {
        shoot = value.isPressed;
    }

    public void OnAutoShoot(InputValue value)
    {
        autoShoot = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        crouch = value.isPressed;
        // Crouch は Press と Release の両方を管理するために、以下のロジックを追加
        if (crouch)
        {
            crouchPressed = true;
        }
        else
        {
            crouchReleased = true;
        }
    }

    public void OnPause(InputValue value)
    {
        pause = value.isPressed;
    }

#endif
    
    // LateUpdateを追加してPress/Releaseフラグをリセット
    private void LateUpdate()
    {
        // フレームの終わりにリセットすることで、1フレームだけtrueになるようにする
        crouchPressed = false;
        crouchReleased = false;
    }

    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    }

    public void LookInput(Vector2 newLookDirection)
    {
        look = newLookDirection;
    }

    public void JumpInput(bool newJumpState)
    {
        jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        sprint = newSprintState;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetCursorState(cursorLocked);
    }

    private void SetCursorState(bool newState)
    {
        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}