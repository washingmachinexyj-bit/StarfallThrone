using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Mining;

public abstract class MiningTool : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "Tool" + Index;
    public static bool IsDrill(int index) => index is 5 or 10 or 12 or 13;
    public static int ModeCount(int index) => index switch { 7 or 9 => 3, 10 or 11 or 12 => 2, 13 => 4, _ => 1 };
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
        if (IsDrill(Index)) ItemID.Sets.IsDrill[Type] = true;
    }
    public override void SetDefaults()
    {
        if (IsDrill(Index))
        {
            Item.CloneDefaults(ItemID.SolarFlareDrill);
            Item.shoot = ModContent.ProjectileType<MiningDrillProjectile>();
        }
        else
        {
            Item.width = Item.height = Index < 4 ? 32 : 48;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Index < 4 ? 18 - Index * 2 : Math.Max(5, 13 - Index / 2);
            Item.useAnimation = Index < 4 ? 25 - Index * 2 : 18;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;
        }
        Item.pick = MiningCatalog.ToolPower[Index];
        Item.tileBoost = Index switch { 0 => 0, 1 or 2 => 0, 3 => 1, 4 or 5 => 4, 6 or 7 => 5, 9 or 10 => 6, 11 => 8, _ => 10 };
        Item.damage = Index < 4 ? 4 + Index * 2 : 80 + Index * 6;
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = 2;
        Item.rare = Index < 4 ? ItemRarityID.Blue : ItemRarityID.Red;
        Item.value = Index < 4 ? Item.sellPrice(silver: 5 + Index * 5) : Item.sellPrice(gold: 6 + Index);
    }
    public override bool AltFunctionUse(Player player) => ModeCount(Index) > 1;
    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse != 2) return true;
        if (player.whoAmI == Main.myPlayer) player.GetModPlayer<MiningPlayer>().CycleMode(Index);
        return false;
    }
    public override void HoldItem(Player player)
    {
        if (Main.dedServ) return;
        if (Index is 0 or 2 or 3 or 5 or 12 or 13)
            Lighting.AddLight(player.Center, Index < 4 ? new Vector3(.13f, .20f, .14f) : new Vector3(.45f, .35f, .30f));
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        if (ModeCount(Index) <= 1 || Main.gameMenu) return;
        MiningPlayer state = Main.LocalPlayer.GetModPlayer<MiningPlayer>();
        tooltips.Add(new TooltipLine(Mod, "MiningMode", MiningText.Get("CurrentMode", MiningText.Mode(Index, state.Modes[Index]))));
    }
}
public sealed class MiningTool0 : MiningTool { public override int Index => 0; }
public sealed class MiningTool1 : MiningTool { public override int Index => 1; }
public sealed class MiningTool2 : MiningTool { public override int Index => 2; }
public sealed class MiningTool3 : MiningTool { public override int Index => 3; }
public sealed class MiningTool4 : MiningTool { public override int Index => 4; }
public sealed class MiningTool5 : MiningTool { public override int Index => 5; }
public sealed class MiningTool6 : MiningTool { public override int Index => 6; }
public sealed class MiningTool7 : MiningTool { public override int Index => 7; }
public sealed class MiningTool9 : MiningTool { public override int Index => 9; }
public sealed class MiningTool10 : MiningTool { public override int Index => 10; }
public sealed class MiningTool11 : MiningTool { public override int Index => 11; }
public sealed class MiningTool12 : MiningTool { public override int Index => 12; }
public sealed class MiningTool13 : MiningTool { public override int Index => 13; }

public sealed class MiningDrillProjectile : ModProjectile
{
    public override string Texture => MiningCatalog.Root + "Tool5";
    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.SolarFlareDrill);
        AIType = ProjectileID.SolarFlareDrill;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        if (owner.HeldItem.ModItem is not MiningTool) return false;
        Texture2D texture = TextureAssets.Item[owner.HeldItem.type].Value;
        Vector2 direction = Projectile.Center - owner.MountedCenter;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
            direction.ToRotation() + MathHelper.PiOver4, texture.Size() * .5f, 1f, SpriteEffects.None);
        return false;
    }
}

public static class MiningText
{
    public static string Get(string key, params object[] args)
        => Language.GetTextValue("Mods.StarfallThrone.MiningToolsUI." + key, args);
    public static string Mode(int index, int mode) => Get(mode == 0 ? "ModeSingle" : index switch
    {
        10 => "ModeVein", 11 => "ModeSquare", 12 => "ModeWide",
        13 => mode switch { 1 => "ModeWide", 2 => "ModeTall", _ => "ModeSquare" },
        _ => mode == 1 ? "ModeHorizontal" : "ModeVertical"
    });
}

// Applies to the preserved engineering drill without changing its content ID or recipe.
public sealed class MiningToolGlobal : GlobalItem
{
    public override void HoldItem(Item item, Player player)
    {
        if (item.ModItem is MiningTool tool && tool.Index is 6 or 12 or 13)
            player.GetModPlayer<MiningPlayer>().ToolMagnet = true;
    }
}
