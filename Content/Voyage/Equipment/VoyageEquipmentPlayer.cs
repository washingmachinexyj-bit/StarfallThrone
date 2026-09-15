using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameInput;

namespace StarfallThrone.Content.Voyage.Equipment;

public sealed class VoyageEquipmentKeys : ModSystem
{
    public static ModKeybind ArmorSkill;
    public static ModKeybind AccessoryDash;
    public override void Load()
    {
        ArmorSkill = KeybindLoader.RegisterKeybind(Mod, "VoyageArmorSkill", "V");
        AccessoryDash = KeybindLoader.RegisterKeybind(Mod, "VoyageAccessoryDash", "B");
    }
    public override void Unload() { ArmorSkill = null; AccessoryDash = null; }
}

public sealed class VoyageEquipmentPlayer : ModPlayer
{
    // Slots 0..5 are independent armor cooldowns. Everything survives unequipping/death and is saved.
    public const int DashSlot = 6, EmergencySlot = 7, DroneSlot = 8, MirrorSlot = 9,
        AnchorSlot = 10, SteadySlot = 11, HealSlot = 12, EchoSlot = 13, CoilSlot = 14, NodeSlot = 15;
    public bool[] Expert;
    public bool[] Ordinary;
    public int[] Cooldowns;
    public int SetTier = -1, SetStyle;
    public float AmmoSaving, FlightBonus;
    public bool Hover;
    public int GelShield, ArkShield, ArkTime, FortifyTime, AnchorTime, DroneTime, MirrorTime;
    public int NoHurtTicks, CombatTicks, SteadyCharge, NodeCharge, CoilCharge;
    public bool SteadyReady;
    public Vector2 FortifyDirection = Vector2.UnitX;
    public int FocusTarget = -1, FocusCharge, FocusGrace;
    private int healRemaining, healTicks, healBudget, rangedCount, magicCount, lastDirection, lastManualTarget = -1, pendingLockTarget = -1;
    private ulong tickStamp = ulong.MaxValue, lastHitTick, lastProcTick, lastChargeTick, lastRangedTick = ulong.MaxValue, lastMagicTick = ulong.MaxValue, lastRequestTick;
    private bool consumedShieldThisHit;
    private bool fortificationApplied;
    private bool sentDroneActive, sentMirrorActive;
    public int EffectiveShield => Math.Max(Expert[0] ? GelShield : 0, SetTier == 5 && ArkTime > 0 ? ArkShield : 0);

    public override void Initialize()
    {
        Expert = new bool[17]; Ordinary = new bool[11]; Cooldowns = new int[16];
    }
    public override void ResetEffects()
    {
        Array.Clear(Expert); Array.Clear(Ordinary);
        SetTier = -1; SetStyle = 0; AmmoSaving = FlightBonus = 0; Hover = false;
    }
    private void TickCooldowns()
    {
        if (tickStamp == Main.GameUpdateCount) return;
        tickStamp = Main.GameUpdateCount;
        for (int i = 0; i < Cooldowns.Length; i++) if (Cooldowns[i] > 0) Cooldowns[i]--;
    }
    public override void PreUpdate() => TickCooldowns();
    public override void UpdateDead()
    {
        TickCooldowns();
        ResetEffects();
        GelShield = ArkShield = ArkTime = FortifyTime = AnchorTime = DroneTime = MirrorTime = 0;
        CombatTicks = NoHurtTicks = FocusCharge = FocusGrace = NodeCharge = CoilCharge = SteadyCharge = 0;
        FocusTarget = pendingLockTarget = -1; SteadyReady = false; healRemaining = healTicks = rangedCount = magicCount = 0;
    }
    public override void SaveData(TagCompound tag)
    {
        tag["voyageEquipmentCooldowns"] = (int[])Cooldowns.Clone();
        tag["voyageShieldRecharge"] = NoHurtTicks;
    }
    public override void LoadData(TagCompound tag)
    {
        int[] saved = tag.GetIntArray("voyageEquipmentCooldowns");
        for (int i = 0; i < Math.Min(saved.Length, Cooldowns.Length); i++) Cooldowns[i] = Math.Clamp(saved[i], 0, 7200);
        NoHurtTicks = Math.Clamp(tag.GetInt("voyageShieldRecharge"), 0, 720);
    }
    public override void CopyClientState(ModPlayer targetCopy)
    {
        var copy = (VoyageEquipmentPlayer)targetCopy;
        copy.Cooldowns = (int[])Cooldowns.Clone();
    }
    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        var previous = (VoyageEquipmentPlayer)clientPlayer;
        for (int i = 0; i < Cooldowns.Length; i++)
            if (Cooldowns[i] > previous.Cooldowns[i]) { SendCooldownSnapshot(2, -1, -1); break; }
    }
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendCooldownSnapshot(Main.netMode == NetmodeID.Server ? (byte)3 : (byte)2, toWho, fromWho);
    private void SendCooldownSnapshot(byte mode, int toWho, int fromWho)
    {
        ModPacket packet = Mod.GetPacket(); packet.Write((byte)21); packet.Write(mode); packet.Write((byte)Player.whoAmI); packet.Write((byte)0);
        foreach (int cooldown in Cooldowns) packet.Write((ushort)Math.Clamp(cooldown, 0, 7200));
        packet.Send(toWho, fromWho);
    }
    public override bool CanConsumeAmmo(Item weapon, Item ammo) => AmmoSaving <= 0 || !weapon.CountsAsClass(DamageClass.Ranged) || Main.rand.NextFloat() >= AmmoSaving;
    public override void PostUpdateEquips()
    {
        if (Expert[5] && AnchorTime > 0) Player.endurance += .08f;
        if (Expert[11] && NoHurtTicks >= 360) Player.lifeRegen += 12;
    }
    public override void PostUpdateRunSpeeds()
    {
        if (FlightBonus > 0) Player.wingTimeMax = (int)(Player.wingTimeMax * (1 + FlightBonus));
        if (Hover && Player.wingsLogic > 0 && Player.controlDown && Player.controlJump && Player.wingTime > 0 && !Player.mount.Active)
        {
            Player.velocity.Y *= .35f;
            Player.maxFallSpeed = Math.Min(Player.maxFallSpeed, 1f);
            // Vanilla flight still spends wingTime; hovering never refills it.
        }
    }
    public override void PostUpdate()
    {
        if (Player.dead || !Player.active) return;
        NoHurtTicks = Math.Min(720, NoHurtTicks + 1);
        if (CombatTicks > 0) CombatTicks--;
        if (Player.itemAnimation > 0)
        {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.CanBeChasedBy() && Vector2.DistanceSquared(npc.Center, Player.Center) < 1600 * 1600) { CombatTicks = 180; break; }
        }
        else if (SetTier == 1 && CombatTicks == 0)
        {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.boss && npc.target == Player.whoAmI && npc.CanBeChasedBy() && Vector2.DistanceSquared(npc.Center, Player.Center) < 1600 * 1600) { CombatTicks = 180; break; }
        }
        if (ArkTime > 0) ArkTime--; else ArkShield = 0;
        if (FortifyTime > 0) FortifyTime--;
        if (AnchorTime > 0) AnchorTime--;
        if (DroneTime > 0) DroneTime--;
        if (MirrorTime > 0) MirrorTime--;
        if (FocusGrace > 0) FocusGrace--; else { FocusTarget = -1; FocusCharge = 0; }
        if (!Expert[1]) { FocusTarget = -1; FocusCharge = FocusGrace = 0; }
        if (!Expert[0]) GelShield = 0;
        else if (NoHurtTicks >= 720 && GelShield == 0)
        {
            GelShield = Math.Min(220, (int)(Player.statLifeMax2 * .12f));
            if (Player.whoAmI == Main.myPlayer) Effect(Color.Turquoise, 10);
        }
        if (SetTier != 5) { ArkTime = ArkShield = 0; }
        if (SetTier != 4) FortifyTime = 0;
        if (SetTier != 1) DroneTime = 0;
        if (!Expert[7]) MirrorTime = 0;

        if (Expert[12] && Cooldowns[SteadySlot] == 0)
        {
            if (Player.velocity.LengthSquared() < .4f) SteadyCharge = Math.Min(60, SteadyCharge + 1);
            else if (!SteadyReady) SteadyCharge = 0;
            if (SteadyCharge >= 60) SteadyReady = true;
        }
        else if (!Expert[12]) { SteadyCharge = 0; SteadyReady = false; }

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            if (SetTier == 1 && CombatTicks > 0 && Cooldowns[DroneSlot] == 0 && DroneTime == 0)
            { DroneTime = 360; Cooldowns[DroneSlot] = 720; }
            int direction = Math.Abs(Player.velocity.X) > 2 ? Math.Sign(Player.velocity.X) : 0;
            if (Expert[7] && direction != 0 && lastDirection != 0 && direction != lastDirection && Cooldowns[MirrorSlot] == 0)
            { MirrorTime = 180; Cooldowns[MirrorSlot] = 360; }
            if (direction != 0) lastDirection = direction;
            if (DroneTime > 0 || MirrorTime > 0) TryIntercept();
            if (Main.netMode == NetmodeID.Server && (sentDroneActive != (DroneTime > 0) || sentMirrorActive != (MirrorTime > 0) ||
                (DroneTime > 0 || MirrorTime > 0) && Main.GameUpdateCount % 30 == (ulong)(Player.whoAmI % 30)))
            {
                ModPacket visual = Mod.GetPacket(); visual.Write((byte)21); visual.Write((byte)4); visual.Write((byte)Player.whoAmI); visual.Write((byte)0);
                visual.Write((ushort)DroneTime); visual.Write((ushort)MirrorTime); visual.Send();
                sentDroneActive = DroneTime > 0; sentMirrorActive = MirrorTime > 0;
            }
        }

        if (Player.whoAmI == Main.myPlayer)
        {
            if (Expert[16] && healTicks > 0 && healRemaining > 0)
            {
                int heal = Math.Min(healRemaining, Math.Max(1, (healBudget + 239) / 240));
                // Spread the total amount across four seconds instead of healing every tick.
                if (healTicks % 15 == 0)
                {
                    heal = Math.Min(healRemaining, Math.Max(1, (healBudget + 15) / 16));
                    Player.Heal(heal); healRemaining -= heal;
                }
                healTicks--;
            }
            else if (!Expert[16]) healTicks = healRemaining = 0;
            int manual = Player.MinionAttackTargetNPC;
            if (!Expert[15]) pendingLockTarget = -1;
            else if (manual != lastManualTarget)
                pendingLockTarget = manual >= 0 && manual < Main.maxNPCs && Main.npc[manual].CanBeChasedBy() ? manual : -1;
            if ((Expert[4] || Expert[9]) && manual != lastManualTarget && manual >= 0 && manual < Main.maxNPCs && Main.npc[manual].CanBeChasedBy())
            {
                VoyageEquipmentPulse.Spawn(Player, Main.npc[manual].Center, Vector2.Zero, 0, 4, SetStyle, manual);
                // Replicate the manual targeting change; the minion's own AI remains authoritative for its movement.
                foreach (Projectile p in Main.ActiveProjectiles)
                    if (p.owner == Player.whoAmI && (p.minion || p.sentry)) p.netUpdate = true;
            }
            lastManualTarget = manual;
        }
    }
    private void TryIntercept()
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (!EligibleForInterception(projectile) || Vector2.DistanceSquared(projectile.Center, Player.Center) > 64 * 64) continue;
            // Do not run OnKill explosions: interception removes an ordinary finite bullet, never an encounter controller.
            projectile.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, projectile.identity, projectile.owner);
            if (DroneTime > 0) DroneTime = 0; else MirrorTime = 0;
            Effect(Color.LightCyan, 12);
            break;
        }
    }
    public static bool EligibleForInterception(Projectile p)
    {
        if (p.ModProjectile is global::StarfallThrone.Content.Voyage.Bosses.VoyageHazard hazard)
            return p.active && p.hostile && hazard.ActiveDamage && hazard.Radius <= 16 &&
                hazard.Shape is global::StarfallThrone.Content.Voyage.Bosses.VoyageHazardShape.Bolt or global::StarfallThrone.Content.Voyage.Bosses.VoyageHazardShape.Arc;
        if (!p.active || !p.hostile || p.friendly || p.damage <= 0 || p.width > 32 || p.height > 32 ||
            p.velocity.LengthSquared() < 4 || !p.tileCollide || p.penetrate != 1 || p.timeLeft > 600) return false;
        if (p.ModProjectile != null && p.ModProjectile.CanDamage() == false) return false;
        // A bounded physical bullet only: beams, stationary fields, rings and infinite-penetration attacks are excluded.
        return p.aiStyle != ProjAIStyleID.Beam && p.aiStyle != ProjAIStyleID.ThickLaser;
    }

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        consumedShieldThisHit = false;
        fortificationApplied = false;
        if (SetTier == 4 && FortifyTime > 0)
        {
            Vector2 towardSource = new Vector2(-modifiers.HitDirection, 0);
            if (Player.whoAmI == Main.myPlayer && modifiers.DamageSource != null && modifiers.DamageSource.TryGetCausingEntity(out Entity entity) && entity != null)
                towardSource = entity.Center - Player.Center;
            if (towardSource.LengthSquared() < .001f || Vector2.Dot(towardSource.SafeNormalize(Vector2.UnitX), FortifyDirection) >= 0)
            { modifiers.FinalDamage *= .60f; fortificationApplied = true; }
        }
        if (Expert[12] && SteadyReady) modifiers.FinalDamage *= .85f;
        if (EffectiveShield > 0)
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                int shield = EffectiveShield;
                if (shield > 0 && shield < info.Damage)
                {
                    ConsumeShield(shield); info.Damage -= shield; consumedShieldThisHit = true;
                }
            };
    }
    public override bool ConsumableDodge(Player.HurtInfo info)
    {
        if (EffectiveShield < info.Damage || info.Damage <= 0 || consumedShieldThisHit) return false;
        ConsumeShield(info.Damage); ConsumeStances();
        // Shield interception grants a short contact debounce, not a teleport/dash invulnerability window.
        Player.immune = true; Player.immuneTime = Math.Max(Player.immuneTime, 12);
        Effect(Color.Cyan, 12);
        return true;
    }
    private void ConsumeShield(int amount)
    {
        // Reduce both overlapping capacities by the same hit: they do not become sequential additive shields.
        GelShield = Math.Max(0, GelShield - amount); ArkShield = Math.Max(0, ArkShield - amount);
        NoHurtTicks = 0;
    }
    private void ConsumeStances()
    {
        if (SetTier == 4 && FortifyTime > 0 && fortificationApplied) { FortifyTime = 0; Effect(Color.Gold, 16); }
        if (Expert[12] && SteadyReady) { SteadyReady = false; SteadyCharge = 0; Cooldowns[SteadySlot] = 480; }
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        NoHurtTicks = 0; CombatTicks = 180; ConsumeStances();
        if (Expert[5] && Cooldowns[AnchorSlot] == 0)
        { AnchorTime = 120; Cooldowns[AnchorSlot] = 360; }
    }
    public override void PostHurt(Player.HurtInfo info)
    {
        if (Expert[16] && Player.statLife > 0 && Player.statLife < Player.statLifeMax2 * .30f && Cooldowns[HealSlot] == 0)
        {
            Cooldowns[HealSlot] = 1800; healTicks = 240;
            healBudget = healRemaining = (int)(Player.statLifeMax2 * .08f);
            Effect(Color.LightGreen, 18);
        }
    }
    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
    {
        if (pvp || Player.statLife > 0 || !Expert[6] || Cooldowns[EmergencySlot] > 0) return true;
        Cooldowns[EmergencySlot] = 7200;
        Player.statLife = 1; Player.immune = true; Player.immuneTime = Math.Max(Player.immuneTime, 60);
        playSound = false; genDust = false; Effect(Color.Orange, 28);
        return false;
    }

    public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) => ApplyFocus(target, ref modifiers);
    public override void ModifyHitNPCWithProj(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (projectile.owner != Player.whoAmI || projectile.npcProj || projectile.trap || projectile.ModProjectile is VoyageEquipmentPulse ||
            projectile.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromEquipment) return;
        ApplyFocus(target, ref modifiers);
        if (Expert[9] && (projectile.sentry || projectile.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromSentry)) modifiers.SourceDamage += .25f;
    }
    private void ApplyFocus(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (Expert[1] && FocusTarget == target.whoAmI && FocusCharge >= 120 && FocusGrace > 0) modifiers.SourceDamage += .18f;
    }
    public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => RecordHit(target, item.DamageType, damageDone);
    public override void OnHitNPCWithProj(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Expert[15] && pendingLockTarget == target.whoAmI && Player.whoAmI == Main.myPlayer && projectile.owner == Player.whoAmI &&
            !Player.dead && projectile.friendly && !projectile.hostile && !projectile.npcProj && !projectile.trap &&
            !projectile.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromEquipment &&
            (projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[projectile.type]))
        {
            if (VoyageEquipmentPulse.Spawn(Player, target.Center, Vector2.Zero, 0, 4, 3, target.whoAmI) >= 0) pendingLockTarget = -1;
        }
        if (!VoyageEquipmentProjectileOrigin.CanTrigger(Player, projectile)) return;
        RecordHit(target, projectile.DamageType, damageDone);
    }
    private void RecordHit(NPC target, DamageClass damageClass, int damage)
    {
        if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer || !Player.active || Player.dead ||
            damage <= 0 || target == null || target.friendly || target.type == NPCID.TargetDummy) return;
        CombatTicks = 180;
        if (Expert[1])
        {
            if (FocusTarget != target.whoAmI || FocusGrace == 0) { FocusTarget = target.whoAmI; FocusCharge = 0; lastHitTick = Main.GameUpdateCount; }
            else FocusCharge = Math.Min(120, FocusCharge + (int)Math.Min(180UL, Main.GameUpdateCount - lastHitTick));
            lastHitTick = Main.GameUpdateCount; FocusGrace = 180;
        }
        if (Main.GameUpdateCount == lastProcTick) return;
        lastProcTick = Main.GameUpdateCount;
        if (Expert[3] && Cooldowns[EchoSlot] == 0)
        {
            Cooldowns[EchoSlot] = 60;
            VoyageEquipmentPulse.Spawn(Player, Player.Center, (target.Center - Player.Center).SafeNormalize(Vector2.UnitX) * 12, Math.Clamp(damage / 5, 1, 8000), 0, ClassIndex(damageClass), target.whoAmI);
        }
        bool canCharge = Main.GameUpdateCount - lastChargeTick >= 12;
        if (canCharge) lastChargeTick = Main.GameUpdateCount;
        if (canCharge && Expert[10] && damageClass.CountsAsClass(DamageClass.Melee) && Cooldowns[CoilSlot] == 0)
        {
            CoilCharge++;
            if (CoilCharge >= 8)
            {
                CoilCharge = 0; Cooldowns[CoilSlot] = 90;
                VoyageEquipmentPulse.Spawn(Player, Player.Center, (target.Center - Player.Center).SafeNormalize(Vector2.UnitX), Math.Clamp(damage / 2, 1, 14000), 1, 0, target.whoAmI);
            }
        }
        if (canCharge && SetTier == 3 && Cooldowns[NodeSlot] == 0)
        {
            NodeCharge++;
            if (NodeCharge >= 8)
            {
                NodeCharge = 0; Cooldowns[NodeSlot] = 600;
                VoyageEquipmentPulse.Spawn(Player, Player.Center + new Vector2(Player.direction * 60, -55), Vector2.Zero, Math.Clamp(damage / 3, 1, 7000), 2, SetStyle, target.whoAmI);
            }
        }
    }
    private static int ClassIndex(DamageClass damageClass) => damageClass.CountsAsClass(DamageClass.Melee) ? 0 : damageClass.CountsAsClass(DamageClass.Ranged) ? 1 : damageClass.CountsAsClass(DamageClass.Magic) ? 2 : 3;
    public override bool Shoot(Item item, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        if (Main.netMode == NetmodeID.Server || !Player.active || Player.dead || Player.whoAmI != Main.myPlayer || item == null ||
            source == null || source.Player != Player || item.damage <= 0 || damage <= 0) return true;
        if (Expert[8] && item.CountsAsClass(DamageClass.Ranged) && lastRangedTick != Main.GameUpdateCount)
        {
            lastRangedTick = Main.GameUpdateCount;
            if (++rangedCount >= 5)
            {
                rangedCount = 0;
                VoyageEquipmentPulse.Spawn(Player, position, velocity.SafeNormalize(Vector2.UnitX) * 16, Math.Clamp(damage / 3, 1, 9000), 0, 1, -1);
            }
        }
        if (Expert[14] && item.CountsAsClass(DamageClass.Magic) && lastMagicTick != Main.GameUpdateCount)
        {
            lastMagicTick = Main.GameUpdateCount;
            if (++magicCount >= 6)
            {
                magicCount = 0;
                VoyageEquipmentPulse.Spawn(Player, position, velocity.SafeNormalize(Vector2.UnitX) * 13, Math.Clamp(damage / 3, 1, 10000), 0, 2, -1);
            }
        }
        return true;
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Player.whoAmI != Main.myPlayer || Player.dead || Main.gameMenu || Main.drawingPlayerChat || Main.editSign || Main.editChest) return;
        if (VoyageEquipmentKeys.ArmorSkill?.JustPressed == true) RequestAction(0, Main.MouseWorld - Player.Center);
        if (VoyageEquipmentKeys.AccessoryDash?.JustPressed == true)
            RequestAction(1, new Vector2((Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0), (Player.controlDown ? 1 : 0) - (Player.controlUp ? 1 : 0)));
    }
    public void RequestAction(byte action, Vector2 direction)
    {
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y)) return;
        direction = direction.SafeNormalize(new Vector2(Player.direction, 0));
        if (Main.netMode == NetmodeID.SinglePlayer) TryAction(action, direction);
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)21); packet.Write((byte)0);
            packet.Write((byte)Player.whoAmI); packet.Write(action); packet.Write(direction.X); packet.Write(direction.Y); packet.Send();
        }
    }
    private bool TryAction(byte action, Vector2 direction)
    {
        if (!Player.active || Player.dead || Player.mount.Active || Player.frozen || Player.stoned || Player.webbed) return false;
        int tier = VoyageEquipmentData.EquippedTier(Player);
        if (action == 0)
        {
            if (tier is not (0 or 2 or 4 or 5) || Cooldowns[tier] > 0) return false;
            if (tier == 0)
            {
                Vector2 d = new((Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0), (Player.controlDown ? 1 : 0) - (Player.controlUp ? 1 : 0));
                Player.velocity = d.SafeNormalize(new Vector2(Player.direction, 0)) * 15;
                Cooldowns[0] = 480;
            }
            if (tier == 2)
            {
                Vector2 start = Player.position, safe = start;
                for (int step = 1; step <= 24; step++)
                {
                    Vector2 next = start + direction * (step * 8);
                    if (next.X < 32 || next.Y < 32 || next.X + Player.width > Main.maxTilesX * 16 - 32 ||
                        next.Y + Player.height > Main.maxTilesY * 16 - 32 ||
                        Collision.SolidCollision(next, Player.width, Player.height) ||
                        !Collision.CanHitLine(start, Player.width, Player.height, next, Player.width, Player.height)) break;
                    safe = next;
                }
                if (Vector2.DistanceSquared(start, safe) < 64) return false;
                bool immune = Player.immune, immuneNoBlink = Player.immuneNoBlink;
                int immuneTime = Player.immuneTime; int[] hurtCooldowns = (int[])Player.hurtCooldowns.Clone();
                Player.Teleport(safe, TeleportationStyleID.RodOfDiscord);
                Player.immune = immune; Player.immuneNoBlink = immuneNoBlink; Player.immuneTime = immuneTime;
                Array.Copy(hurtCooldowns, Player.hurtCooldowns, hurtCooldowns.Length);
                // The validated equipment acknowledgement replicates this position without adding teleport immunity.
                Cooldowns[2] = 720;
            }
            if (tier == 4) { FortifyTime = 30; FortifyDirection = direction; Cooldowns[4] = 600; }
            if (tier == 5)
            {
                ArkTime = 300; ArkShield = Math.Min(450, (int)(Player.statLifeMax2 * .25f)); Cooldowns[5] = 2700;
            }
        }
        else if (action == 1)
        {
            if (Cooldowns[DashSlot] > 0 || !(Expert[13] || Expert[2] && Math.Abs(Player.velocity.Y) > .01f)) return false;
            Player.velocity = direction * (Expert[13] ? 20 : 16);
            Cooldowns[DashSlot] = Expert[13] ? 180 : 360;
        }
        else return false;
        Player.fallStart = (int)(Player.position.Y / 16);
        Effect(Color.LightCyan, 18);
        return true;
    }
    private void SendActionState(byte action)
    {
        ModPacket packet = Mod.GetPacket(); packet.Write((byte)21); packet.Write((byte)1); packet.Write((byte)Player.whoAmI); packet.Write(action);
        packet.Write(Player.position.X); packet.Write(Player.position.Y); packet.Write(Player.velocity.X); packet.Write(Player.velocity.Y);
        for (int i = 0; i < 7; i++) packet.Write((ushort)Cooldowns[i]);
        packet.Write((ushort)ArkTime); packet.Write((ushort)ArkShield); packet.Write((byte)FortifyTime);
        packet.Write(FortifyDirection.X); packet.Write(FortifyDirection.Y); packet.Send();
    }
    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        byte mode = reader.ReadByte(), identity = reader.ReadByte(), action = reader.ReadByte();
        if (identity >= Main.maxPlayers) return;
        var state = Main.player[identity].GetModPlayer<VoyageEquipmentPlayer>();
        if (mode == 0)
        {
            Vector2 direction = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode != NetmodeID.Server || sender != identity || action > 1 ||
                !float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared() > 1.1f ||
                !state.Player.active || state.Player.dead || Main.GameUpdateCount - state.lastRequestTick < 8) return;
            state.lastRequestTick = Main.GameUpdateCount;
            if (state.TryAction(action, direction.SafeNormalize(new Vector2(state.Player.direction, 0)))) state.SendActionState(action);
        }
        else if (mode == 4 && Main.netMode == NetmodeID.MultiplayerClient)
        {
            state.DroneTime = Math.Min(360, (int)reader.ReadUInt16());
            state.MirrorTime = Math.Min(180, (int)reader.ReadUInt16());
        }
        else if (mode is 2 or 3)
        {
            if (mode == 2 && (Main.netMode != NetmodeID.Server || sender != identity) || mode == 3 && Main.netMode != NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < state.Cooldowns.Length; i++)
                state.Cooldowns[i] = Math.Max(state.Cooldowns[i], Math.Min(7200, (int)reader.ReadUInt16()));
            // A client can only lengthen its own cooldown; it cannot reset one or change another player's state.
            if (mode == 2) state.SendCooldownSnapshot(3, -1, -1);
        }
        else if (mode == 1 && Main.netMode == NetmodeID.MultiplayerClient)
        {
            Vector2 position = new(reader.ReadSingle(), reader.ReadSingle()), velocity = new(reader.ReadSingle(), reader.ReadSingle());
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y)) return;
            state.Player.position = position; state.Player.velocity = velocity;
            state.Player.fallStart = (int)(position.Y / 16);
            for (int i = 0; i < 7; i++) state.Cooldowns[i] = reader.ReadUInt16();
            state.ArkTime = reader.ReadUInt16(); state.ArkShield = reader.ReadUInt16(); state.FortifyTime = reader.ReadByte();
            Vector2 guard = new(reader.ReadSingle(), reader.ReadSingle());
            state.FortifyDirection = float.IsFinite(guard.X) && float.IsFinite(guard.Y) ? guard.SafeNormalize(Vector2.UnitX) : Vector2.UnitX;
            state.Effect(Color.LightCyan, 18);
        }
    }
    private void Effect(Color color, int count)
    {
        if (Main.dedServ || Player.whoAmI < 0 || Player.whoAmI >= Main.maxPlayers || !ReferenceEquals(Main.player[Player.whoAmI], Player)) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 v = (MathHelper.TwoPi * i / count).ToRotationVector2();
            Dust d = Dust.NewDustPerfect(Player.Center + v * 22, DustID.MagicMirror, v * 2, 100, color, 1.15f); d.noGravity = true;
        }
        SoundEngine.PlaySound(SoundID.Item8 with { Volume = .45f }, Player.Center);
    }
}
