using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Bosses;

public enum VoyageHazardShape { Bolt, Arc, Beam, Ring, Mine, Pillar, Wave, GravityField, Current, Lane }

/// <summary>Every collision primitive is drawn using the same dimensions and committed geometry.</summary>
public sealed class VoyageHazard : ModProjectile
{
    public override string Texture => VoyageCatalog.Root + "Material0";
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public int BossIndex => Math.Clamp((int)Projectile.ai[1], 0, VoyageCatalog.Count - 1);
    public VoyageHazardShape Shape => (VoyageHazardShape)(int)Projectile.ai[2];
    public int Serial, Age, Delay, Duration, DeviceSlot = -1;
    public float Radius = 16, Length = 1000, Angle, Turn, Gap = .65f;
    public Vector2 Anchor, Launch;
    public bool Ready;
    public bool ActiveDamage => Ready && Boss != null && Age >= Delay && Age < Delay + Duration && Shape is not VoyageHazardShape.GravityField and not VoyageHazardShape.Current;
    public bool Stationary => Shape is not VoyageHazardShape.Bolt and not VoyageHazardShape.Arc and not VoyageHazardShape.Wave;
    public VoyageBossNPC Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active &&
        Main.npc[OwnerSlot].ModNPC is VoyageBossNPC boss && boss.Serial == Serial && boss.BossIndex == BossIndex ? boss : null;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.hostile = true;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 600;
        Projectile.netImportant = true;
    }
    public void Configure(VoyageBossNPC boss, int delay, int duration, float radius, float angle, float turn, int source, float length, float gap)
    {
        Serial = boss.Serial;
        Delay = Math.Max(60, delay);
        Duration = Math.Clamp(duration, 1, 260);
        Radius = radius;
        Angle = angle;
        Turn = turn;
        DeviceSlot = source;
        Length = length;
        Gap = gap;
        Anchor = Projectile.Center;
        Launch = Projectile.velocity;
        Projectile.timeLeft = Delay + Duration + 5;
        Ready = true;
        Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => Ready && Age >= Delay && !Stationary;
    public override bool? CanDamage() => ActiveDamage ? null : false;
    public override void AI()
    {
        if (!Ready) { Projectile.velocity = Vector2.Zero; return; }
        VoyageBossNPC boss = Boss;
        if (boss == null)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || ++Projectile.localAI[0] > 60) Projectile.Kill();
            return;
        }
        Projectile.localAI[0] = 0;
        bool invalidDevice = DeviceSlot >= 0 && (DeviceSlot >= Main.maxNPCs || !Main.npc[DeviceSlot].active ||
            Main.npc[DeviceSlot].ModNPC is not VoyageDeviceNPC device || !device.OwnedBy(boss));
        if (boss == null || !boss.NPC.HasValidTarget || invalidDevice)
        {
            Projectile.Kill();
            return;
        }
        Age++;
        if (Age >= Delay + Duration) { Projectile.Kill(); return; }
        if (Age < Delay)
        {
            Projectile.Center = Anchor;
            Projectile.velocity = Launch;
            return;
        }
        if (Age == Delay) Projectile.velocity = Launch;
        int activeAge = Age - Delay;
        switch (Shape)
        {
            case VoyageHazardShape.Arc: Projectile.velocity.Y += .11f; break;
            case VoyageHazardShape.Wave:
                Projectile.velocity = Launch + Launch.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * (MathF.Sin(activeAge * .09f) * 2.5f);
                break;
            case VoyageHazardShape.GravityField:
            case VoyageHazardShape.Current:
                if (Main.netMode != NetmodeID.Server && Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers)
                {
                    Player player = Main.player[Main.myPlayer];
                    if (player.active && !player.dead && player.Distance(Anchor) < Radius)
                    {
                        if (Shape == VoyageHazardShape.GravityField)
                        {
                            // Local low-gravity pocket, not loss of control or reversed controls.
                            player.velocity.Y -= .12f;
                            if (player.velocity.Y < -12) player.velocity.Y = -12;
                        }
                        else
                        {
                            Vector2 direction = Turn == 0 ? Angle.ToRotationVector2() : (Anchor - player.Center).SafeNormalize(Vector2.Zero).RotatedBy(Turn);
                            player.velocity += direction * .1f;
                        }
                    }
                }
                break;
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
    }
    public float BeamAngle => Angle + Turn * Math.Max(0, Age - Delay);
    public float RingRadius => Radius * Math.Clamp((Age - Delay + 12f) / Duration, 0, 1);
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!ActiveDamage) return false;
        float distance = 0;
        Vector2 center = targetHitbox.Center.ToVector2();
        switch (Shape)
        {
            case VoyageHazardShape.Beam:
            case VoyageHazardShape.Lane:
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Anchor,
                    Anchor + BeamAngle.ToRotationVector2() * Length, Radius * 2, ref distance);
            case VoyageHazardShape.Pillar:
                return targetHitbox.Intersects(new Rectangle((int)(Anchor.X - Radius), (int)(Anchor.Y - Length), (int)(Radius * 2), (int)Length));
            case VoyageHazardShape.Mine:
                return Vector2.DistanceSquared(Anchor, targetHitbox.ClosestPointInRect(Anchor)) < Radius * Radius;
            case VoyageHazardShape.Ring:
                Vector2 delta = center - Anchor;
                float margin = Math.Max(targetHitbox.Width, targetHitbox.Height) * .5f;
                if (Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - Angle)) < Gap) return false;
                return Math.Abs(delta.Length() - RingRadius) < 12 + margin;
            case VoyageHazardShape.GravityField:
            case VoyageHazardShape.Current: return false;
            default:
                return Vector2.DistanceSquared(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) < Radius * Radius;
        }
    }
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Ready); writer.Write(Serial); writer.Write(Age); writer.Write(Delay); writer.Write(Duration); writer.Write(DeviceSlot);
        writer.Write(Radius); writer.Write(Length); writer.Write(Angle); writer.Write(Turn); writer.Write(Gap);
        VoyageBossNPC.WriteVector(writer, Anchor); VoyageBossNPC.WriteVector(writer, Launch);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Ready = reader.ReadBoolean(); Serial = reader.ReadInt32(); Age = reader.ReadInt32(); Delay = reader.ReadInt32(); Duration = reader.ReadInt32(); DeviceSlot = reader.ReadInt32();
        Radius = reader.ReadSingle(); Length = reader.ReadSingle(); Angle = reader.ReadSingle(); Turn = reader.ReadSingle(); Gap = reader.ReadSingle();
        Anchor = VoyageBossNPC.ReadVector(reader); Launch = VoyageBossNPC.ReadVector(reader);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        SpriteBatch batch = Main.spriteBatch;
        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 anchor = Anchor - Main.screenPosition;
        Color tint = VoyageCatalog.Colors[BossIndex];
        bool warning = Age < Delay;
        Color color = warning ? tint * (.25f + .3f * (Age / (float)Delay)) : Color.Lerp(tint, Color.White, .25f) * .85f;
        float width = warning ? 2 : Math.Max(2, Radius * 2);
        switch (Shape)
        {
            case VoyageHazardShape.Beam:
            case VoyageHazardShape.Lane:
                CombatDrawing.Line(batch, anchor, anchor + BeamAngle.ToRotationVector2() * Length, color, width);
                if (warning)
                {
                    Vector2 normal = Angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Radius;
                    CombatDrawing.Line(batch, anchor + normal, anchor + normal + Angle.ToRotationVector2() * Length, tint * .2f, 1);
                    CombatDrawing.Line(batch, anchor - normal, anchor - normal + Angle.ToRotationVector2() * Length, tint * .2f, 1);
                    if (Turn != 0) CombatDrawing.Line(batch, anchor, anchor + (Angle + Turn * Duration).ToRotationVector2() * Length, tint * .18f, 2);
                }
                else CombatDrawing.Line(batch, anchor, anchor + BeamAngle.ToRotationVector2() * Length, Color.White * .6f, Math.Max(2, Radius * .6f));
                break;
            case VoyageHazardShape.Pillar:
                CombatDrawing.Line(batch, anchor, anchor - Vector2.UnitY * Length, color, width);
                CombatDrawing.Line(batch, anchor - new Vector2(Radius, 0), anchor + new Vector2(Radius, 0), tint, 4);
                if (warning)
                {
                    CombatDrawing.Line(batch, anchor - new Vector2(Radius, 0), anchor - new Vector2(Radius, Length), tint * .25f, 1);
                    CombatDrawing.Line(batch, anchor + new Vector2(Radius, 0), anchor + new Vector2(Radius, -Length), tint * .25f, 1);
                }
                break;
            case VoyageHazardShape.Ring:
                CombatDrawing.Circle(batch, anchor, warning ? 24 : RingRadius, color, warning ? 3 : 20, Angle, Gap);
                if (warning) CombatDrawing.Circle(batch, anchor, Radius, tint * .2f, 2, Angle, Gap);
                // Safe-sector rays remain visible during expansion.
                CombatDrawing.Line(batch, anchor, anchor + (Angle - Gap).ToRotationVector2() * Radius, Color.LightGreen * .18f, 2);
                CombatDrawing.Line(batch, anchor, anchor + (Angle + Gap).ToRotationVector2() * Radius, Color.LightGreen * .18f, 2);
                break;
            case VoyageHazardShape.Mine:
                CombatDrawing.Circle(batch, anchor, Radius, color, warning ? 2 : 5);
                if (!warning) CombatDrawing.Disc(batch, anchor, Radius, tint * .22f);
                CombatDrawing.Line(batch, anchor - new Vector2(10, 0), anchor + new Vector2(10, 0), color, 3);
                CombatDrawing.Line(batch, anchor - new Vector2(0, 10), anchor + new Vector2(0, 10), color, 3);
                break;
            case VoyageHazardShape.GravityField:
            case VoyageHazardShape.Current:
                CombatDrawing.Circle(batch, anchor, Radius, color * .55f, 3, Age * .02f, .35f);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = anchor + (i * MathHelper.PiOver2 + Age * .015f).ToRotationVector2() * Radius * .65f;
                    CombatDrawing.Line(batch, p, p + (Shape == VoyageHazardShape.GravityField ? -Vector2.UnitY : Angle.ToRotationVector2()) * 25, color * .6f, 3);
                }
                break;
            default:
                if (warning)
                {
                    CombatDrawing.Circle(batch, anchor, Radius + 5, color, 2);
                    CombatDrawing.Line(batch, anchor, anchor + Launch.SafeNormalize(Vector2.UnitY) * 90, color * .6f, 2);
                }
                else
                {
                    Texture2D tex = ModContent.Request<Texture2D>(VoyageCatalog.Root + "Material" + BossIndex).Value;
                    batch.Draw(tex, center, null, Color.White, Projectile.rotation, tex.Size() * .5f, Radius * 2 / Math.Max(tex.Width, tex.Height), SpriteEffects.None, 0);
                    CombatDrawing.Circle(batch, center, Radius, tint * .65f, 2);
                }
                break;
        }
        return false;
    }
}

public abstract partial class VoyageBossNPC
{
    /// <summary>Damage values are pre-defense player hits, divided by Terraria's hostile projectile multipliers.</summary>
    public Projectile Emit(VoyageHazardShape shape, Vector2 at, Vector2 velocity, int delay = 65, int duration = 100,
        float radius = 16, float angle = 0, float damageScale = 1, int deviceSlot = -1, float length = 1100, float turn = 0, float gap = .65f)
    {
        if (!IsServer) return null;
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is VoyageHazard h && h.OwnerSlot == NPC.whoAmI && h.Serial == Serial) count++;
        if (count >= (BossIndex == 16 ? 42 : 32)) return null;
        float difficulty = Main.masterMode ? 1.12f : Main.expertMode ? 1.06f : 1;
        float hostileMultiplier = Main.masterMode ? 6 : Main.expertMode ? 4 : 2;
        int damage = (int)((620 + BossIndex * 20) * damageScale * difficulty / hostileMultiplier);
        if (shape is VoyageHazardShape.GravityField or VoyageHazardShape.Current) damage = 0;
        int id = Projectile.NewProjectile(NPC.GetSource_FromAI(), at, velocity, ModContent.ProjectileType<VoyageHazard>(),
            damage, 0, Main.myPlayer, NPC.whoAmI + 1, BossIndex, (int)shape);
        if (id < 0 || id >= Main.maxProjectiles) return null;
        Projectile projectile = Main.projectile[id];
        ((VoyageHazard)projectile.ModProjectile).Configure(this, delay, duration, radius, angle, turn, deviceSlot, length, gap);
        return projectile;
    }
}
