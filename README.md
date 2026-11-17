# Kye

A Unity remake of **Kye**, the 1992 Windows 3.1 puzzle game by Colin Garbutt. Collect every diamond on a 30×20 grid while pushing blocks and avoiding monsters, sliders, rockies, black holes and the rest of the cast. Experiment with the objects to work out how each one behaves.

- 500+ original and community levels
- A built-in level editor (levels can be shared as `.kye` files or as `K64_…_K64` text from the clipboard)
- Keyboard, mouse, touch and controller input
- Runs on Windows, Linux, macOS, Android, WebGL and PortMaster handhelds

Play it online and download builds at **https://playkye.com**.

## Thanks

Huge thanks to **Colin Garbutt**, who created the original Kye in 1992 and gave so many people so many hours of puzzling. This project exists because of that game.

Thanks also to the Kye community, who have made and shared levels for decades. The levels in this game are their work, and their messages and author credits are kept inside the levels. If you wrote one of them and would like it credited differently or removed, please open an issue.

## Building

- **Engine:** Unity `7000.0.0a16` (an alpha), URP, the new Input System, uGUI and TextMesh Pro. The exact version is in `ProjectSettings/ProjectVersion.txt`.
- **Scene:** everything (menus, game, level picker, editor, help, settings) lives in `Assets/Scenes/Game.unity`.
- **Command line:** `./build.sh [linux windows mac android web source]` builds and packages each platform (default: all). Output goes to `Export/<version>/`. The Unity editor must not have the project open. See the header of `build.sh` for details, and `Stuff/` for the packaging tools (Windows launcher and installer, AppImage, PortMaster).

Architecture notes (the grid, tick order, level format and so on) are in [`CLAUDE.md`](CLAUDE.md). They were written for AI coding assistants but are useful to people too.

## Missing third-party assets

Some assets used by the game can't be redistributed, so they are **not in this repository** (they are listed in `.gitignore`). A fresh clone will not build or look right until they are replaced. I'm working towards replacing them with assets I can publish. (The sounds are ignored because I can't confirm I may publish them.)

| Folder | What it is | What to do |
|---|---|---|
| `Assets/UI/Sprites/` | UI sprites from Layer Lab's *GUI Pro – Simple Casual* (Unity Asset Store). Buttons, panels and backgrounds used by the menus. | Licence (Asset Store EULA) doesn't allow publishing them. Buy the pack, or draw replacements and re-assign them on the UI in `Game.unity` and the prefabs. Without them the UI shows missing sprites. |
| `Assets/TextMesh Pro.old/` | The old TextMesh Pro resources: `TMP Settings`, the TMP shaders, and the fonts and materials the UI text and the Quicksand font assets are built on (Liberation Sans, Bangers, Roboto and others). | Use *Window → TextMeshPro → Import TMP Essential Resources* (and the examples, if you want them). The scene and prefabs refer to the old assets by GUID, so some font and material references will need re-assigning. |
| `Assets/Sounds/*.mp3` | The 8 sound effects and music: `music`, `diamond`, `levelcomplete`, `boom`, `playerdeath`, `pop`, `blackhole`, `trackerkill`. | I'm not able to publish them. Drop in your own clips with the same names (or assign other clips on the `AudioManager` in `Game.unity`). Without them the game runs silently. |

`Assets/Layer Lab/` and `Assets/_Recovery/` are also ignored. They are not needed.

[SimpleFileBrowser](https://github.com/yasirkula/UnitySimpleFileBrowser) (the file dialog for `.kye` files) is MIT-licensed and **is** included in `Assets/Plugins/SimpleFileBrowser/`.

The Quicksand fonts in `Assets/UI/Fonts/` are under the SIL Open Font License and are included. See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Dev bot (Unity Editor only)

`DevBot` and `DevSolver` (in `Assets/Scripts/`) are a very early, experimental tool for playing levels automatically. They only exist inside the Unity Editor (`#if UNITY_EDITOR`) and are not part of any build. Press **B** during a level to hand Kye over to the bot, and **B** again to take control back. It is rough: expect it to get stuck on many levels. It never records best times and switches analytics off while it runs.

## Analytics

The game sends a few anonymous events (launch, level started and similar, with a random player ID, game version and platform) to `https://playkye.com/log.php`, in the Unity Editor and WebGL builds only. See `Assets/Scripts/KyeAnalytics.cs`. If you fork the game, change the URL or turn `KyeAnalytics.enabled` off.

## Licence

The source code is released under the **GNU General Public License v3.0**. See [`LICENSE`](LICENSE). In short, you are free to use, change and share it, and anything you distribute that is based on it must stay open source under the same licence.

Copyright (C) 2026 Geoff Woollams.

The GPL covers the code only. It does **not** cover:

- the levels (the work of Colin Garbutt and the community authors),
- third-party fonts, plugins and art (see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)),
- the assets listed in "Missing third-party assets" above, which keep their own licences.
