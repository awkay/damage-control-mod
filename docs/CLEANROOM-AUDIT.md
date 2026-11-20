# Clean-room audit

Audited revision: `2422ddb` (HEAD, "Merge WS5 repair and parts"), plus every blob reachable from any ref
(`git rev-list --all`). Date: 2026-11-19. Auditor: independent; no files edited other than this report.

**Verdict: clean enough to release under MIT once 3 small text/identifier fixes are made (F1, F3, F4).
No original source code, comments or log text was copied.** All 26 icons come out of our own generator.
What remains is short labels, one identifier and one functional data list. Each is cheap to change.

## 1. Method

Sources compared:

| Side | What |
|---|---|
| Original A | Iron Coffins (Workshop 2421790822): `Source/**/*.cs` (16 files), `Data Sheets/Locales.xlsx`, `Data Sheets/Crew.xlsx` |
| Original B | Custom Actions (Workshop 2374336654): `Source/**/*.cs` (6 files), `Data Sheets/Crew.xlsx` |
| New | Every git-tracked file: `mod/Source/**`, `tests/**`, `tools/**` (89 + test/tool `.cs` files, about 8.4 kLOC), `data/**/*.json`, `mod/Data Sheets/*.xlsx`, `mod/Assets/Icons/**`, `docs/**` |
| Vanilla reference | UBOAT 2026.1 `com.uboat.game.dll`, `com.uboat.modapi.dll` and `Assembly-CSharp.dll`, decompiled with ilspycmd (2,658 files). Used only to decide whether a shared fragment is the game's own idiom. |

Mechanical checks. The scripts and outputs are in the session scratchpad and are not committed.

1. **Token-run matching.** A C# tokenizer strips comments and `using` directives. Every new `.cs` file was
   matched against every original `.cs` file in two modes:
   - *raw*: exact tokens, runs of 12 or more tokens;
   - *normalized*: identifiers become `I`, literals become `S`/`N`, keywords and punctuation are kept.
     This catches renamed-variable transliteration. Runs of 20, 30 and 40 or more tokens were tested.

   Each hit was then checked against the decompiled vanilla game. A hit that also occurs in vanilla
   counts as the game's API idiom.
2. **Identifier overlap.** Identifiers that appear in both original and new code, minus every name in
   the game assemblies (metadata strings, `get_`/`set_` stripped, Harmony `___field` injection names
   resolved). The leftovers are candidate mod-private identifiers. The 180 private identifiers of six or
   more characters were also grepped across all new files, `docs/**` included.
3. **Literals and comments.** Every original string literal of four or more characters was searched
   verbatim in the new repo. Original comments were checked for word 5-gram overlap with new lines.
4. **Player-facing text.** All strings in the original `.xlsx` files (shared strings and inline) and the
   original's hard-coded English UI literals were compared with all new JSON string values and new
   `.xlsx` strings. The comparison covered exact matches plus a difflib fuzzy ratio of 0.6 or more,
   followed by manual review. Every historical `.xlsx` blob and every historical `"English"` JSON line
   was rechecked.
5. **Art.** `tools/placeholder-icons/make_icons.py` was run for every glyph × badge × colour
   (81 PNGs). Each tracked PNG, and every PNG blob in git history, was matched to that output by SHA-1.
6. **Specs.** All of `docs/spec/*.md` was read or grepped for code blocks, original identifiers,
   original comments and original UI strings. The most code-adjacent sections were compared line by
   line with the original methods.

## 2. Results summary

| Check | Result |
|---|---|
| Longest raw shared token run | 35 tokens (a 4-line loop), present in vanilla `RepairAction.GetState`. Benign. |
| Normalized runs of 30+ tokens | 8. All are constructor, GetState or Harmony-signature boilerplate that vanilla also has. Benign. |
| Normalized runs of 20+ tokens not in vanilla | ~30. All are `using`/namespace headers, `[HarmonyPatch]` attributes, Harmony parameter lists or two-line game-API call sequences. Benign. |
| Mod-private identifiers shared | 4 class names: F1 and F2 |
| Original comments or log messages in new code | none |
| Original string literals in new code | only game keys: vanilla locale keys, game sound, tag, perk and slot names. Benign. |
| Player-facing text | 1 close paraphrase (F3), 1 near-identical short label (F4), 3 coincidental short labels (F7) |
| Data | 1 verbatim functional list (F5) |
| Icons | 26/26 tracked PNGs and 21/21 historical PNG blobs are byte-identical to generator output. None are copied. |
| Data-sheet workbooks | produced by `tools/SheetBuilder` (different part layout, no `sharedStrings`, different `styles.xml`). No original workbook was reused as a template. |
| Specs | no source code, no private identifiers, no copied comments. Formula-level algorithm descriptions (F6) and a few original labels quoted as names (F4, F7). |

Non-benign findings (copied or close paraphrase): **5** (F1, F3, F4, F5, F6). All are low risk.
Coincidental: 2 (F2, F7).

## 3. Findings

Severity scale: **copied** / **close paraphrase** / **benign-shared-API** / **coincidental**.

### F1. Class name `EqualizePressureAction` is the original's own identifier. Severity: copied (identifier), low.

- New: `mod/Source/Game/Hatches/EqualizePressureAction.cs:23`. Also used as a type string at
  `data/hatches/hatches.json:10` and `tests/DamageControl.Tests/HatchTextTests.cs:36`.
- Original: `2421790822/Source/Actions/EqualizePressureAction.cs:19`, and type
  `UBOAT.Mods.IronCoffins.EqualizePressureAction` in the original `Crew.xlsx`.
- The game has no such type. This is the only mod-private class name the two mods share.
  The method bodies differ: the new `UpdateAction` delegates to the `HatchExchange` kernel, and
  `GetState` follows vanilla `HatchOpenCloseAction`/`OpenCloseFoldingDoorAction`, which use the same
  flag set.
- **Recommendation:** rename it, for example to `HatchEqualizeAction`, and update the Crew row `Type`
  and the test. The name is descriptive, so the legal risk is minimal, but renaming removes the only
  shared invented identifier.

### F2. Harmony patch class names match the original's nested classes. Severity: coincidental.

- New: `mod/Source/Game/Hatches/HatchCanBeOpenedPatch.cs:22`, `HatchDoUpdatePatch.cs:32`,
  `HatchOnDisablePatch.cs:19`.
- Original: `2421790822/Source/HatchesPatches.cs:44`, `:137`, `:106`.
- Both follow the usual `<GameType><Member>Patch` convention for Harmony targets (`Hatch.CanBeOpened`,
  `Hatch.DoUpdate`, `Hatch.OnDisable`). The bodies are unrelated: in the normalized token check, no run
  of 20 or more tokens outside the method signature.
- **Recommendation:** none required. A rename is optional hygiene.

### F3. Damaged-hatch warning is a close paraphrase of the original message. Severity: close paraphrase, low.

- New: `data/hatches/hatches.json:18`, "This hatch is wrecked and leaking. Its seal will hardly hold
  more than a few atmospheres."
- Original: `2421790822/Source/HatchesPatches.cs:313`, a hard-coded message that the hatch is severely
  damaged, will leak, and will "most likely not hold more than few atmospheres" of pressure difference.
- Path: spec `docs/spec/02-hatches-leaks-crew.md:149-150` restates the message closely ("badly damaged,
  will leak, and its locks probably won't hold more than a few atmospheres"). The implementer then
  reworded the spec text. The sentence structure (damaged → leaks → won't hold more than a few
  atmospheres) survives the reword.
- The companion line `hatches.json:19` ("Partly sealed: the leak is much smaller.") versus the original
  `HatchesPatches.cs:315` shares the idea only. That is acceptable.
- **Recommendation:** rewrite the string from scratch in a different form, for example "Hatch destroyed:
  water is getting past it, and it cannot take much pressure." In spec 02 §2.4 item 4, change the text to
  meaning only, for example "warn that the hatch is destroyed and leaks and that its pressure rating is
  gone."

### F4. Exhaust-blow label is near-identical to the original label. Severity: close paraphrase, low.

- New: `data/ballast/ballast.json:16`, "Blow ballast with diesel exhaust".
- Original: `2421790822/Source/Actions/BlowBallastUsingDieselExhaustAction.cs:42`, "Blow ballast using
  diesel exhaust". One word differs (similarity 0.89).
- Path: the spec carries the original label almost verbatim in `docs/spec/03-ballast-depth-navigation.md:66`
  and `:408`, and in `docs/spec/04-repair-parts-bootstrap-data.md:457`.
- A six-word functional name is weakly protectable at most. It is the only label where the original's
  wording survives nearly intact.
- **Recommendation:** pick a distinct label, for example "Exhaust Blow: purge ballast with diesel
  exhaust" or "Empty ballast with engine exhaust". Change the spec's descriptive phrase to *Exhaust Blow*.

### F5. Dismantle activator-tag list is verbatim, including its order. Severity: copied (functional data), low.

- New: `data/repair/repair.json:7`, also present in the generated `mod/Data Sheets/Crew.DamageControl.xlsx`.
- Original: `2421790822/Data Sheets/Crew.xlsx`, `Crew/Actions` row `DismantleForPartsAction`, column
  Activator Tags.
- Spec: `docs/spec/04-repair-parts-bootstrap-data.md:240-246` lists the same 18 tags as "game facts".
- Each tag is a game key ("Ventillation" is the game's own spelling, found in `resources.assets`). The
  set is a gameplay parameter: which devices can be dismantled. The selection and ordering are still the
  original author's choice; vanilla has no such list.
- **Recommendation:** acceptable as a functional parameter under CLEANROOM.md. To remove the verbatim
  fingerprint at no cost, reorder the tags (alphabetically or by boat section) and make sure the set is
  what we actually want.

### F6. Specs describe some algorithms at formula and branch level. Severity: close paraphrase (functional), low.

- `docs/spec/02-hatches-leaks-crew.md:85-131` (§2.3 b-g, hatch per-frame exchange) mirrors the branch
  order and expressions of `2421790822/Source/HatchesPatches.cs:137-260` (`HatchDoUpdatePatch.Postfix`).
  For example, the air step `min(max(0, A_L − 0.2), d × min(10, (1.85 − h_L) × 10) × s)` is the
  original expression in math notation.
- Similar density appears in `docs/spec/01-pressure-air-buoyancy.md:177-285` (§2.9-2.11, venting,
  compressor and equalize; compare `CompartmentPressurePatches.cs:415-480` and
  `Actions/EqualizePressureAction.cs:75-110`) and in `docs/spec/03-ballast-depth-navigation.md:66-112`
  (§A; compare `Actions/BlowBallastUsingDieselExhaustAction.cs:99-130`).
- Not present: C# code, original variable names (`flowTimeScale`, `interactionStarted`,
  `totalDismantleProgress` and the rest), and original comments. Each spec lists open questions and
  bugs and gives architect overrides, which is the behavioral-spec form CLEANROOM.md asks for.
- CLEANROOM.md allows "numeric constants and formulas, stated as behavior". Formulas are functional and
  not protectable expression. The code built from these specs is structurally independent: for example,
  `HatchExchange`/`HatchRuntime` and `ExhaustBlow`/`ExhaustBlowAction` are factored differently, and
  the normalized token check found no shared run of 20 or more tokens outside API boilerplate.
- **Recommendation:** acceptable to publish. For extra margin, recast the §2.3 f/g air and water steps
  as piecewise definitions (flow = f(Δp, clearance) with a cap and a 0.2 reserve) instead of
  if/else-if sequences in source order.

### F7. Short action labels that share wording. Severity: coincidental.

| New | Original | Note |
|---|---|---|
| `data/hatches/hatches.json:13` "Equalize pressure" | `Actions/EqualizePressureAction.cs:34` "Equalize pressure between compartments" | the mechanic's name |
| `data/leaks/leaks.json:8` "Carry the wounded to their bunks" | `Actions/TriggerAutoCarryWoundedToSafetyAction.cs:35` "Carry wounded to beds" | different wording |
| `data/repair/repair.json:15` "Dismantle for scrap" | `Actions/DismantleForPartsAction.cs:44` "Dismantle for parts" | different object |
| `data/common/foundation.json:16` "Enable hatch pressure blocking" | `Settings/HatchPressureBlockingToggle.cs:48` "Enable hatch blocking by pressure differences between compartments" | name of the setting |

- The spec also uses some original labels as working names: `04-repair-parts-bootstrap-data.md:28`,
  `:235` and `:430-458` ("Dismantle for parts", "carry wounded to beds", "block leak by regular
  sailor", "adjust ballast for deck awash", "pump water to control room").
- **Recommendation:** none required for the shipped text. Optionally replace those working names in
  spec 04 with our own (*Dismantle*, *Carry Wounded Now* and so on).

### Benign shared-API matches (no action)

The table lists the strongest mechanical hits. Every hit in the token-run output falls into one of
these classes.

| New | Original | Why benign |
|---|---|---|
| `Game/Repair/RepairStatePatch.cs:56-59`, `Game/Repair/DismantleAction.cs:72-75` (reverse loop over `activator.Users` testing `Action is RepairAction`) | `Actions/RepairAction.cs:135-138` | The original copied vanilla `RepairAction.GetState`, and vanilla has the same loop and the local name `users`. |
| `Game/Registration/ActionRegistrationPatch.cs:28-153` | Custom Actions `CustomActionsPatches.cs:48-121` | Both reproduce vanilla `CharacterActionsDatabase.LoadActions` (column indices 0/1/2/3/5/6/7, context flag mapping, tag-index dictionaries). The new code differs in structure: a postfix that skips registered names and looks only in its own assembly. |
| `Game/Hatches/EqualizePressureAction.cs:45-61`, `Game/Ballast/ExhaustBlowAction.cs:74-94` (`CheckActivatorUseConditions` → `<= Disabled` → `CheckInteractableUseConditions(..., "Default"/"Use", ... AllowUnderWater...)`) | `Actions/EqualizePressureAction.cs:42-58`, `Actions/BlowBallastUsingDieselExhaustAction.cs:72-80` | The standard vanilla `GetState` template (`HatchOpenCloseAction`, `OpenCloseFoldingDoorAction`, `RepairAction`). |
| `(GameDataRow)` and `(source, activator)` constructors, `OnPreStart`, `OnDeviceUsed() {}`, `base.UpdateAction()` tails | toggles and actions in both originals | required by the `CharacterAction`/`DeviceUseAction` contract |
| `ExhaustBlowAction.cs:209-214`, `:234-238` (valve `Mode = ManualBlowing` / `OptimalBlowing` + `SetDisableFlag(TurnedOff, true)`) | `BlowBallastUsingDieselExhaustAction.cs:106-111` | A minimal two-call game-API sequence that spec 03 §A dictates as behavior. The surrounding method is independently structured (gate watch, tunables, `ExhaustBlow.Distribute`). |
| `[HarmonyPatch(typeof(X), "M")]` attributes and `__instance`/`___field` parameter lists (for example `CompartmentTickPatch.cs:33-36`, `SlotCrowdingPatch.cs:20`, `CompressorDrawPatch.cs:26-29`, `TraversePatches.cs:42-56`) | various | Harmony target and injection names are fixed by the game. |
| `HatchDoUpdatePatch.cs:149` `GetClip("Pressure Damage", …)` | `HatchesPatches.cs:170` | game sound asset name |
| `RepairText.cs:35-42` "Repair (Requires Parts)", "Actions/RepairAction/Already Being Repaired", "…/Replacement Parts Info" | `Actions/RepairAction.cs:108,140,159` | vanilla locale keys (in decompiled vanilla `RepairAction`) |
| `Dismantle.cs:43,49` "Thrifty", "Handyman"; "Salvager", "Workshop", "Valves", "Light Switch", "Hatch" | various | game perk and activator tags (vanilla `CharacterClasses.xlsx`, `General.xlsx`, `Crew.xlsx`) |
| `Crew/Actions` column headers, `Normal, Queue, NoCharacter`, locale language headers | original `Crew.xlsx`, `Locales.xlsx` | the game's data-sheet schema |
| `docs/GAME-API-NOTES.md`, `docs/spec/*` game member names (`Tank.SimulateBreach`, `ShipControlGUINew.OnDepthToggle`, scene names such as `Stern Vertical Steer (L)`) | n/a | game API facts, allowed by CLEANROOM.md |

## 4. Art

- `mod/Assets/Icons/DamageControl/**`: 26 PNGs, all 128×128 RGBA. Every file is byte-identical
  (SHA-1) to an output of `tools/placeholder-icons/make_icons.py`, a stdlib-only geometric rasterizer
  with no embedded bitmap data.
- All 21 PNG blobs in git history match generator output as well.
- No other images, Unity bundles or binaries from either original are tracked or were ever committed.
  `build/out/*.dll` is ignored and untracked.

## 5. Process notes

- Git history contains no original files: no `IronCoffins.cs`, no `Locales.xlsx` or `Crew.xlsx` from
  the Workshop folders, no bundle and no `Preview.png`.
- `tools/placeholder-icons/__pycache__/` is untracked. Add it to `.gitignore` before release.

## 6. Overall verdict

**PASS, with minor fixes recommended before open-sourcing.** The new mod is an independent
implementation. There is no copied or transliterated source code, no copied comments, no copied log or
diagnostic text, and no copied art. The structural overlap that exists is the game's own API idiom,
confirmed against decompiled vanilla.

Five low-risk non-benign items remain:

| ID | Severity | Fix effort |
|---|---|---|
| F1 | copied identifier | rename the class (3 files) |
| F3 | close paraphrase of a UI warning | reword 1 string and 1 spec line |
| F4 | near-identical action label | reword 1 string and 3 spec phrases |
| F5 | verbatim functional tag list | optional reorder |
| F6 | formula-level spec prose | optional recast; allowed by CLEANROOM.md |

Doing F1, F3 and F4 removes every trace of the original's wording and invented naming from shipped
files. F5 and F6 are defensible as functional parameters.
