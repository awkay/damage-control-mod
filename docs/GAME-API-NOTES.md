# UBOAT 2026.1 game API notes (damage control)

Clean-room map of the game's own API, written from the decompiled game assemblies
(`com.uboat.game`, `com.uboat.modapi`) and `com.playway.water.Runtime.dll`. Type and
member names are facts about the game; everything else is our own description. No
game method bodies are reproduced. "(inferred)" marks conclusions not directly seen
in code.

Paths below are relative to `uboat-2026.1-decompiled/com.uboat.game/`.

## 0. The one big structural fact

There are **two separate flooding simulations**:

| Simulation | Used by | Code | Burst? |
|---|---|---|---|
| **Interior tank model**: `Compartment`/`Tank`/`Hatch`/`ExternalPressure`/`LeakableEquipment` | The **player U-boat** (`PlayerShip : PlayableSubmarine`) | managed C#, `FixedUpdate` + `ExecutionQueue` update listeners | **No** - fully Harmony-patchable |
| **Abstract hull model**: `Hull` + `HullData` (box compartments, `NativeArray` state) | AI ships/subs (crew is `NonPlayableCrew`) | `Hull.Update` calling Burst jobs/functions | **Yes** for the inner math |

`PlayerShip.HasHullHoles` is hard-wired `false`, and `Hull` resolves its crew as a
`NonPlayableCrew`. Conclusion (inferred, but strongly supported): **a damage-control mod
for the player boat lives entirely in the managed interior model**; the Burst code only
matters if we also want to change how enemy ships flood/sink.

Buoyancy of every floating entity (player included) goes through PlayWay's
`NeuralNetworkPhysics`, which *is* partly Burst (section 2).

## 1. Player-boat flooding, pressure, leaks, hatches, ballast

All in assembly `com.uboat.game`.

### 1.1 `Tank` - `UBOAT.Game.Scene.Physics.Tank : Device` (`UBOAT.Game.Scene.Physics/Tank.cs`)

Base for every fluid volume: ballast tanks, trim tanks, fuel, compressed air,
the hull-internal tank, **and compartments**. `TankType` enum: `BallastTank,
QuickDivingTank, CompressedAirTank, Compartment, DrinkableWaterTank, FuelTank, TrimTank`.

| Member | Notes |
|---|---|
| `double Water { get; set; }` | m^3 of water. Setter clamps at 0, recomputes centre of mass, raises `WaterAmountChanged`. Primary write point for any flooding/pumping we add. |
| `double Air { get; set; }`, `double Fuel` | Air is an amount, not a pressure. |
| `double AirPressure` | air / (capacity - water - fuel). The compartment "air pressure" in bar-ish units. |
| `float FluidsPressure` | outside-water pressure at this tank: ~1 + depth*0.1 (+ `ExtraPressure`). |
| `float Capacity`, `float Mass` (water*1000), `Vector3 CenterOfMass`, `float CenterOfMassImpact` | read by `PlayableSubmarine.FixedUpdate` for mass/trim. |
| `float WaterElevation` | surface height of the fluid inside (interior-space Y, offset by ~100; `Compartment` compares against `FloorElevation`). |
| `ReadOnlyCollection<ExternalPressure> Holes`, `AddHole(ExternalPressure)`, `RemoveHole(ExternalPressure)` | openings to the sea (or vents). |
| `event Action<Tank> WaterAmountChanged` | per-tank change notification. |
| `TankType TankType`, `bool StabilizedMode` | |

Tick: `Tank.FixedUpdate` (protected, Unity message) iterates `holes` and calls private
`SimulateBreach(ExternalPressure, bool isInBottom)`, which exchanges water and air with
the sea using `breach.HoleSize * breach.Openess`, the air/sea pressure difference and
`Time.fixedDeltaTime` (clamped). Visual fluid surface is a separate `DoUpdate` on the
`ExecutionQueue`. `BallastTank` and `HullInternalTank` are empty/thin subclasses.

### 1.2 `Compartment` - `UBOAT.Game.Scene.Items.Compartment : Tank` (sealed, `UBOAT.Game.Scene.Items/Compartment.cs`)

| Member | Notes |
|---|---|
| `new double Water`, `new float WaterElevation` | redirect to `MergedCompartment` when set (merged upper/lower decks). Always use these, not `Tank`'s. |
| `float FloorElevation`, `float WaterElevationAboveFloor` | depth of water on the deck. |
| `float AirToxicity`, `bool IsAirToxic` | derived from `AirPressure`, the boat's `"Oxygen"` resource and water height; toggles `Choking` status on characters. |
| `List<PlayableCharacter> Characters`, `Compartment[] NeighbouringCompartments`, `int HorizontalIndex`, `int VerticalIndex` | topology; horizontal index orders bow-stern. |
| `bool Evacuation { get; set; }`, `bool Evacuated`, `event Action<Compartment> EvacuationChanged`, `Hatch[] HatchesToCloseDuringEvacuation` | game's evacuation logic. |
| `bool ElectricityEnabled`, `SetElectricityDisableReason(string, bool)`, `HasElectricityDisableReason(string)` | reason-keyed power cut (useful for "flooded switchboard"). |
| `AddPressureApplyPoint/RemovePressureApplyPoint(CompartmentPressure)` | equipment that takes pressure damage when submerged. |
| `Contains(Vector3)`, `GetRandomNavMeshPoint()`, `GetRandomUnoccupiedNavMeshPoint()` | |

Tick: private `LowFrequencyUpdate()` is an `ExecutionQueue.AddTimedUpdateListener` callback
(returns the next delay, 4 s): for submerged `CompartmentPressure` points it sets
`IsBeingDamaged` (adds/removes the `PressureDamage` equipment status) and may call
`Target.AddWaterDamage(...)`; it also sets `InteractableSlot.IsBelowWater`. `Update()` only
handles the air-toxicity toggle.

`CompartmentPressure` (`Items/CompartmentPressure.cs`): `Elevation`, `Target` (Equipment),
`Pressure`, `IsUnderWater`, `IsBeingDamaged`. `PressureDamage` is an `EquipmentStatus`.

Pressure-area findings (WS1):

- `CompartmentPressure.Elevation` is metres above the compartment **floor** (vanilla compares it with
  `WaterElevationAboveFloor`), but the `Pressure` and `IsUnderWater` getters compare it with the
  absolute `WaterElevation` (~100), so in practice they always return the compartment's
  `FluidsPressure`. The mod keeps that for non-hatch points (00 §4) and only changes interior-hatch points.
- The `IsBeingDamaged` setter never clears its private `pressureDamage` field on `false`, so after one
  detach a point can never re-attach the status. The P6 tick writes the field itself
  (`Game/Pressure/CompartmentTickPatch`, `PressureDamageStatus`).
- Private fields used: `Compartment.mergedCompartment`, `oxygen` (`Resource`), `pressurizedEquipment`,
  `interactableSlots`, `devices`; `InteractableSlot.waterElevationToleration` (float) and
  `interactionType` (a plain **int** fed to the animator's `interaction_type`, not an enum);
  `AirCompressor.tanks`, `oxygenCompression` (`Parameter`), `maxPressure`; `Rebreather.durability`
  (`Parameter`, wear is counted only while worn, from `OnEquipped`).
- Slots register in `Compartment.Awake` (`AddInteractableSlot`), before `[Inject]` gameplay singletons
  may be set, so P7 resolves the ship through `GetComponentInParent<PlayerShip>()`; its
  `HatchToConningTower` and `ElectricEngine` are serialized fields.
- Bilges are plain `Storage` objects whose name contains "Water", parented under their compartment;
  vanilla `Storage.GetStateText` itself picks them out that way. The galley is the compartment holding
  the `Kitchen` (a `Storage`). Compartment and tank names localize as `Activators/<Equipment.Name>`.
- `Rebreather.Awake` is 19 bytes of IL (inlining risk); patch `OnEquipped` instead.

### 1.3 `ExternalPressure` - `UBOAT.Game.Scene.Items.ExternalPressure` (`Items/ExternalPressure.cs`)

A hole/vent to the sea attached to a tank. `HoleSize`, `float Openess {get;set;}`,
`Pressure` (1 + 0.1/m of depth at the hole), `IsUnderWater`, `IsVent`,
`LocalPosition`, `LeakLocalPosition`, `Target` (Equipment).
Static events `Added`/`Removed`. Changing `Openess` is how the game modulates inflow.

### 1.4 Leaks - `LeakableEquipment : Equipment` (`Items/LeakableEquipment.cs`)

A breakable pipe/valve/hull fitting in a compartment that owns an `ExternalPressure hole`.

- `Compartment Compartment`, `bool IsLeaking`, `Parameter LeakIntensity`
  (base delta 0.01; other code adds scale modifiers), `Activator Activator` (same GameObject).
- Leak starts when `Equipment.Health` reaches 0 and stops when health becomes > 0
  (private `Leak_HealthChanged` on `Equipment.HealthChanged`). Start adds the `Leaking`
  status (`Items.Statuses/Leaking.cs`) and registers a per-frame `DoUpdate` on the
  `ExecutionQueue`; that `DoUpdate` sets `hole.Openess` = 0.35 * `LeakIntensity` *
  damage-difficulty multiplier (data row `General/Settings/DamageDifficulty/Multiplier`).
- Notifications: `PlayerShip.LeakStarted` / `LeakStopped` events
  (`event Action<LeakableEquipment>`), raised via `PlayerShip.OnLeakStarted/OnLeakStopped`
  and mirrored on `PlayerShipProxy`.
- `UBOAT.Game.Scene.Tasks.LeakBlockingBySailors` (a background task) listens to those events
  and auto-assigns sailors to block leaks.
- `Equipment` base: `float Health {get;set;}`, `Parameter MaxHealth`, `AddDamage(...)`,
  `virtual AddWaterDamage(float, bool)`, `AddStatus/RemoveStatus/GetStatus<T>/HasStatus<T>`,
  events `Damaged`, `HealthChanged`, `StatusAdded`, `StatusRemoved`, static `EquipmentAdded/Removed`.

### 1.5 Hatches - `Hatch : Device` (sealed, `Items/Hatch.cs`)

`Compartment LeftCompartment`, `RightCompartment`, `bool Locked {get;set;}` (raises
`LockStateChange`; also drives an optional `ExternalPressure` to 0/1 for exterior hatches),
`bool CanBeOpened` (false if `PressureBlockadeCompartment` is flooded > 1 m and its
`FluidsPressure` > 9), `Openess`, `IsAnimating`, `IsExteriorHatch`,
`IsPreferredToBeLocked`, `WasUnlockedByPlayer`, `Animate(bool, float)`.

Tick: private `DoUpdate()` registered with `ExecutionQueue.AddUpdateListener` when both
compartments exist. While **not** `Locked` it equalises `Air` between the two compartments
and moves `Water` across using private `GetTransfer(Compartment)` (water above the sill,
capped per frame and scaled by `gameTime.ArtificialTimeScale`). A locked hatch is a
perfect seal - there is no bulkhead/hatch leakage or pressure-failure model for the player boat.

Hatch lists on the boat: `PlayerShip.HatchesLeftToRight`, `HatchToConningTower`,
`ExteriorHatch`, `CargoLoadingHatch`. Actions: `HatchOpenCloseAction`,
`EvacuateCompartmentAction`, `StopEvacuationAction`.

Details used by WS2 (2026.1):

- Private fields: `lowestPoint` (Transform; vanilla's water sill, the 02 `lowY`), `pressureBlockade`
  (bool, default true), `verticalPassage` (bool; left = lower, right = upper compartment, confirmed by
  vanilla's overflow direction), `isAnimating`, `occlusionPortal` (from `GetComponentInParent` in `Start`),
  `flowEffectLeft`/`flowEffectRight` (ParticleSystem), `leftCompartment`/`rightCompartment`,
  `externalPressure`, `obstacle` (NavMeshObstacle).
- `DoUpdate` is registered in `Start` only when both compartments exist, and removed only in `OnDestroy`,
  so it keeps running while the hatch is disabled. Vanilla moves **no** air through horizontal hatches;
  only the vertical passage exchanges air.
- Vanilla open horizontal hatch, water: each side offers `min(W, s × max(0, WE - sillY), W × sillY / WE)`
  (only while it holds water above the sill), with `s = min(8, game dt × k)`, `k = 2`, damped to
  `2 / sqrt(S)` above 1x (S = Unity time scale × ArtificialTimeScale). The net (left offer minus right
  offer) moves, clamped to the receiver's room. So at 1x it moves 2 m3/s per metre of level difference,
  ignoring air pressure. Vertical passage: air `(pL - pR) × s × 0.2`; water drains down at `0.15 × s`
  while the lower side is under 99.5 % full, and anything above 99.5 % is pushed up at once.
  WS2 matches the horizontal water rate, including under time compression via 0.5 s substeps
  (WS2-API §2).
- `CanBeOpened` (vanilla): `!pressureBlockade` → true; with a blockade compartment, false only when it
  holds more than 1 m of water and its `FluidsPressure` exceeds 9; without one, on a ship, true iff
  `TargetDepth < 2` and `DeckDepth < 2.5`. Read by `HatchOpenCloseAction.GetState`, which disables Open
  with `Actions/HatchOpenCloseAction/Pressure Warning` (or the two exterior submerged warnings).
- `OnDisable` (private, declared on `Hatch`) unlocks the hatch unless the game is quitting and it has a
  nav obstacle. `GameState.State`: `NotStarted, Starting, Running, Gameplay, Quitting`.
- Callers of the `Locked` setter include `NavMeshDoors`/`NavMeshLadder` traversal (crew opening on the
  way), `StopEvacuationAction`, `HatchLockGUI`, `PortWorkersController` (cargo hatch) and the toilet door,
  which is a compartment-less `Hatch`.
- Locale keys: `Activators/Hatch Pressure Durability` (`{0:0}` atm), `Activators/Hatch Pressure`,
  `Activators/Compartment Evacuated` (already red), `Activators/Leak - Pressure`. State texts accept TMP
  rich text (`<color=...>`). Sound tag `"Pressure Damage"` (`ISoundEffectsDatabase.GetClip(tag, Transform)`).
- Crew/Actions row `HatchOpenCloseAction` uses activator tag `Hatch`, contexts Normal, Queue, NoCharacter.
- Equipment statuses: `Leaking` and `IsBeingRepaired` are plain `EquipmentStatus` classes; `Status<T>.Hidden`
  is the visibility flag vanilla checks. `RepairAction` adds `IsBeingRepaired` on start.

### 1.6 Ballast, trim, buoyancy (player)

- `PlayableSubmarine : Ship` (`Scene.Entities/PlayableSubmarine.cs`): `Tank[] Tanks`,
  `Tank[] BallastTanks`, `Tank[] QuickDivingTanks`, `Tank HullInternalTank`, `BaseMass`,
  `BuoyancyRatio`, `CurrentBalance`, `CustomBalance`, `Depth`, `TargetDepth`,
  `bool FloodBallastTanks`, `SetDepthPreset`, `SetTargetDepth`, event `TargetDepthChanged`.
  **`protected new void FixedUpdate()`** sums `Tank.Mass * CenterOfMassImpact` over all
  tanks (compartments included) into `rigidBody.mass` and `rigidBody.centerOfMass`, and
  derives a buoyancy factor from `NeuralNetworkPhysics.Volume/BuoyancyIntensity`. This is
  where flooding becomes sinking/trim.
- `PlayerShip` adds `FloodAllBallastTanksInstantly()` (internal), `BlowAllBallastTanksInstantly()`,
  `RepairEverything()`, `Alarmed`, `AlarmStarted/Stopped`, `Emergency`, `WarningDepth`,
  `CurrentColliderBottomDepth`, `InitializationComplete` (UniTask), `EquipmentDamageProtection`.
- `FloodingAndBlowingValve : Device` (`Items/FloodingAndBlowingValve.cs`): `OperatingMode Mode`
  (`OptimalBlowing, FullBlowing, ManualBlowing, AdjustmentToNeutral, Flooding,
  FloodingBackwards, Paused, ...`), `BallastFillRatio`, `InputTanks`; works in `FixedUpdate`.
- `TrimPump : Device` (`Items/TrimPump.cs`): `IsWaterLeft`, nested `ConnectedTank`; works in `Update`.
  Related actions: `AdjustBallastAction`, `FloodBallastTanksAction`,
  `PartiallyFlood/PartiallyBlowBallastTanksAction`, `SwitchTrimPumpAction`,
  `CompressedAirValvesUseAction`, `CompressedAirValvesEmergencyUseAction`.
- Crush depth: `UBOAT.Game.Scene.Tasks.HullCrushController : BackgroundTaskBase` reads the
  sandbox parameter `"CrushDepth"` per entity, starts a timer below it (events
  `HullCrushStarted/HullCrushCancelled`) and sets `Entity.Health = 0` when it expires.
  Polled from a timed `DoUpdate`; `GetCrushTime(Entity)` is public.

## 2. Burst-compiled parts (not Harmony-patchable)

Burst-compiled code runs as native code; a Harmony patch on the C# method does nothing
while Burst is enabled. Everything below was checked for `[BurstCompile]`.

| Burst code | Where | Managed caller (patchable) | Write-back to managed state |
|---|---|---|---|
| `Hull.PropagateBallastJob : IJob` (private struct; inputs `NativeArray<ShipCompartmentBurstData>`, connections, capacities, `ConnectionStatus`, `NativeArray<ShipCompartmentState>`) | `Items/Hull.cs` | `Hull.Update()` (private Unity message) builds it and calls **`.Run()`** - synchronous on main thread, no `Schedule/Complete` | mutates `Hull.compartmentStates` in place |
| `Hull.SimulateBreachesJob : IJob` (breaches -> inflow) | same | `Hull.Update()`, `.Run()` | same `compartmentStates` |
| static `Hull.SimulateBallast(...)`, `SimulateBallastCrackedHull(...)`, `ComputeBallastCenterOfMass(...)`, `FindRepairableBreach(...)` - `[BurstCompile]` static methods taking raw pointers, dispatched through generated `*_BurstDirectCall` delegates | same | `Hull.Update()` (ballast), `Hull.SimulateDamageControl(float)` (private; AI crew patching breaches) | out params -> `TotalBallast` (private setter applies the `"Flooding"` mass modifier and recomputes `Health`), `ballastCenterOfMass` |
| `SphereBoxIntersectionCompute.ComputeWeightedIntersectionVolume` | `Scene.Tasks/SphereBoxIntersectionCompute.cs` | `Hull.DistributeDamageInSphere` (private) | return value |
| `PlayWay.Water.Extensions.NeuralNetworkJobsRunner.ComputeNeuralNetworkJob : IJob` (`[BurstCompile(Standard, Default)]`) - the learned buoyancy/drag network | `com.playway.water.Runtime.dll` (decompile with ilspycmd) | scheduled from static `NeuralNetworkPhysics.Execute(List<NeuralNetworkPhysics>, NeuralNetworkPhysicsTemps[], float)`, which is driven by `NeuralNetworkPhysicsRunner.FixedUpdate()`; completed in the same `Execute` or in `RemovePhysics` | outputs read back in `Execute`, applied to the rigidbody via `NeuralNetworkPhysics.SimpleUpdate` (private) |

Notes and uncertainty:

- The generated `*_$BurstManaged` twins (e.g. `Hull.SimulateBallast$BurstManaged`) are the
  managed fallbacks; patching them only has effect if Burst is disabled
  (`BurstCompiler.IsEnabled == false`). Do not rely on it.
- `Hull`'s data types are plain structs and safe to read from managed code on the main
  thread (jobs use `.Run()`, so no race): `ShipCompartmentState {ballast, damage, waterNormal}`,
  `HullBreach` struct `{compartmentIndex, inflowRate, radius, localPosition, initialWaterNormal}`,
  `ShipCompartment {capacity, center, size, armorThickness, pressureResistance, type, ...}`,
  `ShipCompartmentConnection {compartmentA, compartmentB}`. Public accessors:
  `Hull.CompartmentStates`, `Breaches` (`NativeList<HullBreach>`), `HullData`,
  `TotalBallast`, `AddBreach(...)`, `DamageCompartment(...)`, `StopAllLeaks()`,
  `AllowDamageControl`, static event `CompartmentExploded`.
- To change AI-ship flooding: Prefix/Postfix `Hull.Update` and edit `CompartmentStates`/
  `Breaches` before/after, or patch `Hull.SimulateDamageControl`. The per-step maths is out of reach.
- `NeuralNetworkPhysics` exposes patchable managed knobs: `Volume`, `BuoyancyIntensity`,
  `MaxAllowedStabilizationFactor`, `GetTotalBuoyancy(float)`, `AddContinuousForce(Vector3)`,
  `AddContinuousForceFunc(...)`. Prefer feeding mass/forces through these or through
  `PlayableSubmarine.FixedUpdate` rather than touching the network.
- Other Burst code in the game (`UpdateObservationsJob`, terrain `WorldSegmentGenerator`,
  `Burst_DynamicContactVisualizationUI`) is unrelated to damage control.
- **None** of the player-interior model (`Tank`, `Compartment`, `Hatch`, `ExternalPressure`,
  `LeakableEquipment`, `PlayableSubmarine`) is Burst or uses jobs.

## 3. Crew actions and the interaction wheel

Namespaces `UBOAT.Game.Scene.Characters.Actions` and `...Characters.Activators`.

### 3.1 Class hierarchy

```
CharacterAction (abstract, ICharacterAction)
  SingleCharacterAction (abstract)       one main character + workers + followers
    ProgressingCharacterAction (abstract) adds OnActivated()
    AnimatedAction (abstract)             animator-driven
    DeviceUseAction (abstract)            walk to slot, play anim, OnDeviceUsed()
      RepairAction, BlockLeakAction, ...
    DamageControlAction                   alarm-time inspection at a CommandStation
  BasicWorkAction                         multi-character
```

### 3.2 `CharacterAction` overridable surface (`Actions/CharacterAction.cs`)

Constructors (all `protected`): `()`, `(Activator)`, `(CharacterAction source, Activator)`,
`(GameDataRow data)`. **A subclass must provide both the `(GameDataRow)` constructor** (used
by the database to build the template) **and the `(TSelf source, Activator)` copy
constructor** (used by `Duplicate`, which calls `System.Activator.CreateInstance(GetType(), this, activator)`).

Abstract: `protected abstract void StartCharacter(PlayableCharacter)`,
`protected abstract void StopCharacter(PlayableCharacter)` (sealed in `SingleCharacterAction`).

Virtual (exact signatures; `Activator` = `UBOAT.Game.Scene.Characters.Activators.Activator`):

```csharp
public virtual async UniTask<bool> OnPreStart(PlayableCharacter character)   // only async hook
public virtual ActionStateData GetState(Activator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
protected virtual ActionState GetState(Activator activator, PlayableCharacter character)
protected virtual void StartAction();  protected virtual void StopAction();
protected virtual void OnStageChanged();  protected virtual void OnAddedToActionQueue(ActionQueue actionQueue)
public virtual CharacterAction Duplicate(Activator activator)
public virtual LocalizedString GetLocalizedName(Activator, PlayableCharacter, ActionActivationContext)
public virtual string GetLocalizationKeyword(Activator, PlayableCharacter)   // default: GetType().Name
public virtual string GetLocalizedDescription(Activator, PlayableCharacter)
public virtual string GetLocalizedWarnings(Activator, PlayableCharacter)
public virtual string GetLocalizedState()
public virtual string GetIconName(Activator, PlayableCharacter)            // default: localization keyword
public virtual string GetVisualCluePath(Activator, PlayableCharacter)
public virtual int GetSortingPriority(Activator, PlayableCharacter)
public virtual ActionProfit GetActionProfit(Activator, PlayableCharacter)   // AI utility
public virtual float GetActionProfit(Activator, PlayableCharacter, float baseProfit)
public virtual bool ShowInTabMenuForActivator(Activator)
public virtual bool IsInterchangeable(CharacterAction other, Activator otherActivator)
public virtual bool DisplayAlternativeActivationUI(Activator, PlayableCharacter); public virtual void StopDisplayingAlternativeActivationUI()
public virtual void GetStageViewActions(List<LocalizedString>); public virtual void OnStageViewActionActivated(int, CameraTransitionType = FadeInOut)
// virtual properties
bool AllowDelegationToOtherCharacters, AllowGrouping, AllowMultipleTargetsInTabMenu,
     CanBeStartedWithoutCharacter, ExecuteInPersonalQueue, IsForcedAction,
     IsLowPriorityAction, IsInterruptable;  protected bool StartWithTransitionInFPP
```

`SingleCharacterAction` adds: `protected virtual void StartMainCharacter/StopMainCharacter/
StartWorker/StopWorker/StartFollower/StopFollower(PlayableCharacter)`, **`public virtual void
UpdateAction()`** (registered as an `ExecutionQueue` update listener while the main character
runs it - the per-frame work hook), `SetFollowerLimits(...)`, `Character`, `Workers`, `Followers`.
`DeviceUseAction` adds `protected abstract void OnDeviceUsed()`, `protected virtual float
AnimationTimeout`, and the helper `UseDevice(character, slotTag, animatorParameter,
interactionType, ...)`. `ProgressingCharacterAction`: `protected abstract void OnActivated()`.

**No `CancellationToken` anywhere in the action API.** Cancellation is imperative:
`PlayableCharacter.StopCurrentAction(bool ignoreCanBeStoppedFlag = false)`,
`PlayableCharacter.CancelOrders()`, `CharacterAction.CompleteAction()`,
`RemoveCharacter/RemoveAllCharacters`, `ActionQueue.Remove(action, completeAction)`.
`OnPreStart` returning `false` aborts the start.

Other members: `Stage` (`CharacterActionStage`: Uninitialized/Initialized/Running/Completed),
`Progress`, `QueuePriority`, `IsDirectPlayerOrder`, `Characters`, `Activator`, `Role`,
`HardRequirements`/`SoftRequirements`; events `Changed`, `CharacterAdded`, `CharacterRemoved`,
`StageChanged`; helpers `CheckUseConditions`, `CheckActivatorUseConditions`,
`CheckInteractableUseConditions`, `CheckDefaultStateConditions`. `ActionState` enum:
`Hidden, Disabled, Utility, Enabled`.

### 3.3 Registration (data-driven)

`CharacterActionsDatabase : BackgroundTaskBase` (`Actions/CharacterActionsDatabase.cs`)
loads every row of data path **`Crew/Actions`** in `Start()` -> private `LoadActions()`.
Columns by index: 0 action name (key), 1 **type name resolved with `Type.GetType`**,
2 activator tags (array), 3 character tags (= also read as hard requirements by the base
ctor), 4 soft requirements, 5 contexts (`Normal`, `Queue`, `NoCharacter`, `Upgrade` ->
`ActionContext` flags), 6 group, 7 subgroup. One template instance per row is created via
the `(GameDataRow)` ctor. Lookups: `GetActionByName`, `GetActionsForActivatorTag`,
`GetActionsForCharacterTag`, `GetActionInfo(action)`.

Gotcha: `Type.GetType` only sees game assemblies; types in a mod assembly (loaded from
bytes by the game's Roslyn `ScriptDomain`) will most likely not resolve, and the row is
silently skipped (inferred; the existence of a workshop "Custom Actions" utility that
patches `LoadActions` corroborates this). `BackgroundTasksManager`, by contrast, uses
`ModManager.GetType(name)`, which does search mod assemblies. Our options: Postfix
`CharacterActionsDatabase.LoadActions` and register our rows by resolving with
`ModManager.GetType`, or reuse a game action type and only add data rows.

Implemented (WS0, `Game/Registration/ActionRegistrationPatch`): a postfix on the private
`LoadActions` takes the private dictionaries `actionsByName`, `actionsByActivatorTag` and
`actionsByCharacterTag` (Harmony `___field` injection). It re-walks `Crew/Actions` and registers rows
whose type is in the `DamageControl.` namespace, resolved in our assembly only. The template is built
through the `(GameDataRow)` constructor, as the game does. The postfix runs at `Priority.Last` and skips
names already present. Note that `CharacterActionsDatabase` is a `GameplayStart` task, so `LoadActions`
runs again on every entry to gameplay, each time on a fresh instance.

### 3.4 How an action reaches a target and the wheel

- `Activator : MonoBehaviour` (abstract, `Activators/Activator.cs`) sits on interactable
  things. Key: `IReadOnlyList<string> Tags`, `AddTag/RemoveTag/HasTag/HasTagStartingWith`,
  `Equipment AssociatedEquipment`, `HasAssociatedEquipment`, `Interactable Interactable`,
  `List<PlayableCharacter> Users`, `Entity ParentEntity`, `IsInInterior`,
  `IsParentedToPlayerBoat`, `abstract string BannerPrefabPath`,
  **`public abstract void AddActionInfos(List<Skills.CharacterActionInfo>, PlayableCharacter, ActionContext, bool isPlayerOrder)`**,
  `GetActionInfos(...)`, `GetActionInfo<T>(...)`, `OnStateChanged()`; events `ActionStarted`,
  `ActionStopped`, `StateChanged`, `ActivatorTagAdded/Removed`, static `ActivatorEnabled/Disabled`.
- `GenericActivator.AddActionInfos` collects database actions whose activator tag matches one
  of its tags (or a sandbox status name on its entity), filters by context and character
  tags (character statuses / held-item tags), and adds
  `Skills.CharacterActionInfo(CharacterAction ActionTemplate, ActionState State)` using the
  template's `GetState(...)`. **So adding a tag to an activator + a `Crew/Actions` row is the
  normal way to contribute a wheel entry.**
- Repair-like actions find their target through the activator, not a search:
  `RepairAction`/`BlockLeakAction` use `activator.AssociatedEquipment` (BlockLeak hides unless
  that equipment has a visible `Leaking` status; it scales `LeakableEquipment.LeakIntensity`
  with a `"BlockLeak"` scale modifier while interacting). `DamageControlAction` uses a
  `CommandStation`/`InspectionSpot`.
- `ActivatorRegistry : BackgroundTaskBase, IActivatorRegistry, ISingleton`:
  `GetActivators(string tag | string[] tags, List<IActivator>)`, `HasActivators(tag)`,
  `GetActivatorsDirect()`, events `ActivatorAdded/Removed` (by tag).
- Interaction wheel: `UBOAT.Game.Scene.UI.InteractionWheelUI` (`IInteractionUI`, `ISingleton`),
  buttons `UBOAT.Game.UI.InteractionWheelButtonUI`. `Open(IActivator, IPlayableCharacter)`,
  `Close()`; private `CreateButtons()` -> `activator.GetActionInfos(...)`, `AddActions`,
  `AddOtherOfficerActions`, `FilterActions()`. On click it calls
  `ActionTemplate.Duplicate(activator)` then `PreStartAction` -> `ActionQueue.ResetAndAddPreStarted`
  or `ActionQueue.Add`, or `PlayerCrew.GlobalActionQueue.Add` for crew-less actions.
- `ActionQueue` (`Characters/ActionQueue.cs`): `UniTask<bool> PreStartAction(CharacterAction)`,
  `UniTask Add(CharacterAction)`, `UniTask ResetAndAdd(...)`, `AddPreStarted`,
  `ResetAndAddPreStarted`, `Remove`, `Clear`, `HasAction<T>(...)`, `GetAction<T>(...)`;
  events `ActionAdded/Removed`. Starting an action from code = duplicate a template, then
  queue it on a character's `ActionQueue`.
- Localisation of actions: name key `Activators/<keyword>` (with optional `... FPP` variant),
  description `Activators/<keyword> Short Description` or `... Description`, state
  `Activators/<keyword> Progressing Description`; icon name defaults to the keyword.

## 4. Data sheets and locales

### 4.1 Loading (`UBOAT.Game.Core.Data.GameData`, `Core.Data/GameData.cs`)

- `GameData` is a `ScriptableObjectSingleton` (also `GameData.Instance`, `IGameData`).
  Base sheets are every `*.xlsx` in the game's spreadsheet folder (source id `"UBOAT"`).
- Each mod's `Data Sheets/**/*.xlsx` (also inside `Version X` / `Version X-Y` subfolders
  matching the game version) is loaded by the `Mod` constructor via
  **`gameData.LoadSpreadsheet(string filePath, string sourceId = mod name)`**, during
  `ModManager.AwakeSingleton` - i.e. before any mod code runs.
- Row path = `<file stem>/<sheet name>[/<sub>]/<column A value>`. The file stem is cut at
  its **last dot**, so `Crew.DamageControl.xlsx` merges into the `Crew` namespace
  (rows land in `Crew/Actions/...`). This is the expected naming for mod sheets.
- Merging is **per row, last writer wins** (`rowsMap[path] = row`); there is no
  column-level merge, so overriding a base row means re-supplying the whole row. New
  rows simply add. `GetDataRows(prefix)` returns all rows under a path prefix.
- Header rows: a row whose column A starts with `/` defines column keys for the rows below
  (`/` or `/.` = no extra path segment; another `/Name` adds a sub-path unless it equals
  the sheet name suffix). Rows starting with `//` are comments; rows with empty column A
  are skipped. A header cell `…` expands `key=value;key=value` cells into extra columns.
- Parsed sheets are cached as `<persistentDataPath>/Data Sheets/<sourceId> - <stem>.dat`,
  keyed by `Application.version`; staleness uses file *access* time. Delete the cache if
  edits don't show up.
- Reading: `gameData.GetDataRow(path)`, `GetDataRows(path)`, `GetParameter(key[, type|index])`;
  `GameDataRow.GetString/GetArray/GetBoolean/GetParameter(index|key)`; the convenience
  `new GameDataReference("Sheet/Path/Row")` with implicit `float`/`bool`/explicit `int`
  conversions (lazy lookup - safe as a static field). `GameData.Changed` fires after any load;
  `ModManager.ReloadUserModSpreadsheets(name)` hot-reloads a mod's sheets.

### 4.2 Locales (`UBOAT.Game.Core.Data.Locale`, `Core.Data/Locale.cs`)

- Locale rows come from spreadsheets under `Locales/...` (i.e. a file stem `Locales`, e.g.
  `Locales.DamageControl.xlsx`; one worksheet per category). Columns: 0 key,
  1 English, 2 German, 3 Polish, 4 French, 5 Italian, 6 Spanish, 7 Russian,
  8 Simplified Chinese, 9 Korean, 10 Turkish, 11 Portuguese, 12 Czech, 13 Japanese, 14 Ukrainian.
- Lookup key is `"<Sheet>/<key>"` (row path minus `Locales/`), except sheet `General`,
  whose keys are bare. English is loaded first as fallback, then the Steam language.
- API: indexer `locale[key]` (missing -> returns the last path segment of the key),
  `locale[tab, key]`, `RetrieveString(key)` (null if missing), `RetrieveString(key, default)`,
  `Format(key, args)`, `FormatSafe(...)`. `LocalizedString(keyword[, args])` defers lookup;
  `LocalizedString.CreateUnlocalized(text)` for raw text.
- **Strings are loaded once in `Locale.AwakeSingleton`**, which itself awakes `GameData` and
  `ModManager` first, so mod locale sheets loaded through the normal folder path are
  included; sheets loaded later (hot reload / manual `LoadSpreadsheet`) are not re-read (inferred).

## 5. Mod lifecycle, injection, settings

- Mod layout/manifest (`ModManager.ModManifest`): `name, version, description, author,
  supportedGameVersions[], assemblyName, permissions[], steamFileId`. Permissions parse to
  `Mod.SecurityOptions` flags `IO, Reflection, Net, NativeAccess`. **Harmony (`0Harmony`)
  is only referenced for compilation when `Reflection` is granted**; `System.IO`,
  `System.Reflection`, `System.Net` namespaces are denied without the matching permission,
  `System.Diagnostics` always.
- The game compiles `Source/**/*.cs` itself with Roslyn into
  `<persistentDataPath>/Temp/<assemblyName>.dll` (recompiled when sources or
  `com.uboat.game.dll` are newer) and loads it into a `ScriptDomain`. Referenced assemblies
  include game, modapi, UniTask, ZString, Unity.Mathematics, PlayWay water.
- Order: `ModManager.AwakeSingleton` -> per mod: load assembly, asset bundles, raw assets,
  **spreadsheets** -> (later) `Locale.AwakeSingleton`. Then at the end of the intro scene,
  `IntroScene` calls `ModManager.InitializeAssemblies()` -> per mod
  `InjectionFramework.Instance.InjectIntoAssembly(asm)` then instantiates the **first** type
  implementing `UBOAT.Game.IUserMod` and calls **`void OnLoaded()`** once. This is before
  `"Main Scene"` loads: no `PlayerShip`, crew or save exists yet; ScriptableObject
  singletons (`GameData`, `Locale`, `UserSettings`, `ExecutionQueue`, `ResourceManager`,
  `ModManager`) do. Background tasks such as `CharacterActionsDatabase.Start` run later, so
  Harmony patches applied in `OnLoaded` are in place before actions load.
- `[Inject]` (`DWS.Common.InjectionFramework.InjectAttribute`, in `com.uboat.modapi`) on
  **static fields** of mod types is filled by `InjectIntoAssembly` by exact field type
  (class or interface). Every injected field is remembered, and when a singleton instance
  is registered later (e.g. scene objects implementing `ISingleton` when `Main Scene`
  awakes, via `AddInjectableInstancesFromScene`) all matching fields are re-assigned. So
  `[Inject] static PlayerShip playerShip;` is null in `OnLoaded` and populated once the game
  scene loads (the game's own actions use exactly this). `[TaggedInject("tag")]` resolves by
  GameObject tag. Fields on generic types are rejected. Manual access:
  `InjectionFramework.Instance.GetInstance<T>()`, `GetSingletons<T>(List<T>)`.
- Per-frame/timed callbacks without a MonoBehaviour: `ExecutionQueue` -
  `AddUpdateListener(Action)`, `AddFixedUpdateListener(Action)`,
  `AddTimedUpdateListener(Func<float> returnsNextDelay, float delay, TimedUpdatesQueue)`,
  `RemoveUpdateListener(Action)`.
- Game-session services and per-campaign saved state: subclass `BackgroundTaskBase` and
  declare it in `General/Background Tasks`; see section 6.
- **User settings: no mod-settings API exists.** `UserSettings` (`GameplaySettings`,
  `AudioSettings`, ...) is fixed, and the options panels are hard-coded
  (`Scene.UI.MainMenu/*SettingsPanel.cs`). The game's own tunables are data rows under
  `General/Settings/...` read via `GameDataReference`; a mod should expose its tunables the
  same way (its own sheet rows), or build its own UI. Relevant existing settings:
  `GameplaySettings.damageDifficulty`, `sinkingPhysicsMode` (`SinkingPhysicsMode.Realistic`).

## 6. Per-campaign persistent mod state (background tasks)

There is no `UBOAT.ModAPI` namespace in 2026.1. The type is
**`UBOAT.Game.Core.BackgroundTaskBase`** (assembly `com.uboat.game`,
`UBOAT.Game.Core/BackgroundTaskBase.cs`), an abstract class implementing
`IBackgroundTask` and `IManagedSerializableObject`. The manager is
`UBOAT.Game.Scene.Tasks.BackgroundTasksManager` (`ScriptableObjectSingleton`,
`ISerializableObjectsManager`, `Scene.Tasks/BackgroundTasksManager.cs`).

### 6.1 Members

| Member | Notes |
|---|---|
| `public virtual void Load(GameDataRow data)` | called once on a **fresh** start with the task's own declaring row. **Not** called on restore. |
| `public virtual void Start()` | called right after `Load` on a fresh start (exceptions are logged and swallowed). **Not** called on restore. |
| `public virtual void Resume()` | called on **restored** tasks after a save finishes loading (`SavesManager.Loaded`). Re-acquire everything non-serialized here (event subscriptions, `ExecutionQueue` listeners, data rows). |
| `public void Finish()` (non-virtual) | calls `OnFinished()` and raises `Stopped`; the manager then drops the task. |
| `protected virtual void OnFinished()` | cleanup: unsubscribe and remove listeners. Called on the stop event, when a save starts loading, or when the mod assembly unloads. |
| `ISerializableObjectsManager Manager {get;set;}` | set to the manager before `Load`. |
| `event Action<IBackgroundTask> Stopped`, `virtual event Action<TemporaryHostilityEvent> Changed` | |

Find a running instance with `BackgroundTasksManager.GetRunningTask<T>()` or
`GetRunningTasks(List<IBackgroundTask>)`. Freshly run tasks are also registered with
`InjectionFramework.AddInjectableInstance`, so `[Inject] static MyTask` works in mod code.
Whether restored instances are registered too is unverified (inferred: they are not;
re-register in `Resume` if that matters). `TaskCreated` fires for fresh instances.

### 6.2 Declaration: `General.xlsx` -> sheet `Background Tasks`

- The data path is `General/Background Tasks`, so a mod file named `General.<anything>.xlsx`
  with a worksheet `Background Tasks` works. Columns: **0** the row key (unique; becomes the
  task's `DataPath` `General/Background Tasks/<key>`, the row handed to `Load`), **1** the type
  name, **2** the event that starts it. Extra columns are free for the task's own `Load`.
- Column 1 is resolved with **`ModManager.GetType(name)`**. That searches `com.uboat.game`,
  `com.uboat.modapi`, then each loaded mod assembly via `Assembly.GetType(name)`, then
  `Type.GetType`. A plain **namespace-qualified name** (`DamageControl.DcCampaignState`) resolves
  mod types; no assembly qualifier is needed. Types that don't implement `IBackgroundTask`
  are rejected with an error.
- Events, from `FireEvent`: `GameStart` (manager start), `MenuStart` (game state `Running`),
  `GameplayStart` (state `Gameplay`), **`CampaignStart`** (state `Gameplay` only when *not* a
  restored game state and no save is loading, i.e. a brand-new campaign), plus `CampaignStop`
  and `GameplayStop` (back to menu or quitting). The stop event is derived by replacing
  "Start" with "Stop" in the start event; when it fires, the task's `Finish()` is called.
- **Only tasks started by `CampaignStart` are saved** (`runningSerializableTasks`); all
  others are transient. `IsSerializable(target)` enforces this for `IBackgroundTask` instances.
- Timing: `BackgroundTasksManager.AwakeSingleton` reads the sheet after
  `GameData`/`ModManager` have woken, so mod rows and mod types are already present. Rows
  are read again when entering the menu and after a save loads; already-defined paths are
  skipped. `ModManager.AssemblyLoaded`/`AssemblyUnloaded` (hot reload) add or finish a mod's tasks.

### 6.3 Lifecycle in one campaign

1. New campaign -> `CampaignStart` -> `Activator.CreateInstance(type)` (needs a public
   parameterless ctor) -> `Load(row)` -> `Start()`.
2. Save -> the manager's `runningSerializableTasks` list is written by the game-state
   serializer, so the task's fields go into the save.
3. Load save -> `SavesManager.LoadingStarted` calls `Finish()` on current campaign tasks ->
   the deserializer recreates the saved task instances and fills their fields (no ctor
   logic beyond field initialisers should be assumed; inferred) -> `CustomDeserialization`
   drops null entries -> on `SavesManager.Loaded`, `Resume()` is called on each. `CampaignStart`
   is not re-fired for a restored game, so no duplicate is created.
4. Back to menu -> `CampaignStop` -> `Finish()`.

**Saves made before a mod was installed get no `CampaignStart` task.** `CampaignStart` fires only for a
brand-new campaign. After a save loads, `SavesManagerOnLoaded` calls `LoadTasks(true)`, which starts
only rows whose event is currently active, and `CampaignStart` is not active for a restored game.
`BackgroundTasksManager.StartTask<T>()` runs a defined task through the normal path, and a task
declared with `CampaignStart` is added to the saved list. WS0 calls it from an action queued on
`SavesManager.Loaded` (the event hands out a `Queue<Action>` that runs after all handlers) when
`GetRunningTask<T>()` returns null. The game enters `GameState.State.Gameplay` when "Main Scene"
loads, which is before the save is deserialized, and fires `GameplayStart` then `CampaignStart` in the
same handler.

**ExecutionQueue listeners are saved.** `ExecutionQueue.CustomSerialization` writes the update,
fixed-update and timed queues, which hold delegates, into the save. Code in a transient object should
not leave listeners registered across a save. Prefer events, or remove the listener in `OnFinished`.
Timed listeners stop when they return `float.NaN`.

### 6.4 What gets serialized (`UBOAT.Game.Core.Serialization`)

The game uses its own reflection serializer (`GameStateSerializer` /
`GameStateDeserializer` in `UBOAT.Game.Serialization`, driven by `ClassDescriptor`,
`TypeDescriptor` and `GameStateSerializationSettings`), not Unity's or BinaryFormatter.

- **The class must be marked `[System.Serializable]`.** Non-`MonoBehaviour`/`ScriptableObject`
  types without it are treated as non-serializable. A non-serializable task is (inferred)
  written as null and silently dropped on load. The same rule applies to every custom
  class or struct stored in a field.
- Fields: **all instance fields, public and private, including `readonly`**, declared on the
  class and its base classes. Excluded: `static`, `const`, fields marked `[NonSerialized]`
  (unless also `[GameStateField]`), and fields marked `[NonSerializedInGameState]`
  (`UBOAT.Game.Core.Serialization`). Reference-type fields whose name contains "prefab" are skipped.
- Supported field types: primitives, `string`, enums/structs (value types), `Nullable<T>`,
  1-D arrays, `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Type`, `Vector2/3/4`, `Color`,
  `Color32`, `Quaternion`, references to other serializable objects (shared references are
  preserved by id), and `MonoBehaviour`/`ScriptableObject` references (resolved as scene
  or singleton objects).
- Pitfalls: multi-dimensional arrays, non-generic `System.Collections` types, other
  `System.Collections.*` generics (`Queue`, `Stack`, `LinkedList`, `SortedDictionary`...), `IntPtr`,
  and plain UnityEngine reference types that aren't MonoBehaviours (`Texture2D`, `Sprite`,
  `Material`...) are not serializable. Delegates and events are their own type code: mark
  event-handler fields and caches `[NonSerialized]`. Dictionary keys must be non-null and unique.
  `NativeArray` and Unity.Mathematics types fall under "value type" handling; avoid them in
  saved state (inferred). Prefer arrays/lists of `[Serializable]` structs keyed by stable
  string ids (e.g. compartment name or `HorizontalIndex`) over direct `Compartment` references.
- Versioning: on load, saved fields missing from the current class are logged and skipped;
  new fields keep their initialiser value. `[FormerlySerializedName("Old.Full.Name")]` on a
  class maps a renamed type. `ISerializationEventsCallbackReceiver`
  (`OnBeforeSerialize`, `OnBeforeDeserialize`, `OnAfterDeserialize`) and
  `ICustomSerializationCallbackReceiver` (`CustomSerialization`, `CustomDeserialization`
  for hand-written byte streams) give hooks. `ISerializationMissingFieldEventReceiver`
  handles removed fields.
- Type identity in saves: classes are recorded by `AssemblyQualifiedName` and resolved through
  `InjectionFramework.GetTypeByName`. That function first checks `[FormerlySerializedName]`
  aliases, both for the full string and for the part before the first comma, then calls
  `ModManager.GetType`. Mod assemblies are loaded from bytes, so resolution of a mod type
  from its assembly-qualified name is **unverified**. Cheap insurance (inferred to work):
  put `[FormerlySerializedName("Our.Namespace.TaskType")]` on each saved mod class, so the
  alias path always resolves it. Verify with a save/load round-trip in-game.
- A save made with the mod and loaded without it will log errors for the unknown type
  (inferred); the task simply disappears.

## 7. Raw assets (PNG/JPG/audio without asset bundles)

### 7.1 Loading

`Mod`'s constructor (during `ModManager.AwakeSingleton`, before any mod code) calls:

- `LoadAssetBundles()`: every file directly in `<mod>/Bundles/` (top level only) and in
  matching `Version X[-Y]/Bundles/` folders, except names ending `.meta`, `.manifest`,
  `.trees` or `Bundles` (the Unity manifest bundle), is passed to
  `ResourceManager.AddAssetBundle(mod, path, simulate:false)`.
- `LoadRawAssets()`: every file under `<mod>/Assets/` (recursive) and matching
  `Version X[-Y]/Assets/`, except `.meta`/`.manifest`, is passed to
  `ResourceManager.AddFileResource(mod, filePath)` (`DWS.Common.Resources/ResourceManager.cs`).

### 7.2 `AddFileResource`

- Supported files: **`.png`, `.jpg`** become `TextureFileResource : ResourceBase<Texture2D>`,
  loaded through `UnityWebRequestTexture`. **`.wav`, `.ogg`, `.aiff`, `.aif`** become
  `AudioFileResource : ResourceBase<AudioClip>`, loaded through `UnityWebRequestMultimedia`.
  Extension checks are case-insensitive. Anything else is ignored.
- Key: the file path made relative to `<mod>/Assets/` with `Uri.MakeRelativeUri`, using `/`
  separators, **keeping the extension and the original case**. The dictionary lookup is
  case-sensitive. Example: `Assets/Icons/DamageControl/pump.png` is keyed
  `Icons/DamageControl/pump.png`. The parent folders are added to the resource map, so
  `GetAssetsBelow("Icons/DamageControl")` lists it.
- Pitfall (inferred from `Uri` semantics): files in `Version X/Assets/` are made relative
  to the mod's root `Assets/` folder, so their keys come out as
  `../Version X/Assets/...`. Keep raw assets in the root `Assets/` folder.
- A raw file only overrides an existing key if the key matches exactly; it is stored in
  the same `resources` map that `RetrieveAsset` checks first.

### 7.3 How lookups behave

`ResourceManager.RetrieveAsset<T>(path, referent)` checks `resources[path]` first, then
asset-bundle contents (`assetToBundleMap`), then `UnityEngine.Resources.Load<T>(path)`. The
returned `Resource<T>` exposes `Asset`/`Valid`. `ResourceBase.GetAsset<T>` only returns the
stored object if it **is** a `T`, or a component of it. **There is no Texture2D-to-Sprite
conversion** (`Sprite.Create` is never called by the game).

Consequences:

- Game UI asks for **extension-less `Sprite`** paths, for example the interaction wheel in
  `InteractionWheelButtonUI.Start()`: `Icons/InteractionWheelUI/<IconName>`, plus
  ` (Disabled)` / ` (Highlighted)` variants, falling back to `Generic Interaction`. The
  default-action cursor (`InteractionWheelUI`) asks for `Texture2D` at
  `Icons/Actions - Cursor/<IconName>`. Raw PNG keys always end in `.png` and hold a
  `Texture2D`, so **raw PNGs can neither supply nor override base-game icons or sprites**.
  Overriding happens through bundles: `AddAssetBundle` records assets whose GUID already
  exists in `assetReplacementMap`, and bundle paths are found by extension-less lookups (inferred).
- Raw **audio** can be data-driven: `SoundEffectsDatabase` builds each clip path as the row
  path minus `General/` (from sheet `General/Sound Effects`, with any `#...` suffix
  stripped). It then calls `RetrieveAsset<AudioClip>` on that path. A row keyed
  `pump_start.ogg` in our `General.*.xlsx` worksheet `Sound Effects` therefore resolves to
  `Assets/Sound Effects/pump_start.ogg` (inferred; column 1 holds the tags used by
  `GetClip(tag, ...)`, other columns follow the base sheet).

### 7.4 Conclusion: PNG icons without a bundle in 2026.1

**Yes, but only with code; data rows alone can't do it.** Ship
`Assets/Icons/DamageControl/<name>.png`, then:

1. Load it: `resourceManager.RetrieveAsset<Texture2D>("Icons/DamageControl/<name>.png", owner)`
   (`[Inject] static ResourceManager`). Wrap it once in a cached `Sprite.Create(...)`.
2. Feed it to the UI. The least invasive route (inferred; requires the `Reflection`
   permission) is to insert a `DWS.Common.Resources.DirectResource` wrapping the created
   `Sprite`, which has a public constructor taking a `UnityEngine.Object`. Insert it into
   `ResourceManager`'s private `resources` dictionary (`Dictionary<string, IResource>`) under
   the exact key the game requests: `Icons/InteractionWheelUI/<GetIconName()>` and the
   ` (Highlighted)` / ` (Disabled)` variants. A `Texture2D` entry also goes under
   `Icons/Actions - Cursor/<GetIconName()>`. Then the stock `RetrieveAsset<Sprite>` path
   finds our icon with no patch. Do it before the first wheel opens, e.g. in a task's
   `Start`/`Resume` or in `OnLoaded`, after waiting for the texture to load.
3. Alternative: a Harmony Postfix on `InteractionWheelButtonUI.Start` that swaps the button
   sprite when the template is ours. This is more fragile, because the fields are private.

Override `CharacterAction.GetIconName(...)` to return our unique icon name, so we never
collide with base keys. If no icon is found, the wheel silently uses
`Generic Interaction`, so missing art is not fatal.

Implemented (WS0, `Game/Infrastructure/IconRegistry`, route 2). In `OnLoaded`,
`resourceManager.GetAssetsBelow("Icons/DamageControl")` lists our raw keys, for which `resourceMap`
holds every ancestor folder. Each texture is loaded with `RetrieveAssetAsync<Texture2D>`. We wrap it
with `Sprite.Create(tex, full rect, pivot (0.5, 0.5), 100 ppu)`, or keep the Texture2D for
`Actions - Cursor`, and store a `DirectResource` under `Icons/<Category>/<IconName>` in the private
`resources` dictionary (`AccessTools.FieldRefAccess`). `DirectResource.AddUsage`/`RemoveUsage`/`Unload`
are no-ops, and `ClearAllOtherSceneResources` removes only `SceneResource`s, so the entries live for the
whole session. Lookup types in the 2026.1 UI: wheel, `Actions`, `Actions (Banners)` and
`Statuses (Portraits)` request `Sprite`; `Actions - Cursor` requests `Texture2D`.

## 8. Hook points

### Good, stable hooks

| Hook | Why |
|---|---|
| `Tank.FixedUpdate` / `Tank.SimulateBreach` (Prefix/Postfix) | the single place sea water enters any player tank; large method, called per hole per physics step. |
| `Hatch.DoUpdate` (Prefix to replace/augment flow, e.g. leaky closed hatches) | one per hatch, large, private but stable name. |
| `Compartment.LowFrequencyUpdate` (Postfix) | 4-second cadence, ideal for pressure/air/structure checks per compartment. |
| `PlayableSubmarine.FixedUpdate` (Postfix) | after mass/CoM are applied; add trim/buoyancy effects. |
| `LeakableEquipment` events via `Equipment.HealthChanged`, `PlayerShip.LeakStarted/LeakStopped` | subscribe instead of patching. |
| `Tank.WaterAmountChanged`, `Compartment.EvacuationChanged`, `Hatch.LockStateChange`, `ExternalPressure.Added/Removed`, `Equipment.EquipmentAdded` | events; no patching needed. |
| `CharacterActionsDatabase.LoadActions` (Postfix) | to register mod action types. |
| `GenericActivator.AddActionInfos` (Postfix) | inject wheel entries without tagging. |
| `HullCrushController.DoUpdate`, `Hull.Update` (AI ships only) | managed wrappers around crush / Burst flooding. |
| Our own subclasses of `SingleCharacterAction`/`DeviceUseAction` | override virtuals; no patching. |

### Avoid

| Avoid | Reason |
|---|---|
| Anything `[BurstCompile]`: `Hull.SimulateBallast*`, `ComputeBallastCenterOfMass`, `FindRepairableBreach`, `PropagateBallastJob`, `SimulateBreachesJob`, `ComputeNeuralNetworkJob`, `SphereBoxIntersectionCompute` | native code; patches silently ignored. |
| Expression-bodied getters: `Tank.AirPressure`, `FluidsPressure`, `Mass`, `Compartment.WaterElevationAboveFloor`, `ExternalPressure.Pressure`, `IsUnderWater`, `Hatch.Locked` getter | tiny, JIT may inline into callers so the patch misses some call sites. Patch the larger caller instead. |
| `Tank.Water` / `Compartment.Water` setter | hot (called many times per frame by hatches, pumps, breaches) and small; observe via `WaterAmountChanged` instead. |
| Generic methods: `Equipment.GetStatus<T>/HasStatus<T>/AddStatus<T>`, `ActionQueue.GetAction<T>/HasAction<T>`, `CharacterAction.Duplicate<T>`, `InjectionFramework.GetInstance<T>` | Harmony on shared generic instantiations is unreliable. |
| `Tank.DoUpdate`, `LeakableEquipment.DoUpdate`, `InteractionWheelUI.LateUpdate` (`async UniTaskVoid`) | per-frame visual paths / async state machines (patching an async method patches only the stub). |
| `Hull.TotalBallast` private setter | side effects (mass, health) and inlined into `Hull.Update`. |
| `CharacterAction.OnPreStart` in general | `async` -> patch target is the state-machine stub; override in a subclass instead. |

## 9. Ballast valve, diesel and crew navigation (WS4)

- **Valve on/off.** `Device.SetDisableFlag(DeviceDisableFlag.TurnedOff, true)` sets the component's
  `enabled` to false (enabled only while no disable flag is set), so `FloodingAndBlowingValve.FixedUpdate`
  stops running: a turned-off valve does not touch the tanks, and an action can then drive `Tank.Air` itself.
  The `Mode` setter is a no-op when the mode is unchanged; on a change it resets the flooding timer and closes
  every ballast vent (`Openess` 0, `ExtraPressure` 0). Vanilla blow orders set `OptimalBlowing` and clear
  `TurnedOff`; the valve sets `TurnedOff` itself when its blow target is reached.
- **Valve order gating (2026.1).** The vanilla valve orders check `CheckActivatorUseConditions(RequireWorkingDevice
  | DisallowDuplicateActions | CheckForBlockingItems)` and then the static `CheckInteractableUseConditions(interactable,
  "Use", "Default", character, SlotUsageFlags.AllowUnderWaterAlways, fillDetails, isPlayerOrder)`; their rows are
  on activator tag `Valves`, contexts `Normal, Queue, NoCharacter`, no group. Compressed-air "blow" is hidden while
  `Mode < AdjustmentToNeutral`. `AutomaticActionsController` queues "blow" only when `TargetDepth == 0` and
  `Mode >= AdjustmentToNeutral`, and "flood" only when `TargetDepth > 0.05`, the mode is a blowing mode and nobody
  uses the valve station.
- **Ballast tank order.** `PlayableSubmarine.BallastTanks` is a serialized array. The valve treats index 0 and
  the last index as the end tanks (deck-awash venting) and handles 3, 5 and 8 tank layouts.
- **Diesel.** `PlayerShip.DieselEngine` is a `PlayerShipEngine` (a `Device`). It is unmounted (disabled flag) while
  the electric motors drive the boat. `CurrentSignedRPM` eases toward the current gear preset's RPM (negative
  astern) and is zeroed when the component is disabled.
- **Deck depth.** `Ship.DeckDepth` is clamped at 0; `Ship.DeckDepthUnclamped` is negative while the deck is
  above the water.
- **Held device orders.** `DeviceUseAction.UpdateAction` completes the action once the character's
  `SkillProgress` reaches 1 (or after `AnimationTimeout`, NaN = never). The animator's `UpdateSkillProgress`
  state behaviour writes `SkillProgress` too, so a held order rewrites it below 1 every frame before calling the
  base. `StopMainCharacter` calls `OnDeviceUsed` only when `SkillProgress >= 1`.
- **Reachability.** `NavMeshNavigator.IsDestinationReachable(Vector3 pos, bool playerOrder = false)` still uses
  the 2022.1 constants 101.55 / 102.8 / 106 and returns true at once for player orders; for other orders it walks
  `HatchesLeftToRight` by `ObstaclePositionZ` (starting at the hatch beside `PlayableCharacter.CurrentCompartment`)
  and refuses compartments with `Evacuation`, plus `Evacuated` checks for the tower and exterior levels. It does
  not look at `Hatch.CanBeOpened`.
- **Hatch links.** `NavMeshDoors` (private `hatch`) is active only while its hatch is `Locked` (closed);
  `TraverseStart(NavMeshNavigator navigator)` unlocks the hatch at once. `NavMeshLadder` (public `Hatch`)
  unlocks its hatch part-way through the climb. Neither checks `CanBeOpened`.
- **Cancelling orders.** `InteractiveCharacter.CancelActiveOrders(profitMin, profitMax)` removes the first
  active AI behaviour whose profit is in the band and re-runs the AI; `CancelOrders(profit = 2.3)` removes every
  behaviour with exactly that profit (device-use orders are added at 2.3). The 2022.1 call
  `CancelActiveOrders(0, 5)` therefore means "drop the current order-level behaviour".
- **Evacuation.** `EvacuateToSafetyAction.StartCharacter` sets `CanBeStopped = false` and then calls the private
  `Escape(CharacterData)`; a timed update calls `Escape` again for each new leg.

## 10. Leaks and crew automation (WS3)

- **`BlockLeakAction` (2026.1).** Private fields `leak` (`Leaking`), `leakIntensityModifier` (`Modifier`),
  `replacesRepairOrder`. `UpdateAction` adds the `"BlockLeak"` 0.2 scale modifier only while
  `Character.IsInteracting` and the field is null, through a hard cast of `leak.Equipment` to
  `LeakableEquipment`; then it cancels on crowding (`InteractingCharacters.Count > MaxCharacters`) or when
  `leak.Equipment` is null, and calls the base device-use update. `StopMainCharacter` removes
  `IsBeingRepaired` and calls `Remove()` on the modifier field. There is no open-hatch cancel. A `Modifier`
  built with a null target is safe to `Remove()` and serializes as name id, value and flags only.
  `StartMainCharacter` reads `GetComponent<Interactable>()` on the equipment without a null check and uses
  the `"Block Leak"` slot, falling back to `"Default"`; it stops `RepairAction` users when the
  interactable's `MaxCharacters` is 1.
- **`LeakBlockingBySailors.DoUpdate`** (private, per-frame update listener) dispatches `new BlockLeakAction`
  directly, only for `LeakableEquipment` (its list comes from `PlayerShip.LeakStarted` and `HasStatus<Leaking>`
  over `LeakableEquipment`), so it never targets hatches.
- **`AutoCarryWoundedToSafety.DoUpdate`** (private, timed, returns the next interval: 2.5 s idle) first
  pulls divers aboard when `diverSpot.Divers.Count != 0`, then round-robins `PlayerShip.RagdollCrew`. Its
  listener is registered only when the boat has a `DiverSpot`.
- **`AutomaticActionsController.DoUpdate`** (private, timed, 1.5 s) keeps sustained-condition deadlines as
  private `long` game-tick fields (0 = not running); the pump uses `pumpDelay` with `PumpTurnOffDelay`
  (55 s on the vanilla `General/Background Tasks` row).
- **`Interactable`.** Private `maxCharacters` (initial, also the clamp of the `MaxCharacters` setter) and
  `maxCharactersCurrent`; nothing in the game sets `MaxCharacters`. Sibling-slot exclusion is
  `InteractableSlot.DisabledSlots` plus `AddDisableReason/RemoveDisableReason(slot)`, applied on `Reserve`.
- **Light switch.** `LightSwitch` equipment; vanilla `Crew/Actions` rows use activator tag `Light Switch`.
- **Orders list.** The 2026.1 hook for "not shown in the orders list" is `CharacterAction.ShowInTabMenuForActivator`.
