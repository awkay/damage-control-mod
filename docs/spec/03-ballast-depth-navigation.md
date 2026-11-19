# Spec 03: Ballast, depth control, valves, pumping and crew navigation

Status: behavioral spec (dirty room). Source of facts: Iron Coffins 0.61 for UBOAT 2022.1.
Every game type and member named here is a **2022.1** name and has to be re-mapped to the
**2026.1** assemblies before implementation. Names in *italics* are working names invented
for this spec. They are not the original's identifiers, and implementers may choose their own.

Units: depth and heights in metres (world space). "Buoyancy ratio" is the game's
player-ship reserve-buoyancy ratio, where 1.0 is fully surfaced and 0 is neutral. Elsewhere the
mod displays it as ratio x 150 t, so 0.40 is about 60 t of reserve buoyancy. Tank `Air` and
`Water` amounts use the game's tank volume units (presumably m^3). Time is in game seconds
(scaled delta time), unless stated otherwise.

---

## 1. Overview

| # | Mechanic | Player-facing problem | Change vs. vanilla |
|---|----------|-----------------------|--------------------|
| A | *Exhaust Blow*: blow main ballast with diesel exhaust | Surfacing always spends high-pressure air from the bottles, which is a scarce resource under the mod's air economy. | Once the boat breaks surface with the diesel running, a crewman at the valve station uses diesel exhaust to empty the remaining main ballast. This costs no bottle air, and the blow rate scales with diesel RPM. |
| B | *Awash Trim*: ballast-based deck-awash | In vanilla, deck awash is a depth setpoint that the hydroplanes hold. | Deck awash becomes a buoyancy target of 40 % reserve buoyancy, reached by blowing or venting ballast through the flooding/blowing valve. A special target-depth sentinel selects it from the existing UI toggle. |
| C | Valve-station logic override | Vanilla valve modes do not support a held, partially vented trim state. | Two valve modes get custom vent-openness schedules keyed to buoyancy and depth. |
| D | Valve actions gating | Valve actions are blocked when their interaction slot is under water, among other cases. | The four vanilla valve actions are always available while not docked and the device works, even when the slot is flooded. Their gating logic is replaced entirely. |
| E | Hydroplane visuals and surface depth-hold | The planes animate and fight to hold the surface even when ballast should do that job. | The plane angles are scaled by reserve buoyancy and boat speed. With a surface target, the internal depth setpoint is biased deeper at low speed so the planes do not try to hold the boat up. |
| F | Crew navigation vs. hatches | Crew path through pressure-blocked hatches or into evacuated compartments, or get stuck. | A custom reachability test uses the "hatch can be opened" check and compartment evacuation flags. Crew who start crossing a hatch that cannot be opened have their orders cancelled. Evacuation can be cancelled. Officers do not trigger compartment evacuation logic. |
| G | Automation gates (in the navigation file) | The player wants control over automatic leak blocking and automatic carrying of the wounded. | Vanilla automatic behaviors run only when the corresponding mod setting is on, or after a one-shot trigger for carrying the wounded. |
| H | *Control Room Pump* (main bilge pump) | Pump flood water out of other compartments. | **Unfinished and disabled in 0.61.** Spec only for completeness. See section 2.H. |

The *automatic actions controller* override, which decides when each valve action is queued
from the target depth, belongs to automation, but mechanics A and B do not work without it.
Its valve-related part is specified here in section 2.I. Its compressor, pump and ventilation
parts belong to the automation spec, if one exists.

Architect note (00 §2.5, C7, C8): spec 02 §2.11 owns the controller hook, and 02 §2.10 and §4
own the automation gates in G. In 2026.1 the controller is not replaced. Mechanics B, C, D, E
(visuals) and I are dropped, because 2026.1 vanilla now covers them. A is v1 as a manual order
only. See the 00 §1.3 scope table.

---

## 2. Mechanics

### Target-depth sentinels (shared contract)

The mod encodes ballast "intent" in the ship's target depth at values the depth dial would
not normally produce. Implementers must preserve these semantics, or swap in an explicit
state variable (recommended, see section 7).

| Target depth | Meaning |
|--------------|---------|
| exactly 0 m | Surface, using compressed air (the vanilla surface preset). |
| about 0.6 m | Surfaced with manual or settled ballast. This is a parking value, and nothing is triggered by it. |
| about 0.7 m | Awash trim in progress or achieved. The UI shows deck-awash as selected. |
| about 0.75 m | Awash trim requested by the UI toggle. The UI shows deck-awash as selected, and the controller queues *Awash Trim*. |
| about 1.0 m | Request diesel-exhaust blow. The controller queues *Exhaust Blow* if the diesel is enabled and ballast holds water. |
| ≤ 1 m (generic) | A "surface-family" target that the hydroplane bias in E acts on. |
| > 5 m | Submerged target. The controller queues the vanilla flood action. |

"About" means float-approximate equality, except where the original uses exact equality
(0 m in the controller).

Architect note (00 D5, C32): v1 uses **no** sentinels. They would collide with 2026.1's
`DepthPreset.DeckAwash` and with the controller's target-depth checks. This section remains the
owner of ballast intent, which is implemented later as explicit saved state (00 B1).

### A. *Exhaust Blow*: blow ballast with diesel exhaust

- **Performer:** any crew member (no character tag requirement). It is queued on the activator
  of the flooding-and-blowing valve station, which carries the activator tag "Valves" and sits
  in the same group and subgroup as the vanilla valve actions. It may run with no character
  context, from the queue or automation. It is not listed in the orders list, allows several
  targets in the tab menu, allows grouping, and has sorting priority 1.
- **Availability:**
  - Disabled while the ship is docked.
  - The standard activator-use check applies: the device must work, duplicate actions are not
    allowed, and blocking items are checked.
  - The interactable's "Use" slot must be usable with "allow under water always" semantics.
  - The availability check does **not** test that the boat is surfaced or that the diesel is running.
- **Start:** if the current target depth is ≤ 1 m or > 5 m, the target depth is set to 0, the depth
  preset is cleared, and the target is set to 0.6 m. This consumes the 1.0 m request so the
  controller stops queuing more blows. The character then uses the device's "Use" slot and holds
  it with no fixed duration.
- **While the character is interacting (per frame):**
  - On the first interacting frame, the valve's operating mode is set to *manual blowing* and the
    valve gets the "turned off" disable flag. The vanilla valve then stops driving the tanks,
    and the action drives them directly.
  - Exhaust flow per frame = (diesel RPM / 60) x 0.1933768 x frame delta time, in tank air units.
    RPM is taken from the diesel engine's gear. It is treated as 0 if the diesel engine
    component is disabled. Equivalently, flow is about 0.193 units of air per crankshaft revolution.
  - Main ballast tanks are the ship's tanks of ballast-tank type, in the game's list order.
    The mod uses list positions 1 to 5 and ignores position 0. A tank receives air only while it
    still contains water (water > 0):
    - Stage 1, always: position 1 gets 52.5 % of the flow, position 5 gets 47.5 %.
    - Stage 2, only while positions 1 **and** 5 each hold less than 5 units of water: position 2
      gets 20 %, position 4 gets 25 %, position 3 gets 55 %. Stage 2 adds to stage 1 (it is not
      a hand-off), so while both stages run, up to 200 % of the nominal flow is injected in total.
  - Adding air to a tank is expected to displace water through the game's tank simulation.
    The action itself does not remove water.
- **Progress, which runs every frame including before interaction starts:**
  - Progress = 0.999 x (total capacity - total water) / total capacity, summed over positions 1 to 5.
  - The value is written both to the action's progress and to the character's skill-progress display.
- **Completion:** when total water in positions 1 to 5 is **exactly 0**:
  - The valve mode is set to *optimal blowing*.
  - The valve keeps (or gets) the "turned off" flag.
  - The ship's depth preset is set to Surface.
  - The action completes.
- **Stop or cancel:** no cleanup. The valve remains in manual blowing mode and turned off.
- **State tracked:** a per-instance flag that says whether interaction has started. It is not persisted.
- **Interactions:**
  - Air/pressure: this action does not draw on compressed-air bottles. That is its purpose.
  - Engines: blow rate is proportional to diesel RPM.
  - Automation: see section 2.I.
- **Edge cases:**
  - The diesel stops mid-action. Flow drops to 0 and the action never completes on its own.
  - Water never reaches exactly 0. This depends on how the tank simulation clamps, and the action
    may stall at 99.9 %.
  - The action can run while submerged, because nothing gates it. A clean implementation should
    gate it (see section 7).

### B. *Awash Trim*: adjust ballast for deck awash

- **Performer, activator, flags, priority:** the same as A, except the availability check omits the
  blocking-items check.
- **Availability:**
  - Disabled while docked.
  - The device must work and duplicate actions are not allowed.
  - The "Use" slot follows "allow under water always" semantics.
- **Start:** if the target depth is < 1 m or > 5 m, the target depth is set to 0.7 m. This also turns
  the 0.75 m request into 0.7 m. The character then uses the device's "Use" slot and holds it.
- **First interacting frame:**
  - If the valve is already in *manual blowing*, it is first switched to *optimal blowing*. This
    forces a real mode change and is likely meant to reset vanilla's internal timer.
  - The valve is then set to *manual blowing* and the "turned off" flag is **cleared**, so the valve
    operates. Its manual-blowing behavior is the modified schedule in section 2.C.
- **Progress (per frame):** 1 - |0.4 - min(0, buoyancy ratio)| x 2.5. This is a suspected bug: see section 7.
- **Completion:** when 0.398 < buoyancy ratio < 0.402 **and** the valve carries the "turned off"
  flag. The valve sets that flag itself after it dwells in its hold band; see section 2.C.
- **Stop or cancel:** the valve gets the "turned off" flag.
- **Net behavior:** the valve vents ballast air when buoyancy is above 40 %. It runs the vanilla
  manual blow, which uses compressed air, when buoyancy is below 39.5 %. After 10 s inside the
  39.5 to 40 % band it shuts off, and then the action completes.

### C. Flooding-and-blowing valve physics override

This hooks the valve's fixed update. It acts on the valve's own vent array (5 vents, positions 0 to 4)
and its own ballast-tank array (5 tanks, positions 0 to 4). These arrays are not the same indexing as the
ship tank list used in section A.

- Let *B%* = buoyancy ratio x 100 and *d* = ship depth in metres.
- **Mode *adjustment to neutral*, with B% > 10:**
  - Vent openness for positions 0 to 4 = 0.1, 0.2, 0.1, 0.1, 0.1.
  - Each tank's "extra pressure" = 1.
  - Vanilla update is skipped.
  - At B% ≤ 10, vanilla runs.
- **Any mode other than *manual blowing* (and not caught above):** vanilla runs.
- **Mode *manual blowing*:**
  - d > 5 and B% < 5: vanilla runs.
  - d < 5, by band:

    | Band | Vents | Extra pressure | Vanilla update |
    |------|-------|----------------|----------------|
    | B% < 39.5 | all openness 0 | all 0 | **runs** (vanilla manual blow raises buoyancy) |
    | B% > 70 | 0.2, 0.3, 0.2, 0.2, 0.2 | all 1 | skipped |
    | 40 < B% ≤ 70 | 0.05, 0.1, 0.05, 0.05, 0.05 | all 1 | skipped |
    | 39.5 ≤ B% ≤ 40 (hold band) | all openness 0 | all 0 | skipped |

  - In the hold band, the valve's existing flooding-time accumulator also increases by the fixed
    time step. Once it exceeds 10 s, the valve gets the "turned off" disable flag. The accumulator
    is never reset by the mod. It relies on vanilla to reset it on a mode change.
  - Every other case with mode *manual blowing* (d ≥ 5 with B% ≥ 5, including exactly d = 5)
    skips vanilla, so nothing happens.
- Vent position 1 always gets about double the openness of the others. This is presumably the
  largest or central tank.
- The physical meaning of tank "extra pressure" is to be confirmed in 2026.1. The working
  hypothesis: it is a pressure differential that pushes air out through open vents, so that
  opening vents vents air and lowers buoyancy.

### D. Vanilla valve actions: availability replaced

This applies to four vanilla actions that target the valve station:

- compressed-air valves use (normal blow)
- compressed-air valves emergency use
- adjust ballast (trim to neutral)
- flood ballast tanks

Each one's availability check is **fully replaced** with the following:

1. If docked, the action is disabled.
2. The activator-use check runs with "device must be working" and "no duplicate actions".
   If it fails (disabled or hidden), its state is returned.
3. Otherwise, the interactable "Use" slot check runs with "allow under water always" and the
   player-order flag, and its state is returned.

Only the bare state is returned. Detail or explanation data from the checks is dropped, so the UI
loses its "why disabled" text. That is a suspected bug, see section 7. Any extra vanilla
preconditions are removed, such as depth limits, air availability or slot flooding rules. The
implementer must diff against the 2026.1 vanilla bodies and decide which to keep.

**Adjust ballast, after the device is used:** if the target depth is ≤ 1 m, the depth preset is
cleared and the target set to 0.6 m. This keeps "trim to neutral" at the surface from leaving a
sentinel that re-triggers automation.

### E. Hydroplane visuals and surface depth bias

This hooks the player submarine's fixed update and runs before it.

- **Plane objects:** the scene objects named `Stern Vertical Steer (L)`, `Stern Vertical Steer (R)`,
  `Bow Vertical Steer (L)` and `Bow Vertical Steer (R)` are found by global name lookup. The lookup
  repeats each tick while an object is missing; there is no null guard.
- **Rotation:** let *r* = the submarine's current depth-steer rotation (normalized, about -1 to 1)
  x (1 - buoyancy ratio). Each plane's local rotation about X is overwritten:
  - Stern planes: r x 28 degrees.
  - Bow planes: if r > 0, -r x 23 degrees; otherwise -r x 33 degrees. The angle is then linearly
    interpolated toward 0 by the factor (|average velocity| - 4), clamped to 0 to 1. Bow planes are
    fully used at 4 units of speed or less, are centered at 5 or more, and are blended in between.
    The speed unit is whatever the game's average velocity uses; confirm in 2026.1.
  - Net effect: planes deflect less the more reserve buoyancy the boat has, and do not move at
    all when fully surfaced (ratio 1).
- **Surface depth bias:**
  - offset = (13 - |average velocity|) x (1 - min(0, buoyancy ratio)).
  - If the target depth is ≤ 1 m and the internal (controller) target depth < offset, the internal
    target depth increases by offset x fixed time step each tick.
  - Effect: at surface targets and low speed, the depth controller aims a few metres deeper, ramping
    at about offset m/s up to about offset m. The planes therefore do not drive the boat upward,
    and buoyancy alone holds it on the surface. At |v| ≥ 13 the bias is zero or negative, so it
    has no effect.

### F. Crew navigation and hatches

- **Reachability (the navigator's "is destination reachable" check, fully replaced):**
  - *Horizontal (along the hull, world Z).*
    - If the destination is at world height ≥ 101.55 m (conning tower or above), its Z is treated as 0.
    - The origin is the agent's next position Z if on the U-boat interior navmesh layer, otherwise 0.
    - The ship's hatch list ordered bow-to-stern ("left to right") is walked in the direction of
      travel. The walk starts at the hatch adjoining the character's current compartment for a
      playable character. Otherwise it starts at the first hatch past the origin by its obstacle Z.
    - It stops at the first hatch beyond the destination.
    - The destination is **unreachable** if any crossed hatch either cannot be opened or leads into
      a compartment flagged as *evacuation in progress*. The evacuation test is waived for direct
      player orders; the cannot-be-opened test is not.
  - *Vertical.* The origin height is the agent's next position Y on the interior layer, otherwise 106 m.
    - Crossing the 101.55 m level in either direction (control room and conning tower) requires
      the conning-tower hatch to be openable. It also requires the far-side compartment not to be
      flagged *evacuated*, unless the move is a player order.
    - Going above 102.8 m (conning tower to the exterior) requires the exterior hatch's lower-side
      compartment not to be *evacuated*, or a player order. This crossing has no openability test.
  - Note: the horizontal test uses the "evacuation" flag and the vertical test uses the "evacuated"
    flag. These are two distinct game properties. Keep both, or confirm the distinction in 2026.1.
  - The heights 101.55, 102.8 and 106 m are 2022.1 VIIC interior scene coordinates. They must be
    re-derived for 2026.1 and for every boat type, because the 2022.1 version assumed one interior.
- **Door traverse start (horizontal hatches):** if the door link has a hatch that cannot be opened,
  the traversing character's active orders are cancelled (arguments: 0 s delay and a 5 s second
  parameter, meaning to be confirmed). The vanilla traverse still runs.
- **Ladder traverse start:** the same, except for the exterior hatch, which is identified by
  "Exterior" in its object name.
- **Evacuation escape start:** the evacuate-to-safety action is made stoppable before the escape
  begins, so the player can cancel an evacuation order.
- **Compartment reacting to a character's action change:** skipped entirely for officers. Officers
  changing action never trigger the compartment's evacuation handling.
- **Interactions:** "hatch can be opened" is the pressure- and water-head-based check owned by the
  hatches/pressure spec, so navigation quality depends on it. The evacuation flags are set by
  the hatch and evacuation logic in that spec.

### G. Automation gates

These live in the navigation file but relate to other areas.

Architect note (00 C8, C27): this subsection duplicates 02 §2.10 and §4, which own it.
In 2026.1 the carry-wounded routine also recovers divers, so the gate must not suppress that.

- **Automatic carrying of wounded to safety (vanilla behavior's update):**
  - Vanilla runs normally if the "auto-carry wounded" setting is ON.
  - Otherwise it runs only when no load is in progress and a one-shot trigger is active. The trigger
    clears itself once no crew are ragdolled.
  - Otherwise it is suppressed.
  - The trigger is static and in memory. It defaults to active and is set by a "carry wounded to
    beds" action on the light switch.
- **Automatic leak blocking by sailors (vanilla behavior's update):** runs only if the
  "auto leak blocking" setting is ON, and is suppressed otherwise.

### H. *Control Room Pump* / Main Pump prefab (unfinished)

- The asset bundle contains an equipment prefab called "Main Pump". Inspecting the bundle shows
  it is built from:
  - a mesh, material, texture and box collider
  - a raycast selectable and pointer-events relay
  - a highlight effect
  - a virtual scene transform
  - an interactable slot set up like the trim pump's operation, repair and control-room slot
  - a generic activator whose banner uses the default equipment banner
  - a **vanilla trim-pump component**

  In effect it is a second trim-pump device with its own model.
- The code that would instantiate it at scene start, as a child of the control-room object in the
  VIIC interior with a fixed local offset, is commented out. The associated action is disabled
  in the action sheet, which prefixes its type name so it fails to resolve. The action's own
  availability check always returns enabled; its intended pressure-equalizing water and air
  transfer logic is commented out. A locale entry tells the player the pump is unfinished and
  to use buckets.
- Intended behavior, inferred: a crewman at a control-room main pump moves bilge water from a
  neighboring flooded compartment into the control room, where the trim pump or bilge can
  remove it. The work uses the "Default" slot with the "Skill" animation, variant 2, and has
  sorting priority 11. The action targets activators tagged "Compartment".
- **Recommendation:** out of scope for v1. If wanted later, **no custom Unity asset is required**.
  Clone the existing trim-pump GameObject at runtime (instantiate a copy and re-parent it into the
  control room), or attach the new action to the existing trim-pump or compartment activator.
  The prefab adds only a mesh, and the functional components are all vanilla.

### I. Valve automation (the automatic actions controller's update; valve-related part)

The controller's periodic update is fully replaced. Its valve part runs on every update tick
with no debounce:

1. If the target depth is exactly 0 and the valve mode is *manual blowing* or a later mode in
   the game's mode enum order (to confirm against the 2026.1 enum):
   - Stop any valve-station user running *Exhaust Blow*, *Awash Trim* or the flood action.
   - If the valve station then has no users, queue the vanilla compressed-air valves use action.
2. If the target depth is about 1.0, the diesel engine is enabled, and ballast positions 1 to 5
   hold any water:
   - Stop valve users running compressed-air use, *Awash Trim* or flood.
   - Queue *Exhaust Blow*, looked up by name in the actions database.
3. If the target depth is about 0.75:
   - Stop valve users running compressed-air use, *Exhaust Blow* or flood.
   - Queue *Awash Trim*.
4. If the target depth is > 5, the valve mode is before *flooding* in enum order, and the mode
   is not *adjustment to neutral*:
   - Stop valve users running compressed-air use, *Exhaust Blow* or *Awash Trim*.
   - Queue the vanilla flood action unless some crew member is already running it.

The controller returns a 1 s re-run interval. Steps 2 and 3 rely on the queue's own duplicate
rejection to avoid stacking. The new actions' "no duplicate actions" flag also helps.

---

## 3. Game integration points (2022.1 names; all must be re-mapped to 2026.1)

| Game type . member | Hook | Purpose |
|--------------------|------|---------|
| `CompressedAirValvesEmergencyUseAction.GetState(Activator, PlayableCharacter, bool, bool)` | Replace (prefix skips the original; result set in postfix) | Section D gating |
| `CompressedAirValvesUseAction.GetState(...)` | Replace | Section D |
| `AdjustBallastAction.GetState(...)` | Replace | Section D |
| `FloodBallastTanksAction.GetState(...)` | Replace | Section D |
| `AdjustBallastAction.OnDeviceUsed` | Postfix | Section D: surface sentinel reset |
| `FloodingAndBlowingValve.FixedUpdate` | Prefix that may skip the original; reads private `mode`, `vents` (ExternalPressure[]), `ballastTanks` (Tank[]); writes `floodingTime` | Section C |
| `PlayableSubmarine.FixedUpdate` | Prefix; reads private `currentDepthSteersRotation`, writes private `internalTargetDepth` | Section E |
| `ShipControlGUINew.OnDepthToggle` | Prefix; reads private `depthDeckAwashToggle` (Toggle); skips the original when awash is on | Sets target 0.75 m and clears the preset |
| `ShipControlGUINew.ValidateDepthToggles` | Prefix; skips the original when the target is about 0.7 or 0.75 | Keeps the awash toggle lit for the sentinels |
| `AutomaticActionsController.DoUpdate` | Replace; uses private delays, cached actions, the valve activator, and the private `AddAction` via reflection | Section I, plus the automation spec |
| `NavMeshNavigator.IsDestinationReachable(Vector3 pos, bool playerOrder)` | Replace | Section F |
| `NavMeshDoors.TraverseStart` / `NavMeshLadder.TraverseStart` | Prefix; reads private `hatch` | Section F order cancel |
| `EvacuateToSafetyAction.Escape` | Prefix (sets `CanBeStopped`) | Section F |
| `Compartment.Character_CurrentActionChanged` | Prefix; skips for officers | Section F |
| `AutoCarryWoundedToSafety.DoUpdate`, `LeakBlockingBySailors.DoUpdate` | Prefix that may skip | Section G |

**Game API used (not patched):**

- `PlayerShip`:
  - `Docked`, `TargetDepth`, `SetTargetDepth(float, bool, bool)`, `SetDepthPreset(DepthPreset, bool)`
  - `BuoyancyRatio`, `Depth`, `AverageVelocity`
  - `DieselEngine` (`.enabled`, `.Gear.RPM`), `Tanks` (filtered to `BallastTank`)
  - `GetEquipment<FloodingAndBlowingValve>()`, `HatchesLeftToRight`, `HatchToConningTower`, `ExteriorHatch`
  - `Crew`, `RagdollCrew`, `Energy.Gain` (scale modifiers)
- `FloodingAndBlowingValve`: `Mode`, `OperatingMode` {AdjustmentToNeutral, ManualBlowing, OptimalBlowing, FullBlowing, Flooding, FloodingBackwards}, `SetDisableFlag`/`HasDisableFlag(DeviceDisableFlag.TurnedOff)`
- `Tank`: `Water`, `Air`, `Capacity`, `ExtraPressure`; `ExternalPressure.Openess`
- `Hatch`: `CanBeOpened`, `LeftCompartment`, `RightCompartment`, `ObstaclePositionZ`; `Compartment.Evacuation`, `.Evacuated`
- `DeviceUseAction` / `CharacterAction`: `CheckActivatorUseConditions` with flags `RequireWorkingDevice | DisallowDuplicateActions [| CheckForBlockingItems]`, static `CheckInteractableUseConditions(..., SlotUsageFlags.AllowUnderWaterAlways, ...)`, `UseDevice`, `Progress`, `CompleteAction`; `PlayableCharacter.SkillProgress`, `IsInteracting`, `StopCurrentAction`, `CancelActiveOrders`
- `CharacterActionsDatabase.GetActionByName`; `GameTime.Ticks`

**Registration.** New actions are added as rows in a mod "Actions" data sheet. A row gives the
action key, the fully qualified type, the activator tags ("Valves"), the character tags (any),
and the contexts (Normal, Queue, NoCharacter), with group Other and subgroup Valves. Each action
type has a data-row constructor and a copy constructor (source plus activator). Whether 2026.1
loads mod-defined action *types* from mod sheets natively, or still needs the **Custom Actions**
utility mod (Workshop 2374336654), must be checked. 0.61 contains no code reference to that
mod, so the dependency would be implicit, through sheet-driven type loading and the "Settings"
activator tag used by the toggle actions. Treat it as a probable dependency until a 2026.1
check rules it out.

**Boat-type coupling:** scene object names (planes, the "Exterior" hatch, the control-room path)
and the ballast-tank list positions all assume the 2022.1 Type VIIC. 2026.1 has several boat
types, and each mapping must be data-driven or looked up per boat.

---

## 4. Settings and persistence

- No settings belong to mechanics A to E. They are always on in 0.61. **Recommendation:** add
  toggles for *Exhaust Blow* automation, *Awash Trim* (vs. vanilla awash), hydroplane visuals,
  and the valve-gating override, all defaulting ON.
- Mechanic G uses two settings: auto-carry-wounded and auto-leak-blocking. The section 2.F
  reachability check uses no setting, but it depends on the hatch-pressure-blocking setting owned
  by the hatches spec. In 0.61 each setting is persisted by adding or removing a uniquely named
  scale modifier on the player ship's energy-gain stat, which piggybacks on the save game. A
  missing modifier means OFF, so new campaigns start with all three OFF. That means vanilla
  automatic leak blocking and automatic carrying of the wounded are **suppressed by default**.
  A clean implementation should use proper mod save data, and must state the defaults explicitly.
  (Architect note: storage is the 04 §3 settings task (00 C4); defaults are user decision 00 D3.)
- The target-depth sentinels persist through the vanilla save, because target depth is saved.
  The per-action interaction-started flags and the auto-carry one-shot trigger are not saved.
  Mid-action loads restart the action from its start logic.

---

## 5. UI

- **Interaction wheel at the valve station:** two new entries:
  - *Exhaust Blow*: a label meaning "blow ballast with diesel exhaust".
  - *Awash Trim*: a label meaning "trim ballast for deck awash".
- **Icons needed for each of the two actions:** these are new art, described only:
  - wheel icon: normal, highlighted and disabled
  - cursor icon
  - action banner
  - action icon
  - status portrait (*Awash Trim* has a portrait; give *Exhaust Blow* one too)

  Motif ideas: exhaust or smoke flowing into a tank for *Exhaust Blow*; a waterline at deck level
  for *Awash Trim*.
- **Ship control panel:** no new widgets. The existing deck-awash depth toggle is repurposed to
  request ballast-based awash, and stays lit while the awash sentinels are active.
- **Progress display:** both actions show progress on the character's skill-progress bar
  (fraction of ballast blown; nearness to 40 % buoyancy).
- *Control Room Pump* (deferred): a wheel entry meaning "pump water into the control room", with
  normal, highlighted and disabled icons. While unfinished, the description says the main pump is
  not yet available and suggests buckets.
- Messages: none beyond the labels. The buoyancy tooltip that shows valve mode and target depth
  belongs to the notifications spec.

---

## 6. Acceptance criteria

Criteria marked **[game]** need an in-game check. All others can be checked by unit tests
against pure functions, or by code and patch inspection.

**Exhaust Blow**

1. The exhaust flow function returns (rpm / 60) x 0.1933768 x dt, and 0 when the diesel is disabled,
   whatever the RPM.
2. The distribution function returns 52.5 % of flow to tank position 1 and 47.5 % to position 5, for
   tanks with water > 0 only. It returns 0 for a tank with water ≤ 0.
3. Positions 2, 4 and 3 get 20 %, 25 % and 55 % only when positions 1 and 5 both hold < 5 units of
   water. Otherwise they get 0.
4. Progress = 0.999 x (capacity - water) / capacity over positions 1 to 5. It never reaches 1.0.
5. Completion fires when total ballast water is 0. On completion the valve mode is optimal blowing,
   the valve is turned off, and the depth preset is Surface.
6. On start, a target depth of 1.0, 0.0 or 12.0 becomes 0.6 with the preset cleared. A target of 3.0
   is unchanged.
7. Availability is disabled when docked. It is enabled with a working valve and a free, flooded
   "Use" slot.
8. **[game]** Surfaced on the diesels with ballast partly flooded: setting the 1 m target queues the
   action, the tanks empty without the HP air bottles dropping, and the boat ends on the Surface
   preset.

**Awash Trim**

9. On start, a target depth of 0.75 or 0.0 becomes 0.7. A target of 2.0 is unchanged.
10. Completion requires both 0.398 < ratio < 0.402 and a turned-off valve. Either condition alone is
    not enough.
11. **[game]** Toggling deck awash from the surface settles the boat at 40 ±0.5 % buoyancy, with the
    valve shutting itself off and the action completing.

**Valve override (C)**

12. The vent and pressure schedule matches the tables in section 2.C exactly for each (mode, depth,
    B%) band, including the boundaries at 5 m, 5 %, 10 %, 39.5 %, 40 % and 70 %.
13. In the hold band, after more than 10 s of accumulated fixed time steps, the valve is turned off.
14. Any mode not covered in section 2.C delegates to vanilla.

**Valve action gating (D)**

15. All four vanilla valve actions return Disabled when docked, and are not blocked by a flooded
    slot otherwise.
16. Adjust ballast, after the device is used with a target ≤ 1 m, leaves the target at 0.6 and the
    preset at None.

**Depth control (E)**

17. The stern plane angle is r x 28 degrees with r = steer x (1 - buoyancy ratio). The bow angle is
    -23r for r > 0 and -33r otherwise, then blended to 0 by clamp01(|v| - 4).
18. With a target ≤ 1 m, |v| = 3 and ratio 0.5, the internal target rises by 10 x dt per tick until
    it reaches ≥ 10.
19. A missing plane object (another boat type) does not throw. This is a requirement for the
    reimplementation; the original throws.
20. The awash toggle sets the target to 0.75. The toggle shows as on for targets 0.7 and 0.75.

**Automation (I)**

21. A target of exactly 0 with the valve in manual blowing or later stops the custom ballast
    actions and queues compressed-air use only when the station is free.
22. A target of about 1 does not queue *Exhaust Blow* when the diesel is disabled or the ballast is
    dry.
23. At most one flood action runs across the crew.

**Navigation (F, G)**

24. Reachability returns false when a crossed hatch cannot be opened, even for a player order.
25. Reachability returns false for an evacuation-flagged compartment on a non-player order, and true
    for a player order.
26. **[game]** A crewman ordered through a pressure-blocked hatch stops at it instead of clipping
    through. An evacuation order can be cancelled by the player.
27. Officers changing action do not trigger compartment evacuation handling.
28. With auto leak blocking OFF, the vanilla sailor leak-blocking behavior does not run. With it
    ON, it runs.

**Assets**

29. No custom Unity asset bundle is required for v1, because the main pump is deferred.

---

## 7. Open questions and suspected bugs

1. **Awash progress formula is broken.** min(0, ratio) clamps positive buoyancy to 0, so progress
   is always ≤ 0. The intent was probably 1 - |0.4 - ratio| x 2.5, clamped to 0 to 1. The surface
   depth bias in section 2.E uses the same min(0, ...) pattern, so it is a no-op for positive
   buoyancy and possibly meant to be max. Decide the intended behavior.
2. **Awash completion may never fire.** The valve shuts off after 10 s anywhere in 39.5 to 40 %,
   but completion needs 39.8 to 40.2 %. A boat that settles at, for example, 39.6 % is left with
   the valve off and the action stuck. Recommendation: complete when the valve is turned off
   and B% is in [39.5, 40.5].
3. **Exhaust Blow completion uses exact equality on float water.** It may never complete. Use a
   small epsilon.
4. **Exhaust Blow has no surfaced or diesel prerequisite** and does not stop when the diesel stops.
   Recommend requiring the deck to be above water (or snorkel use) and the diesel to be enabled with
   RPM > 0, and cancelling when either fails.
5. Stage 2 of the exhaust distribution adds to stage 1, so total injection is up to 200 %. This is
   probably harmless, because stage 1 tanks are nearly empty by then. Confirm whether it is intended.
6. **Tank indexing is fragile.** The action uses ship ballast-tank list positions 1 to 5 and ignores
   position 0; the valve override uses its own 5-entry arrays at positions 0 to 4. The physical tank
   at each position is unknown. The likely Type VIIC layout is No. 1 aft, No. 2/4 saddle tanks, No. 3
   internal centre and No. 5 forward. It must be mapped by name or role in 2026.1.
7. Section D drops the explanation details from availability checks (only the bare state is
   returned). It also removes every extra vanilla precondition. Audit the 2026.1 vanilla bodies.
8. The surface sentinels at 0.6, 0.7, 0.75 and 1.0 m can collide with a player dialing those depths
   manually, and with 2026.1 depth presets. Strongly recommend replacing them with an explicit
   mod state ("ballast intent"), saved with the game.
9. Who sets the 1.0 m target that triggers *Exhaust Blow*? Nothing in the mod writes it, so it
   presumably comes from a vanilla preset or the player's dial. This must be confirmed in 2026.1,
   or a UI entry must be provided.
10. The meaning of the "extra pressure" field on tanks, and of the second argument to
    `CancelActiveOrders` (5), are unknown.
11. The automation replaces the whole controller update, including vanilla behaviors the mod did
    not reimplement. Diff it against 2026.1.
12. With mechanic G, vanilla automatic leak blocking and automatic carrying of the wounded are
    effectively OFF by default in 0.61. Confirm that the defaults are intended.
13. Officers being exempt from compartment action-change handling may let officers remain in
    flooding compartments. Confirm the intent: probably to let officers keep working on leaks.
