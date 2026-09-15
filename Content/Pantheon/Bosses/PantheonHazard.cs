#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;
namespace StarfallThrone.Content.Pantheon.Bosses;

public enum PantheonShape { Star, Arc, Beam, Ring, Column, Wall, Echo, Gravity }

public sealed class PantheonHazard : ModProjectile
{
    public override string Texture => "Terraria/Images/MagicPixel";
    public PantheonShape Shape => (PantheonShape)(int)Projectile.ai[2];
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public int Serial, Age, Delay = 75, Duration = 100, Index, Mark;
    public float Width = 16, Length = 500, Angle, Gap = .8f;
    public bool Ready;
    public Vector2 Anchor, Launch, Previous;
    public PantheonBossNPC? Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active
        && Main.npc[OwnerSlot].ModNPC is PantheonBossNPC b && b.Ready && !b.Cancelled && b.Serial == Serial ? b : null;
    public bool OwnedBy(PantheonBossNPC b) => OwnerSlot == b.NPC.whoAmI && Serial == b.Serial;
    public bool Damaging => Ready && Boss != null && Age >= Delay && Age < Delay + Duration && Shape is not PantheonShape.Echo and not PantheonShape.Gravity;
    public float Radius => Math.Max(12, Length >= 0 ? 45 + Math.Max(0, Age - Delay) * Length / Duration : -Length * (1 - Math.Clamp((Age - Delay) / (float)Duration, 0, 1)) + 65);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16; Projectile.hostile = true; Projectile.friendly = false;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.netImportant = true; Projectile.timeLeft = 800;
        Ready = false; Age = Serial = 0; Anchor = Launch = Previous = Vector2.Zero;
    }
    public void Setup(PantheonBossNPC boss, int delay, int duration, float width, float length, float angle, float gap, int mark)
    {
        Serial = boss.Serial; Index = boss.Index; Delay = Math.Clamp(delay, 60, 600); Duration = Math.Clamp(duration, 1, 1000);
        Width = Math.Clamp(width, 4, 120); Length = Math.Clamp(length, -1400, 1800); Angle = angle; Gap = gap; Mark = mark;
        Anchor = Previous = Projectile.Center; Launch = Projectile.velocity; Ready = true;
        Projectile.width = Projectile.height = (int)Width; Projectile.Center = Anchor;
        Projectile.timeLeft = Delay + Duration + 5; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Damaging ? null : false;
    public Vector2 PositionAt(float t)
    {
        if (Shape == PantheonShape.Arc)
        {
            float spin = Angle * t;
            return Anchor + Launch * t + Launch.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * MathF.Sin(spin) * Length;
        }
        return Anchor + Launch * t;
    }
    public override void AI()
    {
        if (!Ready) return;
        if (Boss == null || ++Age >= Delay + Duration) { Projectile.Kill(); return; }
        Previous = Projectile.Center;
        if (Age >= Delay)
        {
            if (Shape is PantheonShape.Star or PantheonShape.Arc or PantheonShape.Wall or PantheonShape.Echo) Projectile.Center = PositionAt(Age - Delay);
            if (Shape == PantheonShape.Gravity && Main.netMode != NetmodeID.Server && Main.myPlayer < Main.maxPlayers)
            {
                Player p = Main.player[Main.myPlayer]; float d = Vector2.Distance(p.Center, Anchor);
                if (p.active && !p.dead && d > 85 && d < Length)
                {
                    // Local owner movement only: bounded acceleration, never forced flight or teleport.
                    Vector2 push = (Anchor - p.Center).SafeNormalize(Vector2.Zero) * .045f;
                    if (p.velocity.LengthSquared() < 400) p.velocity += push;
                }
            }
        }
        if (!float.IsFinite(Projectile.Center.X) || !float.IsFinite(Projectile.Center.Y)) Projectile.Kill();
    }
    internal static bool Segment(Rectangle target, Vector2 a, Vector2 z, float width)
    { float point = 0; return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), a, z, width, ref point); }
    private bool Shielded(Rectangle target)
    {
        PantheonBossNPC? boss = Boss;
        if (boss == null || boss.Index is not 11 and not 12) return false;
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is PantheonNode node && node.OwnedBy(boss) && !node.Part && node.Open
                && Vector2.DistanceSquared(Anchor, n.Center) < Vector2.DistanceSquared(Anchor, target.Center.ToVector2())
                && Segment(n.Hitbox, Anchor, target.Center.ToVector2(), 12)) return true;
        return false;
    }
    public override bool? Colliding(Rectangle projectileHitbox, Rectangle targetHitbox)
    {
        if (!Damaging) return false;
        switch (Shape)
        {
            case PantheonShape.Beam:
                return !Shielded(targetHitbox) && Segment(targetHitbox, Anchor, Anchor + Angle.ToRotationVector2() * Length, Width);
            case PantheonShape.Column:
                return Segment(targetHitbox, Anchor, Anchor - new Vector2(0, Length), Width);
            case PantheonShape.Wall:
                // A finite moving gate with a permanent visible gap, not an infinite world barrier.
                Vector2 top = Projectile.Center - new Vector2(0, Length / 2), bottom = Projectile.Center + new Vector2(0, Length / 2);
                return Segment(targetHitbox, top, Projectile.Center - new Vector2(0, Gap), Width)
                    || Segment(targetHitbox, Projectile.Center + new Vector2(0, Gap), bottom, Width);
            case PantheonShape.Ring:
                for (int i = 0; i < 72; i++)
                {
                    float a = i * MathHelper.TwoPi / 72, z = (i + 1) * MathHelper.TwoPi / 72;
                    if (Math.Abs(MathHelper.WrapAngle((a + z) * .5f - Angle)) < Gap) continue;
                    if (Segment(targetHitbox, Anchor + a.ToRotationVector2() * Radius, Anchor + z.ToRotationVector2() * Radius, Width)) return true;
                }
                return false;
            default: return Segment(targetHitbox, Previous, Projectile.Center, Width) || projectileHitbox.Intersects(targetHitbox);
        }
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Serial); w.Write(Age); w.Write(Delay); w.Write(Duration); w.Write(Index); w.Write(Mark);
        w.Write(Width); w.Write(Length); w.Write(Angle); w.Write(Gap);
        PantheonBossNPC.Write(w, Anchor); PantheonBossNPC.Write(w, Launch); PantheonBossNPC.Write(w, Previous);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Serial = r.ReadInt32(); Age = Math.Clamp(r.ReadInt32(), 0, 1600);
        Delay = Math.Clamp(r.ReadInt32(), 60, 600); Duration = Math.Clamp(r.ReadInt32(), 1, 1000); Index = Math.Clamp(r.ReadInt32(), 0, 16); Mark = r.ReadInt32();
        Width = Finite(r.ReadSingle(), 4, 120); Length = Finite(r.ReadSingle(), -1400, 1800); Angle = Finite(r.ReadSingle(), -100, 100); Gap = Finite(r.ReadSingle(), 0, 250);
        Anchor = PantheonBossNPC.Point(PantheonBossNPC.Read(r)); Launch = PantheonBossNPC.Read(r); Previous = PantheonBossNPC.Point(PantheonBossNPC.Read(r));
        Launch = new(Finite(Launch.X, -40, 40), Finite(Launch.Y, -40, 40));
        Vector2 center = Projectile.Center; Projectile.width = Projectile.height = (int)Width; Projectile.Center = center;
        Projectile.timeLeft = Math.Max(1, Delay + Duration - Age + 5);
    }
    private static float Finite(float value, float low, float high) => Math.Clamp(float.IsFinite(value) ? value : low, low, high);
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        SpriteBatch b = Main.spriteBatch; Vector2 screen = Main.screenPosition, at = Projectile.Center - screen;
        bool warn = Age < Delay; Color color = PantheonCatalog.Colors[Index] * (warn ? .35f + .45f * Age / Delay : .9f);
        Vector2 start = Anchor - screen;
        if (Shape == PantheonShape.Echo && Mark != -10) color *= .22f;
        switch (Shape)
        {
            case PantheonShape.Echo when Mark == -10:
                // A committed body dash has a full-width, high contrast lane, unlike harmless dream echoes.
                Ray(b, start, start + Launch * Duration, color, true);
                if (!warn) CombatDrawing.Circle(b, at, Width / 2, color * .6f, 3);
                break;
            case PantheonShape.Beam: Ray(b, start, start + Angle.ToRotationVector2() * Length, color, warn); break;
            case PantheonShape.Column: Ray(b, start, start - new Vector2(0, Length), color, warn); break;
            case PantheonShape.Ring:
                CombatDrawing.Circle(b, start, Radius, color, warn ? 2 : Width, Angle, Gap);
                if (warn)
                {
                    CombatDrawing.Circle(b, start, Math.Abs(Length) + 45, color * .18f, 2, Angle, Gap);
                    CombatDrawing.Line(b, start + Angle.ToRotationVector2() * 35, start + Angle.ToRotationVector2() * 120, Color.White * .7f, 3);
                }
                break;
            case PantheonShape.Wall:
                Ray(b, at - new Vector2(0, Length / 2), at - new Vector2(0, Gap), color, warn);
                Ray(b, at + new Vector2(0, Gap), at + new Vector2(0, Length / 2), color, warn);
                CombatDrawing.Line(b, at - new Vector2(34, Gap), at + new Vector2(34, -Gap), Color.White, 3);
                CombatDrawing.Line(b, at - new Vector2(34, -Gap), at + new Vector2(34, Gap), Color.White, 3);
                break;
            case PantheonShape.Gravity:
                for (int i = 0; i < 3; i++) CombatDrawing.Circle(b, start, 85 + i * (Length - 85) / 3, color * .22f, 2, Age * .01f + i, .8f);
                break;
            default:
                if (warn)
                {
                    Vector2 prior = start;
                    for (int i = 1; i <= 20; i++)
                    {
                        Vector2 next = PositionAt(Math.Min(Duration, 100) * i / 20f) - screen;
                        CombatDrawing.Line(b, prior, next, color * .32f, 2); prior = next;
                    }
                    CombatDrawing.Circle(b, start, Width + 6, color, 2);
                }
                else
                {
                    CombatDrawing.Line(b, Previous - screen, at, color, Width);
                    PantheonMotif.Draw(b, at, Index, Width * .75f, color);
                    CombatDrawing.Disc(b, at, Width / 3, Color.White * .8f);
                }
                break;
        }
        if (warn) CombatDrawing.Circle(b, start, 16, Color.White * .7f, 2, -MathHelper.PiOver2, MathHelper.Pi * (1 - Age / (float)Delay));
        return false;
    }
    private void Ray(SpriteBatch b, Vector2 a, Vector2 z, Color color, bool warn)
    {
        if (warn)
        {
            Vector2 side = (z - a).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * Width / 2;
            CombatDrawing.Line(b, a - side, z - side, color * .45f, 2); CombatDrawing.Line(b, a + side, z + side, color * .45f, 2);
            CombatDrawing.Line(b, a, z, color, 2);
        }
        else { CombatDrawing.Line(b, a, z, color, Width); CombatDrawing.Line(b, a, z, Color.White * .6f, Width / 3); }
    }
}

public abstract partial class PantheonBossNPC
{
    public PantheonHazard? Emit(PantheonShape shape, Vector2 at, Vector2 velocity, int delay = 75, int duration = 120,
        float width = 18, float length = 650, float angle = 0, float gap = .85f, float damage = 1, int mark = -1)
    {
        if (!Authority || !Ready || Cancelled || IsBroken(mark)) return null;
        int count = 0; foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is PantheonHazard h && h.OwnedBy(this)) count++;
        if (count >= 38) return null;
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), Point(at), velocity, ModContent.ProjectileType<PantheonHazard>(), EngineDamage(damage), 0, Main.myPlayer, NPC.whoAmI + 1, Index, (int)shape);
        if (slot < 0 || slot >= Main.maxProjectiles) return null;
        var hazard = (PantheonHazard)Main.projectile[slot].ModProjectile;
        hazard.Setup(this, delay, duration, width, length, angle, gap, mark); return hazard;
    }
    private void Beam(Vector2 a, Vector2 z, int mark = -1, int delay = 75, float width = 24, int duration = 40)
        => Emit(PantheonShape.Beam, a, Vector2.Zero, delay, duration, width, Vector2.Distance(a, z), (z - a).ToRotation(), damage: 1.4f, mark: mark);
    private void Fan(Vector2 a, Vector2 toward, int count, float spread, float speed = 7, int mark = -1, bool arc = false)
    {
        float direction = (toward - a).ToRotation();
        for (int i = 0; i < count; i++)
            Emit(arc ? PantheonShape.Arc : PantheonShape.Star, a, (direction + (i - (count - 1) / 2f) * spread).ToRotationVector2() * speed,
                width: 15, duration: 100, length: 55, angle: .04f, damage: .7f, mark: mark);
    }
    private void Ring(Vector2 at, float size = 450, float gapAngle = 0, int mark = -1, int delay = 75, int duration = 150)
        => Emit(PantheonShape.Ring, at, Vector2.Zero, delay, duration, 18, size, gapAngle, .95f, mark: mark);
    private void Dash(Vector2 target)
    {
        if (!Authority) return;
        DashStart = Timer; DashFrom = NPC.Center; DashTo = Point(target);
        // The visible lane is narrower than the full decorative body. Body contact uses the core only.
        Emit(PantheonShape.Echo, DashFrom, (DashTo - DashFrom) / 30, duration: 30, width: NPC.width, damage: 0, mark: -10);
        NPC.netUpdate = true;
    }
}

public static class PantheonMotif
{
    public static void Draw(SpriteBatch b, Vector2 at, int index, float size, Color c)
    {
        int tips = 3 + index % 5;
        for (int i = 0; i < tips * 2; i++)
        {
            float a = i * MathHelper.Pi / tips - MathHelper.PiOver2, z = (i + 1) * MathHelper.Pi / tips - MathHelper.PiOver2;
            Vector2 stretch = index is 1 or 8 ? new Vector2(1.5f, .65f) : index is 4 or 11 ? new Vector2(.8f, 1.3f) : Vector2.One;
            CombatDrawing.Line(b, at + a.ToRotationVector2() * size * (i % 2 == 0 ? 1 : .48f) * stretch,
                at + z.ToRotationVector2() * size * (i % 2 == 0 ? .48f : 1) * stretch, c, 3);
        }
        if (index is 1 or 3 or 8) CombatDrawing.Circle(b, at, size * .32f, c, 2);
        else if (index is 9 or 12 or 16) CombatDrawing.Line(b, at - new Vector2(size, 0), at + new Vector2(size, 0), c, 3);
    }
}
