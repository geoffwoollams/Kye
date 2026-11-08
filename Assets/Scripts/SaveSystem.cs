using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string _lastSavedJson;
    private static string PathV3 => Path.Combine(Application.persistentDataPath, "settings.v3.json");

    public static bool Save(SettingsV3 settings)
    {
        try
        {
            string json = JsonUtility.ToJson(settings, true);

            // Nothing changed since the last write, so skip the disk write.
            if (json == _lastSavedJson && File.Exists(PathV3))
                return true;

            string tempPath = PathV3 + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(PathV3)) File.Delete(PathV3);
            File.Move(tempPath, PathV3);
            _lastSavedJson = json;
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to save settings: " + ex);
            return false;
        }
    }

    public static SettingsV3 Load()
    {
        SettingsV3 settings = null;

        if (File.Exists(PathV3))
            settings = TryLoadV3();

        if (settings == null)
        {
            Debug.Log("No usable save found. Creating new one at " + PathV3);
            settings = CreateDefault();
            if(settings.PlayerID == "")
                settings.PlayerID = SettingsV3.GeneratePlayerID();
            
            Save(settings);
        }

        settings.Validate();
        return settings;
    }

    private static SettingsV3 TryLoadV3()
    {
        try
        {
            return LoadV3(PathV3);
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to load " + PathV3 + ": " + ex);
            try { File.Copy(PathV3, PathV3 + ".corrupt", true); } catch { /* best effort */ }
            return null;
        }
    }

    private static SettingsV3 LoadV3(string path)
    {
        string json = File.ReadAllText(path);
        SettingsV3 settings = JsonUtility.FromJson<SettingsV3>(json);

        if (settings.PlayerID == null)
            settings.PlayerID = SettingsV3.GeneratePlayerID();

        return settings;
    }

    private static SettingsV3 CreateDefault()
    {
        SettingsV3 settings = new SettingsV3(
            "first",
            820,
            640,
            true,
            "first",
            false,
            false,
            false
        );

#if UNITY_WEBGL && !UNITY_EDITOR
        settings.UnlockAllLevels = true;
#endif

        return settings;
    }
}
