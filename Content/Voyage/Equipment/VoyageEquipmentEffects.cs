using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Voyage.Equipment;

// Tracks only origin metadata. It never changes damage/AI for unrelated projectiles.
public sealed class VoyageEquipmentProjectileOrigin : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    public bool FromSentry;
    public bool FromEquipment;
    public bool FromSecondary;
    private int manualTarget = -1, responseTime;
    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        FromEquipment = projectile.ModProjectile is VoyageEquipmentPulse;
        FromSecondary = FromEquipment || projectile.ModProjectile is IVoyageWeaponProjectile { Secondary: true };
        if (source is EntitySource_Parent { Entity: Projectile parent })
        {
            // A cross-owner child cannot inherit a sentry bonus or become a primary equipment trigger.
            if (parent.owner != projectile.owner) { FromSecondary = true; return; }
            var origin = parent.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>();
            FromSentry = parent.sentry || origin.FromSentry;
            FromEquipment |= parent.ModProjectile is VoyageEquipmentPulse || origin.FromEquipment;
            FromSecondary |= FromEquipment || origin.FromSecondary || parent.ModProjectile is IVoyageWeaponProjectile { Secondary: true };
        }
    }
    public static bool Secondary(Projectile projectile) => projectile.ModProjectile is VoyageEquipmentPulse or IVoyageWeaponProjectile { Secondary: true } ||
        projectile.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromEquipment || projectile.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromSecondary;
    public static bool IsOwnedPrimary(Player owner, Projectile projectile) => owner != null && projectile != null && owner.active && !owner.dead &&
        owner.whoAmI >= 0 && owner.whoAmI < Main.maxPlayers && projectile.owner == owner.whoAmI &&
        projectile.friendly && !projectile.hostile && !projectile.npcProj && !projectile.trap && !Secondary(projectile);
    public static bool CanTrigger(Player owner, Projectile projectile) => IsOwnedPrimary(owner, projectile) &&
        Main.netMode != NetmodeID.Server && owner.whoAmI == Main.myPlayer;
    public override void SendExtraAI(Projectile projectile, Terraria.ModLoader.IO.BitWriter bitWriter, System.IO.BinaryWriter binaryWriter) { bitWriter.WriteBit(FromSentry); bitWriter.WriteBit(FromEquipment); bitWriter.WriteBit(FromSecondary); }
    public override void ReceiveExtraAI(Projectile projectile, Terraria.ModLoader.IO.BitReader bitReader, System.IO.BinaryReader binaryReader) { FromSentry = bitReader.ReadBit(); FromEquipment = bitReader.ReadBit(); FromSecondary = bitReader.ReadBit(); }
    public override void PostAI(Projectile projectile)
    {
        if (Main.netMode == NetmodeID.Server || projectile.owner < 0 || projectile.owner >= Main.maxPlayers || projectile.owner != Main.myPlayer || !projectile.minion || projectile.sentry || projectile.npcProj || projectile.trap) return;
        Player player = Main.player[projectile.owner];
        var state = player.GetModPlayer<VoyageEquipmentPlayer>();
        if (!state.Expert[4]) return;
        int target = player.MinionAttackTargetNPC;
        if (target != manualTarget) { manualTarget = target; responseTime = 36; }
        if (responseTime <= 0 || target < 0 || target >= Main.maxNPCs) return;
        responseTime--;
        NPC npc = Main.npc[target];
        if (!npc.CanBeChasedBy() || Vector2.DistanceSquared(projectile.Center, npc.Center) > 900 * 900 ||
            !Collision.CanHitLine(projectile.position, projectile.width, projectile.height, npc.position, npc.width, npc.height)) return;
        // Only assist an existing pursuit vector, never redirect orbiting/contact windup AI or move stationary sentries.
        Vector2 toward = (npc.Center - projectile.Center).SafeNormalize(Vector2.Zero);
        float speed = projectile.velocity.Length();
        if (speed > 2 && speed < 20 && Vector2.Dot(projectile.velocity / speed, toward) > .8f)
            projectile.velocity *= Math.Min(1.06f, 20 / speed);
    }
}

public sealed class VoyageEquipmentPulse : ModProjectile
{
    // 0 weak echo; 1 short arc; 2 four-second node; 3 node bullet; 4 harmless target-lock visual.
    public int Kind => (int)Projectile.ai[0];
    public int Style => Math.Clamp((int)Projectile.ai[1], 0, 3);
    public int Target => (int)Projectile.ai[2] - 1;
    private int age;
    private Vector2 start;
    public override string Texture => VoyageCatalog.Root + "Expert3";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 14;
        Projectile.friendly = true; Projectile.tileCollide = true; Projectile.ignoreWater = true;
        Projectile.penetrate = 1; Projectile.timeLeft = 120;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
    {
        Projectile.DamageType = VoyageEquipmentData.Class(Style);
        start = Projectile.Center;
        if (Kind is 1 or 2 or 4) Projectile.tileCollide = false;
        if (Kind == 1) { Projectile.timeLeft = 16; Projectile.penetrate = -1; }
        if (Kind == 2) { Projectile.timeLeft = 240; Projectile.penetrate = -1; }
        if (Kind == 4) { Projectile.timeLeft = 50; Projectile.friendly = false; }
    }
    public override void SendExtraAI(System.IO.BinaryWriter writer) { writer.Write(age); writer.Write(start.X); writer.Write(start.Y); writer.Write(Projectile.timeLeft); }
    public override void ReceiveExtraAI(System.IO.BinaryReader reader)
    {
        age = reader.ReadInt32(); start = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        Projectile.timeLeft = Math.Clamp(reader.ReadInt32(), 1, 240);
        Projectile.DamageType = VoyageEquipmentData.Class(Style);
        Projectile.tileCollide = Kind is not (1 or 2 or 4);
        Projectile.penetrate = Kind is 1 or 2 ? -1 : 1;
        Projectile.friendly = Kind != 4;
    }
    public static int Spawn(Player player, Vector2 position, Vector2 velocity, int damage, int kind, int style, int target)
    {
        if (player == null || !player.active || player.dead || player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers ||
            player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server || kind < 0 || kind > 4 || style < 0 || style > 3 ||
            target < -1 || target >= Main.maxNPCs || damage < 0 ||
            !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y)) return -1;
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles)
        {
            if (p.owner != player.whoAmI || p.ModProjectile is not VoyageEquipmentPulse other) continue;
            if (++count >= 24 || kind == 2 && other.Kind == 2) return -1;
        }
        return Projectile.NewProjectile(player.GetSource_Misc("VoyageEquipment"), position, velocity, ModContent.ProjectileType<VoyageEquipmentPulse>(),
            damage, 1f, player.whoAmI, kind, style, target + 1);
    }
    public override bool ShouldUpdatePosition() => Kind is 0 or 3;
    public override bool? CanDamage() => Kind is 2 or 4 ? false : null;
    public override bool? CanCutTiles() => false;
    public override void AI()
    {
        age++;
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead) { Projectile.Kill(); return; }
        var state = player.GetModPlayer<VoyageEquipmentPlayer>();
        if (Kind == 2)
        {
            if (state.SetTier != 3) { Projectile.Kill(); return; }
            Projectile.Center = player.Center + new Vector2(player.direction * 62, -60 + MathF.Sin(age * .05f) * 8);
            if (age % 30 == 0 && Projectile.owner == Main.myPlayer)
            {
                NPC target = FindTarget(Projectile.Center, Target, 850);
                if (target != null) Spawn(player, Projectile.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 15, Projectile.damage, 3, Style, target.whoAmI);
            }
        }
        else if (Kind == 4)
        {
            if (Target >= 0 && Target < Main.maxNPCs && Main.npc[Target].active) Projectile.Center = Main.npc[Target].Center;
        }
        else if (Kind == 1) Projectile.Center = player.Center;
        else if (age > 5 && age < 35)
        {
            NPC target = FindTarget(Projectile.Center, Target, 600);
            if (target != null)
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14, .08f);
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, Tint.ToVector3() * .3f);
    }
    private static NPC FindTarget(Vector2 position, int preferred, float range)
    {
        if (preferred >= 0 && preferred < Main.maxNPCs)
        {
            NPC npc = Main.npc[preferred];
            if (npc.CanBeChasedBy() && Vector2.DistanceSquared(npc.Center, position) < range * range &&
                Collision.CanHitLine(position, 1, 1, npc.position, npc.width, npc.height)) return npc;
        }
        NPC result = null; float distance = range * range;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            float next = Vector2.DistanceSquared(npc.Center, position);
            if (npc.CanBeChasedBy() && next < distance && Collision.CanHitLine(position, 1, 1, npc.position, npc.width, npc.height)) { result = npc; distance = next; }
        }
        return result;
    }
    private Color Tint => Style switch { 0 => Color.Gold, 1 => Color.LightSkyBlue, 2 => Color.Violet, _ => Color.LightGreen };
    private Vector2 ArcEnd()
    {
        float[] distances = new float[3];
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Collision.LaserScan(Projectile.Center, direction, 8, 280, distances);
        float distance = Math.Min(distances[0], Math.Min(distances[1], distances[2]));
        return Projectile.Center + direction * distance;
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Kind != 1) return null;
        float distance = 0;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, ArcEnd(), 12, ref distance);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 point = Projectile.Center - Main.screenPosition;
        float fade = Math.Min(1, Projectile.timeLeft / 12f);
        if (Kind == 1)
        {
            CombatDrawing.Line(Main.spriteBatch, point, ArcEnd() - Main.screenPosition, Tint * fade, 6);
            CombatDrawing.Line(Main.spriteBatch, point, ArcEnd() - Main.screenPosition, Color.White * fade, 2);
        }
        else if (Kind == 2)
        {
            Texture2D texture = ModContent.Request<Texture2D>(VoyageCatalog.Root + "Expert9").Value;
            Main.EntitySpriteDraw(texture, point, null, Tint * fade, MathF.Sin(age * .025f) * .15f, texture.Size() / 2, .8f, SpriteEffects.None);
            CombatDrawing.Circle(Main.spriteBatch, point, 22, Tint * .6f, 2, age * .04f, .4f);
        }
        else if (Kind == 4)
        {
            CombatDrawing.Circle(Main.spriteBatch, point, 35 + Projectile.timeLeft * .3f, Tint * fade, 2, age * .04f, .3f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 v = (MathHelper.PiOver2 * i).ToRotationVector2();
                CombatDrawing.Line(Main.spriteBatch, point + v * 30, point + v * 45, Tint * fade, 2);
            }
        }
        else
        {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            CombatDrawing.Line(Main.spriteBatch, point - direction * 20, point + direction * 6, Tint * fade, 5);
            CombatDrawing.Circle(Main.spriteBatch, point, Kind == 0 ? 6 : 4, Color.White * fade, 2);
        }
        return false;
    }
}

// A light-weight character-attached overlay gives shields/stances a visible state without new art files.
public sealed class VoyageEquipmentDrawLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);
    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        if (drawInfo.shadow != 0 || player.dead) return;
        var s = player.GetModPlayer<VoyageEquipmentPlayer>();
        int shield = s.EffectiveShield;
        if (shield <= 0 && s.FortifyTime <= 0 && !s.SteadyReady && s.DroneTime <= 0 && s.MirrorTime <= 0) return;
        Vector2 center = drawInfo.Position + player.Size / 2 - Main.screenPosition;
        Color color = s.FortifyTime > 0 ? Color.Gold : Color.LightCyan;
        if (shield > 0 || s.FortifyTime > 0 || s.SteadyReady)
            for (int i = 0; i < 24; i++)
            {
                float a = MathHelper.TwoPi * i / 24;
                if (s.FortifyTime > 0 && Vector2.Dot(a.ToRotationVector2(), s.FortifyDirection) < 0) continue;
                Vector2 p = center + new Vector2(MathF.Cos(a) * 29, MathF.Sin(a) * 39);
                var data = new DrawData(TextureAssets.MagicPixel.Value, p, new Rectangle(0,0,1,1), color * .65f, a, Vector2.Zero, new Vector2(3,3), SpriteEffects.None, 0);
                drawInfo.DrawDataCache.Add(data);
            }
        if (s.DroneTime > 0 || s.MirrorTime > 0)
        {
            Texture2D texture = ModContent.Request<Texture2D>(VoyageCatalog.Root + (s.DroneTime > 0 ? "Expert4" : "Expert7")).Value;
            float a = (float)Main.GlobalTimeWrappedHourly * 2;
            Vector2 p = center + new Vector2(MathF.Cos(a) * 48, -40 + MathF.Sin(a) * 12);
            drawInfo.DrawDataCache.Add(new DrawData(texture, p, null, Color.White, 0, texture.Size()/2, .65f, SpriteEffects.None, 0));
        }
    }
}
