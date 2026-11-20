# WS2 hatches: API for WS1, WS3, WS4 and WS5

Status: implemented (WS2). This is what other workstreams may call. Names are ours, except game types.
Code: `mod/Source/Core/Hatches/` (pure, unit-tested) and `mod/Source/Game/Hatches/` (patches, the
equalize action, game-side helpers). Rows: `data/hatches/hatches.json`.

## 1. `HatchSeals`: the hatch half of the leak-blocking mechanism (00 §2.5, C9, C28)

`DamageControl.Core.Hatches.HatchSeals`, pure C#. Use the shared instance from Game code:

```csharp
using DamageControl.Core.Hatches;

HatchSeals.Shared.Add(hatch, SealSource.Blocker, this);   // WS3: BlockLeakAction hatch case, while interacting
HatchSeals.Shared.Add(hatch, SealSource.Repair, this);    // WS5: RepairAction on a hatch, while interacting
HatchSeals.Shared.Remove(hatch, this);                    // when the crewman stops interacting / the action stops
HatchSeals.Shared.RemoveOwner(this);                      // cleanup when the hatch reference is gone
```

| Member | Meaning |
|---|---|
| `bool Add(object hatch, SealSource source, object owner)` | Registers one seal source. **Idempotent per (hatch, owner)**: calling it every frame is fine and cheap. True when new. Nulls are ignored. |
| `bool Remove(object hatch, object owner)` | Removes that owner's seal. True if one was removed. |
| `int RemoveOwner(object owner)` | Removes the owner's seals on every hatch. |
| `void ClearHatch(object hatch)`, `void Clear()` | Forget one hatch / everything. WS2 calls `Clear()` on `DcSettings.SessionStarted`. |
| `int Count(object hatch)`, `int Count(object hatch, SealSource)`, `bool IsSealed(object hatch)`, `bool Has(object hatch, object owner)` | Reads. |
| `float SealFactor(object hatch)` | `0.1 ^ sources` (1 when unsealed). |
| `float ClosedLeakMultiplier(object hatch)` | `0.02 × SealFactor`: the fraction of the open-hatch exchange rate a closed, leaking hatch passes. |
| `const float FactorPerSource = 0.1f`, `static float FactorFor(int sources)` | |

Rules for owners:

- Pass the `Hatch` component itself as `hatch` and your action instance as `owner`. Keys compare by
  reference.
- Presence, not value, matters (C9): a seal source cuts the leak to a tenth whatever the repair progress.
  Do **not** add modifiers to the hatch's `Performance` stat.
- State is session-only, not saved. After a save loads, `Shared` is empty; an action restored from the
  save re-registers on its next `UpdateAction` if it calls `Add` there (recommended: `Add` whenever
  `Character.IsInteracting`, `Remove` otherwise and in `StopMainCharacter`).
- Who reads it: the hatch exchange (`Hatch.DoUpdate` replacement, 02 §2.3e) and the hatch panel's single
  "partially sealed" line (02 §2.4 item 5). Leakable equipment (pipes, holes) does **not** use this
  registry: there the blocker relies on vanilla's own 0.2 `"BlockLeak"` modifier and repair adds one
  `LeakIntensity` scale modifier of `1 - progress` (C9, owned by WS3/WS5).
- The `LeakThrottle` helper named in 00 §2.5 (the `1 - progress` value for leakable equipment) is not
  part of WS2; it belongs with H8/R2.

## 2. Water-head unit (D1)

`WaterHead.AtmPerMetreOfWater = 0.1f`, `WaterHead.Atm(metres)`, and
`WaterHead.SidePressure(airPressure, waterElevation, referenceY) = p + 0.1 × max(0, WE - referenceY)`.
Every hatch rule, the panel and equalize use it. Anything else that mixes water head into pressure should too.

Open-hatch water rate (vanilla parity): the passage profile multiplies the 02 §2.3g driving term by
`HatchExchange.PassageWaterGain = 1 / AtmPerMetreOfWater = 10`, i.e. it moves the driving term in metres
of head × step. At equal air pressure a level difference of h m above the sill then moves `h × s`,
which is vanilla 2026.1's own open-hatch rate (same k = 2 and 2/sqrt(S) damping). An air-pressure
difference drives like its equivalent head (1 atm = 10 m); vanilla has none. Remaining differences from
vanilla, all spec-defined: the `(1.01 - fill)²` inflow choke near a full receiver. Closed, leaking
hatches pass 2 % × seal factor of the converted open rate.

Substeps (C2 note): the hatch patch does not drop game time above the 0.5 s cap.
`HatchExchange.Substeps(clock, rate)` takes the frame's whole `game dt × damped rate`, clamps it at
`MaxFrameTotalStep` = 8 (vanilla's own per-frame clamp), and splits it into `ceil(dt / 0.5 s)` equal
substeps, at most `MaxSubsteps` = 16 (only reached past 8 game seconds per frame, where a substep is still
≤ 0.5, below the 1x half-second step of 1.0). The patch runs the whole exchange (air, water, leaks, both
passages) once per substep, re-reading both compartments each time, so the air floor, the
no-equalization-overshoot guard and the no-overfill guard hold per substep. Frame totals match vanilla at
any compression. `FrameStep` (one capped step) remains for the equalize action. Equalize keeps gain 1 (01 §2.11). The vertical passage is unchanged.

## 3. Hatch rules and kernel (Core)

| Type | Use |
|---|---|
| `HatchLockRule.CanBeOpened(HatchLockInput)`, `DefaultThreshold` 0.9, `DeckDepthLimit` 2.5 | 02 §2.1 steps 1 to 6 |
| `HatchExchange.Substeps(SimClock, rate)` → `FrameSubsteps`, `FrameStep(SimClock, rate)`, `EqualizeStep`, `GatedStep`, `Horizontal`, `Vertical`, `Equalize`, `Exchange(..., ExchangeProfile, step)` | the exchange kernel (02 §2.3, 01 §2.11). `HatchFlow` amounts are positive left to right. |
| `HatchSide`, `HatchSide.Create(p, air, water, capacity, we, floor)` | one compartment as plain values |
| `HatchDamageRules.CanBlowOpen`, `RollBlowOpen`, `UpkeepLeak`, `StaysShutWhenDisabled` | H5, H3 |
| `HatchPanel.Build(HatchPanelInput, HatchPanelText)`, `Classify`, `ViewerFarSideIsRight` | H6 |
| `EqualizeRules.IsOffered(...)`, `SortingPriority` 11 | P11 |
| `HatchPressurePoint.Reading(isInteriorHatch, owning, pL, pR)` | **for WS1's P4 postfix** on `CompartmentPressure.Pressure`: `|pL - pR|` for an interior hatch, else the owning compartment's pressure (01 §6 #5) |
| `HatchText` | locale keys, the equalize keyword, tunable row names |

## 4. Game side (`DamageControl.Game.Hatches`)

- **`Hatch.CanBeOpened` is patched (H1).** WS4 (B9 reachability, B10 traverse cancel) and WS5 (R3) read
  the vanilla property and get the mod's answer: a visible `IsBeingRepaired` status means "cannot be
  opened" always (C10); with P14 on, the pressure rule decides for hatches that have compartments; with P14
  off (or for compartment-less hatches such as the exterior hatch and doors), vanilla decides.
- **`Hatch.Locked` setter is guarded (H2).** Opening a closed interior hatch that cannot be opened is
  silently ignored while P14 is on or a repair hold is active. The mod's own forced openings use
  `HatchRuntime.SetLockedUnguarded(hatch, locked)`. WS5's R3 "close once on repair start" is a close,
  which the guard never blocks.
- `HatchRuntime`: `ReadSide(Compartment)`, `FarSide(hatch, seat)`, `IsRepairHeld(hatch)`,
  `HasNoCompartments(hatch)`, `Apply(left, right, HatchFlow)`, tunables `LockThreshold`, `FlowRate`,
  `BlowOpenChance`.
- Hatch geometry comes from `BoatLayout.TryGetGeometry` (WS0), which WS2 changed to use the hatch's
  private `lowestPoint` marker as lowY (the 02 convention) and to measure hatches outside the ship's lists
  on demand. See WS0-API §4.

## 5. Tunables (`General/Settings/Damage Control/`)

| Row | Default | Meaning |
|---|---|---|
| `Hatch Lock Threshold` | 0.9 | atm by which the seat side (air + 0.1 atm per m of water above lowY) may exceed the far side before the hatch locks (D1) |
| `Hatch Flow Rate` | 2 | base exchange rate k through hatches at 1x (02 §2.3b), a multiplier on air and water. 2 gives vanilla 2026.1 parity for open-hatch water flow (see §2); closed leaks stay 2 % of it |
| `Hatch Blow Open Chance` | 0.75 | per-second chance a destroyed hatch blows open under over-pressure (D7) |

Read once per campaign session (and on first use).
