using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.NPCs;

namespace StarfallThrone.Content.Projectiles;

public sealed class MiniHostileProjectile : ModProjectile
{
    public static readonly string[] Sprites = { "ClayShard", "WindRibbon", "Pollen", "Ember", "FlintShard", "ThornSeed", "DewDrop", "FluffSeed" };
    public override string Texture => "StarfallThrone/Content/Assets/Minis/ClayShard";
    public int Kind => Math.Clamp((int)Projectile.ai[0], 0, 7);
    public int Mode => (int)Projectile.ai[1];
    private int Age => (int)Projectile.localAI[0];
    private bool Ground => Mode is 1 or 3;
    private int WarningTime => Kind == 3 ? 60 : Mode == 3 ? 66 : 54;
    public override void SetDefaults()
    {
        Projectile.width = 12; Projectile.height = 12;
        Projectile.hostile = true; Projectile.friendly = false;
        Projectile.tileCollide = true; Projectile.penetrate = -1; Projectile.timeLeft = 110;
        Projectile.ignoreWater = true;
    }
    public override void OnSpawn(IEntitySource source)
    {
        if (Kind == 7) Projectile.timeLeft = 65;
        if (Ground)
        {
            Vector2 bottom = Projectile.Center;
            Projectile.width = Kind == 3 ? 40 : 30;
            Projectile.height = Kind == 3 ? 18 : 38;
            Projectile.Bottom = bottom;
            Projectile.tileCollide = false;
            Projectile.timeLeft = WarningTime + (Kind == 3 ? 120 : 12);
        }
        else if (Mode == 2)
        {
            Projectile.timeLeft = 12;
            Projectile.width = 18; Projectile.height = 22;
        }
    }
    public override bool? CanDamage() => Ground && Age < WarningTime ? false : null;
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Projectile.width); writer.Write(Projectile.height);
        writer.Write(Projectile.timeLeft); writer.Write(Age);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        // OnSpawn only runs on the spawning peer, so synchronize hitboxes and telegraph age explicitly.
        Projectile.width = Math.Clamp(reader.ReadInt32(), 2, 64);
        Projectile.height = Math.Clamp(reader.ReadInt32(), 2, 64);
        Projectile.timeLeft = Math.Clamp(reader.ReadInt32(), 0, 240);
        Projectile.localAI[0] = Math.Clamp(reader.ReadInt32(), 0, 400);
        Projectile.tileCollide = !Ground;
    }
    public override void AI()
    {
        Projectile.localAI[0]++;
        int owner = (int)Projectile.ai[2] - 1;
        if (owner < 0 || owner >= Main.maxNPCs || !Main.npc[owner].active || Main.npc[owner].ModNPC is not MiniBossNPC)
        { Projectile.Kill(); return; }
        if (Ground) { Projectile.velocity = Vector2.Zero; return; }
        if (Mode != 2)
        {
            if (Kind is 0 or 4 or 5 or 6) Projectile.velocity.Y += 0.12f;
            if (Kind == 7) Projectile.velocity.Y += 0.025f;
            if (Kind == 1) Projectile.velocity = Projectile.velocity.RotatedBy(0.018f);
            if (Kind == 2) { Projectile.velocity.X *= 0.995f; Projectile.velocity.Y = Math.Min(1.4f, Projectile.velocity.Y + 0.012f); }
        }
        Projectile.rotation = Projectile.velocity.ToRotation() + (Kind == 7 ? MathHelper.Pi : MathHelper.PiOver4);
        if (Kind is 1 or 2 or 3) Lighting.AddLight(Projectile.Center, new Vector3(0.22f, 0.18f, 0.08f));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Color color = Kind == 3 ? new Color(255, 195, 70) : new Color(156, 214, 90);
        if (Ground && Age < WarningTime)
        {
            Vector2 bottom = Projectile.Bottom - Main.screenPosition;
            float opacity = 0.4f + 0.5f * Age / WarningTime;
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)bottom.X - Projectile.width / 2, (int)bottom.Y - 3, Projectile.width, 3), color * opacity);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)bottom.X - 2, (int)bottom.Y - 10, 4, 6), color * opacity);
            return false;
        }
        Texture2D texture = ModContent.Request<Texture2D>("StarfallThrone/Content/Assets/Minis/" + Sprites[Kind]).Value;
        if (Ground)
        {
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
                0, texture.Size() / 2f, new Vector2(Projectile.width / (float)texture.Width, Projectile.height / (float)texture.Height), SpriteEffects.None);
        }
        else Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2f, 0.65f, SpriteEffects.None);
        return false;
    }
}

public sealed class MiniMagicProjectile : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Minis/WindRibbon";
    public override void SetDefaults()
    {
        Projectile.width = 12; Projectile.height = 12; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic; Projectile.penetrate = 1;
        Projectile.timeLeft = 65; Projectile.tileCollide = true;
    }
    public override void AI()
    {
        Projectile.rotation = Projectile.velocity.ToRotation() + (Projectile.ai[0] == 2 ? MathHelper.Pi : 0);
        if (Projectile.ai[0] == 2) { Projectile.velocity.Y += 0.055f; Projectile.timeLeft = Math.Min(Projectile.timeLeft, 55); }
        if (Projectile.ai[0] == 1) Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.27f, 0.08f));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>("StarfallThrone/Content/Assets/Minis/" + (Projectile.ai[0] == 2 ? "FluffSeed" : Projectile.ai[0] == 1 ? "Ember" : "WindRibbon")).Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, texture.Size() / 2f, 0.75f, SpriteEffects.None);
        return false;
    }
}
