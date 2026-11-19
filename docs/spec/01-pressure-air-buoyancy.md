# Spec 01: Compartment pressure, air and buoyancy

Status: behavioral spec (dirty room). Source analyzed: Iron Coffins 0.61, UBOAT 2022.1.
Implementers: build from this document and the 2026.1 game assemblies only.

All game identifiers below are **2022.1 names** and must be re-mapped against the
2026.1 assemblies before use. Where this spec says "air pressure" it means the
game's per-compartment air pressure value, which the game exposes in
atmospheres (atm). "Air amount" means the game's per-tank air quantity (the
quantity whose ratio to free volume gives pressure). "Water elevation" is a
world-space Y coordinate of the water surface inside a compartment; "water above
floor" is that surface's height above the compartment floor in metres.

---

## 1. Overview

Vanilla UBOAT treats compartment air pressure mostly as a cosmetic/secondary
value. This area turns each compartment into a pressurized air volume that
matters:

| Problem the player faces | What the mod changes |
|---|---|
| Flooded or crushed compartments have no air-related consequence beyond water level | Compartments can become unbreathable from low/high pressure or deep water; equipment and lights get damaged by over-pressure |
| Air in a compartment can reach zero when a breach is open, giving degenerate flooding | A breached compartment always keeps a small air floor |
| Diesels and the compressor create air from nowhere | The running diesel and the air compressor draw air from the compartment they sit in; the diesel compartment and conning tower re-ventilate only when connected to the atmosphere |
| A closed hatch between compartments at different pressure has no meaning | Hatch pressure points measure the *difference* across the hatch; a crew action ("equalize pressure") bleeds air/water through the hatch until it can be opened |
| Rebreathers run out too fast for long damage-control work | Rebreather consumption is slowed |
| Buoyancy indicator is opaque | Buoyancy readout becomes a clamped percentage with a trend arrow, depth-aware severity icon, and a tooltip breaking down ballast-tank and compartment flooding |
| Crew interaction slots get "underwater" at unrealistic water heights | Water tolerance of specific slots is retuned; slots near a bulkhead also consider the neighbouring compartment's water |

Pressure-based hatch *blocking* (hatch cannot be opened while pressure
differs) lives in the hatches/leaks area; this area owns the **toggle** that
turns that blocking on/off and the **equalize** action that resolves it.

Ownership (see `00-overview.md` §2.5): toggle *storage* is owned by 04 §3
(settings task); hole/pipe/hatch crowding (§2.8) is owned by 02; the
air/water exchange kernel used by equalize (§2.11) is owned by 02 §2.3; the
metres-of-water-to-atm unit is decision 00 D1.

---

## 2. Mechanics

### 2.1 Breached-compartment air floor

- **When:** each time the game simulates a breach into a tank, if the tank is a
  *compartment* (not a ballast/fuel/trim tank), the breach openness is > 0, and
  the tank's air amount is < 0.1.
- **Effect:** set air amount to 0.1 before the breach simulation runs.
- **Intent:** prevent air reaching zero (which in vanilla lets water fill the
  last of the volume / yields division-by-near-zero pressure spikes).
- **Note:** the original computed an expression from free volume × fluid
  pressure but clamped it so the result is always exactly 0.1 (see §7). Spec:
  the floor is a constant 0.1 air units.
- Interacts with: hatches/leaks (breach openness).

### 2.2 Unbreathable air ("toxic air") rule

Replaces vanilla's toxic-air test for every compartment.

A compartment's air is unbreathable when **any** of:

1. ship-wide oxygen amount < 0.05 (the game's normalized oxygen reserve, 0..1),
2. compartment air pressure < 0.15 atm,
3. compartment air pressure > 8.5 atm,
4. water above floor > 1.5 m.

Otherwise breathable. Vanilla's original criteria are fully discarded.

A paired "toxicity" value is also replaced: −0.1 when unbreathable, −1.0
when breathable. (The game consumes this as a signed rate/severity; the
semantics of the sign must be confirmed in 2026.1 — see §7.)

Interacts with: rebreathers (crew in unbreathable compartments rely on them),
ballast/depth (pressure rises as compartments flood at depth).

### 2.3 Rebreather endurance

On each rebreather update, with probability 0.6 the update is skipped
entirely. Expected consumption rate is therefore 0.4× vanilla, i.e. endurance
≈ 2.5× vanilla (the original author's intent was "about 3×").

Implementation guidance: a deterministic equivalent (scale consumption by 0.4)
is preferred over per-frame randomness; see §7.

### 2.4 Pressure-point measurement (equipment pressure sensing)

The game attaches "compartment pressure" sensing points to pressure-sensitive
equipment (each with a target equipment and an elevation). The mod replaces
the value a point reports:

- If the point's target is a **hatch** that is **not** the exterior (deck)
  hatch: reported pressure = |p_left − p_right| where p are the air pressures
  of the two compartments the hatch connects (atm).
- Otherwise: reported pressure = the owning compartment's air pressure (atm).

### 2.5 Pressure-damage status bookkeeping

Replaces the setter that flags a pressure point as "being damaged":

- The target is re-resolved as the nearest parent equipment of the point.
- Setting true: if no pressure-damage status is attached from this point,
  create one and attach it to the target.
- Setting false: detach and forget that status.

(Effect: a visible "pressure damage" status on the equipment while it is
overstressed; idempotent in both directions.)

### 2.6 Compartment periodic update (replaces vanilla entirely)

Runs at the compartment's low-frequency tick; the mod requests a tick interval
of **1 s**. Vanilla's own low-frequency work is **not** executed (see §7 for
the risk). Per tick:

**(a) Light bulbs.** For each light bulb in the compartment, it is *at risk*
when (water elevation > bulb's world Y **and** p > 4 atm) **or** p > 16 atm.
An at-risk bulb with health > 0 takes 1 point of water damage with per-tick
probability

  P_bulb = 0.1 × clamp(p/40 − 0.1, 0, 1)

(e.g. p = 8 → 0.01; p = 16 → 0.03; p = 40 → 0.09; at most 0.1).

**(b) Pressurized equipment.** For each pressure point in the compartment, with
target equipment T, reported pressure P (§2.4) and point elevation e:

- Overstressed when (T is a hatch **or** water above floor > e) **and**
  P > T's interior design pressure.
- Overstressed → mark being damaged (§2.5); additionally if T.health > 0,
  apply 1 point of water damage with per-tick probability
  0.05 × clamp(1 − D, 0, 1), where D is T's interior pressure durability
  (0..1).
- Not overstressed → clear the being-damaged flag.

Hatches are therefore stressed by pressure difference across them regardless
of water level; other equipment only once water has risen past its pressure
point.

**(c) Interaction slots underwater.** For each interaction slot registered to
the compartment:

- underwater := water above floor > slot's water tolerance.
- Then, for each neighbouring compartment whose volume contains the slot's
  position, underwater := (that neighbour's water above floor > tolerance).
  If several neighbours contain it, the last one checked wins.

Interacts with: hatches/leaks (crew at bulkhead hatches can be blocked by
water on either side), repair/parts (water damage feeds the repair loop).

### 2.7 Interaction-slot tuning (at slot registration)

When a slot is registered with a compartment, its water tolerance (metres of
water above floor at which the slot counts as underwater) and in some cases
its interaction type are overridden:

| Slot belongs to | Water tolerance | Interaction type |
|---|---|---|
| Depth-rudder (dive plane) station | 0.7 m | unchanged |
| Electric engines | 0.6 m | unchanged |
| Bulkhead or hatch, general | 1.4 m | game enum value 5 |
| The specific hatch the original identified as "Hatch 06" (believed to be the control-room ↔ conning-tower hatch), its second default slot | 1.4 m | game enum value 6 |
| Same hatch, other slots | 0.7 m | game enum value 4 |

Each overridden slot is reset to "not underwater" at registration.
Matching was by object name; implementers must identify these objects in
2026.1 and map the integer interaction types to the 2026.1 enum (§7).

### 2.8 Hole/pipe/hatch crowding (cross-area: hatches/leaks)

When an interactable's slot usage changes: if it is a hole, pipe, or hatch
interactable that allows exactly 1 character, raise its maximum (and current
maximum) to 2. For holes additionally, clear any "disabled by sibling slot"
reasons so both slots can be used simultaneously. Owned functionally by the
leaks area; listed here because the original placed it here.

### 2.9 Atmospheric venting (per physics step)

After each ship physics step (dt = fixed timestep, s). Define the
equalization step for a compartment with pressure p and rate constant k:

  Δ = 1 − p (atm; target is 1 atm)
  ΔAir = sign(Δ) × min(|Δ| × dt × k, 1)

applied to that compartment's air amount. Each rule below is evaluated
independently and their effects stack within a step.

| Compartment | Condition | k (1/s) |
|---|---|---|
| Conning tower (compartment containing the attack periscope) | Exterior (deck) hatch is open | 10 |
| Conning tower | Deck depth < 2 m | 1 |
| Diesel engine compartment | Deck depth < 3.5 m (induction above water) | 10 |
| Diesel engine compartment | Deck depth < 7 m **and** a snorkel is fitted with performance ≥ 0.5 | 10 |

**Diesel air consumption:** if the active propulsion engine is the diesel
and the diesel compartment pressure > 0.35 atm:

  Air_diesel −= min(|fuel usage rate × dt × 10000|, 0.1)

Consequence: running diesels while submerged without a working snorkel pulls
the engine room down toward ~0.35 atm (not below, so this alone never
reaches the 0.15 atm unbreathable threshold).

Interacts with: ballast/depth (deck depth), hatches/leaks (exterior hatch
state; air then spreads to other compartments only via hatches/equalize).

### 2.10 Air compressor draws from its compartment (replaces vanilla update)

Per frame, with frame time t (s) split into chunks of at most 4 s (full 4 s
chunks first, then the remainder), and N = number of compressed-air tanks the
compressor feeds, C = the compressor's current compression rate parameter,
p_max = the compressor's maximum tank pressure:

For each chunk of length τ and each tank i:

  transfer_i = (τ × C / N) × (1 − p_i / p_max)

Performed only if the compressor's compartment pressure > 0.35 atm **and**
the compartment's air amount > transfer_i. Then:

  tank_i.Air += transfer_i
  compartment.Air −= min(transfer_i / 20, 0.1)

(The 1/20 ratio reflects tank air being stored compressed; ~20 units of tank
air per unit of compartment air.) Chunking keeps behavior stable under time
compression.

Interacts with: ballast/depth (compressed air used to blow ballast),
equalize/hatches (compartment depressurization).

### 2.11 Equalize-pressure crew action

A new crew order on hatches.

> Architect note (00 C2, C3): the transfer below is computed by the shared
> hatch exchange kernel owned by 02 §2.3, parameterized with this action's
> rate constants. "Frame delta" and "time scale" mean the 00 §2.1 `SimClock`
> values. H_air and H_w come from the hatch's `highY` and `lowY` (02
> conventions), not the world constants 102 and 100.

**Availability** (otherwise hidden):
- the activator's equipment is a hatch, not the exterior hatch;
- the hatch is closed;
- the hatch has both connected compartments and a designated
  "pressure-blockade" side compartment.

Then standard activator use checks apply: no duplicate orders, a sailor may
do it without an officer present. The hatch's default slot must be usable; a
crew member may use it even if the slot is underwater provided they have a
rebreather. Contexts: normal order, queued, and no-character (auto-assign).
Sort priority among the hatch's actions: 11 (game's ordering scale). It plays
the generic skilled-device-use animation at the hatch's default slot and does
not complete on its own.

**Per-frame effect while the crew member is actually interacting:**

Let dt = min(frame delta, 0.5 s), s = Unity time scale,
r = 2/√s if s > 1 else 2, and k = dt × r / 10.
(In game time this is a rate of 0.2/s at 1×, falling as 0.2/√s at higher time
compression, for stability.)

Constants (world-space Y coordinates of the interior model):
- air vent height H_air = 102
- water valve height H_w = 100

*Air step.* Δp = p_L − p_R (atm).
- If Δp > 0.001 and (left water elevation h_L < H_air or left water above
  floor < 0.01 m): move
  f = min(max(0, Air_L − 0.2), Δp × min(50, max(0.01, H_air − h_L) × 250) × k)
  air units from left to right.
- Else if Δp < −0.001 and the mirror condition on the right: move the mirror
  amount right → left.

(The vent conductance scales with how far the water is below the vent,
capped at 50; source keeps at least 0.2 air units.)

*Water step* (uses pressures after the air step).
Δ = (p_L + max(0, h_L − H_w)) − (p_R + max(0, h_R − H_w))
(atm plus metres of water above the valve, mixed 1:1 — see §7).
- If Δ > 0.001 and h_L > H_w: move min(Water_L, Δ × k) m³ of water left → right.
- Else if Δ < −0.001 and h_R > H_w: move the mirror amount right → left.

*Completion.* If the hatch's "can be opened" test now passes (hatches area:
pressure-blocking rule), open (unlock) it. Whenever the hatch is open, the
crew member's order is cancelled with the game's 2.3 s cancel delay.

Interacts with: hatches/leaks (pressure-blocking rule decides completion;
the same water/air transfer is also used by that area's "pump water to
control room" action).

### 2.12 Buoyancy indicator (replaces vanilla update and tooltip)

Let R = ship buoyancy ratio (game value; positive = buoyant).

- tons T = 150 × R (assumes 150 t reserve buoyancy, Type VII scale).
- percent B = round(clamp(100 × R, −90, 100)).
- Update interval: 2 s.

**Trend arrow:** compare T with T from the previous update:
up if T > T_prev + 0.2; down if T < T_prev − 0.2; neither otherwise. Then
T_prev := T. (Threshold ≈ 0.1 t/s.)

**Label:** B followed by a percent sign; only rewritten when B changes.

**Severity icon:**
- Surfaced (deck depth < 5 m **and** target depth < 5 m): count active
  buoyancy danger zones: 0 → normal, 1 → warning, ≥ 2 → danger.
- Otherwise by B: B > 10 → warning; −5 ≤ B ≤ 10 → normal;
  −15 ≤ B < −5 → warning; B < −15 → danger.

**Tooltip** (vanilla text replaced):
1. Total ballast-tank water (m³) and % of total ballast capacity.
2. Per ballast tank, water m³ and % of that tank's capacity, for the six
   ballast tanks in ship order: quick-diving tank, main ballast 1 (stern),
   main ballast/reserve fuel 2, main ballast 3 (central), main ballast/reserve
   fuel 4, main ballast 5 (bow).
3. Total compartment flooding (m³).
4. Per compartment flooding m³: stern torpedo room, engine room, galley, crew
   quarters, control room, officers' area, bow torpedo room. Value is
   highlighted as alarming at ≥ 5 m³ (galley: ≥ 2 m³).
5. Diagnostic block (the original gated this on a debug flag that shipped
   **enabled**, so players saw it): flooding/blowing valve mode, target depth,
   deck depth, depth, dive-plane angle (rotation × 30), dive-plane force
   scale and performance, and air pressure (atm) of each of the 7 compartments
   plus the conning tower.

Percentages formatted to integers, volumes to one decimal, pressures and
depths to two decimals.

Interacts with: ballast/depth (same ratio drives depth-control logic in that
area).

### 2.13 Diagnostic state text additions

- Water storage items (objects identified as water storage) inside a
  compartment whose mass value > 100 append a line giving that compartment's
  air pressure in atm (2 decimals). This is the player's main way to read
  pressure without debug.
- Leakable equipment state text appends the hole's size, openness and
  underwater flag (debug info, ungated). Owned by leaks area; optional.

---

## 3. Game integration points (2022.1 names — re-map to 2026.1)

| # | Game member (2022.1) | Kind | Purpose |
|---|---|---|---|
| 1 | `UBOAT.Game.Scene.Physics.Tank.SimulateBreach(ExternalPressure)` | prefix (runs, then vanilla) | §2.1 air floor; reads private tank-type field, `Air`, `Capacity`, `Water`, breach `Openess` |
| 2 | `UBOAT.Game.Scene.Entities.Compartment.IsAirToxic` getter | replace | §2.2 |
| 3 | `Compartment.AirToxicity` getter | replace | §2.2 |
| 4 | `UBOAT.Game.Scene.Characters.Statuses.Rebreather.DoUpdate` | prefix, conditionally skip original | §2.3 |
| 5 | `UBOAT.Game.Scene.Items.Storage.GetStateText` | postfix (append) | §2.13 |
| 6 | `CompartmentPressure.Pressure` getter | replace | §2.4; reads private owning-compartment field, `Target` |
| 7 | `CompartmentPressure.IsBeingDamaged` setter | replace | §2.5; writes private target and pressure-damage-status fields; uses `Equipment.AddStatus/RemoveStatus`, `PressureDamage` status type |
| 8 | `Compartment.LowFrequencyUpdate` (returns next interval) | replace, return 1 | §2.6; reads private pressurized-equipment list and interactable-slot list; uses `LightBulb`, `Equipment.AddWaterDamage`, `InteriorDesignPressure`, `InteriorPressureDurability`, `Compartment.WaterElevation`, `WaterElevationAboveFloor`, `NeighbouringCompartments`, `Compartment.Contains(Vector3)`, `InteractableSlot.WaterElevationToleration`, `IsBelowWater` |
| 9 | `UBOAT.Game.Scene.Interactions.Interactable.OnSlotInteractionChanged` | prefix | §2.8; writes private max-characters fields; `InteractableSlot.DisabledSlots`, `RemoveDisableReason` |
| 10 | `Compartment.AddInteractableSlot(InteractableSlot)` | prefix | §2.7; writes private slot fields for water tolerance and interaction type |
| 11 | `UBOAT.Game.Scene.Entities.PlayerShip.FixedUpdate` | postfix | §2.9; uses `GetEquipment<Periscope>("Attack Periscope")`, `ExteriorHatch.Locked`, `DeckDepth`, `DieselEngine`, `SnorkelSlot.AssignedDevice`, `Snorkel.Performance`, `ActiveEngine`, `DieselEngine.FuelUsage`, `Compartment.Air`, `AirPressure` |
| 12 | `AirCompressor.Update` | replace | §2.10; reads private compression parameter, tank array, max pressure |
| 13 | `UBOAT.Game.UI.Notifications.BuoyancyNotification.DoUpdate` | replace, return 2 | §2.12; private label, displayed value, arrow objects, danger zones, three icon sprites; `PlayerShip.BuoyancyRatio`, `TargetDepth` |
| 14 | `BuoyancyNotification.GetTooltipContents` | replace | §2.12; `PlayerShip.Tanks` (filter `BallastTank`), private compartments array and valve reference; `FloodingAndBlowingValve.Mode`, `HasDisableFlag(TurnedOff)`; `CurrentDepthSteersRotation`, `VerticalSteersForceScale`, `VerticalSteersPerformance` |
| 15 | `LeakableEquipment.GetStateText` | postfix | §2.13 (leaks area) |
| 16 | New action deriving from `DeviceUseAction` (Equalize) | new type, registered via the mod's Crew data sheet "Actions" table with activator tag *Hatch*, contexts Normal/Queue/NoCharacter | §2.11; uses `CheckActivatorUseConditions`, `CheckInteractableUseConditions` (slot "Default", allow-underwater-with-rebreather flag), `UseDevice`, `Character.IsInteracting`, `CancelOrders`, `Hatch.Locked`, `CanBeOpened`, `LeftCompartment`, `RightCompartment`, `PressureBlockadeCompartment` |
| 17 | New action deriving from `DeviceUseAction` (blocking toggle) | new type, registered with activator tag *Settings*, contexts Normal/Queue | §4 |

Bootstrap (context): the original applied all patches at mod load
(`IUserMod.OnLoaded`), re-ran the game's dependency injection into the mod
assembly on scene awake, and on main-scene start reordered GUI siblings so
scene notifications draw above task markers and below activator banners /
interaction wheel. The GUI reordering is UI-layering, not part of this area.

Compartment indices assumed by the tooltip (0..7 = stern torpedo, engine,
galley, crew, control, officers, bow torpedo, conning tower) and ballast-tank
indices (0..5 as in §2.12) are positional; re-derive in 2026.1 by identity,
not index.

---

## 4. Settings and persistence

| Setting | Where | Default | Effect |
|---|---|---|---|
| Hatch pressure blocking | A crew order on the ship's *Settings* activator; label states whether choosing it will enable or disable | **Off** (blocking disabled) | When on, the hatches area's rule prevents opening a hatch while the pressure-blockade side is ≥ 0.9 (atm + metres of water, per that area) above the other side; equalize (§2.11) becomes necessary. When off, pressure never blocks hatches. Always enabled/selectable; executes instantly (no crew walk). |

Architect note: persistence is the background-task settings store owned by
04 §3 (00 C4). Toggle contexts are Normal, Queue and NoCharacter (00 C6).

Persistence: the original stored the flag as the presence of a named dummy
scale modifier on the ship's energy-gain parameter, apparently to piggy-back
on save serialization. Requirement for the reimplementation: the flag must
survive save/load with the campaign, default Off for new and pre-existing
saves, and must not affect any gameplay parameter.

No other tunables are exposed; all constants in §2 are fixed.

Runtime state that is not saved and need not be: the buoyancy trend's
previous-tons value; pressure-damage status objects (re-derived each tick).

---

## 5. Player-visible messages (meaning only; write new text)

- Equalize order label: "equalize the pressure across this hatch".
- Toggle label (two variants): "turn pressure-based hatch blocking on" /
  "turn it off".
- Water-storage state line: the compartment's internal air pressure in atm.
- Buoyancy label: signed integer percent.
- Buoyancy tooltip section headings: ballast total; each ballast tank's name;
  compartment flooding total; each compartment's name; diagnostic labels
  (valve mode names: inactive, optimal blowing, emergency blowing, manual,
  adjusting to neutral, flooding, flooding backwards; target depth; deck depth;
  depth; dive-plane angle/force/performance; per-compartment pressure).
- Pressure-damage status on equipment (game's existing status/visuals).
- Unbreathable air: the game's existing toxic-air feedback (crew choking).

---

## 6. Acceptance criteria

Pure-calculation (no game needed) unless marked **[in-game]**.

1. Breach floor: compartment tank, openness 0.3, air 0.02 → air becomes 0.1. Air 0.5 → unchanged. Ballast tank with air 0.02 → unchanged. Openness 0 → unchanged.
2. Unbreathable: (oxygen 0.5, p 1.0, water 0.2 m) → breathable; oxygen 0.04 → unbreathable; p 0.14 → unbreathable; p 0.15 → breathable; p 8.5 → breathable; p 8.51 → unbreathable; water 1.51 m → unbreathable; water 1.5 m → breathable.
3. Toxicity value is −0.1 exactly when (2) says unbreathable, else −1.0.
4. Rebreather: over many updates, mean consumption is 0.4× vanilla (±statistical tolerance), or exactly 0.4× in a deterministic implementation.
5. Interior hatch pressure point with p_L 3.0, p_R 1.2 reports 1.8; with p_L 1.2, p_R 3.0 reports 1.8. Exterior-hatch point and non-hatch point report the owning compartment's pressure.
6. Bulb risk probability: p 3, submerged → 0; p 5, not submerged → 0; p 5, submerged → 0.1 × clamp(0.025, 0, 1) = 0.0025; p 20, not submerged → 0.1 × 0.4 = 0.04; p 50 → 0.1 (capped). Bulb with health 0 → never damaged.
7. Equipment overstress: non-hatch, water above floor 0.5 m, point elevation 0.8 m, P 10 > design 5 → not overstressed; water 0.9 m → overstressed. Hatch with P 2 > design 1 and dry compartment → overstressed. Damage probability per tick = 0.05 × (1 − D): D 0.8 → 0.01; D 1 → 0.
8. Setting being-damaged true twice attaches one status; false removes it; false again is a no-op.
9. Slot underwater: own water 1.0 m, tolerance 1.4 → false; a containing neighbour with 1.5 m → true; own 1.5 m but containing neighbour 0.2 m → false (neighbour overrides).
10. Venting: conning tower p 0.8, dt 0.02, exterior hatch open, deck depth 10 → ΔAir = +min(0.2×0.02×10, 1) = +0.04. Same with deck depth 1 m → +0.04 + 0.004 = +0.044. p 1.5, hatch open → −0.1. Clamp: |Δ|=10, dt 0.02, k 10 → ±1 per step from that rule.
11. Diesel compartment, p 0.7, deck depth 5 m, no snorkel → no venting; with snorkel performance 0.5 → +0.06 at dt 0.02; performance 0.49 → none; deck depth 8 m with snorkel → none.
12. Diesel draw: diesel active, p 0.36, fuel usage 1e-5/s, dt 0.02 → −min(0.002, 0.1) = −0.002; p 0.35 → no draw; electric engine active → no draw.
13. Compressor: t = 10 s, N 2, C 0.5, p_max 200, tank p 100 → chunks 4, 4, 2; per tank per 4 s chunk transfer = (4×0.5/2)×0.5 = 0.5 (ignoring the tank's pressure rise within the frame), compartment loses 0.025 per transfer. Compartment p 0.35 → no transfer. Tank at p_max → transfer 0.
14. Equalize rate factor k: dt 0.02, s 1 → 0.004; s 4 → 0.02×1/10 = 0.002; frame delta 2.0, s 1 → dt clamped to 0.5 → k 0.1.
15. Equalize air flow: p_L 2, p_R 1, h_L 99 (vent clearance 3 m → conductance min(50, 750) = 50), Air_L 10, k 0.004 → f = min(9.8, 1×50×0.004) = 0.2 left→right. Air_L 0.25 → f capped at 0.05. Δp 0.0005 → no flow. h_L 103 and water above floor 3 m → no air flow (vent submerged).
16. Equalize water flow: p_L 1, p_R 1, h_L 101.5, h_R 99 → Δ = 1.5, k 0.004 → 0.006 m³ left→right; h_L 99.5 → none.
17. Equalize is hidden for: open hatch, exterior hatch, hatch lacking either compartment or the blockade side, non-hatch activator.
18. **[in-game]** Ordering equalize on a pressure-blocked closed hatch: the crew member walks to it, pressures converge, the hatch opens on its own once the hatches-area test passes, and the order clears ~2.3 s later.
19. Buoyancy: R 0.123 → B 12, T 18.45; R −1.5 → B −90; R 1.2 → B 100.
20. Trend: T_prev 10, T 10.25 → up; T 9.85 → none; T 9.7 → down.
21. Icon submerged: B 11 → warning; 10 → normal; −5 → normal; −6 → warning; −15 → warning; −16 → danger. Surfaced (deck 4, target 0): 0/1/2/3 active danger zones → normal/warning/danger/danger regardless of B. Deck 4 but target depth 20 → uses B rules.
22. Tooltip flooding highlight: engine room 4.9 m³ normal, 5.0 alarming; galley 2.0 alarming.
23. Toggle: default Off; one use → On, label flips; second use → Off. **[in-game]** state survives save/load; with Off, a hatch between 1 atm and 5 atm compartments opens freely.
24. **[in-game]** Running diesels submerged with no snorkel drops engine room pressure toward 0.35 atm and no lower from diesel alone; surfacing (deck depth < 3.5 m) restores ~1 atm within a few seconds.
25. **[in-game]** Light bulbs and pressure-sensitive equipment in a deeply flooded, high-pressure compartment accumulate water damage and show the pressure-damage status.

---

## 7. Open questions, suspected bugs, ambiguities

1. **Breach floor expression is a no-op wrapper.** The original computes a
   free-volume-based value but clamps it between 0.1 and 0.1, so it is always
   0.1. Likely intended min(max(x, 0.1), something larger). Spec keeps the
   observed behavior (constant 0.1).
2. **Vanilla low-frequency compartment update is dropped entirely.** Whatever
   vanilla 2022.1 (or 2026.1) does there (e.g. its own pressure damage, slot
   flags, possibly oxygen/fire/smoke bookkeeping) stops running. The
   reimplementation must diff vanilla 2026.1's method and decide whether to
   wrap (postfix) rather than replace.
3. **AirCompressor update fully replaced** with no explicit power/running
   check; it relies on the compression parameter being 0 when off. Confirm in
   2026.1, and confirm vanilla did not also consume power/oxygen there.
4. **Toxicity value sign** (−0.1 vs −1.0): meaning in the game is unclear
   (possibly rate of oxygen change or toxicity accumulation). Verify against
   2026.1 consumers before choosing values. **Resolved (00 C18):** the 2026.1
   choking status adds this value to its per-check injury chance. Keep −0.1 / −1.0.
5. **Unit mixing.** Equalize and the hatch can-open test add metres of water
   head 1:1 to atmospheres, whereas the hatches-area tooltip divides water
   head by 10 (physically correct: ~0.1 atm/m). One of them is inconsistent;
   decide with the hatches spec owner. **Now user decision 00 D1** (single
   constant owned by 02 §2.1).
6. **Hard-coded world heights** (vent 102, valve 100) and the hatches area's
   101.55 threshold assume the 2022.1 interior model's Y offset. Derive from
   hatch geometry (e.g. hatch lowest/highest points) in 2026.1.
7. **Name-based object identification** throughout (exterior hatch,
   "Hatch 06", "Bulkhead", "Hole", "Pipe", "Depth Steers", "Electric Engines",
   storage containing "Water", attack periscope). Fragile; prefer
   type/component lookups in 2026.1.
8. **Interaction types 4/5/6** are raw enum integers; map to the 2026.1
   interaction-type enum by meaning (they likely select crouch/stand/climb
   animations at hatches).
9. **Storage pressure readout** is gated on compartment "mass" > 100, an odd
   proxy (possibly mass of contained water). Clarify intent; a simpler rule
   (always show) may be preferable.
10. **Shipped debug output**: the tooltip diagnostic block and leak hole
    state text were visible to players because the debug flag was on. Decide
    whether the reimplementation shows them (suggest: behind a setting).
11. **Static trend memory** is shared across all buoyancy notification
    instances and starts at 0, so the first update after load shows a
    spurious arrow.
12. **Tooltip assumes Type VII layout** (exactly 6 ballast tanks in fixed
    order, 8 compartments in fixed order, 150 t reserve). Other boat types in
    2026.1 will break or mislabel; also divides by tank capacity without a
    zero guard.
13. **Rebreather randomness is frame-rate dependent** in the original
    (per-update coin flip); a deterministic 0.4× scale is the intended
    behavior.
14. **Toggle storage hack**: a named scale modifier with flag argument
    `false` on energy gain. Unclear whether it alters energy gain or is
    persisted by the save system; the reimplementation should use proper mod
    save state.
15. **Equalize time scaling** uses Unity's frame delta (already time-scaled)
    and further multiplies by 2/√s, so absolute equalization takes longer in
    game time at high compression. Intentional for stability; confirm feel.
16. **Neighbour slot override order**: if a slot lies in more than one
    neighbouring compartment, the last checked wins, ignoring the own
    compartment. Probably should be "underwater if any side is above
    tolerance".
17. **Hatch overstress uses |Δp| vs the hatch's interior design pressure**
    irrespective of water, so a hatch can take damage in a dry boat with a
    pressure difference; likely intended (bulkhead strain) but confirm
    damage frequency is acceptable.
