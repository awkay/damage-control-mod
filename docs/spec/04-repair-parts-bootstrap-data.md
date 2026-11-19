# Spec 04 — Repair, spare parts, mod bootstrap and data sheets

Status: behavioral spec (dirty room). Source of observation: the original mod's
2022.1-era build (v0.61) and, for comparison, the vanilla game 2026.1 assembly
and data sheets. Implementers: build only from this document plus the game's
own assemblies/data sheets. Never open the original mod.

Conventions used below:

- "Game fact" = a name the game itself defines (type, method, data sheet key,
  item name, status name, slot name, activator tag). These are quoted exactly.
- "Our key" = a name we choose. Where the original introduced its own
  identifier, this spec gives only its *purpose* and proposes a neutral name of
  our own (prefixed `DC`), which implementers may change.
- Time is in seconds of simulation time unless noted. "Per frame" quantities
  are written as rates.

---

## 1. Overview

Three crew actions make up the repair / spare-parts economy:

| Action | Who | Where | Effect |
|---|---|---|---|
| Repair (replacement for vanilla) | Engineer officers at full rate; radioman / leader officers at one third | any damaged equipment (activator tag `Damaged`) | restores health 1 point at a time; consumes one `Replacement Parts` per point when the equipment is at 0 health and its data says repairs consume parts; while repairing a hatch, hole or pipe, the leak flow is throttled down proportionally to progress |
| Salvage replacement parts (replacement for vanilla) | any crew, preferring the `Salvager` perk | workshop (tag `Workshop`) | converts `Scrap` into `Replacement Parts` at **4 : 1** (vanilla 2 : 1) |
| Dismantle for parts (new) | Engineer officers | a fixed list of device/equipment tags | removes health 1 point at a time down to 0, then yields `Scrap` (1, or 2 with the `Thrifty` perk) |

How this changes vanilla:

1. **Repair works from more stations.** Vanilla repair uses only the `Repair`
   interaction slot (with `Default` as a state-check fallback). Ours tries, in
   order: `Default` first for hatches; otherwise `Repair`, then `Block Leak`,
   then `Default` — whichever has a free slot. The animation/interaction type is
   taken from the chosen slot (so a repair at a leak uses the leak-blocking
   pose), clamped to at least 1.
2. **Repairing a leak reduces the leak while you work.** On hatches and on
   leakable equipment whose interactable object is a hull hole or a pipe, the
   inflow is multiplied by `(1 − repair progress)` for as long as the repair
   continues; the multiplier is removed when the action stops.
3. **A hatch under repair is held locked** (cannot be operated) for every frame
   the repairer is interacting with it.
4. **Parts are scarcer.** Salvage costs twice as much scrap per part as vanilla.
   Dismantling working gear is a (poor) source of scrap: a device's whole
   current health is traded for 1 scrap (2 with `Thrifty`) = 1/4 part (1/2).
5. The original's Repair copied the 2022.1 vanilla repair otherwise (same
   progress formula, parts rule, XP, accomplishment, "already being repaired"
   rule). It therefore **lacks** the 2026.1 vanilla additions listed in §2.1.7;
   the reimplementation must decide per item (see open questions).

---

## 2. Actions

### 2.1 Repair (replaces vanilla `RepairAction1` and `RepairAction2` rows)

Base class: the game's device-use action (`DeviceUseAction`). Delegation to
other characters is allowed (an officer can order a sailor to do it).

#### 2.1.1 Performer and rate parameter

Two data rows (see §4.1) register the same action class:

- Engineer officers, parameter `Performance = 1.0`.
- Radioman and leader (captain/first officer) officers, `Performance = 0.33333`.

Group `Engineer`, subgroup `Repair`, contexts `Normal, Queue`.

#### 2.1.2 Availability (state shown on the interaction wheel)

Evaluated for (activator, character):

1. Hidden if the activator has no associated equipment, or that equipment has
   no interactable component.
2. Run the game's generic activator-use check with "check for blocking items";
   if that yields Disabled or Hidden, return it.
3. If any *other* character currently using this activator is performing a
   Repair, Disabled with the vanilla "already being repaired by {name}" warning.
4. Let H = health, Hmax = max health.
   - **H = 0:** if the equipment's data does not require parts, or a part is
     obtainable (see 2.1.6): check slot usability for `Repair` or
     `Block Leak` (underwater allowed with rebreather); if not usable, check
     `Default`. Attach the vanilla "a replacement part will be consumed" info
     warning when no other warning is present. If parts are required and none
     obtainable: Disabled with the vanilla "replacement parts are required"
     warning.
   - **0 < H < Hmax:** slot check as above (`Repair`/`Block Leak`, then
     `Default`).
   - **H = Hmax** but a visible (non-hidden) `Flaw` status is present: same slot
     check.
   - Otherwise Hidden.

#### 2.1.3 Label (localization keyword)

- No equipment → vanilla key `Repair`.
- Health 0 and repairs consume parts → `Repair (Requires Parts)`.
- Visible flaw present → `Repair (Requires Parts)`.
- Otherwise `Repair`.

(Identical to vanilla. Note the vanilla 2026.1 English text for these two keys
appears swapped; the original overrode both texts so that each key reads
naturally. See §4.2.)

#### 2.1.4 AI desirability and ordering

- Profit when H ≠ Hmax: `0.98 + 0.02 / max(1, d²)` where d is the
  character→activator distance (closer is marginally better). Otherwise −1
  (never auto-chosen).
- Wheel sorting priority 10.

#### 2.1.5 Start

1. If the character has the `Handyman` perk, rate multiplier m = 1.25, else 1.
2. Remember the equipment's `Leaking` status (may be absent).
3. Disallow transition to stage view.
4. Interaction type: from the equipment's `Repair` slot, or `Default` slot if
   no `Repair` slot exists or the equipment is a hatch; values ≤ 0 become 1.
5. Choose the station (all with animation keyword `Repair`):
   hatch with a free `Default` slot → `Default`; else free `Repair` → `Repair`;
   else free `Block Leak` → `Block Leak`; else `Default`.
6. If H = 0, parts are required and none is obtainable: walk to the station but
   immediately complete (cancel) the action. Otherwise add the game's
   `IsBeingRepaired` status to the equipment.

#### 2.1.6 Part source rule

A part is obtainable if the character is carrying an item named
`Replacement Parts` in its backpack slot; otherwise, if the user gameplay
setting *manual item delivery* is **off**, the first `Replacement Parts` cargo
in the ship's main storage; otherwise none.

#### 2.1.7 Progress (each frame while the character is actually interacting)

Rate per second:

`progress_rate = performance × m × character_performance / equipment.RepairTime`

i.e. one health point takes `RepairTime / (performance × m × character_performance)`
seconds. When the accumulated point-progress reaches 1 (carry the remainder):

1. If H = 0 and repairs consume parts: take a part (2.1.6). If none, cancel the
   character's orders and stop. Otherwise consume 1 unit; if the character has
   the `Thrifty` perk, generate 1 `Scrap` in the main storage. (So the part is
   paid for the *first* point only — after that H > 0.)
2. If H < Hmax, H += 1.
3. If a visible `Flaw` status is present, remove it.
4. If H ≥ Hmax: grant the XP value from game data `General/Settings/Experience/Repair`;
   if the equipment consumes parts *and* is leakable, add the vanilla
   "repaired leak in {compartment}" accomplishment (vanilla icon `Iron Cross`,
   weight 3; compartment name via `Activators/<compartment>` or `Activators/Deck`);
   say the generic voice line `Repair Completed`; cancel orders; stop.

Otherwise each frame:

- Set the `IsBeingRepaired` status progress to the current point-progress.
- Overall progress P = `0.999 × total / (floor(total) + remaining)` where
  `total` is cumulative progress and `remaining` = round(Hmax) − round(H), or 1
  if that is ≤ 0 and a visible flaw exists. P drives both the action progress
  bar and the character's skill progress display.
- **Leak throttling (new):** if the equipment is a hatch, or its interactable
  object's name contains "Hole" or "Pipe" (game object naming), create once a
  scale modifier — on the hatch device's performance for hatches, otherwise on
  the leakable equipment's leak intensity — and set its value to `1 − P` each
  frame.
- **Hatch lock (new):** if the equipment is a hatch, set it locked each frame.

Differences from vanilla 2026.1 RepairAction that the original does not have
(decide per open question Q1):

- time scaled by the game's artificial time scale;
- multiplied by the ship's crew-commanding performance modifier;
- NPC characters exempt from part consumption;
- equipment-specific completion voice lines;
- removes `DisallowRepair` on start, and zero profit when `DisallowRepair` is set;
- hides itself while a sailor is blocking the leak by direct player order;
- uses `IFlawStatus` (all flaw kinds) rather than only `Flaw`;
- the vanilla `RepairAction1` row additionally lists flaw activator tags
  (`Time Interval Flaw`, `Pressure Threshold Flaw`, `Usability Flaw`,
  `Damage Vulnerability Flaw`) and there is a sailor row `RepairAction3`.

#### 2.1.8 Stop

Remove the `IsBeingRepaired` status if added; remove the leak-throttle
modifier if created; then default device-use stop.

#### 2.1.9 Edge cases

- If "Hole"/"Pipe" equipment has no `Leaking` status at start, the original
  would fail when creating the throttle (null). Ours must skip throttling then.
- The hatch lock is set while interacting but is never explicitly released by
  this action; the hatch-handling area (spec for hatches) must release it, or
  ours must restore the previous lock state on stop. (Q2. Resolved in 00 C10:
  the hatch is closed once when interaction starts and held by 02 §2.1 step 1
  while `IsBeingRepaired` is visible; nothing reopens it afterwards. The leak
  throttle uses the 02 §2.6 mechanism.)
- Throttle is created on the first frame of interaction and persists until the
  action ends, even if the leak status ends earlier.
- Two characters cannot repair the same activator at once.

### 2.2 Salvage replacement parts (replaces vanilla `SalvageReplacementParts` row)

Base class: the game's single-character action. Interruptible.

- **Performer:** any crew (`*`), soft requirement perk/tag `Salvager`. Group
  `Engineer`, subgroup `Salvage Replacement Parts`, contexts `Normal, Queue`.
- **Where:** the workshop (activator tag `Workshop`), slot `Work`.
- **Availability:** Disabled ("not enough scrap", vanilla key) if the workshop
  storage holds fewer than **4** `Scrap`; Disabled ("storage full", vanilla
  key) if one `Replacement Parts` (mass from the equipment database, default 1)
  cannot be stored in the workshop storage; then activator-use check with
  "disallow duplicate actions" + "check for blocking items"; then `Work` slot
  usability (no special flags).
- **AI profit:** 1 when ≥ 4 scrap in workshop storage, else default.
- **Label key:** `Salvage Replacement Parts` (vanilla).
- **Start:** cancel the character's current orders, use the workshop
  interactable (2.3 s blend), subscribe to behaviour detachment (removing the
  character), say the generic confirmation line unless silent/already speaking.
- **Progress:** while the interaction behaviour is active, character skill
  progress rises at **0.01 per second** (one part per **100 s**; not scaled by
  character performance). On reaching 1: reset to 0, then — in order — require
  room for one part, consume 4 `Scrap` from workshop storage (ignoring the
  manual-delivery setting), generate one `Replacement Parts` there, and require
  ≥ 4 scrap remaining; if any step fails, complete the action. Thus it loops
  producing parts until scrap runs out or storage fills.
- **End:** when the AI behaviour is cancelled or finished (state ≥ cancelled)
  and the AI is active, complete.
- **Stop:** unsubscribe, cancel the behaviour if still attached.
- **Only change vs vanilla:** ratio 4 instead of 2 (both the threshold and the
  consumed amount). Vanilla 2026.1 additionally scales the 100 s by the
  artificial time scale and shows an engineer visual clue; keep those (Q1).
- Interaction with locales: the item description of `Scrap` must state the new
  ratio (§4.2).

### 2.3 Dismantle for parts (new action)

Base class: device-use action.

- **Performer:** Engineer officers. Group `Engineer`, no subgroup, contexts
  `Normal, Queue`.
- **Where (activator tags, game facts):** `Compartment Hatch`, `Engines`,
  `Compressor`, `Trim Pump`, `Valves`, `Depth Steers Station`, `Ventillation`
  (game spelling), `Power Distribution Board Bow`,
  `Power Distribution Board Stern`, `Radio Room`, `Listening Room`,
  `Gyrocompass`, `Torpedo Launcher`, `Deck Gun`, `Flak`, `Pill Thrower`,
  `Targeting Sight`, `Periscope`.
- **Availability:** Hidden if no associated equipment, health < 1, or no
  interactable component; otherwise **always Enabled** — the original performs
  no occupancy, flooding, power, duplicate or "already in use" checks. (A
  disabled draft of such checks existed: activator-use with "require working
  device" + "disallow duplicate actions", then `Default` slot with rebreather
  allowed. Recommend implementing those — Q3.)
- **Label key:** our key `DC Dismantle` (purpose: "dismantle this device to
  recover scrap"). Sorting priority −1 (listed last). No AI profit override
  (never auto-chosen beyond the default).
- **Start:** `Handyman` → m = 1.25; disallow stage view; total work W = current
  health; use station `Default` for hatches else `Repair`, animation `Repair`,
  interaction type from the behaviour (≤ 0 → 1); confirmation voice unless
  silent/speaking.
- **Progress (while interacting):**
  `rate = character_performance × m / equipment.RepairTime` per second (no
  performance parameter). Each full point: if H ≥ 1, H −= 1. When H reaches
  exactly 0: generate 1 `Scrap` in the main storage (+1 more if `Thrifty`),
  complete. Overall progress = `0.999 × total / W`.
- **Result:** the device is left at 0 health — broken, and if its data says
  repairs consume parts, a later repair will need a part.
- **Edge cases:**
  - Non-integer health (e.g. 2.5) never reaches exactly 0 in the original and
    the action never completes. Ours: clamp to 0 when H < 1 and complete.
  - If main storage is full, scrap generation fails silently in the original.
    Ours: try the main storage, then drop/notify (Q4).
  - Dismantling does not apply `IsBeingRepaired`, grants no XP, and does not
    interact with leaks.
- **Interactions:** dismantling a hatch, valves, trim pump or compressor
  disables systems covered by other specs (hatches, ballast, pressure); no
  special handling is done.

### 2.4 Interactions with other areas

- Leak blocking (other spec): both share the `Block Leak` slot and the
  "leak intensity scale modifier" technique; Repair falls back to the
  `Block Leak` slot when `Repair` is occupied.
- Hatches (other spec): Repair locks hatches while working; dismantle targets
  `Compartment Hatch`.
- Settings toggles (other spec) live on the `Settings` activator (§6).

---

## 3. Bootstrap

At mod load (the game's `IUserMod.OnLoaded`):

1. Log a load line with the mod version.
2. Create a Harmony instance with a unique id and apply all patch classes in the
   assembly. (2022.1 shipped Harmony 1.x API; 2026.1 ships `0Harmony` 2.2.2 —
   use the `HarmonyLib` API.)
3. Subscribe to the game's scene events `SceneEventsListener.OnSceneAwake` and
   `OnSceneStart`.
4. Every step is wrapped so an exception is logged and never propagates into
   the game.

On every scene awake: ask the game's injection framework to inject into our
assembly (`InjectionFramework.Instance.InjectIntoAssembly(...)`) so that static
`[Inject]` fields (player ship, user settings, equipment database, locale,
resource manager) are populated.

On scene start, only for the scene named `Main Scene`: reorder GUI siblings
under `GUI/Viewport` so that, from back to front, the order is
`Task Markers` → `Scene Notifications` → `Activator Banners 2D` →
`InteractionWheelUI`. Purpose: the interaction wheel draws above activator
banners, and banners above scene notifications. (Verify these object paths
still exist in 2026.1 — Q5.)

Dead/disabled code in the original (do **not** implement unless separately
specified): spawning the bundled "Main Pump" prefab into the control room,
a diesel smoke effect, a fire effect test.

**Settings storage.** The original keeps its three boolean player settings
(hatch pressure blocking, automatic leak blocking by sailors, automatic
carrying of wounded) in the save game by presence/absence of a *named scale
modifier* on the player ship's energy-gain parameter (value irrelevant; the
parameter is saved with the ship). Toggle = add the modifier if absent,
remove if present; read = "is the named modifier present". Custom Actions 3.2
uses the same trick for its own flags. ~~Recommendation: reuse the trick.~~
**Superseded (00 C4):** this section owns settings persistence. It uses a
`[Serializable]` subclass of `UBOAT.Game.Core.BackgroundTaskBase` declared in the
mod's `General.xlsx`, saved with the campaign, and does not touch gameplay
parameters. Q6 is moot.

Debug logging: the original has a compile-time debug flag that is left on.
Ours: logging behind a runtime flag, off by default.

Manifest (`Manifest.json`, game facts for the format): `name`, `version`,
`description`, `author`, `supportedGameVersions` (original listed b131 and
2022.1 through Patch 8; ours must list `2026.1`), `assemblyName`,
`permissions: ["Reflection"]` (required for Harmony), `steamFileId`.
The original declares no dependency field even though it needs Custom Actions.

---

## 4. Data sheets

UBOAT merges rows from `Data Sheets/*.xlsx` in a mod into the same-named game
workbook/sheet; a row whose key (column A) matches a vanilla row replaces it,
new keys are appended (observed behavior — verify, Q7). Sheet headers must
match the game's.

### 4.1 `Crew.xlsx` → sheet `Actions` (game sheet `Crew/Actions`)

Columns (game facts): A `/` (row key), B `Type` (full .NET type name), C
`Activator Tags`, D `Character Tags`, E `Character Tags (Soft Req)`, F
`Contexts` (`Normal`, `Queue`, `NoCharacter`, `Upgrade`), G `Group`, H
`Subgroup`, I `…` (parameters, `Name = value`).

Vanilla's loader resolves column B with plain `Type.GetType(name)`, which only
finds types in the game assembly; rows whose type is not found are silently
skipped. That is why mod action types need either Custom Actions (§6) or an
assembly-qualified type name (Q8). A deliberately unresolvable type (the
original prefixes the type with a stray letter) is used to *disable* a row.

Rows in this spec's area:

| Row key | Overrides vanilla? | Type | Activator tags | Char tags | Soft req | Contexts | Group / Subgroup | Params | Purpose |
|---|---|---|---|---|---|---|---|---|---|
| `RepairAction1` | yes | our Repair | `Damaged` | `Engineer` | `*` | Normal, Queue | Engineer / Repair | Performance = 1.0 | full-rate engineer repair |
| `RepairAction2` | yes | our Repair | `Damaged` | `Radioman, Leader` | `*` | Normal, Queue | Engineer / Repair | Performance = 0.33333 | slow repair by non-engineer officers |
| `SalvageReplacementParts` | yes | our Salvage | `Workshop` | `*` | `Salvager` | Normal, Queue | Engineer / Salvage Replacement Parts | – | 4:1 salvage |
| our `DCDismantleForParts` | new | our Dismantle | list in §2.3 | `Engineer` | `*` | Normal, Queue | Engineer / – | – | dismantle for scrap |

Note for 2026.1: overriding `RepairAction1` with tag `Damaged` only *drops*
the vanilla flaw tags from engineer repair; ours should keep them (Q1). Vanilla
`RepairAction3` (sailor repair) remains a vanilla-type row unless we also
override it.

Other rows in the original's sheet, owned by other specs but recorded here so
the sheet is complete (one-line purpose each):

| Row key | Overrides vanilla? | Activator tag | Purpose |
|---|---|---|---|
| `CompressedAirValvesUseAction`, `CompressedAirValvesEmergencyUseAction`, `AdjustBallastAction`, `FloodBallastTanksAction` | yes, unchanged vanilla type | `Valves` | re-listed so the mod's new valve actions sit between them in wheel order |
| our blow-ballast-with-diesel-exhaust action | new | `Valves` | (ballast spec) |
| our adjust-ballast-for-deck-awash action | new | `Valves` | (ballast spec) |
| `BlockLeakAction` | yes, mod type | `Leaking` | officer leak blocking (leak spec) |
| our block-leak-by-sailor action | new, soft req `Sailor`, contexts incl. NoCharacter | `Leaking` | sailor leak blocking (leak spec) |
| `HatchOpenCloseAction` | yes, mod type | `Hatch` | hatch spec |
| our equalize-pressure action | new | `Hatch` | pressure spec |
| our trigger-auto-carry-wounded action | new | `Light Switch` | crew-care spec |
| our pump-water-to-control-room action | new, **disabled** | `Compartment` | unfinished; omit |
| debug variant of dismantle | new, **disabled** | `Device`, … | omit |
| three settings toggles | new | `Settings` | §3 settings; need the `Settings` activator from §6 |

### 4.2 `Locales.xlsx`

Mod workbook sheets: `Parameters`, `Items`, `Activators`, `Tooltips`, `UI`,
`Statements` (only `Items` and `Activators` have rows). Columns: `Key`, then
one column per language (English, German, Polish, French, Italian, Spanish,
Russian, Chinese, Korean, Turkish, Portuguese, Czech, Japanese; 2026.1 adds
Ukrainian). Sub-tables inside a sheet start with a row whose key is
`/<Name>`; lookups are `<Sheet>/<Name>/<Key>` (e.g. `Actions/RepairAction/...`).
All text is ours to write; at minimum English, other languages may fall back
to English.

Keys **we must provide or override** (this area):

| Sheet | Key | Vanilla? | Meaning |
|---|---|---|---|
| Items | `Scrap Description` | override | item tooltip for scrap; must state engineers turn 4 scrap into 1 spare part |
| Activators | `Repair` | override | wheel label for an ordinary repair (no parts needed) |
| Activators | `Repair (Requires Parts)` | override | wheel label for a repair that will consume a spare part |
| Activators | `DC Dismantle` (ours) | new | wheel label: take the device apart to recover scrap |

Vanilla keys this area *uses* unchanged (no row needed):
`Actions/RepairAction/Already Being Repaired` ({0} = character name),
`Actions/RepairAction/Replacement Parts Info`,
`Actions/RepairAction/Replacement Parts Warning`,
`Actions/SalvageReplacementParts/Not Enough Scrap`,
`Actions/SalvageReplacementParts/Storage Full`,
`Activators/Salvage Replacement Parts`,
`UI/Accomplishments/Repaired Leak` ({0} = compartment),
`Activators/<compartment name>`, `Activators/Deck`,
`Activators/<activator object name>` (used in the action's state description
"device – compartment").

Other keys in the original's sheet (owned by other specs; listed so nothing is
lost): `Activators/Block Leak` and `Block Leak Description` (override: leak
blocking label and its effect, a sailor reduces inflow by a percentage);
`Activators/Open`, `Activators/Close` (re-declared vanilla labels for the
hatch action); `Activators/Replacement Parts` (label for the parts item when
shown as an activator/sprite name); new labels for: equalize pressure between
compartments, carry wounded to beds, block leak by regular sailor, adjust
ballast for deck awash, blow ballast using diesel exhaust, pump water to
control room, and a "main pump" description stating the feature is
unfinished. The settings toggles' enable/disable labels are referenced by
code but have no locale rows in the original (they render as raw keys) — ours
must add six rows (enable/disable × three settings).

---

## 5. Asset bundle

The original ships one Unity AssetBundle; its manifest lists assets under a
package root, and the game resolves them by the path *after* the package
root (e.g. `Icons/InteractionWheelUI/<label key>`). Icons are looked up by the
action's localization keyword. Categories (one PNG per action label):

| Folder (resource key prefix) | Purpose | Variants |
|---|---|---|
| `Icons/InteractionWheelUI/` | wheel button icon | normal, `(Disabled)`, `(Highlighted)` |
| `Icons/Actions/` | action icon in character order list | normal |
| `Icons/Actions - Cursor/` | cursor icon when issuing the order | normal |
| `Icons/Actions (Banners)/` | icon on activator banners | normal |
| `Icons/Statuses (Portraits)/` | status icon on the character portrait | normal |

Icons in this area: **Dismantle** has the full set (wheel ×3, actions,
cursor, banner, portrait). Repair and Salvage use vanilla icons (vanilla keys).
Other icons in the bundle belong to other specs: equalize pressure (full set),
blow ballast with diesel exhaust (full set), adjust ballast for deck awash
(full set), pump water to control room (wheel ×3), carry wounded to beds
(wheel ×3), block leak / block leak by sailor (wheel), keep hatches
closed / leave hatches open (wheel), and enable/disable icons for the three
settings toggles (wheel). Plus `Equipment/Main Pump` prefab — unused; omit.

All art must be newly drawn.

**Can runtime PNGs replace the bundle?** Yes for icons, with a hook: load PNGs
from the mod folder at startup (`Texture2D.LoadImage` → `Sprite.Create`) and
answer the game's resource lookups for our keys — either by inserting into
the game's `DWS.Common.Resources.ResourceManager` cache or by a Harmony
postfix on its sprite/asset load method returning our sprite for our keys.
This removes the Unity-editor build step. Risks: the resource manager's
internal API must be inspected in 2026.1; sprite import settings (pivot,
pixels-per-unit, mip maps, compression) must match what the UI expects.
The prefab cannot be replaced by PNGs but is not needed. Fallback: build a
bundle with the Unity version 2026.1 uses.

---

## 6. Custom Actions dependency contract (utility mod, v3.2, 2026.1)

Services it provides to dependent mods:

1. **Cross-assembly action import.** It replaces the game's
   `CharacterActionsDatabase.LoadActions` (prefix returns false, postfix
   reimplements it). Same row parsing as vanilla (columns A,B,C,D,F,G,H; the
   soft-requirement column E is not read by the loader), but the type in
   column B is searched in **every loaded assembly**, so a dependent mod only
   has to (a) ship a public, `[Serializable]` `CharacterAction` subclass with
   the two constructors the game uses — one taking a `GameDataRow`, one copy
   constructor taking (source, activator) — and (b) add a `Crew/Actions` row
   whose `Type` is the class's full name. Rows whose type is not found are
   skipped. Last row with a given key wins.
2. **`Settings` activator.** On loading any scene whose name contains
   "Interior", it builds (in code, no prefab) a clickable "Toolbox"
   interaction point in the `Stern Torpedo Room` beside the workshop, tagged
   `Settings`, with banner settings copied from the workshop activator, and
   suppresses the wheel's default action on it. Any row with activator tag
   `Settings` therefore appears on this toolbox. Contexts must include
   `NoCharacter` for toggles usable without a selected crewman (it forces this
   for its own two toggles).
3. Its own toggles: developer mode, developer console, verbose logging; flag
   storage via named scale modifiers on ship energy gain (same as §3).
4. Patches also `BaseActivatorBanner.ValidateDefaultSprites` (resource keyword
   for the toolbox banner) and `InteractionWheelUI.FilterActions`.
5. Load order does not matter; it uses Harmony id distinct from ours.

(Architect note: superseded by user decision 00 D2. The recommended route is our own
registration through a `LoadActions` postfix that adds only our rows and coexists with
Custom Actions.) Original recommendation: **declare Custom Actions as a required dependency** rather
than re-implementing it — it is maintained for 2026.1 and replacing
`LoadActions` in two mods would conflict. Alternative to evaluate (Q8): put
assembly-qualified names in column B so vanilla `Type.GetType` resolves our
types without Custom Actions; we would then still need our own `Settings`
activator (or move toggles elsewhere).

---

## 7. Game integration points

2022.1 names (as used by the original) → 2026.1 status (verified against the
2026.1 `com.uboat.game.dll` unless marked "verify").

| Purpose | 2022.1 | 2026.1 |
|---|---|---|
| mod entry | `UBOAT.Game.IUserMod.OnLoaded` | same |
| scene events | `SceneEventsListener.OnSceneAwake/OnSceneStart` | `DWS.Common.InjectionFramework.SceneEventsListener` (also `OnSceneDestroy`) |
| injection | `DWS.Common.InjectionFramework.InjectionFramework.Instance.InjectIntoAssembly`, `[Inject]` | same |
| Harmony | `Harmony.HarmonyInstance` (1.x) | **remap** to `HarmonyLib.Harmony` (0Harmony 2.2.2) |
| activator | `UBOAT.Game.Activators.Activator` | **remap** `UBOAT.Game.Scene.Characters.Activators.Activator` |
| action state types (`ActionStateData`, `ActionState`, `ActionProfit`) | `UBOAT.Game.Activators.Actions` | **remap** — verify namespace |
| `CharacterAction.GetState(activator, character, fillDetails, isPlayerOrder)` | 4 args | **remap**: 5th arg `ActionContext` |
| base actions | `DeviceUseAction`, `SingleCharacterAction` (`UBOAT.Game.Scene.Characters.Actions`) | same |
| action loader | `CharacterActionsDatabase.LoadActions` | `UBOAT.Game.Scene.Characters.Actions.CharacterActionsDatabase` (private) |
| helper calls | `UseDevice(char, slot, anim, type, …)`, `CheckActivatorUseConditions`, `CheckInteractableUseConditions(interactable, slot[, slot2], char, SlotUsageFlags, …)`, `CompleteAction`, `RemoveCharacter`, `Character.Use`, `CancelOrders(delay)`, `Say(key)` | present; signatures changed (optional args, `PlayConfirmationStatement`, `CancelOrders()`) — verify |
| flags | `ActivatorUseFlags.CheckForBlockingItems`, `DisallowDuplicateActions`, `RequireWorkingDevice`; `SlotUsageFlags.AllowUnderWaterWithRebreather` | present (+ `AlwaysAllowSailorWithoutOfficer`, `SlotUsageFlags.None`) |
| equipment | `Equipment.Health/MaxHealth/Data.RepairTime/Data.RepairConsumesParts`, `AddStatus/GetStatus/RemoveStatus` | present (MaxHealth now integer-typed) |
| statuses | `IsBeingRepaired`, `Flaw`, `Leaking` (`UBOAT.Game.Scene.Items.Statuses`) | present; vanilla now uses `IFlawStatus`; `DisallowRepair` new |
| leak / hatch | `LeakableEquipment.LeakIntensity` (parameter with scale modifiers), `.Compartment`; `Hatch.Locked`; `Device.Performance` | present |
| storage | `StorageUtility.GenerateItem/ConsumeItem/CanStoreItem`, `Storage.GetTotalCount/GetItem`, `Cargo.Consume`, `PlayerShip.MainStorage`, `Workshop.Storage` | present |
| data | `GameDataRow.GetParameter`, `GameDataReference("General/Settings/Experience/Repair")`, `EquipmentDatabase.GetEquipmentByName` | present |
| character | `PlayableCharacter.Backpack.Item`, `.Performance`, `.SkillProgress`, `.IsInteracting`, `Data.HasStatus`, `Data.AddExperiencePoints`, `Data.AddAccomplishment` | present |
| settings | `UserSettings.GameplaySettings.manualItemDelivery` | present |
| GUI objects | `GUI/Viewport/{Task Markers, Scene Notifications, Activator Banners 2D, InteractionWheelUI}` in `Main Scene` | verify |
| resources | `DWS.Common.Resources.ResourceManager` | present |

---

## 8. Acceptance criteria

Offline-checkable (unit tests against pure functions / data files):

1. Repair point time = `RepairTime / (performance × m × charPerf)`; with
   `Handyman` m = 1.25; data rows give 1.0 (engineer) and 0.33333
   (radioman/leader).
2. A repair from H = 0 on parts-consuming equipment consumes exactly one part
   for the whole repair; with `Thrifty` it also yields one scrap.
3. Repair from 0 with parts required and no part (backpack empty, and manual
   delivery on or main storage empty) is Disabled with the parts-required
   warning; with manual delivery off and a part in main storage it is enabled.
4. Overall progress formula P matches §2.1.7 and never reaches 1 before
   completion.
5. Leak throttle value = 1 − P while repairing a hatch/hole/pipe; it is removed
   on stop; equipment without `Leaking` does not throw.
6. Slot preference: hatch → `Default`; else first free of `Repair`, `Block Leak`,
   `Default`; interaction type ≥ 1.
7. Salvage: disabled below 4 scrap; each cycle (100 s at default time scale)
   removes 4 scrap and adds 1 part; stops when < 4 scrap remain or storage is
   full.
8. Dismantle: hidden at health < 1; consumes health 1/point at repair speed
   (×1.25 `Handyman`); at 0 health yields 1 scrap (2 with `Thrifty`) and ends;
   non-integer health still terminates.
9. `Crew.xlsx` contains the four rows of §4.1 with exact game-fact columns;
   every `Type` resolves in our built assembly (test by reflection).
10. Every localization keyword returned by our actions has an English row in
    `Locales.xlsx`; `Scrap Description` states the 4:1 ratio.
11. Every icon key our actions request (wheel ×3, actions, cursor, banner,
    portrait for Dismantle) exists as a PNG/bundle asset.
12. `Manifest.json` lists `2026.1`, `Reflection` permission, our assembly name.
13. No file in the repository contains text or identifiers copied from the
    original mod (clean-room review).

In-game (manual):

14. Mod loads with no exceptions in the log; Harmony patches applied once.
15. With Custom Actions enabled, Dismantle appears on listed devices for an
    engineer and Repair on damaged devices; with it disabled, our actions are
    absent and nothing crashes (or, if Q8 is adopted, still present).
16. Interaction wheel renders above activator banners in the main scene.
17. Hatch under repair cannot be operated; after repair it can.
18. Saving and loading preserves the three settings toggles.

## Open questions

- **Q1** Adopt 2026.1 vanilla repair improvements (time scale, commanding
  modifier, NPC exemption, specific voice lines, `DisallowRepair`,
  `IFlawStatus`, flaw activator tags, sailor `RepairAction3`)? Recommended: yes,
  keep the original's three changes (slot fallback, leak throttle, hatch lock).
- **Q2** Who releases the hatch lock after repair (this action or the hatch
  spec)? Recommended: restore prior lock state on stop.
- **Q3** Add use-condition checks to Dismantle (occupied, flooded, working
  device, duplicates)? Recommended: yes.
- **Q4** Where does scrap go when main storage is full?
- **Q5** Do the four GUI object paths and the draw-order problem still exist
  in 2026.1? If the wheel already draws on top, drop the reorder.
- **Q6** Is an unvalued scale modifier on ship energy gain neutral (factor 1)
  in 2026.1, and is it saved?
- **Q7** Confirm row-merge semantics (same key replaces vanilla row in place
  vs appends) — affects wheel ordering.
- **Q8** Do assembly-qualified type names in `Crew/Actions` resolve under the
  game's Mono runtime, letting us drop the Custom Actions dependency?
- **Q9** Keep the 4:1 salvage ratio as a tunable (config) rather than a
  constant? Recommended: constant 4 with a single named setting.
