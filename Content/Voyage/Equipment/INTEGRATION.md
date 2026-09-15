# Voyage equipment worker handoff

Owned changes only: this folder and Localization/VoyageEquipment/{zh-Hans,en-US}.hjson. No existing encounter, mini, artwork, main integration or build scripts were modified.

## Registered content

- VoyageArmor0..35: six tiers, melee/ranged/magic/summon heads 0..3, chest4, legs5. Concrete AutoloadEquip classes, no dynamic-name registration required.
- VoyageExpert0..16: expert-only accessories. VoyageExpert7 is an actual wing item.
- VoyageAccessory0..10: eight ordinary items and three ingredient-exclusive upgrades. Accessories7 and10 are actual wing items.
- VoyageEquipmentPulse: bounded equipment proc/short arc/fire-support node/lock visual. Not a new weapon or independent minion.
- Global projectile origin metadata records sentry descendants and equipment descendants; the latter cannot recursively proc equipment.

## Required main integration

Wire message byte21 to `Content.Voyage.Equipment.VoyageEquipmentPlayer.ReceivePacket(reader, whoAmI)`.

The keybinds auto-register: `VoyageArmorSkill` defaults to V, `VoyageAccessoryDash` defaults to B. Labels and key-dependent item tooltips are localized. Main may use public SetTier, Cooldowns, EffectiveShield, FortifyTime, DroneTime, MirrorTime for a dedicated HUD; item tooltips already display current key/cooldown/shield values.

Call `VoyageEquipmentValidation.Validate()` inside the isolated client after content/recipes load. It now also calls VoyageEquipmentExpandedValidation. It checks 24 armor combinations and all28 accessories for exact stat deltas, class/crit/mana/summon bonuses, reapplication across actual ResetEffects calls, full unequip, five emblem states, full metadata/texture bindings for three wings, eight incompatibilities, all16 cooldowns through binary TagIO save/load and death, shield absorption through HurtModifiers/ConsumableDodge, directional guard front/rear damage, fatal protection, actual projectile Secondary/owner/source ancestry, sentry-child25% damage and four-class ExtraAI/lifetime restoration. Markers: `VOYAGE_EQUIPMENT_EXPANDED_PASS`, `VOYAGE_EQUIPMENT_PASS`. It uses detached Player/Item/Projectile objects without calling NewProjectile or loading/saving a real character/world.

## Assets (main owned)

Use the existing voyage contract: Armor0..35 and their _Head/_Body/_Legs equip sheets, Expert0..16, Accessory0..10. Additional wing sheets: Expert7_Wings, Accessory7_Wings, Accessory10_Wings (40x224). Equipment effects reuse Expert3, Expert4, Expert7, Expert9; no extra textures are required.

## Implemented effects / authority

Active armor: collision-aware thrust, bounded LOS-safe warp with original immunity explicitly preserved, half-second directional one-hit40% guard, five-second max450 shield. Guard orientation uses the server-approved aim, is drawn as a front semicircle, and checks attacker position or incoming hit direction. Unknown direction falls back to protection; rear physical hits are not reduced. Passive armor: server-owned one-bullet maintenance interception, owner-only four-second class-specific support node. The node charges at most once per0.2s and has a10s cooldown.

All17 expert effects and11 ordinary/combined effects are implemented, including fatal-hit survival, regeneration, focus damage, chain-excluded echoes, movement, wings, manual summon target indicators and source-aware sentry damage. All cooldowns are stored on ModPlayer, not ResetEffects, and saved. Multiplayer key requests are checked against sender identity, equipped state, cooldown, finite bounded direction and request rate; only server acknowledgements move players. Cooldown snapshots cannot reduce server cooldowns or edit another player.

The local injured player resolves hurt/shield/death hooks, as tModLoader's standard player-damage authority requires. This is not a server-authoritative overhaul of Terraria damage. Passive interception is server-only, and drone/mirror visuals are replicated. Ordinary small finite physical bullets are eligible, plus active VoyageHazard Bolt/Arc with radius<=16; rays/rings/fields/big attacks are never intercepted.

## Explicit calibration choices

- Armor stat totals match the design; pieces carry disjoint shares and never re-add totals at set activation.
- Armor/ordinary ammo saving uses the highest Voyage chance, not stacked independent rolls. This is explicit in tooltips.
- Goldenwing Hangar assists an already pursuing minion for0.6s after manual targeting, up to6% acceleration per frame capped at speed20, only with LOS; it does not rewrite arbitrary minion targeting AI.
- Coil/node require8 charge events at most every0.2s. Echo, coil and node damage has per-proc caps stated in item tooltips. These are deliberate anti-chain safeguards.
- A fully absorbed shield hit applies a12-tick contact debounce; the warp and both thrust abilities provide no invulnerability.
- Fatal protection ignores PvP and does not activate after a previous mod has already restored positive life. A cross-mod guarantee about other mods' later PreKill hooks is outside this module's control.

## Weapon API review and exact remaining design restrictions

- Equipment now reads IVoyageWeaponProjectile.Secondary directly. VoyageArsenal.LaunchProc writes a Shard with variant100; this path is recognized even when no equipment-origin parent exists. Secondary ancestry propagates into vanilla/modded children and grandchildren; a cross-owner child cannot inherit sentry bonuses. The four weapon source files were read/compiled but not edited.
- IsOwnedPrimary enforces valid matching owner, active/living player, friendly non-hostile/non-NPC/non-trap attack and non-secondary origin. CanTrigger additionally requires the local owner and never the server. Actual Item.Shoot counters also validate the source Player. Own equipment effects remain separate bounded pulses; LaunchProc is recognized but is not needed for their custom visuals.
- Strict safety consequence: VoyageAlly and its shots advertise Secondary=true, so minion/sentry/fragment hits do not charge the support node, coil, neural echo or observation timer. Summoners use primary whip hits for these interactions. This narrows the original generic 'valid hit' wording and is now explicit in both languages. Passive class bonuses and the25% sentry-child bonus still apply to those attacks.
- Star Chart Calibrator was corrected to display on the first valid summon hit after manual selection, not immediately on selection. Its lock visual deliberately may react to a secondary minion hit, but is harmless and never charges/spawns a damage proc.
- Goldenwing Hangar remains a bounded pursuit-speed assistance, not forced retargeting of arbitrary third-party minion AI. The existing/manual target API is honored by the weapon AI.
- No other armor/accessory effect is intentionally missing. Armor totals and recipe counts are unchanged; geometry/target-marker omissions identified in the first implementation have been corrected. Gameplay balance/full-modpack multiplayer remain untested here.

## Verification status

Scoped compilation against installed tML2026.07 plus the real weapon-worker sources succeeded with zero errors/warnings. Main still needs to execute the expanded Validate entry point in the isolated game; these tests have been compiled, not falsely reported as runtime-passing. Full combined build, texture render checks and multiplayer tests remain main integration work; this worker did not run build-release or launch clients. Temporary compile artifacts are removed before handoff.

Official APIs checked: tModLoader ModPlayer/ModItem/EquipLoader/WingStats documentation and official1.4.4 ExampleMod wing/armor/keybind examples. EquipLoader automatically enables composite body framing for _Body textures.
