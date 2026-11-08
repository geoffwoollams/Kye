using UnityEngine;

public class AppController : MonoBehaviour
{
    public SettingsV3 settings;

    public static AppController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Go()
    {
        if(Instance != null)
            return;
        
        GameObject appGo = new GameObject();
        appGo.name = "AppController";
        DontDestroyOnLoad(appGo);

        AppController app = appGo.AddComponent<AppController>();
        Instance = app;
    }

    void Awake()
    {
        settings = SaveSystem.Load();
    }

    void Start()
    {
        GameController.Instance.tUnlimitedLives.isOn = settings.UnlimitedLives;
        GameController.Instance.tUnlockAllLevels.isOn = settings.UnlockAllLevels;
        GameController.Instance.tGodMode.isOn = settings.GodMode;
        
        KyeAnalytics.Launch(this);
    }

    public void Save()
    {
        SaveSystem.Save(settings);
    }
}
