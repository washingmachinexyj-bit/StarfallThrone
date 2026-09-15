using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Ecology;

public static class EcologyValidation
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Ecology validation: "+message);}
    internal static bool Headless=>Main.dedServ&&Terraria.Program.LaunchParameters.ContainsKey("-testservermodloading")&&Terraria.Program.LaunchParameters.ContainsKey("-starfall-ecology-headless");
    public static void Data()
    {
        var mod=ModContent.GetInstance<global::StarfallThrone.StarfallThrone>();
        Check(mod.GetContent<ModBiome>().Count(b=>b is EcologyBiome)==10,"ten biomes registered");
        Check(mod.GetContent<ModTile>().Count(t=>t is EcologyTerrain)==50,"fifty reversible terrain types");
        Check(mod.GetContent<ModTile>().Count(t=>t is EcologyOreTile)==10,"ten ore tiles");
        for(int i=0;i<10;i++)
        {
            Check(TileLoader.GetTile(EcologyCatalog.OreTile(i)).MinPick==EcologyCatalog.PickRequirements[i],"ore pick requirement "+i);
            Check(ItemLoader.GetItem(EcologyCatalog.Item("EcologyPowder"+i)) is EcologyPowder {Solution:false},"powder "+i);
            Check(ItemLoader.GetItem(EcologyCatalog.Item("EcologySolution"+i)) is EcologyPowder {Solution:true},"solution "+i);
            Check(EcologyCatalog.ToolPower[i]>=EcologyCatalog.PickRequirements[i],"first-tool power "+i);
        }
        mod.Logger.Info("ECOLOGY_WORLD_DATA_PASS: 10 biome/ore branches, 50 reversible block types, powders/solutions and pick thresholds registered.");
    }
    private readonly record struct Cell(TileTypeData Type,WallTypeData Wall,TileWallWireStateData State,LiquidData Liquid,TileWallBrightnessInvisibilityData Light)
    {
        public Cell(Tile t):this(t.Get<TileTypeData>(),t.Get<WallTypeData>(),t.Get<TileWallWireStateData>(),t.Get<LiquidData>(),t.Get<TileWallBrightnessInvisibilityData>()){}
        public void Restore(Tile t){t.Get<TileTypeData>()=Type;t.Get<WallTypeData>()=Wall;t.Get<TileWallWireStateData>()=State;t.Get<LiquidData>()=Liquid;t.Get<TileWallBrightnessInvisibilityData>()=Light;}
    }
    public static void Run()
    {
        if(!Headless&&(!Main.gameMenu||!Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke")))throw new InvalidOperationException("Ecology smoke requires isolated menu or ecology headless mode");
        var world=ModContent.GetInstance<EcologyWorld>();var original=new TagCompound();world.SaveWorldData(original);
        var mining=ModContent.GetInstance<global::StarfallThrone.Content.Mining.MiningWorld>();var savedMining=new TagCompound();mining.SaveWorldData(savedMining);
        var flags=typeof(NPC).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(bool)&&f.Name.StartsWith("downed")).ToDictionary(f=>f,f=>f.GetValue(null));
        bool hard=Main.hardMode,menu=Main.gameMenu,day=Main.dayTime;int net=Main.netMode,my=Main.myPlayer;double surface=Main.worldSurface;
        Player[] players=(Player[])Main.player.Clone();NPC[] npcs=(NPC[])Main.npc.Clone();Item[] items=(Item[])Main.item.Clone();
        const int left=420,top=350,width=230,height=170;var cells=new Cell[width*height];
        for(int x=0;x<width;x++)for(int y=0;y<height;y++)cells[x*height+y]=new Cell(Main.tile[left+x,top+y]);
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.worldSurface=360;world.ClearWorld();
            foreach(var f in flags.Keys)f.SetValue(null,false);Main.hardMode=false;
            for(int i=0;i<10;i++)Check(!EcologyCatalog.VanillaUnlocked(i),"fresh world lock "+i);
            NPC.downedFishron=true;Check(!EcologyCatalog.VanillaUnlocked(8),"Fishron alone locked");NPC.downedFishron=false;NPC.downedEmpressOfLight=true;Check(!EcologyCatalog.VanillaUnlocked(8),"Empress alone locked");NPC.downedFishron=true;Check(EcologyCatalog.VanillaUnlocked(8),"both unlock without Golem");
            foreach(var f in flags.Keys)f.SetValue(null,true);Main.hardMode=true;
            for(int x=left;x<left+width;x++)for(int y=top;y<top+height;y++)
            {Tile t=Main.tile[x,y];t.Get<TileTypeData>()=default;t.Get<WallTypeData>()=default;t.Get<TileWallWireStateData>()=default;t.Get<LiquidData>()=default;t.Get<TileWallBrightnessInvisibilityData>()=default;t.HasTile=true;t.TileType=TileID.Stone;}
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i,active=false};
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item();
            var p=new Player{whoAmI=0,active=true,position=new Vector2(500*16,400*16)};Main.player[0]=p;p.GetModPlayer<EcologyPlayer>().Identity=Guid.NewGuid().ToString("N");
            p.ResetEffects();
            foreach(int gun in new[]{ModContent.ItemType<EcologySprayCan>(),(int)ItemID.Clentaminator,(int)ItemID.Clentaminator2})
            {
                p.inventory[54]=new Item(EcologyCatalog.Item("EcologySolution0"),99);
                Check(p.PickAmmo(new Item(gun),out int shot,out float speed,out int damage,out float knockback,out int ammo,true)&&shot==ModContent.ProjectileType<EcologySolutionJet>()&&ammo==EcologyCatalog.Item("EcologySolution0"),"native solution offset/applicator compatibility "+gun);
            }
            p.inventory[54]=new Item();
            p.inventory[1]=new Item(EcologyCatalog.Item("EcologySolution0"),99);p.inventory[54]=new Item(ItemID.GreenSolution,99);
            var spray=new Item(ModContent.ItemType<EcologySprayCan>());
            Check(p.ChooseAmmo(spray)?.type==EcologyCatalog.Item("EcologySolution0"),"sprayer rejects vanilla ammo even when native ammo slots have priority");
            p.inventory[1]=new Item();Check(p.ChooseAmmo(spray)==null,"sprayer cannot fire vanilla world conversion");p.inventory[54]=new Item();
            Point anchor=new(500,410);for(int x=0;x<2;x++)for(int y=0;y<2;y++){var t=Main.tile[500+x,410+y];t.TileType=(ushort)ModContent.TileType<EcologyAnchorTile>();t.TileFrameX=(short)(x*18);t.TileFrameY=(short)(y*18);}
            Rectangle bounds=new(455,375,100,80);
            Check(EcologyWorld.SetRegion(p,anchor,bounds,0,true),"create owner region");
            Check(!EcologyWorld.ValidBounds(new(int.MaxValue,375,100,80),anchor),"overflow bounds reject");
            var stranger=new Player{whoAmI=1,active=true,position=p.position};stranger.GetModPlayer<EcologyPlayer>().Identity=Guid.NewGuid().ToString("N");
            Check(EcologyConversion.Convert(stranger,new Point(500,400),0,false,2)==0,"other player conversion rejected");
            Check(EcologyConversion.Convert(p,new Point(450,400),0,false,2)==0,"outside region rejected");
            Main.tile[480,390].TileType=TileID.Gold;Tile wired=Main.tile[481,390];wired.RedWire=true;Main.tile[482,390].TileType=TileID.Ebonstone;Main.tile[483,390].WallType=WallID.Wood;
            Check(EcologyConversion.Convert(p,new Point(481,390),0,false,2)>0,"convert ordinary blocks only");
            Check(Main.tile[480,390].TileType==TileID.Gold&&Main.tile[481,390].TileType==TileID.Stone&&Main.tile[482,390].TileType==EcologyCatalog.TerrainTile(0,0)&&Main.tile[483,390].TileType==EcologyCatalog.TerrainTile(0)&&Main.tile[483,390].WallType==WallID.Wood,"ore and wire protected while vanilla evil terrain and walls remain usable");
            for(int kind=0;kind<5;kind++)
            {
                int x=490+kind;Main.tile[x,385].TileType=(ushort)EcologyTerrain.OriginalTiles[kind];
                Check(EcologyConversion.Convert(p,new Point(x,385),0,false,0)==1,"base conversion "+kind);
                Check(EcologyConversion.Convert(p,new Point(x,385),0,true,0)==1&&Main.tile[x,385].TileType==EcologyTerrain.OriginalTiles[kind],"lossless reverse "+kind);
            }
            // Exercise the two player-facing applicator paths on an underground
            // block with a background wall. The wall must remain intact while
            // the solid block changes to the selected ecology terrain.
            Point applicatorPoint=new(500,400);Main.tile[applicatorPoint.X,applicatorPoint.Y].TileType=TileID.Stone;Main.tile[applicatorPoint.X,applicatorPoint.Y].WallType=WallID.Wood;
            EcologyRegionUI.Biome=9; // A stale panel selection must not override the held powder.
            p.inventory[0]=new Item(EcologyCatalog.Item("EcologyPowder0"),2);p.selectedItem=0;
            EcologyPlayer.Perform(p,4,applicatorPoint,default,9,true);
            Check(Main.tile[applicatorPoint.X,applicatorPoint.Y].TileType==EcologyCatalog.TerrainTile(0)&&Main.tile[applicatorPoint.X,applicatorPoint.Y].WallType==WallID.Wood&&p.HeldItem.stack==1,"powder converts walled underground block");
            Point restorePoint=new(502,400);Main.tile[restorePoint.X,restorePoint.Y].TileType=(ushort)EcologyCatalog.TerrainTile(0);Main.tile[restorePoint.X,restorePoint.Y].WallType=WallID.Wood;
            p.inventory[0]=new Item(ModContent.ItemType<EcologyRestorePowder>(),2);p.selectedItem=0;
            EcologyPlayer.Perform(p,4,restorePoint,default,-1,true);
            Check(Main.tile[restorePoint.X,restorePoint.Y].TileType==TileID.Stone&&Main.tile[restorePoint.X,restorePoint.Y].WallType==WallID.Wood&&p.HeldItem.stack==1,"restore powder resolves region biome instead of sentinel index");
            Point solutionPoint=new(501,400);Main.tile[solutionPoint.X,solutionPoint.Y].TileType=TileID.Stone;Main.tile[solutionPoint.X,solutionPoint.Y].WallType=WallID.Wood;
            p.inventory[0]=new Item(ModContent.ItemType<EcologySprayCan>());p.inventory[54]=new Item(EcologyCatalog.Item("EcologySolution0"),99);p.selectedItem=0;
            Check(p.PickAmmo(p.HeldItem,out int solutionShot,out _,out int solutionDamage,out float solutionKnockback,out int solutionAmmo,true)&&solutionShot==ModContent.ProjectileType<EcologySolutionJet>(),"solution applicator picks custom jet");
            var solutionSource=new EntitySource_ItemUse_WithAmmo(p,p.HeldItem,solutionAmmo,"EcologySolutionValidation");int jetSlot=Projectile.NewProjectile(solutionSource,solutionPoint.ToWorldCoordinates(),Vector2.UnitX,solutionShot,solutionDamage,solutionKnockback,p.whoAmI);
            Check(jetSlot>=0&&Main.projectile[jetSlot].ModProjectile is EcologySolutionJet jet&&jet.Biome==0,"solution jet binds selected ecology");
            Main.projectile[jetSlot].ModProjectile.AI();
            Check(Main.tile[solutionPoint.X,solutionPoint.Y].TileType==EcologyCatalog.TerrainTile(0)&&Main.tile[solutionPoint.X,solutionPoint.Y].WallType==WallID.Wood,"solution converts walled underground block");Main.projectile[jetSlot].Kill();
            for(int x=460;x<550;x+=7)for(int y=380;y<445;y+=7)EcologyConversion.Convert(p,new Point(x,y),0,false,3);
            var region=EcologyWorld.ByAnchor(anchor);region.TileCount=EcologyWorld.CountTiles(region);
            Check(EcologyWorld.ActiveBiome(p,true,600)==0,"600 blocks boss ecology");
            p.inventory[0]=new Item(EcologyCatalog.Summon(0));p.selectedItem=0;Main.dayTime=false;
            Check(Combat.EcologySummonBase.Valid(p,0),"real owned arena accepts matching reusable summon at night");
            Main.dayTime=true;Check(!Combat.EcologySummonBase.Valid(p,0),"night summon rejects daytime");Main.dayTime=false;
            Check(!Combat.EcologySummonBase.Valid(p,1),"mismatched ecology summon rejected");
            region.Ecology=false;Check(EcologyWorld.ActiveBiome(p)==-1&&EcologyWorld.ActiveBiome(p,false)==0,"observation mode only scene");region.Ecology=true;
            EcologyWorld.Defeat(0);int quota=EcologyWorld.PendingOre[0];EcologyWorld.Defeat(0);Check(quota==360&&EcologyWorld.PendingOre[0]==quota,"firstkill finite once");
            region.Cursor=int.MaxValue; // Simulate a very old cursor; scanning must stay in this rectangle.
            Main.gameMenu=false;for(int k=0;k<600;k++)world.PostUpdateWorld();Main.gameMenu=true;
            Check(EcologyWorld.PendingOre[0]<quota,"mineral job actually changes owned stone");
            int oreCount=0;for(int x=left;x<left+width;x++)for(int y=top;y<top+height;y++)if(Main.tile[x,y].HasTile&&Main.tile[x,y].TileType==EcologyCatalog.OreTile(0)){oreCount++;Check(bounds.Contains(x,y),"ore never outside region");}
            Check(oreCount+EcologyWorld.PendingOre[0]==quota,"ore budget conservation");
            var checkpoint=new TagCompound();world.SaveWorldData(checkpoint);int pending=EcologyWorld.PendingOre[0];world.ClearWorld();world.LoadWorldData(checkpoint);
            Check(EcologyWorld.Downed[0]&&EcologyWorld.PendingOre[0]==pending&&EcologyWorld.Regions.Count==1,"save/load preserves quota and ownership");
            using(var stream=new MemoryStream())
            {using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))world.NetSend(w);stream.Position=0;world.NetReceive(new BinaryReader(stream));Check(EcologyWorld.PendingOre[0]==pending&&EcologyWorld.Regions[0].Owner==p.GetModPlayer<EcologyPlayer>().Identity,"network roundtrip");}
            Main.netMode=NetmodeID.MultiplayerClient;Check(EcologyConversion.Convert(p,new Point(530,440),0,false,1)==0,"client cannot change world");Main.netMode=NetmodeID.SinglePlayer;
            region=EcologyWorld.ByAnchor(anchor);region.Ecology=false;int before=EcologyWorld.PendingOre[0];Main.gameMenu=false;for(int k=0;k<90;k++)world.PostUpdateWorld();Main.gameMenu=true;Check(EcologyWorld.PendingOre[0]==before,"observation pauses ore");region.Ecology=true;
            // The renewable path commits actual tiles, not loot items, and survives save/load.
            Point bed=new(520,430);p.position=new Vector2(519*16,428*16);
            for(int dx=0;dx<3;dx++)for(int dy=0;dy<2;dy++){Tile bt=Main.tile[bed.X+dx,bed.Y+dy];bt.TileType=(ushort)ModContent.TileType<EcologyMineralBedTile>();bt.HasTile=true;bt.TileFrameX=(short)(dx*18);bt.TileFrameY=(short)(dy*18);}
            for(int dx=0;dx<3;dx++)for(int dy=-3;dy<0;dy++){Tile empty=Main.tile[bed.X+dx,bed.Y+dy];empty.HasTile=false;}
            p.inventory[0]=new Item(EcologyCatalog.Item("EcologyMineralSeed0"),2);p.selectedItem=0;
            EcologyMineralBeds.Interact(p,bed);EcologyMineralBeds.Interact(p,bed);
            Check(EcologyMineralBeds.Jobs.Count==1&&p.HeldItem.stack==1,"one paid bed job, duplicate click no double spend");
            for(int step=0;step<900;step++)EcologyMineralBeds.Update();var bedSave=new TagCompound();world.SaveWorldData(bedSave);world.ClearWorld();world.LoadWorldData(bedSave);
            Check(EcologyMineralBeds.Jobs.Count==1&&EcologyMineralBeds.Jobs[0].Work==900,"bed progress persisted");
            for(int step=0;step<901;step++)EcologyMineralBeds.Update();
            Check(EcologyMineralBeds.Jobs.Count==0,"bed finite job completed");
            for(int dx=0;dx<3;dx++)for(int dy=-3;dy<0;dy++)Check(Main.tile[bed.X+dx,bed.Y+dy].HasTile&&Main.tile[bed.X+dx,bed.Y+dy].TileType==EcologyCatalog.OreTile(0),"bed yields nine mineable ore tiles");
            Check(p.HeldItem.stack==1,"bed never consumes twice on resume");
            for(int dx=0;dx<3;dx++)for(int dy=-3;dy<0;dy++){Tile empty=Main.tile[bed.X+dx,bed.Y+dy];empty.HasTile=false;}
            EcologyMineralBeds.Interact(p,bed);Check(EcologyMineralBeds.Jobs.Count==1,"second paid bed job");
            int seedType=EcologyCatalog.Item("EcologyMineralSeed0");int seedBefore=Main.item.Where(it=>it.active&&it.type==seedType).Sum(it=>it.stack);
            EcologyMineralBeds.Cancel(bed);EcologyMineralBeds.Cancel(bed);
            Check(EcologyMineralBeds.Jobs.Count==0&&Main.item.Where(it=>it.active&&it.type==seedType).Sum(it=>it.stack)==seedBefore+1,"broken unfinished bed refunds exactly once");
            for(int i=0;i<10;i++)
            {
                EcologyWorld.Downed[i]=true;int x=570+i*3,y=440;var t=Main.tile[x,y];t.HasTile=true;t.TileType=(ushort)EcologyCatalog.OreTile(i);t.TileFrameX=t.TileFrameY=0;
                p.position=new Vector2((x-2)*16,(y-2)*16);p.inventory[0]=new Item(EcologyCatalog.Item("EcologyTool"+i));p.selectedItem=0;
                for(int k=0;k<20;k++)p.PickTile(x,y,EcologyCatalog.PickRequirements[i]-1);Check(t.HasTile,"native underpower blocked "+i);
                for(int k=0;k<40&&t.HasTile;k++)p.PickTile(x,y,EcologyCatalog.ToolPower[i]);Check(!t.HasTile,"native first tool can mine "+i);
            }
            var newer=new TagCompound{{"schema",99},{"futureSentinel","keep"}};world.ClearWorld();world.LoadWorldData(newer);var preserved=new TagCompound();world.SaveWorldData(preserved);Check(preserved.GetString("futureSentinel")=="keep"&&!EcologyWorld.Editable,"future schema preserved read-only");
            ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().Logger.Info("ECOLOGY_WORLD_RUNTIME_PASS: vanilla dual gate, owned conversion, protected sentinels, reversibility, 300/600 activation, finite ore, save/network roundtrip, client authority and 10 native pick tests.");
        }
        finally
        {
            for(int x=0;x<width;x++)for(int y=0;y<height;y++)cells[x*height+y].Restore(Main.tile[left+x,top+y]);
            foreach(var pair in flags)pair.Key.SetValue(null,pair.Value);Main.hardMode=hard;Main.gameMenu=menu;Main.dayTime=day;Main.netMode=net;Main.myPlayer=my;Main.worldSurface=surface;
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(items,Main.item,items.Length);
            world.ClearWorld();world.LoadWorldData(original);mining.LoadWorldData(savedMining);
        }
    }
}

public sealed class EcologyHeadlessValidation : ModSystem
{
    public override void PostAddRecipes()
    {
        if(!EcologyValidation.Headless)return;
        EcologyValidation.Data();
        EcologyValidation.Run();
        Mod.Logger.Info("ECOLOGY_HEADLESS_PASS: conversion, restore, solution projectile, ownership and ore progression passed.");
    }
}
