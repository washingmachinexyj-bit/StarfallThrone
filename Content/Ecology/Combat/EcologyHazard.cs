using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ecology.Combat;

public enum EcologyShape { Shard, Beam, Pool, Wave, Ring, Orbit, Bubble, Pillar }

/// <summary>Owned, finite hazards. Telegraph geometry and collision share committed server data.</summary>
public sealed class EcologyHazard : ModProjectile
{
    public override string Texture => EcologyCatalog.Root + "Core0";
    public EcologyShape Shape => (EcologyShape)(int)Projectile.ai[2];
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public int Serial, OwnerType, Biome, Age, Delay, Duration, PartSlot = -1;
    public bool Ready, Refracted;
    public float Width, Length, Turn;
    public Vector2 Anchor, Launch, End, Previous;
    public NPC OwnerNPC
    {
        get
        {
            if (OwnerSlot < 0 || OwnerSlot >= Main.maxNPCs) return null;
            NPC n = Main.npc[OwnerSlot]; if (!n.active || n.type != OwnerType) return null;
            if (n.ModNPC is EcologyBossNPC b && b.Serial == Serial && !b.Cancelled) return n;
            if (n.ModNPC is EcologyMobNPC m && m.Serial == Serial) return n;
            return null;
        }
    }
    public bool OwnedBy(EcologyBossNPC b) => OwnerSlot == b.NPC.whoAmI && Serial == b.Serial && OwnerType == b.Type;
    public bool Damaging => Ready && OwnerNPC != null && Age >= Delay && Age < Delay + Duration;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16; Projectile.hostile = true; Projectile.tileCollide = false;
        Projectile.ignoreWater = true; Projectile.penetrate = -1; Projectile.timeLeft = 600;
        Ready = Refracted = false; Age = 0; PartSlot = -1;
    }
    public void Setup(NPC owner, int serial, int biome, int delay, int duration, float width, Vector2 end, float turn)
    {
        OwnerType = owner.type; Serial = serial; Biome = biome; Delay = Math.Max(24, delay); Duration = Math.Clamp(duration, 1, 400);
        Width = Math.Clamp(width, 2, 160); End = end; Turn = turn; Anchor = Previous = Projectile.Center; Launch = Projectile.velocity;
        Length = Vector2.Distance(Anchor, End); Projectile.width = Projectile.height = Math.Max(8, (int)width);
        Projectile.Center = Anchor; Projectile.timeLeft = Delay + Duration + 3; Ready = true; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Damaging ? null : false;
    public override void AI()
    {
        if (!Ready) return;
        NPC owner = OwnerNPC;
        if (owner == null || ++Age >= Delay + Duration) { Projectile.Kill(); return; }
        if (PartSlot >= 0 && (PartSlot >= Main.maxNPCs || !Main.npc[PartSlot].active || Main.npc[PartSlot].ModNPC is not EcologyPart attachedPart || attachedPart.Serial != Serial)) { Projectile.Kill(); return; }
        Previous = Projectile.Center;
        if (Age < Delay) return;
        switch (Shape)
        {
            case EcologyShape.Shard:
            case EcologyShape.Wave:
                Projectile.Center += Launch;
                if (Turn != 0) Launch = Launch.RotatedBy(Turn);
                break;
            case EcologyShape.Bubble:
                Projectile.Center += Launch; Launch *= .997f;
                break;
            case EcologyShape.Orbit:
                float a = (Age - Delay) * Turn + Projectile.ai[1];
                Projectile.Center = Anchor + a.ToRotationVector2() * Math.Max(40, Length);
                break;
        }
        if (owner.ModNPC is EcologyBossNPC boss && Main.netMode != NetmodeID.MultiplayerClient)
        {
            // Magnetic nodes only bend this encounter's bullets; they never touch player items or other mods.
            if (boss.Index == 5 && Shape == EcologyShape.Shard)
                foreach (NPC n in Main.ActiveNPCs)
                    if (n.ModNPC is EcologyPart part && part.OwnedBy(boss) && part.Kind == 6 && n.Distance(Projectile.Center) < 240)
                    { Launch = Vector2.Lerp(Launch, (n.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(.8) * Launch.Length(), .025f); Projectile.netUpdate = Age % 20 == 0; }
            // Each shot may use one portal only; slot+serial prevent stale encounters borrowing another's gates.
            if (boss.Index == 9 && Shape == EcologyShape.Shard && !Refracted)
                foreach (NPC n in Main.ActiveNPCs)
                    if (n.ModNPC is EcologyPart part && part.OwnedBy(boss) && part.Kind == 8 && n.Distance(Projectile.Center) < 34)
                    { Projectile.Center = part.Exit; Previous = Projectile.Center; Refracted = true; Delay = Age + 24; Duration = Math.Max(24, Duration - 24); Projectile.netUpdate = true; break; }
        }
        Projectile.rotation = Launch.ToRotation();
    }
    public static bool SegmentHit(Rectangle target, Vector2 start, Vector2 end, float width)
    {
        if (target.Contains(start.ToPoint()) || target.Contains(end.ToPoint())) return true;
        float distance = 0; return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), start, end, width, ref distance);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!Damaging) return false;
        if (Shape is EcologyShape.Beam or EcologyShape.Pillar) return SegmentHit(targetHitbox, Anchor, End, Width);
        if (Shape == EcologyShape.Pool) return new Rectangle((int)(Anchor.X - Width), (int)(Anchor.Y - 9), (int)Width * 2, 18).Intersects(targetHitbox);
        if (Shape == EcologyShape.Ring)
        {
            float radius = Math.Max(8, (Age - Delay) * Length / Duration);
            for (int i = 0; i < 48; i++)
            {
                float a = i * MathHelper.TwoPi / 48;
                // Fixed upward opening is displayed and physically safe.
                if (Math.Abs(MathHelper.WrapAngle(a + MathHelper.PiOver2)) < .55f) continue;
                if (SegmentHit(targetHitbox, Anchor + a.ToRotationVector2() * radius, Anchor + (a + MathHelper.TwoPi / 48).ToRotationVector2() * radius, Width)) return true;
            }
            return false;
        }
        return SegmentHit(targetHitbox, Previous, Projectile.Center, Width);
    }
    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        if (Shape == EcologyShape.Pool || Biome == 1) target.AddBuff(BuffID.Slow, 45);
        if (Biome == 4) target.AddBuff(BuffID.OnFire, 90);
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Refracted); w.Write(Serial); w.Write(OwnerType); w.Write(Biome); w.Write(Age); w.Write(Delay); w.Write(Duration);
        w.Write(Width); w.Write(Length); w.Write(Turn); w.Write(PartSlot); EcologyBossNPC.Write(w, Anchor); EcologyBossNPC.Write(w, Launch); EcologyBossNPC.Write(w, End);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Refracted = r.ReadBoolean(); Serial = r.ReadInt32(); OwnerType = r.ReadInt32(); Biome = Math.Clamp(r.ReadInt32(), 0, 9);
        Age = r.ReadInt32(); Delay = r.ReadInt32(); Duration = r.ReadInt32(); Width = r.ReadSingle(); Length = r.ReadSingle(); Turn = r.ReadSingle(); PartSlot = r.ReadInt32();
        Anchor = EcologyBossNPC.Read(r); Launch = new Vector2(r.ReadSingle(), r.ReadSingle()); End = EcologyBossNPC.Read(r); Previous = Projectile.Center;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        SpriteBatch batch = Main.spriteBatch; Vector2 camera = Main.screenPosition;
        Color c = EcologyCatalog.Colors[Biome]; bool warning = Age < Delay; float alpha = warning ? .35f + .35f * Age / Math.Max(1f, Delay) : .85f;
        if (Shape is EcologyShape.Beam or EcologyShape.Pillar) CombatDrawing.Line(batch, Anchor - camera, End - camera, c * alpha, warning ? 2 : Width);
        else if (Shape == EcologyShape.Pool) CombatDrawing.Line(batch, Anchor - camera - new Vector2(Width, 0), Anchor - camera + new Vector2(Width, 0), c * alpha, warning ? 2 : 16);
        else if (Shape == EcologyShape.Ring) CombatDrawing.Circle(batch, Anchor - camera, warning ? 30 : Math.Max(8, (Age - Delay) * Length / Duration), c * alpha, warning ? 2 : Width, -MathHelper.PiOver2, .55f);
        else
        {
            if (warning)
            {
                if (Shape == EcologyShape.Orbit) CombatDrawing.Circle(batch, Anchor - camera, Math.Max(40, Length), c * .3f, 2);
                else CombatDrawing.Line(batch, Projectile.Center - camera, Projectile.Center + Launch.SafeNormalize(Vector2.UnitX) * 110 - camera, c * .5f, 2);
            }
            Texture2D tex = ModContent.Request<Texture2D>(EcologyCatalog.Root + "Core" + Biome).Value;
            float size = Shape == EcologyShape.Bubble ? Width * 2 : Math.Max(14, Width * 1.5f);
            batch.Draw(tex, Projectile.Center - camera, null, c * alpha, Projectile.rotation, tex.Size() / 2, size / Math.Max(tex.Width, tex.Height), SpriteEffects.None, 0);
            if (Shape == EcologyShape.Bubble) CombatDrawing.Circle(batch, Projectile.Center - camera, Width, c * alpha, 2);
        }
        return false;
    }
}
