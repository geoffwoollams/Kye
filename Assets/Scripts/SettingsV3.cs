using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One entry per level. JsonUtility can't serialize Dictionary,
/// so we store a List of these and look them up by name.
/// </summary>
[Serializable]
public class LevelRecord
{
    public string LevelName;
    public float BestTime;      // seconds; 0 or less = never completed
    public int Deaths;
    public int TimesCompleted;

    public LevelRecord(string levelName, float bestTime = 0f)
    {
        LevelName = levelName;
        BestTime = bestTime;
        Deaths = 0;
        TimesCompleted = 0;
    }

    public bool HasBeenCompleted => BestTime > 0f;
}

[Serializable]
public class SettingsV3
{
    // Bump this whenever the saved layout changes.
    public const int CurrentVersion = 3;
    public int Version = CurrentVersion;

    public string PlayerID = "";

    // --- Display ---
    public bool IsFullScreen;
    public int ScreenWidth;
    public int ScreenHeight;
    public bool VSync;

    // --- Audio ---
    [Range(0f, 1f)] public float MasterVolume;
    [Range(0f, 1f)] public float MusicVolume;
    [Range(0f, 1f)] public float SfxVolume;

    // --- Progress ---
    public string LastLevelPlayed;
    public string UnlockedLevel;
    public List<LevelRecord> LevelRecords = new List<LevelRecord>();

    // --- Cheats / debug ---
    public bool UnlockAllLevels;
    public bool GodMode;
    public bool UnlimitedLives;

    // Parameterless constructor gives sensible defaults for a brand new save.
    public SettingsV3()
    {
        PlayerID = GeneratePlayerID();
        
        IsFullScreen = true;
        ScreenWidth = 1920;
        ScreenHeight = 1080;
        VSync = true;

        MasterVolume = 1f;
        MusicVolume = 0.8f;
        SfxVolume = 0.8f;

        LastLevelPlayed = "";
        UnlockedLevel = "";

        UnlockAllLevels = false;
        GodMode = false;
        UnlimitedLives = false;
    }

    // Convenience constructor: defaults, then the given values.
    public SettingsV3(string lastLevelPlayed, int screenWidth, int screenHeight, bool isFullScreen,
                      string unlockedLevel, bool unlockAllLevels, bool godMode, bool unlimitedLives)
        : this()
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
    
    public static string GeneratePlayerID()
    {
        return Guid.NewGuid().ToString("N");
    }

    // ---------------- Level record helpers ----------------

    public LevelRecord GetRecord(string levelName)
    {
        return LevelRecords.Find(r => r.LevelName == levelName);
    }

    /// <summary>Returns the best time in seconds, or null if the level was never completed.</summary>
    public float? GetBestTime(string levelName)
    {
        LevelRecord record = GetRecord(levelName);
        return (record != null && record.HasBeenCompleted) ? record.BestTime : (float?)null;
    }

    /// <summary>
    /// Call when a level is finished. Returns true if this was a new best time
    /// (handy for showing a "NEW RECORD!" message).
    /// </summary>
    public bool SubmitLevelTime(string levelName, float timeInSeconds)
    {
        LevelRecord record = GetRecord(levelName);
        if (record == null)
        {
            record = new LevelRecord(levelName, timeInSeconds);
            LevelRecords.Add(record);
        }

        record.TimesCompleted++;

        if (!record.HasBeenCompleted || timeInSeconds < record.BestTime)
        {
            record.BestTime = timeInSeconds;
            return true;
        }
        return false;
    }

    public void AddDeath(string levelName)
    {
        LevelRecord record = GetRecord(levelName);
        if (record == null)
        {
            record = new LevelRecord(levelName);
            LevelRecords.Add(record);
        }
        record.Deaths++;
    }

    /// <summary>Call after loading to make sure old/missing data doesn't cause null errors.</summary>
    public void Validate()
    {
        if (LevelRecords == null) LevelRecords = new List<LevelRecord>();
        MasterVolume = Mathf.Clamp01(MasterVolume);
        MusicVolume = Mathf.Clamp01(MusicVolume);
        SfxVolume = Mathf.Clamp01(SfxVolume);
        if (ScreenWidth <= 0 || ScreenHeight <= 0)
        {
            ScreenWidth = 1920;
            ScreenHeight = 1080;
        }
        Version = CurrentVersion;
        
        if(PlayerID == "")
            PlayerID = GeneratePlayerID();
    }
}
