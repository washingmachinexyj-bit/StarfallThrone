# Pantheon combat integration (v0.11.0)

Owned files: six C# files in this directory, this document, and `Localization/PantheonCombat/{zh-Hans,en-US}.hjson`. No old gameplay files, assets, build scripts, installed packages or saves modified by combat worker. No worker build/install performed.

## Entry points / contract

- `PantheonBossNPC` subclasses exactly match `PantheonCatalog.Keys[i]+NPC`, indices 0–16.
- `PantheonCombatValidation.Data()` runs registration, native boss head/bar and 51 difficulty/life/damage budgets; multiplayer HP multiplication uses double before a 1.5-billion cap.
- `PantheonCombatValidation.Run()` must only run in `-starfall-art-smoke` menu context. It restores actors, players, items, world flags, game mode, transport, and render targets in finally blocks.
- Production loot calls `PantheonLoot.Add(NPCLoot,index)` and once-only `PantheonLoot.OnVictory(NPC,index)`; support owns all rewards/world unlocks. Cancelled/invalid prerequisites cannot drop loot.
- First prerequisite is Voyage boss16; later bosses require previous Pantheon. Time/biomes are start-only. Extra bosses interrupt safely, never kill the other boss. No tiles or old flags are written.

## Combat

17 individually scripted three-move encounters, three phases. All significant hazards have 75+ tick committed local geometry (75 tick body dash lane). Normal/expert/master life factors 1/1.5/1.9, damage 1/1.35/1.65; hostile projectile base values compensate Terraria 2/4/6 multipliers. Small shots .7 standard, heavy beams/palms 1.4 standard. Decorative NPC bodies contact-harmless except explicit committed core dashes. Body hitboxes are smaller than art; no wings/halos count as contact.

Breakable tactical objectives cancel marked attacks and give 20% exposure (4 seconds; judgment 6 seconds). Mirrors cancel reflection segments; serpent anchors and eclipse authority cancel enclosure; geometric vertices remove neighboring edges; selecting one judgment implement locks remaining options; seed husks and pillars actually occlude beams, but become dangerous later; gravity fields use limited local-owner acceleration and never invert gravity/teleport/remove wings; pages reorder and delete strokes; final rite is once-only without health locking. Helpers and hazards are server-spawned with slot+random serial ownership and complete ExtraAI snapshots.

Shared parts exist for serpent2/dragon10 (10 each), ferryman5/gate6/twins8 (2), judgment9 (4), final16 (3). Helpers never drop separate loot. The anonymous server throttle was REMOVED after review: every accepted native remote part delta is forwarded, including simultaneous different-player hits. Lethal proxy hits accumulate in a long pending counter before proxy reset. Attack-owning native `CanBeHitByProjectile`/`OnHitByProjectile` hooks share owner+identity+slot cooldown across visible parts and body, respecting projectile local/static cooldowns; item hits share the usual per-player ten-tick guard. This stops legitimate clients' single piercing projectile from multiplying across ten segments without merging other players' damage. It follows vanilla's attack-owner trust model, not a new anti-cheat guarantee against forged vanilla strike packets. Original contribution flags and `lastInteraction` are copied to the boss BEFORE forwarded damage so helper-only kills retain credit.

Twins8 controller is invisible, unchaseable, rejects direct item/projectile hits and never deals contact damage. Visible eye helpers remain damageable and chaseable. Only forwarded native strikes reach the controller pool. Current native smoke explicitly asserts both-eye targeting and shared health.

## Art (no additional assets needed)

Actual AI art inspected for Boss2, Boss8, Boss10. Boss8 uses distinct horizontal half-crops, never repeats the paired image. Serpent2 head crop `(0,28,109,101)`, dragon10 `(0,22,111,97)`; articulated bone/forge segments include inspected source scale/fog patches `(45,110,50,50)` and `(54,112,52,48)`, rotated along the spine with tapered final segments. Native render fixture checks crop bounds and actual nontransparent pixels. No tiled whole-S bodies. Other full bodies use their supplied 256/320 PNG. Frames are one supplied pose with squash, sway, trails, articulated helpers and effect geometry, not hand-painted multi-frame sprite animation. Bestiary still shows full source art.

## Markers / evidence

- `PANTHEON_COMBAT_DATA_PASS`
- `PANTHEON_BOSS_AI_PASS: id=0` ... `id=16` exactly once each successful Run
- `PANTHEON_COLLISION_PASS`
- `PANTHEON_AUTHORITY_PASS`
- `PANTHEON_COUNTERPLAY_PASS`
- `PANTHEON_MULTIPART_CONSERVATION_PASS` (eight real native remote hits, two parts, exact same-frame sums, second-part lethal combination and first-part lethal hit with the other contributor still pending). Before any forwarded lethal strike all owned helpers' interaction flags are merged, while the actual killing part's lastInteraction is preserved. Native broadcast assertion uses local `MessageID.DamageNPC` (28), not the nonexistent StrikeNPC constant.
- `PANTHEON_COMBAT_LOCALIZATION_PASS` (104 actual loaded display keys, raw/empty values rejected)
- `PANTHEON_COMBAT_RENDER_PASS`
- `PANTHEON_COMBAT_RUNTIME_PASS`
- native screenshot `outputs/pantheon-combat-runtime.png`, 17 rendered encounter cells, each with minimum actual nontransparent-pixel assertion.

Run exercises every boss, all 3 phases, all 3 moves and 3 difficulty budgets, native NPC/Projectile/Node AI, native `NPC.SimpleStrikeNPC`, shared-pool damage, distinct eye positions, projectile line/ring/gate/arc collision, real seed-cover occlusion, objective hits, phase/despawn/gate cleanup, complete ExtraAI roundtrips, no client spawning/break forging, server node/part native-damage deltas and both-player interaction credit, once-only finale and judgment choice. Menu transport uses temporary complete `NetMessage.buffer` / `Netplay.Clients` arrays and in-memory `ISocket` capture; no gameplay exception suppression, real listener or external connection.

First parent native build B03E ran all17 AI and collision checks. The server-only fixture then failed because menu clients do not initialize server message buffers. That fixture now supplies/restores native buffers and requires a real framed StrikeNPC broadcast. Latest localization and invisible-controller safety edits also require the parent final rebuild/retest. Do not describe the first run as a full pass.

BA8843 follow-up: all17 AI/collision checks still passed; newly lethal native server checks reached Boss Checklist's real `RecordBossNPC.OnKill`, where its server record table was absent in menu mode. An isolated optional record fixture now snapshots/replaces the four `RecordSystem` static collections, calls normal `OnWorldLoad` in SERVER mode and `LoadWorldData`, asserts both player rows exist, and restores original references and WorldFileData in Dispose. No GlobalNPC hooks/configs are disabled and no gameplay exceptions are suppressed. The transport also temporarily registers/restores all loaded Mod.netID values and ModNet.netMods, allowing ordinary optional OnKill packets through in-memory sockets. READY/RESTORED markers prove the scope ran; final native rerun remains required.

Honest limits: this is scripted native-engine smoke, not full manual fight balancing or a real two-client latency session. Patterns implement recognizable mechanics within a common safe state machine; ambient current, solid temporary tile platforms, scenery-changing weather, custom music, and dedicated multi-frame body animation are not supplied. No map mutation. Post-render screenshots must be visually checked on the final tested build (first snapshot preceded combat-localization completion).
