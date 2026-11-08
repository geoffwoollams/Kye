using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System;
using System.Linq;
using SimpleFileBrowser;
using TMPro;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    public InputManager inputManager;

    [HideInInspector]
    public Vector2 StartPoint;
    [HideInInspector]
    public Item Kye;

    public Transform CameraTransform;
    public Camera actualCamera;

    public Canvas MainCanvas;

    public GameObject GamePanel, MainMenu, PauseMenu, EditorPanel, LevelPickerPanel, LevelFailed, LevelComplete;
    public GameObject PlayButton, LoadLevelButton, ResumeButton, LevelFailedButton, LevelCompleteButton, EditorMenuButton, LevelsScreenPrimaryButton, LevelsScreenOpenButton;
    public TMPro.TMP_InputField LoadLevelInput;
    public GameObject LevelPickerItem, LevelPickerContainer;
    public GameObject LevelPickerContainerContainer;
    public ScrollRectX LevelPickerScrollRect;

    public Button PausePreviousLevel, PauseNextLevel;

    public GameObject SettingsPanel;
    public Toggle tUnlockAllLevels, tUnlimitedLives, tGodMode;
    public GameObject SettingsBack;

    public GameObject HelpPanel, HelpItem, HelpPrimaryButton;
    
    public Transform ItemContainer;
    public TMPro.TMP_Text Diamonds;
    public TMPro.TMP_Text LevelNotes, LevelName;
    public Image Life1, Life2, Life3;

    public GameObject Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9;
    public GameObject ItemKye, ItemEarth, ItemDiamond;
    public GameObject ItemBlockSquare, ItemBlockRound;
    public GameObject ItemSliderUp, ItemSliderDown, ItemSliderLeft, ItemSliderRight;
    public GameObject ItemStickerTB, ItemStickerLR;
    public GameObject ItemBouncerUp, ItemBouncerDown, ItemBouncerLeft, ItemBouncerRight;
    public GameObject ItemRockyUp, ItemRockyDown, ItemRockyLeft, ItemRockyRight;
    public GameObject ItemTwister, ItemGnasher, ItemBlob, ItemVirus, ItemSpike;
    public GameObject ItemAntiClocker, ItemClocker, ItemAutoSlider, ItemAutoRocky, ItemBlackhole;
    public GameObject ItemDoorLR, ItemDoorRL, ItemDoorUD, ItemDoorDU;
    public GameObject ItemTimer3, ItemTimer4, ItemTimer5, ItemTimer6, ItemTimer7, ItemTimer8, ItemTimer9;
    public GameObject ItemSploder, SplodeEffect, ItemTracker, ItemGuard;
    public GameObject KyeGhost;
    public GameObject TouchControls;

    private bool _autoFollowMouse;
    private float _lastAutoMove;
    private GameObject _autoFollowGhost;

    public Item[,] Items = new Item[30, 20];

    private Vector2 _input;
    public Vector2Int _inputClickPosition, _lastInputClickPosition;
    private int _startLives = 3;
    private int _lives;
    private int _diamonds;
    private float _nextMoveTime;
    private float _movementDelayInitial = 0.4f;
    private float _movementDelay = 0.05f;
    public bool _canStartMoving;
    private int _itemCount;

    public int CurrentLevelID = 0;
    public string CurrentLevelKey = "";
    public string CurrentLevelName = "";
    public string CurrentLevelNameR = "";
    public string CurrentLevelNotes = "";
    public string CurrentLevelFinishMessage = "";

    private float _timeSinceLevelLoaded;
    private float _lastTimeUpdate;
    
    private string _currentKyeLevelName = "";
    private string _editorPlaytestLevel = "";

    public LevelEditor levelEditor;
    public GameObject levelEditorBackground;

    public GameObject FrameUI, FrameSprites;

    public static Dictionary<string, string> KyeFile = new Dictionary<string, string>();

    public GameObject MessageContainer;
    public TMPro.TMP_Text MessageTitle, MessageText, VersionNote;

    public GameObject LevelEditorItemBorder;
    public Transform LevelEditorGridItemOutline;

    public Sprite helpShortcuts;
    public Sprite helpControls;

    private string _currentLevelB64;
    private string _currentLevelFilePath = "";
    
    private Vector2Int _lastRawInput;
    private bool _applyInitialDelay;
    
    private static readonly System.Random random = new System.Random();

    void Awake()
    {
        Instance = this;
        VersionNote.text = "Version " + Application.version;

#if UNITY_EDITOR
        Levels.CheckForDuplicates(Levels.Classic);
#endif
    }

    public void OpenKyeWebsite()
    {
        Application.OpenURL("https://playkye.com");
    }

    void Start()
    {
        LoadLocalFiles();
        LoadLevelPicker();
        ShowMainMenu();
        
        if (Kye == null)
            Kye = GameObject.Find("kye").GetComponent<Item>();
        
        ResetLevel();
        _canStartMoving = true;

        UpdateUI();
        
#if UNITY_EDITOR 
        LoadLevelWithString(
            "DEV\r\n" +
            "Testing\r\n" + 
            "Yay?\r\n" +
            "555555555555555555555555555555\r\n" +
            "5  v *           5D5         5\r\n" +
            "5                 D   D      5\r\n" +
            "5           K          D     5\r\n" +
            "5                            5\r\n" +
            "5%      5                    5\r\n" +
            "5      5          H     C    5\r\n" +
            "5       5         H          5\r\n" +
            "5             C              5\r\n" +
            "5         5                 %5\r\n" +
            "5        %5                  5\r\n" +
            "5         5      [           5\r\n" +
            "5         5!                 5\r\n" +
            "5          e          BBB    5\r\n" +
            "5         5                  5\r\n" +
            "5         5%      be         5\r\n" +
            "5         5       b#e        5\r\n" +
            "5             ~         l    5\r\n" +
            "5      !                     5\r\n" +
            "555555555555555555555555555555\r\n"
            );
#endif
    }

    public void CloseMessage()
    {
        MessageContainer.SetActive(false);
    }

    public void Message(string title, string text)
    {
        MessageTitle.text = title;
        MessageText.text = text;
        MessageContainer.SetActive(true);
    }

    private void LoadLocalFiles() {
        //Levels.Classic.Add("test33333333","RklSU1QKSnVzdCBmb3IgcHJhY3RpY2UNClRoZSBmaXJzdCBsZXZlbCB3YXMgZm9yIHByYWN0aWNlLg0KNTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1DQo1VCAgIGUgICAgICAgSyogIGEgICAgZCBlICAgRTUNCjUgICAgYiA0NTU1NTYgICAgICAgIGEgIGIgICAgNQ0KNSAgICBiIGR2dnZ2ZCAgICAgICAgICAgYiAgICA1DQo1ICAgIGIgZHZ2dnZkICAgICAgICAgIGFiICAgIDUNCjVlYmJiZSBlZUJCZWUgICAgICAgYyAgIGViYmJlNQ0KNSAgICAgICAgICAgICAgIGEgICAgICAgICAgICA1DQo1IDhycmUgICAgICAgICAgICAgICAgYSBlbGw4IDUNCjUgNT4+ZSAgICAgIHMgIFMgICAgICAgIGU8PDUgNQ0KNSA1Pj5CICAgICAgICAgICAgICAgICAgQjw8NSA1DQo1IDU+PkIgICAgICAgICAgICAgICBiICBCPDw1IDUNCjUgNT4+ZSAgICAgIFMgIHMgICAgIFUgIGU8PDUgNQ0KNSAycnJlICAgICAgICAgICAgICAgYiAgZWxsMiA1DQo1ICAgICAgICAgICAgICAgICBiUmJiICAgICAgIDUNCjVlYmJiZSBlZWVlZWUgIDc1NTU1NTkgIGViYmJlNQ0KNSAgICBiIHVeXl5edSAgNSAgICAgNSAgYiAgICA1DQo1ICAgIGIgdV5eXl51ICA1ICAgICA1ICBiICAgIDUNCjUgICAgYiA0NTU1NTYgIDUgICAgIDUgIGIgICAgNQ0KNUMgICBlICAgICAgICAgZSAgWyAgZSAgZSAgIH41DQo1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTU1NTUNCg==");
    }

    private void LevelPickerItemInit(ref GameObject levelItem, string levelB64, int x, int y, int ln, bool unlocked)
    {
        var LevelName = levelItem.transform.Find("LevelName").GetComponent<TMPro.TMP_Text>();
        var LevelNotes = levelItem.transform.Find("LevelNotes").GetComponent<TMPro.TMP_Text>();
        var LevelButton = levelItem.GetComponent<Button>();
        var EditButton = levelItem.transform.Find("Edit").GetComponent<Button>();

        var LevelNumber = levelItem.transform.Find("Number").Find("LevelNumber").GetComponent<TMPro.TMP_Text>();
        LevelNumber.text = ln.ToString();

        var levelItemRect = levelItem.GetComponent<RectTransform>();
        //Common.SetLeft(ref levelItemRect, 20);
        //Common.SetRight(ref levelItemRect, 20);


        var levelItemRectPosition = levelItemRect.anchoredPosition;
        //levelItemRectPosition.x = x;
        levelItemRectPosition.y = y;
        levelItemRect.anchoredPosition = levelItemRectPosition;

        var level = Common.Base64Decode(levelB64);

        string[] lines = level.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        );

        LevelName.text = lines[0].Trim();
        LevelNotes.text = lines[1].Trim();

        levelItem.name = "LevelItem " + LevelName.text;
        
        LevelItem lItem = levelItem.GetComponent<LevelItem>();

        float? bestTime = AppController.Instance.settings.GetBestTime(LevelName.text.ToLower());
        if (bestTime != null && bestTime > 0)
        {
            Debug.Log("bestTime: " + bestTime);
            lItem.bestTimeText.gameObject.SetActive(true);
            lItem.bestTimeText.text = Common.FormatTime(bestTime);
        }
        else
        {
            lItem.bestTimeText.gameObject.SetActive(false);
        }

        if(unlocked)
        {
            LevelButton.onClick.AddListener(delegate { LevelPickerClick(); });
            EditButton.onClick.AddListener(delegate{LevelPickerEditClick();});
        }
        else
        {
            levelItem.transform.Find("Number").gameObject.SetActive(false);
            levelItem.transform.Find("Lock").gameObject.SetActive(true);
            EditButton.gameObject.SetActive(false);
        }
    }

    public string GetLevelName(string b64) {
        var level = Common.Base64Decode(b64);

        string[] lines = level.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        );

        return lines[0].Trim();
    }

    public void ClearLevelList()
    {
        LoadLevelInput.text = "";
        KyeFile = new Dictionary<string, string>();
        LoadLevelPicker();
    }

    public void LoadLevelPicker()
    {
        LoadLevelPicker(Levels.Classic, false);
        LoadLevelInput.gameObject.SetActive(true);
    }

    public void LoadLevelPicker(Dictionary<string, string> levels, bool unlockAll)
    {
        while (LevelPickerContainer.transform.childCount > 0)
        {
            DestroyImmediate(LevelPickerContainer.transform.GetChild(0).gameObject);
        }
        
        var filterInput = LoadLevelInput.text.Trim();
        var filter = filterInput.ToLower();

        if (filterInput.StartsWith("K64_") && filterInput.EndsWith("_K64"))
        {
            LoadLevelInput.text = "";
                
            var levelB64 = filterInput.Substring(4);
            levelB64 = levelB64.Substring(0, levelB64.Length - 4);

            LoadLevelWithB64(levelB64);
            return;
        }

        int y = -5;
        int x = 0;

        //var levels = Common.ShuffledLevels();
        //var levels = Levels.Classic.Values;

        GameObject levelItem, prevItem = null;
        Selectable selectOnUp = LevelsScreenPrimaryButton.GetComponent<Button>();

        bool doneFirst = false;

        int ln = 1;

        //foreach (var levelB64 in levels.Values)
        foreach (KeyValuePair<string, string> entry in levels)
        {
            string levelKey = entry.Key;
            string levelB64 = entry.Value;

            bool isUnlocked = Common.IsLevelUnlocked(levelKey);
            if(unlockAll)
                isUnlocked = true;
            
            if (filter == "" || GetLevelName(levelB64).ToLower().Contains(filter))
            {
                levelItem = Instantiate(LevelPickerItem);
                levelItem.transform.SetParent(LevelPickerContainer.transform, false);
                var lItem = levelItem.GetComponent<LevelItem>();
                lItem.b64 = levelB64;
                LevelPickerItemInit(ref levelItem, levelB64, x, y, ln, isUnlocked);
                y -= 55;

                ln++;
                
                var editButton = levelItem.transform.Find("Edit").GetComponent<Button>();
                var itemButton = levelItem.GetComponent<Button>();
                
                Navigation editNav = editButton.navigation;
                editNav.mode = Navigation.Mode.Explicit;
                
                if(prevItem)
                    editNav.selectOnUp = prevItem.transform.Find("Edit").GetComponent<Button>();
                else
                    editNav.selectOnUp = LevelsScreenPrimaryButton.GetComponent<Button>();
                
                editNav.selectOnLeft = itemButton;
                editButton.navigation = editNav;
                
                Navigation itemNav = itemButton.navigation;
                itemNav.mode = Navigation.Mode.Explicit;
                itemNav.selectOnUp = selectOnUp;
                itemNav.selectOnRight = editButton;
                itemButton.navigation = itemNav;

                if(!isUnlocked)
                {
                    itemButton.interactable = false;
                }

                if (!doneFirst)
                {
                    var buttonMenu = LevelsScreenPrimaryButton.GetComponent<Button>();
                    var nav = buttonMenu.navigation;
                    nav.selectOnDown = itemButton;
                    buttonMenu.navigation = nav;

                    var buttonOpen = LevelsScreenOpenButton.GetComponent<Button>();
                    nav = buttonOpen.navigation;
                    nav.selectOnDown = itemButton;
                    buttonOpen.navigation = nav;

                    var searchInput = LoadLevelInput.GetComponent<TMP_InputField>();
                    nav = searchInput.navigation;
                    nav.selectOnDown = itemButton;
                    searchInput.navigation = nav;

                    doneFirst = true;
                }

                if (prevItem)
                {
                    var prevEditButton = prevItem.transform.Find("Edit").GetComponent<Button>();
                    Navigation prevEditNav = prevEditButton.navigation;
                    prevEditNav.selectOnDown = editButton;
                    prevEditButton.navigation = prevEditNav;
                    
                    var prevButton = prevItem.GetComponent<Button>();
                    Navigation prevNav = prevButton.navigation;
                    
                    prevNav.mode = Navigation.Mode.Explicit;
                    prevNav.selectOnDown = levelItem.GetComponent<Button>();
                    prevButton.navigation = prevNav;
                }

                prevItem = levelItem;
                selectOnUp = itemButton;
            }
        }

        //y -= 5;

        var container = LevelPickerContainer.GetComponent<RectTransform>();

        var sizeDelta = container.sizeDelta;
        sizeDelta.y = -y;
        container.sizeDelta = sizeDelta;
    }

    void LevelPickerClick()
    {
        if(EventSystem.current.currentSelectedGameObject == null)
            return;

        HideMainMenu();
        
        var LevelName = EventSystem.current.currentSelectedGameObject.transform.Find("LevelName").GetComponent<TMPro.TMP_Text>();
        LoadLevelWithNoR(LevelName.text);
    }


    void LevelPickerEditClick()
    {
        var csItem = EventSystem.current.currentSelectedGameObject;

        if (csItem == null)
            return;

        if (csItem.name == "Edit")
            csItem = csItem.transform.parent.gameObject;

        var levelB64 = csItem.transform.GetComponent<LevelItem>().b64;
        var levelDecoded = Common.Base64Decode(levelB64);
        ShowEditor();
        EditorPanel.GetComponent<LevelEditor>().LoadLevel(levelDecoded);
    }

    public void QuitGame()
    {
        AppController.Instance.Save();
        Application.Quit();
    }
    
    public void SaveSettings()
    {
        AppController.Instance.settings.UnlimitedLives = GameController.Instance.tUnlimitedLives.isOn;
        AppController.Instance.settings.UnlockAllLevels = GameController.Instance.tUnlockAllLevels.isOn;
        AppController.Instance.settings.GodMode = GameController.Instance.tGodMode.isOn;
        AppController.Instance.Save();
    }

    public void ShowMainMenu()
    {
        levelEditor.IsPlaying = false;
        ItemContainer.gameObject.SetActive(true);

        LoadMenuLevel();
        GamePanel.SetActive(true);
        PauseMenu.SetActive(false);
        EditorPanel.SetActive(false);
        levelEditorBackground.SetActive(false);
        LevelPickerPanel.SetActive(false);
        LevelPickerContainerContainer.SetActive(false);
        LevelFailed.SetActive(false);
        LevelComplete.SetActive(false);

        FrameSprites.SetActive(true);
        FrameUI.SetActive(false);

        SettingsPanel.SetActive(false);
        MainMenu.SetActive(true);


        CloseMessage();

        if (MainMenu.activeSelf)
            EventSystem.current.SetSelectedGameObject(PlayButton);

        _canStartMoving = false;
        Time.timeScale = 0;
    }

    public void HideMainMenu()
    {
        GamePanel.SetActive(false);
        PauseMenu.SetActive(false);
        MainMenu.SetActive(false);
        EditorPanel.SetActive(false);
        levelEditorBackground.SetActive(false);
        LevelPickerPanel.SetActive(false);
        LevelFailed.SetActive(false);
        LevelComplete.SetActive(false);
    }

    public void SettingsToggle()
    {
        if (SettingsPanel.activeSelf)
        {
            AppController.Instance.settings.UnlimitedLives = GameController.Instance.tUnlimitedLives.isOn;
            AppController.Instance.settings.UnlockAllLevels = GameController.Instance.tUnlockAllLevels.isOn;
            AppController.Instance.settings.GodMode = GameController.Instance.tGodMode.isOn;
            
            SaveSettings();
            
            SettingsPanel.SetActive(!SettingsPanel.activeSelf);
            EventSystem.current.SetSelectedGameObject(PlayButton);
        }
        else
        {
            SettingsPanel.SetActive(!SettingsPanel.activeSelf);
            EventSystem.current.SetSelectedGameObject(SettingsBack);
        }
    }
    
    public void OpenHelp()
    {
        HelpPanel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(HelpPrimaryButton);
    }

    public void CloseHelp()
    {
        HelpPanel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(PlayButton);
    }

    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

    public void PlayGame()
    {
        LoadLevelWithNoR(AppController.Instance.settings.LastLevelPlayed);
    }

    public void ShowEditor()
    {
        ItemContainer.gameObject.SetActive(false);
        HideMainMenu();

        FrameSprites.SetActive(false);
        FrameUI.SetActive(true);

        EditorPanel.SetActive(true);
        levelEditorBackground.SetActive(true);

        EventSystem.current.SetSelectedGameObject(EditorMenuButton);
    }

    public void ShowLevelPicker()
    {
        HideMainMenu();

        FrameSprites.SetActive(true);
        FrameUI.SetActive(false);

        ClearLevelList();
        LoadLevelInput.text = "";  //AppController.Instance.settings.LastLevelPlayed;
        LevelPickerContainerContainer.SetActive(true);
        //LevelPickerContainer.GetComponent<ScrollRect>().SetY(0);
        LevelPickerPanel.SetActive(true);

        EventSystem.current.SetSelectedGameObject(LevelsScreenPrimaryButton);
    }

    public void PauseToggle()
    {
        PauseMenu.SetActive(!PauseMenu.activeSelf);
        Time.timeScale = PauseMenu.activeSelf ? 0 : 1;

        if(PauseMenu.activeSelf)
        {
            if (!EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(ResumeButton);
            _canStartMoving = false;
        }
        else
            _canStartMoving = true;
    }

    public void RandomPicker()
    {
        HideMainMenu();
        LoadRandomLevel();
        UpdateUI();
        PauseMenu.SetActive(false);
        Time.timeScale = 1;
    }

    public void LoadMenuLevel()
    {
        KyeAnalytics.enabled = false;
        LoadLevelWithNoR(AppController.Instance.settings.UnlockedLevel);
        KyeAnalytics.enabled = true;
    }

    public void LoadRandomLevel()
    {
        // IsLevelUnlocked returns true for every level when UnlockAllLevels is on
        List<string> keyList = Levels.Classic.Keys.Where(Common.IsLevelUnlocked).ToList();
        if (keyList.Count == 0)
            keyList.Add("first");

        string randomKey = keyList[Common.GetRandom(0, keyList.Count)];
        KyeAnalytics.RandomLevel(this, randomKey);
        LoadLevelWithNoR(randomKey);
    }

    public void LoadLevelWithString(string level)
    {
        if (levelEditor.IsPlaying)
            _editorPlaytestLevel = level;

        LoadLevelWithB64(Common.Base64Encode(level));
    }

    public void LoadLevelWithB64(string levelB64)
    {
        HideMainMenu();
        CurrentLevelID = 0;
        LoadLevel(levelB64);
        LoadLevelInput.text = "";
    }

    public void ClickOpen()
    {
        FileBrowser.SetFilters( true, new FileBrowser.Filter( "Kye Levels", ".kye" ) );
        FileBrowser.SetDefaultFilter( ".kye" );
        
        string openPath = Application.dataPath;
        
#if UNITY_STANDALONE_WIN
        openPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#endif

        FileBrowser.ShowLoadDialog(
            FileBrowserOnSuccess,
            FileBrowserOnCancel,
            FileBrowser.PickMode.Files, false, openPath
        );
    }

    public void FileBrowserOnSuccess(string[] paths)
    {
        string filePath = paths[0];
        LoadLevelWithKyeFile(filePath);
        _currentLevelFilePath = filePath;
    }
    
    public void FileBrowserOnCancel()
    {
        // nothing?
    }

    public void LoadLevelWithKyeFile(string file)
    {
        if (System.IO.File.Exists(file))
        {
            var level = System.IO.File.ReadAllLines(file);
            var levelCount = Int32.Parse(level[0].Trim());

            level = level.Skip(1).ToArray();

            if (levelCount <= 1)
            {
                var levelLines = string.Join("\n", level);
                //levelLines.TrimStart('\n', ' ');
                LoadLevelWithString(levelLines);
            }
            else
            {
                KyeFile = Get23(level);
                LoadLevelPicker(KyeFile, true);
                LoadLevelInput.gameObject.SetActive(false);
            }
        }
    }

    public Dictionary<string, string> Get23(string[] levels)
    {
        int i = 0;
        string thisLevel = "";

        Dictionary<string, string> LoadKyeFile = new Dictionary<string, string>();

        foreach (string levelLine in levels)
        {
            thisLevel = thisLevel + "\n" + levelLine;
            i++;

            if (i == 23)
            {
                thisLevel = thisLevel.Substring(1);

                var b64 = Common.Base64Encode(thisLevel);
                var lName = GetLevelName(b64);
                if (LoadKyeFile.ContainsKey(lName))
                {
                    Debug.Log("Existing Key: " + lName);
                    lName = lName + "_" + UnityEngine.Random.Range(1000, 9999);
                }
                LoadKyeFile.Add(lName.ToLower(), b64);
                thisLevel = "";
                i = 0;
            }
        }

        return LoadKyeFile;
    }

    public void LoadLevelWithNoR(string levelName, bool setInputOnFail = false)
    {
        HideMainMenu();

        if (levelName.Trim() == "")
            levelName = "first";

        AppController.Instance.settings.LastLevelPlayed = levelName;
        levelName = levelName.ToLower();

        if (KyeFile.Count > 0 && KyeFile.ContainsKey(levelName))
        {
            CurrentLevelID = 0;
            _currentKyeLevelName = levelName;
            LoadLevel(KyeFile[levelName]);
            return;
        }
        else if (Levels.Classic.ContainsKey(levelName))
        {
            KyeAnalytics.LevelStarted(this, levelName);
            CurrentLevelID = LevelNameToID(levelName);
            LoadLevel(Levels.Classic[levelName]);
            return;
        }
        else
        {
            KyeAnalytics.LevelStarted(this, "first");
            levelName = "first";
            CurrentLevelID = LevelNameToID(levelName);
            LoadLevel(Levels.Classic[levelName]);
            return;
        }
    }

    public void PrevLevel()
    {
        HideMainMenu();

        if (levelEditor.IsPlaying)
        {
            ReturnToLevelEditorFromPlaytest();
            return;
        }

        CurrentLevelID--;
        CurrentLevelID = Mathf.Clamp(CurrentLevelID, 0, Levels.Classic.Count - 1);
        LoadLevel(CurrentLevelID);
    }

    public void NextLevel()
    {
        HideMainMenu();

        if (levelEditor.IsPlaying)
        {
            ReturnToLevelEditorFromPlaytest();
            return;
        }

        CurrentLevelID++;
        CurrentLevelID = Mathf.Clamp(CurrentLevelID, 0, Levels.Classic.Count - 1);
        LoadLevel(CurrentLevelID);
    }

    private void ReturnToLevelEditorFromPlaytest()
    {
        Time.timeScale = 1;
        levelEditor.IsPlaying = false;
        levelEditor.IsEditing = true;
        ShowEditor();
    }

    private int LevelNameToID(string levelName)
    {
        if(!Levels.Classic.ContainsKey(levelName))
            return 0;
        
        int id = 0;
        foreach(var level in Levels.Classic)
        {
            if(level.Key == levelName)
                return id;
            id++;
        }

        return 0;
    }

    private string LevelIDToName(int levelID)
    {
        int id = 0;
        foreach(var level in Levels.Classic)
        {
            if(id == levelID)
                return level.Key;
            id++;
        }

        return "";
    }

    private void CountDiamonds()
    {
        _diamonds = 0;
        foreach(var item in Items)
        {
            if(item == null)
                continue;
            
            if(item.IsDiamond)
                _diamonds++;
        }
    }

    public void PrepInput()
    {
        if(LoadLevelInput.text == "No such level...")
            LoadLevelInput.text = "";
    }

    public void LoadLevelFromMenu()
    {
        var attempt = LoadLevelInput.text.ToLower();
        
        if(!Levels.Classic.ContainsKey(attempt.ToLower()))
        {
            LoadLevelInput.text = "";
            return;
        }

        LoadLevelWithNoR(attempt, true);
    }

    public void LoadLevel(int levelID)
    {
        LoadLevelWithNoR(LevelIDToName(levelID));
    }

    private void LoadLevel(string level)
    {
        try
        {
            _currentLevelFilePath = "";
            EraseLevel();
            InsertLevel(level);
            _currentLevelB64 = level;
            AppController.Instance.Save();
            _canStartMoving = true;
            Time.timeScale = 1;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
            HasError();
        }
    }

    public void EraseLevel()
    {
        foreach (Transform item in ItemContainer.transform)
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }

        Items = new Item[30, 20];
        _lives = _startLives;
        _itemCount = 0;
        
        _autoFollowMouse = false;
        Destroy(_autoFollowGhost);
        
        Destroy(Kye);
        Kye = null;
    }

    public void InsertLevel(string level, bool isEditor = false)
    {
        ItemContainer.gameObject.SetActive(true);

        level = Common.Base64Decode(level);

        string[] lines = level.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        );

        //CurrentLevelKey = Common.LevelNameToKey(lines[0]);

        //CurrentLevelID = LevelNameToID(lines[0]);
        CurrentLevelName = lines[0].Trim();
        CurrentLevelNotes = lines[1].Trim();
        CurrentLevelFinishMessage = lines[2].Trim();

        for(int y = 19; y >= 0; y--)
        {
            for(int x = 0; x <= 29; x++)
            {
                try
                {
                    string itemChar = lines[3 + y][x].ToString();
                    var itemPrefab = Common.GetPrefabFromItemChar(itemChar);

                    if(itemPrefab != null)
                    {
                        var item = Instantiate(itemPrefab, new Vector3(x, 19 - y, 0), itemPrefab.transform.rotation);
                        item.transform.SetParent(ItemContainer);
                        
                        NewItem(item.GetComponent<Item>());

                        if(itemChar == "K")
                        {
                            Kye = item.GetComponent<Item>();
                            StartPoint = item.transform.position;
                        }
                    }
                }
                catch(Exception ex)
                {
                    Debug.Log("[InsertLevel] " + ex.ToString());
                }
            }
        }

        if(Kye == null)
        {
            Debug.Log("NO KYE !!!");
            
            var itemPrefab = Common.GetPrefabFromItemChar("K");

            var three = Common.GetItem(3, 3);
            if(three != null)
            {
                Debug.Log("Item at 3x3: " + three.name);
                Common.DestroyItem(three, false);
            }

            var item = Instantiate(itemPrefab, new Vector3(3, 3, 0), itemPrefab.transform.rotation);
            item.transform.SetParent(ItemContainer);
            NewItem(item.GetComponent<Item>());
            Kye = item.GetComponent<Item>();
            StartPoint = item.transform.position;
            Debug.Log(Kye);
        }
        
        Time.timeScale = 1;
        _timeSinceLevelLoaded = 0;
        GamePanel.SetActive(true);
        ResetLevel();
        UpdateUI();
        
        UpdatePauseLevelNavigationButtons();
    }

    private void UpdatePauseLevelNavigationButtons()
    {
        bool fromEditorPlaytest = levelEditor.IsPlaying;

        PauseNextLevel.gameObject.SetActive(!fromEditorPlaytest);

        var pausePreviousLabel = PausePreviousLevel.GetComponentInChildren<TMPro.TMP_Text>();

        if (fromEditorPlaytest)
        {
            if (pausePreviousLabel != null)
                pausePreviousLabel.text = "Level Editor";
            PausePreviousLevel.interactable = true;
        }
        else if (_currentLevelFilePath != "")
        {
            if (pausePreviousLabel != null)
                pausePreviousLabel.text = "Previous";
            PausePreviousLevel.interactable = false;
            PauseNextLevel.interactable = false;
        }
        else
        {
            if (pausePreviousLabel != null)
                pausePreviousLabel.text = "Previous";

            if (CurrentLevelName.ToLower() == "first")
                PausePreviousLevel.interactable = false;
            else
                PausePreviousLevel.interactable = true;

            if (Common.IsNextLevelUnlocked(CurrentLevelName.ToLower()))
                PauseNextLevel.interactable = true;
            else
                PauseNextLevel.interactable = false;
        }
    }

    public void NewItem(Item item)
    {
        item.x = (int)item.transform.position.x;
        item.y = (int)item.transform.position.y;
        item.Thinker = _itemCount;
        _itemCount++;
    }

    public void RestartLevel()
    {
        try
        {
            HideMainMenu();

            if (levelEditor.IsPlaying && !string.IsNullOrEmpty(_editorPlaytestLevel))
            {
                // Is level editor
                LoadLevelWithString(_editorPlaytestLevel);
                return;
            }

            if (_currentLevelFilePath != "")
            {
                // Is .kye file
                string kyeFilePath = _currentLevelFilePath;
                LoadLevel(_currentLevelB64);
                _currentLevelFilePath = kyeFilePath;
                return;
            }

            KyeAnalytics.LevelRestarted(this, CurrentLevelName);
            LoadLevel(_currentLevelB64);
            return;

            string levelToLoad = _currentKyeLevelName;
            if (string.IsNullOrEmpty(levelToLoad))
                levelToLoad = AppController.Instance.settings.LastLevelPlayed;

            LoadLevelWithNoR(levelToLoad);
        }
        catch(Exception ex)
        {
            Debug.Log(ex.ToString());
            HasError();
        }
    }

    public void HasError()
    {
        ShowMainMenu();
    }

    // not a restart of the level (things back in place), but a reset (kye back to start)
    private void ResetLevel()
    {
        var spawnPos = FindRespawnSpot(new Vector2(StartPoint.x, StartPoint.y));

        if (spawnPos == Vector2.negativeInfinity)
        {
            RestartLevel();
            return;
        }

        _canStartMoving = false;
        CountDiamonds();
        Common.MoveItem(spawnPos.x, spawnPos.y, Kye);

        if(GamePanel.activeSelf)
        {
            _canStartMoving = true;
        }
    }
    
    public Vector2 FindRespawnSpot(Vector2 spawnPos)
    {
        if(Common.IsEmpty(spawnPos.x, spawnPos.y))
            return spawnPos;
        if(Common.GetItem(spawnPos.x, spawnPos.y).IsKye)
            return spawnPos;
        
        for (int r = 0; r < 40; r++)
        {
            // Number of tries grows with the radius
            int tries = 4 * r + 1;

            for (int t = 0; t < tries; t++)
            {
                float x, y;

                if (r == 0)
                {
                    x = spawnPos.x;
                    y = spawnPos.y;
                }
                else
                {
                    // Random.Next's upper bound is exclusive, so +1 to include r
                    x = spawnPos.x + random.Next(-r, r + 1);
                    y = spawnPos.y + random.Next(-r, r + 1);
                }

                if (Common.IsEmpty(x, y))
                {
                    return new Vector2(x, y);
                }
            }
        }
        
        return Vector2.negativeInfinity;
    }

    private void StepToClickPosition()
    {
        Vector2Int direction = Vector2Int.zero;
        if(Kye.position != _inputClickPosition)
        {
            var spotItem = Common.GetItem(_inputClickPosition);
            if(spotItem == null || spotItem.IsDoor || spotItem.DestroyOnPlayerContact || spotItem.IsDiamond)
            {
                if(_autoFollowMouse)
                {
                    // Auto move, move the transparent Kye to _inputClickPosition
                    var ghostSpot = new Vector3(_inputClickPosition.x, _inputClickPosition.y, 0);
                    if(_autoFollowGhost == null)
                    {
                        _autoFollowGhost = Instantiate(KyeGhost, ghostSpot, KyeGhost.transform.rotation);
                        Destroy(_autoFollowGhost.GetComponent<FadeAndDestroy>());
                    }
                    else
                        _autoFollowGhost.transform.position = ghostSpot;
                }
                else
                {
                    // Manual click, put a ghost Kye there.
                    var ghostPosition = new Vector3(_inputClickPosition.x, _inputClickPosition.y, 0);
                    var kyeGhost = Instantiate(KyeGhost, ghostPosition, KyeGhost.transform.rotation);
                }
            }

            if(_autoFollowMouse && Time.time - _lastAutoMove < (_movementDelay * 3))
                return;

            direction = _inputClickPosition - Kye.position;

            if(direction.y != 0)
                direction.y = direction.y > 0 ? 1 : -1;
            if(direction.x != 0)
                direction.x = direction.x > 0 ? 1 : -1;

            GameController.Instance.inputManager.SetXY(direction);
            _lastAutoMove = Time.time;
        }
    }

    private void ProcessInput()
    {
        if (Mouse.current != null && !MainMenu.activeInHierarchy)
        {
            _lastInputClickPosition = _inputClickPosition;
            _inputClickPosition = Vector2Int.down;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                _autoFollowMouse = !_autoFollowMouse;

                if (!_autoFollowMouse && _autoFollowGhost != null)
                    Destroy(_autoFollowGhost);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame || _autoFollowMouse)
            {
                var clickPosition = actualCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                _inputClickPosition.x = Mathf.RoundToInt(clickPosition.x);
                _inputClickPosition.y = Mathf.RoundToInt(clickPosition.y);
            }

            if (_inputClickPosition != Vector2Int.down && Common.InBounds(_inputClickPosition))
                StepToClickPosition();
        }

        var input = inputManager.Get101();
        
        TrackInputChange(input);

        int moveX = input.x;
        int moveY = input.y;

        if(inputManager.AnyKeyDown && !LevelFailed.activeSelf && !LevelComplete.activeSelf && !PauseMenu.activeSelf)
            _canStartMoving = true;
        
        //if(inputManager.AnyKeyChange)
        //    _nextMoveTime = 0;

        // Set input to 0 if blocked to allow the other direction to move
        if(moveX != 0f && !Common.CanOccupyBy(moveX, 0, Kye))
            moveX = 0;
        if(moveY != 0f && !Common.CanOccupyBy(0, moveY, Kye))
            moveY = 0;

        _input.x = moveX;
        _input.y = moveY;
    }

    void Update()
    {
        if(levelEditor.IsEditing)
            return;

        if(MainMenu.activeSelf)
            return;
        
        if(MessageContainer.activeSelf)
            return;

        CheckInputForGameStuff();

        if(!GamePanel.activeSelf)
        {
            Tick();
            return;
        }

        _timeSinceLevelLoaded += Time.deltaTime;

        TickKye();
        Tick();

        if(_diamonds == 0 && _canStartMoving)
            FinishLevel();
    }

    private void CheckInputForGameStuff()
    {
        float scrollY = GameController.Instance._input.y;

        if(Mouse.current != null)
        {
            var mouseScrollY = Mouse.current.scroll.ReadValue().y;
            if(mouseScrollY != 0)
                scrollY = mouseScrollY;
        }

        if(scrollY != 0)
        {
            scrollY *= -0.01f;

            if (LevelPickerPanel.activeSelf)
            {
                LevelPickerScrollRect.Scroll(scrollY);
            }
        }
    }

    private bool CheckKyeForBeasts()
    {
        if(Kye == null)
            return false;

        if (AppController.Instance.settings.GodMode)
            return false;
        
        var left = Common.GetItem(Kye.x - 1, Kye.y);
        var right = Common.GetItem(Kye.x + 1, Kye.y);
        var down = Common.GetItem(Kye.x, Kye.y - 1);
        var up = Common.GetItem(Kye.x, Kye.y + 1);

        if(
            (left && left.KillsPlayer && !left.IsStuckToSticker) || 
            (right && right.KillsPlayer && !right.IsStuckToSticker) || 
            (up && up.KillsPlayer && !up.IsStuckToSticker) || 
            (down && down.KillsPlayer && !down.IsStuckToSticker))
        {
            KillPlayer();
            return true;
        }

        return false;
    }

    private void TickKye()
    {
        bool canMove = true;

        Vector2 prevPos = new Vector2(Kye.x, Kye.y);

        ProcessInput();

        if(!_canStartMoving)
            return;

        if(CheckKyeForBeasts())
            return;

        if(_input == Vector2.zero)
            return;

        if(Time.time < _nextMoveTime)
            return;
        
        int x = (int)Kye.x + (int)_input.x;
        int y = (int)Kye.y + (int)_input.y;

        var item = Common.GetItem(x, y);

        ProcessItem(item, ref canMove);

        if(canMove)
        {
            Common.MoveItem(x, y, Kye);

            float thisDelay = _applyInitialDelay ? _movementDelayInitial : _movementDelay;
            _applyInitialDelay = false;
            _nextMoveTime = Time.time + thisDelay;

            if(CheckKyeForBeasts())
                return;

            Vector2 currentPos = new Vector2(Kye.x, Kye.y);
            if(currentPos != prevPos)
                Common.StickersFollowKye(currentPos);
        }
    }

    private void Tick()
    {
        if(!_canStartMoving)
            return;
        
        var Thinkers = ItemContainer.GetComponentsInChildren<Item>();

        for(int thinker = 0; thinker <= _itemCount; thinker++)
        {
            foreach (Item item in Thinkers)
            {
                if(item == null) continue;                
                if(item.Thinker != thinker) continue;
                item.Tick();
            }
        }
    }

    private void FinishLevel()
    {
        KyeAnalytics.LevelComplete(this, CurrentLevelName);
        AudioManager.Instance.PlayLevelComplete();
        
        _canStartMoving = false;

        if (!levelEditor.IsPlaying && _currentLevelFilePath == "")
        {
            Common.UpdateUnlockedLevel(CurrentLevelName);

            bool _isNewRecord = false;
            string key = CurrentLevelName.Trim().ToLower();
            var s = AppController.Instance.settings;
            if (Levels.Classic.ContainsKey(key) && !s.GodMode && !s.UnlimitedLives)
                _isNewRecord = s.SubmitLevelTime(key, _timeSinceLevelLoaded);

            if (_isNewRecord)
            {
                // todo: let player know they have a new record
            }
        }

        AppController.Instance.Save();
        var message = LevelComplete.transform.Find("Message").GetComponent<TMPro.TMP_Text>();
        message.text = CurrentLevelFinishMessage;

        var levelCompleteLabel = LevelCompleteButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (levelCompleteLabel != null)
            levelCompleteLabel.text = levelEditor.IsPlaying ? "Level Editor" : "Next";

        LevelComplete.SetActive(true);
        EventSystem.current.SetSelectedGameObject(LevelCompleteButton);
        Time.timeScale = 0;
    }

    private void ProcessItem(Item item, ref bool canMove)
    {
        if(item == null)
            return;
        
        if(item.IsDiamond)
        {
            AudioManager.Instance.PlayDiamondPickup();
            _diamonds--;
            Common.DestroyItem(item, false);
            UpdateUI();
            return;
        }

        if(item.IsSploder)
        {
            item._sploder.Tock();
        }

        if((item.KillsPlayer || (item.IsBlackhole && item._blackhole._munches <= 0)) && !AppController.Instance.settings.GodMode)
        {
            if(item.IsBlackhole)
            {
                item.GetComponent<Blackhole>().Consume(Kye);
            }
            
            KillPlayer();
            canMove = false;
            return;
        }
        if(item.DestroyOnPlayerContact)
        {
            AudioManager.Instance.PlayPop();
            Common.DestroyItem(item, false);
            return;
        }
        if(item.IsDoor)
        {
            canMove = item.Direction == _input;
            return;
        }
        
        if(item.IsPushable && !item.IsBlackhole)
        {
            Vector2 spot = (Vector2)item.transform.position + _input;
            if(!Common.InBounds(spot.x, spot.y))
            {
                canMove = false;
                return;
            }

            var itemAtSpot = Common.GetItem(spot);
            if (itemAtSpot == null)
            {
                item.Move((int)_input.x, (int)_input.y, true);
                item.IsStuckToSticker = false;
            }
            else if (itemAtSpot.IsBlackhole)
            {
                itemAtSpot.GetComponent<Blackhole>().Consume(item);
                return;
            }
            else
                canMove = false;
        }
        else
        {
            canMove = false;
        }
    }

    public void Kill(string message = "Beware of beasts and blackholes!")
    {
        KyeAnalytics.LevelFailed(this, CurrentLevelName);
        _lives = 0;
        _canStartMoving = false;
        LevelFailed.transform.Find("Reason").GetComponent<TMPro.TMP_Text>().text = message;
        LevelFailed.SetActive(true);
        EventSystem.current.SetSelectedGameObject(LevelFailedButton);
        Time.timeScale = 0;
    }

    public void KillPlayer(string message = "You ran out of lives!")
    {
        if(!AppController.Instance.settings.UnlimitedLives)
            _lives--;
        
        AudioManager.Instance.PlayPlayerDeath();
        
        if(_lives > 0)
        {
            var ghostPosition = new Vector3(Kye.x, Kye.y, 0);
            var kyeGhost = Instantiate(KyeGhost, ghostPosition, KyeGhost.transform.rotation);

            UpdateUI();

            //if(Common.IsEmpty(StartPoint.x, StartPoint.y) || Common.GetItem(StartPoint.x, StartPoint.y).IsKye)
            //{
                ResetLevel();
            //}
            //else
            //{
            //    Debug.Log("handle this killplayer() respawn not empty");
            //    RestartLevel();
            //}
        }
        else
        {
            UpdateUI();
            Kill(message);
        }
    }

    private void UpdateUI()
    {
        Diamonds.text = _diamonds.ToString();
        LevelName.text = CurrentLevelName;
        LevelNotes.text = CurrentLevelNotes;

        if(_lives == 3)
        {
            Life1.color = Common.ChangeAlpha(Life1.color, 1f);
            Life2.color = Common.ChangeAlpha(Life1.color, 1f);
            Life3.color = Common.ChangeAlpha(Life1.color, 1f);
        }
        else if(_lives == 2)
        {
            Life1.color = Common.ChangeAlpha(Life1.color, 1f);
            Life2.color = Common.ChangeAlpha(Life1.color, 1f);
            Life3.color = Common.ChangeAlpha(Life1.color, 0.2f);
        }
        else if(_lives == 1)
        {
            Life1.color = Common.ChangeAlpha(Life1.color, 1f);
            Life2.color = Common.ChangeAlpha(Life1.color, 0.2f);
            Life3.color = Common.ChangeAlpha(Life1.color, 0.2f);
        }
        else if (_lives == 0)
        {
            Life1.color = Common.ChangeAlpha(Life1.color, 0.2f);
            Life2.color = Common.ChangeAlpha(Life1.color, 0.2f);
            Life3.color = Common.ChangeAlpha(Life1.color, 0.2f);
        }
    }
    
    private void TrackInputChange(Vector2Int raw)
    {
        if (raw != Vector2Int.zero)
        {
            if (inputManager.AnyKeyDown)
            {
                // A key went down this frame: step right away in the new direction.
                _nextMoveTime = 0;
                _applyInitialDelay = true;
            }
            else if (raw != _lastRawInput)
            {
                // A newer key was released and an older held key took over again.
                // Resume repeating quickly instead of waiting out the pending timer.
                _nextMoveTime = Mathf.Min(_nextMoveTime, Time.time + _movementDelay);
            }
        }

        _lastRawInput = raw;
    }
}
