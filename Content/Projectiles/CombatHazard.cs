using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Projectiles;

public enum HazardShape { Bolt, Gravity, Wave, Homing, Bounce, Mine, Beam, Pillar, Ring, Spiral }

public sealed class CombatHazard : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/StarfallBolt";
    public HazardShape Shape => (HazardShape)(int)Projectile.ai[2];
    public int Age;
    public int Delay = 12, Duration = 120, Bounces = 1;
    public float Size = 18, Length = 600, Turn, Angle, Gap = .65f;
    public Vector2 Anchor, LaunchVelocity;
    public bool Ready;
    public bool Stationary => Shape is HazardShape.Beam or HazardShape.Mine or HazardShape.Pillar or HazardShape.Ring;
    public int OwnerSlot => (int)Projectile.ai[0] - 1;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18; Projectile.hostile = true; Projectile.penetrate = -1;
        Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.timeLeft = 300;
    }
    public void Setup(int delay, int duration, float size, float length, float turn, float gap)
    {
        Delay = delay; Duration = duration; Size = size; Length = length; Turn = turn; Gap = gap;
        Anchor = Projectile.Center; LaunchVelocity = Projectile.velocity; Angle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
        Projectile.timeLeft = delay + duration + 2; Projectile.width = Projectile.height = (int)size;
        Projectile.Center = Anchor; Projectile.tileCollide = !Stationary && Projectile.ai[1] < 10;
        Ready = true; Projectile.netUpdate = true;
    }
    public override bool ShouldUpdatePosition() => Ready && Age >= Delay && !Stationary;
    public override bool? CanDamage() => !Ready || Age < Delay || Age >= Delay + Duration ? false : null;
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Age); w.Write(Delay); w.Write(Duration); w.Write(Bounces); w.Write(Size); w.Write(Length); w.Write(Turn); w.Write(Angle); w.Write(Gap);
        w.Write(Anchor.X); w.Write(Anchor.Y); w.Write(LaunchVelocity.X); w.Write(LaunchVelocity.Y); w.Write(Ready);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Age = r.ReadInt32(); Delay = r.ReadInt32(); Duration = r.ReadInt32(); Bounces = r.ReadInt32(); Size = r.ReadSingle(); Length = r.ReadSingle();
        Turn = r.ReadSingle(); Angle = r.ReadSingle(); Gap = r.ReadSingle(); Anchor = new(r.ReadSingle(), r.ReadSingle()); LaunchVelocity = new(r.ReadSingle(), r.ReadSingle()); Ready = r.ReadBoolean();
        Projectile.width = Projectile.height = (int)Size;
        Projectile.tileCollide = !Stationary && Projectile.ai[1] < 10;
        Projectile.timeLeft = Math.Max(1, Delay + Duration - Age + 2);
    }
    public override void AI()
    {
        if (!Ready) return;
        int slot = OwnerSlot;
        if (slot < 0 || slot >= Main.maxNPCs || !Main.npc[slot].active || Main.npc[slot].ModNPC is not ReforgedBossNPC owner || owner.EncounterId != (int)Projectile.ai[1])
        { Projectile.Kill(); return; }
        if (++Age >= Delay + Duration) { Projectile.Kill(); return; }
        if (Age < Delay) return;
        int activeAge = Age - Delay;
        switch (Shape)
        {
            case HazardShape.Gravity: Projectile.velocity.Y = Math.Min(11, Projectile.velocity.Y + .14f); break;
            case HazardShape.Wave: Projectile.velocity = LaunchVelocity + LaunchVelocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * MathF.Sin(activeAge * .12f) * 1.4f; break;
            case HazardShape.Homing:
                if (activeAge is >= 15 and <= 45 && owner.NPC.HasValidTarget)
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, (Main.player[owner.NPC.target].Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * LaunchVelocity.Length(), .035f);
                break;
            case HazardShape.Spiral: Projectile.velocity = Projectile.velocity.RotatedBy(Turn); break;
            case HazardShape.Beam: Angle += Turn; Projectile.Center = Anchor; break;
            case HazardShape.Mine: case HazardShape.Pillar: case HazardShape.Ring: Projectile.Center = Anchor; break;
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
    }
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Shape != HazardShape.Bounce || Bounces-- <= 0) return true;
        if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X;
        if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y;
        Projectile.netUpdate = true; return false;
    }
    private float RingRadius => Math.Max(12, (Age - Delay) * Length / Math.Max(1, Duration));
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        if (Shape == HazardShape.Beam)
        {
            float collision = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Anchor, Anchor + Angle.ToRotationVector2() * Length, Size, ref collision);
        }
        if (Shape == HazardShape.Pillar) return new Rectangle((int)(Anchor.X - Size / 2), (int)(Anchor.Y - Length), (int)Size, (int)Length).Intersects(targetHitbox);
        if (Shape == HazardShape.Mine) return Vector2.Distance(targetHitbox.ClosestPointInRect(Anchor), Anchor) < Size;
        if (Shape == HazardShape.Ring)
        {
            Vector2 d = targetHitbox.Center.ToVector2() - Anchor;
            if (Math.Abs(MathHelper.WrapAngle(d.ToRotation() - Angle)) < Gap) return false;
            float margin = targetHitbox.Size().Length() / 2 + 5;
            return Math.Abs(d.Length() - RingRadius) < margin;
        }
        return null;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!Ready) return false;
        Color c = CombatProfiles.Color(Math.Clamp((int)Projectile.ai[1], 0, 26));
        Vector2 at = Projectile.Center - Main.screenPosition;
        bool warn = Age < Delay;
        float alpha = warn ? .3f + .5f * Age / Math.Max(1, Delay) : .9f;
        if (Shape == HazardShape.Beam)
        {
            Vector2 end = at + Angle.ToRotationVector2() * Length;
            CombatDrawing.Line(Main.spriteBatch, at, end, c * alpha, warn ? 2 : Size);
            if (!warn) CombatDrawing.Line(Main.spriteBatch, at, end, Color.White * .85f, Size * .35f);
        }
        else if (Shape == HazardShape.Pillar)
        {
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)(at.X - Size / 2), (int)(at.Y - (warn ? 3 : Length)), (int)Size, warn ? 3 : (int)Length), c * alpha);
            if (warn) CombatDrawing.Line(Main.spriteBatch, at, at - Vector2.UnitY * Length, c * .25f, 2);
        }
        else if (Shape == HazardShape.Mine)
        {
            if (!warn) CombatDrawing.Disc(Main.spriteBatch, at, Size, c * .25f);
            CombatDrawing.Circle(Main.spriteBatch, at, Size, c * alpha, warn ? 2 : 8);
        }
        else if (Shape == HazardShape.Ring) CombatDrawing.Circle(Main.spriteBatch, at, warn ? 20 : RingRadius, c * alpha, warn ? 2 : 9, Angle, Gap);
        else
        {
            Texture2D texture = ModContent.Request<Texture2D>((Projectile.ai[1] < 10 ? "StarfallThrone/Content/Assets/Projectiles/PrimordialBossShot" : Texture)).Value;
            if (warn) CombatDrawing.Circle(Main.spriteBatch, at, Size * .8f, c * alpha, 2);
            Main.EntitySpriteDraw(texture, at, null, Color.Lerp(c, Color.White, .5f) * alpha, Projectile.rotation + MathHelper.PiOver2, texture.Size() / 2, Size * 1.5f / texture.Width, SpriteEffects.None);
        }
        return false;
    }
}
