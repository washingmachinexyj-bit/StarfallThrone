using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.GameContent.ItemDropRules;
using Terraria.Utilities;

namespace StarfallThrone.Content.Pantheon;

// Only the opt-in isolated native-client menu fixture may execute these mutations.
public static partial class PantheonValidation
{
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Pantheon support: "+why);checks++;}
    static void Log(string message)=>ModContent.GetInstance<PantheonWorld>().Mod.Logger.Info(message);
    static void Guard()=>Check(Main.gameMenu&&!Main.dedServ&&Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"),"isolated menu guard");
    static IEnumerable<Recipe> Recipes(int type)=>Main.recipe.Take(Recipe.numRecipes).Where(r=>r.createItem.type==type);
    static List<DropRateInfo> Rates(IEnumerable<IItemDropRule> rules)
    {List<DropRateInfo> rates=new();foreach(var rule in rules)rule.ReportDroprates(rates,new DropRateInfoChainFeed(1));return rates;}
    public static void Data()
    {
        Guard();checks=0;bool[] saved=PantheonWorld.Downed;bool ark=Voyage.VoyageWorld.Downed[16];int mode=Main.GameMode;
        try
        {
            PantheonWorld.Downed=new bool[17];
            for(int stage=-1;stage<17;stage++)
            {
                Voyage.VoyageWorld.Downed[16]=stage>=0;
                for(int j=0;j<17;j++)PantheonWorld.Downed[j]=j<stage;
                for(int i=0;i<17;i++)Check(PantheonWorld.CanSummon(i)==(i==0?stage>=0:i<=stage),"world sequential gate "+stage+"/"+i);
            }
            Voyage.VoyageWorld.Downed[16]=true;Array.Fill(PantheonWorld.Downed,true);
            for(int i=0;i<17;i++)
            {
                Item summon=new(PantheonCatalog.Summon(i)),bag=new(PantheonCatalog.Bag(i));
                Check(!summon.consumable&&summon.maxStack==1,"reusable summon "+i);
                Check(bag.expert&&bag.ModItem.CanRightClick()&&ItemID.Sets.BossBag[bag.type]&&ItemID.Sets.OpenableBag[bag.type],"native bag "+i);
                Check(new Item(PantheonCatalog.Relic(i)).master&&new Item(PantheonCatalog.Pet(i)).master,"master collectibles "+i);
                Check(new Item(PantheonCatalog.Mask(i)).headSlot>=0,"native mask equip "+i);
                Recipe r=Recipes(summon.type).Single();int grade=PantheonCatalog.Tier(i);
                Check(r.requiredTile.Contains(PantheonCatalog.CraftStation(grade)),"summon current craft station "+i);
                if(i==0)
                {
                    Check(r.requiredItem.Any(x=>x.type==Voyage.VoyageCatalog.Material(16)&&x.stack==12),"first Ark material");
                    Check(r.requiredItem.Any(x=>x.type==Voyage.VoyageCatalog.Core(16)&&x.stack==2),"first Ark core");
                    Check(r.requiredItem.Any(x=>x.type==Mining.MiningCatalog.BarType(12)&&x.stack==6),"first genesis bar");
                }
                else
                {
                    Check(r.requiredItem.Any(x=>x.type==PantheonCatalog.Material(i-1)&&x.stack==12),"previous material "+i);
                    Check(r.requiredItem.Any(x=>x.type==PantheonCatalog.Core(i-1)&&x.stack==2),"previous guaranteed core "+i);
                    Check(r.requiredItem.Any(x=>x.type==PantheonRecipes.ThemeIngredients[i]),"common themed ingredient "+i);
                    Check(grade==0||PantheonRecipes.StationUnlocks[grade-1]<i,"station unlocked before boss "+i);
                }
                for(int future=i;future<17;future++)Check(!r.requiredItem.Any(x=>x.type==PantheonCatalog.Material(future)||x.type==PantheonCatalog.Core(future)),"no future summon material "+i+"/"+future);
                var drops=Rates(Main.ItemDropsDB.GetRulesForItemID(bag.type));
                Check(drops.Any(x=>x.itemId==PantheonCatalog.Expert(i)&&x.dropRate>=.999f),"guaranteed expert "+i);
                Check(drops.Any(x=>x.itemId==PantheonCatalog.Core(i)&&x.stackMin==2&&x.stackMax==2&&x.dropRate>=.999f),"guaranteed two cores "+i);
                Check(!drops.Any(x=>x.itemId==PantheonCatalog.Relic(i)||x.itemId==PantheonCatalog.Trophy(i)),"no external collectibles in bag "+i);
                var bossDrops=Rates(Main.ItemDropsDB.GetRulesForNPCID(PantheonCatalog.BossType(i),false));
                for(int difficulty=0;difficulty<3;difficulty++)
                {
                    Main.GameMode=difficulty;var info=new DropAttemptInfo{IsExpertMode=difficulty>0,IsMasterMode=difficulty==2};
                    bool Has(int type)=>bossDrops.Any(x=>x.itemId==type&&(x.conditions==null||x.conditions.All(c=>c.CanDrop(info))));
                    Check(Has(PantheonCatalog.Material(i))==(difficulty==0)&&Has(PantheonCatalog.Core(i))==(difficulty==0),"no loose expert materials "+i+"/"+difficulty);
                    Check(Has(PantheonCatalog.Bag(i))==(difficulty>0),"bag difficulty "+i+"/"+difficulty);
                    Check(Has(PantheonCatalog.Relic(i))==(difficulty==2),"relic difficulty "+i+"/"+difficulty);
                    Check(!Has(PantheonCatalog.Expert(i)),"expert only in bag "+i);
                }
            }
            for(int grade=0;grade<6;grade++)
            {
                int first=PantheonRecipes.EssenceSources[grade];
                var essence=Recipes(PantheonCatalog.Essence(grade)).ToArray();
                Check(essence.Length==Enumerable.Range(0,17).Count(i=>PantheonCatalog.Tier(i)==grade)*2,"two essence conversions per same-grade boss "+grade);
                Check(essence.Any(r=>r.requiredItem.Any(x=>x.type==PantheonCatalog.Material(first)&&x.stack==6)&&r.createItem.stack==3),"essence earliest material accessible "+grade);
                Check(essence.Any(r=>r.requiredItem.Any(x=>x.type==PantheonCatalog.Core(first)&&x.stack==1)&&r.createItem.stack==6),"essence earliest core accessible "+grade);
                Check(essence.All(r=>r.requiredTile.Contains(PantheonCatalog.CraftStation(grade))),"essence grade station "+grade);
                Recipe station=Recipes(PantheonCatalog.Station(grade)).Single();int unlock=PantheonRecipes.StationUnlocks[grade];
                Check(station.requiredTile.Contains(PantheonCatalog.CraftStation(grade)),"station predecessor workbench "+grade);
                Check(station.requiredItem.Any(x=>x.type==PantheonCatalog.Material(unlock)&&x.stack==16)&&station.requiredItem.Any(x=>x.type==PantheonCatalog.Core(unlock)&&x.stack==2),"station current resources "+grade);
                Check(!station.requiredItem.Any(x=>Enumerable.Range(0,6).Any(n=>x.type==PantheonCatalog.Station(n))),"do not consume placed workstation "+grade);
                for(int future=unlock+1;future<17;future++)Check(!station.requiredItem.Any(x=>x.type==PantheonCatalog.Material(future)||x.type==PantheonCatalog.Core(future)),"no station future cycle "+grade+"/"+future);
                PantheonWorld.Downed[unlock]=false;Check(station.Conditions.All(c=>!c.IsMet()),"station locked condition "+grade);
                PantheonWorld.Downed[unlock]=true;Check(station.Conditions.All(c=>c.IsMet()),"station unlocked condition "+grade);
                ModTile tile=TileLoader.GetTile(PantheonCatalog.StationTile(grade));TileObjectData data=TileObjectData.GetTileData(tile.Type,0);
                Check(data.Width==5&&data.Height==4&&data.CoordinateHeights.SequenceEqual(new[]{16,16,16,16}),"station 5x4 frame contract "+grade);
                Check(tile.AdjTiles.SequenceEqual(Enumerable.Range(0,grade).Select(PantheonCatalog.StationTile)),"only same chain station adjacency "+grade);
            }
            StateRoundtrip();Log("PANTHEON_SUPPORT_DATA_PASS checks="+checks+" summons=17 bags=17 grades=6 stations=6 recipeCycles=0 gatedStages=18");
        }
        finally{PantheonWorld.Downed=saved;Voyage.VoyageWorld.Downed[16]=ark;Main.GameMode=mode;}
    }
    static void StateRoundtrip()
    {
        var world=ModContent.GetInstance<PantheonWorld>();
        for(int i=0;i<17;i++)PantheonWorld.Downed[i]=i%2==0;
        TagCompound tag=new();world.SaveWorldData(tag);world.OnWorldUnload();world.LoadWorldData(tag);
        Check(PantheonWorld.Downed.SequenceEqual(Enumerable.Range(0,17).Select(i=>i%2==0)),"world save roundtrip");
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);world.NetSend(writer);writer.Flush();stream.Position=0;
        world.OnWorldUnload();world.NetReceive(new BinaryReader(stream));Check(PantheonWorld.Downed.SequenceEqual(Enumerable.Range(0,17).Select(i=>i%2==0)),"world net roundtrip");
        bool[] before=(bool[])PantheonWorld.Downed.Clone();
        for(int length=0;length<17;length++){using var shortStream=new MemoryStream(new byte[length]);world.NetReceive(new BinaryReader(shortStream));Check(PantheonWorld.Downed.SequenceEqual(before),"truncated world packet atomic "+length);}
        world.LoadWorldData(new TagCompound());Check(!PantheonWorld.Downed.Any(x=>x),"legacy save defaults false");
        Check(!PantheonWorld.CanSummon(-1)&&!PantheonWorld.CanSummon(17)&&!PantheonWorld.IsDowned(255),"bounded world flags");
    }
    public static void Run()
    {
        Guard();checks=0;
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var items=(Item[])Main.item.Clone();
        var projectiles=(Projectile[])Main.projectile.Clone();var dust=(Dust[])Main.dust.Clone();var gore=(Gore[])Main.gore.Clone();var texts=(CombatText[])Main.combatText.Clone();
        var identities=(int[,])Main.projectileIdentity.Clone();var rand=Main.rand;var worldFile=Main.ActiveWorldFileData;
        bool[] downed=PantheonWorld.Downed;bool ark=Voyage.VoyageWorld.Downed[16];bool day=Main.dayTime,pumpkin=Main.pumpkinMoon,snow=Main.snowMoon;
        int mode=Main.GameMode,net=Main.netMode,my=Main.myPlayer,invasion=Main.invasionType,countdown=NPC.MoonLordCountdown;
        ModSystem checklistRecords=null;TagCompound checklistState=new();
        try
        {
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{active=false,whoAmI=i};
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{active=false,whoAmI=i};
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{active=false,whoAmI=i};
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.GameMode=0;Main.invasionType=0;Main.pumpkinMoon=Main.snowMoon=false;NPC.MoonLordCountdown=0;
            Main.rand=new UnifiedRandom(110017);ClearItems();Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            if(ModLoader.TryGetMod("BossChecklist",out Mod checklist))
            {checklistRecords=checklist.GetContent<ModSystem>().Single(s=>s.Name=="RecordSystem");checklistRecords.SaveWorldData(checklistState);checklistRecords.OnWorldLoad();checklistRecords.LoadWorldData(new TagCompound());}
            PantheonWorld.Downed=new bool[17];Voyage.VoyageWorld.Downed[16]=true;
            Player p=new(){active=true,whoAmI=0,position=new Vector2(16000,2400)};p.ResetEffects();Main.player[0]=p;
            StateChecks();Summons(p);Packets(p);Loot(p);Pets(p);PetRender(p);WearRender();FurnitureRender();
            Log("PANTHEON_SUPPORT_RUNTIME_PASS checks="+checks+" nativeSummons=17 normalLoot=17 expertBags=34 furniture=40 pets=17 masks=17 isolatedRestore=true");
        }
        finally
        {
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(items,Main.item,items.Length);Array.Copy(projectiles,Main.projectile,projectiles.Length);
            Array.Copy(dust,Main.dust,dust.Length);Array.Copy(gore,Main.gore,gore.Length);Array.Copy(texts,Main.combatText,texts.Length);Array.Copy(identities,Main.projectileIdentity,identities.Length);
            Main.rand=rand;Main.ActiveWorldFileData=worldFile;Main.netMode=net;Main.myPlayer=my;Main.GameMode=mode;Main.invasionType=invasion;
            Main.pumpkinMoon=pumpkin;Main.snowMoon=snow;Main.dayTime=day;NPC.MoonLordCountdown=countdown;
            PantheonWorld.Downed=downed;Voyage.VoyageWorld.Downed[16]=ark;
            if(checklistRecords!=null){checklistRecords.OnWorldLoad();checklistRecords.LoadWorldData(checklistState);}
        }
    }
    static void ClearItems(){for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item();}
    static void StateChecks()
    {
        bool king=NPC.downedSlimeKing,hard=Main.hardMode,moon=NPC.downedMoonlord;
        bool[] divine=(bool[])Divine.DivineWorld.Downed.Clone(),ascendant=(bool[])Ascendant.AscendantWorld.Downed.Clone();
        try
        {
            StateRoundtrip();Array.Fill(PantheonWorld.Downed,true);Voyage.VoyageWorld.Downed[16]=true;
            for(int mask=0;mask<8;mask++)
            {
                NPC.downedSlimeKing=(mask&1)!=0;Main.hardMode=(mask&2)!=0;NPC.downedMoonlord=(mask&4)!=0;
                for(int i=0;i<17;i++)Check(PantheonWorld.CanSummon(i),"no legacy challenge deadline "+mask+"/"+i);
            }
            Check(Divine.DivineWorld.Downed.SequenceEqual(divine)&&Ascendant.AscendantWorld.Downed.SequenceEqual(ascendant),"old challenge flags untouched");
            Log("PANTHEON_STATE_RUNTIME_PASS saveRoundtrip=true netRoundtrip=true shortNetAtomic=17 oldSaveDefaults=true legacyDeadlineMatrix=136 oldChallengeFlagsUntouched=true");
        }
        finally{NPC.downedSlimeKing=king;Main.hardMode=hard;NPC.downedMoonlord=moon;}
    }
    static void ClearNPCs(){foreach(var n in Main.npc)n.active=false;}
    static int Amount(int type)=>Main.item.Where(x=>x.active&&x.type==type).Sum(x=>x.stack);
    static void Arena(Player p,int i)
    {
        p.ZoneOverworldHeight=i!=2&&i!=6;p.ZoneDirtLayerHeight=i==2;p.ZoneRockLayerHeight=false;p.ZoneSkyHeight=false;p.ZoneUnderworldHeight=i==6;
        p.ZoneJungle=i==4||i==11;p.ZoneHallow=i==7;p.ZoneBeach=i==13;Main.dayTime=false;
        p.active=true;p.dead=p.ghost=false;p.selectedItem=0;p.inventory[0]=new Item(PantheonCatalog.Summon(i));p.GetModPlayer<PantheonSummonPlayer>().Cooldown=0;
    }
    static void Summons(Player p)
    {
        for(int i=0;i<17;i++)
        {
            ClearNPCs();Array.Clear(PantheonWorld.Downed);Voyage.VoyageWorld.Downed[16]=false;Arena(p,i);
            Check(!PantheonWorld.TrySummon(i,0),"locked native summon "+i);
            if(i==0)Voyage.VoyageWorld.Downed[16]=true;else PantheonWorld.Downed[i-1]=true;
            Check(PantheonSummonBase.Valid(p,i),"own prerequisite enough "+i);
            p.inventory[0]=new Item(ItemID.DirtBlock);Check(!PantheonWorld.TrySummon(i,0),"held authorization "+i);p.inventory[0]=new Item(PantheonCatalog.Summon(i));
            p.dead=true;Check(!PantheonWorld.TrySummon(i,0),"dead "+i);p.dead=false;p.ghost=true;Check(!PantheonWorld.TrySummon(i,0),"ghost "+i);p.ghost=false;
            p.active=false;Check(!PantheonWorld.TrySummon(i,0),"inactive "+i);p.active=true;
            Main.invasionType=1;Check(!PantheonSummonBase.Valid(p,i),"invasion "+i);Main.invasionType=0;
            Main.pumpkinMoon=true;Check(!PantheonSummonBase.Valid(p,i),"pumpkin "+i);Main.pumpkinMoon=false;
            Main.snowMoon=true;Check(!PantheonSummonBase.Valid(p,i),"snowmoon "+i);Main.snowMoon=false;
            NPC.MoonLordCountdown=30;Check(!PantheonSummonBase.Valid(p,i),"arrival "+i);NPC.MoonLordCountdown=0;
            Main.netMode=NetmodeID.MultiplayerClient;Check(!PantheonWorld.TrySummon(i,0),"client cannot spawn "+i);Main.netMode=NetmodeID.SinglePlayer;
            Main.npc[0].SetDefaults(NPCID.EyeofCthulhu);Main.npc[0].active=true;Check(!PantheonWorld.TrySummon(i,0),"other boss "+i);ClearNPCs();
            Check(PantheonWorld.TrySummon(i,0),"native summon "+i);Check(Main.npc.Count(n=>n.active&&n.type==PantheonCatalog.BossType(i))==1,"one native boss "+i);
            Check(!PantheonWorld.TrySummon(i,0),"duplicate rejected "+i);ClearNPCs();Check(!PantheonWorld.TrySummon(i,0),"cooldown spam "+i);
            var cooldown=p.GetModPlayer<PantheonSummonPlayer>();for(int t=0;t<120;t++)cooldown.PostUpdate();Check(cooldown.Cooldown==0,"cooldown progresses "+i);
            p.ZoneOverworldHeight=p.ZoneDirtLayerHeight=p.ZoneRockLayerHeight=p.ZoneUnderworldHeight=p.ZoneSkyHeight=p.ZoneJungle=p.ZoneHallow=p.ZoneBeach=false;Main.dayTime=true;
            Check(!PantheonSummonBase.Location(p,i),"wrong biome/time rejects "+i);
        }
        Check(!PantheonWorld.TrySummon(-1,0)&&!PantheonWorld.TrySummon(17,0)&&!PantheonWorld.TrySummon(0,-1)&&!PantheonWorld.TrySummon(0,Main.maxPlayers),"bounded summon indices");
        Log("PANTHEON_SUMMON_RUNTIME_PASS nativeSummons=17 worldGates=17 heldAuthorization=17 biomeTime=17 duplicateCooldown=17");
    }
    static void Packets(Player p)
    {
        ClearNPCs();Array.Fill(PantheonWorld.Downed,true);Voyage.VoyageWorld.Downed[16]=true;Arena(p,0);
        void Send(byte[] payload,int sender){using var stream=new MemoryStream(payload);PantheonSummonBase.ReceivePacket(new BinaryReader(stream),sender);}
        Main.netMode=NetmodeID.Server;
        foreach(byte[] payload in new[]{Array.Empty<byte>(),new byte[]{255},new byte[]{17},new byte[]{0,0},new byte[256]})
        {Send(payload,0);Check(!Main.npc.Any(n=>n.active),"invalid packet has no spawn");}
        foreach(int sender in new[]{-1,Main.maxPlayers,255}){Send(new byte[]{0},sender);Check(!Main.npc.Any(n=>n.active),"invalid sender");}
        p.inventory[0]=new Item(ItemID.DirtBlock);Send(new byte[]{0},0);Check(!Main.npc.Any(n=>n.active),"forged held inventory rejected");
        Arena(p,0);p.GetModPlayer<PantheonSummonPlayer>().Cooldown=120;Send(new byte[]{0},0);Check(!Main.npc.Any(n=>n.active),"packet cooldown rejected");
        Arena(p,0);Voyage.VoyageWorld.Downed[16]=false;Send(new byte[]{0},0);Check(!Main.npc.Any(n=>n.active),"packet world gate rejected");
        Voyage.VoyageWorld.Downed[16]=true;Main.netMode=NetmodeID.MultiplayerClient;Send(new byte[]{0},0);Check(!Main.npc.Any(n=>n.active),"client receive cannot spawn");
        Main.netMode=NetmodeID.SinglePlayer;Log("PANTHEON_PACKET_RUNTIME_PASS truncatedOversized=true invalidIndices=true authoritativeSender=true heldItemWorldCooldown=true");
    }
    static void Loot(Player p)
    {
        var resolver=new ItemDropResolver(Main.ItemDropsDB);Array.Fill(PantheonWorld.Downed,true);Voyage.VoyageWorld.Downed[16]=true;
        for(int i=0;i<17;i++)for(int mode=0;mode<3;mode++)
        {
            Main.GameMode=mode;ClearItems();NPC boss=new();boss.SetDefaults(PantheonCatalog.BossType(i));boss.whoAmI=0;boss.position=p.position;boss.active=true;boss.playerInteraction[0]=true;Main.npc[0]=boss;
            resolver.TryDropping(new DropAttemptInfo{npc=boss,player=p,rng=new UnifiedRandom(117000+i),IsExpertMode=mode>0,IsMasterMode=mode==2});
            if(mode==0)
            {
                Check(Amount(PantheonCatalog.Material(i)) is >=36 and <=44&&Amount(PantheonCatalog.Core(i))==2,"native normal resources "+i);
                Check(PantheonLoot.Weapons(i).Sum(Amount)==1,"native exactly one weapon "+i);
                Check(Amount(PantheonCatalog.Bag(i))==0&&Amount(PantheonCatalog.Expert(i))==0&&Amount(PantheonCatalog.Relic(i))==0,"normal excludes expert/master "+i);
            }
            else
            {
                Check(Amount(PantheonCatalog.Bag(i))==1,"one native bag per player "+i+"/"+mode);
                Check(Amount(PantheonCatalog.Material(i))==0&&Amount(PantheonCatalog.Core(i))==0&&Amount(PantheonCatalog.Expert(i))==0&&Amount(PantheonCatalog.Mask(i))==0&&PantheonLoot.Weapons(i).Sum(Amount)==0,"no loose expert combat loot "+i);
                Check(Amount(PantheonCatalog.Relic(i))==(mode==2?1:0),"native guaranteed master relic "+i);
                ClearItems();resolver.TryDropping(new DropAttemptInfo{item=PantheonCatalog.Bag(i),player=p,rng=new UnifiedRandom(118000+i),IsExpertMode=true,IsMasterMode=mode==2});
                Check(Amount(PantheonCatalog.Material(i)) is >=44 and <=56&&Amount(PantheonCatalog.Core(i))==2&&Amount(PantheonCatalog.Expert(i))==1,"native bag resources/expert "+i);
                Check(PantheonLoot.Weapons(i).Sum(Amount)==1,"native bag exactly one weapon "+i);
            }
            ClearItems();if(i==0)Voyage.VoyageWorld.Downed[16]=false;else PantheonWorld.Downed[i-1]=false;
            resolver.TryDropping(new DropAttemptInfo{npc=boss,player=p,rng=new UnifiedRandom(119000+i),IsExpertMode=mode>0,IsMasterMode=mode==2});
            Check(!Main.item.Any(x=>x.active),"forced locked kill has no loot "+i+"/"+mode);
            if(i==0)Voyage.VoyageWorld.Downed[16]=true;else PantheonWorld.Downed[i-1]=true;
            PantheonWorld.Downed[i]=false;boss.life=1;PantheonLoot.OnVictory(boss,i);Check(!PantheonWorld.IsDowned(i),"live NPC cannot award victory "+i);
            boss.life=0;PantheonLoot.OnVictory(boss,i);Check(PantheonWorld.IsDowned(i),"actual victory unlock "+i);
            PantheonLoot.OnVictory(boss,i);Check(!Main.item.Any(x=>x.active),"victory callback cannot duplicate table loot "+i);boss.active=false;
        }
        Main.GameMode=0;ClearNPCs();Log("PANTHEON_LOOT_RUNTIME_PASS normal=17 expertBags=34 lockedRolls=51 masterRelics=17 victoryFlags=17 noDuplicateLooseLoot=true");
    }
}
