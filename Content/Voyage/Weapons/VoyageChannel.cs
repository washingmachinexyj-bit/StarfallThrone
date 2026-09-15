#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Weapons;

/// <summary>The owning client alone changes the charge/release state; aim is ExtraAI-synchronized.</summary>
public sealed class VoyageChannel : ModProjectile, IVoyageWeaponProjectile
{
    public int WeaponIndex => Math.Clamp((int)Projectile.ai[0], 0, 35);
    public bool Secondary => false;
    public int Charge => (int)Projectile.ai[2];
    public bool Released => Projectile.ai[1] > 0;
    public Vector2 Aim = Vector2.UnitX;
    public int Age;
    private bool emitted;
    public override string Texture => VoyageArsenal.Art(2);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 26; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.friendly = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
    }
    public override void OnSpawn(IEntitySource source)
    { Aim = Projectile.velocity.SafeNormalize(Vector2.UnitX); Projectile.DamageType = VoyageArsenal.Class(WeaponIndex); }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;
    public override bool? CanCutTiles() => false;
    public override void SendExtraAI(BinaryWriter w) { w.Write(Aim.X); w.Write(Aim.Y); w.Write(Age); w.Write(emitted); }
    public override void ReceiveExtraAI(BinaryReader r)
    { Aim = new(r.ReadSingle(), r.ReadSingle()); Age = r.ReadInt32(); emitted = r.ReadBoolean(); Projectile.DamageType = VoyageArsenal.Class(WeaponIndex); }
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner];
        if (!p.active || p.dead || p.CCed || p.noItems || p.HeldItem.type != VoyageCatalog.Weapon(WeaponIndex)) { Projectile.Kill(); return; }
        Age++; Projectile.timeLeft = 2; Projectile.velocity = Vector2.Zero;
        bool owner = Projectile.owner == Main.myPlayer;
        if (!Released)
        {
            if (owner)
            {
                Vector2 newAim = (Main.MouseWorld - p.MountedCenter).SafeNormalize(new Vector2(p.direction, 0));
                if (Age % 6 == 0 || Vector2.DistanceSquared(Aim, newAim) > .035f) { Aim = newAim; Projectile.netUpdate = true; }
                Projectile.ai[2] = Math.Min(WeaponIndex == 34 ? 180 : 90, Projectile.ai[2] + 1);
                bool paid = true;
                if (WeaponIndex == 34 && Age % 30 == 0)
                    paid = p.CheckMana(p.HeldItem, Math.Max(1, (int)(12 * p.manaCost)), true);
                if (!p.channel || !paid || Age >= 900) { Projectile.ai[1] = 1; Projectile.netUpdate = true; }
            }
            else Projectile.ai[2] = Math.Min(WeaponIndex == 34 ? 180 : 90, Projectile.ai[2] + 1);
            if (WeaponIndex == 34 && !Released && Age % 45 == 0 && owner)
                VoyageArsenal.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponIndex, VoyageShotKind.FlameArc,
                    StarPosition(p), Aim, (int)(Projectile.damage * .32f), 2, 100);
        }
        Projectile.Center = WeaponIndex == 34 ? StarPosition(p) : p.MountedCenter + Aim * 38;
        p.heldProj = Projectile.whoAmI; p.direction = Aim.X >= 0 ? 1 : -1;
        p.itemRotation = (Aim * p.direction).ToRotation();
        p.itemTime = 2; p.itemAnimation = 2;
        if (Released)
        {
            if (owner && !emitted) { emitted = true; Release(p); Projectile.netUpdate = true; }
            Projectile.ai[1]++;
            int recovery = WeaponIndex == 33 ? 42 : WeaponIndex == 8 ? 36 : 20;
            if (Projectile.ai[1] >= recovery) Projectile.Kill();
        }
    }
    private Vector2 StarPosition(Player p)
    {
        float distance = Math.Max(24, VoyageShot.Clip(p.MountedCenter, Aim, 150) - 24);
        return p.MountedCenter + Aim * distance;
    }
    private void Release(Player p)
    {
        float charge = Math.Clamp(Charge / (WeaponIndex == 34 ? 180f : 90f), 0, 1);
        int slot;
        switch (WeaponIndex)
        {
            case 2:
                slot = VoyageArsenal.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponIndex, VoyageShotKind.Beam,
                    p.MountedCenter + Aim * 28, Aim, (int)(Projectile.damage * (.5f + 1.7f * charge)), Projectile.knockBack, 100);
                if (slot >= 0)
                {
                    var shot = (VoyageShot)Main.projectile[slot].ModProjectile;
                    shot.Delay = 0; shot.Range = 550 + charge * 850; shot.Projectile.timeLeft = 16; shot.Projectile.netUpdate = true;
                }
                break;
            case 8:
                slot = VoyageArsenal.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponIndex, VoyageShotKind.Spear,
                    p.MountedCenter, Aim, (int)(Projectile.damage * (.85f + charge * .9f)), Projectile.knockBack, 100);
                if (slot >= 0) { var shot = (VoyageShot)Main.projectile[slot].ModProjectile; shot.Range = 190 + charge * 230; shot.Projectile.netUpdate = true; }
                break;
            case 33:
                slot = VoyageArsenal.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponIndex, VoyageShotKind.Beam,
                    p.MountedCenter + Aim * 36, Aim, (int)(Projectile.damage * (.6f + charge * 1.8f)), Projectile.knockBack, 100);
                if (slot >= 0) { var shot = (VoyageShot)Main.projectile[slot].ModProjectile; shot.Delay = 0; shot.Projectile.timeLeft = 18; shot.Projectile.netUpdate = true; }
                break;
            case 34:
                VoyageArsenal.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponIndex, VoyageShotKind.StarBall,
                    StarPosition(p), Aim * 18, (int)(Projectile.damage * (.7f + charge * 1.6f)), Projectile.knockBack, 100);
                break;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return false;
        Player p = Main.player[Projectile.owner]; Color tint = VoyageArsenal.Color(WeaponIndex);
        float charge = Math.Clamp(Charge / (WeaponIndex == 34 ? 180f : 90f), 0, 1);
        Vector2 at = Projectile.Center - Main.screenPosition;
        Texture2D art = ModContent.Request<Texture2D>(VoyageArsenal.Art(WeaponIndex)).Value;
        if (WeaponIndex == 34 && !Released)
        {
            CombatDrawing.Disc(Main.spriteBatch, at, 20 + charge * 18, tint * .25f);
            for (int n = 0; n < 3; n++) CombatDrawing.Circle(Main.spriteBatch, at, 27 + n * 8 + charge * 12, tint * (.8f - n * .15f), 3, Age * .025f + n * 2, .4f);
        }
        else
        {
            Main.EntitySpriteDraw(art, at, null, Color.Lerp(lightColor, Color.White, .5f), Aim.ToRotation() + MathHelper.PiOver4,
                art.Size() / 2, 64f / Math.Max(art.Width, art.Height), SpriteEffects.None);
            if (!Released)
            {
                Vector2 end = p.MountedCenter + Aim * VoyageShot.Clip(p.MountedCenter, Aim, WeaponIndex == 8 ? 190 + charge * 230 : 500 + charge * 850);
                CombatDrawing.Line(Main.spriteBatch, p.MountedCenter - Main.screenPosition, end - Main.screenPosition, tint * .23f, 1);
                CombatDrawing.Circle(Main.spriteBatch, end - Main.screenPosition, 6 + (1 - charge) * 24, tint * .65f, 2);
            }
        }
        Vector2 bar = p.Top - Main.screenPosition - new Vector2(25, 18);
        CombatDrawing.Line(Main.spriteBatch, bar, bar + new Vector2(50, 0), Color.Black * .65f, 6);
        CombatDrawing.Line(Main.spriteBatch, bar, bar + new Vector2(50 * charge, 0), tint, 3);
        return false;
    }
}
