using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.Enums;

namespace StarfallThrone.Content.Pantheon;

public static class PantheonRecipes
{
    public static readonly int[] StationUnlocks={1,4,7,10,13,16};
    public static readonly int[] EssenceSources={0,2,5,8,11,14};
    public static readonly int[] ThemeIngredients={ItemID.Gel,ItemID.Lens,ItemID.Obsidian,ItemID.FallenStar,ItemID.BottledHoney,ItemID.Bone,ItemID.Obsidian,ItemID.CrystalShard,ItemID.FallenStar,ItemID.Chain,ItemID.HellstoneBar,ItemID.JungleSpores,ItemID.StoneBlock,ItemID.Coral,ItemID.CrystalShard,ItemID.Book,ItemID.FallenStar};
    public static readonly int[] ThemeAmounts={20,12,30,12,8,30,30,20,20,20,12,20,80,20,30,5,30};
    public static bool StationUnlocked(int i)=>i>=0&&i<6&&PantheonWorld.IsDowned(StationUnlocks[i]);
}
public abstract class PantheonEssenceBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Essence"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=32;Item.maxStack=9999;Item.material=true;Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(gold:2);}
    public override void AddRecipes()
    {
        // Every boss in a grade can supply its essence; no future drop is mandatory.
        for(int i=0;i<17;i++)if(PantheonCatalog.Tier(i)==Index)
        {
            CreateRecipe(3).AddIngredient(PantheonCatalog.Material(i),6).AddTile(PantheonCatalog.CraftStation(Index)).Register();
            CreateRecipe(6).AddIngredient(PantheonCatalog.Core(i),1).AddTile(PantheonCatalog.CraftStation(Index)).Register();
        }
    }
}
public abstract class PantheonStationBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Station"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(PantheonCatalog.StationTile(Index));Item.width=40;Item.height=48;Item.maxStack=99;Item.rare=ItemRarityID.Red;}
    public override void AddRecipes()
    {
        int boss=PantheonRecipes.StationUnlocks[Index];
        Recipe r=CreateRecipe().AddIngredient(PantheonCatalog.Material(boss),16).AddIngredient(PantheonCatalog.Core(boss),2).AddIngredient(ItemID.StoneBlock,60)
            .AddTile(PantheonCatalog.CraftStation(Index)).AddCondition(new Condition("Mods.StarfallThrone.Pantheon.StationCondition"+Index,()=>PantheonRecipes.StationUnlocked(Index)));
        // Upgrade consumes the predecessor item but must be crafted beside a placed copy: avoid that trap.
        // Stations are additive recipes. Higher stations inherit only earlier Pantheon stations.
        r.Register();
    }
}
public abstract class PantheonStationTileBase : ModTile
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"StationTile"+Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);TileObjectData.newTile.Width=5;TileObjectData.newTile.Height=4;
        TileObjectData.newTile.CoordinateHeights=new[]{16,16,16,16};TileObjectData.newTile.Origin=new Point16(2,3);
        TileObjectData.newTile.AnchorBottom=new AnchorData(AnchorType.SolidTile|AnchorType.SolidWithTop|AnchorType.SolidSide,5,0);
        TileObjectData.newTile.LavaDeath=false;TileObjectData.addTile(Type);
        // No unconditional vanilla tile adjacency: merely possessing a late station cannot unlock unrelated crafts.
        AdjTiles=Enumerable.Range(0,Index).Select(PantheonCatalog.StationTile).ToArray();
        DustType=DustID.GoldFlame;HitSound=SoundID.Tink;
        AddMapEntry(PantheonCatalog.Colors[PantheonRecipes.StationUnlocks[Index]],Language.GetText("Mods.StarfallThrone.Items.PantheonStation"+Index+".DisplayName"));
    }
    public override System.Collections.Generic.IEnumerable<Item> GetItemDrops(int i,int j){yield return new Item(PantheonCatalog.Station(Index));}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=PantheonCatalog.Station(Index);}
}
