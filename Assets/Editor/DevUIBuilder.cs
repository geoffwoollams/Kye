using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Menu: Kye > Create Dev UI
// Creates the Dev button and Dev page as real objects in the open scene (plus the DevLevelItem prefab used
// for the level rows), so they can be edited by hand. Run it once in Edit mode, then save the scene.
//
//   Canvas/Dev                 DevPanel component (always active)
//   Canvas/Dev/DevPage         the page (copy of LevelPanel; starts inactive)
//   MainMenu/.../DevButton     the main menu button (copy of the Settings button)
//   Assets/Prefabs/DevLevelItem.prefab   a level row (copy of LevelItem: no best time, Edit became Bot)
public static class DevUIBuilder
{
    private const string RowPrefabPath = "Assets/Prefabs/DevLevelItem.prefab";

    [MenuItem("Kye/Create Dev UI")]
    private static void Create()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Create Dev UI", "Leave Play mode first.", "OK");
            return;
        }

        var gc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        var os = Object.FindFirstObjectByType<OSAdjustments>(FindObjectsInactive.Include);
        if (gc == null || os == null || os.SettingsButton == null)
        {
            EditorUtility.DisplayDialog("Create Dev UI", "Open Game.unity first (GameController / OSAdjustments not found).", "OK");
            return;
        }

        if (Object.FindFirstObjectByType<DevPanel>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("Create Dev UI", "The Dev UI already exists in this scene. Delete the Dev object and DevButton first to create it again.", "OK");
            return;
        }

        var rowPrefab = CreateRowPrefab(gc);

        // Root: always active, holds DevPanel, and the page below it.
        var canvasParent = gc.LevelPickerPanel.transform.parent;
        var root = new GameObject("Dev", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Dev UI");
        root.transform.SetParent(canvasParent, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.transform.SetSiblingIndex(gc.LevelPickerPanel.transform.GetSiblingIndex() + 1);
        var dev = root.AddComponent<DevPanel>();

        // Page: a copy of the level picker panel.
        var page = Object.Instantiate(gc.LevelPickerPanel, root.transform, false);
        page.name = "DevPage";
        page.SetActive(false);

        var open = page.transform.Find("Open");
        if (open != null)
            Object.DestroyImmediate(open.gameObject);

        var scrollContainer = page.transform.Find("ScrollContainer").gameObject;
        scrollContainer.SetActive(true);
        var scroll = scrollContainer.GetComponentInChildren<ScrollRectX>(true);
        var content = scroll.GetComponent<RectTransform>();
        for (int i = content.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(content.GetChild(i).gameObject);

        // Search box: only filters this list.
        var filter = page.transform.Find("LoadLevelInput").GetComponent<TMP_InputField>();
        ClearPersistent(filter.onValueChanged);
        ClearPersistent(filter.onEndEdit);
        ClearPersistent(filter.onSubmit);
        UnityEventTools.AddPersistentListener(filter.onValueChanged, new UnityAction<string>(dev.ApplyFilter));

        var clear = filter.transform.Find("Clear");
        if (clear != null && clear.GetComponent<Button>() != null)
        {
            var clearButton = clear.GetComponent<Button>();
            ClearPersistent(clearButton.onClick);
            UnityEventTools.AddPersistentListener(clearButton.onClick, new UnityAction(dev.ClearFilter));
        }

        // Main menu button: a copy of Settings. The free slot to the right of Settings in the Editor layout.
        var source = os.SettingsButton;
        var buttonObject = Object.Instantiate(source, source.transform.parent);
        buttonObject.name = "DevButton";
        buttonObject.SetActive(true);
        Undo.RegisterCreatedObjectUndo(buttonObject, "Create Dev UI");

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(150f, source.GetComponent<RectTransform>().anchoredPosition.y);

        var label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "Dev";

        var button = buttonObject.GetComponent<Button>();
        ClearPersistent(button.onClick);
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(dev.ShowPanel));

        var settingsButton = source.GetComponent<Button>();
        var nav = button.navigation;
        if (nav.mode == Navigation.Mode.Explicit)
        {
            nav.selectOnLeft = settingsButton;
            nav.selectOnRight = null;
            button.navigation = nav;
        }
        var settingsNav = settingsButton.navigation;
        if (settingsNav.mode == Navigation.Mode.Explicit)
        {
            settingsNav.selectOnRight = button;
            settingsButton.navigation = settingsNav;
        }

        dev.Page = page;
        dev.ScrollContainer = scrollContainer;
        dev.Scroll = scroll;
        dev.Content = content;
        dev.Filter = filter;
        dev.RowPrefab = rowPrefab;
        dev.OnlyInEditor = new[] { buttonObject, root };

        EditorUtility.SetDirty(dev);
        EditorUtility.SetDirty(button);
        EditorSceneManager.MarkSceneDirty(gc.gameObject.scene);
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("Create Dev UI",
            "Created Canvas/Dev (with DevPage) and the DevButton on the main menu, and the prefab " + RowPrefabPath + ".\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static GameObject CreateRowPrefab(GameController gc)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (existing != null)
            return existing;

        var source = gc.LevelPickerItem;
        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(source))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            instance = Object.Instantiate(source);
        }

        instance.name = "DevLevelItem";
        instance.SetActive(true);

        // No best time on these rows.
        var bestTime = instance.transform.Find("BestTime");
        if (bestTime != null)
            Object.DestroyImmediate(bestTime.gameObject);

        // The Edit button becomes the Bot button.
        var edit = instance.transform.Find("Edit");
        edit.name = "Bot";
        edit.gameObject.SetActive(true);

        var editLabel = edit.Find("Text");
        if (editLabel != null)
            editLabel.GetComponent<TMP_Text>().text = "Bot";

        // Icon: the one on the main menu's Play button, if there is one.
        var editIcon = edit.Find("Icon");
        var playIcon = gc.PlayButton != null ? gc.PlayButton.transform.Find("Icon") : null;
        if (editIcon != null && playIcon != null && editIcon.GetComponent<Image>() != null && playIcon.GetComponent<Image>() != null)
        {
            var image = editIcon.GetComponent<Image>();
            image.sprite = playIcon.GetComponent<Image>().sprite;
            image.preserveAspect = true;
        }

        var lockObject = instance.transform.Find("Lock");
        if (lockObject != null)
            lockObject.gameObject.SetActive(false);

        var asset = PrefabUtility.SaveAsPrefabAsset(instance, RowPrefabPath);
        Object.DestroyImmediate(instance);
        return asset;
    }

    private static void ClearPersistent(UnityEventBase unityEvent)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(unityEvent, i);
    }
}
