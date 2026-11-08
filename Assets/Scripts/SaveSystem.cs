using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveSystem
{
    // Properties instead of static fields: Application.persistentDataPath
    // is safer to read lazily than during static initialisation.
    private static string PathV1 => Path.Combine(Application.persistentDataPath, "settings.v1.dat");
    private static string PathV2 => Path.Combine(Application.persistentDataPath, "settings.v2.dat");
    private static string _lastSavedJson;
    private static string PathV3 => Path.Combine(Application.persistentDataPath, "settings.v3.json");

    // ---------------------------------------------------------------
    // Save
    // ---------------------------------------------------------------

    /// <summary>Returns true if the file was written successfully.</summary>
    public static bool Save(SettingsV3 settings)
    {
        try
        {
            string json = JsonUtility.ToJson(settings, true);

            // Nothing changed since the last write, so skip the disk write.
            if (json == _lastSavedJson && File.Exists(PathV3))
                return true;

            // Write to a temp file first so a crash mid-write can't corrupt the real save.
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

    // ---------------------------------------------------------------
    // Load
    // ---------------------------------------------------------------

    /// <summary>Always returns a valid object (never null).</summary>
    public static SettingsV3 Load()
    {
        SettingsV3 settings = null;

        // Newest format first, so an old leftover file can never overwrite newer data.
        if (File.Exists(PathV3))      settings = TryLoad(PathV3, LoadV3);
        else if (File.Exists(PathV2)) settings = TryLoad(PathV2, MigrateFromV2);
        else if (File.Exists(PathV1)) settings = TryLoad(PathV1, MigrateFromV1);

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

    /// <summary>
    /// Runs a loader. If it throws (corrupt file etc.) the bad file is kept as ".corrupt"
    /// instead of being silently overwritten, and null is returned.
    /// </summary>
    private static SettingsV3 TryLoad(string path, Func<string, SettingsV3> loader)
    {
        try
        {
            return loader(path);
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to load " + path + ": " + ex);
            try { File.Copy(path, path + ".corrupt", true); } catch { /* best effort */ }
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

    private static SettingsV3 MigrateFromV2(string path)
    {
        SettingsV2 old = ReadLegacy<SettingsV2>(path);

        SettingsV3 migrated = new SettingsV3(
            old.LastLevelPlayed,
            old.ScreenWidth,
            old.ScreenHeight,
            old.IsFullScreen,
            old.LastLevelPlayed,   // TODO: if SettingsV2 has its own UnlockedLevel, use that instead
            false,
            false,
            false
        );

        // Only delete the old file once the new one is safely written.
        if (Save(migrated)) File.Delete(path);
        return migrated;
    }

    private static SettingsV3 MigrateFromV1(string path)
    {
        Settings old = ReadLegacy<Settings>(path);

        SettingsV3 migrated = new SettingsV3(
            old.LastLevelPlayed,
            old.ScreenWidth,
            old.ScreenHeight,
            old.IsFullScreen,
            old.LastLevelPlayed,   // V1 had no unlock data, so this is the best guess
            false,
            false,
            false
        );

        if (Save(migrated)) File.Delete(path);
        return migrated;
    }

    // BinaryFormatter is only used to READ old V1/V2 files now.
    // Once you no longer support those, delete this method and the using above.
#pragma warning disable SYSLIB0011
    private static T ReadLegacy<T>(string path) where T : class
    {
        using (FileStream stream = new FileStream(path, FileMode.Open))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            T data = formatter.Deserialize(stream) as T;
            if (data == null) throw new InvalidDataException("Legacy save was not a " + typeof(T).Name);
            return data;
        }
    }
#pragma warning restore SYSLIB0011

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
