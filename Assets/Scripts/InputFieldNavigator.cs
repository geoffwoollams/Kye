using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A focused TMP input field swallows direction input, so the player can get stuck in it.
/// Call <see cref="Tick"/> from LateUpdate: on a press of one of the allowed directions
/// (keyboard arrows, gamepad d-pad or left stick) it selects the field's own navigation
/// target for that direction.
///
/// The UI module can handle the same press one frame later and move on from the field we
/// just selected, so the frame after a forced move the selection is put back.
/// Skips frames where the UI module already changed the selection, so a press never moves twice.
/// </summary>
public class InputFieldNavigator
{
    public enum Dir { None, Up, Down, Left, Right }

    private GameObject _prevSelected, _handledTarget;
    private int _handledFrame = -10;
    private bool _stickUp, _stickDown, _stickLeft, _stickRight;

    /// <param name="fields">The fields to handle.</param>
    /// <param name="allowed">The directions that force a move.</param>
    public void Tick(TMP_InputField[] fields, params Dir[] allowed)
    {
        var eventSystem = EventSystem.current;
        var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        Dir dir = ReadPress();
        if (System.Array.IndexOf(allowed, dir) < 0)
            dir = Dir.None;

        // Undo a late move by the UI module (there is no new press on that frame)
        if (_handledTarget != null && Time.frameCount == _handledFrame + 1 &&
            current != _handledTarget && dir == Dir.None)
        {
            eventSystem.SetSelectedGameObject(_handledTarget);
            current = _handledTarget;
        }

        if (dir != Dir.None && current != null && current == _prevSelected)
        {
            var field = current.GetComponent<TMP_InputField>();
            if (field != null && field.isFocused && System.Array.IndexOf(fields, field) >= 0)
            {
                Selectable target = null;
                switch (dir)
                {
                    case Dir.Up: target = field.FindSelectableOnUp(); break;
                    case Dir.Down: target = field.FindSelectableOnDown(); break;
                    case Dir.Left: target = field.FindSelectableOnLeft(); break;
                    case Dir.Right: target = field.FindSelectableOnRight(); break;
                }

                if (target != null && target.gameObject.activeInHierarchy)
                {
                    eventSystem.SetSelectedGameObject(target.gameObject);
                    _handledTarget = target.gameObject;
                    _handledFrame = Time.frameCount;
                }
            }
        }

        _prevSelected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
    }

    // The direction pressed this frame (the stick counts once per push), or None
    private Dir ReadPress()
    {
        Dir dir = Dir.None;
        var kb = Keyboard.current;
        var pad = Gamepad.current;

        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame) dir = Dir.Up;
            else if (kb.downArrowKey.wasPressedThisFrame) dir = Dir.Down;
            else if (kb.leftArrowKey.wasPressedThisFrame) dir = Dir.Left;
            else if (kb.rightArrowKey.wasPressedThisFrame) dir = Dir.Right;
        }

        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            bool up = stick.y > 0.5f, down = stick.y < -0.5f, left = stick.x < -0.5f, right = stick.x > 0.5f;

            if (dir == Dir.None)
            {
                if (pad.dpad.up.wasPressedThisFrame || (up && !_stickUp)) dir = Dir.Up;
                else if (pad.dpad.down.wasPressedThisFrame || (down && !_stickDown)) dir = Dir.Down;
                else if (pad.dpad.left.wasPressedThisFrame || (left && !_stickLeft)) dir = Dir.Left;
                else if (pad.dpad.right.wasPressedThisFrame || (right && !_stickRight)) dir = Dir.Right;
            }

            _stickUp = up; _stickDown = down; _stickLeft = left; _stickRight = right;
        }

        return dir;
    }
}
