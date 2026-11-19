# Clean-room process

This mod is an independent reimplementation of the *ideas* in salamander's
"Iron Coffins" UBOAT mod (Workshop 2421790822), which carries no license.
Ideas, game mechanics and behavior are not copyrightable; the original's code,
comments, identifiers and text are. We keep the two apart:

## Roles

- **Spec writers (dirty room)** read the original mod and write behavioral
  specifications in `docs/spec/`. Specs describe *what* the mod does in terms of
  game concepts, tunable numbers, player-visible effects and acceptance criteria.
  Specs never contain the original's source code, pseudo-code transliterations,
  private identifiers, comments, or player-facing strings.
- **Implementers (clean room)** build from `docs/spec/` and the *game's own*
  assemblies only. They must never open the original mod's files
  (`.../workshop/content/494840/2421790822/`).

## Allowed in specs

- Game (Deep Water Studio) type/method names the original hooked, as integration
  points — these are facts about the game's API.
- Numeric constants and formulas, stated as behavior ("pressure rises 1 bar per
  10 m depth"), since functional parameters are needed to reproduce mechanics.
- Descriptions of UI elements and what they do. Implementers write all new text.

## Not allowed

- Copy/paste or line-by-line paraphrase of the original source.
- The original's wording for locale strings, descriptions, or icons/art.
