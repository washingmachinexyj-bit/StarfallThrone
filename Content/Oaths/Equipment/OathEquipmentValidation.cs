#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.Oaths.Support;

namespace StarfallThrone.Content.Oaths.Equipment;

// Invoked by main's isolated opt-in harness, never an ordinary world update.
public static class OathEquipmentValidation
{
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Oath equipment: "+why);checks++;}
    static void Log(string text)=>ModContent.GetInstance<OathEquipmentKeys>().Mod.Logger.Info(text);
    static void Guard()=>Check(Main.gameMenu&&Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke")||Main.dedServ&&Terraria.Program.LaunchParameters.ContainsKey("-testservermodloading")&&Terraria.Program.LaunchParameters.Keys.Any(k=>k.StartsWith("-starfall-")&&k.EndsWith("-headless")),"isolated opt-in test only");
    static Recipe RecipeFor(int item)=>Main.recipe.Take(Recipe.numRecipes).Single(r=>!r.Disabled&&r.createItem.type==item);
    static List<DropRateInfo> Rates(IEnumerable<IItemDropRule> rules){var list=new List<DropRateInfo>();foreach(var rule in rules)rule.ReportDroprates(list,new DropRateInfoChainFeed(1));return list;}
    public static void Data()
    {
        Guard();checks=0;
        for(int i=0;i<12;i++)
        {
            var item=new Item(OathCatalog.Weapon(i));Check(item.ModItem is SupremeWeaponBase&&item.damage==SupremeArsenal.Damage[i]&&item.DamageType==SupremeArsenal.Class(i),"weapon class/damage "+i);
            Check(item.useTime==SupremeArsenal.Speed[i]&&item.noMelee,"native attack cadence "+i);
            if(i%4==1)Check(item.useAmmo!=AmmoID.None,"native ammo "+i);
            if(i%4>=2)Check(item.mana>0,"native mana "+i);
            Recipe r=RecipeFor(item.type);Check(r.requiredItem.Count==2&&r.requiredItem.Any(x=>x.type==OathCatalog.Material(i/4)&&x.stack==12)&&r.requiredItem.Any(x=>x.type==OathCatalog.Bar(i/4)&&x.stack==6)&&r.requiredTile.SequenceEqual(new[]{OathCatalog.Station(i/4)}),"independent weapon crafting "+i);
            if(i%4==3){Projectile q=new();q.SetDefaults(item.shoot);Check(q.minion&&q.minionSlots==(i==11?4:2)&&q.DamageType==DamageClass.Summon&&ProjectileID.Sets.MinionSacrificable[q.type],"native minion slots "+i);}
        }
        for(int head=0;head<4;head++)
        {
            Player p=new();p.ResetEffects();int life=p.statLifeMax2,defense=0;
            for(int slot=0;slot<3;slot++){int index=slot==0?head:slot+3;Item item=new(OathSupport.Item("SupremeArmor"+index));p.armor[slot]=item;item.ModItem.UpdateEquip(p);defense+=item.defense;Check(slot==0?item.headSlot>=0:slot==1?item.bodySlot>=0:item.legSlot>=0,"native equip slot "+index);
                Recipe r=RecipeFor(item.type);Check(r.requiredItem.Count==6&&Enumerable.Range(0,3).All(i=>r.requiredItem.Any(x=>x.type==OathCatalog.Material(i))&&r.requiredItem.Any(x=>x.type==OathCatalog.Bar(i)))&&r.requiredTile.Contains(ModContent.TileType<TriuneAltarTile>()),"all-three ore armor "+index);}
            Check(p.statLifeMax2-life==4200&&defense==new[]{850,765,697,663}[head],"armor totals "+head);
            Check(p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"matching set "+head);p.armor[2]=new Item(ItemID.WoodGreaves);Check(!p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"mixed set rejected "+head);
        }
        for(int i=0;i<3;i++)
        {
            Item expert=new(OathSupport.Item("SupremeExpert"+i)),summon=new(OathSupport.Item("SupremeSummon"+i)),bag=new(OathSupport.Item("SupremeBag"+i));
            Check(expert.expert&&expert.accessory&&!expert.ModItem.CanAccessoryBeEquippedWith(expert,expert,new Player()),"expert mutual exclusion "+i);
            Check(!summon.consumable&&summon.maxStack==1,"reusable summon "+i);Recipe r=RecipeFor(summon.type);
            Check(r.requiredItem.Count==4&&r.requiredItem.Any(x=>x.type==OathSupport.Item("SeedToken"+i))&&r.requiredItem.Any(x=>x.type==OathSupport.Item("DivineMaterial"+i)&&x.stack==20)&&r.requiredItem.Any(x=>x.type==OathSupport.Item("AscendantMaterial"+i)&&x.stack==15)&&r.requiredItem.Any(x=>x.type==OathSupport.Item("PantheonMaterial16")&&x.stack==12)&&r.Conditions.Count>0,"summon world gate/materials "+i);
            Check(bag.expert&&bag.ModItem.CanRightClick()&&ItemID.Sets.BossBag[bag.type],"expert openable bag "+i);
            var rates=Rates(Main.ItemDropsDB.GetRulesForItemID(bag.type));
            Check(rates.Any(x=>x.itemId==expert.type&&x.dropRate>=.999f),"guaranteed expert "+i);
            Check(rates.Any(x=>x.itemId==OathCatalog.Material(i)&&x.stackMin==40&&x.stackMax==55),"bag material range "+i);
            Check(SupremeLoot.Weapons(i).All(w=>rates.Any(x=>x.itemId==w&&Math.Abs(x.dropRate-.25f)<.0001f)),"one of four equal weapons "+i);
            Check(!rates.Any(x=>x.itemId==OathSupport.Item("SupremeRelic"+i)||x.itemId==OathSupport.Item("SupremeTrophy"+i)),"external collectibles not bag "+i);
            Item relic=new(OathSupport.Item("SupremeRelic"+i)),pet=new(OathSupport.Item("SupremePetItem"+i)),mask=new(OathSupport.Item("SupremeMask"+i));Check(relic.master&&pet.master&&mask.vanity&&mask.headSlot>=0,"native collectibles "+i);
            foreach(string prefix in new[]{"SupremeRelicTile","SupremeTrophyTile"}){int tile=ModContent.Find<ModTile>("StarfallThrone",prefix+i).Type;var d=TileObjectData.GetTileData(tile,0);Check(d.Width==3&&d.Height==3&&d.CoordinatePadding==2,"3x3 native display atlas "+prefix+i);Check(TileLoader.GetItemDropFromTypeAndStyle(tile,0)==OathSupport.Item(prefix.Replace("Tile","")+i),"display item recovery "+i);}
        }
        Recipe altar=RecipeFor(ModContent.ItemType<TriuneAltarItem>());Check(altar.requiredItem.Count==3&&!altar.requiredItem.Any(x=>Enumerable.Range(0,3).Any(i=>x.type==OathCatalog.Material(i)||x.type==OathCatalog.Bar(i))),"noncircular altar");
        var culture=Language.ActiveCulture;
        try{foreach(string lang in new[]{"zh-Hans","en-US"}){LanguageManager.Instance.SetLanguage(GameCulture.FromName(lang));foreach(ModItem item in ModContent.GetInstance<OathEquipmentKeys>().Mod.GetContent<ModItem>().Where(i=>i is SupremeWeaponBase or SupremeArmorBase or SupremeExpertBase or SupremeBagBase or SupremeSummonBase or SupremeMaterialBase or SupremePetItemBase or SupremeMaskBase or SupremeRelicBase or SupremeTrophyBase or TriuneAltarItem))Check(item.DisplayName.Value!=item.Name&&item.Tooltip.Value.Length>12,"bilingual item "+lang+"/"+item.Name);Check(OathSupport.Text("ArmorSet").Length>20,"localized armor bonus "+lang);}}finally{LanguageManager.Instance.SetLanguage(culture);}
        var state=new Player().GetModPlayer<OathEquipmentPlayer>();state.Cooldowns[0]=1700;state.Cooldowns[1]=1400;state.Cooldowns[2]=2600;state.CommandCooldown=500;
        TagCompound tag=new();state.SaveData(tag);var copy=new Player().GetModPlayer<OathEquipmentPlayer>();copy.LoadData(tag);copy.OnEnterWorld();copy.ResetEffects();Check(copy.Cooldowns.SequenceEqual(state.Cooldowns)&&copy.CommandCooldown==500,"persistent cooldowns");
        Log("OATH_EQUIPMENT_DATA_PASS checks="+checks+" weapons=12 armor=6 experts=3 bags=3 summons=3 nativeSlots=true recipes=true bilingual=true");
    }
    public static void Run()
    {
        Guard();
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var projectiles=(Projectile[])Main.projectile.Clone();var identities=(int[,])Main.projectileIdentity.Clone();var random=Main.rand;
        int mode=Main.netMode,me=Main.myPlayer,mx=Main.mouseX,my=Main.mouseY,maxX=Main.maxTilesX,maxY=Main.maxTilesY;Vector2 screen=Main.screenPosition;
        var tiles=new List<(int X,int Y,bool Has,ushort Type)>();
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.rand=new Terraria.Utilities.UnifiedRandom(130613);
            Main.maxTilesX=Math.Min((int)Main.tile.Width,Math.Max(1024,maxX));Main.maxTilesY=Math.Min((int)Main.tile.Height,Math.Max(640,maxY));
            for(int i=0;i<Main.maxPlayers;i++)Main.player[i]=new Player{active=false,whoAmI=i};
            for(int i=0;i<Main.maxNPCs;i++)Main.npc[i]=new NPC{active=false,whoAmI=i};
            for(int i=0;i<Main.maxProjectiles;i++)Main.projectile[i]=new Projectile{active=false,whoAmI=i};
            Player p=new(){active=true,whoAmI=0,position=new Vector2(18000,1000),selectedItem=0};p.ResetEffects();p.statLife=p.statLifeMax2=5000;p.statMana=p.statManaMax2=2000;Main.player[0]=p;
            var state=p.GetModPlayer<OathEquipmentPlayer>();state.Expert[0]=true;
            NPC target=new(){active=true,whoAmI=0,boss=true,life=10000000,lifeMax=10000000,width=44,height=80,position=p.Center+new Vector2(100,-40),damage=100};Main.npc[0]=target;
            int before=p.statLifeMax2;state.PostUpdateEquips();Check(p.statLifeMax2==(int)(before*1.15f),"15 percent max life");
            for(int n=0;n<7;n++)state.ChargeAt(target,1000,(ulong)(n*30));Check(state.Reservoir==(int)(p.statLifeMax2*.12f),"bounded reservoir charges");
            int reserve=state.Reservoir;target.type=NPCID.TargetDummy;state.ChargeAt(target,1000,240);Check(state.Reservoir==reserve,"dummy cannot charge");target.type=NPCID.BlueSlime;
            int hurtsLife=p.statLife;state.OnHurt(new Player.HurtInfo{Damage=1000,PvP=true});Check(p.statLife==hurtsLife,"PvP cannot trigger heal");
            state.Cooldowns[0]=200;state.Cooldowns[1]=300;state.Cooldowns[2]=400;state.ResetEffects();state.UpdateDead();Check(state.Cooldowns.SequenceEqual(new[]{199,299,399}),"death preserves countdowns");
            // Atomic malformed/forged requests never teleport or shorten any cooldown.
            Vector2 position=p.position;Main.netMode=NetmodeID.Server;state.Expert[2]=true;
            foreach(byte[] bytes in new[]{new byte[]{0},new byte[]{0,0,99},new byte[]{2,0,0},new byte[]{255,0}}){using var stream=new MemoryStream(bytes);using var reader=new BinaryReader(stream);OathEquipmentPlayer.ReceivePacket(reader,0);}
            using(var stream=new MemoryStream(new byte[]{0,0})){using var reader=new BinaryReader(stream);OathEquipmentPlayer.ReceivePacket(reader,1);}
            Check(p.position==position&&state.Cooldowns[2]==399,"malformed and spoofed rewind rejected");Main.netMode=NetmodeID.SinglePlayer;
            Check(!OathEquipmentPlayer.SafeRewind(p,new Vector2(float.NaN,0))&&!OathEquipmentPlayer.SafeRewind(p,p.position+new Vector2(2000,0)),"nonfinite and excessive rewind rejected");
            Check(!OathEquipmentPlayer.SafeRewind(p,p.position),"non-Supreme boss rewind rejected");target.active=false;
            for(int x=185;x<210;x++)for(int y=55;y<70;y++){Tile tile=Main.tile[x,y];tiles.Add((x,y,tile.HasTile,tile.TileType));tile.HasTile=false;}
            p.position=new Vector2(3000,960);state.OnEnterWorld();state.Expert[2]=true;state.Cooldowns[2]=0;
            for(int n=0;n<=120;n++){p.position=new Vector2(3000+n,960);state.RecordPositionAt((ulong)n);}
            int lifeBefore=p.statLife,manaBefore=p.statMana;state.Cooldowns[0]=700;state.Cooldowns[1]=800;
            Check(state.TryRewind()&&p.position==new Vector2(3000,960),"native rewind returns to 120-tick history");
            Check(p.statLife==lifeBefore&&p.statMana==manaBefore&&state.Cooldowns.SequenceEqual(new[]{700,800,2700}),"rewind does not heal or reset cooldowns");
            Check(!state.TryRewind(),"rewind cannot repeat on same history");
            Tile obstruction=Main.tile[191,60];obstruction.HasTile=true;obstruction.TileType=TileID.Stone;
            Check(!OathEquipmentPlayer.SafeRewind(p,p.position+new Vector2(120,0)),"swept player box rejects solid crossing");obstruction.HasTile=false;
            // Native defaults and actual AI transitions, independent of damage/defense balance.
            for(int id=0;id<12;id++)
            {
                for(int j=0;j<Main.maxProjectiles;j++)Main.projectile[j].active=false;
                p.inventory[0]=new Item(OathCatalog.Weapon(id));p.itemAnimationMax=SupremeArsenal.Speed[id];
                Projectile q=new(){owner=0,active=true,whoAmI=0};q.SetDefaults(id%4==3?p.HeldItem.shoot:ModContent.ProjectileType<SupremeShot>());q.owner=0;q.active=true;q.whoAmI=0;q.Center=p.Center;q.velocity=Vector2.UnitX*24;q.damage=p.HeldItem.damage;q.ai[0]=id;q.ai[1]=id%4==0?0:1;Main.projectile[0]=q;
                if(q.ModProjectile is SupremeShot shot){shot.Configure();for(int t=0;t<20&&q.active;t++){shot.AI();if(shot.ShouldUpdatePosition())q.position+=q.velocity;}Check(q.DamageType==SupremeArsenal.Class(id),"shot class after AI "+id);}
                else{q.ai[0]=q.ai[1]=0;p.AddBuff(p.HeldItem.buffType,3600);for(int t=0;t<3;t++)q.ModProjectile.AI();Check(q.timeLeft==2&&!q.ModProjectile.MinionContactDamage(),"persistent noncontact minion "+id);}
            }
            Log("OATH_EQUIPMENT_RUN_PASS reservoir=true cooldownPersistence=true packetAtomicity=true nativeRewind=true rewindGuards=true weaponAI=12");
        }
        finally{foreach(var saved in tiles){Tile tile=Main.tile[saved.X,saved.Y];tile.HasTile=saved.Has;tile.TileType=saved.Type;}Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(projectiles,Main.projectile,projectiles.Length);Array.Copy(identities,Main.projectileIdentity,identities.Length);Main.rand=random;Main.netMode=mode;Main.myPlayer=me;Main.mouseX=mx;Main.mouseY=my;Main.screenPosition=screen;Main.maxTilesX=maxX;Main.maxTilesY=maxY;}
    }
}
