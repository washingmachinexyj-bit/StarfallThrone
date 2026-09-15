using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.GameContent.ItemDropRules;
using Terraria.ObjectData;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage;

public abstract class VoyageMaterialBase : ModItem
{
    public abstract int Index { get; }
    public virtual bool IsCore => false;
    public override string Texture => VoyageCatalog.Root + (IsCore ? "Core" : "Material") + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = IsCore ? 3 : 25;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.maxStack = 9999;
        Item.rare = ItemRarityID.Red; Item.material = true;
        Item.value = Item.sellPrice(gold: IsCore ? 8 : 1);
    }
}

public abstract class VoyageSummonBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Summon" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 40; Item.maxStack = 1; Item.consumable = false;
        Item.rare = ItemRarityID.Red; Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 45; Item.UseSound = SoundID.Item44;
        Item.value = Item.sellPrice(gold: 5);
    }
    public override bool CanUseItem(Player player) => Valid(player, Index);
    public static bool Valid(Player p, int index) => index >= 0 && index < 17 && p.active && !p.dead && !p.ghost &&
        VoyageWorld.CanSummon(index) && !VoyageCatalog.AnyEncounter();
    public override bool? UseItem(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI == Main.myPlayer)
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)20); packet.Write((byte)Index); packet.Send();
        }
        else if (Main.netMode == NetmodeID.SinglePlayer) TrySummon(player, Index);
        return true;
    }
    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        int index = reader.ReadByte();
        if (Main.netMode == NetmodeID.Server && sender >= 0 && sender < Main.maxPlayers) TrySummon(Main.player[sender], index);
    }
    public static bool TrySummon(Player p, int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !Valid(p, index) || p.HeldItem.type != VoyageCatalog.Summon(index)) return false;
        NPC.SpawnOnPlayer(p.whoAmI, VoyageCatalog.BossType(index));
        return true;
    }
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe().AddIngredient(VoyageCatalog.Alloy, 8).AddTile(VoyageCatalog.Workbench);
        if (Index == 0) r.AddIngredient(ItemID.Gel, 25).AddIngredient<MoonGodCore>();
        else r.AddIngredient(VoyageCatalog.Material(Index - 1), 10);
        r.Register();
    }
}

public abstract class VoyageBagBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Bag" + Index;
    public override void SetStaticDefaults()
    {
        ItemID.Sets.BossBag[Type] = true; ItemID.Sets.OpenableBag[Type] = true; Item.ResearchUnlockCount = 3;
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 40; Item.maxStack = 9999; Item.expert = true;
        Item.rare = ItemRarityID.Expert;
    }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(VoyageCatalog.Material(Index), 1, Index == 16 ? 72 : 32, Index == 16 ? 96 : 46));
        loot.Add(ItemDropRule.Common(VoyageCatalog.Core(Index), 1, 2, 3));
        int[] weapons = Index == 16 ? new[] { VoyageCatalog.Weapon(32), VoyageCatalog.Weapon(33), VoyageCatalog.Weapon(34), VoyageCatalog.Weapon(35) } :
            new[] { VoyageCatalog.Weapon(Index * 2), VoyageCatalog.Weapon(Index * 2 + 1) };
        loot.Add(ItemDropRule.OneFromOptions(1, weapons));
        loot.Add(ItemDropRule.Common(VoyageCatalog.Expert(Index)));
        loot.Add(ItemDropRule.Common(VoyageCatalog.Mask(Index), 7));
        loot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 20 + Index * 2, 30 + Index * 2));
    }
}

public abstract class VoyageRelicBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Relic" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<VoyageRelicTile>(), Index);
        Item.width = 40; Item.height = 48; Item.rare = ItemRarityID.Master; Item.master = true;
        Item.value = Item.sellPrice(gold: 5);
    }
}
public abstract class VoyageTrophyBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Trophy" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<VoyageTrophyTile>(), Index);
        Item.width = Item.height = 40; Item.rare = ItemRarityID.Cyan; Item.value = Item.sellPrice(gold: 2);
    }
}
public abstract class VoyageMaskBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Mask" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 30; Item.vanity = true; Item.rare = ItemRarityID.Cyan;
        Item.value = Item.sellPrice(gold: 1);
    }
}

public sealed class VoyageAlloy : ModItem
{
    public override string Texture => VoyageCatalog.Root + "Alloy";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = 32; Item.height = 24; Item.maxStack = 9999; Item.rare = ItemRarityID.Red;
        Item.material = true; Item.value = Item.sellPrice(gold: 2);
    }
    public override void AddRecipes() => CreateRecipe(6).AddIngredient<AstralIngot>(2).AddIngredient<StarfallResidue>(6)
        .AddTile(VoyageCatalog.Workbench).Register();
}
public abstract class VoyagePlateBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Plate" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = 36; Item.height = 30; Item.maxStack = 9999; Item.rare = ItemRarityID.Red;
        Item.material = true; Item.value = Item.sellPrice(gold: 3);
    }
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe(10).AddIngredient(VoyageCatalog.Alloy, 10).AddTile(VoyageCatalog.Workbench);
        if (Index < 4) for (int j = 0; j < 4; j++) r.AddIngredient(VoyageCatalog.Material(Index * 4 + j), 6);
        else
        {
            r.AddIngredient(VoyageCatalog.Material(16), 12);
            for (int j = 0; j < 4; j++) r.AddIngredient(VoyageCatalog.Plate(j), 2);
        }
        r.Register();
    }
}
public sealed class VoyageWorkbench : ModItem
{
    public override string Texture => VoyageCatalog.Root + "Workbench";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<VoyageWorkbenchTile>());
        Item.width = 48; Item.height = 32; Item.rare = ItemRarityID.Red; Item.value = Item.sellPrice(gold: 10);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<AstralIngot>(12).AddIngredient<MoonGodCore>(2)
        .AddIngredient(ItemID.Wire, 20).AddTile(TileID.LunarCraftingStation)
        .AddCondition(new Condition(Language.GetText("Mods.StarfallThrone.Voyage.DefeatEndMoon"), () => StarfallWorld.IsDowned(16))).Register();
}

public sealed class VoyageWorkbenchTile : ModTile
{
    public override string Texture => VoyageCatalog.Root + "WorkbenchTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileSolidTop[Type] = true; Main.tileTable[Type] = true;
        Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2); TileObjectData.addTile(Type);
        AddMapEntry(new Color(100, 190, 220), Language.GetText("Mods.StarfallThrone.Tiles.VoyageWorkbenchTile.MapEntry"));
        AdjTiles = new int[] { TileID.WorkBenches }; DustType = DustID.Electric;
    }
}
public sealed class VoyageRelicTile : ModTile
{
    public override string Texture => VoyageCatalog.Root + "RelicTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3); TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = 17; TileObjectData.addTile(Type);
        AddMapEntry(new Color(240, 205, 100), Language.GetText("Mods.StarfallThrone.Tiles.VoyageRelicTile.MapEntry"));
        DustType = DustID.GoldFlame;
    }
}
public sealed class VoyageTrophyTile : ModTile
{
    public override string Texture => VoyageCatalog.Root + "TrophyTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3); TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = 17; TileObjectData.newTile.AnchorBottom = default; TileObjectData.newTile.AnchorWall = true;
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(125, 190, 235), Language.GetText("Mods.StarfallThrone.Tiles.VoyageTrophyTile.MapEntry")); DustType = DustID.BlueCrystalShard;
    }
}
