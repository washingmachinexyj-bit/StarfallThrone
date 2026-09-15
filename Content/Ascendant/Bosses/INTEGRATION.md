# Ascendant combat integration

Only `Content/Ascendant/Bosses/**` and `Localization/AscendantCombat/**` are owned by this combat implementation. No Divine file or main mod entry point is changed.

## Parent contracts used

- `AscendantCatalog.Root`, `Colors`, `Names`, `Defense`, `Life(int,int)`, `NPCType(int)`.
- `AscendantWorld.Open(int)`, `Prerequisites(int)`, `RecordVictory(NPC,int,bool[],bool[])`.
- `AscendantLoot.Configure(NPCLoot,int)`.
- Existing `AscendantChallengePlayer` handles all-hurt/death no-hit tracking and client->server hurt notifications; combat also marks direct body/projectile hits.

## Content / assets

- `AscendantBossNPC` abstract partial, `Index`; `[AutoloadBossHead] AscendantBoss0/1/2`.
- `AscendantBossBar`, `AscendantHazard`, `AscendantNode` are independently registered.
- Required only: `Root + Boss0/1/2` (single frame; native size) and `Root + Boss0/1/2_Head_Boss`.
- Hazards/nodes draw with existing MagicPixel/CombatDrawing. No additional textures are required.
- CN/EN NPC/projectile/bestiary/mechanic captions are under `Localization/AscendantCombat`.

## Validation entry points

- `AscendantCombatValidation.Data()` checks registration, all nine HP/contact budgets, defense, boss heads/bars, 2/4/6 hostile projectile conversion.
- `AscendantCombatValidation.Run()` requires `Main.gameMenu` and `-starfall-art-smoke`; saves/restores global actor arrays, prerequisite flags, mode and rendering state.
- Runtime: 3 phases x 3 difficulties x 3 bosses, attack cycling, warning non-collision, bounded helpers, packet roundtrips, late-join no-hit exclusion, day/night continuity, phase/death/ownership cleanup, node charging/cancellation, lane/beam/ring native collision, distinct left/right chosen attacks, finite once-only finales, native expired-window loot cancellation, missing prerequisites and server authority.
- Run emits **three** `ASCENDANT_BOSS_AI_PASS:` lines, plus `ASCENDANT_COMBAT_RENDER_PASS:` and `ASCENDANT_COMBAT_RUNTIME_PASS:`.
- Native hook capture: `outputs/ascendant-combat-runtime.png` relative to the existing isolated smoke SavePath convention.

## Combat review notes

- Every emitted hazard has a >=60-tick local preview; trace replay has 72 ticks after 180 ticks of recording.
- Clients do not create hazards/nodes, resolve choices, apply node progress, change encounter stage or record victory.
- All helpers validate NPC slot + random encounter serial. End-window/prerequisite checks exist at AI, CheckDead and PreKill/OnKill.
- Finales never set `dontTakeDamage`, intercept a legitimate death, or alter tiles. Dawn/dusk is not a running-fight condition.
- The first two fights use locked floor anchors, ordinary-jump-height hazards and a walking-speed lane. Physical balance still needs human playtesting.
- Parent owns compilation, smoke execution and installation. This combat task does not build or install.
