using UnityEngine;
using UnityEngine.UI;

public class OSAdjustments : MonoBehaviour
{
    public GameObject ExitButton, HelpButton, SettingsButton;
    public Button LevelsButton;
    public GameObject[] HideOnWebGL;
    
    void Start()
    {
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        // If we are on mobile/web, hide the exit button and move the help and settings buttons to the right
        ExitButton.SetActive(false);
        
        var helpRect = HelpButton.GetComponent<RectTransform>();
        var settingsRect = SettingsButton.GetComponent<RectTransform>();
        
        helpRect.anchoredPosition = new Vector2(helpRect.anchoredPosition.x + 50, helpRect.anchoredPosition.y);
        settingsRect.anchoredPosition = new Vector2(settingsRect.anchoredPosition.x + 50, settingsRect.anchoredPosition.y);

        Navigation levelsNav = LevelsButton.navigation;
        levelsNav.selectOnDown = SettingsButton.GetComponent<Button>();
        LevelsButton.navigation = levelsNav;
#endif

#if UNITY_WEBGL
        // Browsers have no file dialog, so hide the open and save buttons.
        foreach (var button in HideOnWebGL)
            if (button != null) button.SetActive(false);
#endif
    }
}
