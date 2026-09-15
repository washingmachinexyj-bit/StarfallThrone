using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Divine.Equipment;

public sealed class DivineEquipmentKeys : ModSystem
{
    public static ModKeybind Skill;
    public override void Load() => Skill = KeybindLoader.RegisterKeybind(Mod, "DivineArmorSkill", "K");
    public override void Unload() => Skill = null;
}

public sealed class DivineEquipmentPlayer : ModPlayer
{
    // Cooldowns survive death, loadouts, unequipping and world changes. No buffs hold authority.
    public readonly bool[] Expert = new bool[3];
    public readonly int[] Cooldowns = new int[5];
    public int SetTier = -1, Shield, Calm, Heat, HeatTime, MarkTime;
    public Vector2 MarkPosition;
    public Guid MarkWorld;
    public int Refund, RefundWait, RefundTick;
    public int MeleeCombo, Pressure, ArrowTarget = -1, ArrowType, ArrowCount;
    public bool ArrowReady;
    public int GunCadence, MagicCadence;
    private ulong tickStamp = ulong.MaxValue, lastHeatTick = ulong.MaxValue, lastRequestTick = ulong.MaxValue;
    private bool partialShield;

    public override void ResetEffects() { Array.Clear(Expert); SetTier = -1; }
    private void Tick()
    {
        if (tickStamp == Main.GameUpdateCount) return;
        tickStamp = Main.GameUpdateCount;
        for (int i = 0; i < 5; i++) if (Cooldowns[i] > 0) Cooldowns[i]--;
    }
    public override void PreUpdate() => Tick();
    public override void PostUpdate()
    {
        if (!Player.active || Player.dead) return;
        Calm = SetTier == 0 ? Math.Min(480, Calm + 1) : 0;
        if (!Expert[0]) Shield = 0;
        else if (Cooldowns[0] == 0 && Shield == 0) { Shield = 30; Cooldowns[0] = 1200; }
        if (SetTier != 1) { Heat = 0; HeatTime = 0; }
        if (HeatTime > 0) HeatTime--;
        if (SetTier != 2) MarkTime = 0;
        if (MarkTime > 0) MarkTime--;
        if (!Expert[1]) { Refund = RefundWait = RefundTick = 0; }
        if (RefundWait > 0) RefundWait--;
        else if (Refund > 0 && ++RefundTick >= 6 && Player.whoAmI == Main.myPlayer)
        {
            RefundTick = 0; Refund--;
            if (Player.statLife > 0 && Player.statLife < Player.statLifeMax2)
            { Player.statLife++; Player.HealEffect(1); }
        }
        if (Player.whoAmI == Main.myPlayer && !Main.dedServ && Main.GameUpdateCount % 90 == 0 && (Heat == 100 || MarkTime > 0))
            CombatText.NewText(Player.Hitbox, DivineEquipmentData.Color(SetTier == 1 ? 1 : 2), Language.GetTextValue("Mods.StarfallThrone.DivineEquipment." + (MarkTime > 0 ? "ReturnReady" : "HeatReady")), false, true);
    }
    public override void PostUpdateEquips()
    { if (SetTier == 1 && HeatTime > 0) Player.GetDamage(DamageClass.Generic) += .18f; }
    public override void UpdateDead()
    {
        Tick(); ResetEffects(); Shield = Calm = Heat = HeatTime = MarkTime = Refund = RefundWait = RefundTick = 0;
        MeleeCombo = Pressure = GunCadence = MagicCadence = ArrowCount = 0; ArrowTarget = -1; ArrowReady = false;
    }
    public override void SaveData(TagCompound tag) => tag["divineEquipmentCooldowns"] = (int[])Cooldowns.Clone();
    public override void LoadData(TagCompound tag)
    {
        int[] saved = tag.GetIntArray("divineEquipmentCooldowns");
        for (int i = 0; i < Math.Min(saved.Length, 5); i++) Cooldowns[i] = Math.Clamp(saved[i], 0, 3600);
    }
    public override void OnEnterWorld() { MarkTime = HeatTime = Refund = RefundWait = Shield = 0; }
    public override void CopyClientState(ModPlayer targetCopy) => Array.Copy(Cooldowns, ((DivineEquipmentPlayer)targetCopy).Cooldowns, 5);
    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        int[] previous = ((DivineEquipmentPlayer)clientPlayer).Cooldowns;
        for (int i = 0; i < 5; i++) if (Cooldowns[i] > previous[i]) { SendCooldowns(2, -1, -1); break; }
    }
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendCooldowns(Main.netMode == NetmodeID.Server ? (byte)3 : (byte)2, toWho, fromWho);
    private void SendCooldowns(byte mode, int toWho, int fromWho)
    {
        var p = Mod.GetPacket(); p.Write((byte)41); p.Write(mode); p.Write((byte)Player.whoAmI);
        foreach (int cd in Cooldowns) p.Write((ushort)Math.Clamp(cd, 0, 3600)); p.Send(toWho, fromWho);
    }
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (DivineEquipmentKeys.Skill == null || !DivineEquipmentKeys.Skill.JustPressed || Player.dead) return;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        { var p = Mod.GetPacket(); p.Write((byte)41); p.Write((byte)0); p.Write((byte)Player.whoAmI); p.Send(); }
        else TrySkill();
    }
    public bool TrySkill()
    {
        if (!Player.active || Player.dead || Main.netMode == NetmodeID.MultiplayerClient) return false;
        int tier = DivineEquipmentData.EquippedTier(Player);
        if (tier == 1)
        {
            if (Cooldowns[3] > 0 || Heat < 100) return false;
            Heat = 0; HeatTime = 480; Cooldowns[3] = 1920; // 8 seconds active, then 24 seconds recovery.
        }
        else if (tier == 2)
        {
            if (MarkTime > 0)
            {
                MarkTime = 0;
                if (!CanReturn(MarkPosition)) return false;
                bool immune = Player.immune, blink = Player.immuneNoBlink; int immuneTime = Player.immuneTime;
                int[] hurt = (int[])Player.hurtCooldowns.Clone();
                if (Main.netMode == NetmodeID.Server) RemoteClient.CheckSection(Player.whoAmI, MarkPosition);
                Player.Teleport(MarkPosition, TeleportationStyleID.RodOfDiscord);
                Player.immune = immune; Player.immuneNoBlink = blink; Player.immuneTime = immuneTime;
                Array.Copy(hurt, Player.hurtCooldowns, hurt.Length);
                Player.fallStart = (int)(Player.position.Y / 16);
            }
            else
            {
                if (Cooldowns[4] > 0) return false;
                MarkPosition = Player.position; MarkWorld = Main.ActiveWorldFileData.UniqueId;
                MarkTime = 240; Cooldowns[4] = 2700;
            }
        }
        else return false;
        if (Main.netMode == NetmodeID.Server) SendState();
        return true;
    }
    public bool CanReturn(Vector2 position) => MarkWorld == Main.ActiveWorldFileData.UniqueId && float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        Vector2.DistanceSquared(Player.position, position) <= 1600 * 1600 && position.X >= 32 && position.Y >= 32 &&
        position.X + Player.width < Main.maxTilesX * 16 - 32 && position.Y + Player.height < Main.maxTilesY * 16 - 32 &&
        !Collision.SolidCollision(position, Player.width, Player.height);
    private void SendState()
    {
        var p = Mod.GetPacket(); p.Write((byte)41); p.Write((byte)1); p.Write((byte)Player.whoAmI);
        p.Write(Player.position.X); p.Write(Player.position.Y); p.Write((byte)Heat); p.Write((ushort)HeatTime); p.Write((ushort)MarkTime);
        p.Write(MarkPosition.X); p.Write(MarkPosition.Y); foreach (int cd in Cooldowns) p.Write((ushort)cd); p.Send();
    }
    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        byte mode = reader.ReadByte(), who = reader.ReadByte();
        if (who >= Main.maxPlayers) return;
        var s = Main.player[who].GetModPlayer<DivineEquipmentPlayer>();
        if (mode == 0)
        {
            if (Main.netMode != NetmodeID.Server || sender != who || !s.Player.active || s.Player.dead ||
                s.lastRequestTick != ulong.MaxValue && Main.GameUpdateCount - s.lastRequestTick < 8) return;
            s.lastRequestTick = Main.GameUpdateCount; s.TrySkill();
        }
        else if (mode == 1 && Main.netMode == NetmodeID.MultiplayerClient)
        {
            Vector2 pos = new(reader.ReadSingle(), reader.ReadSingle()); int heat = reader.ReadByte(), time = reader.ReadUInt16(), markTime = reader.ReadUInt16();
            Vector2 mark = new(reader.ReadSingle(), reader.ReadSingle()); int[] cds = new int[5];
            for (int i = 0; i < 5; i++) cds[i] = Math.Min(3600, (int)reader.ReadUInt16());
            if (!float.IsFinite(pos.X) || !float.IsFinite(pos.Y) || !float.IsFinite(mark.X) || !float.IsFinite(mark.Y) ||
                pos.X < 0 || pos.Y < 0 || pos.X > Main.maxTilesX * 16 || pos.Y > Main.maxTilesY * 16 || heat > 100 || time > 480 || markTime > 240) return;
            s.Player.position = pos; s.Player.fallStart = (int)(pos.Y / 16); s.Heat = heat; s.HeatTime = time; s.MarkTime = markTime;
            s.MarkPosition = mark; s.MarkWorld = Main.ActiveWorldFileData.UniqueId; Array.Copy(cds, s.Cooldowns, 5);
        }
        else if (mode == 4)
        {
            int target = reader.ReadInt16(), identity = reader.ReadInt32();
            if (Main.netMode != NetmodeID.Server || sender != who || !s.Player.active || s.Player.dead ||
                DivineEquipmentData.EquippedTier(s.Player) != 1 || target < 0 || target >= Main.maxNPCs || !Main.npc[target].CanBeChasedBy()) return;
            NPC npc = Main.npc[target]; bool witness = false;
            if (identity == -1)
                witness = s.Player.itemAnimation > 0 && !s.Player.HeldItem.noMelee && s.Player.HeldItem.damage > 0 &&
                    Vector2.DistanceSquared(s.Player.Center, npc.Center) < 180 * 180 && Collision.CanHitLine(s.Player.Center, 1, 1, npc.Center, 1, 1);
            else
                foreach (Projectile q in Main.ActiveProjectiles)
                    if (q.owner == who && q.identity == identity && q.damage > 0 && VoyageEquipmentProjectileOrigin.IsOwnedPrimary(s.Player, q) &&
                        q.ModProjectile?.CanDamage() != false)
                    {
                        Rectangle tolerance = npc.Hitbox; tolerance.Inflate(48, 48);
                        witness = (q.ModProjectile?.Colliding(q.Hitbox, tolerance) ?? q.Hitbox.Intersects(tolerance)) &&
                            Collision.CanHitLine(q.Center, 1, 1, npc.Center, 1, 1); break;
                    }
            // No client supplies a heat value. The server independently checks current equipment,
            // live owned primary attack geometry and a 6-tick award budget. Invalid/stale evidence gives no credit.
            if (witness && s.AwardHeat())
            { var p = s.Mod.GetPacket(); p.Write((byte)41); p.Write((byte)5); p.Write(who); p.Write((byte)s.Heat); p.Send(who); }
        }
        else if (mode == 5 && Main.netMode == NetmodeID.MultiplayerClient)
            s.Heat = Math.Clamp((int)reader.ReadByte(), 0, 100);
        else if (mode is 2 or 3)
        {
            if (mode == 2 && (Main.netMode != NetmodeID.Server || sender != who) || mode == 3 && Main.netMode != NetmodeID.MultiplayerClient) return;
            // An untrusted client may only lengthen its own cooldowns, never supply heat, position or a ready shield.
            int[] cds = new int[5]; for (int i = 0; i < 5; i++) cds[i] = Math.Min(3600, (int)reader.ReadUInt16());
            for (int i = 0; i < 5; i++) s.Cooldowns[i] = Math.Max(s.Cooldowns[i], cds[i]);
            if (mode == 2) s.SendCooldowns(3, -1, -1);
        }
    }
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        partialShield = false;
        modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
        {
            if (Expert[2] && Cooldowns[2] == 0 && info.Damage >= 60)
            { info.Damage -= Math.Min(120, (int)(info.Damage * .4f)); Cooldowns[2] = 2100; Shatter(2); }
            if (Expert[0] && Shield > 0 && info.Damage > Shield)
            { info.Damage -= Shield; Shield = 0; partialShield = true; Shatter(0); }
        };
    }
    public override bool ConsumableDodge(Player.HurtInfo info)
    {
        if (!Expert[0] || partialShield || info.Damage <= 0 || Shield < info.Damage) return false;
        Shield -= info.Damage; Calm = 0; Refund = RefundWait = 0;
        // No immunity is granted: the finite 30-point barrier alone absorbs this hit.
        Shatter(0); return true;
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        Calm = 0; Heat = Math.Max(0, Heat - 25); Refund = RefundWait = RefundTick = 0;
    }
    public override void PostHurt(Player.HurtInfo info)
    {
        if (!Expert[1] || Cooldowns[1] > 0 || info.PvP || Player.dead || Player.statLife <= 0 || info.Damage <= 0 || info.DamageSource == null) return;
        if (!info.DamageSource.TryGetCausingEntity(out Entity source) || source is not NPC && source is not Projectile) return;
        if (source is NPC npc && (npc.friendly || npc.damage <= 0)) return;
        if (source is Projectile q && (!q.hostile || q.owner == Player.whoAmI || q.trap || q.damage <= 0)) return;
        Refund = Math.Min(60, info.Damage / 4); RefundWait = 360; RefundTick = 0;
        if (Refund > 0) Cooldowns[1] = 1200;
    }
    private void Shatter(int index)
    {
        if (Main.dedServ) return;
        for (int i = 0; i < 12; i++)
        { var d = Dust.NewDustPerfect(Player.Center, DustID.MagicMirror, (i * MathHelper.TwoPi / 12).ToRotationVector2() * 3, 100, DivineEquipmentData.Color(index)); d.noGravity = true; }
    }
    public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => RecordHit(target, damageDone, -1);
    public override void OnHitNPCWithProj(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(Player, projectile)) return;
        RecordHit(target, damageDone, projectile.identity);
    }
    private bool AwardHeat()
    {
        if (HeatTime > 0 || lastHeatTick != ulong.MaxValue && Main.GameUpdateCount - lastHeatTick < 6) return false;
        Heat = Math.Min(100, Heat + 5); lastHeatTick = Main.GameUpdateCount; return true;
    }
    private void RecordHit(NPC target, int damage, int identity)
    {
        if (!Player.active || Player.dead || damage <= 0 || target.friendly || target.type == NPCID.TargetDummy || !target.CanBeChasedBy()) return;
        if (SetTier == 1 && AwardHeat() && Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
        {
            var p = Mod.GetPacket(); p.Write((byte)41); p.Write((byte)4); p.Write((byte)Player.whoAmI);
            p.Write((short)target.whoAmI); p.Write(identity); p.Send();
        }
        if (SetTier == 0 && Calm >= 480 && Player.whoAmI == Main.myPlayer)
        {
            Calm = 0;
            DivineArsenal.Launch(Player.GetSource_Misc("DivineDewSet"), Player.whoAmI, 0, DivineShotKind.DewProc,
                Player.Center, (target.Center - Player.Center).SafeNormalize(Vector2.UnitX) * 10, 18, 0);
        }
    }
}
