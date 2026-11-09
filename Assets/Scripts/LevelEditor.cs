using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
    public GameController gameController;
    public Image _mouseImage;
    public RectTransform Bounds;
    public EditorGridItem editorGridItemPrefab;
    public Transform editorGridContainer;

    public bool IsEditing, HasChanges, IsPlaying, IsInMenu;

    public string _currentItem = "kye";

    public Sprite _newSprite;

    public EditorGridItem _kyeGridItem;

    public EditorGridItem _hoveredGridItem;

    public Vector3 m_GridSize = new(2.5f, 2.5f, 2.5f);

    public Sprite gridBackgroundSprite;

    public GameObject EditorMenu;

    List<EditorGridItem> GridItems = new List<EditorGridItem>();
    Dictionary<(int, int), EditorGridItem> GridItemLookup = new Dictionary<(int, int), EditorGridItem>();

    public TMPro.TMP_InputField LevelName, LevelHint, LevelFinish;
    public TMPro.TMP_Text CurrentItemText, DiamondCount;

    public LevelEditorItemButton SelectedButton;

    private int _cursorX = 15, _cursorY = 18;
    public float CursorMoveDelay = 0.12f;
    public float CursorMoveDelayInitial = 0.4f;
    private float _nextCursorMoveTime;

    public bool SuppressMouseHover;
    private const float MouseMoveThresholdSqr = 0.01f;

    private string _loadedLevelB64;

    public GameObject UnsavedChangesMessageContainer;
    public Button UnsavedChangesProceedButton;

    // --- .kye file tracking ---
    private const int LinesPerLevel = 23;
    private string _sourceFilePath;   // file the current level was loaded from / last saved to
    private string _sourceLevelName;  // name the level had in that file (survives renames)
    private string _lastOpenedPath;   // set when a file is opened, consumed by LoadLevel (covers the level picker)

    private Canvas _boundsCanvas;

    void Awake()
    {
        _newSprite = _mouseImage.sprite;

        // We handle overwrite/merge ourselves, so don't show SimpleFileBrowser's "file exists" prompt.
        FileBrowser.ShowFileOverwriteDialog = false;

        InitGridItems();
        NewLevel();
        HideMenu();
        Prep();

        _loadedLevelB64 = Common.Base64Encode(GetLevel());
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string Clean(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ");

    private static bool NamesMatch(string a, string b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private Camera GetBoundsCamera()
    {
        if (_boundsCanvas == null)
            _boundsCanvas = Bounds.GetComponentInParent<Canvas>();

        if (_boundsCanvas == null || _boundsCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return _boundsCanvas.worldCamera;
    }

    // ------------------------------------------------------------------
    // Open
    // ------------------------------------------------------------------

    public void ClickOpen()
    {
        IsInMenu = true;
        Cursor.visible = true;

        FileBrowser.SetFilters(true, new FileBrowser.Filter("Kye Levels", ".kye"));
        FileBrowser.SetDefaultFilter(".kye");

        FileBrowser.ShowLoadDialog(
            FileBrowserOnOpenSuccess,
            FileBrowserOnCancel,
            FileBrowser.PickMode.Files, false, Common.GetDefaultFilePath()
        );
    }

    public void FileBrowserOnOpenSuccess(string[] paths)
    {
        IsInMenu = false;
        if (paths == null || paths.Length == 0) return;

        string filePath = paths[0];
        if (!File.Exists(filePath)) return;

        try
        {
            var level = File.ReadAllLines(filePath);
            if (level.Length == 0)
                throw new Exception("File is empty.");

            if (!int.TryParse(level[0].Trim(), out int levelCount))
                levelCount = 1;

            _lastOpenedPath = filePath;
            level = level.Skip(1).ToArray();

            if (levelCount <= 1)
            {
                LoadLevel(string.Join("\n", level));
            }
            else
            {
                GameController.Instance.ShowLevelPicker();
                var kyeFile = GameController.Instance.Get23(level);
                GameController.Instance.LoadLevelPicker(kyeFile, true); // picker should end up calling LoadLevel
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            GameController.Instance.Message("Error", "Could not open level: " + ex.Message);
        }
    }

    public void FileBrowserOnCancel()
    {
        IsInMenu = false;
    }

    public void SetTextFields(string name, string hint, string message)
    {
        LevelName.text = name;
        LevelHint.text = hint;
        LevelFinish.text = message;
    }

    public void ShowK64()
    {
        GUIUtility.systemCopyBuffer = "K64_" + Common.Base64Encode(GetLevel()) + "_K64";
        GameController.Instance.Message("K64 Level Exported", "Your K64 level has been saved in your clipboard!");
    }

    // ------------------------------------------------------------------
    // Save (merge into existing .kye file)
    // ------------------------------------------------------------------

    public void ClickSave()
    {
        if (CountDiamonds() == 0)
        {
            GameController.Instance.Message("Error", "You must have at least 1 diamond!");
            return;
        }

        IsInMenu = true;
        Cursor.visible = true;

        FileBrowser.SetFilters(true, new FileBrowser.Filter("Kye Levels", ".kye"));
        FileBrowser.SetDefaultFilter(".kye");

        // Default back to the file we came from
        string initialPath = Common.GetDefaultFilePath();
        string initialName = Clean(LevelName.text).Trim() + ".kye";
        if (!string.IsNullOrEmpty(_sourceFilePath))
        {
            initialPath = Path.GetDirectoryName(_sourceFilePath);
            initialName = Path.GetFileName(_sourceFilePath);
        }

        FileBrowser.ShowSaveDialog(
            FileBrowserOnSaveSuccess,
            FileBrowserOnCancel,
            FileBrowser.PickMode.Files,
            false,
            initialPath,
            initialName
        );
    }

    public void FileBrowserOnSaveSuccess(string[] paths)
    {
        IsInMenu = false;
        HideMenu();
        if (paths == null || paths.Length == 0) return;

        string filePath = paths[0];
        if (!filePath.EndsWith(".kye", true, CultureInfo.CurrentCulture))
            filePath += ".kye";

        string level = GetLevel();

        try
        {
            string result = SaveLevelToFile(filePath, level);
            _loadedLevelB64 = Common.Base64Encode(level);
            GameController.Instance.Message("Level Saved", result);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            GameController.Instance.Message("Error", "Could not save level: " + ex.Message);
        }
    }

    /// <summary>
    /// Merges the level into the file. Matching order:
    /// 1) if saving to the file the level came from, the entry with the originally loaded name
    /// 2) an entry with the current name (trimmed, case-insensitive)
    /// 3) otherwise append.
    /// Writes a .bak of the original and goes through a temp file.
    /// </summary>
    private string SaveLevelToFile(string filePath, string level)
    {
        string[] newLevel = level.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Take(LinesPerLevel).ToArray();
        if (newLevel.Length != LinesPerLevel)
            throw new Exception("Level data is malformed (expected " + LinesPerLevel + " lines, got " + newLevel.Length + ").");

        var levels = new List<string[]>();

        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);

            int end = lines.Length;
            while (end > 1 && string.IsNullOrEmpty(lines[end - 1])) end--; // ignore trailing blank lines

            int count = Math.Max(0, (end - 1) / LinesPerLevel); // trust real content, not the header
            for (int i = 0; i < count; i++)
                levels.Add(lines.Skip(1 + i * LinesPerLevel).Take(LinesPerLevel).ToArray());

            File.Copy(filePath, filePath + ".bak", true);
        }

        int index = -1;
        if (_sourceLevelName != null && SamePath(_sourceFilePath, filePath))
            index = levels.FindIndex(l => NamesMatch(l[0], _sourceLevelName));
        if (index < 0)
            index = levels.FindIndex(l => NamesMatch(l[0], newLevel[0]));

        bool replaced = index >= 0;
        if (replaced)
        {
            levels[index] = newLevel;
        }
        else
        {
            levels.Add(newLevel);
            index = levels.Count - 1;
        }

        var sb = new StringBuilder();
        sb.Append(levels.Count).Append('\n');
        foreach (var l in levels)
            foreach (var line in l)
                sb.Append(line).Append('\n');

        string tmp = filePath + ".tmp";
        File.WriteAllText(tmp, sb.ToString());
        File.Copy(tmp, filePath, true);
        File.Delete(tmp);

        _sourceFilePath = filePath;
        _sourceLevelName = newLevel[0].Trim();

        string name = _sourceLevelName;
        return replaced
            ? $"Updated \"{name}\" (level {index + 1} of {levels.Count})."
            : $"Added \"{name}\" as level {levels.Count}.";
    }

    // ------------------------------------------------------------------
    // Play
    // ------------------------------------------------------------------

    public void ClickPlay()
    {
        if (CountDiamonds() == 0)
        {
            GameController.Instance.Message("Error", "You must have at least 1 diamond!");
            return;
        }

        IsEditing = false;
        IsPlaying = true;

        HideMenu();
        GameController.Instance.HideMainMenu();
        GameController.Instance.FrameUI.SetActive(false);
        GameController.Instance.FrameSprites.SetActive(true);
        GameController.Instance.LoadLevelWithString(GetLevel());
    }

    // ------------------------------------------------------------------
    // Level data
    // ------------------------------------------------------------------

    public string GetLevel()
    {
        var sb = new StringBuilder();
        sb.Append(Clean(LevelName.text)).Append('\n');
        sb.Append(Clean(LevelHint.text)).Append('\n');
        sb.Append(Clean(LevelFinish.text)).Append('\n');

        for (int y = 19; y >= 0; y--)
        {
            for (int x = 0; x <= 29; x++)
            {
                var item = GetGridItem(x, y);
                sb.Append(item != null ? item.GetCode() : " "); // " " assumed to be "clear"; adjust if not
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    public void ClearGrid()
    {
        foreach (EditorGridItem levelGridItem in GridItems)
        {
            levelGridItem.SetAs("clear");
        }
    }

    public void UnsavedChanges()
    {
        UnsavedChangesMessageContainer.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(UnsavedChangesProceedButton.gameObject);
    }

    public void UnsavedChangesProceed()
    {
        NewLevel();
        UnsavedChangesMessageContainer.SetActive(false);
    }

    public void UnsavedChangesCancel()
    {
        UnsavedChangesMessageContainer.SetActive(false);
    }

    public void NewLevelClick()
    {
#if !UNITY_WEBGL
        // On WebGL the Open and Save buttons are hidden, so changes can't be saved and the warning is pointless
        var currentLevelB64 = Common.Base64Encode(GetLevel());

        if (currentLevelB64 != _loadedLevelB64)
        {
            UnsavedChanges();
            return;
        }
#endif

        NewLevel();
    }

    public void NewLevel()
    {
        SetTextFields("MYLEVEL", "Good luck!", "Yay you completed the level!");

        foreach (EditorGridItem levelGridItem in GridItems)
        {
            if (levelGridItem.x == 0 || levelGridItem.x == 29 || levelGridItem.y == 0 || levelGridItem.y == 19)
            {
                levelGridItem.SetAs("wall5");
            }
            else if (levelGridItem.x == 15 && levelGridItem.y == 18)
            {
                levelGridItem.SetAs("kye");
            }
            else
            {
                levelGridItem.SetAs("clear");
            }
        }

        // A fresh level must not overwrite anything by accident
        _sourceFilePath = null;
        _sourceLevelName = null;
        _lastOpenedPath = null;

        _loadedLevelB64 = Common.Base64Encode(GetLevel());

        HideMenu();
    }

    public void LoadLevel(string level)
    {
        ClearGrid();

        string[] lines = level.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string At(int i) => i < lines.Length ? lines[i] : "";

        SetTextFields(At(0).Trim(), At(1).Trim(), At(2).Trim());

        foreach (EditorGridItem item in GridItems)
        {
            string row = At(3 + (19 - item.y));
            string itemChar = item.x < row.Length ? row[item.x].ToString() : " ";
            item.SetAs(Common.GetItemNameFromKyeCode(itemChar));
        }

        _sourceFilePath = _lastOpenedPath;
        _sourceLevelName = At(0).Trim();
        _loadedLevelB64 = Common.Base64Encode(GetLevel());

        HideMenu();
    }

    public void SetKye(EditorGridItem that)
    {
        if (_kyeGridItem != null)
        {
            _kyeGridItem._setSprite = gridBackgroundSprite;
            _kyeGridItem._setItem = "clear";
            _kyeGridItem._image.sprite = gridBackgroundSprite;
            _kyeGridItem._rect.localScale = Vector3.one;
        }
        _kyeGridItem = that;
    }

    void InitGridItems()
    {
        for (int y = 19; y >= 0; y--)
        {
            for (int x = 0; x <= 29; x++)
            {
                try
                {
                    var item = Instantiate(editorGridItemPrefab, new Vector3(x, 19 - y, 1), editorGridItemPrefab.transform.rotation);
                    item.transform.SetParent(editorGridContainer);
                    item.name = "EditorGridItem (" + x + "x" + y + ")";
                    item._editor = this;
                    item.SetBackground(gridBackgroundSprite);
                    item.x = x;
                    item.y = y;
                    GridItems.Add(item);
                    GridItemLookup[(x, y)] = item;
                }
                catch (Exception ex)
                {
                    Debug.Log("[InitGridItems] " + ex.ToString());
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Update / input
    // ------------------------------------------------------------------

    void Update()
    {
        if (!GameController.Instance.EditorPanel.activeSelf)
            return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool typing = IsTextFieldFocused();

        if (!IsInMenu && kb != null)
        {
            bool ctrl = kb.ctrlKey.isPressed;

            // Ctrl+S works even while typing in a text field; single-letter shortcuts do not.
#if !UNITY_WEBGL
            if (ctrl && kb.sKey.wasPressedThisFrame)
                ClickSave();
#endif

            if (!typing)
            {
                if (kb.pKey.wasPressedThisFrame) ClickPlay();
#if !UNITY_WEBGL
                if (kb.oKey.wasPressedThisFrame) ClickOpen();
#endif
                if (kb.kKey.wasPressedThisFrame) ShowK64();
            }
        }

        m_GridSize.x = Bounds.rect.width / 30f;
        m_GridSize.y = Bounds.rect.height / 20f;

        if (mouse != null)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            Cursor.visible = IsInMenu || !InUIBounds(mousePos);

            if (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThresholdSqr)
                SuppressMouseHover = false;
        }
        else
        {
            Cursor.visible = true;
        }

        if (SelectedButton != null)
        {
            float scrollWheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;

            if (Gamepad.current != null)
            {
                if (Gamepad.current.leftShoulder.wasPressedThisFrame)
                    scrollWheel += 1;
                else if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                    scrollWheel -= 1;
            }

            if (kb != null && !typing)
            {
                if (kb.pageUpKey.wasPressedThisFrame || (kb.tabKey.wasPressedThisFrame && kb.shiftKey.isPressed))
                    scrollWheel += 1;
                else if (kb.pageDownKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                    scrollWheel -= 1;
            }

            if (scrollWheel > 0)
                SelectedButton.ClickPrev();
            else if (scrollWheel < 0)
                SelectedButton.ClickNext();
        }

        if (!IsInMenu && !typing)
        {
            HandleGridNavigationInput();
        }
    }

    private readonly InputFieldNavigator _fieldNavigator = new InputFieldNavigator();
    private TMPro.TMP_InputField[] _textFields;

    // A focused TMP input field swallows up/down, so the player could not leave the three
    // level text fields. See InputFieldNavigator.
    void LateUpdate()
    {
        if (!GameController.Instance.EditorPanel.activeSelf)
            return;

        if (_textFields == null)
            _textFields = new[] { LevelName, LevelHint, LevelFinish };

        _fieldNavigator.Tick(_textFields, InputFieldNavigator.Dir.Up, InputFieldNavigator.Dir.Down);
    }

    private bool IsTextFieldFocused()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        if (selected == null)
            return false;

        var inputField = selected.GetComponent<TMPro.TMP_InputField>();
        return inputField != null && inputField.isFocused;
    }

    private void HandleGridNavigationInput()
    {
        int moveX = 0, moveY = 0;
        bool directionPressedThisFrame = false;

        var kb = Keyboard.current;
        var pad = Gamepad.current;

        if (kb != null)
        {
            // Ignore WASD while Ctrl is held so Ctrl+S (save) doesn't also move the cursor
            bool wasd = !kb.ctrlKey.isPressed;

            if (kb.upArrowKey.isPressed || (wasd && kb.wKey.isPressed)) moveY++;
            if (kb.downArrowKey.isPressed || (wasd && kb.sKey.isPressed)) moveY--;
            if (kb.leftArrowKey.isPressed || (wasd && kb.aKey.isPressed)) moveX--;
            if (kb.rightArrowKey.isPressed || (wasd && kb.dKey.isPressed)) moveX++;

            directionPressedThisFrame |=
                kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;

            if (wasd)
            {
                directionPressedThisFrame |=
                    kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame ||
                    kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            }
        }

        if (pad != null)
        {
            Vector2 leftStick = pad.leftStick.ReadValue();

            if (pad.dpad.up.isPressed || leftStick.y > 0.5f) moveY++;
            if (pad.dpad.down.isPressed || leftStick.y < -0.5f) moveY--;
            if (pad.dpad.left.isPressed || leftStick.x < -0.5f) moveX--;
            if (pad.dpad.right.isPressed || leftStick.x > 0.5f) moveX++;

            directionPressedThisFrame |=
                pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;
        }

        moveX = Mathf.Clamp(moveX, -1, 1);
        moveY = Mathf.Clamp(moveY, -1, 1);

        bool placePressed = false;
        bool erasePressed = false;

        if (kb != null)
        {
            placePressed |= kb.enterKey.wasPressedThisFrame ||
                            kb.numpadEnterKey.wasPressedThisFrame ||
                            kb.spaceKey.wasPressedThisFrame;

            erasePressed |= kb.backspaceKey.wasPressedThisFrame ||
                            kb.deleteKey.wasPressedThisFrame;
        }

        if (pad != null)
        {
            placePressed |= pad.buttonSouth.wasPressedThisFrame ||
                            pad.rightTrigger.wasPressedThisFrame;

            erasePressed |= pad.buttonWest.wasPressedThisFrame ||
                            pad.buttonEast.wasPressedThisFrame ||
                            pad.leftTrigger.wasPressedThisFrame;
        }

        bool anyNavInput = moveX != 0 || moveY != 0 || placePressed || erasePressed;

        if (anyNavInput)
        {
            SuppressMouseHover = true;
        }

        if (_hoveredGridItem == null && anyNavInput)
        {
            SelectGridItem(GetGridItem(_cursorX, _cursorY));
        }

        if (moveX != 0 || moveY != 0)
        {
            if (directionPressedThisFrame)
                _nextCursorMoveTime = 0f;

            if (Time.time >= _nextCursorMoveTime)
            {
                MoveCursor(moveX, moveY);
                _nextCursorMoveTime = Time.time + (directionPressedThisFrame ? CursorMoveDelayInitial : CursorMoveDelay);
            }
        }
        else
        {
            _nextCursorMoveTime = 0f;
        }

        if (placePressed && _hoveredGridItem != null)
        {
            _hoveredGridItem.PlaceCurrentItem();
        }
        else if (erasePressed && _hoveredGridItem != null)
        {
            _hoveredGridItem.EraseItem();
        }
    }

    private void MoveCursor(int dx, int dy)
    {
        int fromX = _hoveredGridItem != null ? _hoveredGridItem.x : _cursorX;
        int fromY = _hoveredGridItem != null ? _hoveredGridItem.y : _cursorY;

        _cursorX = Mathf.Clamp(fromX + dx, 0, 29);
        _cursorY = Mathf.Clamp(fromY + dy, 0, 19);

        SelectGridItem(GetGridItem(_cursorX, _cursorY));
    }

    private void SelectGridItem(EditorGridItem item)
    {
        if (item == null || item == _hoveredGridItem)
            return;

        if (_hoveredGridItem != null)
            _hoveredGridItem.OnPointerExit();

        item.OnPointerEnter(null);
    }

    public EditorGridItem GetGridItem(int x, int y)
    {
        GridItemLookup.TryGetValue((x, y), out var item);
        return item;
    }

    public bool InUIBounds(Vector2 screenPosition)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(Bounds, screenPosition, GetBoundsCamera());
    }
    public bool InUIBounds(float x, float y) { return InUIBounds(new Vector2(x, y)); }
    public bool InUIBounds(int x, int y) { return InUIBounds(new Vector2(x, y)); }

    // ------------------------------------------------------------------
    // Misc
    // ------------------------------------------------------------------

    public void Prep()
    {
        IsEditing = false;
        HasChanges = false;
        IsPlaying = false;

        gameController.EraseLevel();
    }

    public void GoHome()
    {
        //todo: check for unsaved changes etc
        IsEditing = false;
        HideMenu();
        gameController.ShowMainMenu();
    }

    // Menu buttons: top row is Return, Main menu, New, Play. File row is Open, Save, which
    // OSAdjustments may hide (e.g. WebGL). Both are assigned in the scene.
    public Button[] MenuTopRow, MenuFileRow;

    // The scene wires navigation as if every button exists. Rewire it around hidden buttons
    // so up/down never points at an inactive one.
    private void UpdateMenuNavigation()
    {
        if (MenuTopRow == null || MenuFileRow == null || MenuTopRow.Length < 4 || MenuFileRow.Length < 2)
            return;

        Button open = MenuFileRow[0], save = MenuFileRow[1];
        // activeSelf, not activeInHierarchy: the menu itself is still inactive when this runs
        bool openOn = open.gameObject.activeSelf, saveOn = save.gameObject.activeSelf;


        Selectable leftFile = openOn ? open : (saveOn ? save : null);
        Selectable rightFile = saveOn ? save : (openOn ? open : null);

        for (int i = 0; i < 4; i++)
        {
            var nav = MenuTopRow[i].navigation;
            Selectable file = i < 2 ? leftFile : rightFile;
            nav.selectOnDown = file != null ? file : LevelName;
            MenuTopRow[i].navigation = nav;
        }

        if (openOn)
        {
            var nav = open.navigation;
            nav.selectOnRight = saveOn ? save : null;
            open.navigation = nav;
        }

        if (saveOn)
        {
            var nav = save.navigation;
            nav.selectOnLeft = openOn ? open : null;
            save.navigation = nav;
        }

        var nameNav = LevelName.navigation;
        nameNav.selectOnUp = leftFile != null ? leftFile : MenuTopRow[1];
        LevelName.navigation = nameNav;
    }

    public void ShowMenu()
    {
        UpdateMenuNavigation();
        EditorMenu.SetActive(true);
        Cursor.visible = true;
        IsInMenu = true;
    }

    public void HideMenu()
    {
        EditorMenu.SetActive(false);
        IsInMenu = false;
    }

    public void SetItem(string itemName)
    {
        _newSprite = Common.UnitPrefab(itemName).GetComponent<SpriteRenderer>().sprite;
    }

    public void SelectItem(string itemName)
    {
        _currentItem = itemName;

        CurrentItemText.text = "Current Item: " + Common.ItemNameToNiceName(itemName);

        if (itemName == "" || itemName == " " || itemName == "clear")
        {
            _newSprite = gridBackgroundSprite;
            _currentItem = "clear";
        }
        else
        {
            _newSprite = Common.UnitPrefab(itemName).GetComponent<SpriteRenderer>().sprite;
        }

        _mouseImage.sprite = _newSprite;

        if (_hoveredGridItem != null)
            _hoveredGridItem.RefreshPreview();
    }

    public int CountDiamonds()
    {
        int diamonds = 0;

        foreach (EditorGridItem levelGridItem in GridItems)
        {
            if (levelGridItem._setItem == "diamond")
                diamonds++;
        }

        DiamondCount.text = diamonds.ToString();

        return diamonds;
    }
}