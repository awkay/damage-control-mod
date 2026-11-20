# Damage Control

A mod for **UBOAT 2026.1** that reworks flooding, air pressure and damage control aboard your boat. A
breach becomes a fight for each compartment: hatches seal against the water, pressure builds behind
them, and the crew have to work leaks, hatches and ballast by hand.

> Status: 0.1.0, work in progress. It builds and passes the offline checks. In-game testing follows the
> staged plan in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md#in-game-test-plan).

Players: install, settings and every feature are explained in the [player manual](docs/MANUAL.md).

## What it does

**Compartments, air and pressure**
- A flooded compartment keeps a pocket of compressed air instead of filling completely. Each
  compartment's air pressure is shown in its bilge panel.
- Deep water and high pressure make the air unbreathable, so crew choke without rebreathers.
  Rebreathers last about two and a half times longer.
- Pressure damages things. Light bulbs and pressure-sensitive equipment under water fail over time,
  and a hatch strains when the pressure on its two sides differs.
- The diesels take their air from the engine room. Run them submerged without a snorkel and the room's
  pressure falls. The conning tower and the engine room vent back to normal once they are open to the
  air again.
- The air compressor takes the air it stores from its own compartment.
- The buoyancy tooltip lists the water in every ballast tank and every compartment.

**Hatches**
- A closed hatch is a real seal. Water and air move only through open hatches, or slowly through a
  wrecked one.
- Optional pressure blocking (off by default): a hatch with a clearly heavier load of water and air
  on one side cannot be opened. A crewman can equalize the pressure through it first.
- A wrecked hatch leaks, and enough pressure behind it can blow it open.
- The hatch panel shows the pressure on both sides.

**Leaks, repair and crew**
- A crewman can block a leak while another repairs it. A repair slows the leak as it progresses.
- A hatch under repair is closed and kept shut until the repair is done.
- Repairs can use any free station on the damaged equipment.
- Salvaging spare parts at the workshop costs more scrap (4 per part by default).
- Engineers can dismantle working equipment for scrap.
- Switches to stop sailors blocking leaks on their own and to stop the crew carrying the wounded. While
  the automatic carrying is off, a one-off "carry the wounded now" order is available on the light
  switch.
- The trim pump stays on while anything aboard still leaks.

**Ballast and movement**
- A new valve order blows the ballast tanks with diesel exhaust while surfaced, which saves
  compressed air.
- Crew will not path through a hatch that cannot be opened, and stop at it if it locks in front of them.
- An evacuation order can be cancelled for one crewman.

## Settings

Three per-campaign toggles sit on the **Workshop** interaction wheel (stern torpedo room). They are
saved with the campaign and take effect at once.

| Toggle | Default |
|---|---|
| Hatch pressure blocking | Off |
| Sailors block leaks on their own | On (vanilla behaviour) |
| Crew carry the wounded on their own | On (vanilla behaviour) |

Advanced numbers live in the mod's data rows under `General/Settings/Damage Control`, in
`data/<area>/*.json`. Examples are `Hatch Lock Threshold` (0.9 atm), `Hatch Flow Rate` (2),
`Hatch Blow Open Chance` (0.75 per second), `Salvage Scrap Per Part` (4), the `Exhaust Blow ...` rows,
and `Debug` (False). Debug adds extra log lines and `[DC]` diagnostics on the hatch panel.

## Relationship to Iron Coffins

Damage Control is an independent, clean-room reimplementation of the **ideas** in salamander's
*Iron Coffins* mod for UBOAT. The credit for those ideas belongs to salamander. **No code, text or art
was copied from Iron Coffins.** Behaviour was written up as specifications, and the mod was built from
those specifications and the game's own API. The process is described in
[`docs/CLEANROOM.md`](docs/CLEANROOM.md). Damage Control targets the 2026.1 game and leaves alone
features the game now provides itself. The full scope and its decisions are in
[`docs/spec/00-overview.md`](docs/spec/00-overview.md).

## Requirements

- UBOAT **2026.1**.
- No other mods are needed. Damage Control registers its own crew orders.
- Compatible with **Custom Actions**: each toggle still appears once.
- The mod asks for the game's **Reflection** permission, which it needs for its Harmony patches.

## Install

- **Steam Workshop:** coming later.
- **Local install** (macOS with CrossOver, which is how this repo is set up): run `./deploy`. It runs
  `./verify`, then copies `mod/` into the bottle's
  `.../LocalLow/Deep Water Studio/UBOAT/Mods/damage-control/`. Enable "Damage Control" once in the
  launcher's Mods tab. On Windows, copy the contents of `mod/` to
  `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\damage-control\` instead.

Removing the mod: a save made with it still loads without it. Expect at most one error about the
mod's unknown settings task.

## Building and verifying

The game compiles the C# in `mod/Source` itself when it loads the mod, so there is nothing to ship
except the `mod/` folder. `./verify` checks offline everything that would stop the mod loading. It:

- builds the data sheets from `data/`;
- compiles the code with the game's own compiler version and settings (C# 7.0);
- runs the game's security check;
- checks every Harmony patch against the game assemblies;
- runs the unit tests of the pure logic in `mod/Source/Core`.

You need the .NET SDK and a UBOAT install. The details are in
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md); the list of patches is in
[`docs/PATCHES.md`](docs/PATCHES.md).

```
./verify           # build sheets, compile, verify, unit tests
./deploy           # verify, then copy mod/ into the game's Mods folder
```

## Known issues

- **Repair labels.** In 2026.1 the game's "Repair" and "Repair (requires parts)" labels are swapped
  in English and most other languages. The mod does not override them, because an English-only
  override would replace all 13 translations with English.
- **Scrap tooltip.** The mod rewrites the scrap item's tooltip, because the game's text states the
  vanilla 2:1 ratio. Only English text is supplied, so other languages show the English sentence.
- Behaviour is not yet fully verified in game. See the staged test plan.

## License

MIT. See [`LICENSE`](LICENSE).
