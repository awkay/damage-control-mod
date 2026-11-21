# Harmony patches

Every patch the mod applies, as of WS7 integration. All patches are attribute-declared `[HarmonyPatch]`
classes applied by one `PatchAll()` in `Game/Bootstrap/DamageControlMod.cs`; no other code patches.
Priority is Harmony's default (`Priority.Normal`, 400) unless the table says otherwise.

Columns:

- **Kind**: prefix, postfix or transpiler. "skips original" means the prefix can return `false`.
- **Gate**: `G` means the patch stands aside, so vanilla runs, outside the game's Gameplay state and while a
  save loads (`Game/Infrastructure/GameplayState`). `-` means the patch is not gated. It is either
  read-only, configuration applied while the scene builds, or cleanup that must always run.

## Fail-safe rules (all patches)

1. **Apply time.** Each patch class has `Cleanup(Exception)` returning `PatchGuard.Cleanup(id, ex)`.
   If Harmony cannot apply that class (missing target, renamed private field), the exception is logged
   once as `patch <id> (not applied) threw and falls back to vanilla behaviour`. That class stays
   unpatched and `PatchAll` goes on with the next one. Without this, the first failure would leave
   every later class unpatched. The bootstrap then logs `Harmony patches applied.`, or a warning with
   the number of classes it skipped.
2. **Run time.** Every patch body is wrapped in `try/catch`. On an exception it calls
   `DcLog.PatchFailed(id, e)`, which logs once per session. A prefix then returns `true` (vanilla runs)
   and a postfix leaves `__result` untouched.
3. **Unknown boat.** Features that need boat geometry call `BoatLayout.Require(feature)` or
   `TryGetGeometry`. They do nothing on an unknown layout (one warning per feature) and never throw.
4. **Outside gameplay.** The simulation and hatch-rule patches (gate `G`) do nothing in menus, while
   scenes load and while a save restores the boat.

## Table

| Target (2026.1) | Patch class | WS | Kind | Prio | Gate | Purpose |
|---|---|---|---|---|---|---|
| `CharacterActionsDatabase.LoadActions` (private) | `ActionRegistrationPatch` | WS0 | postfix | **Last** | - | R10/D2: register our `Crew/Actions` rows that vanilla's `Type.GetType` could not resolve. Skips names already registered (Custom Actions). |
| `Tank.SimulateBreach(ExternalPressure, bool)` (private) | `BreachAirFloorPatch` | WS1 | prefix | | G | P1: a breached compartment keeps a 0.1 air floor. |
| `Compartment.IsAirToxic` getter | `AirToxicPatch` | WS1 | prefix, skips original | | - | P2: unbreathable-air rule. Merged compartments delegate to vanilla. |
| `Compartment.AirToxicity` getter | `AirToxicityPatch` | WS1 | postfix | | - | P2: toxicity value read by `Choking`. |
| `CompartmentPressure.Pressure` getter | `HatchPressureReadingPatch` | WS1 | postfix | | - | P4: an interior-hatch pressure point reports \|pL - pR\|. |
| `Rebreather.OnEquipped` | `RebreatherEndurancePatch` | WS1 | postfix | | - | P3: x2.5 durability modifier (idempotent). |
| `Compartment.LowFrequencyUpdate` (private) | `CompartmentTickPatch` | WS1 | prefix, **whole-method replacement** | | G | P6: bulbs, overstress damage, slot underwater flags. Returns a 1 s interval. Damage is rolled over the real elapsed game time (WS7). |
| `Compartment.AddInteractableSlot` | `SlotTuningPatch` | WS1 | prefix | | - | P7: slot water tolerance and interaction types, before vanilla's sorted insert. |
| `PlayerShip.FixedUpdate` (private) | `VentingPatch` | WS1 | postfix | | G | P9: tower/diesel venting and diesel air draw, integrated in 1/75 s substeps (WS7). |
| `AirCompressor.Update` (private) | `CompressorDrawPatch` | WS1 | prefix (skips the frame at <= 0.35 atm) + postfix | | G | P10: gate and room-air draw. Vanilla's transfer law is kept. |
| `BuoyancyNotification.GetTooltipContents` | `BuoyancyTooltipPatch` | WS1 | postfix | | - | P12b: ballast and flooding breakdown appended. |
| `Storage.GetStateText` | `BilgePressurePatch` | WS1 | postfix | | - | P13: bilge panel air-pressure line. |
| `Hatch.CanBeOpened` getter | `HatchCanBeOpenedPatch` | WS2 | postfix | | G | H1: pressure lock while P14 is on. A visible repair status always blocks (C10, R3 hold). |
| `Hatch.Locked` setter | `HatchLockedSetterPatch` | WS2 | prefix, skips original (refuses an opening) | | G | H2: lock guard. The mod's own openings bypass it (`HatchRuntime.SetLockedUnguarded`). |
| `Hatch.OnDisable` (private) | `HatchOnDisablePatch` | WS2 | prefix + postfix | | Gameplay only | H3: a disabled hatch under pressure stays shut and leaks. |
| `Hatch.DoUpdate` (private) | `HatchDoUpdatePatch` | WS2 | prefix, **whole-method replacement** | | G | H4/H5: portal, air/water exchange (substeps of at most 0.5 s), blow-open hazard over the whole frame (WS7), leak upkeep, jets. |
| `Hatch.GetStateText` | `HatchStateTextPatch` | WS2 | postfix | | - | H6: hatch panel. |
| `BlockLeakAction.GetState(Activator, PlayableCharacter)` | `BlockLeakStatePatch` | WS3 | postfix | | - | H8: hide "Block leak" on open hatches and slotless hatches. |
| `BlockLeakAction.UpdateAction` | `BlockLeakUpdatePatch` | WS3 | prefix, skips original only when the leak is null | | - | H8/C28: hatch targets bypass vanilla's leakable cast, hold a blocker seal, and cancel on an open hatch. |
| `BlockLeakAction.StopMainCharacter` | `BlockLeakStopPatch` | WS3 | prefix + postfix | | - | H8: release seals. WS7: put back a running repair's "is being repaired" status that vanilla's stop removed. |
| `BlockLeakAction.StartMainCharacter` | `BlockLeakStartPatch` | WS3 | prefix + finalizer; widens the target (private fields through `AccessTools.FieldRefAccess`) and hides kept blockers from `Activator.Users` during vanilla's start | | - | P8: a second blocker joins the first instead of replacing him; at most two at the leak. |
| `Interactable.OnSlotInteractionChanged` (private) | `SlotCrowdingPatch` | WS3 | postfix, writes private `maxCharacters`/`maxCharactersCurrent` | | - | P8 backstop: leaking holes, pipes and hatches without crew work slots take two characters. |
| `LeakBlockingBySailors.DoUpdate` (private) | `LeakBlockingGatePatch` | WS3 | prefix, skips original when off | | - | H12 gate. Outside a campaign the setting reads its default (on), so vanilla runs. |
| `AutoCarryWoundedToSafety.DoUpdate` (private) | `AutoCarryWoundedGatePatch` | WS3 | prefix, skips original (returns 2.5 s) | | - | H13 gate. Never skips while a diver waits (C27). |
| `AutomaticActionsController.DoUpdate` (private) | `TrimPumpLeakHoldPatch` | WS3 | prefix + postfix, writes private `pumpDelay` | | - | H14: no trim-pump auto-off while anything leaks. |
| `NavMeshNavigator.IsDestinationReachable` | `ReachabilityPatch` | WS4 | postfix (only true -> false) | | G | B9: hatches the mod holds shut (repair hold; pressure lock with P14 on) make destinations unreachable. Vanilla-only locks never count (00 C33). |
| `NavMeshDoors.TraverseStart` | `DoorTraversePatch` | WS4 | prefix | | G | B10: cancel orders at a door the mod holds shut (00 C33). Never for evacuating crew or crew leaving the deck on a dive. |
| `NavMeshLadder.TraverseStart` | `LadderTraversePatch` | WS4 | prefix | | G | B10: same for ladder hatches; also never for a climber already on the ladder's queue (reads private `navigators`). The exterior hatch is exempt. |
| `EvacuateToSafetyAction.Escape` (private) | `EvacuationCancelPatch` | WS4 | prefix | | - | B11: evacuation can be cancelled. |
| `DeviceUseAction.UseDevice` | `RepairSlotPatch` | WS5 (+WS3 step) | prefix (ref `slotTag`; acts only for `RepairAction` and, on a shared leak target, `BlockLeakAction`) | | - | R1: repair slot fallback. P8: a second blocker takes `Default`, then `Repair`, when `Block Leak` is taken. |
| `RepairAction.GetState(5 args)` | `RepairStatePatch` | WS5 (+WS3 step) | postfix | | - | R1: offer the repair through a free Block Leak slot. P8: greyed out while two others already work a shared leak. |
| `RepairAction.UpdateAction` | `RepairUpdatePatch` | WS5 | postfix | | - | R2 leak throttle, R3 one close, repair seal on hatches. |
| `RepairAction.StopMainCharacter` | `RepairStopPatch` | WS5 | postfix | | - | R2/R3 cleanup. |
| `PlayerShip.Update` (private) | `DiagnosticsTickPatch` | Diagnostics | postfix, read-only | | G | Drives the `[DC diag]` report and event lines while the diagnostics setting is on (`Game/Diagnostics`). Changes no game state. |
| `SalvageReplacementParts.GetState` / `GetActionProfit` / `UpdateAction` | `SalvageStateRatioPatch`, `SalvageProfitRatioPatch`, `SalvageUpdateRatioPatch` | WS5 | transpilers | | - | R5: `2f` -> tunable scrap per part. If any of the three fails to match, or is not applied, all three stay at vanilla's 2. |

## Diagnostics instrumentation

The diagnostics report adds one patch of its own (`DiagnosticsTickPatch`, above) and reads everything
else from the game. Three existing patches also hand values they already computed to
`Core/Diagnostics/DiagRecorder.Shared`, which nothing in the simulation reads back:

| Patch | What it records | Cost when diagnostics are off |
|---|---|---|
| `HatchDoUpdatePatch` (WS2) | per hatch and frame: the air and water its substeps moved (left to right), and the frame's game seconds; zero on frames where the hatch is shut tight or at rest | one call per hatch per frame that returns at once |
| `VentingPatch` (WS1) | the tower and diesel compartments' applied air change | one field read per physics step |
| `CompressorDrawPatch` (WS1) | the air charged to the compressor's room | one field read per compressor frame |
| `HatchDoUpdatePatch` (WS2), blow-open | calls `DiagnosticsRuntime.OnHatchBlownOpen` for the immediate event line | nothing (returns at once) |

None of these changes a value, a branch or an early return of the patch it sits in.

## Cross-workstream interactions (WS7 audit)

**Every target above has one patch class**, except the salvage trio, which belongs to WS5 alone. Two
WS5 classes carry a WS3 (P8) step inside the same class rather than a second patch on the same target:
`RepairSlotPatch` (the second blocker's slot) and `RepairStatePatch` (the two-worker cap, run after the
R1 fallback). Several targets that sound shared are not:

- **Separate targets.** `BlockLeakAction` (WS3) and `RepairAction` (WS5) declare their own
  `UpdateAction`, `GetState`, `StartMainCharacter` and `StopMainCharacter`. `DeviceUseAction.UseDevice`
  acts only for `RepairAction` and, on a shared leak target, `BlockLeakAction`. The mod's own
  `DeviceUseAction` subclasses (equalize, exhaust blow, dismantle) pass through it unchanged.
- **Single owners.** `Hatch.*` is patched only by WS2. WS4 and WS5 only read `CanBeOpened` and write
  `Locked`. `Compartment.*` is patched only by WS1. `AutomaticActionsController.DoUpdate` is patched
  only by WS3.

So no prefix that skips the original can suppress another workstream's patch on the same method. The
interactions happen through shared game state instead:

| Shared state | Writers | Readers | Verdict |
|---|---|---|---|
| `Hatch.Locked` | WS2 (blow-open, H3, equalize opening through the guard), WS5 (R3 close), vanilla | WS2 rules, WS4 B9/B10 | No fight. Closes always pass the guard. Openings go through H2. The mod's forced openings use `SetLockedUnguarded`. The guard stands aside while a save restores hatch states (gate). |
| `Hatch.CanBeOpened` (patched) | - | WS2 H2, WS4 B9/B10, equalize | Consistent: everyone reads the same patched rule. A hatch under repair is unreachable and blocks traversal even with P14 off. This is intended (WS5-4). |
| `IsBeingRepaired` status | vanilla `RepairAction` (add/remove its own instance), **vanilla `BlockLeakAction` stop (removes any instance)** | WS2 H1 step 1 (repair hold), WS5 R3 | **Fixed (WS7).** P8 (WS3) lets a blocker and a repairer share one target, so a blocker leaving stripped the repairer's status mid-repair and ended the hatch repair hold. `BlockLeakStopPatch` now puts the same status object back while another crewman's repair runs. |
| `HatchSeals.Shared` | WS3 blocker, WS5 repair | WS2 exchange and panel | Keyed by (hatch, owner). Re-added every interacting frame and removed on stop. WS2 clears it on session start. No conflict. |
| `LeakableEquipment.LeakIntensity` modifiers | vanilla blocker (`"BlockLeak"` 0.2), WS5 (`"DC Repair Throttle"` 1 - P) | vanilla leak | Different names, multiplicative (C9). WS3's hatch placeholder is never attached to a stat. |
| `Compartment.Air` / `Water` | WS1 (venting, compressor, breach floor), WS2 (exchange, equalize) | everyone | All writes are additive deltas against the current value, with no cached totals, so they compose. |
| `Interactable.maxCharacters` | WS3 (P8), only on leaking holes, pipes and hatches without crew work slots | vanilla `RepairAction.GetState`, `BlockLeakAction.Start` (their `MaxCharacters == 1` rules), vanilla block-leak crowding cancel; WS3/WS5 shared-target rules (`MaxCharacters == 2`) | Intended: with two seats, vanilla no longer hides the repair or stops a repairer when a blocker starts. Saved with the game, so a widened target stays widened. |
| `Activator.Users` (block-leak target) | WS3 `BlockLeakStartPatch` (removes kept blockers, puts them back in its finalizer) | vanilla `BlockLeakAction.StartMainCharacter` replacement loop | Only for the duration of that one vanilla call. The only other reader then is `BlockLeakStopPatch` (for a replaced blocker), which looks for repairers, and those are never hidden. |
| `AutomaticActionsController.pumpDelay` | WS3 only | vanilla | Single writer. |
| `InteractableSlot.IsBelowWater` / tolerance | WS1 (P6 tick, P7 tuning) | vanilla, WS5 slot checks | Single writer. |
| Session reset (`DcSettings.SessionStarted`) | WS2 seals and tunables, WS3 one-shot, WS5 repair tracks, salvage tunable, compartment tick clock (WS7) | - | Each subscriber resets only its own state. Order does not matter. |

Priorities: only `ActionRegistrationPatch` sets one (`Last`, so it runs after Custom Actions'
replacement of `LoadActions`). No other pair of patches shares a target, so no ordering is needed.
Whole-method replacements (`Hatch.DoUpdate`, `Compartment.LowFrequencyUpdate`, `Compartment.IsAirToxic`)
replace methods that no other mod we know of patches. A third-party patch on one of them would run
against our behaviour, not vanilla's. This is not verified offline (DEVELOPMENT.md, "Not verified
offline").
