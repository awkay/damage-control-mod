# Spec 02 — Hatches, leak blocking and crew automation

Status: behavioral spec (dirty room output). Source studied: "Iron Coffins" 0.61 for
UBOAT 2022.1. All game type/member names below are **2022.1 names** and must be re-mapped
against the 2026.1 assemblies before implementation. Everything here is described in terms of
game concepts; implementers write their own code, identifiers, text and art.

Conventions used below:

- *Air pressure* `P` of a compartment is the game's per-compartment air pressure, in
  atmospheres (1.0 ≈ surface).
- *Air* `A`, *Water* `W`, *Capacity* `C` are the game's per-compartment air amount, water
  amount and total volume (same unit as each other, as the game defines them).
- *Water elevation* `WE` is the world-space height (m) of the water surface in a compartment;
  *water above floor* `h` is water depth (m) above that compartment's floor.
- For a hatch: the game names the two adjoining compartments *left* and *right*, and one of
  them is the hatch's *pressure-blockade compartment* (the side the hatch leaf seals against).
  In this spec the blockade compartment is called the **seat side** and the other one the
  **far side**. `lowY` is the world height of the hatch opening's lowest point; `highY` =
  `lowY` + (the hatch's box-collider height × the hatch transform's vertical scale).
- *Step* `s` is the per-frame flow step defined in 2.3.

---

## 1. Overview

| Mechanic | Player-facing problem | Change vs. vanilla |
|---|---|---|
| Pressure-locked hatches (optional) | In vanilla, a closed hatch can always be opened regardless of what is on the other side; crews walk into flooded/pressurised compartments. | When the setting is on, a closed interior hatch cannot be opened while the pressure pushing it shut exceeds the pressure on the other side by ~0.9 atm (water head included). Crew pathing treats such hatches as walls. |
| Hatch as a physical seal | Vanilla moves air/water between compartments in its own way. | The mod replaces per-hatch air and water exchange: open hatches pass air and water according to pressure difference and water head; closed-but-broken hatches leak at 2 % of the open rate; leak-blocked broken hatches at 0.2 %. Destroyed hatches can be blown open by over-pressure. |
| Hatch info panel | Vanilla shows little about the two sides. | Hatch tooltip shows both-side pressures (colour-coded), whether it is pressure-locked, its design-pressure durability or a "badly damaged" warning, and whether it is currently leak-blocked. |
| Hatch open/close action | Vanilla behaviour. | Re-implemented so that a pressure-locked hatch shows a disabled Open with an explanation, the player in first-person toggles instantly, opening clears the "evacuated" flag on both adjoining compartments, and the player's choice is remembered as the hatch's preferred state. |
| Leak blocking (officer order, sailor self-initiated) | Vanilla's block-leak lets a man hold a leak; the mod wants an explicit "hold the leak until repaired" job that both officers can order and ordinary sailors can do on their own. | Two actions. While a man is physically at the leak, leakage of that equipment is cut by 80 % (non-hatch) or the broken-hatch leak drops to one tenth. It never repairs anything; it lasts until the leak is gone, the hatch is opened, or it is cancelled. |
| Automatic leak blocking toggle | Vanilla sailors auto-block leaks; players wanted control. | Vanilla automatic leak-blocking routine is **suppressed unless** the player enables it. |
| Automatic carrying of wounded toggle + manual trigger | Vanilla auto-carries incapacitated crew to bunks, sometimes at bad times. | Vanilla auto-carry routine is **suppressed unless** enabled, or until the player presses a one-shot "carry wounded now" order on a light switch; the one-shot runs until no incapacitated crew remain. |
| Automatic actions controller | Vanilla automation (ballast valves, compressors, pump, ventilation). | Whole routine replaced. Relevant to this area: the trim pump is never auto-switched-off while any leak exists on the boat. Ballast/compressor parts are cross-referenced to the ballast spec. |
| Interaction wheel on leaks | In first person, the wheel offers "do it yourself" actions only. | When the player in first person opens the wheel on a leaking non-hatch item, the wheel is built as if no character were selected, i.e. it offers crew orders. |

---

## 2. Mechanics and actions

### 2.1 Hatch "can be opened" rule (pressure lock)

Replaces the game's hatch "can be opened" property entirely. Evaluated in this order:

1. If the hatch currently carries a *visible* "is being repaired" status → **cannot** be opened.
2. If the hatch is not a pressure-blockade hatch (game flag on the hatch) → **can** be opened.
3. If the global "hatch pressure blocking" setting (§4) is **off** → **can** be opened.
4. If the hatch has left, right and seat-side compartments:
   - seat-side pressure `Pseat' = P_seat + max(0, WE_seat − lowY)`
   - far-side pressure `Pfar' = P_far + max(0, WE_far − lowY)`
   - **can** be opened iff `Pseat' < Pfar' + 0.9`.
   - Note: here water head in **metres is added directly to atmospheres** (1 m treated as
     1 atm). The tooltip (2.4) divides by 10. See §7, Q1.
5. Else if only the seat-side compartment exists → **can** be opened.
6. Else (no compartments, e.g. exterior hatch): if the hatch belongs to a ship, **can** be
   opened iff the ship's deck depth is < 2.5 m; if not on a ship, **can**.

Consequences: the rule is read by the open/close action (2.5), by the lock-state setter guard
(2.2), by the tooltip (2.4) and by crew navigation (other spec: navigation) which treats a
non-openable hatch as impassable for path reachability.

### 2.2 Lock-state guard

Hook on the hatch "locked" setter (prefix, can cancel): if the hatch is currently locked
(closed), its scene object name does **not** contain the word for "exterior", and the hatch
cannot be opened (2.1), the assignment is **suppressed** (hatch stays closed). This blocks every
code path — player, crew AI, vanilla scripts — from opening a pressure-locked interior hatch.
Exterior hatches are excluded by name match.

### 2.3 Hatch per-frame simulation (replaces vanilla hatch update)

Runs every frame per hatch, fully replacing the game's hatch update.

**a. Occlusion portal (rendering, must preserve).** If the hatch has an occlusion portal:
- If the main camera is inside a compartment: portal is open iff (hatch unlocked AND the
  smaller of |camIdx − leftIdx| and |camIdx − rightIdx| is < N) OR the hatch is animating, where
  idx = the compartment's horizontal index and N = 3 if camIdx = 1 or (camIdx ≤ 3 and
  leftIdx < 3), otherwise N = 2.
- If the camera is not in a compartment: portal open iff hatch unlocked or animating.
Implementation may instead keep vanilla's portal logic if the 2026.1 hook allows a postfix
rather than full replacement — this part is not a gameplay change. (In-game check.)

**b. Frame step.** `dt` = frame delta time capped at 0.5 s. Rate factor `k` = 2 when game time
scale ≤ 1, else `2 / sqrt(timeScale)`. Base step `s = dt × k`. Effect: at 1× the exchange runs
at "2 units per second per unit driving term"; under time compression the per-game-second rate
is reduced (stability damping): at time scale T the per-game-second rate is `2/sqrt(T)`,
e.g. 0.5 at 16×.

**c. Destroyed-hatch blow-open.** If hatch health = 0, it is not being repaired, it is closed
and not animating: if far-side air pressure > seat-side air pressure + (hatch interior design
pressure / 3), then with probability 5 % **per frame** the hatch is forced open and a
"pressure damage" sound plays at the hatch. (Water head not included here.)

**d. Leak status upkeep.** If health = 0, closed, not animating and no "leaking" status →
add the game's leaking status. If health > 0 and leaking status present → remove all leaking
statuses. (So any destroyed closed hatch leaks; repairing even 1 HP stops the leak.)

**e. Flow gate.** Exchange runs only if the hatch is open, or closed **and** leaking. If closed
and leaking: `s ×= 0.02`, then `s ×= 0.1` once **per** leak-block modifier present on the
hatch (2.6). Otherwise (closed, not leaking) no exchange and both water-jet particle effects
stop.

**f. Vertical passage** (game flag on the hatch; the conning-tower hatch). Left = lower
compartment, right = upper (verify in 2026.1). Opening threshold height `H = 1.85 m` of water
above the lower compartment's floor.
- Air: `d = P_L − P_R`. If `d > 0.001` and `h_L < 1.85`:
  `flow = min(max(0, A_L − 0.2), d × min(10, (1.85 − h_L) × 10) × s)`, moved L→R.
  If `d < −0.001` and `h_L < 1.85`: same with roles swapped (limit `A_R − 0.2`, factor uses
  `h_L` still), moved R→L. A compartment never gives away air below 0.2 units.
- Water: `d2 = P_L − P_R − h_R` (upper compartment's water depth in metres subtracted
  directly from atm; see §7 Q1).
  - If `d2 > 1e-6` and `h_L > 1.85`: `flow = min(W_L, min(d2 × s, (1.005 − W_R/C_R)² × s))`, L→R.
  - Else if `W_R > 0`: `flow = min(W_R, max(0.1 × s, min(−d2 × s, (1.005 − W_L/C_L)² × s)))`,
    R→L. I.e. water in the upper compartment always drains down at least `0.1 × s` per frame,
    even if the lower compartment is full (§7 Q4).
  - No particle effects are driven for the vertical hatch.

**g. Horizontal passage** (all other hatches).
- Air: `d = P_L − P_R`. If `d > 0.001` and `WE_L < highY`:
  `flow = min(max(0, A_L − 0.2), d × min(10, (highY − WE_L) × 10) × s)`, L→R.
  Else if `d < −0.001` and `WE_R < highY`: mirror, R→L. (Air passes only while the opening is
  not fully submerged on the source side; the opening factor grows linearly with the dry
  height of the opening and saturates at 10 once ≥ 1 m of it is above the source water.)
- Water: `d = (P_L + max(0, WE_L − lowY)) − (P_R + max(0, WE_R − lowY))` (metres added to atm
  raw; §7 Q1).
  - If `d > 1e-6` and `WE_L > lowY`: `flow = min(W_L, min(d × s, (1.01 − W_R/C_R)² × s × 10))`,
    L→R; start the right-side water-jet effect, stop the left one.
  - Else if `d < −1e-6` and `WE_R > lowY`: mirror R→L; start left jet, stop right.
  - Else stop both jets.
- The `(1.01 − fill)²` term chokes inflow as the receiving compartment approaches full.

Interactions: this is the inter-compartment half of the pressure/air model (spec on
compartment pressure): air moved here changes compartment pressure; water moved here changes
flooding, buoyancy and trim (ballast/depth spec).

### 2.4 Hatch info panel (replaces vanilla hatch state text)

- Exterior hatch: show only the outside water pressure value (the game's existing localized
  "leak – pressure" text with the external pressure).
- If any of left/right/seat compartment is missing: no text.
- Otherwise, lines in order:
  1. If not openable (2.1): red warning that pressure difference prevents opening.
  2. Seat-side ("opening side") pressure = `P_seat + max(0, WE_seat − lowY)/10` atm, 2 decimals.
  3. Far-side ("closing side") pressure, same formula. Each value coloured red if > 2.0 atm,
     blue if < 0.8 atm, otherwise white/neutral.
  4. Blank line, then: if health > 0, the game's existing "hatch design pressure durability"
     text with the hatch's interior design pressure; else a red warning that the hatch is badly
     damaged, will leak, and its locks probably won't hold more than a few atmospheres.
  5. One green line per leak-block modifier on the hatch saying it is partially sealed and
     leakage is much reduced (should be shown once; §7 Q6).
  6. Debug diagnostics (capacity/water/air/water elevation/opening heights) — the original
     ships with debug **on**; the reimplementation must show these only behind a debug flag.
  7. If the viewing character is the player-controlled (first-person) one: pick the compartment
     on the far side of the hatch from the character (character's compartment horizontal index
     ≤ hatch's left index → right compartment, else left; a character not in any compartment is
     treated as index 4). If that compartment is flagged evacuated, append the game's existing
     "compartment evacuated" text.
- All new lines must be localized (original hard-codes English).

### 2.5 Hatch open/close action

Data registration: activator tag = hatch; any character; contexts Normal, Queue, NoCharacter.
Possibly overrides a vanilla row of the same name (§7 Q8).

- **Label**: "Close" when open, "Open" when closed. Wheel sort priority 12.
- **Availability**:
  - Closed and not openable (2.1) → *Disabled*, with an explanatory tooltip (pressure too high).
  - Otherwise standard activator use checks with "no duplicate actions" and "sailor may act
    without an officer present".
  - If the hatch has no interactable component → *Enabled* for the first-person player
    character, *Hidden* for others.
  - Else standard interactable-slot check on the default slot with "usable under water always".
- **AI desirability**: `0.9 + 0.1 / max(1, d²)` where `d` = character-to-hatch distance (m).
- **Execution**:
  - Never transitions the camera to the cut-away stage view.
  - First-person player character: no walk/animation; toggles the lock state immediately, sets
    the hatch's "preferred to be locked" memory to the new state, completes instantly. (Does
    **not** clear evacuation; §7 Q5.)
  - Anyone else: normal device use (walk to hatch, use animation — duration is the game's
    default device use for that hatch). On use: toggle lock state; set preferred state; if the
    hatch is now open, clear the evacuation flag on both adjoining compartments.
  - The lock guard (2.2) still applies, so an attempt to open that became illegal mid-walk
    silently fails.
- Cost: none.

### 2.6 Leak blocking — common effect

> Architect note (00 §2.5, C9, C28): **this section owns the leak-blocking
> mechanism** for the whole mod. On hatches, seals are tracked in a Core
> registry (blocker ×0.1, repair ×0.1, multiplied, then ×0.02). The registry
> replaces the modifier on the hatch Performance stat. For leakable equipment,
> vanilla 2026.1 `BlockLeakAction` already applies a 0.2 `LeakIntensity` scale
> modifier. Vanilla's cast of a hatch to `LeakableEquipment` must be handled.

While the performing character is actually *interacting* (in the slot, animating) a single
named "leak block" scale modifier is attached:
- On a **hatch**: attached to the hatch device's performance stat. The hatch simulation (2.3e)
  only checks for its presence (each one cuts closed-hatch leakage to one tenth); the modifier's
  numeric value is irrelevant there.
- On any **other leakable equipment** (hull holes, pipes, tanks, etc.): attached to that
  equipment's leak-intensity stat with value **0.2** → leak flow reduced by 80 %.
- Removed when the action stops for any reason. The action also removes any "is being repaired"
  status on the equipment when it stops.
- It never adds repair progress and never consumes parts.
- Cross-reference: the mod's repair action (repair spec) also attaches a leak-block modifier to
  hatches/holes/pipes with value `1 − repairProgress`, so repair progressively shrinks a leak.
  For hatches only presence matters. Reimplementation should use one shared modifier name /
  mechanism so 2.3e and the panel line work for both.

Animation selection (both variants): find the equipment's "block leak" slot, falling back to its
"default" slot; translate that slot's interaction pose type into the matching repair-pose type:
1 or 4 → 10; 2 or 5 → 11; 7 → 13; 8 → 14; 9 → 15; ≥ 10 → unchanged; anything else (0, 3, 6)
→ 12. (These are the game's interaction-type enumeration ordinals; re-map to 2026.1 names.)
Then occupy, in priority: a free "default" slot, else a free "block leak" slot, else the
"repair" slot, using the repair animation set.

Termination (checked every update): cancel (with the game's standard 2.3 s order-cancel delay)
when: no leak existed at start; the equipment is a hatch and it is now open; the occupied
interactable has more interacting characters than its maximum; or the leak status no longer
refers to equipment (leak fixed/removed). Otherwise it continues indefinitely.

### 2.7 Block leak — officer order

- Registered under the **same row name as vanilla's block-leak action**, replacing it. Activator
  tag = leaking; any character; contexts Normal, Queue (not NoCharacter).
- May be delegated by an officer to other characters (sailors in his section).
- Label: block leak. Wheel sort priority 10. Description: blocking reduces leak rate by 80 %.
- Availability: Hidden unless the equipment has a visible leaking status; Hidden if it is an
  open hatch; then standard activator checks (normal officer-supervision rules); then
  interactable check on the "block leak" slot (alternative "repair"), falling back to the
  "default" slot, with "usable under water only with rebreather".
- AI desirability: constant 1.0.
- Interruptible (default).

### 2.8 Block leak — sailor self-initiated

- Separate row; activator tag = leaking; soft requirement "sailor" character tag; contexts
  Normal, Queue, NoCharacter.
- Label: block leak by ordinary sailor. Sort priority 11.
- Availability: as 2.7 but activator checks allow a sailor **without an officer**, and slot
  checks use **no** underwater allowance (cannot work submerged).
- AI desirability: `0.98 + 0.02 / max(1, d²)` while leaking, else −1 (never chosen).
- On start sets queue priority 100 (jumps the queue). **Not interruptible.**
- This is the action the vanilla "automatic leak blocking by sailors" routine is expected to
  dispatch (§4 toggle); verify how 2026.1's routine selects its action (§7 Q8).

### 2.9 Interaction wheel on leaks

Hook on the interaction wheel's button-building step (prefix, mutates the wheel's character
field): if the target activator has equipment that is **not a hatch** and has a visible leaking
status, and the wheel's character is the first-person player character, clear the character
so the wheel is built in no-character (orders) context. Result: the sailor block-leak (NoCharacter
context) is offered; the officer variant (no NoCharacter context) is not.

### 2.10 Automatic carrying of wounded

- Toggle (§4) on: vanilla auto-carry-to-safety routine runs normally.
- Toggle off (default): routine is skipped every tick **except** while a session-level one-shot
  flag is set and the game is not loading. When the routine runs under the one-shot and the
  ship has zero ragdolled (incapacitated, lying) crew, the flag is cleared (that tick still runs).
- One-shot flag starts **set** when the mod loads (per process, not per save), so the routine
  runs once at the start until no one needs carrying.
- **Manual trigger action** "carry wounded to bunks": activator tag = light switch (vanilla);
  contexts Normal, Queue, NoCharacter; not shown in the orders list; Enabled iff ship has ≥ 1
  ragdolled crew, else Disabled. Using it sets the one-shot flag instantly; no character moves
  for the action itself (pre-start declines to queue).

### 2.11 Automatic actions controller (replaces vanilla routine)

> Architect note (00 C7): **this section owns the controller hook.** In 2026.1
> the controller is not replaced. Only the trim-pump leak rule ships, as a narrow
> postfix. The valve, compressor and ventilation parts below are dropped
> because vanilla 2026.1 covers them; 03 §2.I points here.

The whole periodic update is replaced; returns a 1 s re-run interval. Shared helper: a
"sustained condition" timer — a condition must stay true continuously for **30 game seconds**
(game ticks, 10⁷ per second) before the action fires; any false resets it.

Leak-relevant:
- **Trim pump auto-off**: queue "turn off" on the pump if it is on, nobody is using it, it has
  no water left to pump, **no equipment on the ship has a leaking status**, sustained 30 s.

Other parts (owned by the ballast/compressor spec; listed so the replacement is complete):
- Target depth exactly 0 and blow valve in manual-blowing mode or higher: stop valve users doing
  diesel-blow / deck-awash / flood actions; if the valve has no users, queue the vanilla
  compressed-air valve action.
- Target depth ≈ 1 m, diesel running, ballast tanks #1–#5 (by ship order) holding any water,
  and the diesel-blow action exists: stop other valve actions, queue diesel-blow.
- Target depth ≈ 0.75 m and the deck-awash action exists: stop other valve actions, queue it.
- Target depth > 5 m, valve mode below flooding and not "adjust to neutral": stop other valve
  actions; if no crew is doing the flood action, queue it. (Original logs every tick — drop.)
- Diesel compressor and electric compressor: queue "turn off" once fill ratio ≥ 0.99999, on,
  unused, sustained 30 s. (Electric timer is mod-owned, not saved.)
- Ventilation: queue "turn off" when on, unused, and the boat is **not** submerged/submerging,
  sustained 30 s (§7 Q7).
- Trim-pump energy draw is overridden to 0.00009 (energy units/s, as a negative gain) on pump
  enable — pump/electrics spec.

---

## 3. Game integration points (2022.1 names — re-map to 2026.1)

Hooks (Harmony):

| Target | Kind | Purpose |
|---|---|---|
| `UBOAT.Game.Scene.Items.Hatch.CanBeOpened` (getter) | replace (skip original) | Rule 2.1. Reads private fields: pressure-blockade flag, lowest-point transform. |
| `Hatch.Locked` (setter) | prefix, may cancel | Guard 2.2. |
| `Hatch.OnDisable` | prefix records lock state; postfix | When a hatch is disabled outside game quit: if seat-side air pressure > far-side, restore the recorded lock state and add leaking status; else force open. Reads game-state "quitting". (Behavior of vanilla OnDisable must be checked in 2026.1.) |
| `Hatch.DoUpdate` | replace | 2.3. Reads private: occlusion portal, main camera, is-animating, vertical-passage, lowest point, exterior flag, left/right flow particle systems. |
| `Hatch.GetStateText(character)` | replace | 2.4. Reads private: lowest point, exterior flag, external pressure, raw lock field. |
| `UBOAT.Game.Scene.UI.InteractionWheelUI.CreateButtons` | prefix (writes private character field by ref) | 2.9. |
| `AutoCarryWoundedToSafety.DoUpdate` | prefix gate | 2.10. |
| `LeakBlockingBySailors.DoUpdate` | prefix gate | Run vanilla only when auto-leak-blocking toggle is on. |
| `AutomaticActionsController.DoUpdate` | replace | 2.11. Reads many private fields (delays, valve, compressors, pump, ventilation, cached actions); invokes private "add action". |
| `TrimPump.OnEnable` | postfix, high priority | Energy draw override. |

Game members read/called: `Hatch` (Left/RightCompartment, PressureBlockadeCompartment, Locked,
IsPreferredToBeLocked, IsAnimating, Health, InteriorDesignPressure, ParentEntity,
Performance.ScaleModifiers, statuses); `Compartment` (AirPressure, Air, Water, Capacity,
WaterElevation, WaterElevationAboveFloor, HorizontalIndex, Evacuation/Evacuated); `Ship.DeckDepth`;
`LeakableEquipment.LeakIntensity` (+ add scale modifier); statuses `Leaking`, `IsBeingRepaired`;
`PlayerShip` (Energy.Gain modifiers, RagdollCrew, Crew, Equipment, Tanks, TargetDepth,
DieselEngine, SubmergedOrGoingToSubmerge); `DeviceUseAction` base (UseDevice,
CheckActivatorUseConditions, CheckInteractableUseConditions, CancelOrders, QueuePriority,
OnPreStart, ShowInOrdersList, AllowDelegationToOtherCharacters, IsInterruptable);
`CharacterActionsDatabase.GetActionByName`; `GameTime.Ticks`; `LoadingUIController.IsLoading`;
sound database clip "pressure damage"; locale keys for external pressure, hatch durability,
compartment evacuated (all vanilla).

Related hooks in other areas that depend on 2.1: `NavMeshNavigator.IsDestinationReachable`
(blocked by non-openable hatches / evacuated compartments unless player order),
`Compartment.Character_CurrentActionChanged` (officers don't trigger evacuation timer),
`EvacuateToSafetyAction.Escape` (made stoppable), repair action.

**Custom Actions utility (Workshop 2374336654) dependency.** Needed for every action in this
spec (hatch open/close, both block-leak actions, carry-wounded trigger, three toggles):
1. It replaces the game's action-database loader so rows in the mod's `Crew/Actions` data
   sheet can name action classes from mod assemblies (vanilla resolves only game types), and so
   a mod row with a vanilla row's name (block leak) overrides it.
2. It creates a "Settings"-tagged toolbox activator in the stern torpedo room; the three
   toggles (§4) are bound to that tag.
The currently installed Custom Actions (v3.2) targets 2026.1. Alternative: register actions
ourselves and create our own settings activator, removing the dependency (decision for the
architect). **Now user decision 00 D2.**

---

## 4. Settings / toggles

All three are actions on the "Settings" activator; any character; contexts Normal, Queue; always
Enabled; instant (pre-start flips the state and declines to queue, no crew walks). Label shows
the action that will happen ("enable …" / "disable …").

| Setting | Default | Effect |
|---|---|---|
| Hatch pressure blocking | **Off** | 2.1 step 3. |
| Automatic leak blocking by sailors | **Off** | Gates vanilla leak-blocking routine. |
| Automatic carrying of wounded | **Off** | 2.10. |

Persistence: the original stores each toggle as the *presence of a named scale modifier on the
player ship's energy-gain stat*, relying on modifiers being saved with the game. This is a hack
(risk of scaling energy gain if the default modifier value ≠ 1; collisions). Reimplementation
must persist per-save (career) with the save game, default Off for new and pre-existing saves.
The one-shot carry flag (2.10) is session-only.

Architect note: storage is the background-task settings store owned by 04 §3 (00 C4).
Defaults await user decision 00 D3. The three toggles also take the NoCharacter context (00 C6).

---

## 5. UI

Interaction-wheel entries (each needs its own new icon in the game's icon folders, keyed by the
action's localization keyword; provide normal, highlighted and disabled variants where noted):

| Entry | Where | Icon idea |
|---|---|---|
| Open / Close hatch | hatch | vanilla open/close icons can be reused (game art) |
| Block leak (officer) | leaking item | hands pressing over a water jet |
| Block leak by ordinary sailor | leaking item | same motif + sailor cap |
| Carry wounded to bunks (+highlighted, +disabled) | light switch | stretcher / bunk |
| Enable / Disable automatic leak blocking | settings toolbox | leak motif with on/off mark |
| Enable / Disable automatic carry of wounded | settings toolbox | stretcher with on/off mark |
| Enable / Disable hatch pressure blocking | settings toolbox | hatch wheel with gauge, on/off |

The original bundle also contains "leave hatches open" / "keep hatches closed" wheel icons not
referenced by any code in 0.61 (orphaned or vanilla-keyword overrides) — not required.

Orders list: carry-wounded trigger is hidden from it; other actions use defaults.

Messages needed (meaning only, write new text, localize all):
- Disabled-open tooltip: hatch cannot be opened because of pressure difference.
- Panel lines of 2.4 (pressure-locked warning; seat/far side pressures in atm; badly damaged
  hatch warning; partially sealed note).
- Block-leak description: reduces leak rate by 80 %.
- Six toggle labels; carry-wounded label; block-leak labels; Open/Close.
The original lacks locale entries for the toggle labels and the disabled-open tooltip (§7 Q9).

---

## 6. Acceptance criteria

Pure-logic (unit-testable without the game, by isolating formulas behind plain functions):

1. Can-open rule: being-repaired (visible) → false regardless of anything else.
2. Can-open: non-blockade hatch → true; setting off → true.
3. Can-open with both compartments: `P_seat=2.0, P_far=1.0`, both dry → false; `P_seat=1.8,
   P_far=1.0` → true (threshold 0.9 strict `<`).
4. Can-open: water head 0.5 m above `lowY` on seat side with equal air pressures → true;
   1.0 m → false (metres-as-atm behavior, unless Q1 resolved otherwise — test must follow the
   decision).
5. Can-open with no compartments on a ship: deck depth 2.4 → true, 2.5 → false.
6. Step: `dt=1.0` → capped to 0.5; timeScale 1 → k=2; timeScale 4 → k=1.
7. Closed leaking hatch: step multiplier 0.02; with one leak-block modifier 0.002; with two 0.0002.
8. Horizontal air flow never takes a compartment's air below 0.2 and equals
   `d × min(10, (highY−WE)×10) × s` when not limited.
9. Horizontal water flow L→R is 0 when `WE_L ≤ lowY`; is bounded by `W_L`; is choked by
   `(1.01 − W_R/C_R)² × 10 × s`.
10. Vertical water: with `W_R > 0` and `d2 ≥ 0` but `h_L ≤ 1.85`, downward flow equals
    `min(W_R, 0.1 × s)`.
11. Blow-open: never when health > 0, open, animating or being repaired; when conditions hold,
    triggers iff far − seat > design/3 (probability path stubbed).
12. Leak status upkeep: destroyed+closed+idle adds exactly one leaking status; health > 0 removes it.
13. Pose mapping table 2.6 for inputs 0–11 matches exactly.
14. Sailor desirability: at d=0 → 1.0; at d=2 m → 0.985; not leaking → −1. Hatch desirability at
    d=0 → 1.0, d=10 m → 0.901.
15. Leak-block modifier on non-hatch equipment has value 0.2; on hatches its presence (not value)
    drives the 0.1 factor.
16. Block-leak visibility: hidden when no visible leak; hidden on open hatch.
17. Toggle: two activations return to the original state; label reflects next action; default
    Off on a fresh save.
18. Carry-wounded gate: toggle on → vanilla runs; toggle off & flag clear → skipped; flag set &
    loading → skipped; flag set & ragdoll count 0 → runs once and clears flag.
19. Carry-wounded trigger Enabled iff ragdoll count ≥ 1.
20. Pump auto-off requires 30 s continuous satisfied condition and no leaking equipment; a single
    false tick resets the timer.
21. Integration: a reflection test against the 2026.1 game assembly confirms every hook target in
    §3 exists with compatible signature (fails loudly if renamed).
22. Data rows: the mod's action sheet registers all seven actions with the activator tags and
    contexts given in §2/§4.

In-game checks (mark: IN-GAME):

23. IN-GAME: With pressure blocking on, flood the bow room to ~1 m and close its aft hatch: the
    hatch shows a disabled Open with the explanation; crew ordered elsewhere path around / report
    unreachable; turning the setting off makes Open available.
24. IN-GAME: An open hatch between a flooded and a dry room equalizes water levels; jet particles
    play on the receiving side and stop at equilibrium.
25. IN-GAME: Destroy a closed hatch: it leaks slowly; ordering block-leak reduces visible inflow;
    the panel shows the partially sealed line; opening the hatch cancels the blocker.
26. IN-GAME: A hull hole's leak rate drops by ~80 % while a man is in the block-leak pose and
    returns when he leaves.
27. IN-GAME: Auto leak blocking off → sailors don't self-dispatch to leaks; on → they do.
28. IN-GAME: Auto carry off → incapacitated crew stay put until the light-switch order is used,
    after which all are carried, then automation stops again.
29. IN-GAME: First-person player looking at a leaking pipe sees crew orders in the wheel;
    first-person toggling a hatch is instant.
30. IN-GAME: Toggle states survive save/load.
31. IN-GAME: Hatch occlusion/rendering has no visible popping vs. vanilla.

---

## 7. Open questions / suspected bugs in the original

1. **Units of water head.** Can-open (2.1) and both water-flow formulas add metres of water
   directly to atmospheres (1 m ≈ 1 atm, 10× too strong), while the tooltip divides by 10.
   Decide: keep original feel (tuned around it) or physically correct (÷10, would require
   retuning the 0.9 threshold). Recommend: keep original for parity, expose as a constant.
2. Blow-open chance is 5 % **per frame** → frame-rate dependent. Recommend a per-second hazard.
3. Blow-open and info-panel code assume seat/left/right compartments exist (no null checks in
   the update path) — guard in the reimplementation.
4. Vertical hatch drains the upper compartment into the lower at ≥ 0.1·s even when the lower is
   full or at higher pressure.
5. First-person instant toggle does not clear compartment evacuation, crew-performed toggle does.
6. Panel prints the "partially sealed" line once per modifier (duplicates when blocker + repair
   both active).
7. Ventilation auto-off condition fires when the boat is **surfaced** (vanilla intent is
   presumably the opposite). Owner: ballast/ventilation spec — confirm intent.
8. Whether the mod's hatch open/close and sailor block-leak rows override vanilla rows of the same
   name, and how vanilla's automatic leak routine picks its action, must be verified in 2026.1.
9. Missing locale entries (toggle labels, disabled-open tooltip); hard-coded English panel text;
   debug output shipped enabled.
10. Block-leak start reads the chosen slot's pose type without a null check if neither slot
    exists; officer variant's update lacks the null-character guard the sailor variant has.
11. Hatch "exterior" exclusion in the lock guard is by object-name substring — fragile; use the
    game's exterior-hatch flag if 2026.1 exposes it.
12. The one-shot carry flag is static and initialised true per process, so it runs on the first
    loaded save only, not on later loads in the same session.
13. Toggle persistence via energy-gain scale modifiers: confirm modifiers are saved and that an
    added modifier's default value is neutral (1.0); otherwise energy is scaled.
