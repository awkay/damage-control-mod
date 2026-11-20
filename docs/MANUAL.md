# Damage Control: Player Manual

Damage Control is a mod for **UBOAT 2026.1**. It makes flooding and damage control aboard your boat
work more like it would on a real U-boat. Water and air stay where the hull and the hatches keep them,
pressure builds up in flooded rooms, and the crew have to deal with leaks, hatches and ballast by hand.

> **Early release (0.1.0).** The mod builds, passes its offline checks and loads in the game. Apart from
> that load test, **nothing has been play-tested yet**. Expect rough edges and keep a backup of any
> campaign you care about. Where this manual says "untested", nobody has confirmed the behaviour in
> game. Bug reports are welcome (see [Reporting a bug](#reporting-a-bug)).

## Contents

1. [What the mod does, and why](#what-the-mod-does-and-why)
2. [Requirements, install and removal](#requirements-install-and-removal)
3. [Settings](#settings)
4. [Feature guide](#feature-guide)
   - [Air and pressure](#air-and-pressure)
   - [Hatches](#hatches)
   - [Leaks and crew](#leaks-and-crew)
   - [Ballast: Exhaust Blow](#ballast-exhaust-blow)
   - [Crew movement](#crew-movement)
   - [Repair and parts](#repair-and-parts)
5. [Tunables](#tunables)
6. [Known issues and limitations](#known-issues-and-limitations)
7. [Troubleshooting](#troubleshooting)

---

## What the mod does, and why

In the base game a breach is mostly a matter of pumps and repair crews. On a real boat, damage control
was about compartments. A closed hatch kept the sea out of the next room. The air trapped in a flooding
room was squeezed until it was unbreathable. A hatch with a heavy load of water behind it could not be
forced open. Damage Control brings those consequences into the game:

- Each compartment keeps its own air and water. They pass between rooms only through open hatches, or
  slowly through a wrecked one.
- Flooding compresses the air, and the pressure matters. Crew choke, light bulbs burst, and hatches
  strain.
- Your crew get new orders to fight it: equalize the pressure at a hatch, block a leak while someone
  else repairs it, blow the ballast with diesel exhaust, take equipment apart for scrap.
- Switches let you take manual control of leak blocking and of carrying the wounded.

**Credit.** The ideas come from salamander's *Iron Coffins* mod for UBOAT, and the credit for them
belongs to salamander. Damage Control is an independent, clean-room reimplementation for the 2026.1
game. No code, text or art was copied from Iron Coffins. Where the 2026.1 game already does something
Iron Coffins once added, Damage Control leaves it to the game.

---

## Requirements, install and removal

### Requirements and compatibility

| | |
|---|---|
| Game version | UBOAT **2026.1** |
| Other mods needed | None. Damage Control registers its own crew orders. |
| Custom Actions | Works alongside it. Each Damage Control order still appears once. |
| Iron Coffins | **Do not run both.** They change the same game systems and will fight each other. |
| Game permission | **Reflection**. The mod needs it to patch the game; the launcher may ask you to allow it. |

Other mods that change hatch behaviour, compartment flooding or breathable air are likely to conflict.
No such combination has been tested.

### Install (local mod)

A Steam Workshop release is planned but not available yet. For now, install the mod by hand:

1. Download this repository (for example **Code > Download ZIP** on GitHub) and unpack it.
2. Create a folder named `damage-control` in the game's `Mods` folder:
   - **Windows:**
     `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\damage-control\`
   - **macOS with CrossOver** (bottle named `Steam`):
     `~/Library/Application Support/CrossOver/Bottles/Steam/drive_c/users/crossover/AppData/LocalLow/Deep Water Studio/UBOAT/Mods/damage-control/`
3. Copy the **contents** of the repository's `mod/` folder into it. When you are done, the folder holds
   `Manifest.json`, `Source`, `Data Sheets` and `Assets` directly, not a nested `mod` folder.
4. Start the game launcher, open the **Mods** tab and enable **Damage Control**. Allow the Reflection
   permission if asked. You only need to enable it once.
5. Start the game. The first launch after an install or update takes a little longer, because the game
   compiles the mod.

To update, replace the folder's contents with the new version's `mod/` contents and launch again.

### Removing the mod

Disable it in the launcher's Mods tab, or delete the `damage-control` folder. Saves made with the mod
still load without it. Expect at most one error in the log about an unknown settings task; the campaign
plays normally after that.

Some changes the mod made stay in the save, because they are ordinary game data:

- **Rebreathers** that were worn while the mod was active carry the mod's endurance bonus as a normal
  item modifier. They will probably keep lasting longer after removal. (Untested.)
- **Equipment you dismantled** stays broken until repaired, and the scrap and parts you made stay in
  storage.
- **Hatch states** (open, closed, leaking) and compartment water and air are saved as they were. The
  base game takes over from there.
- If you save while a crewman is repairing a leaking pipe, the pipe's leak may stay reduced in that
  save. This is untested.

---

## Settings

Damage Control has three switches. You find them on the **Workshop** interaction wheel in the stern
torpedo room, listed after the workshop's own orders. They only appear while a campaign is running.

| Setting | Default | When on | When off |
|---|---|---|---|
| Hatch pressure blocking | **Off** | A closed hatch with a clearly heavier load of water and air on one side cannot be opened. | The base game's own hatch rule applies. |
| Sailors block leaks on their own | **On** (base game) | Idle sailors go and block new leaks without an order. | Sailors block a leak only when you order it. |
| Crew carry the wounded on their own | **On** (base game) | Crewmen carry injured shipmates to safety without an order. | The injured stay where they fell until you order help. Divers are still pulled back aboard. |

How the switches work:

- The wheel entry names what it **will do**. With blocking off it reads "Enable hatch pressure
  blocking"; with it on, "Disable hatch pressure blocking". The other two read "Stop sailors blocking
  leaks on their own" / "Let sailors block leaks on their own" and "Stop the crew carrying the wounded
  on their own" / "Let the crew carry the wounded to safety".
- A click takes effect at once. No crewman needs to be selected and nobody walks to the workshop.
- The settings are **saved per campaign**. A new campaign starts with the defaults above. So does an
  older save made before you installed the mod; from then on the settings are saved with it.

---

## Feature guide

### Air and pressure

**What you see**

- The **bilge panel** of every compartment ends with a line such as "Air pressure here: 1.00 atm".
  This shows even when the room is dry.
- The **buoyancy tooltip** keeps its normal text and adds a breakdown: how much water the ballast tanks
  hold in total and per tank, how much water is inside the hull, and how much is in each compartment.
  A compartment with 5 m³ or more is shown in red (2 m³ for the small galley).

**How it behaves**

- **Flooded rooms keep an air pocket.** A breached compartment never fills completely. The water squeezes
  the remaining air, and its pressure rises.
- **Bad air.** A compartment's air becomes unbreathable when any of these is true:

  | Condition | Limit |
  |---|---|
  | Boat-wide oxygen | below 5 % |
  | Air pressure | below 0.15 atm or above 8.5 atm |
  | Water above the floor | more than 1.5 m |

  Crew there start choking (the game's toxic-air alert) and need rebreathers. Every work station in
  that room then counts as under water. Choking injuries are possible but fairly rare: officers are hit
  more often than sailors.
- **Rebreathers last 2.5 times as long** as in the base game. Taking one off and putting it back on does
  not add the bonus twice.
- **Pressure damage.** Checked once per game second:
  - A light bulb under water is at risk above 4 atm, and any bulb above 16 atm. The higher the
    pressure, the more likely it fails, up to a 10 % chance per second.
  - Equipment that is under water and above its rated pressure takes damage over time and shows the
    game's pressure-damage status. The status clears once the room is dry and back at normal pressure.
  - A **closed interior hatch** is stressed by the difference between the two rooms it separates, wet or
    dry. If that difference exceeds the hatch's rating, it takes damage.
- **Work stations and water.** Some stations keep working in deeper water than in the base game:
  bulkhead hatches up to about 1.4 m of water (the conning-tower hatch partly less), the dive-plane
  station about 0.7 m, the electric motors about 0.6 m. A station near a bulkhead also counts as under water when the room next to it floods past the
  station's limit.
- **Diesels breathe from the engine room.** A running diesel takes its air from its own compartment.
  Run it submerged without a snorkel and that room's pressure falls, down to a floor of 0.35 atm.
- **Venting.** The engine room returns to about 1 atm once the boat is surfaced (deck less than 3.5 m
  under) or a working snorkel is up at snorkel depth. The conning tower returns to 1 atm quickly when
  the exterior hatch is open, and slowly once the boat is nearly surfaced.
- **The air compressor** takes the air it bottles from the room it stands in, so that room's pressure
  slowly drops while it runs. At or below 0.35 atm it stops filling the bottles.

**Tips**

- Watch the bilge pressure line in the engine room after a submerged diesel run. Open the hatch to the
  next room carefully: the air will rush across.
- A room past 1.5 m of water is unbreathable whatever its pressure. Get a rebreather on anyone who has
  to work there.

### Hatches

**A closed hatch is a seal.** Air and water pass only through open hatches, or slowly through a wrecked
one. With a hatch open, water levels move toward each other at about the base game's speed, and the
air pressures of the two rooms even out.

**The hatch panel.** Look at an interior hatch and its panel shows:

| Line | Meaning |
|---|---|
| "The pressure difference is holding this hatch shut." (red) | Only when the hatch is closed and cannot be opened. |
| "Opening side: x.xx atm" | The total push on one side: air pressure plus the weight of any water standing above the bottom of the hatchway. |
| "Closing side: x.xx atm" | The same for the other side. |
| Durability | The hatch's rated pressure, as in the base game. |
| "Wrecked hatch: ..." (red) | Instead of durability, when the hatch is destroyed. |
| "Partly sealed: ..." (green) | Someone is blocking or repairing the hatch, so it leaks much less. |

A pressure is shown in red above 2 atm and in blue below 0.8 atm. The exterior hatch keeps the base
game's panel.

#### Pressure lock (optional)

With **hatch pressure blocking** switched on, a closed interior hatch cannot be opened while its opening
side pushes at least **0.9 atm** harder than its closing side. Water counts at 0.1 atm per metre standing
above the bottom of the hatchway, the same as the game's own physics.

- Open is disabled on the wheel, a crewman walking through stops at the hatch, and no other route
  (first-person toggle, scripted opening) gets it open either.
- The lock works one way only. A higher pressure on the closing side does not lock the hatch.
- Hatches without a room on both sides keep the base game's rules: the exterior hatch, doors, the
  cargo hatch.

Because water counts at its real weight, water alone needs about 9 m of level difference to lock a
hatch. In practice, locks come from **air pressure** differences, for example a flooded room whose air
pocket has been squeezed. If you want water to lock hatches more readily, lower `Hatch Lock Threshold`
(see [Tunables](#tunables)). The default has not yet been tuned in play.

With the setting **off** (the default), the base game's own hatch rule applies unchanged.

#### Equalize Pressure

A new hatch order, **Equalize pressure**, is offered on any closed interior hatch with a room on both
sides. It is not offered on open hatches, the exterior hatch or doors.

- The crewman works the hatch and bleeds air and water through it, so the two pressures move together.
- As soon as the hatch may be opened, he opens it. The order then ends by itself a couple of seconds
  later. It never finishes without opening the hatch.
- You can give it without selecting a crewman; the game picks one.
- He can work it standing in water if he wears a rebreather.

Use it on a pressure-locked hatch before you need to pass. With pressure blocking off, it normally just
opens the hatch straight away.

#### Wrecked hatches

A hatch destroyed while closed starts **leaking**: about 2 % of the flow an open hatch would pass. Any
repair that gives it back some health stops the leak.

A wrecked, closed hatch can also be **blown open**. That happens when the air pressure on its closing
side exceeds the opening side by more than a third of the hatch's rated pressure. Once that is true it
gives way within a few seconds (about a 75 % chance per second), with the pressure-damage sound. An
intact hatch, or one under repair, never blows open.

A hatch that the game disables while closed, with the opening side still pushing harder, stays shut
and leaks instead of swinging open.

**Tips**

- Close hatches early. Once water is through, it stays on your side.
- A wrecked hatch between a flooded room and a dry one is a slow leak. Send a crewman to block it (see
  below) until it can be repaired.

### Leaks and crew

#### Two crewmen on one leak

A leaking hull hole, pipe or hatch takes **two** crewmen at once: one blocking and one repairing, or two
blocking. In the base game a second block-leak order only swapped the men. On a hull hole both stations
can be used at once.

To put a second crewman on a leak:

1. Get the first man working on it. A sailor may already be blocking it on his own; otherwise order
   **Block leak** or **Repair** on it as usual.
2. Select the second man (an officer, or select nobody and let the game send a free crewman).
3. Open the interaction wheel on the same leaking object and choose **Block leak** or **Repair**.
4. He walks to the free station and works beside the first man. The first man keeps working.

Limits:

- **Two at most.** With two men already there, **Repair** is greyed out ("used by somebody else"), and a
  new **Block leak** order takes over from one of the blockers.
- **One repairer.** A second **Repair** stays greyed out ("already being repaired"), as in the base game.
- **Each man needs his own station.** If no station is free for the new man, **Block leak** takes over
  from the man blocking there (the base game's behaviour) and **Repair** is greyed out with the
  game's reason (occupied, unreachable, or under water without a rebreather). On a hatch the free
  station can be on the far side: he needs a way round to it, and a rebreather if that side is flooded.
- Repairs that need parts still need them. With none at hand, **Repair** is greyed out.

Blocking works as in the base game for holes and pipes: while a crewman holds the leak, it runs at
about a fifth of its strength. Two blockers stack, and so do a blocker and a repairer.

#### Blocking a leak on a hatch

**Block leak** is now offered on a leaking (wrecked) hatch that is closed.

- The crewman holds the hatch, and its leak drops to a tenth. The panel shows the green "Partly sealed"
  line while he is there.
- A repairer on the same hatch seals it as well, and the two stack: a hundredth of the leak with both.
- The order is not offered on an open hatch. If the hatch is opened while he holds it, his order ends a
  couple of seconds later.

#### Automatic leak blocking switch

With **"Sailors block leaks on their own"** turned off (see [Settings](#settings)), new leaks get
nobody until you order it. Leak-blocking orders you give still work normally. Turn it back on and idle
sailors go to open leaks again.

#### Carrying the wounded

With **"Crew carry the wounded on their own"** turned off:

- Injured crewmen stay where they fell.
- The **light switch** wheel gets a new order, **Carry the wounded to their bunks**. It is greyed out
  while nobody lies injured. Use it, with or without a crewman selected, and free crewmen carry every
  injured man to a bunk. Once nobody is left on the deck plates, they stop and automatic carrying stays
  off.
- Divers returning to the boat are still pulled aboard.
- Each time you start a campaign or load a save, the wounded are carried once, as if you had given the
  order. After that the switch applies again.

With the switch on, the order is hidden: the crew already do it.

#### Trim pump

The crew normally switch the trim pump off when it runs out of water to move. While **anything aboard
is still leaking**, they leave it running. Once the last leak is stopped, the usual delay starts and
they switch it off as before.

### Ballast: Exhaust Blow

A new order at the **flooding and blowing valve station**, where you give the normal blow and flood
orders: **Exhaust blow (diesel)**. It blows the main ballast tanks with diesel exhaust instead of your
bottled compressed air.

**When it is available**

| Requirement | If not met, the wheel says |
|---|---|
| The boat is not docked | (greyed out) |
| A diesel is running (ahead or astern) | "The diesel must be running." |
| The deck is clear of the water | "The deck must be clear of the water." |
| There is water left in the main ballast | "The ballast tanks are already empty." |

The usual order checks also apply: the valve must work, nobody else may be doing the same order, and the
crewman must not be carrying something that blocks work. He can work the valve standing in water.

**What it does**

1. The crewman goes to the valve and takes it over. The valve's normal compressed-air blowing stays off
   for the whole job, so your air bottles are not touched.
2. Exhaust goes into the tanks at a rate set by the diesel's speed: the faster the diesel turns, the
   faster the tanks empty. The end tanks are blown first; the middle tanks join once both end tanks are
   nearly empty.
3. The progress bar shows how much of the ballast is empty.
4. When the tanks are empty, the order completes, the valve is left off, and the depth preset is set
   to **Surface**.

The order is cancelled if the diesel stops, the deck goes under, or the boat docks for more than about
**3 seconds** at a stretch. A wave washing briefly over the deck does not cancel it.

**Tips**

- Surface on compressed air as usual, get the deck clear, start the diesels, then use Exhaust Blow to
  finish emptying the tanks. That keeps your bottles full for the next emergency.
- More diesel speed means a faster blow.
- This is a manual order only. The mod does not run it automatically when you surface.

### Crew movement

- **Blocked hatches stop crew.** An order whose destination lies behind a hatch the mod is holding
  shut is unavailable for that crewman, even when you give it directly. Orders on his side of the
  hatch still work. An open hatch never counts as blocked.
- A crewman already walking toward a hatch when it locks **stops at the hatch** and his orders are
  cancelled. The hatch stays shut. Men evacuating a compartment and men getting off the deck when
  the boat dives are never stopped.
- Hatches the base game itself keeps closed, for example when diving, are not affected: the crew
  open them as usual. Neither are the exterior hatch and the bridge ladder.
- **Evacuation can be cancelled.** Give an evacuating crewman a new order and he follows it. The others
  keep evacuating.

A hatch can be blocked by the pressure lock (when that setting is on) or by a repair in progress (see
below; this applies whether the setting is on or off).

### Repair and parts

#### Repairs use any free station

A repair order goes to the first free station on the damaged equipment: the repair station first, then
the leak-blocking station, then the default station. A hatch is repaired from its default station when
that one is free. A repair the base game would refuse only because its repair station is taken is now
offered through a free leak-blocking station. Every other reason the game refuses a repair (missing
part, someone else already repairing, an item in the way) still applies.

#### Repair slows the leak

When a crewman repairs a pipe or hull hole that was leaking when he started, the leak shrinks as the
repair bar fills. At half-way it runs at half strength (before any blocking), and it is gone at the
end. If he stops early, the leak returns to full. Repairing something that was not leaking changes
nothing. On a hatch, the repairer instead seals it (see [Blocking a leak on a
hatch](#blocking-a-leak-on-a-hatch)).

#### A hatch under repair is held shut

When a crewman starts repairing an **open interior hatch**, he closes it first. While the hatch shows
that it is being repaired, it cannot be opened, and crew will not route through it. This applies even
with hatch pressure blocking off. When the repair is done the hatch **stays closed**; open it yourself
when you are sure the other side is safe. The exterior hatch is never closed this way.

#### Salvaging parts costs more scrap

At the workshop, **salvaging one replacement part now costs 4 scrap** instead of the base game's 2. The
order still takes about 100 seconds per part at normal speed. It needs at least 4 scrap to start and
ends when fewer than 4 are left. The scrap item's tooltip no longer states a ratio.

If a game update changes the salvage code so that the mod cannot apply its ratio safely, it leaves the
game's 2:1 in force and writes a warning to the log.

#### Dismantle for scrap

Engineers get a new order on most working equipment: **Dismantle for scrap**, listed last on the wheel.

- Offered on hatches, compressors, the deck gun and flak, the dive-plane station, engines, gyrocompass,
  listening room, periscopes, the decoy launcher, the distribution boards, radio room, targeting
  sight, torpedo tubes, trim pump, valves and ventilation.
- The engineer removes one point of health at a time, at repair speed. The Handyman perk works about a
  quarter faster.
- At zero health the equipment is **broken**, and you get **1 scrap** (**2** with the Thrifty perk). It
  goes into the main storage, or into the engineer's hands if storage is full.
- It gives no experience and does not touch leaks.
- Not offered when someone is repairing that equipment, when the same order is already running there,
  on equipment that is out of action, while the engineer carries an item that blocks work, or at a
  flooded station without a rebreather.

Bringing dismantled equipment back from zero health may cost a replacement part, as any repair from zero
does in the base game.

---

## Tunables

Some of the mod's numbers can be changed without touching its code. They live in the mod's data sheet
`Data Sheets/General.DamageControl.xlsx`, worksheet **Settings**, under the group **/Damage Control**.
Each row has a name in the first column and its value in **Value**.

| Name | Default | Meaning |
|---|---|---|
| `Hatch Lock Threshold` | `0.9` | How much harder, in atm, the opening side of a hatch must push before the pressure lock holds it shut. Must be above 0. |
| `Hatch Flow Rate` | `2` | Speed of air and water flow through hatches. 2 matches the base game's water flow through an open hatch. Wrecked-hatch leaks scale with it. Does not affect Equalize Pressure. |
| `Hatch Blow Open Chance` | `0.75` | Chance per second (0 to 1) that a wrecked hatch under enough pressure blows open. |
| `Hatch Slot Interaction Types` | `True` | Also changes which animation crew use at hatches. Set `False` if crew look wrong when working hatches; the water limits still apply. |
| `Salvage Scrap Per Part` | `4` | Scrap per replacement part at the workshop. The base game uses 2. At least 1. |
| `Exhaust Blow Flow Per Revolution` | `0.1933768` | Exhaust led into the ballast per diesel revolution. Higher blows faster. |
| `Exhaust Blow Stage 2 Water Threshold` | `5` | The middle tanks join once both end tanks hold less water than this. |
| `Exhaust Blow Completion Water` | `0.05` | The blow counts as finished when this little water is left in the ballast in total. |
| `Exhaust Blow Minimum Diesel RPM` | `1` | Below this speed the diesel counts as stopped. |
| `Exhaust Blow Deck Clearance` | `0` | Deck depth in metres below which the deck counts as clear of the water. 0 is the waterline. |
| `Exhaust Blow Cancel Grace Seconds` | `3` | How long (game seconds) the requirements may fail before a running blow is cancelled. |
| `Debug` | `False` | `True` writes extra lines to the log and adds `[DC]` diagnostic lines to the hatch panel. For testing and bug reports. |

A missing or unreadable value falls back to the default and writes one warning to the log.

**When changes apply.** The hatch, salvage and Exhaust Blow values are read when a campaign starts or a
save loads. `Debug` is read at game start and on each scene load. `Hatch Slot Interaction Types` is read
as the boat is built. The safest approach is to quit the game, make the change, and start it again.

### Changing a tunable

**Editing the installed sheet** (simplest for a local install):

1. Quit the game.
2. Open `Mods/damage-control/Data Sheets/General.DamageControl.xlsx` in a spreadsheet program, go to
   the **Settings** worksheet, change the **Value** cell of the row you want, and save it as `.xlsx`.
3. Delete the game's cached copies of the mod's sheets (see [Changes do not show
   up](#changes-do-not-show-up)), then start the game.

Updating the mod overwrites this file, so note your changes. The game's sheet reader is picky about some
spreadsheet files; if the mod's rows stop working after you save, restore the original file. Editing the
sheet in a spreadsheet program has not been tested.

**Using your own override mod.** The game merges data sheets from all mods. A row with the same path
from a mod that loads later replaces the earlier one. To override a Damage Control tunable, a mod of your
own can ship a `General.<YourName>.xlsx` with a **Settings** worksheet, a **/Damage Control** group and
a row with the same name and a new Value. Local mods always load before Workshop mods, so a Workshop
mod's rows win over the local Damage Control. Load order between two *local* mods has not been checked,
so this route is untested for local overrides.

**Building from source.** If you build the mod yourself, change the value in `data/<area>/*.json` and
rebuild the sheets; see [`DEVELOPMENT.md`](DEVELOPMENT.md).

---

## Known issues and limitations

- **Untested in play.** Beyond a load test, no feature has been confirmed in game yet. Numbers such as
  the hatch lock threshold have not been tuned against real play.
- **Swapped Repair labels.** In 2026.1 the base game's "Repair" and "Repair (requires parts)" wheel
  labels are swapped in English and most other languages. This is a base-game issue. The mod leaves it
  alone, because fixing it in English only would replace all the other translations with English.
- **English only.** All of the mod's own text is English, in every game language. That includes the
  scrap tooltip, which the mod rewrites because the original text states the 2:1 ratio.
- **Placeholder icons.** The icons for the mod's orders are simple placeholders.
- **Water rarely locks hatches on its own** at the default threshold (about 9 m of level difference is
  needed); air pressure differences are what usually trigger the lock.
- **Exhaust Blow is manual only.** Nothing queues it automatically on surfacing.
- **Exhaust Blow when docked** is greyed out without an explanation.
- **Wounded carried on load.** With automatic carrying off, the crew still carry the wounded once each
  time you start a campaign or load a save.
- **Other mods.** The mod replaces how hatches move air and water, the once-per-second compartment
  damage check, and the breathable-air rule. Another mod changing the same things will conflict.
  Data-sheet rows from Workshop mods override this mod's rows.
- **Save compatibility while in progress.** Saving during an Exhaust Blow, a repair or a dismantle is
  expected to resume or drop the order cleanly, but this is untested.
- **Removing the mod** leaves some changes in the save; see [Removing the mod](#removing-the-mod).

---

## Troubleshooting

### The log file

The game writes its log to `output_log.txt`:

- **Windows:** `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\output_log.txt`
- **macOS with CrossOver:**
  `~/Library/Application Support/CrossOver/Bottles/Steam/drive_c/users/crossover/AppData/LocalLow/Deep Water Studio/UBOAT/output_log.txt`

Every line from the mod starts with `[Damage Control]`. Search the file for `Damage Control` and for
`Exception`.

### What the log lines mean

| Line | Meaning |
|---|---|
| `Damage Control 0.1.0 loaded; applying Harmony patches.` | The mod was compiled and started. |
| `Harmony patches applied.` | Everything hooked into the game. Good. |
| `Harmony patches applied; N patch class(es) could not be applied ...` | Some features could not hook in and run as the base game instead. Look for `(not applied)` lines. |
| `patch <Name> (not applied) threw and falls back to vanilla behaviour` | That feature could not be installed, usually after a game update. The rest of the mod still works. Please report it. |
| `patch <Name> threw and falls back to vanilla behaviour` | That feature hit an error during play and switched itself off for the session. Please report it. |
| `bootstrap step "..." failed` | Part of the mod's start-up failed. Please report it. |
| `icons ready: N served` | The mod's icons loaded. |
| `campaign settings created: ...` / `campaign settings restored: ...` | Your three settings for this campaign, new or loaded from the save. |
| `this save has no Damage Control settings yet; creating them with defaults` | An older save made without the mod. Normal. |
| `setting <Name> is now on/off` | You flipped a switch. |
| `carry wounded: order given; ...` | You used "Carry the wounded to their bunks". |
| `boat layout unknown: ...` / `... disabled: boat layout unknown` | The mod did not recognise the boat's layout, so the named feature is off. Please report it with the boat type. |
| `conning tower compartment not found ...` / `diesel compartment not found ...` | Venting or diesel air draw is off on this boat. Please report it. |
| `salvage ratio: the game's salvage code did not match; keeping the vanilla 2 scrap per part` | The 4:1 salvage change could not be applied; the game's 2:1 applies. |
| `data row ... has "...", expected ...; using ...` | A tunable you edited could not be read; its default is used. |

With `Debug` set to `True` you get many more lines, mostly describing individual events (a hatch kept
shut, an Exhaust Blow finished, and so on).

### The mod does not load

- Check that the folder is `Mods/damage-control/` and holds `Manifest.json` directly.
- Check that Damage Control is enabled in the launcher's Mods tab and that you allowed Reflection.
- Look in the log for `error CS` lines (the mod failed to compile) and for the `loaded` line above.
- If the mod works on the first launch after an install and then stops loading on the next, the game's
  security check rejected it. Please report this with your log.

### Changes do not show up

The game keeps cached copies of compiled mods and data sheets. Normally it notices updates by itself.
If an update or an edited tunable does not take effect, quit the game and delete:

- the parsed data-sheet cache: the files named `Damage Control - *.dat` in
  `.../LocalLow/Deep Water Studio/UBOAT/Data Sheets/`;
- the compiled mod: `.../LocalLow/Deep Water Studio/UBOAT/Temp/damage-control.dll`.

The game rebuilds both on the next launch. `...` is the same `LocalLow` folder as in the install paths
above.

### Reporting a bug

Open an issue at <https://github.com/awkay/damage-control-mod/issues>. Please include:

- what you did, what you expected, and what happened;
- the boat type, and whether it happened at normal speed or with time compression;
- the `[Damage Control]` lines and any exceptions from `output_log.txt`. With `Debug` set to `True`
  the report is much more useful;
- other mods you have enabled;
- if you can, a save from just before the problem.

---

Damage Control is released under the MIT license. See [`LICENSE`](../LICENSE).
