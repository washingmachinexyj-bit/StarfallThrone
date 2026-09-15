# Ecology combat integration

Owned namespaces: `StarfallThrone.Content.Ecology.Combat`; only this directory and `Localization/EcologyCombat` were edited by the combat worker.

## Main entry points

- Dispatch packet **51** to `EcologySummonBase.ReceivePacket(BinaryReader reader, int sender)`. Payload after the byte-51 header is exactly one biome byte. Server validates range, active/alive player, held summon, current vanilla unlock, 600 matching tiles, ecology anchor, night when required, and no other active boss. Summons are reusable.
- `EcologyCombatValidation.Data()` after content/recipes are loaded. It verifies all 10 bosses, 40 mobs/banners, item registration, recipes, localized bestiary, bars/heads and difficulty budgets.
- `EcologyCombatValidation.Run()` only in the isolated `-starfall-art-smoke` menu fixture. It runs 4 moves × 2 phases × 3 difficulties per boss, 40 mob AIs, warning/native collision, ExtraAI roundtrips, client authority, breakable root walls, serial cleanup and actual draw hooks. It restores actor arrays and global game/draw state. It deliberately constructs post-arena-initialization boss fixtures; main's world tests own physical region/anchor eligibility.
- `EcologyChecklist : ModSystem` autoloads and registers ten optional permanent entries when Boss Checklist is installed. No main call needed.
- Boss loot calls root namespace `EcologyLoot.Configure(NPCLoot,int)`. Only `OnKill` invokes `EcologyWorld.Defeat(int)`; cancellation never awards progression or loot.

## Registered content

- `EcologyBoss0..9`, `EcologySummon0..9`, `EcologyMob0..39`.
- `EcologyPart` is a hidden-bestiary, lootless, bounded battle prop, not an extra boss or miniboss.
- `EcologyHazard` is a bounded owned projectile with line/pool/ring/shard/wave/bubble/orbit/pillar geometry. Drawing consumes generated boss/mob/core artwork; worker generated no images.
- `EcologyBossBar` shows life and the finite wax/repair shell.
- Mob banners reference infrastructure's `EcologyBanner0..39` item types. Each mob drops its matching material slot; elites additionally drop common material.
- All 10 bosses and 40 mobs register `SpawnModBiomes` for their corresponding `EcologyBiome0..9`, enabling native bestiary biome icons/filters.
- `SpawnModBiomes` is registration-template metadata, not copied by the default `ModNPC.NewInstance` constructor path. `EcologyBestiary.Attach` additionally binds the registered `ModBiomeBestiaryInfoElement` in each actual `SetBestiary` entry, deduplicating native attachments. Data validation checks both the template and `Main.BestiaryDB`'s real entry, not a fresh combat instance's empty metadata.
- Locale filenames are exactly `Localization/EcologyCombat/zh-Hans.hjson` and `en-US.hjson`. Do not suffix the filenames with `_EcologyCombat`: tML interprets that suffix as an extra key prefix, while these files already contain full `Mods.StarfallThrone` paths. `Localizations()` (called by `Data()`) tests both languages and exact registered item/NPC key paths, restoring the original language afterward.

## Test markers / outputs

`ECOLOGY_COMBAT_DATA_PASS`, ten `ECOLOGY_BOSS_AI_PASS`, `ECOLOGY_MOBS_RUNTIME_PASS`, `ECOLOGY_COMBAT_RENDER_PASS`, `ECOLOGY_COMBAT_RUNTIME_PASS`.

Native render output: `outputs/ecology-combat-runtime.png` through the same isolated-save-relative output path used by divine combat tests.

## Mechanics notes for guide / honest scope

Every boss has four custom attacks, a half-health transition, harmless tells, recovery windows, complete cleanup, no tile destruction and no life reset. Finite wax shielding, breakable hive/radiator/mouth/core props, killable capsules/guards, encounter-only magnetic bending, destructible root/armor barriers, linked refraction segments and one-use bullet portals are functional. Repair drones replenish a small bounded armor shell, **not boss life**. Visual decoys are intentionally harmless. Root walls are dangerous projectiles removable by attacking their knots, not solid terrain. Mob spawn checks both the player's active ecology and the candidate spawn tile's qualified owned region. No ecology mobs spawn from this pool in observation mode.

The worker did not run a full build or launch the game, as requested; the main integration process must execute the native validation before release. No claim of two-human-client or playthrough balance testing is implied.
