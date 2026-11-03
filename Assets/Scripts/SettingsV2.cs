using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SettingsV2
{
    public string LastLevelPlayed;
    public bool IsFullScreen;
    public int ScreenWidth;
    public int ScreenHeight;
    public string UnlockedLevel;
    public bool UnlockAllLevels;
    public bool GodMode;
    public bool UnlimitedLives;

    public SettingsV2(string lastLevelPlayed, int screenWidth, int screenHeight, bool isFullScreen, string unlockedLevel,  bool unlockAllLevels, bool godMode, bool unlimitedLives)
    {
        LastLevelPlayed = lastLevelPlayed;
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        IsFullScreen = isFullScreen;
        UnlockedLevel = unlockedLevel;
        UnlockAllLevels = unlockAllLevels;
        GodMode = godMode;
        UnlimitedLives = unlimitedLives;
    }
}
