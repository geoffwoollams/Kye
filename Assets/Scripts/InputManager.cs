using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Runs before other scripts' Update, so GameController always reads THIS frame's input
// instead of last frame's (or a half-reset mix, depending on Unity's arbitrary order).
[DefaultExecutionOrder(-100)]
public class InputManager : MonoBehaviour
{
    public bool IncludeWSAD = true;
    public bool IncludeKeyboardArrows = true;
    public bool IncludeNumeric = true;
    // Kye can't move diagonally, so numpad 7/9/1/3 would just act as left/right.
    // Leave this off. (If the scene already has it ticked, untick it in the Inspector:
    // the serialized value overrides this default.)
    public bool IncludeNumericDiagonal = false;
    public bool IncludeJoystick = true;
    public bool IncludeDpad = true;
    public bool IncludeLeftStick = true;
    public bool IncludeRightStick = true;
    public bool IncludeSwipes = true;
    public float MinSwipeDistance = 0.1f;
    public float MaxSwipeTime = 1f;

    public bool HasControllerInput;
    public bool HasMouseInput;
    public bool HasKeyboardInput;
    public bool HasTouchInput;

    [HideInInspector]
    public float x, y;

    // NOTE: AnyKeyChange is true for presses AND releases. Don't use it to decide
    // whether a new step is allowed (that was the cause of the double-step bug).
    [HideInInspector]
    public bool AnyKey, AnyKeyUp, AnyKeyDown, AnyKeyChange;

    [HideInInspector]
    public bool SwipedUp, SwipedDown, SwipedLeft, SwipedRight;

    // Renamed from "Key": it clashed with UnityEngine.InputSystem.Key.
    public class DirectionKey
    {
        public bool isPressed;
        public bool isDown;
        public bool isUp;

        // Stamp of when this key was last pressed (higher = more recent).
        // Deliberately NOT cleared by Reset(): it must survive across frames.
        public int pressOrder;

        public void Reset()
        {
            isPressed = isDown = isUp = false;
        }

        public void SetIsDownIfTrue(bool value)
        {
            isDown = isDown || value;
            isPressed = isPressed || value;
        }
    }

    // Created up-front (not in Start) so nothing can hit a null before Start runs.
    public readonly DirectionKey Up = new DirectionKey();
    public readonly DirectionKey Down = new DirectionKey();
    public readonly DirectionKey Left = new DirectionKey();
    public readonly DirectionKey Right = new DirectionKey();

    private Vector2 _swipeStartPosition;
    private float _swipeStartTime;
    private bool _canEnd;
    private int _pressCounter;

    void Update()
    {
        Reset();
        GetMouseInput();
        GetTouchInput();
        GetButtonInput();
        ProcessInput();

        if (AnyKeyDown)
            _canEnd = false;
    }

    public bool HasMouseMoved()
    {
        if (Mouse.current == null) return false;
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        return mouseDelta.sqrMagnitude > 1.0f;
    }

    private void Reset()
    {
        Up.Reset();
        Down.Reset();
        Left.Reset();
        Right.Reset();
        AnyKey = AnyKeyUp = AnyKeyDown = AnyKeyChange = false;
        SwipedUp = SwipedDown = SwipedLeft = SwipedRight = false;
        x = 0;
        y = 0;

        HasControllerInput = false;
        HasMouseInput = false;
        HasKeyboardInput = false;
        HasTouchInput = false;
    }

    // HasMouseInput was never set before.
    private void GetMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (HasMouseMoved() || mouse.leftButton.isPressed || mouse.rightButton.isPressed)
            HasMouseInput = true;
    }

    private void GetTouchInput()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null)
            return;

        // The old check (touches.Count > 0) was always true: Count is the number of
        // touch SLOTS (10), not the number of fingers down.
        TouchControl touch = screen.primaryTouch;
        bool began = touch.press.wasPressedThisFrame;
        bool ended = touch.press.wasReleasedThisFrame;

        if (touch.press.isPressed || ended)
            HasTouchInput = true;

        // Both axes are divided by screen WIDTH on purpose, so a swipe's length
        // means the same thing on any aspect ratio.
        Vector2 pos = touch.position.ReadValue() / Screen.width;

        if (began)
        {
            _swipeStartPosition = pos;
            _swipeStartTime = Time.unscaledTime;   // unscaled: still works while timeScale = 0
            _canEnd = true;
            return;
        }

        if (!ended || !_canEnd)
            return;

        _canEnd = false;

        if (Time.unscaledTime - _swipeStartTime > MaxSwipeTime)
            return;

        Vector2 swipe = pos - _swipeStartPosition;
        if (swipe.magnitude < MinSwipeDistance)
            return;

        if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
        {
            if (swipe.x > 0) SwipedRight = true;
            else SwipedLeft = true;
        }
        else
        {
            if (swipe.y > 0) SwipedUp = true;
            else SwipedDown = true;
        }

        if (IncludeSwipes)
        {
            Up.SetIsDownIfTrue(SwipedUp);
            Down.SetIsDownIfTrue(SwipedDown);
            Left.SetIsDownIfTrue(SwipedLeft);
            Right.SetIsDownIfTrue(SwipedRight);
        }
    }

    // "ref" was unnecessary (DirectionKey is a class). Returns whether the button is
    // active so callers can work out which device is being used.
    private bool Poll(DirectionKey key, ButtonControl button)
    {
        if (button.isPressed)
            key.isPressed = true;
        if (button.wasPressedThisFrame)
        {
            key.isDown = true;
            key.isPressed = true;   // catches taps shorter than one frame
        }
        if (button.wasReleasedThisFrame)
            key.isUp = true;

        return button.isPressed || button.wasReleasedThisFrame;
    }

    private void GetButtonInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.anyKey.isPressed)
                HasKeyboardInput = true;

            if (IncludeKeyboardArrows)
            {
                Poll(Up, keyboard.upArrowKey);
                Poll(Down, keyboard.downArrowKey);
                Poll(Left, keyboard.leftArrowKey);
                Poll(Right, keyboard.rightArrowKey);
            }
            if (IncludeWSAD)
            {
                Poll(Up, keyboard.wKey);
                Poll(Down, keyboard.sKey);
                Poll(Left, keyboard.aKey);
                Poll(Right, keyboard.dKey);
            }
            if (IncludeNumeric)
            {
                Poll(Up, keyboard.numpad8Key);
                Poll(Down, keyboard.numpad2Key);
                Poll(Left, keyboard.numpad4Key);
                Poll(Right, keyboard.numpad6Key);

                if (IncludeNumericDiagonal)
                {
                    Poll(Up, keyboard.numpad7Key);
                    Poll(Left, keyboard.numpad7Key);

                    Poll(Up, keyboard.numpad9Key);
                    Poll(Right, keyboard.numpad9Key);

                    Poll(Down, keyboard.numpad3Key);
                    Poll(Right, keyboard.numpad3Key);

                    Poll(Down, keyboard.numpad1Key);
                    Poll(Left, keyboard.numpad1Key);
                }
            }
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            // |= (not ||) so every Poll still runs.
            bool used = false;

            if (IncludeDpad)
            {
                used |= Poll(Up, pad.dpad.up);
                used |= Poll(Down, pad.dpad.down);
                used |= Poll(Left, pad.dpad.left);
                used |= Poll(Right, pad.dpad.right);
            }

            if (IncludeLeftStick)
            {
                used |= Poll(Up, pad.leftStick.up);
                used |= Poll(Down, pad.leftStick.down);
                used |= Poll(Left, pad.leftStick.left);
                used |= Poll(Right, pad.leftStick.right);
            }

            if (IncludeRightStick)
            {
                used |= Poll(Up, pad.rightStick.up);
                used |= Poll(Down, pad.rightStick.down);
                used |= Poll(Left, pad.rightStick.left);
                used |= Poll(Right, pad.rightStick.right);
            }

            // HasControllerInput was never set before.
            if (used) HasControllerInput = true;
        }

        Joystick joystick = Joystick.current;
        if (IncludeJoystick && joystick != null && joystick.stick != null)
        {
            bool used = false;
            used |= Poll(Up, joystick.stick.up);
            used |= Poll(Down, joystick.stick.down);
            used |= Poll(Left, joystick.stick.left);
            used |= Poll(Right, joystick.stick.right);
            if (used) HasControllerInput = true;
        }

        CalcAnys();
    }

    // Kye moves in ONE direction at a time (never diagonally). The most recently
    // pressed key wins; when it's released, the newest key that's still held takes over.
    private void ProcessInput()
    {
        // Vertical first, horizontal last: if two keys go down on the same frame
        // (e.g. a numpad diagonal) the horizontal one gets the later stamp and wins.
        Stamp(Up);
        Stamp(Down);
        Stamp(Left);
        Stamp(Right);

        DirectionKey winner = Newer(Newer(Newer(Newer(null, Up), Down), Left), Right);

        x = 0;
        y = 0;

        if (winner == Up) y = 1;
        else if (winner == Down) y = -1;
        else if (winner == Left) x = -1;
        else if (winner == Right) x = 1;
    }

    private void Stamp(DirectionKey key)
    {
        if (key.isDown)
            key.pressOrder = ++_pressCounter;
        else if (!key.isPressed)
            key.pressOrder = 0;
    }

    // Returns whichever of a / b is held and was pressed most recently.
    private static DirectionKey Newer(DirectionKey a, DirectionKey b)
    {
        if (!b.isPressed) return a;
        if (a == null || b.pressOrder > a.pressOrder) return b;
        return a;
    }

    // Used by mouse/auto-follow movement in GameController.
    public void SetXY(Vector2 newInput, bool setAnys = true)
    {
        x = newInput.x;
        y = newInput.y;

        if (setAnys && (x != 0 || y != 0))
        {
            AnyKey = true;
            AnyKeyDown = true;
            AnyKeyChange = true;
        }
    }

    private void CalcAnys()
    {
        AnyKey = Up.isPressed || Down.isPressed || Left.isPressed || Right.isPressed;
        AnyKeyDown = Up.isDown || Down.isDown || Left.isDown || Right.isDown;
        AnyKeyUp = Up.isUp || Down.isUp || Left.isUp || Right.isUp;
        AnyKeyChange = AnyKeyDown || AnyKeyUp;
    }

    public Vector2Int Get101()
    {
        int xi = x == 0 ? 0 : (x < 0 ? -1 : 1);
        int yi = y == 0 ? 0 : (y < 0 ? -1 : 1);
        return new Vector2Int(xi, yi);
    }
}
