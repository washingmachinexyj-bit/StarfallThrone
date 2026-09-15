using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameContent.ItemDropRules;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage;

public static class VoyageSupportValidation
{
    static void Check(bool ok,string why) { if(!ok) throw new InvalidOperationException("Voyage support: "+why); }
    static void Log(string message)=>ModContent.GetInstance<VoyageWorld>().Mod.Logger.Info(message);
    public static void Data()
    {
        for(int i=0;i<17;i++)
        {
            Item summon = new(VoyageCatalog.Summon(i)), bag = new(VoyageCatalog.Bag(i));
            Check(!summon.consumable && summon.maxStack==1,"reusable summon "+i);
            Check(bag.expert && bag.ModItem.CanRightClick() && ItemID.Sets.BossBag[bag.type],"openable expert bag "+i);
            Check(new Item(VoyageCatalog.Relic(i)).master,"master relic "+i);
            Check(new Item(VoyageCatalog.Mask(i)).headSlot>=0,"wearable mask "+i);
            Recipe recipe=Main.recipe.Take(Recipe.numRecipes).Single(r=>r.createItem.type==summon.type);
            Check(recipe.requiredItem.Any(x=>x.type==VoyageCatalog.Alloy&&x.stack==8),"summon alloy "+i);
            if(i>0) Check(recipe.requiredItem.Any(x=>x.type==VoyageCatalog.Material(i-1)&&x.stack==10),"summon previous drop "+i);
            Check(recipe.requiredTile.Contains(global::StarfallThrone.Content.Mining.MiningCatalog.StationTileType(
                global::StarfallThrone.Content.Mining.MiningRecipes.VoyageStation(i, true))),"summon stage workbench "+i);
            foreach(int material in new[]{VoyageCatalog.Material(i),VoyageCatalog.Core(i)})
                Check(Main.recipe.Take(Recipe.numRecipes).Any(r=>r.requiredItem.Any(x=>x.type==material)),"unused material "+i);
            List<DropRateInfo> bagDrops=new();
            foreach(var rule in Main.ItemDropsDB.GetRulesForItemID(bag.type)) rule.ReportDroprates(bagDrops,new DropRateInfoChainFeed(1f));
            Check(bagDrops.Any(x=>x.itemId==VoyageCatalog.Material(i))&&bagDrops.Any(x=>x.itemId==VoyageCatalog.Core(i))&&bagDrops.Any(x=>x.itemId==VoyageCatalog.Expert(i)&&x.dropRate>=.999f),"bag contents "+i);
            Check(!bagDrops.Any(x=>x.itemId==VoyageCatalog.Relic(i)||x.itemId==VoyageCatalog.Trophy(i)),"collectibles incorrectly inside bag "+i);
            List<DropRateInfo> bossDrops=new();
            foreach(var rule in Main.ItemDropsDB.GetRulesForNPCID(VoyageCatalog.BossType(i),false)) rule.ReportDroprates(bossDrops,new DropRateInfoChainFeed(1f));
            for(int mode=0;mode<3;mode++)
            {
                int previousMode=Main.GameMode;
                Main.GameMode=mode;
                try
                {
                var info=new DropAttemptInfo{IsExpertMode=mode>0,IsMasterMode=mode==2};
                bool Available(int type)=>bossDrops.Any(x=>x.itemId==type&&(x.conditions==null||x.conditions.All(c=>c.CanDrop(info))));
                Check(Available(VoyageCatalog.Material(i))==(mode==0),"normal material leakage "+i+"/"+mode);
                Check(Available(VoyageCatalog.Bag(i))==(mode>0),"expert bag condition "+i+"/"+mode);
                Check(Available(VoyageCatalog.Relic(i))==(mode==2),"master relic condition "+i+"/"+mode);
                Check(!Available(VoyageCatalog.Expert(i)),"expert accessory outside bag "+i);
                }
                finally { Main.GameMode=previousMode; }
            }
        }
        var world=ModContent.GetInstance<VoyageWorld>(); bool[] old=(bool[])VoyageWorld.Downed.Clone();
        try
        {
            world.LoadWorldData(new TagCompound());Check(!VoyageWorld.Downed.Any(x=>x),"old saves must have no voyage kills");
            for(int i=0;i<17;i++) VoyageWorld.Downed[i]=i%2==0;
            TagCompound data=new();world.SaveWorldData(data);world.OnWorldUnload();world.LoadWorldData(data);
            Check(VoyageWorld.Downed.SequenceEqual(Enumerable.Range(0,17).Select(i=>i%2==0)),"save roundtrip");
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);
            world.NetSend(writer);writer.Flush();stream.Position=0;world.OnWorldUnload();world.NetReceive(new BinaryReader(stream));
            Check(VoyageWorld.Downed.SequenceEqual(Enumerable.Range(0,17).Select(i=>i%2==0)),"network roundtrip");
        }
        finally { VoyageWorld.Downed=old; }
        Log("VOYAGE_SUPPORT_PASS: 17 reusable recipes, 34 used materials, 17 bags with guaranteed experts, normal/expert/master loot separation, world save/network.");
    }
    public static void Run()
    {
        Check(Main.gameMenu,"only isolated menu tests");
        NPC[] oldNPCs=(NPC[])Main.npc.Clone();Player oldPlayer=Main.player[0];
        var oldWorldFile=Main.ActiveWorldFileData;
        bool[] old=(bool[])VoyageWorld.Downed.Clone();bool end=StarfallWorld.Downed[16];int net=Main.netMode,my=Main.myPlayer;
        int invasion=Main.invasionType;bool pumpkin=Main.pumpkinMoon,snow=Main.snowMoon;
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.invasionType=0;Main.pumpkinMoon=Main.snowMoon=false;
        Player p=new(){whoAmI=0,active=true,position=new Vector2(5600,2000)};p.ResetEffects();Main.player[0]=p;
        try
        {
            // NPC.SpawnOnPlayer invokes third-party OnSpawn hooks. Supply the in-memory
            // world lifecycle they normally receive on world entry; never open a save.
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            if(ModLoader.TryGetMod("BossChecklist",out Mod checklist))
            {
                ModSystem records=checklist.GetContent<ModSystem>().Single(s=>s.Name=="RecordSystem");
                records.OnWorldLoad(); records.LoadWorldData(new TagCompound());
            }
            for(int n=0;n<Main.maxNPCs;n++)Main.npc[n]=new NPC(){whoAmI=n};
            for(int i=0;i<17;i++)
            {
                VoyageWorld.Downed=new bool[17];StarfallWorld.Downed[16]=false;
                p.selectedItem=0;p.inventory[0]=new Item(VoyageCatalog.Summon(i));
                Check(!VoyageSummonBase.Valid(p,i)&&!VoyageSummonBase.TrySummon(p,i),"locked summon "+i);
                if(i==0)StarfallWorld.Downed[16]=true;else VoyageWorld.Downed[i-1]=true;
                Check(VoyageSummonBase.Valid(p,i),"unlocked summon "+i);
                p.inventory[0]=new Item(ItemID.DirtBlock);Check(!VoyageSummonBase.TrySummon(p,i),"forged packet without held summon");
                p.inventory[0]=new Item(VoyageCatalog.Summon(i));
                Check(VoyageSummonBase.TrySummon(p,i),"authoritative summon "+i);
                Check(Main.npc.Count(n=>n.active&&n.type==VoyageCatalog.BossType(i))==1,"exactly one spawn "+i);
                Check(!VoyageSummonBase.TrySummon(p,i),"duplicate summon "+i);
                foreach(var n in Main.npc) n.active=false;
                Main.invasionType=1;Check(!VoyageSummonBase.Valid(p,i),"invasion summon "+i);Main.invasionType=0;
            }
            Log("VOYAGE_SUMMON_PASS: all17 progression gates, held-item authorization, exact spawn, duplicate and invasion rejection.");
        }
        finally
        {
            for(int n=0;n<Main.npc.Length;n++)Main.npc[n]=oldNPCs[n];Main.player[0]=oldPlayer;
            VoyageWorld.Downed=old;StarfallWorld.Downed[16]=end;Main.netMode=net;Main.myPlayer=my;
            Main.invasionType=invasion;Main.pumpkinMoon=pumpkin;Main.snowMoon=snow;
            Main.ActiveWorldFileData=oldWorldFile;
        }
    }
}
