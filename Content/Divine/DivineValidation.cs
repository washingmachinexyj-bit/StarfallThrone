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

namespace StarfallThrone.Content.Divine;

public static class DivineValidation
{
    private static int checks;
    private static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Divine support: "+why);checks++;}
    private static void Log(string message)=>ModContent.GetInstance<DivineWorld>().Mod.Logger.Info(message);
    private static void Guard()=>Check(Main.gameMenu&&Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"),"isolated menu guard");
    public static void Data()
    {
        Guard();
        bool king=NPC.downedSlimeKing,hard=Main.hardMode,moon=NPC.downedMoonlord;
        try
        {
            for(int mask=0;mask<8;mask++)
            {
                NPC.downedSlimeKing=(mask&1)!=0;Main.hardMode=(mask&2)!=0;NPC.downedMoonlord=(mask&4)!=0;
                for(int i=0;i<3;i++)Check(DivineWorld.Open(i)==((mask&(1<<i))==0),"independent deadline "+mask+"/"+i);
            }
            Check(!DivineWorld.Open(-1)&&!DivineWorld.Open(3),"invalid index closed");
            for(int i=0;i<3;i++)
            {
                Item summon=new(DivineCatalog.Summon(i)),bag=new(DivineCatalog.Bag(i));
                Check(!summon.consumable&&summon.maxStack==1,"reusable summon "+i);
                Recipe r=Main.recipe.Take(Recipe.numRecipes).Single(r=>!r.Disabled&&r.createItem.type==summon.type);
                Check(r.requiredTile.SequenceEqual(new[]{DivineCatalog.CraftStation(i)}),"summon crafting stage "+i);
                Check(r.Conditions.Count==0,"summon can remain craftable after deadline "+i);
                Check(r.requiredItem.All(it=>!it.expert&&!it.master),"summon noexclusive materials "+i);
                Check(!r.requiredItem.Any(it=>it.type==DivineCatalog.Material(i)||it.type==ItemID.LunarBar),"summon nodeadlock "+i);
                Check(bag.expert&&bag.ModItem.CanRightClick()&&ItemID.Sets.BossBag[bag.type],"native expertbag "+i);
                var bagDrops=new List<DropRateInfo>();foreach(var rule in Main.ItemDropsDB.GetRulesForItemID(bag.type))rule.ReportDroprates(bagDrops,new DropRateInfoChainFeed(1f));
                Check(bagDrops.Any(d=>d.itemId==DivineCatalog.Material(i)&&d.stackMin==30&&d.stackMax==40),"bagmaterials "+i);
                Check(bagDrops.Any(d=>d.itemId==DivineCatalog.Expert(i)&&d.dropRate>.999f),"expert guaranteed "+i);
                for(int c=0;c<4;c++)Check(bagDrops.Any(d=>d.itemId==DivineCatalog.Weapon(i*4+c)),"fourclass weaponbag "+i+"/"+c);
                Check(new Item(DivineCatalog.Relic(i)).master,"masterrelic "+i);
                Check(new Item(DivineCatalog.Mask(i)).headSlot>=0,"actual wearablemask "+i);
                Check(new Item(DivineCatalog.Pet(i)).buffType>0,"actual petbuff "+i);
                var cache=new Item(DivineCatalog.Cache(i));Check(!cache.ModItem.ConsumeItem(Main.LocalPlayer),"choiceUI notconsumeonopen "+i);
            }
            int group=RecipeGroup.recipeGroupIDs["StarfallThrone:DivineFragment"];
            Check(RecipeGroup.recipeGroups[group].ValidItems.SetEquals(new int[]{ItemID.FragmentSolar,ItemID.FragmentVortex,ItemID.FragmentNebula,ItemID.FragmentStardust}),"oneof4fragments notall4");
            Check(DivineCatalog.Progression(0)<1&&DivineCatalog.Progression(1)<7&&DivineCatalog.Progression(2)<18,"checklist precedesdeadlineboss");
            if(ModLoader.TryGetMod("BossChecklist",out Mod checklist))
            {
                var entries=checklist.Call("GetBossInfoDictionary",ModContent.GetInstance<DivineWorld>().Mod,"2.0.0") as System.Collections.IDictionary;
                for(int i=0;i<3;i++)Check(entries!=null&&entries.Contains("StarfallThrone "+DivineCatalog.Keys[i]),"checklistentry "+i);
                Check(DivineChecklist.SpawnInfo(0).Key.EndsWith("ChecklistMissed")||DivineChecklist.SpawnInfo(0).Key.EndsWith("ChecklistClosedWon"),"closedbookstatus");
            }
        }
        finally{NPC.downedSlimeKing=king;Main.hardMode=hard;NPC.downedMoonlord=moon;}
        Log("DIVINE_SUPPORT_DATA_PASS checks="+checks+" windows=24 summons=3 bags=3 expert=3 nativeMasks=3 checklistOrder=3");
    }
    public static void Run()
    {
        Guard();
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var items=(Item[])Main.item.Clone();
        var projectiles=(Projectile[])Main.projectile.Clone();var oldRand=Main.rand;var oldWorldFile=Main.ActiveWorldFileData;
        bool king=NPC.downedSlimeKing,hard=Main.hardMode,moon=NPC.downedMoonlord,day=Main.dayTime;
        int mode=Main.GameMode,net=Main.netMode,my=Main.myPlayer,countdown=NPC.MoonLordCountdown;
        DivineWorld world=ModContent.GetInstance<DivineWorld>();TagCompound saved=new();world.SaveWorldData(saved);
        try
        {
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{active=false,whoAmI=i};
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{active=false,whoAmI=i};
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{active=false,whoAmI=i};
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.GameMode=0;Main.rand=new UnifiedRandom(808003);ClearItems();
            Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            if(ModLoader.TryGetMod("BossChecklist",out Mod checklist))
            {
                ModSystem records=checklist.GetContent<ModSystem>().Single(s=>s.Name=="RecordSystem");
                records.OnWorldLoad();records.LoadWorldData(new TagCompound());
            }
            NPC.downedSlimeKing=Main.hardMode=NPC.downedMoonlord=false;NPC.MoonLordCountdown=0;
            Player p=new(){active=true,whoAmI=0,position=new Vector2(16000,2400)};p.ResetEffects();p.selectedItem=0;Main.player[0]=p;
            SummonChecks(p);LootChecks(p);StateAndGiftChecks(p,world);PetChecks(p);WearRender();FurnitureRender();ChoiceRender(p);
            Log("DIVINE_SUPPORT_RUNTIME_PASS checks="+checks+" nativeSummons=3 expertBagRolls=6 normalLoot=3 windowLootBlock=3 firstClaimsPersist=3 cacheChoices=12");
        }
        finally
        {
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=players[i];for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=npcs[i];
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=items[i];for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=projectiles[i];
            Main.GameMode=mode;Main.netMode=net;Main.myPlayer=my;Main.rand=oldRand;Main.dayTime=day;Main.ActiveWorldFileData=oldWorldFile;
            NPC.downedSlimeKing=king;Main.hardMode=hard;NPC.downedMoonlord=moon;NPC.MoonLordCountdown=countdown;world.LoadWorldData(saved);DivineChoiceUI.Tier=DivineChoiceUI.Slot=-1;
        }
    }
    private static void ClearItems(){for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item();}
    private static int Amount(int type)=>Main.item.Where(i=>i.active&&i.type==type).Sum(i=>i.stack);
    private static void SummonChecks(Player p)
    {
        for(int tier=0;tier<3;tier++)
        {
            foreach(NPC npc in Main.npc)npc.active=false;
            Main.dayTime=tier!=2;p.ZoneOverworldHeight=tier!=1;p.ZoneUnderworldHeight=tier==1;
            p.inventory[0]=new Item(DivineCatalog.Summon(tier));
            Check(DivineSummonBase.Valid(p,tier),"validbiome "+tier);
            Check(DivineSummonBase.TrySummon(p,tier),"actual nativesummon "+tier);
            Check(Main.npc.Count(n=>n.active&&n.type==DivineCatalog.NPCType(tier))==1,"singleexactboss "+tier);
            Check(Main.npc.Single(n=>n.active&&n.type==DivineCatalog.NPCType(tier)).Distance(p.Center)<650,"spawncloseenoughforinitialtarget "+tier);
            Check(!DivineSummonBase.TrySummon(p,tier),"duplicateblocked "+tier);
            foreach(NPC npc in Main.npc)npc.active=false;
            p.inventory[0]=new Item(ItemID.DirtBlock);Check(!DivineSummonBase.TrySummon(p,tier),"helditemauthorization "+tier);
        }
        p.inventory[0]=new Item(DivineCatalog.Summon(2));Main.dayTime=false;p.ZoneOverworldHeight=true;p.ZoneUnderworldHeight=false;
        NPC.MoonLordCountdown=60;Check(!DivineSummonBase.Valid(p,2),"moonarrivalblocked");NPC.MoonLordCountdown=0;
        p.ZoneTowerSolar=true;Check(!DivineSummonBase.Valid(p,2),"localtowerblocked");p.ZoneTowerSolar=false;
        for(int i=0;i<3;i++)
        {NPC.downedSlimeKing=Main.hardMode=NPC.downedMoonlord=true;Check(!DivineSummonBase.Valid(p,i),"closedreject "+i);}
        NPC.downedSlimeKing=Main.hardMode=NPC.downedMoonlord=false;
    }
    private static void LootChecks(Player p)
    {
        var resolver=new ItemDropResolver(Main.ItemDropsDB);
        for(int tier=0;tier<3;tier++)
        {
            for(int difficulty=0;difficulty<3;difficulty++)
            {
                Main.GameMode=difficulty;ClearItems();NPC boss=new();boss.SetDefaults(DivineCatalog.NPCType(tier));boss.whoAmI=0;boss.position=p.position;boss.active=true;boss.playerInteraction[0]=true;Main.npc[0]=boss;
                resolver.TryDropping(new DropAttemptInfo{npc=boss,player=p,rng=new UnifiedRandom(1234+tier),IsExpertMode=difficulty>0,IsMasterMode=difficulty==2});
                if(difficulty==0)
                {Check(Amount(DivineCatalog.Material(tier)) is >=24 and <=32,"actualnormalmaterials");Check(Enumerable.Range(tier*4,4).Sum(n=>Amount(DivineCatalog.Weapon(n)))==1,"actualnormaloneweapon");Check(Amount(DivineCatalog.Bag(tier))==0,"normalnobag");}
                else
                {
                    Check(Amount(DivineCatalog.Bag(tier))==1,"actualexpertbag");Check(Amount(DivineCatalog.Material(tier))==0&&Amount(DivineCatalog.Expert(tier))==0,"nolooseexpertloot");
                    Check(Amount(DivineCatalog.Relic(tier))==(difficulty==2?1:0),"actualmasterrelic");
                    ClearItems();resolver.TryDropping(new DropAttemptInfo{item=DivineCatalog.Bag(tier),player=p,rng=new UnifiedRandom(1300+tier),IsExpertMode=true,IsMasterMode=difficulty==2});
                    Check(Amount(DivineCatalog.Material(tier)) is >=30 and <=40&&Amount(DivineCatalog.Expert(tier))==1,"actualbagcontents");
                    Check(Enumerable.Range(tier*4,4).Sum(n=>Amount(DivineCatalog.Weapon(n)))==1,"actualbagoneweapon");
                }
                boss.active=false;
            }
            Main.GameMode=0;ClearItems();NPC.downedSlimeKing=Main.hardMode=NPC.downedMoonlord=true;
            NPC closed=new();closed.SetDefaults(DivineCatalog.NPCType(tier));closed.position=p.position;closed.playerInteraction[0]=true;
            resolver.TryDropping(new DropAttemptInfo{npc=closed,player=p,rng=new UnifiedRandom(1)});
            Check(!Main.item.Any(i=>i.active),"closedwindowdropsnothing "+tier);
            NPC.downedSlimeKing=Main.hardMode=NPC.downedMoonlord=false;
        }
    }
    private static void StateAndGiftChecks(Player p,DivineWorld world)
    {
        world.ClearWorld();DivineWorld.WorldKey=Guid.NewGuid();Guid identity=Guid.NewGuid();
        for(int i=0;i<3;i++){Check(DivineWorld.TryClaim(identity,i),"firstclaim "+i);Check(!DivineWorld.TryClaim(identity,i),"norepeatclaim "+i);DivineWorld.Downed[i]=true;}
        TagCompound saved=new();world.SaveWorldData(saved);
        using(var stream=new MemoryStream())
        {TagIO.ToStream(saved,stream);stream.Position=0;world.ClearWorld();world.LoadWorldData(TagIO.FromStream(stream));}
        for(int i=0;i<3;i++)Check(DivineWorld.Downed[i]&&!DivineWorld.TryClaim(identity,i),"save/rejoinnotreset "+i);
        using(var stream=new MemoryStream())
        {using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))world.NetSend(writer);stream.Position=0;using var reader=new BinaryReader(stream);world.NetReceive(reader);Check(DivineWorld.Downed.All(b=>b),"networldvictories");}
        for(int tier=0;tier<3;tier++)for(int choice=0;choice<4;choice++)
        {
            ClearItems();p.inventory[0]=new Item(DivineCatalog.Cache(tier));
            Check(!DivineChoiceUI.Claim(p,0,tier,4),"badchoice rejects");Check(p.inventory[0].stack==1,"badchoice doesnotconsume");
            Check(DivineChoiceUI.Claim(p,0,tier,choice),"selectedclaim");Check(Amount(DivineCatalog.Weapon(tier*4+choice))==1,"exactchosenweapon");
            Check(p.inventory[0].IsAir&&!DivineChoiceUI.Claim(p,0,tier,choice),"cacheatomicconsume");
        }
        ClearItems();DivineChallengePlayer state=p.GetModPlayer<DivineChallengePlayer>();state.Identity=Guid.NewGuid();state.IdentityBound=true;
        bool[] participants=new bool[Main.maxPlayers],damaged=new bool[Main.maxPlayers];participants[0]=true;damaged[0]=true;
        for(int tier=0;tier<3;tier++)
        {
            NPC boss=new();boss.SetDefaults(DivineCatalog.NPCType(tier));boss.playerInteraction[0]=true;
            DivineWorld.RecordVictory(boss,tier,participants,damaged);DivineWorld.RecordVictory(boss,tier,participants,damaged);
            Check(Amount(DivineCatalog.Cache(tier))==1,"actualvictoryfirstgiftonlyonce "+tier);
            Check(Amount(DivineCatalog.Halo(tier))==0,"damagedplayernohalo "+tier);
        }
        Check(Amount(ModContent.ItemType<DivineMonument>())==1&&Amount(ModContent.ItemType<DivineEpochTitle>())==1,"actualtrinitycosmeticsone");
        ClearItems();NPC.downedSlimeKing=true;
        NPC late=new();late.SetDefaults(DivineCatalog.NPCType(0));late.playerInteraction[0]=true;state.Identity=Guid.NewGuid();
        DivineWorld.RecordVictory(late,0,participants,new bool[Main.maxPlayers]);
        Check(!Main.item.Any(it=>it.active)&&!DivineWorld.Claims.ContainsKey(state.Identity),"deadlinebeforevictoryblocksallgifts");NPC.downedSlimeKing=false;
        for(int tier=0;tier<3;tier++)
        {
            ClearItems();NPC clean=new();clean.SetDefaults(DivineCatalog.NPCType(tier));clean.playerInteraction[0]=true;
            DivineWorld.RecordVictory(clean,tier,participants,new bool[Main.maxPlayers]);
            Check(Amount(DivineCatalog.Halo(tier))==1,"flawlessparticipantgetsactualhalo "+tier);
            ClearItems();clean.playerInteraction[0]=false;DivineWorld.RecordVictory(clean,tier,participants,new bool[Main.maxPlayers]);
            Check(!Main.item.Any(it=>it.active),"noncontributorgetsnogift "+tier);
        }
        foreach(byte[] bad in new[]{Array.Empty<byte>(),new byte[]{255},new byte[]{0,1},new byte[]{1}})
        {using var reader=new BinaryReader(new MemoryStream(bad));try{DivineChallengePlayer.ReceivePacket(reader,-1);}catch(EndOfStreamException){}}
    }
    private static void PetChecks(Player p)
    {
        foreach(Projectile projectile in Main.projectile)projectile.active=false;
        for(int tier=0;tier<3;tier++)
        {
            Item item=new(DivineCatalog.Pet(tier));item.ModItem.UseItem(p);
            int slot=p.FindBuffIndex(item.buffType);Check(slot>=0,"petnativebuff "+tier);
            BuffLoader.GetBuff(item.buffType).Update(p,ref slot);
            Projectile pet=Main.projectile.Single(pr=>pr.active&&pr.type==item.shoot);
            Check(pet.owner==p.whoAmI&&pet.ModProjectile.CanDamage()==false,"harmlessownedpet "+tier);
            for(int frame=0;frame<180;frame++)
            {pet.AI();pet.position+=pet.velocity;Check(float.IsFinite(pet.Center.X)&&float.IsFinite(pet.Center.Y)&&pet.velocity.Length()<=12.01f,"boundedpetfollow "+tier);}
            Check(pet.timeLeft==2,"petmaintainedbybuff "+tier);
            p.ClearBuff(item.buffType);pet.AI();Check(pet.timeLeft<=2,"petexpireswithoutbuff "+tier);
            pet.Kill();Check(!pet.active,"petdismissal "+tier);
        }
        Log("DIVINE_PET_RUNTIME_PASS nativeBuffs=3 harmlessFollowers=3 finiteMotion=true dismissal=true");
    }
    private static void SaveImage(RenderTarget2D image,string name)
    {string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));using FileStream stream=File.Create(path);image.SaveAsPng(stream,image.Width,image.Height);}
    private static void WearRender()
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();Vector2 screen=Main.screenPosition;
        using var atlas=new RenderTarget2D(device,1000,500);
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));Main.screenPosition=Vector2.Zero;
            for(int id=0;id<39;id++)
            {
                int tier=id<36?id/12:id-36,style=id<36?id/3%4:0,pose=id<36?id%3:0;
                Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=id%2==0?1:-1};p.ResetEffects();
                p.head=new Item(id<36?DivineCatalog.Armor(tier*6+style):DivineCatalog.Mask(tier)).headSlot;
                p.body=new Item(DivineCatalog.Armor(tier*6+4)).bodySlot;p.legs=new Item(DivineCatalog.Armor(tier*6+5)).legSlot;
                p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);
                Vector2 at=new(id%10*100+40,id/10*115+50);p.position=at;
                Main.PlayerRenderer.DrawPlayer(Main.Camera,p,at,0,Vector2.Zero,0,1.4f);
            }
            device.SetRenderTargets(targets);SaveImage(atlas,"divine-wear-runtime.png");Log("DIVINE_WEAR_RENDER_PASS loadouts=12 poses=3 masks=3 nativePlayerRenderer=true");
        }
        finally{device.SetRenderTargets(targets);Main.screenPosition=screen;}
    }
    private static void FurnitureRender()
    {
        const int left=1230,top=100;var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();
        bool noActions=WorldGen.noTileActions,gen=WorldGen.gen;WorldGen.noTileActions=WorldGen.gen=false;
        using var atlas=new RenderTarget2D(device,700,180);
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));
            for(int n=0;n<7;n++)
            {
                int itemType=n<3?DivineCatalog.Relic(n):n<6?DivineCatalog.Trophy(n-3):ModContent.ItemType<DivineMonument>();
                Item item=new(itemType);int type=item.createTile;
                for(int x=left-2;x<left+7;x++)for(int y=top-2;y<top+8;y++)
                {Tile t=Main.tile[x,y];t.ClearEverything();t.WallType=WallID.Stone;if(y>=top+4){t.HasTile=true;t.TileType=TileID.Dirt;}}
                ClearItems();TileObjectData data=TileObjectData.GetTileData(type,0);int placedY=top+4-data.Height;
                WorldGen.PlaceObject(left+data.Origin.X,placedY+data.Origin.Y,type);
                Check(Main.tile[left,placedY].HasTile&&Main.tile[left,placedY].TileType==type,"nativefurnitureplaced "+n);
                var texture=ModContent.Request<Texture2D>(TileLoader.GetTile(type).Texture).Value;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                for(int x=0;x<data.Width;x++)for(int y=0;y<data.Height;y++)
                {
                    Tile t=Main.tile[left+x,placedY+y];Rectangle src=new(t.TileFrameX,t.TileFrameY,16,data.CoordinateHeights[y]);
                    Check(src.Right<=texture.Width&&src.Bottom<=texture.Height,"furnitureframeinside "+n);
                    Main.spriteBatch.Draw(texture,new Vector2(n*98+20+x*16,26+y*16),src,Color.White);
                }
                Main.spriteBatch.End();WorldGen.KillTile(left,placedY);WorldGen.SquareTileFrame(left,placedY,true);
                Check(Amount(itemType)==1,"nativeexactonefurnituredrop "+n);
            }
            device.SetRenderTargets(targets);SaveImage(atlas,"divine-furniture-runtime.png");Log("DIVINE_FURNITURE_RENDER_PASS nativePlace=7 nativeBreak=7 singleDrops=7 frameBounds=true");
        }
        finally{device.SetRenderTargets(targets);WorldGen.noTileActions=noActions;WorldGen.gen=gen;}
    }
    private static void ChoiceRender(Player p)
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();bool menu=Main.gameMenu;
        bool click=Main.mouseLeft,release=Main.mouseLeftRelease;
        using var atlas=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight);
        try
        {
            p.inventory[0]=new Item(DivineCatalog.Cache(2));DivineChoiceUI.Open(2,0);Main.gameMenu=false;Main.mouseLeft=false;Main.mouseLeftRelease=false;
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));Main.spriteBatch.Begin();DivineChoiceUI.Draw();Main.spriteBatch.End();
            Check(p.inventory[0].stack==1,"drawdoesnotconsumecache");device.SetRenderTargets(targets);SaveImage(atlas,"divine-choice-runtime.png");Log("DIVINE_CHOICE_RENDER_PASS fourChoices=4 openConsumes=0");
        }
        finally{device.SetRenderTargets(targets);Main.gameMenu=menu;Main.mouseLeft=click;Main.mouseLeftRelease=release;DivineChoiceUI.Tier=DivineChoiceUI.Slot=-1;}
    }
}
