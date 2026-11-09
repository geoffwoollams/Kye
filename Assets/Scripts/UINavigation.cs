using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UINavigation : MonoBehaviour
{
    private EventSystem eventSystem;
    private GameObject lastSelectedElement;
    private ScrollRect scrollRect;

    void Awake()
    {
        eventSystem = FindAnyObjectByType<EventSystem>();
        lastSelectedElement = eventSystem.firstSelectedGameObject;
        
        scrollRect = GameController.Instance.HelpPanel.transform.Find("Scroll View").GetComponent<ScrollRect>();
    }

    void Update()
    {
        if (eventSystem.currentSelectedGameObject && lastSelectedElement != eventSystem.currentSelectedGameObject)
            lastSelectedElement = eventSystem.currentSelectedGameObject;

        if (!eventSystem.currentSelectedGameObject && lastSelectedElement)
            eventSystem.SetSelectedGameObject(lastSelectedElement);


        if (GameController.Instance.MessageContainer.activeSelf)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    GameController.Instance.CloseMessage();
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.selectButton.wasPressedThisFrame)
                    GameController.Instance.CloseMessage();
                if (Gamepad.current.startButton.wasPressedThisFrame)
                    GameController.Instance.CloseMessage();
                if (Gamepad.current.bButton.wasPressedThisFrame)
                    GameController.Instance.CloseMessage();
            }
        }

        else if (GameController.Instance.GamePanel.activeSelf && !GameController.Instance.MainMenu.activeSelf)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    GameController.Instance.PauseToggle();
                if (Keyboard.current.rKey.wasPressedThisFrame)
                    GameController.Instance.RestartLevel();
                if (Keyboard.current.nKey.wasPressedThisFrame)
                    GameController.Instance.NextLevel();
                if (Keyboard.current.pKey.wasPressedThisFrame)
                    GameController.Instance.PrevLevel();
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.selectButton.wasPressedThisFrame)
                    GameController.Instance.PauseToggle();
                if (Gamepad.current.startButton.wasPressedThisFrame)
                    GameController.Instance.PauseToggle();
                if (Gamepad.current.bButton.wasPressedThisFrame)
                    GameController.Instance.PauseToggle();
                if (Gamepad.current.yButton.wasPressedThisFrame)
                    GameController.Instance.RestartLevel();
            }
        }
        
        else if (GameController.Instance.MainMenu.activeSelf && GameController.Instance.HelpPanel.activeSelf)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    GameController.Instance.CloseHelp();
                
                if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
                {
                    scrollRect.verticalNormalizedPosition -= 0.006f;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
                if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
                {
                    scrollRect.verticalNormalizedPosition += 0.006f;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.bButton.wasPressedThisFrame)
                    GameController.Instance.CloseHelp();
                
                float stickY = Gamepad.current.leftStick.ReadValue().y + Gamepad.current.rightStick.ReadValue().y;
                
                if (Gamepad.current.dpad.down.isPressed || stickY < -0.5f)
                {
                    scrollRect.verticalNormalizedPosition -= 0.006f;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
                if (Gamepad.current.dpad.up.isPressed || stickY > 0.5f)
                {
                    scrollRect.verticalNormalizedPosition += 0.006f;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
            }
        }
        
        else if (GameController.Instance.MainMenu.activeSelf && GameController.Instance.SettingsPanel.activeSelf)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    GameController.Instance.SettingsToggle();
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.bButton.wasPressedThisFrame)
                    GameController.Instance.SettingsToggle();
            }
        }

        else if (GameController.Instance.EditorPanel.activeSelf)
        {
            var editor = GameController.Instance.EditorPanel.GetComponent<LevelEditor>();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.pKey.wasPressedThisFrame)
                    editor.ClickPlay();
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    editor.ShowMenu();
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.selectButton.wasPressedThisFrame)
                    editor.ShowMenu();
                if (Gamepad.current.startButton.wasPressedThisFrame)
                    editor.ShowMenu();
            }
        }

        else if (GameController.Instance.LevelPickerPanel.activeSelf)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    if (EventSystem.current.currentSelectedGameObject == GameController.Instance.LoadLevelInput.gameObject) {
                        //GameController.Instance.LoadLevelInput.text = GameController.Instance.LoadLevelInput.text;
                        EventSystem.current.SetSelectedGameObject(GameController.Instance.LevelsScreenPrimaryButton);
                    }
                    else
                        GameController.Instance.BackToMainMenu();
                }
            }
            
            if (Gamepad.current != null)
            {
                //if (Gamepad.current.bButton.wasPressedThisFrame && !GameController.Instance.LoadLevelInput.isFocused)
                //    GameController.Instance.BackToMainMenu();
            }
        }
    }
}
