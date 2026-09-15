# Ecology equipment integration

Own changes only: `Content/Ecology/Equipment/**`, `Localization/EcologyEquipment/**`.

110 concrete items: EcologyWeapon0..39 (biome*4 + melee/ranged/magic/summon), EcologyArmor0..59 (biome*6 + four class heads/body/legs), EcologyExpert0..9. Native AutoloadEquip attributes cover all 60 pieces. All 110 names/tooltips, 10 minion buffs, 12 projectile names, 10 set bonuses and the keybind have complete zh-Hans/en-US entries.

## Main integration

- No packet52 dispatcher is needed. Player-owned weapon/hit/defense effects use the existing native owner-authoritative combat model; no custom position, world change, or server RPC. Projectile state uses native SendExtraAI/ReceiveExtraAI. Tide stance changes only that player's derived stats and has no world/remote action.
- Keybind `EcologyTideStance` defaults to **N**; current existing defaults checked with rg: V, B, K. It works with Expert8 or the biome8 armor set. Two mutually exclusive stances, two-second switch cooldown.
- After all recipes exist: `StarfallThrone.Content.Ecology.Equipment.EcologyEquipmentValidation.Data()`.
- In main's existing isolated menu `-starfall-art-smoke` fixture: `StarfallThrone.Content.Ecology.Equipment.EcologyEquipmentValidation.Run()`.
- Data checks all 110 item registrations, 40 armor loadouts/native slots, exact melee defense, 140 recipes, minion flags, localization and bounded save/load. Run fires each of the 40 weapons with native ammo/mana, AI, Damage, collision, penetration expiry, network roundtrip, minion dismissal, bounded proc counts; also renders each attack and all 40 armor loadouts in three poses using the native renderer. Graphics/global state and fixture terrain restore in finally; it does not save a world or write artifacts.
- Expected markers: `ECOLOGY_EQUIPMENT_DATA_PASS`, 40 `ECOLOGY_WEAPON_RUNTIME_PASS`, `ECOLOGY_EQUIPMENT_BOUNDARIES_PASS`, `ECOLOGY_EQUIPMENT_ABILITIES_PASS`, `ECOLOGY_EQUIPMENT_RENDER_PASS`, `ECOLOGY_EQUIPMENT_RUNTIME_PASS`.

## Art contract

Exactly contract assets Weapon0..39, Expert0..9, Armor0..59; AutoloadEquip requires matching ArmorN_Head/Body/Legs sheets with native dimensions. All additional shots draw existing weapon/core/expert artwork plus geometric combat cues. Ten minions deliberately reuse **Mob(biome*4)** creature art as domesticated ecology inhabitants; no unannounced Minion PNG requirement. Buffs use their summon weapon icon. No images generated or edited by this worker.

## Recipes and bounded balance

Weapons have a direct core16 + own material12 recipe at the branch base station, plus an advanced alternate bar10 + component4 + core6 recipe. Armor uses own bars/components/core at GearStation(biome). Every recipe explicitly checks vanilla unlock and own ecological victory. No optional branch requires a different ecology boss. Drop weapons work immediately; crafting conditions do not disable equipped items.

Melee armor totals: 12/17/20/24/35/48/58/64/69/78. Four classes differ in head defense, damage and class-specific bonuses. Final-tier weapon damage 165/94/132/86, deliberately Moon-era rather than end-Voyage inflation. Twenty-two persistent cooldown slots prevent unequip/death resets. Gear-origin projectiles implement the existing IVoyageWeaponProjectile secondary contract, preventing loops with old and new equipment. Primary hit counters are owner-only, LOS/range checked, at most once per 12 ticks, and exclude friendly NPCs/target dummies. Damage/heal procs have fixed small base budgets and finite shots.

All code is delivered pending main native smoke results; this worker has not launched a full build or game, and makes no independent runtime-pass claim. Main reported its second compile snapshot had no equipment compile errors before the latest bounded render-fixture addition. Real latency/two-client play and human combat balancing remain separate from the isolated tests.

First native Data finding: weapon2's valid Chinese description has 25 characters; weapon8 has 29. The old >30-character tooltip assertion incorrectly assumed English text length. Replaced all weapon/armor/expert length heuristics with a shared check for nonblank resolved descriptive text, no untranslated localization key, and no unresolved localization reference. It deliberately does not demand digits either, because valid English descriptions may spell out numbers. Read-only source audit covers all 110 zh-Hans and all 110 en-US item tooltips. No tooltip padding or gameplay changes were made; main must rerun native Data/Run.
