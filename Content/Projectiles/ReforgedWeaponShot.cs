using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Projectiles;

public enum WeaponShape { Arrow, Sap, Sand, Mist, Root, Meteor, Returning, Seeking, Prism, Spear, Swing, Flail, Drill, Beam, Ring, Blast, Orbit, Thorn, Blade, MoonArrow }

public class ReforgedWeaponShot : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/PlayerBolt";
    public int WeaponId => Math.Clamp((int)Projectile.ai[0], 0, 38);
    public WeaponShape Shape => (WeaponShape)(int)Projectile.ai[1];
    public bool Secondary => Projectile.ai[2] >= 100;
    public int Age, Delay;
    public Vector2 Anchor, LaunchVelocity;
    public bool ImpactDone;
    public float Range;
    public bool Attached => Shape is WeaponShape.Spear or WeaponShape.Swing or WeaponShape.Drill;
    public Color Tint => WeaponId < 21 ? PrimordialBossData.Color(Math.Min(9, WeaponId / 2)) : BossData.Color(Math.Min(16, WeaponId - 21));
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 14; Projectile.friendly = true; Projectile.tileCollide = true;
        Projectile.ignoreWater = true; Projectile.penetrate = 1; Projectile.timeLeft = 120;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 20;
    }
    public override void OnSpawn(IEntitySource source)
    {
        Anchor = Projectile.Center; LaunchVelocity = Projectile.velocity;
        Range = WeaponId < 21 ? 180 : 330;
        Projectile.DamageType = ReforgedWeapons.Class(WeaponId);
        if (Attached)
        {
            Projectile.timeLeft = WeaponId == 31 ? 36 : 24; Projectile.tileCollide = false; Projectile.penetrate = -1;
            Projectile.localNPCHitCooldown = Shape == WeaponShape.Drill ? 9 : -1;
            if (Shape == WeaponShape.Swing) Range = WeaponId < 21 ? 100 : WeaponId == 27 ? 300 : 175;
            if (WeaponId == 3) Range = 112;
        }
        if (Shape is WeaponShape.Returning or WeaponShape.Flail) { Projectile.penetrate = -1; Projectile.timeLeft = 120; }
        if (Shape == WeaponShape.Arrow || Shape == WeaponShape.Seeking) Projectile.penetrate = WeaponId is 15 or 22 ? 3 : 1;
        if (Shape == WeaponShape.Beam)
        {
            Delay = WeaponId == 22 ? 18 : 12; Range = WeaponId == 38 ? 850 : 1200;
            Projectile.timeLeft = Delay + 15; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.localNPCHitCooldown = -1;
        }
        if (Shape is WeaponShape.Ring or WeaponShape.Blast or WeaponShape.Mist)
        {
            Projectile.penetrate = -1; Projectile.localNPCHitCooldown = Shape == WeaponShape.Mist ? 30 : -1;
            Projectile.timeLeft = Shape == WeaponShape.Mist ? 100 : Shape == WeaponShape.Blast ? 3 : 42;
            Projectile.tileCollide = Shape == WeaponShape.Mist;
            if (Shape == WeaponShape.Blast) Range = WeaponId < 21 ? 65 : 120;
            if (Shape == WeaponShape.Mist) Range = WeaponId == 13 ? 52 : 38;
        }
        if (Shape == WeaponShape.Orbit) { Projectile.tileCollide = false; Projectile.timeLeft = 110; }
    }
    public override void SendExtraAI(BinaryWriter w)
    { w.Write(Age); w.Write(Delay); w.Write(Range); w.Write(Anchor.X); w.Write(Anchor.Y); w.Write(LaunchVelocity.X); w.Write(LaunchVelocity.Y); w.Write(ImpactDone); w.Write(Projectile.timeLeft); w.Write(Projectile.penetrate); }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Age = r.ReadInt32(); Delay = r.ReadInt32(); Range = r.ReadSingle(); Anchor = new(r.ReadSingle(), r.ReadSingle()); LaunchVelocity = new(r.ReadSingle(), r.ReadSingle()); ImpactDone = r.ReadBoolean();
        // OnSpawn is not invoked on receiving peers. Rebuild class, immunity and movement flags too.
        int ttl = r.ReadInt32(), penetrate = r.ReadInt32();
        int savedAge = Age, savedDelay = Delay; float savedRange = Range; Vector2 savedAnchor = Anchor, savedVelocity = LaunchVelocity; bool savedImpact = ImpactDone;
        OnSpawn(null);
        Age = savedAge; Delay = savedDelay; Range = savedRange; Anchor = savedAnchor; LaunchVelocity = savedVelocity; ImpactDone = savedImpact;
        Projectile.timeLeft = ttl; Projectile.penetrate = penetrate;
    }
    public override bool ShouldUpdatePosition() => Age >= Delay && !Attached && Shape is not (WeaponShape.Beam or WeaponShape.Ring or WeaponShape.Blast);
    public override bool? CanDamage() => Age < Delay || Shape == WeaponShape.Orbit && Age < 32 ? false : null;
    public override bool? CanCutTiles() => false;
    public static NPC FindTarget(Vector2 at, float range, int preferred = -1)
    {
        if (preferred >= 0 && preferred < Main.maxNPCs && Main.npc[preferred].CanBeChasedBy() && Main.npc[preferred].Distance(at) < range && Collision.CanHitLine(at, 1, 1, Main.npc[preferred].position, Main.npc[preferred].width, Main.npc[preferred].height)) return Main.npc[preferred];
        NPC best = null;
        foreach (NPC n in Main.ActiveNPCs)
        {
            float distance = n.Distance(at);
            if (distance >= range || !n.CanBeChasedBy() || !Collision.CanHitLine(at, 1, 1, n.position, n.width, n.height)) continue;
            range = distance; best = n;
        }
        return best;
    }
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers || !Main.player[Projectile.owner].active || Main.player[Projectile.owner].dead)
        { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner];
        Age++; if (Age < Delay) return;
        int age = Age - Delay;
        Vector2 dir = LaunchVelocity.SafeNormalize(Vector2.UnitX);
        if (Attached)
        {
            if (p.HeldItem.type != ReforgedWeapons.Type(WeaponId)) { Projectile.Kill(); return; }
            float t = Math.Clamp(age / (WeaponId == 31 ? 36f : 24f), 0, 1);
            float extension = .22f + MathF.Sin(t * MathHelper.Pi) * .78f;
            Vector2 facing = Shape == WeaponShape.Swing ? dir.RotatedBy(MathHelper.Lerp(-1.25f, 1.25f, t) * (Projectile.ai[2] % 2 == 0 ? 1 : -1)) : dir;
            Projectile.Center = p.MountedCenter + facing * Range * extension;
            Projectile.rotation = facing.ToRotation() + MathHelper.PiOver4;
            p.heldProj = Projectile.whoAmI;
            return;
        }
        switch (Shape)
        {
            case WeaponShape.Arrow: Projectile.velocity.Y += WeaponId == 0 ? .09f : .012f; break;
            case WeaponShape.Sap: case WeaponShape.Sand: case WeaponShape.Meteor: case WeaponShape.Root: Projectile.velocity.Y += .12f; break;
            case WeaponShape.Mist:
                Projectile.velocity *= .91f;
                if (age >= 18) Projectile.velocity = Vector2.Zero;
                break;
            case WeaponShape.Returning: case WeaponShape.Flail:
                if (age >= 28 || Projectile.Distance(p.Center) > Range * 1.6f)
                {
                    Projectile.tileCollide = false;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, (p.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * (WeaponId < 21 ? 13 : 20), .17f);
                    if (Projectile.Distance(p.Center) < 30) Projectile.Kill();
                }
                break;
            case WeaponShape.Seeking: case WeaponShape.Thorn:
                if (age is >= 12 and <= 65)
                {
                    NPC target = FindTarget(Projectile.Center, WeaponId < 21 ? 280 : 550);
                    if (target != null) Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target.Center - Projectile.Center).SafeNormalize(dir) * Math.Max(6, LaunchVelocity.Length()), WeaponId == 15 ? .025f : .055f);
                }
                break;
            case WeaponShape.Orbit:
                if (age < 32)
                {
                    Projectile.Center = Anchor + (LaunchVelocity.ToRotation() + age * .065f).ToRotationVector2() * 95;
                    Projectile.velocity = Vector2.Zero;
                }
                else if (age == 32) { Projectile.velocity = (Anchor - Projectile.Center).SafeNormalize(dir) * 10; Projectile.tileCollide = true; }
                break;
            case WeaponShape.Prism: if (age == 28) { Impact(); Projectile.Kill(); } break;
            case WeaponShape.MoonArrow: if (age == 20) Impact(); break;
        }
        Projectile.rotation = Shape is WeaponShape.Flail or WeaponShape.Returning ? Projectile.rotation + .25f : Projectile.velocity.ToRotation();
    }
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Shape is WeaponShape.Flail or WeaponShape.Returning) { Impact(); Age = Math.Max(Age, 29); Projectile.velocity = -oldVelocity * .6f; Projectile.tileCollide = false; return false; }
        if (Shape == WeaponShape.Mist) { Projectile.velocity = Vector2.Zero; Projectile.tileCollide = false; return false; }
        return true;
    }
    private float RingRadius => Math.Min(Range, Math.Max(10, (Age - Delay) * Range / 42));
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        if (Attached || Shape == WeaponShape.Beam)
        {
            Vector2 start = Attached ? Main.player[Projectile.owner].MountedCenter : Anchor;
            Vector2 end = Attached ? Projectile.Center : Anchor + LaunchVelocity.SafeNormalize(Vector2.UnitX) * BeamLength();
            if (Attached && !Collision.CanHitLine(start, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height)) return false;
            float collision = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, Attached ? 14 : 9, ref collision);
        }
        if (Shape == WeaponShape.Ring)
        {
            Vector2 delta = targetHitbox.Center.ToVector2() - Anchor;
            if (WeaponId == 7 && Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - LaunchVelocity.ToRotation())) > .7f) return false;
            return Math.Abs(delta.Length() - RingRadius) <= targetHitbox.Size().Length() / 2 + 5;
        }
        if (Shape is WeaponShape.Blast or WeaponShape.Mist)
            return Vector2.Distance(targetHitbox.ClosestPointInRect(Projectile.Center), Projectile.Center) <= Range && Collision.CanHitLine(Projectile.Center, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height);
        return null;
    }
    private float BeamLength()
    {
        float[] samples = new float[3];
        Collision.LaserScan(Anchor, LaunchVelocity.SafeNormalize(Vector2.UnitX), 8, Range, samples);
        return (samples[0] + samples[1] + samples[2]) / 3;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (WeaponId is 0 or 1 or 13) target.AddBuff(BuffID.Slow, 120);
        if (WeaponId is 4 or 8 or 25) target.AddBuff(BuffID.Poisoned, 180);
        if (WeaponId is 6 or 7) target.AddBuff(BuffID.Frostburn, 150);
        if (WeaponId == 27) target.AddBuff(BuffID.Ichor, 180);
        Impact();
    }
    public override void OnKill(int timeLeft)
    {
        if (Shape is not (WeaponShape.Flail or WeaponShape.Returning)) Impact();
    }
    private void Impact()
    {
        if (ImpactDone || Secondary) return;
        ImpactDone = true;
        if (Projectile.owner != Main.myPlayer) return;
        void Child(WeaponShape shape, float factor, Vector2 velocity, Vector2? at = null)
            => ReforgedWeapons.Launch(Projectile.GetSource_FromThis(), Projectile.owner, WeaponId, shape, at ?? Projectile.Center, velocity, (int)(Projectile.damage * factor), Projectile.knockBack * .5f, 100);
        switch (WeaponId)
        {
            case 1: Child(WeaponShape.Mist, .32f, Vector2.Zero); break;
            case 5: for (int n = 0; n < 4; n++) Child(WeaponShape.Arrow, .4f, new Vector2((n - 1.5f) * 2, -3)); break;
            case 10: case 20: case 25: case 37:
                for (int n = -1; n <= 1; n += 2) Child(WeaponShape.Seeking, .3f, LaunchVelocity.SafeNormalize(Vector2.UnitX).RotatedBy(n * .6f) * 8);
                break;
            case 13: Child(WeaponShape.Mist, .4f, Vector2.Zero); break;
            case 14: Child(WeaponShape.Ring, .45f, Vector2.UnitX); break;
            case 16: case 33:
                Child(WeaponShape.Blast, .65f, Vector2.Zero);
                if (WeaponId == 33) for (int n = 0; n < 4; n++) Child(WeaponShape.Arrow, .18f, (n * MathHelper.PiOver2).ToRotationVector2() * 8);
                break;
            case 28: for (int n = -1; n <= 1; n++) Child(WeaponShape.Beam, .75f, LaunchVelocity.RotatedBy(n * .3f)); break;
            case 34: Child(WeaponShape.Ring, .4f, Vector2.UnitX); break;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 at = Projectile.Center - Main.screenPosition;
        bool warning = Age < Delay;
        if (Shape == WeaponShape.Beam)
        {
            Vector2 end = at + LaunchVelocity.SafeNormalize(Vector2.UnitX) * BeamLength();
            CombatDrawing.Line(Main.spriteBatch, at, end, Tint * (warning ? .4f : .9f), warning ? 2 : 9);
            if (!warning) CombatDrawing.Line(Main.spriteBatch, at, end, Color.White, 3);
        }
        else if (Shape == WeaponShape.Ring)
        {
            // The frost howl is a forward arc; the codex and bell waves are full rings.
            CombatDrawing.Circle(Main.spriteBatch, Anchor - Main.screenPosition, RingRadius, Tint, 7,
                WeaponId == 7 ? LaunchVelocity.ToRotation() + MathHelper.Pi : 0, WeaponId == 7 ? MathHelper.Pi - .7f : 0);
        }
        else if (Shape is WeaponShape.Blast or WeaponShape.Mist)
        {
            CombatDrawing.Circle(Main.spriteBatch, at, Range, Tint * .75f, 5);
            Texture2D glow = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(glow, at, null, Tint * .4f, Age * .025f, glow.Size() / 2, Range * 1.7f / glow.Width, SpriteEffects.None);
        }
        else
        {
            if (Attached || Shape is WeaponShape.Returning or WeaponShape.Flail)
                CombatDrawing.Line(Main.spriteBatch, Main.player[Projectile.owner].MountedCenter - Main.screenPosition, at, Tint * .75f, WeaponId == 27 ? 9 : 3);
            Texture2D texture = Attached || Shape is WeaponShape.Flail or WeaponShape.Returning ? ModContent.Request<Texture2D>(ReforgedWeapons.Art(WeaponId)).Value : TextureAssets.Projectile[Type].Value;
            float size = Attached ? 54 : Shape is WeaponShape.Flail or WeaponShape.Returning ? 42 : Shape is WeaponShape.Sap or WeaponShape.Meteor or WeaponShape.Prism ? 30 : 20;
            Main.EntitySpriteDraw(texture, at, null, Color.Lerp(Tint, Color.White, .65f) * (warning ? .4f : 1), Projectile.rotation, texture.Size() / 2, size / texture.Width, SpriteEffects.None);
            if (warning) CombatDrawing.Circle(Main.spriteBatch, at, 16, Tint * .6f, 2);
        }
        return false;
    }
}

public sealed class ReforgedMinionShot : ReforgedWeaponShot
{
    public override void SetStaticDefaults() => ProjectileID.Sets.MinionShot[Type] = true;
}
