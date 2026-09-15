using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Ecology;

public sealed class EcologyMineralBed : ModItem
{
    public override string Texture=>EcologyCatalog.Root+"MineralBed";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.TileType<EcologyMineralBedTile>());Item.width=48;Item.height=32;Item.maxStack=99;Item.rare=ItemRarityID.Blue;}
    public override void AddRecipes()=>CreateRecipe().AddIngredient(ItemID.StoneBlock,40).AddIngredient(ItemID.Glass,12).AddRecipeGroup(RecipeGroupID.IronBar,6).AddTile(EcologyCatalog.StationTile(0)).Register();
}
public sealed class EcologyMineralBedTile : ModTile
{
    public override string Texture=>EcologyCatalog.Root+"MineralBedTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);TileObjectData.newTile.LavaDeath=false;TileObjectData.addTile(Type);
        AddMapEntry(Color.CadetBlue,Language.GetText("Mods.StarfallThrone.Items.EcologyMineralBed.DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i,int j){yield return new Item(ModContent.ItemType<EcologyMineralBed>());}
    public override void KillMultiTile(int i,int j,int frameX,int frameY)=>EcologyMineralBeds.Cancel(new Point(i,j));
    public override bool RightClick(int i,int j){var t=Main.tile[i,j];EcologyPlayer.Request(3,new Point(i-t.TileFrameX/18%3,j-t.TileFrameY/18%2));return true;}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=ModContent.ItemType<EcologyMineralBed>();}
}
public static class EcologyMineralBeds
{
    public sealed class Job{public Point Point;public int Biome,Work;}
    private static readonly List<Job> jobs=new();
    public static IReadOnlyList<Job> Jobs=>jobs;
    public static void Clear()=>jobs.Clear();
    public static void Cancel(Point point)
    {
        if(!EcologyWorld.Editable)return;
        Job job=jobs.FirstOrDefault(j=>j.Point==point);if(job==null)return;
        // Remove the pending job BEFORE spawning its one seed. Repeated tile/network hooks
        // cannot refund twice, and committed ore has already removed its job.
        jobs.Remove(job);
        Item.NewItem(new Terraria.DataStructures.EntitySource_TileBreak(point.X,point.Y),point.X*16,point.Y*16,48,32,EcologyCatalog.Item("EcologyMineralSeed"+job.Biome));
    }
    private static bool Valid(Point p)=>WorldGen.InWorld(p.X,p.Y,20)&&Main.tile[p.X,p.Y].HasTile&&Main.tile[p.X,p.Y].TileType==ModContent.TileType<EcologyMineralBedTile>()&&Main.tile[p.X,p.Y].TileFrameX%54==0&&Main.tile[p.X,p.Y].TileFrameY%36==0;
    public static void Interact(Player player,Point point)
    {
        if(!EcologyWorld.Editable||!Valid(point)||!EcologyPlayer.Reach(player,point,160))return;
        var r=EcologyWorld.RegionAt(point);
        if(r==null||!r.Ecology||!EcologyWorld.CanEdit(player,r)||r.TileCount<300||!EcologyCatalog.VanillaUnlocked(r.Biome)||!EcologyWorld.Downed[r.Biome]){EcologyPlayer.Tell(player,"BedLocked");return;}
        var old=jobs.FirstOrDefault(j=>j.Point==point);if(old!=null){EcologyPlayer.Tell(player,"BedWorking",old.Work*100/1800);return;}
        if(player.HeldItem.type!=EcologyCatalog.Item("EcologyMineralSeed"+r.Biome)||player.HeldItem.stack<1||jobs.Count>=64){EcologyPlayer.Tell(player,"BedSeed");return;}
        if(!OutputClear(point,r)){EcologyPlayer.Tell(player,"BedBlocked");return;}
        EcologyPlayer.ConsumeHeld(player);jobs.Add(new Job{Point=point,Biome=r.Biome});EcologyPlayer.Tell(player,"BedStarted");
    }
    public static bool OutputClear(Point p,EcologyRegion r)
    {
        for(int x=p.X;x<p.X+3;x++)for(int y=p.Y-3;y<p.Y;y++)
        {
            if(!WorldGen.InWorld(x,y,20)||!r.Bounds.Contains(x,y))return false;
            var t=Main.tile[x,y];if(t.HasTile||t.LiquidAmount!=0||t.WallType!=0||t.HasActuator||t.RedWire||t.BlueWire||t.GreenWire||t.YellowWire)return false;
            if(global::StarfallThrone.Content.Mining.MiningWorld.ProtectedRegions.Any(area=>area.Contains(x,y)))return false;
        }
        return true;
    }
    public static void Update()
    {
        if(!EcologyWorld.Editable)return;
        for(int i=jobs.Count-1;i>=0;i--)
        {
            var job=jobs[i];if(!Valid(job.Point)){Cancel(job.Point);continue;}
            var r=EcologyWorld.RegionAt(job.Point);
            if(r==null||!r.Ecology||r.Biome!=job.Biome||r.TileCount<300||!EcologyCatalog.VanillaUnlocked(job.Biome)||!EcologyWorld.Downed[job.Biome])continue;
            job.Work=Math.Min(1800,job.Work+1);if(job.Work<1800||!OutputClear(job.Point,r))continue;
            for(int x=job.Point.X;x<job.Point.X+3;x++)for(int y=job.Point.Y-3;y<job.Point.Y;y++)
            {var tile=Main.tile[x,y];tile.HasTile=true;tile.TileType=(ushort)EcologyCatalog.OreTile(job.Biome);tile.Slope=SlopeType.Solid;tile.IsHalfBlock=false;EcologyConversion.FrameAndSync(x,y);}
            r.TileCount+=9;jobs.RemoveAt(i);EcologyWorld.Sync();
        }
    }
    public static void Save(TagCompound tag)=>tag["mineralBeds"]=jobs.Select(j=>new TagCompound{{"x",j.Point.X},{"y",j.Point.Y},{"biome",j.Biome},{"work",j.Work}}).ToList();
    public static void Load(TagCompound tag)
    {
        jobs.Clear();foreach(var t in tag.GetList<TagCompound>("mineralBeds").Take(64))
        {var job=new Job{Point=new(t.GetInt("x"),t.GetInt("y")),Biome=t.GetInt("biome"),Work=Math.Clamp(t.GetInt("work"),0,1800)};if(EcologyCatalog.Valid(job.Biome)&&WorldGen.InWorld(job.Point.X,job.Point.Y,20)&&!jobs.Any(j=>j.Point==job.Point))jobs.Add(job);}
    }
}
