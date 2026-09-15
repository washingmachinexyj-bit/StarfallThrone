# Ascendant equipment asset and API contract

Exclusive worker scope: Content/Ascendant/Equipment/** and Localization/AscendantEquipment/**.

Parent catalog: StarfallThrone.Content.Ascendant.AscendantCatalog with Root, Material(tier), Weapon(flat), Armor(flat), Expert(tier), CraftStation(tier).

All art is under Content/Assets/Ascendant/. Required inventory textures: Weapon0..11.png (distinct weapons), Armor0..17.png, Expert0..2.png. AutoloadEquip uses Armor{0,1,2,3,6,7,8,9,12,13,14,15}_Head.png, Armor{4,10,16}_Body.png and Armor{5,11,17}_Legs.png, matching existing Divine equip sheet layout. Additional Minion0..2.png (64x64 each, one frame). Buffs reuse Weapon3/7/11. Combat projectiles render weapon sprites plus native geometry; no further projectile sheets required.

Parent routes packet 61 (after consuming opcode) to AscendantEquipmentPlayer.ReceivePacket(BinaryReader reader, int sender). Existing public DivineEquipmentKeys.Skill K binding is reused only while an Ascendant set is equipped.

Validation entry points: AscendantEquipmentValidation.Data() and AscendantEquipmentValidation.Run(). Worker does not build, launch, install or claim tests passed. Parent runs opt-in validation after native content load.

Core implementation and both locales are now present. Validation emits ASCENDANT_EQUIPMENT_DATA_PASS, exactly 12 ASCENDANT_WEAPON_RUNTIME_PASS id= lines, ASCENDANT_EQUIPMENT_BOUNDARIES_PASS, ASCENDANT_EQUIPMENT_RENDER_PASS and ASCENDANT_EQUIPMENT_RUNTIME_PASS. Fixtures exercise native Projectile.AI/Damage, PickAmmo, CheckMana, Teleport, native armor renderer (12 loadouts, three poses), rendered projectile pixels, exact recipes/defense, binary cooldown persistence, delayed recovery, partial shield, final-damage mitigation, secondary exclusion, packet atomicity and denied server requests. The owner-hit evidence follows the existing bounded geometry/LOS model; this is not proof of two-client latency behavior or anti-cheat.

Packet 61 modes: 0 skill request; 1 server-authorized skill state (only a marked return moves position; a dew release instructs the owning client to launch bounded shots); 2/3 monotonic cooldown snapshots; 4 owned primary-hit evidence; 5 server resource acknowledgement; 6 hurt notification that can only reduce heat. Snapshot modes fully parse before mutating state. Cooldowns are six counters: accessory0/1/2, dew skill, furnace skill, fate skill. Save and death do not reset them. Existing K key reused, no new keybind.

Art is required exactly as stated above; no additional assets beyond Minion0..2 and agreed inventory/equip sheets. AscendantHostileOrigin only records metadata: NPC-fired hostile projectiles may have local-player owner in single player and remain eligible for shields/refunds; player-origin shots and traps cannot farm healing.

Numerical clarifications to match implemented tooltips: dew skill 32 generic base damage per bud; furnace builds 5 heat/6 ticks and loses25 on injury, 600 active +1500 recovery ticks; fate mark360 ticks and1800 total cooldown. Recovery heals1 per4 ticks after240-tick wait. Late weapon base damage340/180/205/150, star links110% per hit, max3 hits with12-tick shared type immunity. Link geometry trades single-target cadence for greater area as more stars are placed. No claim of a measured DPS uplift until actual playtesting.

Final rendering contract: Run outputs `outputs/ascendant-weapons-runtime.png` (1200x1440, 12 weapons x primary/contact and secondary/alternate views) and `outputs/ascendant-armor-runtime.png` (1200x720, 12 class loadouts x3 native player poses), using the same smoke SavePath-to-outputs resolution as Divine validation. Every weapon frame checks visible alpha pixels; primary melee and minion captures assert their real CanDamage contact window. Armor frames use Main.PlayerRenderer.DrawPlayer, not icon sheets. Additional exact markers: ASCENDANT_WEAPONS_RENDER_PASS, ASCENDANT_ARMOR_RENDER_PASS, ASCENDANT_EQUIPMENT_ABILITIES_PASS. Packet61 now preflights minimum remaining bytes for every known mode and rejects truncated seekable frames without first-chance exceptions.
