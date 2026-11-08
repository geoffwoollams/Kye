using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// The "Dev" page: a list of every built-in level with a Bot button that loads the level and lets the DevBot
// play it. Editor only: outside the Editor this component destroys the Dev objects as soon as they load.
//
// The button and the page are real objects in the scene (made once with the menu Kye > Create Dev UI, then
// edit them like anything else):
//   Canvas/Dev           this component (always active, so it can open the page)
//   Canvas/Dev/DevPage   the page itself (starts inactive). Back button, search box, scroll area.
//   MainMenuStuff/DevButton   the main menu button; its click calls ShowPanel.
// Only the level rows are made at runtime, from RowPrefab (Assets/Prefabs/DevLevelItem).
public class DevPanel : MonoBehaviour
{
    public GameObject Page;
    public GameObject ScrollContainer;
    public ScrollRectX Scroll;
    public RectTransform Content;
    public TMP_InputField Filter;
    public GameObject RowPrefab;
    [Tooltip("Destroyed outside the Editor (the Dev button and this object).")]
    public GameObject[] OnlyInEditor;

#if UNITY_EDITOR
    public static DevPanel Instance { get; private set; }

    private sealed class Row
    {
        public GameObject Go;
        public RectTransform Rect;
        public string TitleLower;
        public Button BotButton;
    }

    private readonly List<Row> _rows = new List<Row>();

    void Awake()
    {
        Instance = this;
        gameObject.AddComponent<DevBot>();

        for (int i = Content.childCount - 1; i >= 0; i--)
            Destroy(Content.GetChild(i).gameObject);

        for (int i = 0; i < Levels.Count; i++)
            _rows.Add(CreateRow(i));

        ApplyFilter();
    }

    public static void Hide()
    {
        if (Instance != null && Instance.Page != null)
            Instance.Page.SetActive(false);
    }

    private Row CreateRow(int index)
    {
        var go = Instantiate(RowPrefab, Content, false);
        string key = Levels.NameAt(index);
        go.name = "DevItem " + Levels.TitleAt(index);

        go.transform.Find("LevelName").GetComponent<TMP_Text>().text = Levels.TitleAt(index);
        go.transform.Find("LevelNotes").GetComponent<TMP_Text>().text = Levels.HintAt(index);
        go.transform.Find("Number/LevelNumber").GetComponent<TMP_Text>().text = (index + 1).ToString();

        var bot = go.transform.Find("Bot").GetComponent<Button>();
        bot.onClick.AddListener(() => DevBot.Instance.StartLevel(key));

        return new Row
        {
            Go = go,
            Rect = go.GetComponent<RectTransform>(),
            TitleLower = Levels.TitleLowerAt(index),
            BotButton = bot,
        };
    }

    // The search box calls this (set up in the scene).
    public void ApplyFilter(string _) { ApplyFilter(); }

    public void ApplyFilter()
    {
        string filter = Filter != null ? Filter.text.Trim().ToLower() : "";
        float y = -5f;

        foreach (var row in _rows)
        {
            bool visible = filter == "" || row.TitleLower.Contains(filter);
            row.Go.SetActive(visible);
            if (!visible)
                continue;

            var pos = row.Rect.anchoredPosition;
            pos.y = y;
            row.Rect.anchoredPosition = pos;
            y -= 55f;
        }

        var size = Content.sizeDelta;
        size.y = -y;
        Content.sizeDelta = size;
    }

    public void ClearFilter()
    {
        Filter.text = "";
    }

    // The Dev button calls this (set up in the scene).
    public void ShowPanel()
    {
        var gc = GameController.Instance;
        gc.HideMainMenu(); // also hides this page, so show it after

        gc.FrameSprites.SetActive(true);
        gc.FrameUI.SetActive(false);

        Filter.text = "";
        ApplyFilter();

        ScrollContainer.SetActive(true);
        Page.SetActive(true);

        foreach (var row in _rows)
        {
            if (row.Go.activeSelf)
            {
                EventSystem.current.SetSelectedGameObject(row.BotButton.gameObject);
                break;
            }
        }
    }

    void Update()
    {
        if (!Page.activeSelf)
            return;

        var mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (scrollY != 0)
            Scroll.Scroll(scrollY * -0.01f);
    }
#else
    void Awake()
    {
        foreach (var go in OnlyInEditor)
            if (go != null) Destroy(go);
        Destroy(gameObject);
    }

    public void ApplyFilter(string _) { }
    public void ClearFilter() { }
    public void ShowPanel() { }
#endif
}
