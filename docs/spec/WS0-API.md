# WS0 foundation: API for WS1 to WS5

Status: implemented (WS0). This is the contract the other workstreams build on. Names here are ours,
except game types, which are facts about the 2026.1 game. If you need something that is not here, ask
WS0 rather than adding it to a WS0-owned folder.

## 1. Where things go

| What | Path | Owner |
|---|---|---|
| Pure logic, tests | `mod/Source/Core/<Area>/`, `tests/DamageControl.Tests/<SourceFile>Tests.cs` | each WS |
| Game adapters, patches, actions | `mod/Source/Game/<Area>/` | each WS |
| Data rows and locale rows | `data/<area>/*.json` | each WS |
| Icons | `mod/Assets/Icons/DamageControl/<area>/<Category>/<IconName>.png` | each WS |
| Tables, columns, generator | `data/schema.json`, `tools/SheetBuilder/` | WS0 |
| Generated workbooks (never edit) | `mod/Data Sheets/*.DamageControl.xlsx` | generated |

Area folder names: `common` (WS0), `pressure` (WS1), `hatches` (WS2), `leaks` (WS3), `ballast` (WS4),
`repair` (WS5).

Namespaces: `DamageControl.Core.<Area>` and `DamageControl.Game.<Area>`. Every mod type must live under
`DamageControl.`: the action registration only resolves row types in that namespace.

Spec 00 says icons go in `mod/Icons/`. They go in `mod/Assets/Icons/...` instead, because the game
loads raw files only from a mod's `Assets/` folder, and reading files ourselves would need the `IO`
permission.

## 2. Core (`DamageControl.Core.Common`, pure C# 7.0)

### `DcLog` (logging, R14 debug flag)

```csharp
DcLog.Debug(string)            // only when DcLog.DebugEnabled (R14, off by default)
DcLog.Info(string); DcLog.Warn(string); DcLog.Error(string)
bool DcLog.WarnOnce(string key, string msg)    // once per key per session; true if it logged
bool DcLog.ErrorOnce(string key, string msg)
bool DcLog.Exception(string key, Exception e)  // once per key per session, with stack
bool DcLog.PatchFailed(string patchId, Exception e)  // standard patch catch handler
bool DcLog.DebugEnabled { get; set; }          // set from data row General/Settings/Damage Control/Debug
Action<LogLevel, string> DcLog.Sink { get; set; }  // Game installs Unity's log; tests install a capture
void DcLog.ResetOnceKeys()                     // called by WS0 at every campaign session start
```

Every line is prefixed `[Damage Control] `. Logging never throws.

**Patch body rule (00 §2.1 rule 2):**

```csharp
private static bool Prefix(...)
{
    try { /* read, call Core, write */ }
    catch (Exception e) { DcLog.PatchFailed("HatchDoUpdate", e); return true; } // vanilla runs
    ...
}
```

A postfix catches the exception, calls `PatchFailed`, and leaves `__result` untouched.

Diagnostic text that a player could see, such as panel debug lines, is shown only when
`DcLog.DebugEnabled` is set.

### `SimClock` (00 §2.1 rule 5, C2)

```csharp
new SimClock(float unityDeltaTime, float unityTimeScale, float artificialTimeScale)
SimClock.FromGameSeconds(float s)              // tests and fixed ticks: 1x compression
float GameDt                                   // unityDeltaTime x artificialTimeScale
float Compression                              // unityTimeScale x artificialTimeScale (S)
bool  IsPaused
float CappedGameDt(float max)                  // the per-spec cap, e.g. 0.5 s
float CappedUnityDt(float max)                 // Unity frame delta (already timeScale-scaled), capped
static float DampedRate(float baseRate, float compression) // baseRate if S <= 1, else baseRate / sqrt(S)
float DampedRate(float baseRate)               // at this frame's Compression
```

Core takes a `SimClock` or a `dt` argument and never reads time itself. The Game layer gets one with
`GameClock.Now()`, or with `GameClock.FixedNow()` in FixedUpdate paths.

### Randomness and probabilities (D7)

```csharp
interface IRandom { float NextFloat(); }      // [0, 1)
new SeededRandom(int seed)                     // deterministic, for tests
bool  random.Chance(float p)                   // extension; p clamped; NaN -> false
float random.Range(float min, float max)
float Probability.PerStep(float perSecond, float dt)    // 1 - (1-p)^dt, total
bool  Probability.Roll(IRandom r, float perSecond, float dt)
```

Game: `UnityRandomSource.Instance`.

### `DcMath`

`Clamp01`, `Clamp(float|double, min, max)` (NaN becomes min), `Approximately(a, b, eps = DcMath.Epsilon)`
(Epsilon = 1e-4; use it instead of `==` on floats, D7), and `IsFinite`.

### `HatchGeometry`

`HatchGeometry(lowY, highY)`, `Height`, `IsValid`, and
`HatchGeometry.FromBoxCollider(transformY, centerY, sizeY, scaleY)`. Core formulas that need hatch
heights take `lowY` and `highY` as plain floats. The Game side gets them from `BoatLayout`.

### `IconKeys`, `ActionRowRules`

These are the conventions for icons and action rows (sections 5 and 6). `IconKeys.InteractionWheel`,
`.Actions`, `.ActionsCursor`, `.ActionsBanners` and `.StatusesPortraits` are the category folder names.
`IconKeys.HighlightedSuffix` is `" (Highlighted)"` and `DisabledSuffix` is `" (Disabled)"`.

## 3. Settings

Core, `DamageControl.Core.Settings`:

```csharp
enum SettingId { HatchPressureBlocking, AutoLeakBlocking, AutoCarryWounded }
bool   SettingsCatalog.DefaultValue(SettingId)    // D3: false, true, true
string SettingsCatalog.ToggleKeyword(SettingId, bool current)  // "DC Enable ..." / "DC Disable ..."
class SettingsModel { Get, Set, Toggle, ToggleKeyword, CreateDefaults() }
```

Game, `DamageControl.Game.Settings.DcSettings` (static; main thread):

```csharp
bool DcSettings.HatchPressureBlocking   // WS2 H1: gate the CanBeOpened postfix on this
bool DcSettings.AutoLeakBlocking        // WS3 H12: gate LeakBlockingBySailors.DoUpdate
bool DcSettings.AutoCarryWounded        // WS3 H13: gate AutoCarryWoundedToSafety (not the diver pull-up, C27)
bool DcSettings.Get(SettingId)
bool DcSettings.IsCampaignActive
bool DcSettings.Toggle(SettingId) / Set(SettingId, bool)
event Action<SettingId, bool> DcSettings.Changed
event Action DcSettings.SessionStarted  // new campaign or save loaded: reset session-only statics here
```

Reads never fail. Outside a campaign (menus, loading) they return the D3 defaults. Do not cache a
value across frames: read it each time, which costs a field read.

Storage is `DamageControlSettingsTask`, a `[Serializable] BackgroundTaskBase` declared in
`General/Background Tasks` with start event `CampaignStart`. It holds plain bool fields. A save made
before the mod was installed gets the task, with defaults, right after it loads (`DcSettings.EnsureTask`,
queued from `SavesManager.Loaded`). Nothing else stores settings. A new persistent setting is a WS0
change: add a `SettingId` member, a field on the task, and a toggle row.

The three toggle actions are rows of `SettingToggleAction` on the vanilla `Workshop` activator. They
use contexts Normal, Queue and NoCharacter, take effect instantly, and are hidden outside a campaign.

## 4. Boat layout (`DamageControl.Game.Infrastructure.BoatLayout`, static, main thread)

```csharp
PlayerShip Ship; bool IsKnown; string UnknownReason; int Version
IReadOnlyList<Hatch> HatchesLeftToRight   // PlayerShip.HatchesLeftToRight
Hatch ExteriorHatch; Hatch ConningTowerHatch
IReadOnlyList<Tank> BallastTanks; bool IsBallastTank(Tank)
bool TryGetGeometry(Hatch, out HatchGeometry)   // lowY = the hatch's lowestPoint marker, highY = lowY + collider height (WS2);
                                                // BoxCollider bottom when the marker is missing; unlisted hatches measured on demand
bool TryGetConningTowerLevel(out float y)       // replaces 2022.1's 101.55 m (C12)
bool TryGetExteriorLevel(out float y)           // replaces 2022.1's 102.8 m (C12)
bool Require(string feature)   // false + one warning per feature per session when unknown
event Action Rebuilt; void Invalidate()
```

It is built lazily and rebuilt when the injected `PlayerShip` instance changes. Use it like this:

```csharp
if (!BoatLayout.Require("Hatch exchange")) return true; // leave vanilla alone
```

## 5. Other Game helpers (`DamageControl.Game.Infrastructure`)

| API | Use |
|---|---|
| `GameClock.Now()`, `GameClock.FixedNow()`, `GameClock.ArtificialTimeScale` | `SimClock` for this frame |
| `UnityRandomSource.Instance` | `IRandom` in game |
| `Tunables.GetBool/GetFloat/GetString(path, default)`, `Tunables.GetRow`, `Tunables.GetRows` | null-safe data reads; one warning per malformed row |
| `Tunables.Root` = `"General/Settings/Damage Control/"` | put your tunables here, e.g. `Root + "Hatch Lock Threshold"` |
| `IconRegistry.IsServed(gameKey)` | debugging only |

Every `[Inject]` static field in mod code is assigned by the game, so the compiler reports
CS0649. Wrap each one in `#pragma warning disable 0649` and `#pragma warning restore 0649`, as WS0's
files do. The build must stay warning-free.

## 6. Actions (R10, D2)

1. Write a public, `[Serializable]` `CharacterAction` subclass, usually `SingleCharacterAction` or
   `DeviceUseAction`, in the `DamageControl.` namespace. It needs both constructors:
   ```csharp
   public MyAction(GameDataRow data) : base(data) { ... }
   public MyAction(MyAction source, Activator activator) : base(source, activator) { /* copy fields */ }
   ```
   Alias the game's activator (`using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;`)
   so it does not clash with `System.Activator`.
2. Add a `Crew/Actions` row in your `data/<area>/*.json` with `"Type": "DamageControl.Game.<Area>.MyAction"`.
   WS0's `LoadActions` postfix registers it. A row whose name is already registered, by the game or by
   Custom Actions, is skipped.
3. Labels: `GetLocalizationKeyword` returns a keyword starting with `"DC "`. Add `Locales/Activators`
   rows for `<keyword>` and `<keyword> Description` (or `<keyword> Short Description`). The default
   icon name is the keyword.
4. Rows override vanilla rows by key, last writer wins. Override a vanilla row only when your spec says
   to, and re-supply every column.

`./verify` checks each of our rows offline with the game's own xlsx reader: the type exists in the
compiled mod, it is a concrete public `CharacterAction`, it has both constructors, and its contexts are
valid.

## 7. Data and locale rows

Source format: `data/<area>/<anything>.json`. Comments (`//`) and trailing commas are allowed.

```json
{
  "tables": {
    "Crew/Actions": [
      { "key": "DCDismantle", "Type": "DamageControl.Game.Repair.DismantleAction",
        "Activator Tags": "Engines, Compressor", "Character Tags": "Engineer",
        "Character Tags (Soft Req)": "*", "Contexts": "Normal, Queue",
        "Group": "Engineer", "Subgroup": "", "…": "Name = value; Other = 2" }
    ],
    "General/Settings/Damage Control": [ { "key": "Hatch Lock Threshold", "Value": "0.9" } ],
    "Locales/Activators": [ { "key": "DC Dismantle", "English": "Dismantle for scrap" } ],
    "Locales/Items": [ { "key": "Scrap Description", "English": "..." } ]
  }
}
```

- **Tables.** A table is addressed as `Workbook/Sheet` or `Workbook/Sheet/Group`. `data/schema.json`
  lists the tables and their columns. To get a new one, ask WS0.
- **Columns.** Write the column name exactly as the game sheet's header does. `"..."` is accepted for
  the parameter column `…`.
- **Locales.** Columns are `English` plus the other languages. Only English is required: blank
  languages fall back to English in game.
- **Duplicates.** Each row key has one owner. A key that appears in two source files is an error.
- **Building.** `./build-sheets` (also the first step of `./verify`) writes
  `mod/Data Sheets/<Workbook>.DamageControl.xlsx`. Commit the generated files together with your
  sources. `./build-sheets --check` fails when the shipped files are stale.

Unit tests can read the sources through `tests/DamageControl.Tests/Support/RepoData.cs`, using
`RepoData.Rows(table)`, `RepoData.English(sheet, key)` and `RepoData.IconExists(category, iconName)`.
Use it to assert that every keyword your actions return has an English row and an icon
(04 §8 #10, #11). See `SettingsModelTests`.

## 8. Icons (R12)

1. Draw a PNG at `mod/Assets/Icons/DamageControl/<area>/<Category>/<IconName>.png`. `IconName` must
   start with `"DC "`. `Category` is `InteractionWheelUI`, `Actions`, `Actions - Cursor`,
   `Actions (Banners)` or `Statuses (Portraits)`. Wheel icons also come in ` (Highlighted)` and
   ` (Disabled)` variants. When a variant is missing, the game falls back to the normal icon or to a
   generic one.
2. At startup WS0 serves each PNG under `Icons/<Category>/<IconName>`: a Sprite for every category
   except the cursor, which gets a Texture2D. No code is needed per icon.
3. For placeholder art, run
   `python3 tools/placeholder-icons/make_icons.py <dir> glyph:badge:"DC Name"` (the script lists the
   glyphs). Final art must be newly drawn.
