# Kye

A Unity remake of **Kye**, the 1992 puzzle game by Colin Garbutt. Kye collects every diamond on a 30×20 grid while pushing blocks and avoiding monsters, sliders, rockies, black holes and the other items.

**Odd behaviour may be intentional.** Many rules (stickers, bouncers, black holes eating things, door directions, monster movement) copy the original game, even where they look like bugs. **Ask before changing any gameplay rule or "fixing" behaviour that looks wrong.** Don't assume what the original did.

- Engine: Unity `7000.0.0a16` (alpha), URP, new Input System, uGUI + TextMeshPro
- One scene: `Assets/Scenes/Game.unity` holds the menus, game, level picker, editor, help and settings.
- Version: `bundleVersion` in `ProjectSettings/ProjectSettings.asset` (shown in-game through `Application.version`)

## Folder layout

```
Assets/
  Scripts/            All game code (start here)
  PlayerInput.cs/.inputactions   Input System actions (PlayerInput.cs is generated from the .inputactions)
  Prefabs/            Game, Border, LevelItem, HelpItem, EditorGridItem, kyeGhost, SplodeEffect
  Prefabs/Items/      One prefab per item type (kye, diamond, walls, sliders, monsters, ...)
  Scenes/Game.unity   The only scene
  Sprites/ Materials/ Models/ Sounds/ Fonts/   Game assets
  Settings/Build Profiles/   Unity build profiles
  URP*, URPDefaultResources/, UniversalRenderPipelineGlobalSettings.asset   Render pipeline config
  _Recovery/          Unity scene-recovery output, not real content
Export/<Platform>/    Build output (gitignored)
```

## Third-party folders: never read or edit

- `Assets/Plugins/SimpleFileBrowser/`: the file browser (used for open/save of `.kye` files)
- `Assets/Layer Lab/`: imported GUI image packs
- `Assets/TextMesh Pro.old/`: old imported TextMesh Pro package

Don't read, search or edit these. Leave them out of greps (for example `grep -r ... Assets/Scripts`). Also skip generated folders: `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/`, `Export/`, `build/`, and the generated `*.csproj` / `*.sln`.

## Architecture

### Bootstrapping
- `AppController` creates itself before the scene loads (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`). It loads settings through `SaveSystem` and holds them in `AppController.Instance.settings` (`SettingsV3`).
- `GameController.Instance` is the scene singleton. It owns the grid, the menus, level loading, Kye's input and movement, and the main loop. Item prefabs are assigned to its `Item*` fields in the inspector.

### The grid
- `GameController.Instance.Items` is an `Item[30, 20]`, with **one item per cell** and `null` meaning empty.
- **Grid coordinates are world coordinates.** An item at cell (x, y) has `transform.position == (x, y, 0)`. `Item.x` / `Item.y` cache the same values.
- Playable area: `Common.InBounds` is x 1–28, y 1–18. Row/column 0 and 29/19 are the border, and `InOuterBounds` is 0–30 × 0–20.
- y increases upward. Level text row 0 (the top line) becomes y = 19.
- **Always move, place and remove items through `Common`** (`MoveItem`, `SetItem`, `DestroyItem`). This keeps the array, the transform and `x`/`y` in sync. Setting `transform.position` directly desyncs the grid.
- Exception: Kye can stand **on** a door. The door keeps owning the cell, and `MoveItem` handles this case.

### Items
- `Item.cs` is a bag of flags (`IsKye`, `IsPushable`, `KillsPlayer`, `IsDoor`, `IsStickerLR/UD`, `IsBlackhole`, `Roundness`, `Direction`, ...) plus optional behaviour components that it finds in `Awake`:
  - `GoForward`: sliders, rockies, bouncers. Moves in `Direction`; handles clockers, bouncing, rolling off round things.
  - `Enemy`: monsters. `IsSmort` items can pass doors. `IsGuard` follows a wall.
  - `Auto`: auto-slider/auto-rocky spawners. Rotates and spawns its `Prefab`.
  - `Blackhole`, `Timer` (counts down and replaces itself with `NextTimerPrefab`), `Sploder`
- Stickers: `Common.PullToSticker` and `Common.StickersFollowKye`. Stuck items skip their `GoForward`/`Enemy` tick.
- `Common` also maps between level characters, item names and prefabs. Adding an item type means updating **all four** maps (`GetPrefabFromItemChar`, `GetKyeCodeFromItem`, `GetItemNameFromKyeCode`, `ItemNameToNiceName`), adding a `GameController` prefab field and assigning it in the scene, and adding it to the level editor.
- Wall codes follow the numpad and are flipped vertically: `wall1` ↔ `'7'`, `wall7` ↔ `'1'`, and so on. This is deliberate.

### Tick order
`GameController.Update()`:
1. Return early if the level editor is editing, the main menu is open, or a message is showing.
2. If the game panel is hidden (a level running behind a menu), run `Tick()` only.
3. Otherwise run `TickKye()` (input, beast check, `ProcessItem` for the target cell, move, stickers follow), then `Tick()`, then finish the level when no diamonds are left.

`Tick()` calls `Item.Tick()` on every item **in `Thinker` order**. `Thinker` is a creation counter assigned by `GameController.NewItem`. Level parsing runs from the bottom text row up, left to right, so items lower on the board think first. Spawned items get the next number, so they think last. Anything that creates an item at runtime should call `NewItem`. Changing this order changes how levels play.

`Item.Tick()` order: pull to sticker → `GoForward` / `Enemy` (unless stuck) → `Auto` → `Blackhole` → `Timer` → `Sploder` → pull to sticker again → re-check whether still stuck. Exceptions inside it are caught and logged, not thrown.

There is no fixed timestep. Every component times itself against `Time.time`: `GoForward.Timer`, `Enemy.Speed`, `Auto.RotateSpeed`, a 2.5 s step for `Timer`, and Kye's 0.4 s first-move delay then 0.05 s repeat. `Tick()` runs every frame, but most items act only when their own timer expires. When a level is completed, `Time.timeScale` is set to 0.

### Level loader
- **Level format**: a base64 string that decodes to text. Line 0 is the name, line 1 the hint, line 2 the completion message, and lines 3–22 are 20 rows of 30 characters each, using the codes from `Common`.
- **Built-in levels**: `Levels.Classic` in `Levels.cs` maps a lowercase name to base64. **Dictionary order is the progression order** used for unlocking and next/previous (`Common.IsLevelUnlocked`, `GetNextLevelKey`, `UpdateUnlockedLevel`). Comments group the levels by the original `.kye` file they came from. `Levels.CheckForDuplicates` runs in the Editor.
- **`.kye` files**: the first line is the level count, followed by 23-line blocks (`GameController.Get23`).
- **K64**: the editor exports a level to the clipboard as `K64_<base64>_K64`.
- Entry points on `GameController`: `LoadLevelWithNoR(name)`, `LoadLevelWithString(raw)`, `LoadLevelWithB64(b64)`, `LoadLevelWithKyeFile(path)`. All of them reach `LoadLevel(b64)` → `EraseLevel()` → `InsertLevel()`.
- `InsertLevel` instantiates prefabs into `ItemContainer`. If the level has no Kye, it adds one at (3, 3).

### Saving and analytics
- `SaveSystem` writes `settings.v3.json` to `Application.persistentDataPath` and migrates the older `settings.v1.dat` / `settings.v2.dat` files.
- WebGL builds force `UnlockAllLevels`.
- `KyeAnalytics` posts to `https://playkye.com/log.php`, in the Editor and WebGL only.

## Platform-specific UI: check `OSAdjustments.cs` for any menu change

`OSAdjustments.cs` (on the main menu in `Game.unity`) changes the menu at runtime under `#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR`:
- it **removes** the Exit button (`SetActive(false)`),
- it **moves** the Help and Settings buttons 50 px to the right,
- it **rewires** the Levels button's `selectOnDown` to Settings, for controller and keyboard navigation.

Including `UNITY_EDITOR` is deliberate: the Editor shows the mobile/web layout.

**Before any main-menu change** (adding, removing, moving or reordering buttons, or changing navigation), read `OSAdjustments.cs` and check that the change still works both with and without these adjustments. Update the script if needed.

Other platform-specific code:
- `DisableByPlatform` component: used on several scene objects to hide them on iOS, Android or WebGL.
- `GameController.ClickOpen`: on Windows, the file dialog opens in Documents by default.



## Rules
Before changing any rules or movement code, or any project setting, state the reason and the evidence, and ask me if the change could alter gameplay. Label each finding as bug, performance, dead code, or possible intentional quirk, and apply only the performance and dead-code changes without asking. If you think it is needed, git commit before beginning a large change.
