using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Tiles;

namespace StarfallThrone.Content.Items;

public abstract class MiniSummon : ModItem
{
    public abstract int Index { get; }
    public override string Texture => "StarfallThrone/Content/Assets/Minis/Summon" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = 28; Item.height = 28; Item.maxStack = 1;
        Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = Item.useAnimation = 30;
        Item.rare = ItemRarityID.Blue; Item.consumable = false; Item.value = 0;
        Item.UseSound = SoundID.Item44;
    }
    public override bool CanUseItem(Player player) => MiniBossData.Unlocked(Index) && MiniBossData.Habitat(player, Index) && !MiniBossData.AnyActive();
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI != Main.myPlayer) return true;
        if (Main.netMode == NetmodeID.SinglePlayer) TrySummon(player, Index);
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)1); packet.Write((byte)Index); packet.Send();
        }
        return true;
    }
    public static void TrySummon(Player player, int index)
    {
        // Recheck server-side; clients cannot choose an arbitrary NPC or spawn point.
        if (index < 0 || index >= MiniBossData.Count || !player.active || player.dead
            || player.HeldItem.type != MiniBossData.SummonType(index)
            || !MiniBossData.Unlocked(index) || !MiniBossData.Habitat(player, index) || MiniBossData.AnyActive()) return;
        Vector2 at = player.Center + new Vector2(player.direction * 200, -100);
        if (index is not (2 or 3))
        {
            bool found = MiniBossNPC.FindGround(player.Bottom + new Vector2(player.direction * 200, 0), 56, 64, out at);
            if (!found) found = MiniBossNPC.FindGround(player.Bottom + new Vector2(-player.direction * 160, 0), 56, 64, out at);
            if (!found) return;
        }
        else if (Collision.SolidCollision(at - new Vector2(28, 32), 56, 64)) return;
        int id = NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)at.X, (int)at.Y, MiniBossData.NPCType(index), Target: player.whoAmI);
        if (id < Main.maxNPCs)
        {
            Main.npc[id].netUpdate = true;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, id);
        }
    }
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe();
        switch (Index)
        {
            case 0: r.AddIngredient(ItemID.DirtBlock, 10).AddRecipeGroup(RecipeGroupID.Wood, 5).AddIngredient(ItemID.Gel, 2); break;
            case 1: r.AddIngredient(ItemID.ClayBlock, 8).AddIngredient(ItemID.Gel, 3); break;
            case 2: r.AddRecipeGroup(RecipeGroupID.Wood, 8).AddIngredient(ItemID.Rope, 10).AddIngredient(ItemID.Gel, 3); break;
            case 3: r.AddIngredient(ItemID.Torch, 5).AddIngredient(ItemID.Gel, 5).AddRecipeGroup(RecipeGroupID.Wood, 5); break;
            case 4: r.AddIngredient(ItemID.StoneBlock, 20).AddIngredient(ItemID.DirtBlock, 10).AddIngredient(ItemID.Gel, 5); break;
            case 5: r.AddRecipeGroup(RecipeGroupID.Wood, 20).AddIngredient(ItemID.Acorn, 3).AddIngredient(ItemID.Gel, 8); break;
            case 6: r.AddRecipeGroup(RecipeGroupID.Wood, 3).AddIngredient(ItemID.Gel, 2); break;
            case 7: r.AddRecipeGroup(RecipeGroupID.Wood, 5).AddIngredient(ItemID.Acorn, 1).AddIngredient(ItemID.Gel, 3); break;
        }
        int prerequisite = MiniBossData.Prerequisite(Index);
        if (prerequisite >= 0) r.AddIngredient(MiniBossData.MaterialType(prerequisite), 3);
        r.AddTile(TileID.WorkBenches).Register();
    }
}
public sealed class MiniSummon0 : MiniSummon { public override int Index => 0; }
public sealed class MiniSummon1 : MiniSummon { public override int Index => 1; }
public sealed class MiniSummon2 : MiniSummon { public override int Index => 2; }
public sealed class MiniSummon3 : MiniSummon { public override int Index => 3; }
public sealed class MiniSummon4 : MiniSummon { public override int Index => 4; }
public sealed class MiniSummon5 : MiniSummon { public override int Index => 5; }
public sealed class MiniSummon6 : MiniSummon { public override int Index => 6; }
public sealed class MiniSummon7 : MiniSummon { public override int Index => 7; }

public abstract class MiniMaterial : ModItem
{
    public abstract int Index { get; }
    public override string Texture => "StarfallThrone/Content/Assets/Minis/Material" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 20;
    public override void SetDefaults() { Item.width = Item.height = 20; Item.maxStack = 9999; Item.rare = ItemRarityID.White; Item.value = Item.sellPrice(copper: 10); }
}
public sealed class MiniMaterial0 : MiniMaterial { public override int Index => 0; }
public sealed class MiniMaterial1 : MiniMaterial { public override int Index => 1; }
public sealed class MiniMaterial2 : MiniMaterial { public override int Index => 2; }
public sealed class MiniMaterial3 : MiniMaterial { public override int Index => 3; }
public sealed class MiniMaterial4 : MiniMaterial { public override int Index => 4; }
public sealed class MiniMaterial5 : MiniMaterial { public override int Index => 5; }
public sealed class MiniMaterial6 : MiniMaterial { public override int Index => 6; }
public sealed class MiniMaterial7 : MiniMaterial { public override int Index => 7; }

public abstract class MiniAccessory : ModItem
{
    public abstract int Index { get; }
    public abstract int MaterialIndex { get; }
    public override string Texture => "StarfallThrone/Content/Assets/Minis/Accessory" + Index;
    public override void SetDefaults() { Item.width = Item.height = 26; Item.accessory = true; Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 3); }
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(MaterialIndex), 8).AddTile(TileID.WorkBenches).Register();
}
public sealed class MossInsoles : MiniAccessory
{
    public override int Index => 0; public override int MaterialIndex => 0;
    public override void UpdateAccessory(Player player, bool hideVisual) => player.moveSpeed += 0.03f;
}
public sealed class PotBuckle : MiniAccessory
{
    public override int Index => 1; public override int MaterialIndex => 1;
    public override void UpdateAccessory(Player player, bool hideVisual) => player.statDefense++;
}
public sealed class EmberPendant : MiniAccessory
{
    public override int Index => 2; public override int MaterialIndex => 3;
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.moveSpeed += 0.03f;
        if (!hideVisual) Lighting.AddLight(player.Center, new Vector3(0.28f, 0.18f, 0.05f));
    }
}
public sealed class DiggingClaws : MiniAccessory
{
    public override int Index => 3; public override int MaterialIndex => 4;
    public override void UpdateAccessory(Player player, bool hideVisual) => player.pickSpeed -= 0.05f;
}
public sealed class SproutBadge : MiniAccessory
{
    public override int Index => 4; public override int MaterialIndex => 5;
    public override void UpdateAccessory(Player player, bool hideVisual) { player.statLifeMax2 += 10; player.statDefense++; }
}
public sealed class DewBrooch : MiniAccessory
{
    public override int Index => 5; public override int MaterialIndex => 6;
    public override string Texture => "StarfallThrone/Content/Assets/Minis/DewBrooch";
    public override void UpdateAccessory(Player player, bool hideVisual) => player.statLifeMax2 += 5;
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(6), 6)
        .AddRecipeGroup(RecipeGroupID.Wood, 3).AddTile(TileID.WorkBenches).Register();
}
public sealed class FluffAnklet : MiniAccessory
{
    public override int Index => 6; public override int MaterialIndex => 7;
    public override string Texture => "StarfallThrone/Content/Assets/Minis/FluffAnklet";
    public override void UpdateAccessory(Player player, bool hideVisual) => player.moveSpeed += 0.02f;
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(7), 6)
        .AddRecipeGroup(RecipeGroupID.Wood, 3).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Back)]
public sealed class UmbrellaVanity : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Minis/UmbrellaVanity";
    public override void SetDefaults() { Item.width = 24; Item.height = 34; Item.accessory = true; Item.vanity = true; Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 3); }
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(2), 8).AddTile(TileID.WorkBenches).Register();
}

public abstract class MiniWeapon : ModItem
{
    protected abstract int MaterialIndex { get; }
    public override string Texture => "StarfallThrone/Content/Assets/Minis/" + Name;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 4);
        Item.useStyle = ItemUseStyleID.Swing; Item.useTime = Item.useAnimation = 26;
        Item.DamageType = DamageClass.Melee; Item.damage = 8; Item.knockBack = 3f; Item.UseSound = SoundID.Item1;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(MaterialIndex), 8)
        .AddRecipeGroup(RecipeGroupID.Wood, 8).AddTile(TileID.WorkBenches).Register();
}
public sealed class ClawFork : MiniWeapon
{
    protected override int MaterialIndex => 1;
    public override void SetDefaults() { base.SetDefaults(); Item.damage = 7; Item.useTime = Item.useAnimation = 21; Item.knockBack = 3.5f; }
}
public sealed class FlintHammer : MiniWeapon
{
    protected override int MaterialIndex => 4;
    public override void SetDefaults() { base.SetDefaults(); Item.damage = 11; Item.knockBack = 5f; Item.useTime = Item.useAnimation = 29; }
}
public sealed class ThornBow : MiniWeapon
{
    protected override int MaterialIndex => 5;
    public override void SetDefaults()
    {
        base.SetDefaults(); Item.damage = 9; Item.DamageType = DamageClass.Ranged; Item.useStyle = ItemUseStyleID.Shoot;
        Item.noMelee = true; Item.shoot = ProjectileID.WoodenArrowFriendly; Item.useAmmo = AmmoID.Arrow; Item.shootSpeed = 6.1f; Item.UseSound = SoundID.Item5;
    }
}
public abstract class MiniWand : MiniWeapon
{
    protected abstract int Mode { get; }
    public override void SetDefaults()
    {
        base.SetDefaults(); Item.DamageType = DamageClass.Magic; Item.damage = Mode == 1 ? 9 : 8;
        Item.mana = 4; Item.noMelee = true; Item.useStyle = ItemUseStyleID.Shoot;
        Item.shoot = ModContent.ProjectileType<MiniMagicProjectile>(); Item.shootSpeed = Mode == 1 ? 4f : 4.5f;
        Item.UseSound = SoundID.Item20; Item.knockBack = Mode == 1 ? 2f : 5f;
    }
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, Mode); return false;
    }
}
public sealed class FoldedUmbrella : MiniWand { protected override int MaterialIndex => 2; protected override int Mode => 0; }
public sealed class WickWand : MiniWand { protected override int MaterialIndex => 3; protected override int Mode => 1; }
public sealed class DandelionWand : MiniWand
{
    protected override int MaterialIndex => 7; protected override int Mode => 2;
    public override void SetDefaults()
    {
        base.SetDefaults(); Item.damage = 6; Item.mana = 3; Item.shootSpeed = 3.6f;
        Item.useTime = Item.useAnimation = 30; Item.knockBack = 1.5f;
    }
}

public abstract class MiniTrophy : ModItem
{
    public abstract int Index { get; }
    public override string Texture => "StarfallThrone/Content/Assets/Minis/Trophy" + Index;
    public override void SetDefaults() { Item.DefaultToPlaceableTile(ModContent.TileType<MiniDisplayTile>(), Index); Item.width = Item.height = 28; Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 1); }
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(Index), 4).AddRecipeGroup(RecipeGroupID.Wood, 5).AddTile(TileID.WorkBenches).Register();
}
public sealed class MiniTrophy0 : MiniTrophy { public override int Index => 0; }
public sealed class MiniTrophy1 : MiniTrophy { public override int Index => 1; }
public sealed class MiniTrophy2 : MiniTrophy { public override int Index => 2; }
public sealed class MiniTrophy3 : MiniTrophy { public override int Index => 3; }
public sealed class MiniTrophy4 : MiniTrophy { public override int Index => 4; }
public sealed class MiniTrophy5 : MiniTrophy { public override int Index => 5; }
public sealed class MiniTrophy6 : MiniTrophy { public override int Index => 6; }
public sealed class MiniTrophy7 : MiniTrophy { public override int Index => 7; }
public sealed class MothLantern : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Minis/MothLantern";
    public override void SetDefaults() { Item.DefaultToPlaceableTile(ModContent.TileType<MiniLanternTile>()); Item.width = 18; Item.height = 30; Item.value = Item.sellPrice(silver: 1); }
    public override void AddRecipes() => CreateRecipe().AddIngredient(MiniBossData.MaterialType(3), 4).AddRecipeGroup(RecipeGroupID.Wood, 5).AddTile(TileID.WorkBenches).Register();
}
