using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Ecology;

public sealed class EcologyRegion
{
    public int Id,Biome,TileCount,Cursor;
    public Rectangle Bounds;
    public string Owner="";
    public bool Ecology=true;
    public Point Anchor;
}

// Only this system mutates region ownership and mineral quotas. No world-generation pass exists.
public sealed class EcologyWorld : ModSystem
{
    public static bool[] Downed=new bool[10];
    public static int[] PendingOre=new int[10];
    private static readonly List<EcologyRegion> regions=new();
    public static IReadOnlyList<EcologyRegion> Regions=>regions;
    private static int nextId=1,clock,regionCursor;
    private static bool futureSchema;
    private static TagCompound preserved;
    public static bool Authority=>Main.netMode!=NetmodeID.MultiplayerClient;
    public static bool Editable=>Authority&&!futureSchema;
    public override void ClearWorld(){Downed=new bool[10];PendingOre=new int[10];regions.Clear();nextId=1;clock=regionCursor=0;futureSchema=false;preserved=null;EcologyMineralBeds.Clear();}
    public override void OnWorldLoad()=>ClearWorld();
    public override void OnWorldUnload()=>ClearWorld();
    public static bool AnchorValid(Point a)=>WorldGen.InWorld(a.X,a.Y,10)&&Main.tile[a.X,a.Y].HasTile&&Main.tile[a.X,a.Y].TileType==ModContent.TileType<EcologyAnchorTile>();
    public static EcologyRegion RegionAt(Point p)=>regions.FirstOrDefault(r=>r.Bounds.Contains(p)&&AnchorValid(r.Anchor));
    public static EcologyRegion ByAnchor(Point p)=>regions.FirstOrDefault(r=>r.Anchor==p);
    public static int ActiveBiome(Player player,bool requireEcology=true,int threshold=300)
    {
        if(player==null||!player.active)return -1;
        EcologyRegion r=RegionAt(player.Center.ToTileCoordinates());
        return r!=null&&(!requireEcology||r.Ecology)&&r.TileCount>=threshold&&EcologyCatalog.VanillaUnlocked(r.Biome)?r.Biome:-1;
    }
    public static bool CanEdit(Player player,EcologyRegion r)=>r!=null&&player.GetModPlayer<EcologyPlayer>().Identity==r.Owner;
    public static bool ValidBounds(Rectangle b,Point anchor)=>b.Width>=30&&b.Width<=180&&b.Height>=20&&b.Height<=140&&b.X>=20&&b.Y>=20&&(long)b.X+b.Width<Main.maxTilesX-20&&(long)b.Y+b.Height<Main.maxTilesY-20&&b.Contains(anchor);
    public static bool SetRegion(Player player,Point anchor,Rectangle bounds,int biome,bool ecology)
    {
        if(!Editable||!EcologyCatalog.VanillaUnlocked(biome)||!AnchorValid(anchor)||!ValidBounds(bounds,anchor)||!EcologyPlayer.Reach(player,anchor,960))return false;
        EcologyRegion old=ByAnchor(anchor);
        if(old!=null&&!CanEdit(player,old))return false;
        if(old==null&&regions.Count>=32)return false;
        if(regions.Any(r=>r!=old&&r.Bounds.Intersects(bounds)))return false;
        if(global::StarfallThrone.Content.Mining.MiningWorld.ProtectedRegions.Any(r=>r.Intersects(bounds)))return false;
        // Rebinding existing converted land requires deliberate restoration first.
        if(old!=null&&old.Biome!=biome&&CountTiles(old)>0)return false;
        EcologyRegion entry=old??new EcologyRegion{Id=nextId++,Owner=player.GetModPlayer<EcologyPlayer>().Identity,Anchor=anchor};
        entry.Bounds=bounds;entry.Biome=biome;entry.Ecology=ecology;entry.Cursor=0;
        if(old==null)regions.Add(entry);
        entry.TileCount=CountTiles(entry);Sync();return true;
    }
    public static bool RemoveRegion(Player player,Point anchor)
    {
        var r=ByAnchor(anchor);if(!Editable||!CanEdit(player,r)||!EcologyPlayer.Reach(player,anchor,960))return false;
        regions.Remove(r);Sync();return true;
    }
    public static int CountTiles(EcologyRegion r)
    {
        int n=0;
        for(int x=r.Bounds.Left;x<r.Bounds.Right;x++)for(int y=r.Bounds.Top;y<r.Bounds.Bottom;y++)
        {Tile t=Main.tile[x,y];if(t.HasTile&&EcologyTerrain.BiomeOf(t.TileType)==r.Biome)n++;}
        return n;
    }
    public static void Defeat(int i)
    {
        if(!Editable||!EcologyCatalog.Valid(i)||Downed[i])return;
        Downed[i]=true;PendingOre[i]=360+i*40;
        string text=EcologyCatalog.Text("OreAwake",EcologyCatalog.BossNames[i],EcologyCatalog.OreNames[i],PendingOre[i]);
        if(Main.netMode==NetmodeID.Server)ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text),EcologyCatalog.Colors[i]);else Main.NewText(text,EcologyCatalog.Colors[i]);
        Sync();
    }
    public override void PostUpdateWorld()
    {
        if(!Editable||Main.gameMenu)return;
        EcologyMineralBeds.Update();
        if(++clock%30!=0||regions.Count==0)return;
        regionCursor%=regions.Count;EcologyRegion r=regions[regionCursor++];
        if(!AnchorValid(r.Anchor)){regions.Remove(r);Sync();return;}
        r.TileCount=CountTiles(r);
        if(r.Ecology&&EcologyCatalog.VanillaUnlocked(r.Biome)&&Downed[r.Biome]&&PendingOre[r.Biome]>0)
        {
            int length=r.Bounds.Width*r.Bounds.Height,placed=0;
            for(int step=0;step<Math.Min(length,1024)&&placed<12&&PendingOre[r.Biome]>0;step++)
            {
                int cursor=r.Cursor%length;r.Cursor=(cursor+1)%length;
                int x=r.Bounds.Left+cursor%r.Bounds.Width,y=r.Bounds.Top+cursor/r.Bounds.Width;
                if(y<Main.worldSurface+5||!EcologyConversion.Safe(x,y)||!Main.tile[x,y].HasTile||Main.tile[x,y].TileType!=EcologyCatalog.TerrainTile(r.Biome))continue;
                Main.tile[x,y].TileType=(ushort)EcologyCatalog.OreTile(r.Biome);EcologyConversion.FrameAndSync(x,y);PendingOre[r.Biome]--;placed++;
            }
        }
        if(clock%180==0)Sync();
    }
    public override void SaveWorldData(TagCompound tag)
    {
        if(futureSchema&&preserved!=null){foreach(var pair in preserved)tag[pair.Key]=pair.Value;return;}
        tag["schema"]=1;tag["nextId"]=nextId;
        for(int i=0;i<10;i++){tag["downed"+EcologyCatalog.Keys[i]]=Downed[i];tag["pending"+EcologyCatalog.Keys[i]]=PendingOre[i];}
        tag["regions"]=regions.Select(r=>new TagCompound{{"id",r.Id},{"biome",r.Biome},{"owner",r.Owner},{"x",r.Bounds.X},{"y",r.Bounds.Y},{"w",r.Bounds.Width},{"h",r.Bounds.Height},{"ax",r.Anchor.X},{"ay",r.Anchor.Y},{"ecology",r.Ecology},{"cursor",r.Cursor}}).ToList();
        EcologyMineralBeds.Save(tag);
    }
    public override void LoadWorldData(TagCompound tag)
    {
        if(tag.GetInt("schema")>1){futureSchema=true;preserved=tag;return;}
        for(int i=0;i<10;i++){Downed[i]=tag.GetBool("downed"+EcologyCatalog.Keys[i]);PendingOre[i]=Downed[i]?Math.Clamp(tag.GetInt("pending"+EcologyCatalog.Keys[i]),0,360+i*40):0;}
        nextId=Math.Max(1,tag.GetInt("nextId"));
        foreach(var t in tag.GetList<TagCompound>("regions").Take(32))
        {
            var r=new EcologyRegion{Id=t.GetInt("id"),Biome=t.GetInt("biome"),Owner=t.GetString("owner"),Bounds=new(t.GetInt("x"),t.GetInt("y"),t.GetInt("w"),t.GetInt("h")),Anchor=new(t.GetInt("ax"),t.GetInt("ay")),Ecology=t.GetBool("ecology"),Cursor=Math.Max(0,t.GetInt("cursor"))};
            if(!EcologyCatalog.Valid(r.Biome)||!ValidBounds(r.Bounds,r.Anchor)||regions.Any(o=>o.Bounds.Intersects(r.Bounds)))continue;
            r.TileCount=CountTiles(r);regions.Add(r);nextId=Math.Max(nextId,r.Id+1);
        }
        EcologyMineralBeds.Load(tag);
    }
    public override void NetSend(BinaryWriter w)
    {
        for(int i=0;i<10;i++){w.Write(Downed[i]);w.Write(PendingOre[i]);}
        w.Write((byte)regions.Count);
        foreach(var r in regions){w.Write(r.Id);w.Write((byte)r.Biome);w.Write(r.Owner);w.Write(r.Bounds.X);w.Write(r.Bounds.Y);w.Write((ushort)r.Bounds.Width);w.Write((ushort)r.Bounds.Height);w.Write(r.Anchor.X);w.Write(r.Anchor.Y);w.Write(r.Ecology);w.Write(r.TileCount);}
    }
    public override void NetReceive(BinaryReader r)
    {
        var down=new bool[10];var pending=new int[10];var incoming=new List<EcologyRegion>();
        for(int i=0;i<10;i++){down[i]=r.ReadBoolean();pending[i]=Math.Clamp(r.ReadInt32(),0,360+i*40);}
        int count=r.ReadByte();if(count>32)throw new IOException("Invalid ecology region count");
        for(int i=0;i<count;i++)
        {
            var entry=new EcologyRegion{Id=r.ReadInt32(),Biome=r.ReadByte(),Owner=r.ReadString(),Bounds=new(r.ReadInt32(),r.ReadInt32(),r.ReadUInt16(),r.ReadUInt16()),Anchor=new(r.ReadInt32(),r.ReadInt32()),Ecology=r.ReadBoolean(),TileCount=r.ReadInt32()};
            if(!EcologyCatalog.Valid(entry.Biome)||!ValidBounds(entry.Bounds,entry.Anchor))throw new IOException("Invalid ecology region");incoming.Add(entry);
        }
        Downed=down;PendingOre=pending;regions.Clear();regions.AddRange(incoming);
    }
    public static void Sync(){if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.WorldData);}
}
