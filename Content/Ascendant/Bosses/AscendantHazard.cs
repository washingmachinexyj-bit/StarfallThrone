using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ascendant.Bosses;

public enum AscendantShape { Seed, Drop, Roller, Pillar, Beam, Ring, Trace, Corridor }

public sealed class AscendantHazard : ModProjectile
{
    public override string Texture => "Terraria/Images/MagicPixel";
    public AscendantShape Shape => (AscendantShape)(int)Projectile.ai[2];
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public int Serial, Age, Delay = 75, Duration = 100, Tier, Mark, Count;
    public float Width = 14, Length = 600, Angle, Gap = .9f;
    public bool Ready;
    public Vector2 Anchor, Launch, Previous;
    public Vector2[] Points = new Vector2[31];
    public AscendantBossNPC Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active
        && Main.npc[OwnerSlot].ModNPC is AscendantBossNPC b && b.Ready && b.Serial == Serial && !b.Cancelled && AscendantWorld.Open(b.Index) ? b : null;
    public bool OwnedBy(AscendantBossNPC boss) => OwnerSlot == boss.NPC.whoAmI && Serial == boss.Serial;
    public bool Damaging => Ready && Boss != null && Age >= Delay && Age < Delay + Duration;
    public float RingRadius => Math.Max(12, Math.Max(0, Age - Delay) * Length / Math.Max(1, Duration));
    public float CorridorX => Anchor.X + (Age < Delay ? 0 : MathF.Sin((Age - Delay) * .009f) * 100);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16; Projectile.hostile = true; Projectile.friendly = false;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.timeLeft = 700;
        Projectile.netImportant = true; Ready = false; Age = Count = 0; Points = new Vector2[31];
    }
    public void Setup(AscendantBossNPC boss, int delay, int duration, float width, float length, float angle, float gap, int mark)
    {
        Serial = boss.Serial; Tier = boss.Index; Delay = Math.Clamp(delay, 60, 600); Duration = Math.Clamp(duration, 1, 600);
        Width = Math.Clamp(width, 4, 160); Length = length; Angle = angle; Gap = gap; Mark = mark;
        Anchor = Previous = Projectile.Center; Launch = Projectile.velocity;
        Projectile.width = Projectile.height = (int)Width; Projectile.Center = Anchor;
        Projectile.timeLeft = Delay + Duration + 4; Ready = true; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Damaging ? null : false;
    public override void AI()
    {
        if (!Ready) return;
        if (Boss == null || ++Age >= Delay + Duration) { Projectile.Kill(); return; }
        Previous = Projectile.Center;
        if (Age < Delay) return;
        int elapsed = Age - Delay;
        switch (Shape)
        {
            case AscendantShape.Drop:
                // Analytic motion is reproducible after late-join/network corrections.
                Projectile.Center = Anchor + Launch * elapsed + new Vector2(0, Math.Min(elapsed * elapsed * .025f, elapsed * 3.5f));
                break;
            case AscendantShape.Seed:
            case AscendantShape.Roller:
                Projectile.Center = Anchor + Launch * elapsed;
                break;
            case AscendantShape.Trace:
                if (Count >= 2)
                {
                    float step = Math.Clamp(elapsed / (float)Math.Max(1, Duration - 1), 0, 1) * (Count - 1);
                    int index = Math.Min(Count - 2, (int)step);
                    Projectile.Center = Vector2.Lerp(Points[index], Points[index + 1], step - index);
                    if (elapsed == 0) Previous = Projectile.Center;
                }
                break;
        }
        if (!float.IsFinite(Projectile.Center.X) || !float.IsFinite(Projectile.Center.Y)) Projectile.Kill();
    }
    private static bool SegmentHit(Rectangle target, Vector2 start, Vector2 end, float width)
    { float hit = 0; return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), start, end, width, ref hit); }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!Damaging) return false;
        switch (Shape)
        {
            case AscendantShape.Beam:
                return SegmentHit(targetHitbox, Anchor, Anchor + Angle.ToRotationVector2() * Length, Width);
            case AscendantShape.Pillar:
                return new Rectangle((int)(Anchor.X - Width / 2), (int)(Anchor.Y - Length), (int)Width, (int)Length).Intersects(targetHitbox);
            case AscendantShape.Ring:
                for (int i = 0; i < 96; i++)
                {
                    float a = i * MathHelper.TwoPi / 96, z = (i + 1) * MathHelper.TwoPi / 96;
                    if (Math.Abs(MathHelper.WrapAngle((a + z) / 2 - Angle)) < Gap) continue;
                    if (SegmentHit(targetHitbox, Anchor + a.ToRotationVector2() * RingRadius, Anchor + z.ToRotationVector2() * RingRadius, Width)) return true;
                }
                return false;
            case AscendantShape.Corridor:
                // A broad, slow walking lane. Outside the finite arena there is no invisible damage wall.
                float left = Anchor.X - 650, right = Anchor.X + 650, top = Anchor.Y - Length / 2;
                Rectangle l = new((int)left, (int)top, Math.Max(0, (int)(CorridorX - Gap - left)), (int)Length);
                Rectangle r = new((int)(CorridorX + Gap), (int)top, Math.Max(0, (int)(right - CorridorX - Gap)), (int)Length);
                return l.Intersects(targetHitbox) || r.Intersects(targetHitbox);
            default:
                // Swept collision prevents high-speed trace/seed tunneling; visuals show the identical segment.
                return SegmentHit(targetHitbox, Previous, Projectile.Center, Width) || Projectile.Hitbox.Intersects(targetHitbox);
        }
    }
    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    { if (Boss is AscendantBossNPC boss) boss.Damaged[target.whoAmI] = true; }
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Ready); writer.Write(Serial); writer.Write(Age); writer.Write(Delay); writer.Write(Duration); writer.Write(Tier); writer.Write(Mark); writer.Write(Count);
        writer.Write(Width); writer.Write(Length); writer.Write(Angle); writer.Write(Gap);
        AscendantBossNPC.WriteVector(writer, Anchor); AscendantBossNPC.WriteVector(writer, Launch); AscendantBossNPC.WriteVector(writer, Previous);
        for (int i = 0; i < Points.Length; i++) AscendantBossNPC.WriteVector(writer, Points[i]);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Ready = reader.ReadBoolean(); Serial = reader.ReadInt32(); Age = Math.Clamp(reader.ReadInt32(), 0, 1200);
        Delay = Math.Clamp(reader.ReadInt32(), 60, 600); Duration = Math.Clamp(reader.ReadInt32(), 1, 600);
        Tier = Math.Clamp(reader.ReadInt32(), 0, 2); Mark = reader.ReadInt32(); Count = Math.Clamp(reader.ReadInt32(), 0, 31);
        Width = Math.Clamp(reader.ReadSingle(), 4, 160); Length = Math.Clamp(reader.ReadSingle(), 0, 2200);
        Angle = reader.ReadSingle(); Gap = reader.ReadSingle();
        Anchor = AscendantBossNPC.WorldPoint(AscendantBossNPC.ReadVector(reader)); Launch = AscendantBossNPC.ReadVector(reader); Previous = AscendantBossNPC.WorldPoint(AscendantBossNPC.ReadVector(reader));
        for (int i = 0; i < Points.Length; i++) Points[i] = AscendantBossNPC.WorldPoint(AscendantBossNPC.ReadVector(reader));
        Vector2 center = Projectile.Center; Projectile.width = Projectile.height = (int)Width; Projectile.Center = center;
        Projectile.timeLeft = Math.Max(1, Delay + Duration - Age + 4);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        SpriteBatch batch = Main.spriteBatch; Vector2 screen = Main.screenPosition, at = Projectile.Center - screen;
        bool warning = Age < Delay; Color tint = AscendantCatalog.Colors[Math.Clamp(Tier, 0, 2)];
        Color c = tint * (warning ? .4f + .4f * Age / Delay : .9f);
        switch (Shape)
        {
            case AscendantShape.Beam:
                DrawRay(batch, Anchor - screen, Anchor + Angle.ToRotationVector2() * Length - screen, c, warning);
                break;
            case AscendantShape.Pillar:
                if (warning)
                {
                    Vector2 b = Anchor - screen, t = b - new Vector2(0, Length);
                    CombatDrawing.Line(batch, b - new Vector2(Width / 2, 0), t - new Vector2(Width / 2, 0), c * .6f, 2);
                    CombatDrawing.Line(batch, b + new Vector2(Width / 2, 0), t + new Vector2(Width / 2, 0), c * .6f, 2);
                    CombatDrawing.Line(batch, b - new Vector2(Width / 2 + 7, 0), b + new Vector2(Width / 2 + 7, 0), c, 4);
                }
                else DrawRay(batch, Anchor - screen, Anchor - new Vector2(0, Length) - screen, c, false);
                break;
            case AscendantShape.Ring:
                float radius = warning ? 58 : RingRadius;
                for (int i = 0; i < 96; i++)
                {
                    float a = i * MathHelper.TwoPi / 96, z = (i + 1) * MathHelper.TwoPi / 96;
                    if (Math.Abs(MathHelper.WrapAngle((a + z) / 2 - Angle)) < Gap) continue;
                    CombatDrawing.Line(batch, Anchor - screen + a.ToRotationVector2() * radius, Anchor - screen + z.ToRotationVector2() * radius, c, warning ? 2 : Width);
                }
                if (warning) CombatDrawing.Line(batch, Anchor - screen, Anchor - screen + Angle.ToRotationVector2() * 115, Color.White * .85f, 3);
                break;
            case AscendantShape.Trace:
                for (int i = 1; i < Count; i++) CombatDrawing.Line(batch, Points[i - 1] - screen, Points[i] - screen, c * (warning ? .8f : .25f), warning ? 3 : 2);
                if (!warning) { CombatDrawing.Disc(batch, at, Width / 2, c); CombatDrawing.Line(batch, Previous - screen, at, Color.White, Width / 3); }
                else if (Count > 0) AscendantGlyph.Draw(batch, Points[0] - screen, 4, 20, c);
                break;
            case AscendantShape.Corridor:
                float x = CorridorX - screen.X, y = Anchor.Y - screen.Y;
                float left = Anchor.X - 650 - screen.X, right = Anchor.X + 650 - screen.X;
                for (float sx = left; sx < right; sx += 25)
                    if (sx < x - Gap || sx > x + Gap)
                        CombatDrawing.Line(batch, new Vector2(sx, y - Length / 2), new Vector2(sx, y + Length / 2), c * (warning ? .15f : .35f), warning ? 2 : 12);
                for (int sign = -1; sign <= 1; sign += 2)
                    CombatDrawing.Line(batch, new Vector2(x + sign * Gap, y - Length / 2), new Vector2(x + sign * Gap, y + Length / 2), Color.White * .85f, 3);
                if (warning) AscendantGlyph.Draw(batch, new Vector2(x, y), 1, 26, Color.White);
                break;
            default:
                if (warning)
                {
                    CombatDrawing.Circle(batch, at, Width / 2 + 7, c, 2);
                    // Fixed line or sampled arc is visible before the projectile starts moving.
                    Vector2 prev = Anchor;
                    for (int i = 1; i <= 12; i++)
                    {
                        float step = Math.Min(Duration, 100) * i / 12f;
                        Vector2 next = Anchor + Launch * step;
                        if (Shape == AscendantShape.Drop) next.Y += Math.Min(step * step * .025f, step * 3.5f);
                        CombatDrawing.Line(batch, prev - screen, next - screen, c * .32f, 2); prev = next;
                    }
                }
                else
                {
                    CombatDrawing.Line(batch, Previous - screen, at, c, Width);
                    if (Shape == AscendantShape.Roller)
                    { CombatDrawing.Circle(batch, at, Width / 2, c, 4); AscendantGlyph.Draw(batch, at, 5, Width / 3, c); }
                    else { CombatDrawing.Disc(batch, at, Width / 2, c); CombatDrawing.Disc(batch, at - new Vector2(2), Width / 5, Color.White * .85f); }
                }
                break;
        }
        if (warning)
        {
            Vector2 clock = (Shape == AscendantShape.Corridor ? Anchor : Projectile.Center) - screen;
            CombatDrawing.Circle(batch, clock, 10 + Width / 2, Color.White * .6f, 2, -MathHelper.PiOver2, MathHelper.Pi * (1 - Age / (float)Delay));
        }
        return false;
    }
    private void DrawRay(SpriteBatch batch, Vector2 start, Vector2 end, Color color, bool warning)
    {
        if (warning)
        {
            Vector2 side = (end - start).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * Width / 2;
            CombatDrawing.Line(batch, start - side, end - side, color * .55f, 2);
            CombatDrawing.Line(batch, start + side, end + side, color * .55f, 2);
            CombatDrawing.Line(batch, start, end, color, 2);
        }
        else { CombatDrawing.Line(batch, start, end, color, Width); CombatDrawing.Line(batch, start, end, Color.White * .75f, Width / 3); }
    }
}

public abstract partial class AscendantBossNPC
{
    public Projectile Emit(AscendantShape shape, Vector2 at, Vector2 velocity, int delay = 75, int duration = 100, float width = 14, float length = 600, float angle = 0, float gap = .9f, bool heavy = false, int mark = 0)
    {
        if (!Authority || Cancelled || !Ready || !AscendantWorld.Open(Index)) return null;
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is AscendantHazard h && h.OwnedBy(this)) count++;
        if (count >= (Index == 0 ? 26 : 34)) return null;
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), WorldPoint(at), velocity, ModContent.ProjectileType<AscendantHazard>(), EngineProjectileDamage(heavy), 0, Main.myPlayer, NPC.whoAmI + 1, Index, (int)shape);
        if (slot < 0 || slot >= Main.maxProjectiles) return null;
        Projectile shot = Main.projectile[slot];
        ((AscendantHazard)shot.ModProjectile).Setup(this, delay, duration, width, length, angle, gap, mark);
        return shot;
    }
    public Projectile SpawnTrace(int delay = 72, int mark = 99)
    {
        if (TraceCount < 2) return null;
        Projectile p = Emit(AscendantShape.Trace, Trace[0], Vector2.Zero, delay, 140, width: 20, mark: mark);
        if (p?.ModProjectile is AscendantHazard h) { h.Count = TraceCount; Array.Copy(Trace, h.Points, Trace.Length); p.netUpdate = true; }
        return p;
    }
}

/// <summary>Distinct silhouettes readable even without color: leaf, candle, hexagon, bell, wave and three seed marks.</summary>
public static class AscendantGlyph
{
    public static void Draw(SpriteBatch batch, Vector2 at, int mark, float radius, Color color)
    {
        int sides = (mark % 8) switch { 0 => 4, 1 => 3, 2 => 6, 3 => 5, 4 => 8, 5 => 7, 6 => 12, _ => 3 };
        for (int i = 0; i < sides; i++)
        {
            float a = i * MathHelper.TwoPi / sides - MathHelper.PiOver2, b = (i + 1) * MathHelper.TwoPi / sides - MathHelper.PiOver2;
            Vector2 stretch = mark % 8 == 0 ? new Vector2(.6f, 1.2f) : Vector2.One;
            CombatDrawing.Line(batch, at + a.ToRotationVector2() * radius * stretch, at + b.ToRotationVector2() * radius * stretch, color, 2);
        }
        if (mark % 2 == 0) CombatDrawing.Line(batch, at - new Vector2(0, radius * .65f), at + new Vector2(0, radius * .65f), color, 2);
        else CombatDrawing.Circle(batch, at, radius * .35f, color, 2);
    }
}
