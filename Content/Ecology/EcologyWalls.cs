using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology;
public abstract class EcologyWall : ModWall
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Wall"+Index;
    public override void SetStaticDefaults(){Main.wallHouse[Type]=true;DustType=DustID.Stone;AddMapEntry(EcologyCatalog.Colors[Index]*.5f,Language.GetText("Mods.StarfallThrone.Items.EcologyWallItem"+Index+".DisplayName"));}
    public override bool WallFrame(int i,int j,bool randomizeFrame,ref int style,ref int frameNumber)
    {Tile t=Main.tile[i,j];t.WallFrameX=t.WallFrameY=0;t.WallFrameNumber=0;return false;}
}
public abstract class EcologyWallItem : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Terrain"+Index+"_0";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=400;
    public override void SetDefaults(){Item.DefaultToPlaceableWall(ModContent.Find<ModWall>(Mod.Name,"EcologyWall"+Index).Type);Item.width=Item.height=16;Item.maxStack=9999;}
    public override void AddRecipes()
    {CreateRecipe(4).AddIngredient(EcologyCatalog.Item("EcologyBlock"+Index+"_0")).AddTile(TileID.WorkBenches).Register();Recipe.Create(EcologyCatalog.Item("EcologyBlock"+Index+"_0")).AddIngredient(Type,4).AddTile(TileID.WorkBenches).Register();}
}
