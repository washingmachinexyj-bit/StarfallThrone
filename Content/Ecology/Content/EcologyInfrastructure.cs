using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.GameContent;

namespace StarfallThrone.Content.Ecology;

public static class EcologyInfrastructure
{
    public static int Rarity(int biome) => biome < 2 ? ItemRarityID.Blue : biome < 4 ? ItemRarityID.Orange
        : biome < 6 ? ItemRarityID.LightRed : biome < 9 ? ItemRarityID.Yellow : ItemRarityID.Red;
    // Transitive lists intentionally omit unrelated optional advanced stations.
    public static int[] StationParents(int index) => index switch
    {
        0 => Array.Empty<int>(), 1 => new[] { 0 }, 2 => new[] { 0 },
        3 or 4 or 5 or 6 => new[] { 0, 2 }, 7 => new[] { 0, 1, 2, 3, 4, 5, 6 },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
    public static int StationBiome(int station) => station switch { 0 => 0, 1 => 2, 2 => 4, 3 => 5, 4 => 7, 5 => 8, _ => 9 };
}
public abstract class EcologyToolBase : ModItem
{
    public abstract int Index { get; }
    public static bool IsDrill(int biome) => biome is 5 or 8;
    public static int Metal(int biome) => biome switch
    {
        0 => ItemID.IronBar, 1 or 2 or 3 => ItemID.GoldBar,
        4 => ItemID.HellstoneBar, 5 => ItemID.HallowedBar,
        6 or 7 or 8 => ItemID.ChlorophyteBar, _ => ItemID.LunarBar
    };
    public override string Texture => EcologyCatalog.Root + "Tool" + Index;
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
        if (IsDrill(Index)) ItemID.Sets.IsDrill[Type] = true;
    }
    public override void SetDefaults()
    {
        if (IsDrill(Index))
        {
            Item.CloneDefaults(ItemID.CobaltDrill);
            Item.shoot = ModContent.ProjectileType<EcologyDrillProjectile>();
            Item.useTime = Index == 5 ? 7 : 5;
        }
        else
        {
            Item.width = Item.height = 36; Item.useStyle = ItemUseStyleID.Swing; Item.autoReuse = true;
            Item.useTime = Math.Max(5, 18 - Index); Item.useAnimation = Math.Max(15, 25 - Index);
            Item.UseSound = SoundID.Item1;
        }
        Item.pick = EcologyCatalog.ToolPower[Index]; Item.tileBoost = Index < 4 ? 0 : Index < 7 ? 1 : 2;
        Item.damage = new[] { 7, 10, 12, 14, 25, 33, 39, 43, 47, 58 }[Index];
        Item.DamageType = DamageClass.Melee; Item.knockBack = 2;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 15 + Index * 35);
    }
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe().AddIngredient(EcologyCatalog.Core(Index), 8);
        if (Index == 0) r.AddRecipeGroup(RecipeGroupID.IronBar, 8);
        else if (Index < 4) r.AddRecipeGroup("StarfallThrone:EcologyGold", 8);
        else r.AddIngredient(Metal(Index), Index == 9 ? 4 : 8);
        if (Index == 2) r.AddIngredient(ItemID.Wood, 10);
        r.AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index)))
            .AddCondition(EcologyCatalog.DownedCondition(Index)).Register();
    }
}
/// <summary>Restricts only this module's tools and only before Golem; other mods' picks are untouched.</summary>
public sealed class EcologyPickHooks : ModSystem
{
    public override void Load() => On_Player.PickTile += Pick;
    public override void Unload() => On_Player.PickTile -= Pick;
    public static bool TempleBlocked(Player player, int type) =>
        !NPC.downedGolemBoss && type == TileID.LihzahrdBrick && player.HeldItem.ModItem is EcologyToolBase;
    private static void Pick(On_Player.orig_PickTile orig, Player player, int x, int y, int power)
    {
        if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && TempleBlocked(player, Main.tile[x, y].TileType)) return;
        orig(player, x, y, power);
    }
}
public sealed class EcologyDrillProjectile : ModProjectile
{
    public override string Texture => EcologyCatalog.Root + "Tool5";
    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.CobaltDrill); AIType = ProjectileID.CobaltDrill;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return false;
        Player p = Main.player[Projectile.owner];
        if (p.HeldItem.ModItem is not EcologyToolBase) return false;
        Texture2D texture = TextureAssets.Item[p.HeldItem.type].Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
            (Projectile.Center - p.MountedCenter).ToRotation() + MathHelper.PiOver4, texture.Size() / 2, 1f, SpriteEffects.None);
        return false;
    }
}
public abstract class EcologyStationBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Station" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(EcologyCatalog.StationTile(Index)); Item.width = Item.height = 40;
        Item.maxStack = 99; Item.rare = EcologyInfrastructure.Rarity(EcologyInfrastructure.StationBiome(Index));
        Item.value = Item.sellPrice(silver: 5 + Index * 10);
    }
    public override void AddRecipes()
    {
        if (Index == 0) { BaseDish(CreateRecipe()).AddTile(TileID.WorkBenches).AddCondition(EcologyCatalog.BossCondition(0)).Register(); return; }
        // Both upgrade-in-place and raw-base recipes: players need never sacrifice their only bench.
        for (int alternative = 0; alternative < 2; alternative++)
        {
            Recipe r = CreateRecipe();
            int previous = Index is 1 or 2 ? 0 : Index == 7 ? 6 : 2;
            if (alternative == 0) r.AddIngredient(EcologyCatalog.StationItem(previous));
            else if (previous == 0) BaseDish(r, Index == 2 ? 20 : 0);
            else if (previous == 2) BaseTuner(r);
            else { BaseTuner(r); r.AddIngredient(ItemID.LunarBar, 6).AddRecipeGroup("StarfallThrone:EcologyFragment", 20); }
            switch (Index)
            {
                case 1:
                    r.AddIngredient(EcologyCatalog.Core(2), 6).AddIngredient(EcologyCatalog.Ore(2), 20)
                        .AddIngredient(EcologyCatalog.Material(2, 2), 12).AddCondition(EcologyCatalog.DownedCondition(2)); break;
                case 2:
                    if (alternative == 0) r.AddIngredient(ItemID.Glass, 20);
                    r.AddIngredient(ItemID.HellstoneBar, 8)
                        .AddRecipeGroup("StarfallThrone:EcologyCatalyst", 4).AddCondition(EcologyCatalog.BossCondition(4)); break;
                case 3:
                    r.AddIngredient(EcologyCatalog.Core(5), 6).AddIngredient(EcologyCatalog.Ore(5), 24)
                        .AddIngredient(EcologyCatalog.Material(5, 2), 12).AddCondition(EcologyCatalog.DownedCondition(5)); break;
                case 4:
                    r.AddIngredient(EcologyCatalog.Core(7), 6).AddIngredient(EcologyCatalog.Ore(7), 24)
                        .AddIngredient(EcologyCatalog.Material(7, 1), 20).AddCondition(EcologyCatalog.DownedCondition(7)); break;
                case 5:
                    r.AddIngredient(EcologyCatalog.Core(8), 6).AddIngredient(EcologyCatalog.Ore(8), 24)
                        .AddIngredient(EcologyCatalog.Material(8, 1), 12).AddCondition(EcologyCatalog.DownedCondition(8)); break;
                case 6:
                    r.AddIngredient(ItemID.LunarBar, 6).AddRecipeGroup("StarfallThrone:EcologyFragment", 20)
                        .AddCondition(EcologyCatalog.BossCondition(9)); break;
                case 7:
                    r.AddIngredient(EcologyCatalog.Core(9), 8).AddIngredient(EcologyCatalog.Ore(9), 32)
                        .AddIngredient(EcologyCatalog.Material(9, 3), 8).AddCondition(EcologyCatalog.DownedCondition(9)); break;
            }
            r.AddTile(TileID.WorkBenches).Register();
        }
    }
    private static Recipe BaseDish(Recipe r, int extraGlass = 0) => r.AddIngredient(ItemID.Wood, 20).AddIngredient(ItemID.Glass, 10 + extraGlass).AddRecipeGroup(RecipeGroupID.IronBar, 3);
    private static Recipe BaseTuner(Recipe r) => BaseDish(r, 20)
        .AddIngredient(ItemID.HellstoneBar, 8).AddRecipeGroup("StarfallThrone:EcologyCatalyst", 4);
}
public abstract class EcologyStationTileBase : ModTile
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "StationTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true; Main.tileTable[Type] = true;
        Main.tileLavaDeath[Type] = false; Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3); TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type); AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);
        DustType = DustID.Stone; HitSound = SoundID.Tink;
        AddMapEntry(EcologyCatalog.Colors[EcologyInfrastructure.StationBiome(Index)],
            Language.GetText("Mods.StarfallThrone.Items.EcologyStation" + Index + ".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(EcologyCatalog.StationItem(Index)); }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2; Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = EcologyCatalog.StationItem(Index);
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 c = EcologyCatalog.Colors[EcologyInfrastructure.StationBiome(Index)].ToVector3() * .1f;
        r = c.X; g = c.Y; b = c.Z;
    }
}
public sealed class EcologyStationAdjacency : ModSystem
{
    public override void PostSetupContent()
    {
        for (int i = 0; i < 8; i++)
            TileLoader.GetTile(EcologyCatalog.StationTile(i)).AdjTiles =
                EcologyInfrastructure.StationParents(i).Select(EcologyCatalog.StationTile).Prepend(TileID.WorkBenches).ToArray();
    }
}
