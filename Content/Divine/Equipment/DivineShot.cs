#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Divine.Equipment;

public enum DivineShotKind { Swing, Crescent, Arrow, FallingDew, Charge, WaterRing, HeatWave, Bullet, Flame, Explosion, DelayedCut, Echo, ClockBlade, MinionLance, DewProc }

public class DivineShot : ModProjectile, IVoyageWeaponProjectile
{
    public int Weapon => Math.Clamp((int)Projectile.ai[0], 0, 11);
    public int WeaponIndex => -1; // Only the interface's Secondary contract is shared; never impersonate a Voyage weapon ID.
    public DivineShotKind Kind => (DivineShotKind)(int)Projectile.ai[1];
    public bool Secondary => Kind is DivineShotKind.Crescent or DivineShotKind.FallingDew or DivineShotKind.Explosion or DivineShotKind.DelayedCut or DivineShotKind.Echo or DivineShotKind.MinionLance or DivineShotKind.DewProc;
    public Vector2 Focus;
    public int Age;
    private bool initialized, awarded;
    private float angle;
    private int duration;
    public override string Texture => DivineArsenal.Art(0);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18; Projectile.friendly = true; Projectile.ignoreWater = true;
        Projectile.penetrate = 1; Projectile.timeLeft = 180; Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    private void InitializeShot()
    {
        if (initialized) return;
        initialized = true; angle = Projectile.velocity.ToRotation();
        Projectile.DamageType = Kind == DivineShotKind.DewProc ? DamageClass.Generic : DivineEquipmentData.Class(Weapon % 4);
        duration = Kind switch { DivineShotKind.Swing => Weapon == 4 ? 36 : Weapon == 8 ? 30 : 22,
            DivineShotKind.Charge => 96, DivineShotKind.WaterRing => 28, DivineShotKind.HeatWave => 20,
            DivineShotKind.Explosion => 12, DivineShotKind.DelayedCut => 32, DivineShotKind.Echo => 34,
            DivineShotKind.ClockBlade => 150, DivineShotKind.FallingDew => 90, DivineShotKind.Crescent => 30, DivineShotKind.DewProc => 60, _ => 120 };
        Projectile.timeLeft = duration;
        if (Kind is DivineShotKind.Swing or DivineShotKind.Charge or DivineShotKind.WaterRing or DivineShotKind.DelayedCut or DivineShotKind.Echo or DivineShotKind.Explosion)
        { Projectile.tileCollide = false; Projectile.penetrate = -1; }
        if (Kind == DivineShotKind.ClockBlade) { Projectile.penetrate = 3; Projectile.localNPCHitCooldown = 20; }
    }
    public override void AI()
    {
        InitializeShot();
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers || !Main.player[Projectile.owner].active || Main.player[Projectile.owner].dead || !Enum.IsDefined(Kind))
        { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner]; Age++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        switch (Kind)
        {
            case DivineShotKind.Swing:
                if (p.HeldItem.type != DivineCatalog.Weapon(Weapon)) { Projectile.Kill(); return; }
                float arc = MathHelper.Lerp(-1.1f, 1.1f, Age / (float)duration) * (Weapon == 0 && Projectile.ai[2] == 1 ? -1 : 1);
                Projectile.rotation = angle + arc; Projectile.Center = p.MountedCenter + Projectile.rotation.ToRotationVector2() * (Weapon == 0 ? 40 : 62);
                p.heldProj = Projectile.whoAmI; break;
            case DivineShotKind.Charge:
                if (p.HeldItem.type != DivineCatalog.Weapon(2)) { Projectile.Kill(); return; }
                Projectile.Center = p.MountedCenter + angle.ToRotationVector2() * 30; p.heldProj = Projectile.whoAmI;
                if (p.whoAmI == Main.myPlayer)
                {
                    bool released = !p.channel || Age >= 90;
                    if (!released && Age % 30 == 0 && !p.CheckMana(p.HeldItem, 4, true, false)) released = true;
                    if (released)
                    {
                        float strength = .8f + Math.Min(60, Age) / 60f * .7f;
                        DivineArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, 2, DivineShotKind.WaterRing,
                            p.MountedCenter + angle.ToRotationVector2() * 70, Vector2.Zero, (int)(Projectile.damage * strength), Projectile.knockBack);
                        Projectile.Kill();
                    }
                }
                break;
            case DivineShotKind.FallingDew:
                if (Age <= 18) { Projectile.velocity = Vector2.Zero; break; }
                if (Age == 19) Projectile.velocity = (Focus - Projectile.Center).SafeNormalize(Vector2.UnitY) * 8;
                Projectile.velocity.Y += .16f; break;
            case DivineShotKind.Crescent: Projectile.rotation += MathHelper.PiOver4; break;
            case DivineShotKind.Flame:
                if (Projectile.ai[2] == 1) Projectile.velocity.Y += .06f;
                else Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Age * .22f) * .006f);
                break;
            case DivineShotKind.ClockBlade:
                if (Projectile.ai[2] == 0 && Age > 14) Projectile.velocity *= .80f;
                else if (Projectile.ai[2] == 1)
                {
                    if (Vector2.DistanceSquared(Projectile.Center, Focus) < 22 * 22) { Projectile.Kill(); break; }
                    Projectile.velocity = (Focus - Projectile.Center).SafeNormalize(Vector2.UnitX) * 20;
                }
                Projectile.rotation = angle + Age * .16f; break;
        }
        if (Age >= duration && Kind != DivineShotKind.Charge) Projectile.Kill();
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, DivineEquipmentData.Color(Weapon / 4).ToVector3() * .25f);
    }
    public override bool ShouldUpdatePosition() => Kind is not (DivineShotKind.Swing or DivineShotKind.Charge or DivineShotKind.WaterRing or DivineShotKind.DelayedCut or DivineShotKind.Echo or DivineShotKind.Explosion);
    public override bool? CanCutTiles() => false;
    public override bool? CanDamage() => Kind == DivineShotKind.Charge || Kind == DivineShotKind.Swing && Age < 3 ||
        Kind == DivineShotKind.FallingDew && Age <= 18 || Kind is DivineShotKind.DelayedCut or DivineShotKind.Echo && Age <= 20 ? false : null;
    public override bool? CanHitNPC(NPC target)
    {
        if (!Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height)) return false;
        if (Kind == DivineShotKind.Swing && !Collision.CanHitLine(Main.player[Projectile.owner].MountedCenter, 1, 1, target.Center, 1, 1)) return false;
        return null;
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        if (Kind == DivineShotKind.Swing)
        {
            Vector2 start = Main.player[Projectile.owner].MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Weapon == 0 ? 82 : 122);
            return SegmentHits(targetHitbox, start, end, Weapon == 0 ? 18 : 26);
        }
        if (Kind == DivineShotKind.DelayedCut)
            return SegmentHits(targetHitbox, Projectile.Center - angle.ToRotationVector2() * 75,
                Projectile.Center + angle.ToRotationVector2() * 75, 22);
        if (Kind == DivineShotKind.WaterRing)
        {
            float radius = 12 + Age * 4;
            Vector2 nearest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            Vector2 farthest = new(Math.Max(Math.Abs(targetHitbox.Left - Projectile.Center.X), Math.Abs(targetHitbox.Right - Projectile.Center.X)),
                Math.Max(Math.Abs(targetHitbox.Top - Projectile.Center.Y), Math.Abs(targetHitbox.Bottom - Projectile.Center.Y)));
            return Vector2.DistanceSquared(nearest, Projectile.Center) <= (radius + 12) * (radius + 12) && farthest.LengthSquared() >= Math.Max(0, radius - 12) * Math.Max(0, radius - 12);
        }
        if (Kind is DivineShotKind.Echo or DivineShotKind.Explosion)
        { float radius = Kind == DivineShotKind.Echo ? 72 : 54; return Vector2.DistanceSquared(Projectile.Center, Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight())) <= radius * radius; }
        return null;
    }
    private static bool SegmentHits(Rectangle box, Vector2 start, Vector2 end, float width)
    {
        // Terraria's native edge/corner test misses a finite segment wholly inside the AABB.
        // Endpoint containment supplies the missing case without expanding range or bypassing LOS.
        bool Contains(Vector2 point) => point.X >= box.Left && point.X <= box.Right && point.Y >= box.Top && point.Y <= box.Bottom;
        if (Contains(start) || Contains(end)) return true;
        float collision = 0;
        return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), start, end, width, ref collision);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Projectile.owner != Main.myPlayer || Secondary || target.friendly || target.type == NPCID.TargetDummy) return;
        Player p = Main.player[Projectile.owner]; var s = p.GetModPlayer<DivineEquipmentPlayer>();
        void Proc(DivineShotKind kind, float strength, Vector2 at, Vector2 velocity, Vector2? focus = null)
            => DivineArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, Weapon, kind, at, velocity, (int)(Projectile.damage * strength), Projectile.knockBack, focus: focus);
        if (Kind == DivineShotKind.Swing)
        {
            if (Weapon == 0 && !awarded)
            {
                awarded = true; s.MeleeCombo = (s.MeleeCombo + 1) % 3;
                if (s.MeleeCombo == 0) Proc(DivineShotKind.Crescent, .75f, p.Center, angle.ToRotationVector2() * 12);
            }
            else if (Weapon == 4 && !awarded) { awarded = true; s.Pressure = Math.Min(100, s.Pressure + 20); }
            else if (Weapon == 8) Proc(DivineShotKind.DelayedCut, .6f, target.Center, angle.ToRotationVector2());
        }
        else if (Weapon == 1 && Kind == DivineShotKind.Arrow)
        {
            if (Projectile.ai[2] == 1)
            {
                Proc(DivineShotKind.FallingDew, .4f, target.Center + new Vector2(-35, -110), Vector2.Zero, target.Center);
                Proc(DivineShotKind.FallingDew, .4f, target.Center + new Vector2(35, -130), Vector2.Zero, target.Center);
            }
            if (s.ArrowTarget != target.whoAmI || s.ArrowType != target.type) { s.ArrowTarget = target.whoAmI; s.ArrowType = target.type; s.ArrowCount = 0; }
            if (++s.ArrowCount >= 3) { s.ArrowCount = 0; s.ArrowReady = true; }
        }
        else if (Weapon == 5 && Kind == DivineShotKind.Bullet)
        {
            var mark = target.GetGlobalNPC<DivineWeaponMarks>();
            if (mark.GunHit(Projectile.owner, Projectile.ai[2] == 1)) Proc(DivineShotKind.Explosion, .65f, target.Center, Vector2.Zero);
        }
        else if (Weapon == 6 && Kind == DivineShotKind.Flame)
        {
            if (target.GetGlobalNPC<DivineWeaponMarks>().FlameHit(Projectile.owner, Projectile.ai[2] == 1)) Proc(DivineShotKind.Explosion, .55f, target.Center, Vector2.Zero);
        }
        else if (Weapon == 9 && Kind == DivineShotKind.Arrow) Proc(DivineShotKind.Echo, .5f, target.Center, Vector2.Zero);
    }
    public override void SendExtraAI(BinaryWriter writer)
    { writer.Write(Focus.X); writer.Write(Focus.Y); writer.Write((ushort)Math.Clamp(Age, 0, 180)); writer.Write(angle); writer.Write(awarded); writer.Write((ushort)Math.Clamp(Projectile.timeLeft, 0, 180)); writer.Write((sbyte)Projectile.penetrate); }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Vector2 focus = new(reader.ReadSingle(), reader.ReadSingle()); int age = reader.ReadUInt16(); float a = reader.ReadSingle(); bool reward = reader.ReadBoolean(); int lifetime = reader.ReadUInt16(), penetration = reader.ReadSByte();
        if (!float.IsFinite(focus.X) || !float.IsFinite(focus.Y) || !float.IsFinite(a) || age > 180 || lifetime > 180 || penetration is < -1 or > 3) { Projectile.Kill(); return; }
        InitializeShot(); Focus = focus; Age = age; angle = a; awarded = reward; Projectile.timeLeft = lifetime; Projectile.penetrate = penetration;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Color c = DivineEquipmentData.Color(Weapon / 4); Vector2 at = Projectile.Center - Main.screenPosition;
        var batch = Main.spriteBatch;
        float fade = Math.Clamp(Projectile.timeLeft / 10f, .1f, 1);
        if (Kind is DivineShotKind.WaterRing or DivineShotKind.Echo or DivineShotKind.Explosion)
        {
            float radius = Kind == DivineShotKind.WaterRing ? 12 + Age * 4 : Kind == DivineShotKind.Echo ? 72 : 54;
            CombatDrawing.Circle(batch, at, radius, c * (CanDamage() == false ? .35f : fade), CanDamage() == false ? 2 : 5);
            for (int i = 0; i < 8; i++)
            { Vector2 d = (i * MathHelper.TwoPi / 8 + Age * .02f).ToRotationVector2(); CombatDrawing.Line(batch, at + d * (radius - 9), at + d * (radius + 4), c * fade, 3); }
        }
        else if (Kind == DivineShotKind.DelayedCut)
        {
            Vector2 d = angle.ToRotationVector2() * 75;
            CombatDrawing.Line(batch, at - d, at + d, c * (Age <= 20 ? .3f : fade), Age <= 20 ? 2 : 12);
        }
        else if (Kind == DivineShotKind.Charge)
        {
            CombatDrawing.Circle(batch, at, 14 + Math.Min(60, Age) * .25f, c, 3);
            for (int i = 0; i < 3; i++) CombatDrawing.Circle(batch, at + (Age * .05f + i * MathHelper.TwoPi / 3).ToRotationVector2() * 24, 5, c, 2);
        }
        else if (Kind is DivineShotKind.Arrow or DivineShotKind.Bullet or DivineShotKind.MinionLance)
        {
            Vector2 d = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            CombatDrawing.Line(batch, at - d * (Weapon == 9 ? 26 : 16), at + d * 9, c, Kind == DivineShotKind.Bullet ? 4 : 3);
            CombatDrawing.Line(batch, at + d * 9, at + d.RotatedBy(2.6f) * 8, Color.White, 2);
            CombatDrawing.Line(batch, at + d * 9, at + d.RotatedBy(-2.6f) * 8, Color.White, 2);
        }
        else
        {
            string path = Kind == DivineShotKind.DewProc ? DivineCatalog.Root + "Expert0" : DivineArsenal.Art(Weapon);
            Texture2D texture = ModContent.Request<Texture2D>(path).Value;
            float scale = Kind == DivineShotKind.Swing ? Weapon == 0 ? 1 : 1.35f : Kind == DivineShotKind.ClockBlade ? .5f : .32f;
            float rotation = Kind == DivineShotKind.Swing ? Projectile.rotation + MathHelper.PiOver4 : Projectile.rotation;
            if (Kind == DivineShotKind.Swing)
            {
                Vector2 start = Main.player[Projectile.owner].MountedCenter - Main.screenPosition;
                CombatDrawing.Line(batch, start, at, c * .5f, 5);
            }
            batch.Draw(texture, at, null, Color.White * fade, rotation, texture.Size() * .5f, scale, SpriteEffects.None, 0);
            if (Kind is DivineShotKind.HeatWave or DivineShotKind.Crescent) CombatDrawing.Circle(batch, at, 18, c * fade, 3, Projectile.rotation + MathHelper.Pi, 1.7f);
        }
        return false;
    }
}
public sealed class DivineSummonShot : DivineShot
{
    public override void SetStaticDefaults() => ProjectileID.Sets.MinionShot[Type] = true;
    public override void SetDefaults() { base.SetDefaults(); Projectile.DamageType = DamageClass.Summon; }
}

public sealed class DivineWeaponMarks : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private readonly ulong[] gunUntil = new ulong[Main.maxPlayers], gunReady = new ulong[Main.maxPlayers], lightUntil = new ulong[Main.maxPlayers], heavyUntil = new ulong[Main.maxPlayers], fireReady = new ulong[Main.maxPlayers];
    public bool GunHit(int owner, bool apply)
    {
        if (owner < 0 || owner >= Main.maxPlayers) return false;
        ulong now = Main.GameUpdateCount + 1; bool explode = gunUntil[owner] > now && gunReady[owner] <= now;
        if (explode) { gunUntil[owner] = 0; gunReady[owner] = now + 60; }
        if (apply && !explode) gunUntil[owner] = now + 180;
        return explode;
    }
    public bool FlameHit(int owner, bool heavy)
    {
        if (owner < 0 || owner >= Main.maxPlayers) return false;
        ulong now = Main.GameUpdateCount + 1;
        if (heavy) heavyUntil[owner] = now + 120; else lightUntil[owner] = now + 120;
        if (lightUntil[owner] <= now || heavyUntil[owner] <= now || fireReady[owner] > now) return false;
        lightUntil[owner] = heavyUntil[owner] = 0; fireReady[owner] = now + 90; return true;
    }
}
