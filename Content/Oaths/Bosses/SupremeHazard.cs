#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Oaths.Bosses;
public enum SupremeShape { Star, Beam, Ring, Wall, Pulse }

public sealed class SupremeHazard : ModProjectile
{
    public override string Texture => SupremeBossNPC.Root + "Spark";
    public SupremeShape Shape => (SupremeShape)(int)Projectile.ai[2];
    public int Serial, Age, Delay, Duration, Index, Mark;
    public float Width, Length, Angle, Gap;
    public bool Ready;
    public Vector2 Anchor, Launch, Previous;
    public SupremeBossNPC? Boss => (int)Projectile.ai[0] - 1 is int slot && slot >= 0 && slot < Main.maxNPCs
        && Main.npc[slot].active && Main.npc[slot].ModNPC is SupremeBossNPC b && b.Ready && !b.Cancelled && b.Serial == Serial ? b : null;
    public bool OwnedBy(SupremeBossNPC b) => (int)Projectile.ai[0] - 1 == b.NPC.whoAmI && Serial == b.Serial;
    public bool Damaging => Ready && Boss != null && Age >= Delay && Age < Delay + Duration;
    public float Radius => Length < 0 ? Math.Max(80, -Length * (1 - Math.Clamp((Age - Delay) / (float)Math.Max(1, Duration), 0, 1)))
        : 80 + Math.Max(0, Age - Delay) * Length / Math.Max(1, Duration);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16; Projectile.hostile = true; Projectile.penetrate = -1;
        Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.netImportant = true;
        Projectile.timeLeft = 900; Ready = false; Age = 0;
    }
    public void Setup(SupremeBossNPC b, int delay, int duration, float width, float length, float angle, float gap, int mark)
    {
        Serial = b.Serial; Index = b.Index; Mark = mark; Delay = Math.Clamp(delay, 75, 600); Duration = Math.Clamp(duration, 1, 600);
        Width = Math.Clamp(width, 4, 60); Length = Math.Clamp(length, -1000, 2000); Angle = angle;
        Gap = Shape == SupremeShape.Wall ? Math.Clamp(gap, 180, 400) : Math.Clamp(gap, .9f, 2f);
        Anchor = Previous = Projectile.Center; Launch = Projectile.velocity; Ready = true;
        Projectile.timeLeft = Delay + Duration + 5; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Damaging ? null : false;
    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        => modifiers.SourceDamage *= .5f;
    public override void AI()
    {
        if (!Ready) return;
        if (Boss == null || ++Age >= Delay + Duration) { Projectile.Kill(); return; }
        Previous = Projectile.Center;
        if (Age >= Delay && Shape is SupremeShape.Star or SupremeShape.Wall) Projectile.Center = Anchor + Launch * (Age - Delay);
        if (!float.IsFinite(Projectile.Center.X) || !float.IsFinite(Projectile.Center.Y)) Projectile.Kill();
    }
    internal static bool LineHit(Rectangle box, Vector2 a, Vector2 b, float width)
    { float hit = 0; return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), a, b, width, ref hit); }
    internal static bool RingHit(Rectangle box, Vector2 at, float radius, float angle, float gap, float width)
    {
        // Same polyline in collision and rendering. The gap excludes the entire segment, not just a center test.
        const int pieces = 80;
        for (int j = 0; j < pieces; j++)
        {
            float a = angle + gap / 2 + (MathHelper.TwoPi - gap) * j / pieces;
            float z = angle + gap / 2 + (MathHelper.TwoPi - gap) * (j + 1) / pieces;
            if (LineHit(box, at + a.ToRotationVector2() * radius, at + z.ToRotationVector2() * radius, width)) return true;
        }
        return false;
    }
    public override bool? Colliding(Rectangle projectileHitbox, Rectangle targetHitbox)
    {
        if (!Damaging) return false;
        Vector2 at = Projectile.Center;
        return Shape switch
        {
            SupremeShape.Beam => LineHit(targetHitbox, Anchor, Anchor + Angle.ToRotationVector2() * Length, Width),
            SupremeShape.Ring => RingHit(targetHitbox, Anchor, Radius, Angle, Gap, Width),
            SupremeShape.Wall => LineHit(targetHitbox, at - new Vector2(0, Length), at + new Vector2(0, Angle - Gap / 2), Width)
                || LineHit(targetHitbox, at + new Vector2(0, Angle + Gap / 2), at + new Vector2(0, Length), Width),
            SupremeShape.Pulse => Vector2.DistanceSquared(targetHitbox.ClosestPointInRect(Anchor), Anchor) <= Length * Length,
            _ => targetHitbox.Intersects(Projectile.Hitbox) || LineHit(targetHitbox, Previous, at + Launch.SafeNormalize(Vector2.UnitY), Width)
        };
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Serial); w.Write(Age); w.Write(Delay); w.Write(Duration); w.Write(Index); w.Write(Mark);
        w.Write(Width); w.Write(Length); w.Write(Angle); w.Write(Gap); SupremeBossNPC.Write(w, Anchor); SupremeBossNPC.Write(w, Launch);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        bool ready = r.ReadBoolean(); int serial = r.ReadInt32(), age = r.ReadInt32(), delay = r.ReadInt32(), duration = r.ReadInt32(), index = r.ReadInt32(), mark = r.ReadInt32();
        float width = r.ReadSingle(), length = r.ReadSingle(), angle = r.ReadSingle(), gap = r.ReadSingle(); Vector2 anchor = SupremeBossNPC.Read(r), launch = SupremeBossNPC.Read(r);
        Ready = ready && float.IsFinite(width + length + angle + gap + launch.X + launch.Y); Serial = serial; Age = Math.Clamp(age, 0, 1200);
        Delay = Math.Clamp(delay, 75, 600); Duration = Math.Clamp(duration, 1, 600); Index = Math.Clamp(index, 0, 2); Mark = Math.Clamp(mark, -1, 5);
        Width = Math.Clamp(width, 4, 60); Length = Math.Clamp(length, -1000, 2000); Angle = angle;
        Gap = Shape == SupremeShape.Wall ? Math.Clamp(gap, 180, 400) : Math.Clamp(gap, .9f, 2);
        Anchor = SupremeBossNPC.Point(anchor); Launch = launch; Previous = Projectile.Center;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        SpriteBatch batch = Main.spriteBatch; Vector2 at = Projectile.Center - Main.screenPosition, anchor = Anchor - Main.screenPosition;
        Color color = OathCatalog.Colors[Index]; if (Index == 2 && Mark == 1) color = new Color(177, 150, 240);
        bool warning = Age < Delay; Color c = color * (warning ? .42f : .94f); float width = warning ? 2 : Width;
        switch (Shape)
        {
            case SupremeShape.Beam:
                CombatDrawing.Line(batch, anchor, anchor + Angle.ToRotationVector2() * Length, c, width); break;
            case SupremeShape.Ring:
                float radius = warning ? Length < 0 ? -Length : 80 : Radius;
                for (int j = 0; j < 80; j++)
                {
                    float a = Angle + Gap / 2 + (MathHelper.TwoPi - Gap) * j / 80;
                    float z = Angle + Gap / 2 + (MathHelper.TwoPi - Gap) * (j + 1) / 80;
                    CombatDrawing.Line(batch, anchor + a.ToRotationVector2() * radius, anchor + z.ToRotationVector2() * radius, c, width);
                }
                // White rails mark the safe wedge throughout the warning and expansion.
                for (int j = -1; j <= 1; j += 2)
                    CombatDrawing.Line(batch, anchor + (Angle + j * Gap / 2).ToRotationVector2() * Math.Max(20, radius - 24),
                        anchor + (Angle + j * Gap / 2).ToRotationVector2() * (radius + 24), Color.White * .8f, 3);
                break;
            case SupremeShape.Wall:
                CombatDrawing.Line(batch, at - new Vector2(0, Length), at + new Vector2(0, Angle - Gap / 2), c, width);
                CombatDrawing.Line(batch, at + new Vector2(0, Angle + Gap / 2), at + new Vector2(0, Length), c, width);
                for (int j = -1; j <= 1; j += 2)
                    CombatDrawing.Line(batch, at + new Vector2(-25, Angle + j * Gap / 2), at + new Vector2(25, Angle + j * Gap / 2), Color.White, 3);
                if (warning) CombatDrawing.Line(batch, at + new Vector2(0, Angle), at + new Vector2(Launch.X * 18, Angle), c, 2);
                break;
            case SupremeShape.Pulse:
                if (!warning) CombatDrawing.Disc(batch, at, Length, c * .3f);
                CombatDrawing.Circle(batch, at, Length, c, warning ? 2 : 7);
                if (warning) CombatDrawing.Circle(batch, at, Length * Age / Math.Max(1f, Delay), c, 2);
                break;
            default:
                if (warning) CombatDrawing.Line(batch, anchor, anchor + Launch * Math.Min(Duration, 100), c, 2);
                CombatDrawing.Disc(batch, at, Width / 2, c); CombatDrawing.Circle(batch, at, Width / 2 + 3, c, 2); break;
        }
        return false;
    }
}
