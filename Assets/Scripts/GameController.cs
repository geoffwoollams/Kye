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

    public Camera actualCamera;

    public Canvas MainCanvas;

    public GameObject GamePanel, MainMenu, PauseMenu, EditorPanel, LevelPickerPanel, LevelFailed, LevelComplete;
    public GameObject PlayButton, ResumeButton, LevelFailedButton, LevelCompleteButton, EditorMenuButton, LevelsScreenPrimaryButton, LevelsScreenOpenButton;
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

    private bool _autoFollowMouse;
    private float _lastAutoMove;
    private GameObject _autoFollowGhost;

    [System.NonSerialized] public Item[,] Items = new Item[30, 20];

    private Vector2 _input;
    public Vector2Int _inputClickPosition;
    private int _startLives = 3;
    private int _lives;
    private int _diamonds;
    private float _nextMoveTime;
    private float _movementDelayInitial = 0.4f;
    private float _movementDelay = 0.05f;
    public bool _canStartMoving;
    private int _itemCount;
    private bool _levelHasDiamonds;
    private readonly List<Item> _thinkers = new List<Item>(256);

    public int CurrentLevelID = 0;
    public string CurrentLevelName = "";
    public string CurrentLevelNotes = "";
    public string CurrentLevelFinishMessage = "";

    private float _timeSinceLevelLoaded;
    
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
        Levels.CheckForDuplicates();
#endif
    }

    public void OpenKyeWebsite()
    {
        Application.OpenURL("https://playkye.com");
    }

    void Start()
    {
        LoadLevelPicker();
        ShowMainMenu();
        
        if (Kye == null)
            Kye = GameObject.Find("kye").GetComponent<Item>();
        
        ResetLevel();
        _canStartMoving = true;

        UpdateUI();
        
#if UNITY_EDITOR && false
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

    private static readonly Dictionary<string, string[]> LevelHeaderCache = new Dictionary<string, string[]>();

    private static string[] GetLevelHeader(string b64)
    {
        if (!LevelHeaderCache.TryGetValue(b64, out var header))
        {
            string[] lines = Common.Base64Decode(b64).Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );
            header = new[] { lines[0].Trim(), lines[1].Trim() };
            LevelHeaderCache[b64] = header;
        }

        return header;
    }

    // One row of the level picker. Everything needed to filter and draw the row, so no level has to be decoded.
    private readonly struct PickerRow
    {
        public readonly string Key, Title, TitleLower, Hint, B64;

        public PickerRow(string key, string title, string titleLower, string hint, string b64)
        {
            Key = key;
            Title = title;
            TitleLower = titleLower;
            Hint = hint;
            B64 = b64;
        }
    }

    private static List<PickerRow> _classicRows;

    private static List<PickerRow> ClassicRows()
    {
        if (_classicRows == null)
        {
            var rows = new List<PickerRow>(Levels.Count);
            for (int i = 0; i < Levels.Count; i++)
                rows.Add(new PickerRow(Levels.NameAt(i), Levels.TitleAt(i), Levels.TitleLowerAt(i), Levels.HintAt(i), Levels.GetB64At(i)));
            _classicRows = rows;
        }
        return _classicRows;
    }

    private static List<PickerRow> RowsFromDictionary(Dictionary<string, string> levels)
    {
        var rows = new List<PickerRow>(levels.Count);
        foreach (var entry in levels)
        {
            var header = GetLevelHeader(entry.Value);
            rows.Add(new PickerRow(entry.Key, header[0], header[0].ToLower(), header[1], entry.Value));
        }
        return rows;
    }

    // A level picker row that is created once and reused. The cached fields remember what the row
    // currently shows so a refresh only touches what actually changed (UI changes are what cost time).
    private sealed class PickerView
    {
        public GameObject Go;
        public RectTransform Rect;
        public LevelItem Item;
        public Button ItemButton, EditButton;
        public TMP_Text NameText, NotesText, NumberText;
        public GameObject NumberObj, LockObj;

        public string BoundB64;         // level the title/hint/name are set for
        public int Number = -1;
        public float Y = float.NaN;
        public float BestTime = -1f;    // 0 = none shown
        public int UnlockedState = -1;  // -1 unknown, 0 locked, 1 unlocked
    }

    private readonly List<PickerView> _pickerViews = new List<PickerView>();

    private PickerView CreatePickerView()
    {
        var go = Instantiate(LevelPickerItem);
        go.transform.SetParent(LevelPickerContainer.transform, false);

        var view = new PickerView
        {
            Go = go,
            Rect = go.GetComponent<RectTransform>(),
            Item = go.GetComponent<LevelItem>(),
            ItemButton = go.GetComponent<Button>(),
            EditButton = go.transform.Find("Edit").GetComponent<Button>(),
            NameText = go.transform.Find("LevelName").GetComponent<TMP_Text>(),
            NotesText = go.transform.Find("LevelNotes").GetComponent<TMP_Text>(),
            NumberObj = go.transform.Find("Number").gameObject,
            LockObj = go.transform.Find("Lock").gameObject,
        };
        view.NumberText = view.NumberObj.transform.Find("LevelNumber").GetComponent<TMP_Text>();

        // Added once. A locked row has a non-interactable button and a hidden Edit button.
        view.ItemButton.onClick.AddListener(LevelPickerClick);
        view.EditButton.onClick.AddListener(LevelPickerEditClick);
        return view;
    }

    private static void SetPickerViewActive(PickerView view, bool active)
    {
        if (view.Go.activeSelf != active)
            view.Go.SetActive(active);
    }

    // Sets everything about a row: the parts that depend only on the level (only when the row is
    // showing a different level), then position, number, best time and lock state (only if changed).
    private void UpdatePickerView(PickerView view, PickerRow row, float y, int ln, bool unlocked)
    {
        if (view.BoundB64 != row.B64)
        {
            view.BoundB64 = row.B64;
            view.Item.b64 = row.B64;
            view.NameText.text = row.Title;
            view.NotesText.text = row.Hint;
            view.Go.name = "LevelItem " + row.Title;
            view.BestTime = -1f; // force the best time below to be set again
        }

        if (view.Y != y)
        {
            view.Y = y;
            var pos = view.Rect.anchoredPosition;
            pos.y = y;
            view.Rect.anchoredPosition = pos;
        }

        if (view.Number != ln)
        {
            view.Number = ln;
            view.NumberText.text = ln.ToString();
        }

        float? bestTime = AppController.Instance.settings.GetBestTime(row.TitleLower);
        float best = bestTime != null && bestTime > 0 ? bestTime.Value : 0f;
        if (view.BestTime != best)
        {
            view.BestTime = best;
            view.Item.bestTimeText.gameObject.SetActive(best > 0);
            if (best > 0)
                view.Item.bestTimeText.text = Common.FormatTime(best);
        }

        int state = unlocked ? 1 : 0;
        if (view.UnlockedState != state)
        {
            view.UnlockedState = state;
            view.ItemButton.interactable = unlocked;
            view.NumberObj.SetActive(unlocked);
            view.LockObj.SetActive(!unlocked);
            view.EditButton.gameObject.SetActive(unlocked);
        }

        SetPickerViewActive(view, true);
    }

    public string GetLevelName(string b64)
    {
        return GetLevelHeader(b64)[0];
    }

    public void ClearLevelList()
    {
        LoadLevelInput.text = "";
        KyeFile = new Dictionary<string, string>();
        LoadLevelPicker();
    }

    public void LoadLevelPicker()
    {
        LoadLevelPicker(ClassicRows(), false);
        LoadLevelInput.gameObject.SetActive(true);
    }

    public void LoadLevelPicker(Dictionary<string, string> levels, bool unlockAll)
    {
        LoadLevelPicker(RowsFromDictionary(levels), unlockAll);
    }

    // Shows the given levels in the picker. The row objects are created once and reused, so opening
    // the picker or typing in the filter doesn't create or destroy anything; best times and lock state
    // are refreshed from the settings every call.
    private void LoadLevelPicker(List<PickerRow> rows, bool unlockAll)
    {
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

        while (_pickerViews.Count < rows.Count)
            _pickerViews.Add(CreatePickerView());

        int y = -5;
        int ln = 1;

        PickerView prev = null;
        var primaryButton = LevelsScreenPrimaryButton.GetComponent<Button>();
        Selectable selectOnUp = primaryButton;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var view = _pickerViews[i];

            if (filter != "" && !row.TitleLower.Contains(filter))
            {
                SetPickerViewActive(view, false);
                continue;
            }

            bool isUnlocked = unlockAll || Common.IsLevelUnlocked(row.Key);
            UpdatePickerView(view, row, y, ln, isUnlocked);
            y -= 55;
            ln++;

            // Locked rows are left out of the up/down/left/right links so navigation skips them.
            if (!isUnlocked)
            {
                var lockedNav = view.ItemButton.navigation;
                lockedNav.mode = Navigation.Mode.None;
                view.ItemButton.navigation = lockedNav;
                continue;
            }

            var itemButton = view.ItemButton;
            var editButton = view.EditButton;

            // Rows are reused, so every link is set again (a stale "down" link would point at an old neighbour).
            Navigation editNav = editButton.navigation;
            editNav.mode = Navigation.Mode.Explicit;
            editNav.selectOnUp = prev != null ? prev.EditButton : primaryButton;
            editNav.selectOnDown = null;
            editNav.selectOnLeft = itemButton;
            editButton.navigation = editNav;

            Navigation itemNav = itemButton.navigation;
            itemNav.mode = Navigation.Mode.Explicit;
            itemNav.selectOnUp = selectOnUp;
            itemNav.selectOnDown = null;
            itemNav.selectOnRight = editButton;
            itemButton.navigation = itemNav;

            if (prev == null)
            {
                var nav = primaryButton.navigation;
                nav.selectOnDown = itemButton;
                primaryButton.navigation = nav;

                var buttonOpen = LevelsScreenOpenButton.GetComponent<Button>();
                nav = buttonOpen.navigation;
                nav.selectOnDown = itemButton;
                buttonOpen.navigation = nav;

                var searchInput = LoadLevelInput.GetComponent<TMP_InputField>();
                nav = searchInput.navigation;
                nav.selectOnDown = itemButton;
                searchInput.navigation = nav;
            }
            else
            {
                Navigation prevEditNav = prev.EditButton.navigation;
                prevEditNav.selectOnDown = editButton;
                prev.EditButton.navigation = prevEditNav;

                Navigation prevNav = prev.ItemButton.navigation;
                prevNav.mode = Navigation.Mode.Explicit;
                prevNav.selectOnDown = itemButton;
                prev.ItemButton.navigation = prevNav;
            }

            prev = view;
            selectOnUp = itemButton;
        }

        for (int i = rows.Count; i < _pickerViews.Count; i++)
            SetPickerViewActive(_pickerViews[i], false);

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
        AppController.Instance.settings.UnlimitedLives = tUnlimitedLives.isOn;
        AppController.Instance.settings.UnlockAllLevels = tUnlockAllLevels.isOn;
        AppController.Instance.settings.GodMode = tGodMode.isOn;
        AppController.Instance.Save();
    }

    public void ShowMainMenu()
    {
#if UNITY_EDITOR
        DevBot.NotifyMainMenu();
        DevPanel.Hide();
#endif
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
#if UNITY_EDITOR
        DevPanel.Hide();
#endif
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
        LoadLevelInput.text = "";
        LevelPickerContainerContainer.SetActive(true);
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
        LoadLevelWithNoR(AppController.Instance.settings.LastLevelPlayed);
        KyeAnalytics.enabled = true;
    }

    public void LoadRandomLevel()
    {
        List<string> keyList = Levels.Names.Where(Common.IsLevelUnlocked).ToList();
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
        
        FileBrowser.ShowLoadDialog(
            FileBrowserOnSuccess,
            FileBrowserOnCancel,
            FileBrowser.PickMode.Files, false, Common.GetDefaultFilePath()
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
                    lName = lName + "_" + UnityEngine.Random.Range(1000, 9999);
                }
                LoadKyeFile.Add(lName.ToLower(), b64);
                thisLevel = "";
                i = 0;
            }
        }

        return LoadKyeFile;
    }

    public void LoadLevelWithNoR(string levelName)
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
        else if (Levels.Contains(levelName))
        {
            KyeAnalytics.LevelStarted(this, levelName);
            CurrentLevelID = LevelNameToID(levelName);
            LoadLevel(Levels.GetB64(levelName));
            return;
        }
        else
        {
            KyeAnalytics.LevelStarted(this, "first");
            levelName = "first";
            CurrentLevelID = LevelNameToID(levelName);
            LoadLevel(Levels.GetB64(levelName));
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
        CurrentLevelID = Mathf.Clamp(CurrentLevelID, 0, Levels.Count - 1);
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
        CurrentLevelID = Mathf.Clamp(CurrentLevelID, 0, Levels.Count - 1);
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
        return Mathf.Max(0, Levels.IndexOf(levelName));
    }

    private string LevelIDToName(int levelID)
    {
        return Levels.NameAt(levelID);
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
        
        if(!Levels.Contains(attempt))
        {
            LoadLevelInput.text = "";
            return;
        }

        LoadLevelWithNoR(attempt);
    }

    public void LoadLevel(int levelID)
    {
        LoadLevelWithNoR(LevelIDToName(levelID));
    }

    private void LoadLevel(string level)
    {
#if UNITY_EDITOR
        DevBot.NotifyLevelLoading();
#endif
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

                    // Every item on the border must be a wall; anything else becomes wall5.
                    bool isBorder = x == 0 || x == 29 || y == 0 || y == 19;
                    if(itemPrefab != null && isBorder && !Common.IsWallCode(itemChar))
                    {
                        itemChar = "5";
                        itemPrefab = Common.GetPrefabFromItemChar(itemChar);
                    }

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
                Common.DestroyItem(three, false);
            }

            var item = Instantiate(itemPrefab, new Vector3(3, 3, 0), itemPrefab.transform.rotation);
            item.transform.SetParent(ItemContainer);
            NewItem(item.GetComponent<Item>());
            Kye = item.GetComponent<Item>();
            StartPoint = item.transform.position;
        }
        
        Time.timeScale = 1;
        _timeSinceLevelLoaded = 0;
        GamePanel.SetActive(true);
        ResetLevel();
        _levelHasDiamonds = _diamonds > 0;
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
                LoadLevelWithString(_editorPlaytestLevel);
                return;
            }

            if (_currentLevelFilePath != "")
            {
                string kyeFilePath = _currentLevelFilePath;
                LoadLevel(_currentLevelB64);
                _currentLevelFilePath = kyeFilePath;
                return;
            }

            KyeAnalytics.LevelRestarted(this, CurrentLevelName);
            LoadLevel(_currentLevelB64);
            return;
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

        if (float.IsInfinity(spawnPos.x))
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

            inputManager.SetXY(direction);
            _lastAutoMove = Time.time;
        }
    }

    private void ProcessInput()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            _inputClickPosition = Vector2Int.down;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _autoFollowMouse = !_autoFollowMouse;

                if (!_autoFollowMouse && _autoFollowGhost != null)
                    Destroy(_autoFollowGhost);
            }

            if (mouse.leftButton.wasPressedThisFrame || _autoFollowMouse)
            {
                var clickPosition = actualCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                _inputClickPosition.x = Mathf.RoundToInt(clickPosition.x);
                _inputClickPosition.y = Mathf.RoundToInt(clickPosition.y);
            }

            if (_inputClickPosition != Vector2Int.down && Common.InBounds(_inputClickPosition))
                StepToClickPosition();
        }

#if UNITY_EDITOR
        if (DevBot.Active)
            inputManager.SetXY(DevBot.Instance.NextInput());
#endif

        var input = inputManager.Get101();
        
        TrackInputChange(input);

        int moveX = input.x;
        int moveY = input.y;

        if(inputManager.AnyKeyDown && !LevelFailed.activeSelf && !LevelComplete.activeSelf && !PauseMenu.activeSelf)
            _canStartMoving = true;

        // Set input to 0 if blocked to allow the other direction to move
        if(moveX != 0f && !Common.CanOccupyBy(moveX, 0, Kye))
            moveX = 0;
        if(moveY != 0f && !Common.CanOccupyBy(0, moveY, Kye))
            moveY = 0;

        _input.x = moveX;
        _input.y = moveY;
    }

    private readonly InputFieldNavigator _searchNavigator = new InputFieldNavigator();
    private TMP_InputField[] _searchFields;

    // The focused search field swallows left/down, so force the move to its navigation target.
    // See InputFieldNavigator.
    void LateUpdate()
    {
        if (!LevelPickerPanel.activeSelf)
            return;

        if (_searchFields == null)
            _searchFields = new[] { LoadLevelInput };

        _searchNavigator.Tick(_searchFields, InputFieldNavigator.Dir.Left, InputFieldNavigator.Dir.Down);
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

        // Levels that start with no diamonds never finish.
        if(_levelHasDiamonds && _diamonds == 0 && _canStartMoving)
            FinishLevel();
    }

    private void CheckInputForGameStuff()
    {
        if(!LevelPickerPanel.activeSelf)
            return;

        var mouse = Mouse.current;
        if(mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if(scrollY != 0)
            LevelPickerScrollRect.Scroll(scrollY * -0.01f);
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

        // Items think in Thinker order, and in scene order within the same Thinker. Changing this changes how levels play.
        ItemContainer.GetComponentsInChildren(_thinkers);

        // Stable insertion sort; the list is almost sorted already.
        for(int i = 1; i < _thinkers.Count; i++)
        {
            var current = _thinkers[i];
            int thinker = current.Thinker;
            int j = i - 1;
            while(j >= 0 && _thinkers[j].Thinker > thinker)
            {
                _thinkers[j + 1] = _thinkers[j];
                j--;
            }
            _thinkers[j + 1] = current;
        }

        for(int i = 0; i < _thinkers.Count; i++)
        {
            var item = _thinkers[i];
            if(item == null) continue;
            item.Tick();
        }
    }

    private void FinishLevel()
    {
        KyeAnalytics.LevelComplete(this, CurrentLevelName);
        AudioManager.Instance.PlayLevelComplete();
        
        _canStartMoving = false;

        bool devBot = false;
#if UNITY_EDITOR
        devBot = DevBot.Active; // the Dev bot never records best times
#endif
        if (!levelEditor.IsPlaying && _currentLevelFilePath == "" && !devBot)
        {
            string key = CurrentLevelName.Trim().ToLower();
            var s = AppController.Instance.settings;
            if (Levels.Contains(key) && !s.GodMode && !s.UnlimitedLives)
                s.SubmitLevelTime(key, _timeSinceLevelLoaded);
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
            UpdateDiamondsUI();
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

            UpdateDiamondsUI();
            UpdateLivesUI();

            ResetLevel();
        }
        else
        {
            UpdateDiamondsUI();
            UpdateLivesUI();
            Kill(message);
        }
    }

    private void UpdateUI()
    {
        UpdateDiamondsUI();
        LevelName.text = CurrentLevelName;
        LevelNotes.text = CurrentLevelNotes;
        UpdateLivesUI();
    }

    private void UpdateDiamondsUI()
    {
        Diamonds.text = _diamonds.ToString();
    }

    private void UpdateLivesUI()
    {
        if(_lives < 0 || _lives > 3)
            return;

        var color = Life1.color;
        Life1.color = Common.ChangeAlpha(color, _lives >= 1 ? 1f : 0.2f);
        Life2.color = Common.ChangeAlpha(color, _lives >= 2 ? 1f : 0.2f);
        Life3.color = Common.ChangeAlpha(color, _lives >= 3 ? 1f : 0.2f);
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
