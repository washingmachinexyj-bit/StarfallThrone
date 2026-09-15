using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Divine.Bosses;

public enum DivineShape { Drop, Crown, Wave, Pillar, Beam, Ring, Trace, Offering }

public sealed class DivineHazard : ModProjectile
{
    public override string Texture => DivineCatalog.Root + "Material0";
    public DivineShape Shape => (DivineShape)(int)Projectile.ai[2];
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public int Serial, Age, Delay = 60, Duration = 90, Tier, Count, Mark;
    public float Width = 12, Length = 600, Angle, Gap = .8f, Turn;
    public Vector2 Anchor, Launch, Previous;
    public Vector2[] Points = new Vector2[25];
    public bool Ready;
    private readonly int[] offeringTicks = new int[Main.maxPlayers];
    public DivineBossNPC Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active && Main.npc[OwnerSlot].ModNPC is DivineBossNPC b && b.Serial == Serial && !b.Cancelled ? b : null;
    public bool OwnedBy(DivineBossNPC b) => OwnerSlot == b.NPC.whoAmI && Serial == b.Serial;
    public bool Damaging => Ready && Boss != null && Age >= Delay && Age < Delay + Duration && Shape != DivineShape.Offering;
    public float RingRadius => Math.Max(12, (Age - Delay) * Length / Math.Max(1, Duration));
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16; Projectile.hostile = true; Projectile.friendly = false;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.timeLeft = 500;
        Ready = false; Age = Count = 0;
    }
    public void Setup(DivineBossNPC boss, int delay, int duration, float width, float length, float angle, float gap, float turn, int mark)
    {
        Serial = boss.Serial; Tier = boss.Index; Delay = Math.Max(30, delay); Duration = duration; Width = width; Length = length;
        Angle = angle; Gap = gap; Turn = turn; Mark = mark; Anchor = Projectile.Center; Launch = Projectile.velocity; Previous = Anchor;
        Projectile.width = Projectile.height = Math.Max(2, (int)width); Projectile.Center = Anchor;
        Projectile.timeLeft = Delay + Duration + 3; Ready = true; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Damaging ? null : false;
    public override void AI()
    {
        if (!Ready) return;
        DivineBossNPC boss = Boss;
        if (boss == null) { Projectile.Kill(); return; }
        if (++Age >= Delay + Duration) { Projectile.Kill(); return; }
        Previous = Projectile.Center;
        if (Age < Delay) return;
        switch (Shape)
        {
            case DivineShape.Drop:
                Projectile.velocity.Y = Math.Min(9, Projectile.velocity.Y + .12f);
                Projectile.Center += Projectile.velocity;
                break;
            case DivineShape.Crown:
            case DivineShape.Wave: Projectile.Center += Launch; break;
            case DivineShape.Ring: Angle += Turn; break;
            case DivineShape.Trace:
                if (Count >= 2)
                {
                    float t = Math.Clamp((Age - Delay) / (float)Math.Max(1, Duration - 1), 0, 1) * (Count - 1);
                    int i = Math.Min(Count - 2, (int)t);
                    Projectile.Center = Vector2.Lerp(Points[i], Points[i + 1], t - i);
                    if (Age == Delay) Previous = Projectile.Center;
                }
                break;
            case DivineShape.Offering:
                if (boss.Authority)
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        Player p = Main.player[i];
                        if (p.active && !p.dead && Vector2.Distance(p.Center, Anchor) < Width)
                        {
                            if (++offeringTicks[i] == 60)
                            {
                                boss.AddOffering(Mark, 160);
                                // Staying to offer is optional, and locks this clear warning where the player stood.
                                boss.Emit(DivineShape.Beam, DivineBossNPC.WorldPoint(p.Center - new Vector2(0, 450)), Vector2.Zero, delay: 75, duration: 24, width: 18, length: 700, angle: MathHelper.PiOver2);
                            }
                        }
                        else offeringTicks[i] = 0;
                    }
                break;
        }
        Projectile.rotation = (Projectile.Center - Previous).ToRotation();
    }
    private bool SegmentHit(Rectangle target, Vector2 start, Vector2 end, float width)
    {
        float point = 0;
        return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), start, end, width, ref point);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!Damaging) return false;
        switch (Shape)
        {
            case DivineShape.Beam: return SegmentHit(targetHitbox, Anchor, Anchor + Angle.ToRotationVector2() * Length, Width);
            case DivineShape.Pillar:
                return new Rectangle((int)(Anchor.X - Width / 2), (int)(Anchor.Y - Length), (int)Width, (int)Length).Intersects(targetHitbox);
            case DivineShape.Crown:
                Vector2 wing = new(0, Width / 2), tip = Projectile.Center + new Vector2(Width, 0);
                return SegmentHit(targetHitbox, Projectile.Center - wing, tip, 4) || SegmentHit(targetHitbox, Projectile.Center + wing, tip, 4);
            case DivineShape.Ring:
                // The same 96 polygon segments are used for visual and native collision; no center-point gap shortcut.
                for (int i = 0; i < 96; i++)
                {
                    float a = MathHelper.TwoPi * i / 96, b = MathHelper.TwoPi * (i + 1) / 96;
                    if (Math.Abs(MathHelper.WrapAngle((a + b) / 2 - Angle)) < Gap) continue;
                    if (SegmentHit(targetHitbox, Anchor + a.ToRotationVector2() * RingRadius, Anchor + b.ToRotationVector2() * RingRadius, Width)) return true;
                }
                return false;
            default:
                return SegmentHit(targetHitbox, Previous, Projectile.Center, Width) || Projectile.Hitbox.Intersects(targetHitbox);
        }
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Serial); w.Write(Age); w.Write(Delay); w.Write(Duration); w.Write(Tier); w.Write(Count); w.Write(Mark);
        w.Write(Width); w.Write(Length); w.Write(Angle); w.Write(Gap); w.Write(Turn);
        DivineBossNPC.WritePoint(w, Anchor); DivineBossNPC.WritePoint(w, Launch); DivineBossNPC.WritePoint(w, Previous);
        for (int i = 0; i < 25; i++) DivineBossNPC.WritePoint(w, Points[i]);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Serial = r.ReadInt32(); Age = Math.Clamp(r.ReadInt32(), 0, 1200); Delay = Math.Clamp(r.ReadInt32(), 30, 600); Duration = Math.Clamp(r.ReadInt32(), 1, 600);
        Tier = Math.Clamp(r.ReadInt32(), 0, 2); Count = Math.Clamp(r.ReadInt32(), 0, 25); Mark = Math.Clamp(r.ReadInt32(), 0, 4);
        Width = Math.Clamp(r.ReadSingle(), 2, 150); Length = Math.Clamp(r.ReadSingle(), 0, 2200); Angle = r.ReadSingle(); Gap = r.ReadSingle(); Turn = r.ReadSingle();
        Anchor = DivineBossNPC.ReadPoint(r); Launch = new Vector2(r.ReadSingle(), r.ReadSingle()); Previous = DivineBossNPC.ReadPoint(r);
        for (int i = 0; i < 25; i++) Points[i] = DivineBossNPC.ReadPoint(r);
        Projectile.width = Projectile.height = (int)Width; Projectile.timeLeft = Math.Max(1, Delay + Duration - Age + 3);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        bool warning = Age < Delay;
        Color tint = Shape == DivineShape.Trace && Mark == 2 ? new Color(199, 181, 255) : DivineCatalog.Colors[Math.Clamp(Tier, 0, 2)], c = tint * (warning ? .35f + .45f * Age / Delay : .92f);
        SpriteBatch batch = Main.spriteBatch; Vector2 screen = Main.screenPosition, at = Projectile.Center - screen;
        if (Shape == DivineShape.Beam)
        {
            CombatDrawing.Line(batch, Anchor - screen, Anchor + Angle.ToRotationVector2() * Length - screen, c, warning ? 2 : Width);
            if (!warning) CombatDrawing.Line(batch, Anchor - screen, Anchor + Angle.ToRotationVector2() * Length - screen, Color.White * .75f, Width / 3);
        }
        else if (Shape == DivineShape.Pillar)
        {
            if (warning)
            {
                CombatDrawing.Circle(batch, Anchor - screen, Width / 2 + 6, c, 2);
                CombatDrawing.Line(batch, Anchor - screen, Anchor - new Vector2(0, Length) - screen, c * .5f, 2);
                if (Mark > 0) Utils.DrawBorderString(batch, Mark.ToString(), Anchor - screen - new Vector2(6, 28), Color.White, .7f);
            }
            else
            {
                CombatDrawing.Line(batch, Anchor - screen, Anchor - new Vector2(0, Length) - screen, c, Width);
                CombatDrawing.Line(batch, Anchor - screen, Anchor - new Vector2(0, Length) - screen, Color.White * .75f, Width / 3);
            }
        }
        else if (Shape == DivineShape.Ring)
        {
            float radius = warning ? 55 : RingRadius;
            for (int i = 0; i < 96; i++)
            {
                float a = MathHelper.TwoPi * i / 96, b = MathHelper.TwoPi * (i + 1) / 96;
                if (Math.Abs(MathHelper.WrapAngle((a + b) / 2 - Angle)) < Gap) continue;
                CombatDrawing.Line(batch, Anchor - screen + a.ToRotationVector2() * radius, Anchor - screen + b.ToRotationVector2() * radius, c, warning ? 2 : Width);
            }
            if (warning) CombatDrawing.Line(batch, Anchor - screen, Anchor - screen + Angle.ToRotationVector2() * 100, Color.White * .6f, 2);
            if (warning && Mark > 0) Utils.DrawBorderString(batch, Mark.ToString(), Anchor - screen + Angle.ToRotationVector2() * 110, Color.White, .9f);
        }
        else if (Shape == DivineShape.Trace)
        {
            for (int i = 1; i < Count; i++) CombatDrawing.Line(batch, Points[i - 1] - screen, Points[i] - screen, tint * (warning ? .6f : .2f), 2);
            if (!warning) { CombatDrawing.Disc(batch, at, Width / 2, c); CombatDrawing.Line(batch, Previous - screen, at, Color.White, 4); }
        }
        else if (Shape == DivineShape.Offering)
        {
            CombatDrawing.Circle(batch, at, Width, c, 3);
            Utils.DrawBorderString(batch, (Mark + 1).ToString(), at - new Vector2(5, 12), Color.Gold, .75f);
        }
        else
        {
            if (warning)
            {
                CombatDrawing.Circle(batch, at, Width / 2 + 5, c, 2);
                CombatDrawing.Line(batch, at, at + Launch.SafeNormalize(Vector2.UnitY) * 50, c, 2);
            }
            else if (Shape == DivineShape.Crown)
            {
                Vector2 wing = new(0, Width / 2);
                CombatDrawing.Line(batch, at - wing, at + new Vector2(Width, 0), c, 4);
                CombatDrawing.Line(batch, at + wing, at + new Vector2(Width, 0), c, 4);
            }
            else { CombatDrawing.Disc(batch, at, Width / 2, c); CombatDrawing.Disc(batch, at - new Vector2(2, 2), Width / 5, Color.White * .8f); }
        }
        return false;
    }
}

public abstract partial class DivineBossNPC
{
    public Projectile Emit(DivineShape shape, Vector2 at, Vector2 velocity, int delay = 60, int duration = 100, float width = 14, float length = 700, float angle = 0, float gap = .8f, float turn = 0, bool heavy = false, int mark = 0)
    {
        if (!Authority || Cancelled || !Ready) return null;
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is DivineHazard h && h.OwnedBy(this)) count++;
        if (count >= (Index == 0 ? 18 : 28)) return null;
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), WorldPoint(at), velocity, ModContent.ProjectileType<DivineHazard>(), shape == DivineShape.Offering ? 0 : EngineProjectileDamage(heavy), 0, Main.myPlayer, NPC.whoAmI + 1, Index, (int)shape);
        if (slot < 0 || slot >= Main.maxProjectiles) return null;
        Projectile shot = Main.projectile[slot];
        ((DivineHazard)shot.ModProjectile).Setup(this, delay, duration, width, length, angle, gap, turn, mark);
        return shot;
    }
}
