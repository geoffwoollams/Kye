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
  Editor/BuildScript.cs   Command line build entry point used by build.sh
  Sprites/ Materials/ Models/ Sounds/ Fonts/   Game assets
  Settings/Build Profiles/   Unity build profiles
  URP*, URPDefaultResources/, UniversalRenderPipelineGlobalSettings.asset   Render pipeline config
  _Recovery/          Unity scene-recovery output, not real content (gitignored)
Export/<version>/     Build output (gitignored): <Platform>/ builds, logs/ and Packages/ (the release packages)
Stuff/                Non-Unity code: release tooling, not part of the game
  Launcher/           Kye auto-updater (C#, net48) and the NSIS script for Kye-Setup.exe
  appimage/           AppDir and appimagetool for the Linux AppImage
  portmaster/         PortMaster ports (unityloader is the one build.sh uses)
build.sh              Builds and packages every platform (see Building)
```

## Building

`./build.sh [linux windows mac android web source]` (default: all six) runs Unity in batch mode once per platform through `BuildScript.Build`, then packages. `source` needs no Unity build and runs last. Everything goes in `Export/<version>/` (`Linux/`, `Windows/`, `Mac/`, `Android/`, `Web/`, `logs/`, `Packages/`), so older versions stay available with their debug info and logs. A full build deletes only that version's folder first; a partial build only clears the platform folders it rebuilds. The script ends with a stats table (Unity time, packaging time and total per platform, plus package sizes). The editor binary comes from `ProjectVersion.txt` (override with `UNITY=`), and the version from `bundleVersion`. The Unity editor must not have the project open.

Packages land in `Export/<version>/Packages/`, named from the version (`Kye-3.1.0014a` here): `.tar.gz` (Linux), `.AppImage`, `.zip` (Windows), `-macOS.zip` (contains `Kye.app`), `-web.zip` (the WebGL build files at the zip root), `-source.zip` (source code and build tools, see below), `.apk`, `kye-portmaster.zip` (PortMaster), plus `Kye-Setup.exe`. The unzipped Web build also stays in `Export/<version>/Web/`. Unity's `*_BackUpThisFolder_ButDontShipItWithYourGame` and `*_BurstDebugInformation_DoNotShip` folders are left out of every package.

- **Source zip**: `Kye-<version>-source.zip`, everything under a `Kye-<version>-source/` folder. The file list is git's (`git ls-files --cached --others --exclude-standard`), so it follows `.gitignore` and includes uncommitted and untracked files, and skips files deleted in the working tree. It also leaves out `Export/`, `Stuff/appimage/AppDir/usr/bin/`, `appimagetool-x86_64.AppImage`, `Stuff/Launcher/nsis/app/`, every `*.apk`, and PortMaster's `gamedata/lib/` (except `libc++_shared.so`, added back although `.gitignore`d) and `gamedata/assets/`. The `unityloader` binary is kept. Gitignored third-party folders (below) are left out of the zip, so a built source zip needs them added back (see README.md). Windows packaging also stamps `InstalledVersion` in `Stuff/Launcher/nsis/settings.ini` with the build version (trailing letters dropped), so that file shows as modified after a Windows build. Changing the exclusions means editing the `source)` case in `build.sh`.
- **Windows**: the zip has the game files at its root (the Launcher extracts it straight into its `app` folder). `Stuff/Launcher/nsis/app` is emptied and replaced with the Windows build, then `Stuff/Launcher/build.sh` produces `Kye-Setup.exe`.
- **Linux AppImage**: `Stuff/appimage/AppDir/usr/bin` is emptied and replaced with the Linux build, then `ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir Kye-<version>.AppImage`.
- **Android**: builds an `.apk`, not an `.aab`.
- **PortMaster**: after the apk, its `lib/*` is copied over `Stuff/portmaster/unityloader/kye/gamedata/lib/`, and `assets/bin/Data/` replaces (the target is emptied first) `kye/gamedata/assets/bin/Data/`. There is no toml file any more (unityloader's defaults are used), so no version is stamped into the port. `Kye.sh` and `kye/` are then zipped to `Export/<version>/Packages/kye-portmaster.zip`.

## Third-party folders: never read or edit

- `Assets/Plugins/SimpleFileBrowser/`: the file browser (used for open/save of `.kye` files). MIT licensed, so it **is** committed.
- `Assets/UI/Sprites/`: Layer Lab UI sprites (Asset Store EULA). **Gitignored**, present locally only.
- `Assets/Layer Lab/`: imported GUI image packs. Gitignored.
- `Assets/TextMesh Pro.old/`: old imported TextMesh Pro package, still referenced by the scene (fonts, shaders). Gitignored.

`Assets/Sounds/*.mp3` (and their `.meta` files) are gitignored too (licence unconfirmed), so they exist only locally. The README's "Missing third-party assets" section says what a fresh clone must replace. **Never `git add -f` the gitignored items, and keep that README section and `THIRD_PARTY_NOTICES.md` in step when assets are added, replaced or ignored.** The licences also matter when adding any new third-party asset: ignore it unless it may be redistributed.

Don't read, search or edit the folders above. Leave them out of greps (for example `grep -r ... Assets/Scripts`). Also skip generated folders: `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/`, `Export/`, `build/`, and the generated `*.csproj` / `*.sln`.

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
  - `Enemy`: monsters. `IsSmort` items can pass doors. `IsGuard` follows a wall on its right. At start it faces the best direction: a real wall (`Common.IsWall`) on its right with a free cell ahead, then a wall on its right, then any item on its right with a free cell ahead, then any item on its right, then a free cell ahead (ties go right, up, left, down). With nothing nearby it walks forward until it hits something or finds something on its right.
  - `Auto`: auto-slider/auto-rocky spawners. Rotates and spawns its `Prefab`.
  - `Blackhole`, `Timer` (counts down and replaces itself with `NextTimerPrefab`), `Sploder`
- Stickers: `Common.PullToSticker` and `Common.StickersFollowKye`. Stuck items skip their `GoForward`/`Enemy` tick.
- `Common` also maps between level characters, item names and prefabs. Adding an item type means adding one row to `Common.ItemDefs` (name, nice name, level character, prefab field), adding the `GameController` prefab field and assigning it in the scene, and adding it to the level editor. The four lookups (`GetPrefabFromItemChar`, `GetKyeCodeFromItem`, `GetItemNameFromKyeCode`, `ItemNameToNiceName`) are built from that table.
- Wall codes follow the numpad and are flipped vertically: `wall1` ↔ `'7'`, `wall7` ↔ `'1'`, and so on. This is deliberate.

### Tick order
`GameController.Update()`:
1. Return early if the level editor is editing, the main menu is open, or a message is showing.
2. If the game panel is hidden (a level running behind a menu), run `Tick()` only.
3. Otherwise run `TickKye()` (input, beast check, `ProcessItem` for the target cell, move, stickers follow), then `Tick()`, then finish the level when no diamonds are left. A level that starts with no diamonds never finishes (`_levelHasDiamonds`).

`Tick()` calls `Item.Tick()` on every item **in `Thinker` order**. `Thinker` is a creation counter assigned by `GameController.NewItem`. Level parsing runs from the bottom text row up, left to right, so items lower on the board think first. Spawned items get the next number, so they think last. Anything that creates an item at runtime should call `NewItem`. Items created without `NewItem` (the replacement timers from `Timer`) keep `Thinker` 0, so they think first. `Tick()` collects the items with `GetComponentsInChildren` into a reused list and stable-sorts it by `Thinker`, so ties keep scene order. Changing this order changes how levels play.

`Item.Tick()` returns at once for items with no behaviour components that are not stickable (walls, earth, diamonds, blocks). Otherwise the order is: pull to sticker → `GoForward` / `Enemy` (unless stuck) → `Auto` → `Blackhole` → `Timer` → `Sploder` → pull to sticker again → re-check whether still stuck. Exceptions inside it are caught and logged, not thrown.

There is no fixed timestep. Every component times itself against `Time.time`: `GoForward.Timer`, `Enemy.Speed`, `Auto.RotateSpeed`, a 2.5 s step for `Timer`, and Kye's 0.4 s first-move delay then 0.05 s repeat. `Tick()` runs every frame, but most items act only when their own timer expires. When a level is completed, `Time.timeScale` is set to 0.

### Level loader
- **Level format**: a base64 string that decodes to text. Line 0 is the name, line 1 the hint, line 2 the completion message, and lines 3–22 are 20 rows of 30 characters each, using the codes from `Common`.
- **Built-in levels**: `Levels.cs` is a static class over an ordered `Level[] All` array of `(lowercase name, title, hint, base64)`. **Array order is the progression order** used for unlocking and next/previous (`Common.IsLevelUnlocked`, `GetNextLevelKey`). Use `Levels.Count`, `Names`, `Contains`, `IndexOf`, `NameAt`, `GetB64` / `GetB64At`, and `TitleAt` / `TitleLowerAt` / `HintAt` (the level picker uses the last three so it never decodes a level). Title and hint are copies of the first two lines of the base64 text; when you change a level's base64, update them too (`CheckForDuplicates` warns on a mismatch). The base64 is stored uncompressed on purpose. Comments group the levels by the original `.kye` file they came from. 24 `unknown N` levels with abnormal data (an extra header line) are commented out, and `marbles`, `start` and `end` are commented out with a `//todo` to examine them (non-wall items on the border). `Common.IsLevelUnlocked` unlocks a level if it is the first, has a best time, or follows a level with a best time (names it doesn't know are unlocked). `Levels.CheckForDuplicates` runs in the Editor (duplicate names, duplicate level data, title/hint mismatches).
- **Level picker**: `GameController.LoadLevelPicker` keeps one row object per level (`PickerView`, created once and reused). Each call only hides rows that fail the title filter, repositions the visible ones, and refreshes best time, lock state and navigation from the settings; nothing is created or destroyed. Rows remember what they show, so changing what a row displays means updating `UpdatePickerView`.
- **`.kye` files**: the first line is the level count, followed by 23-line blocks (`GameController.Get23`).
- **K64**: the editor exports a level to the clipboard as `K64_<base64>_K64`.
- Entry points on `GameController`: `LoadLevelWithNoR(name)`, `LoadLevelWithString(raw)`, `LoadLevelWithB64(b64)`, `LoadLevelWithKyeFile(path)`. All of them reach `LoadLevel(b64)` → `EraseLevel()` → `InsertLevel()`.
- `InsertLevel` instantiates prefabs into `ItemContainer`. If the level has no Kye, it adds one at (3, 3). Any non-empty item on the border (x 0 or 29, y 0 or 19) that is not a wall (codes `1`-`9`) becomes `wall5`.

### Dev bot (Editor only)
`DevBot` (`#if UNITY_EDITOR`, with `DevSolver` as its path finder) plays the current level when you press **B** during a level, and stops on B again, on level complete or failed, on any level load, and on the main menu. It creates itself at startup and feeds Kye's input through a hook in `GameController.ProcessInput`; it never records best times and switches analytics off while it runs. It shows a semi-transparent "Bot Enabled" notice at the bottom centre.

### Saving and analytics
- `SaveSystem` writes `settings.v3.json` to `Application.persistentDataPath` and no longer reads the old `settings.v1.dat` / `settings.v2.dat` files (their migration code was removed). If the v3 file is corrupt it is copied to `.corrupt` and defaults are used. `Save` skips the disk write when the settings JSON is unchanged since the last write.
- WebGL builds force `UnlockAllLevels`.
- `KyeAnalytics` posts to `https://playkye.com/log.php`, in the Editor and WebGL only.

## Platform-specific UI: check `OSAdjustments.cs` for any menu change

`OSAdjustments.cs` (on the main menu in `Game.unity`) changes the menu at runtime. Android and desktop keep the Exit button and instead rewire Settings' `selectOnRight` to Exit. Under `#if UNITY_WEBGL || UNITY_IOS || UNITY_EDITOR`:
- it **removes** the Exit button (`SetActive(false)`),
- it **moves** the Help and Settings buttons 50 px to the right,
- it **rewires** the Levels button's `selectOnDown` to Settings, for controller and keyboard navigation.

Separately, under `#if UNITY_WEBGL` only (not the Editor), it hides the Open and Save buttons listed in its `HideOnWebGL` array (the level picker's Open button and the level editor's Open and Save buttons). `LevelEditor` also compiles out its Ctrl+S and O shortcuts on WebGL.

Including `UNITY_EDITOR` is deliberate: the Editor shows the mobile/web layout.

**Before any main-menu change** (adding, removing, moving or reordering buttons, or changing navigation), read `OSAdjustments.cs` and check that the change still works both with and without these adjustments. Update the script if needed.

Other platform-specific code:
- `DisableByPlatform` component: used on several scene objects to hide them on iOS, Android or WebGL.
- `Common.GetDefaultFilePath()`: the default folder for file dialogs (`GameController.ClickOpen` and the `LevelEditor` open/save dialogs). Android, iOS and WebGL use `persistentDataPath`; other platforms use Documents.
- The main menu background level is `settings.LastLevelPlayed`, the same level the Play button loads.



## Rules
Before changing any rules or movement code, or any project setting, state the reason and the evidence, and ask me if the change could alter gameplay. Label each finding as bug, performance, dead code, or possible intentional quirk, and apply only the performance and dead-code changes without asking. If you think it is needed, suggest a git commit. If you are changing something mentioned in CLAUDE.md, or which should be, suggest relevant changes.
