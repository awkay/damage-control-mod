# UBOAT modding reference (summary)

Sources: the community wiki pages <https://uboat.fandom.com/wiki/Modding> and
<https://uboat.fandom.com/wiki/Datasheet_Modding> (the developers' patch notes
point modders there), the official template that ships with the game, and the
decompiled 2026.1 loader (`UBOAT.Game.Core.Mods.Mod`). The fandom content is
CC-BY-SA, so this file summarizes it in our own words rather than quoting it.

## Official SDK

- It ships with the game: `UBOAT_Data/Launcher/templates/Unity mod template.zip`.
  The launcher's Mods tab, "Create new mod", offers it alongside an "Empty project"
  template.
- The template targets Unity **2021.3.33f1**; the game runs on 2021.3.45f2. It bundles
  editor tooling (asset-bundle packing, deploy-and-launch, a Ship Designer), the
  `Standard (DWS)` shader, and the game assemblies as package references.
- Unity is only needed for asset bundles (prefabs, materials, scenes). Scripts and
  data sheets need no Unity at all ("Empty project" template).

## Mod folder layout

| Path | Purpose |
|---|---|
| `Manifest.json` | name, description, `assemblyName` (unique across all mods), `supportedGameVersions`, `permissions` |
| `Source/` | raw `.cs`, compiled by the game with Roslyn at load. Prebuilt DLLs are rejected. |
| `Data Sheets/` | `.xlsx` that override or extend the game sheets, plus custom sheets the mod reads |
| `Bundles/` | Unity asset bundles (built by the template tooling) |
| `Preview.png/jpg` | launcher and Workshop icon |

Everything else in the folder is ignored by the game, so `docs/`, tests and so on
can live alongside the mod.

## Code

- **Language:** the compiler is Roslyn 2.10, with `LanguageVersion=Default`, which means C# **7.0** (not 7.3; see DEVELOPMENT.md).
- **Defines:** `UBOAT`, `UBOAT_2024_1`, `UNITY_STANDALONE_WIN`.
- **Fixed reference list:** see `Mod.Compile` in the decompiled loader.
- **Entry point:** implement `UBOAT.Game.IUserMod` (wiki says `UBOAT.ModAPI.IUserMod`; 2026.1 uses `UBOAT.Game`). It is instantiated at startup
  and its `OnLoaded()` is called.
- **Permissions** (declared in `Manifest.json`, approved by the player):
  - `IO`: `System.IO`
  - `Reflection`: `System.Reflection` **and Harmony**
  - `Net`: `System.Net`
  - `NativeAccess`: interop and unsafe code

  The checks are enforced by `Trivial.CodeSecurity`.
- **Blocked API:** `UnityEngine.Object.Instantiate` is blocked by the compiler. Use
  `ResourceManager.Instantiate` instead.
- **Injection:** put `[Inject] private static T field;` on fields to receive game
  singletons. A field stays null until its singleton exists; gameplay-only
  singletons such as `INotificationBarUI` are null in menus.
- **Periodic callbacks:** use `IExecutionQueue`, which supports per-frame updates,
  timed updates and game-date callbacks.
- **Background tasks:** subclass `UBOAT.Game.Core.BackgroundTaskBase` (wiki: `UBOAT.ModAPI.Core`) and declare
  it in the mod's `General.xlsx`. Add `[System.Serializable]` and the task is created
  once per campaign, with its state saved and restored with the game. **This is the
  supported way to keep per-campaign mod state, such as toggles.**
- **Harmony:** the game ships Harmony 2.2.2 and officially supports patching game
  code with it. Patching Burst-compiled jobs does not work.

## Data sheets

- **Shape:** group rows begin with `/Group`, followed by data rows whose first cell
  is the row ID. Paths are written as `File.xlsx / Sheet / Group / ID`.
- **Overrides:** a mod sheet with the same file name, tab and group, holding a row
  with an existing ID, replaces that row. A new ID adds a row (most sheets are open
  ended).
- **Load order:** base game first, then mods in launcher order. The last writer wins.
- **Keep sheets minimal:** include only the rows you change or add, to reduce
  conflicts with other mods.
- **Base sheets:** `UBOAT_Data/Data Sheets/` (`Crew`, `General`, `Locals`, `U-boat`,
  `Entities`, `Sandbox`, `CharacterClasses`, `Graphics`).
- **Editor caveat** (community reports): xlsx files saved from LibreOffice have
  caused problems, so generate sheets programmatically or use Excel.

## Assets

- **Raw files:** recent versions load raw texture and audio files from mods, per the
  developers' patch notes. This still needs verifying against the 2026.1 loader. If
  it works, PNG icons need no asset bundle.
- **Bundles:** a folder `Assets/Packages/<modname>` maps to one bundle, and materials
  must use `Standard (DWS)`.
- **UBOAT Inspector mod:** browses and exports game assets at runtime.

## Local install

- Mods go in `AppData/LocalLow/Deep Water Studio/UBOAT/Mods/<mod>` inside the bottle.
- Mods are enabled in the launcher. `modlist.txt` in the same `UBOAT` folder records
  the order.
