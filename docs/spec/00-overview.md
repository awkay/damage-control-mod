# Spec 00: Overview, architecture, conflicts, decisions and work plan

Status: architect's synthesis of specs 01 to 04. It was checked against the decompiled 2026.1
game (`com.uboat.game`). The original mod was not consulted. If this document and a
section spec disagree, **this document wins**. Section specs keep the detailed behavior,
formulas and acceptance criteria. This document decides scope, ownership, hook strategy and
conflict resolution.

Feature IDs: **P** = 01 pressure/air/buoyancy, **H** = 02 hatches/leaks/crew,
**B** = 03 ballast/depth/navigation, **R** = 04 repair/parts/bootstrap/data.

A recurring finding: 2026.1 vanilla already does several things that the 2022.1-era original
had to add (hatch open/close rules, leak-blocking modifier, deck-awash ballast modes, buoyancy
percent and trend arrows, compressor auto-off, dive-plane animation, valve actions usable
under water). Where vanilla now covers a feature, it is classified **drop (vanilla)**. The mod
then relies on the game and does not re-implement it.

---

## 1. Scope of v1

Classes: **v1** = build now. **later** = specified, not scheduled. **drop** = do not build.

### 1.1 Pressure, air, buoyancy (spec 01)

| ID | Feature (spec ref) | Class | Note |
|---|---|---|---|
| P1 | Breached compartment keeps a 0.1 air floor (01 §2.1) | v1 | prefix on private `Tank.SimulateBreach(ExternalPressure, bool)` (2026.1 signature) |
| P2 | Unbreathable-air rule and toxicity value (01 §2.2) | v1 | keep the vanilla `mergedCompartment` delegation; toxicity semantics resolved in §3 C18 |
| P3 | Rebreather endurance 2.5x (01 §2.3) | v1 | 2026.1 rebreather is an item with a `Durability` parameter: one 2.5x scale modifier, no per-frame patch |
| P4 | Interior-hatch pressure point reports \|p_L - p_R\| (01 §2.4) | v1 | postfix on `CompartmentPressure.Pressure` |
| P5 | Pressure-damage status bookkeeping (01 §2.5) | drop (vanilla) | the 2026.1 setter is already idempotent and attaches to `Target` |
| P6 | Compartment 1 s tick: bulbs, overstress, slot underwater with neighbours (01 §2.6) | v1 | whole-method replacement kept, see §4 |
| P7 | Interaction-slot water tolerance and pose tuning (01 §2.7) | v1 | the prefix must run before the 2026.1 sorted insert (it does) |
| P8 | Hole/pipe/hatch allow 2 characters (01 §2.8) | v1 | **owner 02** (leaks), built in WS3 |
| P9 | Atmospheric venting and diesel air draw (01 §2.9) | v1 | postfix on `PlayerShip.FixedUpdate` |
| P10 | Compressor draws air from its compartment (01 §2.10) | v1 | prefix gate plus postfix draw, no replacement (§4) |
| P11 | Equalize-pressure crew action (01 §2.11) | v1 | uses the shared hatch exchange kernel (owner 02 §2.3) |
| P12a | Buoyancy label, trend arrow and severity icon (01 §2.12) | drop (vanilla) | 2026.1 shows %, arrows and icons with its own buoyancy model |
| P12b | Buoyancy tooltip breakdown: ballast and compartment flooding (01 §2.12 items 1 to 4) | v1 | postfix appends to vanilla `GetTooltipContents`; tanks found by identity, not position |
| P12c | Tooltip diagnostic block (01 §2.12 item 5) | later | behind the debug flag (R14) |
| P13 | Water-storage pressure readout (01 §2.13) | v1 | postfix; show it always and drop the "mass > 100" proxy |
| P14 | Hatch-pressure-blocking toggle (01 §4) | v1 | stored by R9; the toggle action is owned by WS0 |

### 1.2 Hatches, leaks, crew (spec 02)

| ID | Feature (spec ref) | Class | Note |
|---|---|---|---|
| H1 | Pressure-lock "can be opened" rule (02 §2.1) | v1 | postfix; active only while P14 is on; when off, the 2026.1 vanilla rule stands (C25) |
| H2 | Lock-state guard on `Hatch.Locked` setter (02 §2.2) | v1 | use `Hatch.IsExteriorHatch`, not the object name |
| H3 | Hatch disabled: stay shut and leak if pressure holds (02 §3 `OnDisable`) | v1 | prefix and postfix |
| H4 | Hatch air/water exchange: open, closed-leaking, vertical passage (02 §2.3 b, e, f, g) | v1 | replaces `Hatch.DoUpdate`, see §4; **owner of the exchange kernel** |
| H5 | Destroyed-hatch blow-open and leak-status upkeep (02 §2.3 c, d) | v1 | per-second hazard, not per-frame (D7) |
| H6 | Hatch info panel (02 §2.4) | v1 | postfix rewrites `GetStateText`; debug lines only behind R14 |
| H7 | Hatch open/close action (02 §2.5) | drop (vanilla) | 2026.1 `HatchOpenCloseAction` already disables Open with a reason, toggles instantly in first person, stores the preferred state, clears evacuation and has the same profit curve |
| H8 | Leak-block effect, including hatch seals (02 §2.6) | v1 | **owner of the leak-blocking mechanism**; vanilla already applies the 0.2 modifier to leakable equipment; the mod adds hatch support (C9, C28) |
| H9 | Officer block-leak row override (02 §2.7) | drop (vanilla) | 2026.1 `BlockLeakAction` matches it: same pose map, 0.2 modifier, same cancel rules, not interruptible |
| H10 | Sailor self-initiated block-leak action (02 §2.8) | later | the vanilla auto routine dispatches vanilla `BlockLeakAction` directly (C28) |
| H11 | First-person interaction wheel offers orders on leaks (02 §2.9) | later | |
| H12 | Auto-leak-blocking toggle gating `LeakBlockingBySailors` (02 §2.10, §4) | v1 | also listed in 03 §2.G, **owner 02** |
| H13 | Auto-carry-wounded toggle and "carry wounded now" trigger (02 §2.10) | v1 | the gate must not suppress the 2026.1 diver pull-up in the same method (C27) |
| H14 | Trim pump not auto-switched off while any leak exists (02 §2.11) | v1 | narrow postfix on the vanilla controller; **no controller replacement** (C7) |
| H15 | Other controller parts: compressor and ventilation auto-off, valve queueing (02 §2.11) | drop (vanilla) | 2026.1 does compressor, ventilation (by oxygen), lighting, exterior hatch and deck-awash queueing |
| H16 | Trim-pump energy draw override (02 §2.11) | later | no player problem stated; check the 2026.1 value first |

### 1.3 Ballast, depth, navigation (spec 03)

| ID | Feature (spec ref) | Class | Note |
|---|---|---|---|
| B1 | Explicit ballast-intent state that replaces target-depth sentinels (03 sentinel table, §7.8) | later | needed only for B2 automation (D5) |
| B2 | *Exhaust Blow* action (03 §2.A) | v1 | manual valve-station order only, with the diesel-running and deck-above-water gates of 03 §7.4 |
| B2a | Automatic queueing of *Exhaust Blow* (03 §2.I step 2) | later | through B1, never through a magic depth |
| B3 | *Awash Trim* (03 §2.B) | drop (vanilla) | 2026.1 has `DepthPreset.DeckAwash`, `FloodingDeckAwash` and `BlowingDeckAwash` valve modes, and partial flood/blow actions |
| B4 | Flooding/blowing valve physics override (03 §2.C) | drop | served only B3; vanilla 2026.1 adjusts to neutral from real buoyancy |
| B5 | Valve-action gating replacement (03 §2.D) | drop (vanilla) | 2026.1 already uses "allow under water always"; replacing it would lose the 2026.1 mode and docked hiding |
| B6 | Adjust-ballast sentinel reset (03 §2.D) | drop | no sentinels |
| B7 | Hydroplane visuals (03 §2.E) | drop (vanilla) | 2026.1 animates the planes (`displayed*DepthSteersRotation`) |
| B8 | Surface depth bias (03 §2.E) | later | |
| B9 | Reachability blocked by hatches that cannot be opened (03 §2.F) | v1 | postfix on vanilla `IsDestinationReachable`, which already handles evacuation |
| B10 | Cancel orders at door/ladder traverse through a locked hatch (03 §2.F) | v1 | prefix; exterior hatch via `IsExteriorHatch` |
| B11 | Evacuation order can be cancelled (03 §2.F) | v1 | prefix on `EvacuateToSafetyAction.Escape` |
| B12 | Officers exempt from compartment evacuation handling (03 §2.F) | later | intent unclear (03 §7.13); 2026.1 already exempts some leak blockers |
| B13 | Automation gates (03 §2.G) | dup of H12, H13 | owner 02 |
| B14 | Control-room main pump (03 §2.H) | drop | |
| B15 | Valve automation replacement (03 §2.I) | drop | see H15 and B2a |

### 1.4 Repair, parts, bootstrap, data (spec 04)

| ID | Feature (spec ref) | Class | Note |
|---|---|---|---|
| R1 | Repair slot fallback, Repair then Block Leak then Default, Default for hatches (04 §2.1, items 1 and 5) | v1 | prefix on `DeviceUseAction.UseDevice` scoped to `RepairAction`; vanilla repair rows are kept (D4) |
| R2 | Repair throttles the leak by (1 - progress) (04 §2.1.7) | v1 | postfix on `RepairAction.UpdateAction` and `StopMainCharacter`, through the H8 mechanism |
| R3 | Hatch held shut while under repair (04 §2.1.7) | v1 | resolved in C10 |
| R4 | Other repair behavior: rates, parts, XP (04 §2.1) | drop (vanilla) | keep the 2026.1 `RepairAction` (D4) |
| R5 | Salvage at 4 scrap : 1 part (04 §2.2) | v1 | transpiler swaps the three `2f` constants, see §4 |
| R6 | Dismantle-for-parts action (04 §2.3) | v1 | with the use-condition checks of 04 Q3 |
| R7 | Bootstrap: Harmony, injection, exception isolation (04 §3) | v1 | |
| R8 | GUI sibling reorder (04 §3) | later | only if the 2026.1 wheel still draws under banners (04 Q5) |
| R9 | Per-campaign settings store (04 §3) | v1 | `[Serializable]` background task, not modifiers (C4) |
| R10 | Mod action registration and toggle host activator (04 §4.1, §6) | v1 | per D2 |
| R11 | Locales (04 §4.2) | v1 | English required, other languages fall back to English |
| R12 | Icons as runtime PNGs (04 §5) | v1 | fallback: an asset bundle |
| R13 | Manifest (04 §3) | v1 | |
| R14 | Runtime debug flag for logging and diagnostic text (04 §3) | v1 | off by default |

---

## 2. Architecture

### 2.1 Layers

```
mod/Source/
  Core/      pure C# 7.0: formulas, rules, state machines, settings model.
             No UnityEngine, Harmony or game references. Unit-tested offline.
  Game/      thin adapters: Harmony patches, game-object lookups, actions
             (CharacterAction subclasses), the settings background task.
             It reads game state into plain structs, calls Core, and writes results back.
mod/Data Sheets/   General.xlsx (background task row), Crew.xlsx (Actions rows), Locales.xlsx
mod/Icons/         PNGs loaded at runtime (R12)
```

Rules:

1. **Core is pure.** Inputs are plain values or small structs, such as `HatchSide { AirPressure,
   Air, Water, Capacity, WaterElevation }`. Outputs are deltas or decisions. Randomness and time
   are injected (an `IRandom` interface and a `dt` argument), so tests are deterministic.
   Probabilities are specified per second and converted with `1 - (1-p)^dt` (D7).
2. **Adapters are thin.** A patch reads, calls Core, writes, and contains no formula. Each
   patch body is wrapped in try/catch. On an exception it logs once per patch per session and
   falls back to vanilla: prefixes return `true`, postfixes leave `__result` untouched.
3. **Prefer additive patches.** Use a prefix or postfix on the narrowest member. Whole-method
   replacements (a prefix returning `false`) are allowed only where §4 says so. Transpilers
   only swap a constant or a call, using `CodeMatcher`, and fail closed: if no match is found,
   log and leave the method unpatched.
4. **No positional or name-based identity.** Tanks, compartments, hatches and planes are
   identified by component type and game flags, such as `Hatch.IsExteriorHatch`,
   `PlayerShip.BallastTanks`, `PlayerShip.HatchesLeftToRight` and `PlayerShip.HatchToConningTower`.
   Interior heights (01 §7.6, 03 F) come from hatch geometry (`lowY` and `highY` in 02
   conventions). One adapter, `Game/Infrastructure/BoatLayout`, builds these per boat when the
   player ship changes. An unknown layout disables the dependent feature with one log line
   and never throws.
5. **One clock.** 2026.1 measures game time as `Time.deltaTime × gameTime.ArtificialTimeScale`.
   The effective compression is `Time.timeScale × ArtificialTimeScale`, which is what vanilla
   `Hatch.DoUpdate` uses. Core `SimClock` exposes `gameDt` (capped per feature as each spec
   states) and `compression`. Every "frame delta" and "time scale" in specs 01 to 03 means
   these values (C2).
6. **Language level.** Use C# 7.0: the game compiles with Roslyn 2.10 at LanguageVersion Default (see DEVELOPMENT.md). The game
   compiles `Source/**` at load, so Core and Game ship as source. Tests compile Core outside
   the game, so Core must not use any API that the game's reference list lacks.

### 2.2 Per-campaign state

- `Game/Settings/DamageControlSettingsTask`: a `[Serializable]` subclass of
  `UBOAT.Game.Core.BackgroundTaskBase` (the 2026.1 namespace; see C29), declared in the mod's
  `General.xlsx` background-tasks sheet. The game resolves it through the mod manager. It holds
  `HatchPressureBlocking`, `AutoLeakBlocking` and `AutoCarryWounded`, with defaults per D3.
  A save made before the mod was installed gets the defaults. Removing the mod leaves no
  trace in gameplay parameters.
- Only the task stores fields. Core `SettingsModel` holds the logic: toggle, label for the
  next action, and defaults.
- Session-only state stays static and is reset on each campaign load (`Resume`/`Start`):
  the carry-wounded one-shot (02 §7.12 fixed) and per-action "interaction started" flags.

### 2.3 Actions and data

- Each action is a public `[Serializable]` `CharacterAction` subclass with the
  `(GameDataRow)` and `(source, Activator)` constructors.
- Registration follows D2. The recommended route is a postfix on the private
  `CharacterActionsDatabase.LoadActions`. Vanilla resolves column B with `Type.GetType`, which
  sees only game types; background tasks, by contrast, resolve through the mod manager. The
  postfix re-reads `Crew/Actions` rows whose type vanilla failed to resolve, resolves them
  against **our assembly only**, and registers them the same way vanilla does (by name and by
  activator tag). It skips any name already registered, so it coexists with Custom Actions.
- Data sheets and locale rows are contributed per workstream (§6). No workstream edits a
  shared `.xlsx` by hand.

### 2.4 Icons

PNGs under `mod/Icons/<resource path>.png` are loaded at startup into `Sprite`s, then served
by a postfix on the `DWS.Common.Resources.ResourceManager` sprite lookup for our keys only.
The exact 2026.1 hook point and import settings are a WS0 task, recorded in
`docs/GAME-API-NOTES.md`. If raw loading proves impossible, the fallback is an asset bundle.

### 2.5 Owners of shared concerns

| Concern | Single owner | Consumers |
|---|---|---|
| Leak-blocking mechanism: hatch seals and leak-intensity modifiers | **02 §2.6** (H8). Core `HatchSeals` registry and `LeakThrottle` helper | 02 §2.3e (hatch leak factor), 02 §2.4 (panel line), 04 §2.1.7 (repair throttle) |
| Automatic actions controller | **02 §2.11** (H14): one narrow postfix, no replacement | 03 §2.I (valve parts dropped; B2a later goes through B1, not the controller) |
| Hatch air/water exchange kernel | **02 §2.3** (H4): Core `HatchExchange` | 01 §2.11 equalize (P11) calls it with its own rate and conductance constants |
| Water-head units, metres of water to atm | **02 §2.1**: one Core constant `AtmPerMetreOfWater` (value per D1) | 01 §2.11, 02 §2.1, §2.3, §2.4 |
| Ballast-intent state | **03** sentinel table, implemented later as B1 (Core `BallastIntent` saved in the settings task) | B2a, B8 |
| Settings and persistence | **04 §3** (R9) | 01 §4, 02 §4, 03 §4 |
| Action registration and the toggle host activator | **04 §6** (R10) per D2 | all actions |
| Interaction-slot crowding | **02** (P8) | 01 §2.8 only describes it |
| Automation gates | **02 §2.10, §4** (H12, H13) | 03 §2.G is a duplicate |
| Boat geometry (heights, tank roles, hatch order) | **WS0** `BoatLayout` | 01, 02, 03 |
| Locales and icons pipeline | **04 §4.2, §5** | per-feature strings and art come from each workstream |
| Debug flag | **04 §3** (R14) | P12c, H6 debug lines, leak state text |

---

## 3. Cross-section conflicts and duplications

| # | Where | Conflict | Resolution |
|---|---|---|---|
| C1 | 01 §2.11 water step, §7.5; 02 §2.1, §2.3g, §2.4, §7.1 | Water head is added to atm 1:1 in logic, but the panel divides by 10 | **Decision D1.** Whichever unit is chosen applies everywhere through `AtmPerMetreOfWater`. Logic and panel must agree. |
| C2 | 01 §2.11 (Unity frame delta, Unity time scale); 02 §2.3b ("game time scale"); 03 (scaled delta) | Different time bases | Use `SimClock` (§2.1 rule 5) everywhere. Keep each spec's caps (0.5 s per frame) and its `2/sqrt(S)` damping, with S = effective compression. |
| C3 | 01 §2.11 vs 02 §2.3 | Two copies of the air/water exchange: different rate constants (0.2/s vs 2/s, air cap 50k vs 10s) and world heights 102/100 vs hatch `highY`/`lowY` | One kernel owned by 02. Equalize keeps its own rate constants as parameters but uses **`highY` as vent height and `lowY` as valve height** instead of world 102/100. |
| C4 | 01 §4, 02 §4, 03 §4 vs 04 §3 | Proper save data vs the energy-gain modifier trick | Use the background-task settings store (R9). The modifier trick is not used. 04 §3 is edited. |
| C5 | 02 §4, 03 §4 | Auto leak-blocking and auto-carry default **Off** | **Decision D3.** |
| C6 | 01 §3 #17, 02 §4 (contexts Normal, Queue) vs 04 §6 (needs NoCharacter) | Toggle action contexts | Normal, Queue, NoCharacter. Instant, with no crew walk. |
| C7 | 02 §2.11, 03 §2.I, 04 §4.1 (valve rows re-listed for wheel order) | Three partial descriptions of one whole-method replacement of `AutomaticActionsController.DoUpdate` | No replacement. The 2026.1 controller is much richer (lighting, exterior hatch, deck-awash queueing, compressor management toggles), and replacing it would delete those. H14 is a postfix that zeroes the private `pumpDelay` timer while any equipment is `Leaking`, so the vanilla pump turn-off never matures. The valve parts are dropped (B15). Re-listing the 04 §4.1 valve rows is unnecessary, because no new valve actions need wheel interleaving except B2, which uses its own sort priority. |
| C8 | 02 §2.10, §3 vs 03 §2.G | Automation gates specified twice | Owner 02. 03 §2.G now points to 02. |
| C9 | 02 §2.6 vs 04 §2.1.7 vs vanilla 2026.1 `BlockLeakAction` | Hatch seal = presence of a scale modifier on the hatch **Performance** stat. That changes a real stat; repair and blocker modifiers stack; the panel prints duplicates. Repair creates its own modifier. Vanilla already adds its own `"BlockLeak"` 0.2 modifier on `LeakIntensity` | **Hatches:** do not touch `Performance`. Core `HatchSeals` tracks active seal sources per hatch: blocker gives x0.1, repair gives x0.1 (presence, original parity). The leak factor is 0.02 times their product. The panel shows one "partially sealed" line. **Leakable equipment:** a blocker relies on vanilla's own 0.2 modifier (no second one); repair adds one `LeakIntensity` scale modifier with value 1 - P. Both multiply. |
| C10 | 02 §2.1 step 1 vs 04 §2.1.7 "set Locked every frame" and 04 Q2 | Two mechanisms hold a hatch shut during repair; nothing releases the lock | When the repairer starts interacting, close the hatch once. H1 step 1 (a visible `IsBeingRepaired` means it cannot be opened) and H2 then keep it shut. No per-frame write. Nothing reopens it after repair, because reopening a hatch into a flooded room is unsafe. This answers 04 Q2. |
| C11 | 01 §7.7, 02 §2.2, 02 §7.11, 03 F | Exterior hatch identified by "Exterior" in the object name | Use `Hatch.IsExteriorHatch` (exists in 2026.1). |
| C12 | 01 §2.11 (102/100), 02 (101.55 in 01 §7.6), 03 F (101.55, 102.8, 106) | Hard-coded 2022.1 Type VIIC world heights | Derive them per boat in `BoatLayout`: the conning-tower hatch's `lowY` replaces 101.55, the exterior hatch's `lowY` replaces 102.8, and "no interior layer" means use the agent's current height instead of 106. |
| C13 | 01 §2.12 (ratio x 150 t, clamp), 03 (ratio 1 = surfaced, 40 % awash target) vs 2026.1 | 2026.1 shows buoyancy from `NeuralNetworkPhysics` volume/mass with a 65 or 75 type multiplier, not `BuoyancyRatio` x 100 | The label, icon and awash target are dropped (P12a, B3). P12b only appends flooding volumes, so no buoyancy unit is needed. B2's "deck above water" gate uses `DeckDepth`. |
| C14 | 01 §2.8 vs 02 | Crowding is described in 01 but functionally belongs to leaks | Owner 02 (P8 built in WS3). |
| C15 | 02 §2.7, §2.8 vs 04 §4.1 rows `BlockLeakAction` and the sailor variant | Who owns the block-leak rows | Owner 02. The officer override is dropped (H9). The sailor row is later (H10). |
| C16 | 01 §2.11 ("pump water to control room" in the leaks area), 03 §2.H, 04 §4.1 (disabled row) | Unfinished pump described three times | Drop (B14). |
| C17 | 01 §7.10, 02 §2.4.6, §7.9, 01 §2.13 leak text | Debug output shipped enabled in three places | All of it sits behind R14, off by default. |
| C18 | 01 §2.2, §7.4 | Toxicity sign unknown | Resolved from 2026.1: `Choking` injures when `Random > (0.85 sailor, 0.7 officer) - AirToxicity` every 6 s. So -0.1 gives about a 5 % (sailor) or 20 % (officer) hit chance per check, and -1.0 never injures. Keep the spec values. Choking is attached only while `IsAirToxic`. |
| C19 | 01 §5, 02 §5, 03 §5, 04 §4.2 | Strings listed per section | Each workstream writes its own locale rows. 04 §4.2 owns sheet format and the merge. |
| C20 | 02 §5, 03 §5, 04 §5 | Icons listed per section | The 04 §5 pipeline (R12) is shared. Each workstream supplies its own PNGs. Dropped features (H7, B3, B14) need no art. |
| C21 | 02 §7.7 | Ventilation auto-off when surfaced | Moot: 2026.1 vanilla turns ventilation off when oxygen is at least 0.999, and the controller is not replaced. |
| C22 | 02 §2.4.7 ("evacuated"), 03 F (`Evacuation` and `Evacuated`) | Which flag | 2026.1 has both. Vanilla hatch text and reachability use `Evacuation`. B9 keeps vanilla's `Evacuation` walk and adds only the can-open test. The panel uses `Evacuation`, matching vanilla. |
| C23 | 01 §2.3, §3 #4 | Hooks the rebreather *status* `DoUpdate` | In 2026.1 the rebreather is an item (`UBOAT.Game.Scene.Items.Rebreather`) whose usage counts against a `Durability` parameter. Add a x2.5 scale modifier, with no Harmony patch. |
| C24 | 01 §2.6 vs 2026.1 `LowFrequencyUpdate` | Vanilla 2026.1 marks **all** slots underwater while air is toxic, which makes rebreathers required, and ticks every 4 s | Our replacement keeps that rule, keyed on P2's new `IsAirToxic`, and adds the 01 §2.6c water and neighbour logic. 1 s interval. Neighbour rule per D7: underwater if any containing side is above tolerance. |
| C25 | 02 §2.1 step 3 | With the setting off, *every* hatch can be opened, which removes vanilla 2026.1's own blockade rule (blockade side flooded above 1 m with fluid pressure > 9) | H1 is a postfix that changes the result only while P14 is on. When off, vanilla 2026.1 behavior is unchanged. |
| C26 | 03 F vs 2026.1 | Vanilla `IsDestinationReachable` returns `true` early for player orders; the mod blocks even player orders through hatches that cannot be opened | The B9 postfix re-walks crossed hatches when vanilla returned `true`, including for player orders, and returns `false` on any hatch that cannot be opened. |
| C27 | 02 §2.10, 03 §2.G vs 2026.1 | `AutoCarryWoundedToSafety.DoUpdate` also pulls divers back aboard; gating the whole method would strand divers | The gate skips the method only when no diver is waiting (private `diverSpot.Divers.Count == 0`). Otherwise vanilla runs. |
| C28 | 02 §2.8, §7.8 vs 2026.1 | Which action the vanilla auto-leak routine dispatches; hatch leaks | 2026.1 `LeakBlockingBySailors` builds `new BlockLeakAction(...)` directly (no database lookup), so a row override would not change it. Vanilla `BlockLeakAction.UpdateAction` casts the leaking equipment to `LeakableEquipment`, but `Hatch` is a `Device`, so a crewman blocking a leaking hatch would throw. H8 adds a prefix on `BlockLeakAction.UpdateAction` that handles hatch targets: it registers a `HatchSeals` source while interacting and skips the cast path. |
| C29 | `MODDING-REFERENCE.md` | It names `UBOAT.ModAPI.Core.BackgroundTaskBase` | 2026.1 has `UBOAT.Game.Core.BackgroundTaskBase`, and tasks resolve through `modManager.GetType`. Reported to the reference-doc owner; not edited here. |
| C30 | 01 §2.10 vs 2026.1 `AirCompressor.Update` | The spec replaces it | Vanilla 2026.1 already chunks at 4 s with the same transfer law, using `oxygenCompression` and `maxPressure` (250). Keep vanilla and add only the gate and the draw (§4). The "compressed air = 20x" ratio and 0.1 cap stay. |
| C31 | 04 §2.1.7 notes vs 2026.1 | The original repair lacks the 2026.1 improvements | **Decision D4**. The additive design keeps them for free. |
| C32 | 03 sentinels (0.6, 0.7, 0.75, 1.0 m) vs 2026.1 `DepthPreset.DeckAwash` and the controller's checks of `TargetDepth > 0.02/0.05` | Sentinels would trigger vanilla partial flood/blow and flood queueing | No sentinels in v1 (D5). |

---

## 4. Hook strategy against 2026.1

Every whole-method replacement in specs 01 to 04 is listed below, checked against the
decompiled 2026.1 method.

| Spec hook (replace) | 2026.1 finding | Strategy |
|---|---|---|
| `Compartment.IsAirToxic` getter (P2) | small expression; delegates to `mergedCompartment` | Prefix replacement is fine. It must keep the merged-compartment delegation. |
| `Compartment.AirToxicity` getter (P2) | single expression read by `Choking` | Postfix that sets `__result`. |
| `CompartmentPressure.Pressure` getter (P4) | returns fluid pressure if the point is below water, else air pressure | Postfix: only for interior-hatch targets, set `__result = |Δp|`. |
| `CompartmentPressure.IsBeingDamaged` setter (P5) | already idempotent | Not patched (dropped). |
| `Compartment.LowFrequencyUpdate` (P6) | vanilla does only pressure damage and slot underwater flags, using a sorted-list early exit and returning 4 s | **Replace (prefix returns false, `__result = 1f`).** A postfix would double-apply vanilla's random pressure damage and fight its cached slot indices. Our version covers all of vanilla's work (C24). |
| `AirCompressor.Update` (P10) | vanilla transfer law matches the spec | **No replacement.** The prefix skips the frame when the compartment pressure is at most 0.35 atm. It stores tank air totals, and the postfix draws `min(Δtank/20, 0.1 × chunks × N)` from the compartment. |
| `BuoyancyNotification.DoUpdate` (P12a) | 2026.1 already has %, trend and icons | Not patched (dropped). |
| `BuoyancyNotification.GetTooltipContents` (P12b) | returns a string | Postfix appends. |
| `Rebreather.DoUpdate` (P3) | moved to an item with `Durability` | No patch: a parameter scale modifier on spawn or equip. WS1 verifies where the parameter instance lives. |
| `Hatch.CanBeOpened` (H1) | vanilla has its own blockade and exterior rules | Postfix, only while P14 is on (C25). |
| `Hatch.DoUpdate` (H4, H5) | fuses the occlusion portal, vanilla flow (only when unlocked) and particles; no narrower seam (`GetTransfer` covers horizontal water only) | **Replace (prefix returns false).** The portal rule is reimplemented from the behavioral description in 02 §2.3a. It is not copied from game code. Particle start/stop uses the existing private systems. |
| `Hatch.GetStateText` (H6) | string | Postfix that rewrites `__result`. |
| `AutomaticActionsController.DoUpdate` (H14, B15) | far richer in 2026.1 | **No replacement.** The postfix resets the private `pumpDelay` while leaks exist (C7). |
| 4 x valve `GetState` (B5) | already allows under water always, and adds mode/docked hiding | Not patched (dropped). |
| `FloodingAndBlowingValve.FixedUpdate` conditional skip (B4) | deck-awash modes exist in vanilla | Not patched (dropped). B2 only sets `Mode` and `TurnedOff` and drives tank air itself. |
| `NavMeshNavigator.IsDestinationReachable` (B9) | vanilla already walks hatches for `Evacuation` | Postfix that may flip `true` to `false` (C26). |
| `CharacterActionsDatabase.LoadActions` (Custom Actions replaces it) | private, uses `Type.GetType` | Postfix adds our rows only (D2). |
| `RepairAction` row replacement (R1 to R4) | the vanilla action is public, uses `UseDevice(char, "Repair", "Repair", int.MinValue)`, and has all the 2026.1 improvements | **No replacement action.** R1: a prefix on `DeviceUseAction.UseDevice`, active only when `__instance is RepairAction`, rewrites the slot argument (by ref) to the first free slot in the fallback order. R2: postfix on `UpdateAction`. R2/R3 cleanup: postfix on `StopMainCharacter`. R3: on first interaction. Vanilla rows `RepairAction1` to `RepairAction3`, including the flaw tags, stay untouched. If the 0.33333 radioman/leader performance differs from the vanilla sheet value, override only that row's parameter (WS5 checks). |
| `SalvageReplacementParts` row replacement (R5) | `2f` appears in `GetState`, `GetActionProfit` and `UpdateAction` (twice: consume and the remaining check) | Transpiler swaps `ldc.r4 2.0` for our constant in those three methods. This keeps the 2026.1 time scale and visual clue. If a match count is unexpected, leave it unpatched and log. |
| `Rebreather` / `Tank.SimulateBreach` (P1) | private, now `(ExternalPressure, bool isInBottom)`, plain managed code | Prefix (not a replacement). |

**Burst.** The only Burst code on these paths is in `UBOAT.Game.Scene.Items.Hull`:
`PropagateBallastJob`, `SimulateBreachesJob` and `FindRepairableBreach`. That is the
compartment/breach model of `Hull` equipment (AI ships). It cannot be patched and must not be
targeted. All player-boat hooks above (`Tank`, `Compartment`, `Hatch`, `AirCompressor`, valve,
navigation, actions) are managed code. WS7 confirms that the player U-boat does not route
flooding through `Hull` jobs. If it does, P1 needs a different seam.

Other spec hooks, already narrow prefixes or postfixes and kept: `Compartment.AddInteractableSlot`
(P7), `Interactable.OnSlotInteractionChanged` (P8, private), `PlayerShip.FixedUpdate` (P9,
private), `Storage.GetStateText` (P13), `Hatch.Locked` setter (H2), `Hatch.OnDisable` (H3),
`LeakBlockingBySailors.DoUpdate` (H12), `AutoCarryWoundedToSafety.DoUpdate` (H13),
`NavMeshDoors/NavMeshLadder.TraverseStart` (B10), `EvacuateToSafetyAction.Escape` (B11, private),
and `BlockLeakAction.UpdateAction` (H8 hatch case, new). Every target was confirmed present in
2026.1.

---

## 5. Decisions for the user

> **Status: all recommendations accepted by the maintainer (2026-11-19).** Implementers treat
> each recommendation below as the decided requirement.

**D1. Water-head units in hatch logic.**
Options: (a) the original's 1 m of water = 1 atm; (b) physical, 1 m = 0.1 atm, which matches
vanilla 2026.1 `FluidsPressure`.
Recommendation: **(b)**, keeping the 0.9 atm lock threshold as a tunable constant and retuning
it in-game.
Tradeoff: (a) reproduces the original feel, where hatches lock after about 1 m of
flood-height difference and water rushes hard. (b) is consistent with vanilla and the panel,
but water alone rarely locks a hatch unless the threshold is lowered.

**D2. Custom Actions as a hard dependency, or our own action registration.**
Recommendation: **our own registration**. That is a postfix on `LoadActions` registering only
our rows, which coexists if Custom Actions is installed. The three toggles go on the vanilla
Workshop activator, where Custom Actions puts its toolbox.
Tradeoff: we maintain one small patch and lose the dedicated "Settings" toolbox, but the mod
has no third-party dependency that can lag a game update.

**D3. Defaults for the three toggles.**
Recommendation: vanilla auto leak-blocking **On** and vanilla auto-carry-wounded **On**; the
mod adds Off switches. Hatch pressure blocking **Off**, opt-in as in the original.
Tradeoff: On/On keeps vanilla crew behavior for new players. The original's Off/Off gives
full manual control from day one, but wounded crew lie where they fall until ordered.

**D4. Preserve the 2026.1 vanilla repair improvements.**
These are flaw kinds and tags, sailor repair, crew-commanding modifier, NPC part exemption,
`DisallowRepair` and voice lines.
Recommendation: **yes**. They come for free from patching vanilla `RepairAction` instead of
replacing it; only the slot fallback, leak throttle and hatch hold are added.
Tradeoff: repair speed and parts use follow 2026.1, not the original's 2022.1 copy.

**D5. Ballast scope and intent.**
Options: (a) the original's target-depth sentinels; (b) an explicit, saved ballast-intent
state; (c) v1 ships *Exhaust Blow* as a manual valve order only, with no automation.
Recommendation: **(c) now, (b) later**. *Awash Trim* and the valve override are dropped,
because 2026.1 has native deck-awash ballast modes.
Tradeoff: no automatic diesel blow on surfacing in v1. Sentinels would collide with 2026.1's
deck-awash preset and controller.

**D6. Drop features that 2026.1 vanilla now covers.**
These are the buoyancy label and icons, hatch open/close, officer block-leak, valve gating,
hydroplane visuals, and compressor/ventilation automation.
Recommendation: **drop and rely on vanilla**.
Tradeoff: less code and no fights with vanilla. The look and feel differ slightly from the
original; for example, buoyancy % uses vanilla's scale.

**D7. Fix the original's bugs, or replicate them.**
Recommendation: **fix them**. This covers per-frame probabilities (made per-second), exact
float equality (use an epsilon), missing null and boat-type guards, debug text shipped visible,
the one-shot carry flag that fires only on the first load, neighbour-slot "last wins" (becomes
"any side"), and the exhaust blow having no diesel or surface prerequisite.
Tradeoff: edge cases diverge from the original's numbers, and there is no "bug-compatible"
mode.

---

## 6. Implementation work breakdown

Rules for every implementer brief:

- Build from specs 00 to 04 and the 2026.1 game assemblies only. Never open the original mod.
- Each workstream owns its own folders, listed below, and touches no others. Patches register
  through Harmony `PatchAll`, so there is no shared patch list to edit.
- Data-sheet rows and locale rows go in that workstream's own row-source file, in the format
  the build harness's sheet generator defines (`docs/DEVELOPMENT.md`). No one edits a shared
  `.xlsx` by hand. If the harness supports only one source file, WS0 owns it and the others
  hand their rows to WS0.
- **Tests:** a test for code in an existing source file goes in that file's **existing** test
  file. Add cases there and reuse its helpers. Create a new test file only for a new source
  file that has none. Never create `*_fixes`, `*_regression` or per-feature test files for
  existing code. Tests run through the harness's documented runner.
- Core code is pure (§2.1). Every Core formula has unit tests taken from the section spec's
  acceptance criteria, adjusted by this document. The Game layer has only a reflection test
  that each hook target exists (02 §6 #21 style).

| WS | Scope (feature IDs) | Owns | Depends on | Acceptance |
|---|---|---|---|---|
| **WS0 Foundation** | R7, R9, R10 (per D2), R11 pipeline, R12, R13, R14, P14 toggle actions and the other two toggles (H12/H13 toggle *actions* only), `SimClock`, `BoatLayout`, `IRandom`, logging | `Core/Common/`, `Core/Settings/`, `Game/Bootstrap/`, `Game/Infrastructure/`, `Game/Settings/`, `Game/Registration/`, `mod/Manifest.json`, `mod/Icons/common/` | build harness | 04 §8 #9, #10, #11 (pipeline), #12, #14, #15 (as revised by D2), #18; 02 §6 #17; 01 §6 #23. Settings survive save/load. A pre-mod save gets the D3 defaults. A mod-registered dummy action appears on its activator. |
| **WS1 Pressure and air** | P1, P2, P3, P4, P6, P7, P9, P10, P12b, P13 | `Core/Pressure/`, `Game/Pressure/`, `mod/Icons/pressure/` | WS0 (clock, layout, logging) | 01 §6 #1 to #4 and #6 to #13 (#4 as deterministic 0.4x; #9 per C24/D7), plus #22 (P12b highlight); #24 and #25 in game. #5 belongs to WS2; #19 to #21 are dropped with P12a |
| **WS2 Hatches** | H1, H2, H3, H4, H5, H6, P11 (equalize), Core `HatchExchange`, `HatchSeals` registry and `AtmPerMetreOfWater` | `Core/Hatches/`, `Game/Hatches/`, `mod/Icons/hatches/` | WS0. Per D1 for the unit constant. **Day 0: publish the `HatchSeals` API (add/remove a source and read the factor) so WS3 and WS5 can code against it** | 02 §6 #1 to #12, #15 (hatch part per C9), #23, #24, #25, #31; 01 §6 #5, #14 to #18 (heights per C3) |
| **WS3 Leaks and crew automation** | H8 (vanilla `BlockLeakAction` hatch case, panel-line source), P8, H12 gate, H13 gate and trigger action, H14 | `Core/Leaks/`, `Core/Automation/`, `Game/Leaks/`, `Game/Automation/`, `mod/Icons/leaks/` | WS0 (settings, registration), WS2 `HatchSeals` API | 02 §6 #13, #16, #18, #19, #20 (pump rule as a postfix), #22 (for rows still in scope), #26, #27, #28, #30; C27: divers still recovered with auto-carry off; C28: no exception when blocking a leaking hatch |
| **WS4 Ballast and navigation** | B2 (manual, gated per 03 §7.3 and §7.4), B9, B10, B11 | `Core/Ballast/`, `Core/Navigation/`, `Game/Ballast/`, `Game/Navigation/`, `mod/Icons/ballast/` | WS0. Uses the patched `Hatch.CanBeOpened` at runtime only (no file dependency on WS2) | 03 §6 #1 to #5 (epsilon completion), #7, #8, #24, #25, #26; new: B2 is unavailable when the diesel is off or `DeckDepth` is not above water, and cancels when either fails |
| **WS5 Repair and parts** | R1, R2, R3, R5, R6 | `Core/Repair/`, `Game/Repair/`, `mod/Icons/repair/` | WS0 (registration for R6), WS2 `HatchSeals` API (hatch throttle) | 04 §8 #5, #6, #7, #8 (with Q3 checks), #17; vanilla repair rows unchanged and flaw repairs still offered; the salvage transpiler logs and no-ops if the IL shape changes |
| **WS7 Integration** | reflection test over every hook in §4; in-game checklist run; WS1 to WS5 in-game criteria; Burst/`Hull` check (§4) | `tests/` integration files designated by the harness | all | all **[in-game]** criteria in 01 to 04 that are still in scope; mod loads with no exceptions; no patch applied twice |

Order: build harness, then WS0. WS1, WS2, WS4 and WS5 start in parallel once WS0's
`Core/Common` (clock, random, logging interfaces) is merged. WS3 and WS5 need only WS2's
day-0 `HatchSeals` API. WS7 runs last. D1 blocks only the value of `AtmPerMetreOfWater` (WS2)
and its tests. D2 blocks the WS0 registration route. D3 blocks only default values.

Later backlog (not scheduled): P12c, H10, H11, H16, B1 + B2a, B8, B12, R8.

---

## 7. Changelog of edits to sections 01 to 04

| File | Section | Edit |
|---|---|---|
| 01 | §1, last paragraph | Added ownership pointers: toggle storage moves to 04 §3; crowding (§2.8) is owned by 02; units pending 00 D1. |
| 01 | §2.11 | Added a note that the exchange kernel is owned by 02 §2.3 and heights come from hatch geometry (00 C3). |
| 01 | §4 | Persistence now points to the 04 §3 settings task (00 C4). Toggle contexts include NoCharacter (00 C6). |
| 01 | §7 items 4, 5 | Item 4 marked resolved (00 C18). Item 5 points to 00 D1. |
| 02 | §2.6 | Declared this section owner of the leak-blocking mechanism; hatch seals use a registry, not the Performance stat (00 C9, C28). |
| 02 | §2.11 | Declared owner of the controller hook; no replacement in 2026.1 (00 C7). |
| 02 | §3 Custom Actions paragraph | Points to 00 D2. |
| 02 | §4 | Persistence points to 04 §3; defaults pending 00 D3. |
| 03 | §1 (paragraph after the table) and §2.G | Automation gates and the controller are owned by 02 (00 C7, C8). |
| 03 | sentinel table | Note: sentinels are not used in v1 (00 D5, C32). |
| 03 | §4 | Persistence points to 04 §3. |
| 04 | §2.1.9, Q2 | Hatch lock release resolved by 00 C10. |
| 04 | §3 Settings storage | Recommendation replaced by the background-task store (00 C4). |
| 04 | §6 | Recommendation now points to 00 D2. |
