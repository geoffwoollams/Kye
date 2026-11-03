using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class KyeAnalytics
{
    public static bool enabled = true;
    
    private const string Url = "https://playkye.com/log.php";
    
    public static void Launch(MonoBehaviour runner)
    {
        var data = new Dictionary<string, object>
        {
            { "pid", AppController.Instance.settings.PlayerID },
            { "event", "launch" },
            { "version", Application.version },
            { "platform", Application.platform.ToString() },
            { "os", SystemInfo.operatingSystem }
        };

        Send(runner, data);
    }
    
    public static void LevelStarted(MonoBehaviour runner, string level)
    {
        if (GameController.Instance.levelEditor.IsPlaying)
            return;
        
        var data = new Dictionary<string, object>
        {
            { "pid", AppController.Instance.settings.PlayerID },
            { "event", "level_started" },
            { "version", Application.version },
            { "level", level }
        };

        Send(runner, data);
    }
    
    public static void LevelRestarted(MonoBehaviour runner, string level)
    {
        if (GameController.Instance.levelEditor.IsPlaying)
            return;
        
        var data = new Dictionary<string, object>
        {
            { "pid", AppController.Instance.settings.PlayerID },
            { "event", "level_restarted" },
            { "version", Application.version },
            { "level", level }
        };

        Send(runner, data);
    }

    public static void LevelComplete(MonoBehaviour runner, string level)
    {
        if (GameController.Instance.levelEditor.IsPlaying)
            return;
        
        var data = new Dictionary<string, object>
        {
            { "pid", AppController.Instance.settings.PlayerID },
            { "event", "level_complete" },
            { "version", Application.version },
            { "level", level }
        };

        Send(runner, data);
    }
    
    public static void LevelFailed(MonoBehaviour runner, string level)
    {
        if (GameController.Instance.levelEditor.IsPlaying)
            return;
        
        var data = new Dictionary<string, object>
        {
            { "pid", AppController.Instance.settings.PlayerID },
            { "event", "level_failed" },
            { "version", Application.version },
            { "level", level }
        };

        Send(runner, data);
    }

    private static void Send(MonoBehaviour runner, Dictionary<string, object> data)
    {
#if UNITY_EDITOR || UNITY_WEBGL
        if (!enabled)
            return;
        
        runner.StartCoroutine(SendCoroutine(data));
#endif
    }

    private static IEnumerator SendCoroutine(Dictionary<string, object> data)
    {
        string json = DictionaryToJson(data);
        
        using (UnityWebRequest request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(
                    $"Analytics failed: HTTP {request.responseCode}\n" +
                    $"Error: {request.error}\n" +
                    $"Response: {request.downloadHandler.text}"
                );
            }
        }
    }

    private static string DictionaryToJson(Dictionary<string, object> data)
    {
        var parts = new List<string>();

        foreach (var pair in data)
        {
            string value;

            if (pair.Value is string)
            {
                value = "\"" + EscapeJson(pair.Value.ToString()) + "\"";
            }
            else if (pair.Value is bool)
            {
                value = pair.Value.ToString().ToLowerInvariant();
            }
            else
            {
                value = pair.Value.ToString();
            }

            parts.Add(
                "\"" + EscapeJson(pair.Key) + "\":" + value
            );
        }

        return "{" + string.Join(",", parts) + "}";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
