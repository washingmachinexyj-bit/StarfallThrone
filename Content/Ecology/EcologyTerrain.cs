using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Ecology;

public abstract class EcologyTerrain : ModTile
{
    public abstract int Index{get;}
    public abstract int Kind{get;}
    public static int BiomeOf(int type)=>TileLoader.GetTile(type) switch{EcologyTerrain t=>t.Index,EcologyOreTile o=>o.Index,_=>-1};
    public static readonly int[] OriginalTiles={TileID.Stone,TileID.Dirt,TileID.Grass,TileID.Sand,TileID.IceBlock};
    public override string Texture=>EcologyCatalog.Root+"Terrain"+Index+"_"+Kind;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type]=true;Main.tileBlockLight[Type]=true;
        // Intentionally NOT part of Conversion.Stone/Grass/Sand/Ice: no native evil/hallow spreading.
        TileID.Sets.CanBeClearedDuringGeneration[Type]=false;
        AddMapEntry(EcologyCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items.EcologyBlock"+Index+"_"+Kind+".DisplayName"));
        RegisterItemDrop(EcologyCatalog.Item("EcologyBlock"+Index+"_"+Kind));DustType=DustID.Stone;HitSound=SoundID.Dig;
    }
    public override bool TileFrame(int i,int j,ref bool resetFrame,ref bool noBreak){Main.tile[i,j].TileFrameX=Main.tile[i,j].TileFrameY=0;return false;}
    public override void RandomUpdate(int i,int j)
    {
        if(Kind!=2||!EcologyWorld.Editable||!Main.rand.NextBool(30)||!WorldGen.InWorld(i,j,10))return;
        var region=EcologyWorld.RegionAt(new Point(i,j));
        if(region==null||!region.Ecology||region.Biome!=Index||region.TileCount<300||!EcologyCatalog.VanillaUnlocked(Index)||!region.Bounds.Contains(i,j-1))return;
        Tile above=Main.tile[i,j-1];if(above.HasTile||above.LiquidAmount>0||above.WallType!=0||above.RedWire||above.BlueWire||above.GreenWire||above.YellowWire)return;
        WorldGen.PlaceTile(i,j-1,EcologyCatalog.Tile("EcologyPlantTile"+Index),mute:true);
        if(Main.netMode==NetmodeID.Server)NetMessage.SendTileSquare(-1,i,j-1,1);
    }
}
public abstract class EcologyBlock : ModItem
{
    public abstract int Index{get;} public abstract int Kind{get;}
    public override string Texture=>EcologyCatalog.Root+"Terrain"+Index+"_"+Kind;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=100;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(EcologyCatalog.TerrainTile(Index,Kind));Item.width=Item.height=16;Item.maxStack=9999;}
}
public abstract class EcologyOre : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Ore"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=100;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(EcologyCatalog.OreTile(Index));Item.width=Item.height=24;Item.maxStack=9999;Item.material=true;Item.value=20+Index*20;}
}
public abstract class EcologyOreTile : ModTile
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"OreTile"+Index;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type]=Main.tileBlockLight[Type]=Main.tileSpelunker[Type]=true;
        TileID.Sets.Ore[Type]=true;TileID.Sets.CanBeClearedDuringGeneration[Type]=false;
        Main.tileOreFinderPriority[Type]=(short)(330+Index*12);MinPick=EcologyCatalog.PickRequirements[Index];MineResist=1.2f+Index*.14f;
        RegisterItemDrop(EcologyCatalog.Ore(Index));DustType=DustID.Stone;HitSound=SoundID.Tink;
        AddMapEntry(EcologyCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items.EcologyOre"+Index+".DisplayName"));
    }
    public override bool CanKillTile(int i,int j,ref bool blockDamaged)=>EcologyWorld.Downed[Index];
    public override bool CanExplode(int i,int j)=>false;
    public override bool CanReplace(int i,int j,int tileTypeBeingPlaced)=>false;
    public override bool TileFrame(int i,int j,ref bool resetFrame,ref bool noBreak){Main.tile[i,j].TileFrameX=Main.tile[i,j].TileFrameY=0;return false;}
}
public abstract class EcologyPlant : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Plant"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=24;Item.maxStack=9999;Item.material=true;Item.value=40;}
    public override void AddRecipes()
    {
        Recipe.Create(EcologyCatalog.Material(Index)).AddIngredient(Type,3).AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index))).AddCondition(EcologyCatalog.BossCondition(Index)).Register();
        Recipe.Create(ItemID.RegenerationPotion).AddIngredient(Type,2).AddIngredient(ItemID.BottledWater).AddTile(TileID.Bottles).Register();
    }
}
public abstract class EcologySeed : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Seed"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(EcologyCatalog.Tile("EcologyPlantTile"+Index));Item.width=Item.height=20;Item.maxStack=9999;}
    public override void AddRecipes()=>CreateRecipe(3).AddIngredient(EcologyCatalog.Material(Index),1).AddTile(EcologyCatalog.StationTile(0)).AddCondition(EcologyCatalog.BossCondition(Index)).Register();
}
public abstract class EcologyPlantTile : ModTile
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"PlantTile"+Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileCut[Type]=true;Main.tileNoFail[Type]=true;Main.tileLavaDeath[Type]=true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorValidTiles=new[]{EcologyCatalog.TerrainTile(Index,2)};
        TileObjectData.addTile(Type);
        AddMapEntry(EcologyCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items.EcologyPlant"+Index+".DisplayName"));DustType=DustID.Grass;
    }
    public override bool CanPlace(int i,int j)=>WorldGen.InWorld(i,j,10)&&Main.tile[i,j+1].HasTile&&Main.tile[i,j+1].TileType==EcologyCatalog.TerrainTile(Index,2);
    public override bool TileFrame(int i,int j,ref bool resetFrame,ref bool noBreak)
    {if(!CanPlace(i,j))WorldGen.KillTile(i,j);else Main.tile[i,j].TileFrameX=Main.tile[i,j].TileFrameY=0;return false;}
    public override IEnumerable<Item> GetItemDrops(int i,int j)
    {
        var r=EcologyWorld.RegionAt(new Point(i,j));
        if(r!=null&&r.Ecology&&r.Biome==Index&&r.TileCount>=300&&EcologyCatalog.VanillaUnlocked(Index))
        {yield return new Item(EcologyCatalog.Item("EcologyPlant"+Index));yield return new Item(EcologyCatalog.Item("EcologySeed"+Index));}
    }
}
public abstract class EcologyBiome : ModBiome
{
    public abstract int Index{get;}
    public override string BestiaryIcon=>EcologyCatalog.Root+"Core"+Index;
    public override string BackgroundPath=>EcologyCatalog.Root+"Background"+Index;
    public override string MapBackground=>BackgroundPath;
    public override Color? BackgroundColor=>EcologyCatalog.Colors[Index];
    public override int Music=>Index switch{0=>MusicID.Space,1=>MusicID.Underground,2=>MusicID.Jungle,3=>MusicID.Graveyard,4=>MusicID.TheHallow,5=>MusicID.UndergroundHallow,6=>MusicID.Jungle,7=>MusicID.Desert,8=>MusicID.OceanNight,_=>MusicID.Space};
    public override SceneEffectPriority Priority=>SceneEffectPriority.BiomeHigh;
    public override bool IsBiomeActive(Player player)=>EcologyWorld.ActiveBiome(player,false)==Index;
    public override void OnInBiome(Player player)
    {
        if(Main.dedServ||player.whoAmI!=Main.myPlayer||!Main.rand.NextBool(8))return;
        var dust=Dust.NewDustPerfect(player.Center+new Vector2(Main.rand.Next(-700,701),Main.rand.Next(-400,301)),DustID.TintableDustLighted,new Vector2(0,-.25f),170,EcologyCatalog.Colors[Index],.6f);dust.noGravity=true;
    }
}
