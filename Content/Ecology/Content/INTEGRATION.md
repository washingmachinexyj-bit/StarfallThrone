# Ecology content API

All classes use namespace `StarfallThrone.Content.Ecology` (despite directory Content/).

- `EcologyLoot.Configure(NPCLoot loot, int biome)` installs normal-only core/weapon/mask, expert bag, trophy, master relic and per-player pet rules.
- Pet item is `EcologyPetItem{i}`; projectile `EcologyPetProjectile{i}`; buff `EcologyPetBuff{i}`. This matches the main catalogue's current `Pet(i)` mapping (no catalogue change needed).
- Station items/tiles are `EcologyStation{i}` / `EcologyStationTile{i}`.
- Banner items/tiles are `EcologyBanner{i}` / `EcologyBannerTile{i}` for flattened mob index 0..39. Mob worker should set Banner=Type and BannerItem=EcologyCatalog.Item("EcologyBanner"+flat).
- Mineral bed integration: items `EcologyMineralSeed{i}` derive `EcologyMineralSeedBase`, expose `Index`, have no autonomous UseItem/placement or client-side consumption. Main's authoritative bed UI may consume one from an inventory slot after validating matching biome and `EcologyWorld.Downed[Index]`. Each item represents **9 ore blocks** via public constant `EcologyMineralSeedBase.OreYield=9`; recipe core1 + stone30 yields one seed, at that biome's base station with DownedCondition. Never requires its own ore. Main persists growth jobs and consumes only on accepted job.
- `EcologyContentValidation.Data()` validates native loaded content, recipes, drop-rate tables, station adjacency, 40 banner NPC mappings and minPick invariants. Call only after recipes have loaded in isolated art-smoke menu. `Runtime()` additionally checks native bag drop resolver and pet upkeep, restoring touched arrays/state.
- `EcologyInfrastructure.StationParents(int)` exposes exact transitive optional station adjacency. `EcologyToolBase.Metal(int)` returns first-tool vanilla metal (early recipes use gold/platinum group).
- Fishing is provided by `EcologyFishingPlayer.CatchFish`; only nonquest, nonlava/nonhoney catches in active ecology with native >=300-block threshold. Crates remain ordinary-only and deliberately exclude ore/bar/core/expert rewards.

Art owns all textures; this worker creates no images. Mineral seeds reuse the matching generated Core icon (culturing a fragment), pet item/projectile share Pet{i}, buff uses Core{i}. All other texture names follow contract, with 10 native Mask{i}_Head sheets required.

## Handoff status

Implemented 218 items, 78 placeable tiles, 10 pet buffs and 11 projectiles (10 harmless pet followers + shared native drill), with complete item/buff/projectile Chinese and English localization. Recipe groups are registered by EcologyRecipeGroups, station inheritance by EcologyStationAdjacency. No manual registration or packet dispatch is needed for this worker.

Seed instructions now match main's actual interaction: hold matching seed and right-click owned bed, with clear 3x3 output area above. The seed item has no client-side use/consumption hook.

Static verification performed: loaded native DLL metadata scan found no missing directly referenced vanilla item/tile/projectile/dust/buff constants; both localization files parse as JSON; catalogue, combat Banner IDs and equipment material/component usages reviewed. No builds or client launches were run by this worker. Main should invoke Data() then Runtime() after all content/recipes/assets load, and check ECOLOGY_CONTENT_DATA_PASS / ECOLOGY_CONTENT_RUNTIME_PASS.
