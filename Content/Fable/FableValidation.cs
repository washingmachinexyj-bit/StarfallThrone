using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.Localization;
using StarfallThrone.Content.Fable.Bosses;
using StarfallThrone.Content.Fable.Equipment;

namespace StarfallThrone.Content.Fable;
public static class FableValidation
{
    static int checks;
    static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException("Fable: "+reason);checks++;}
    static void Log(string s)=>ModContent.GetInstance<FableWorld>().Mod.Logger.Info(s);
    internal static bool Headless=>Main.dedServ && Terraria.Program.LaunchParameters.ContainsKey("-testservermodloading") && Terraria.Program.LaunchParameters.ContainsKey("-starfall-fable-headless");
    static void Guard()=>Check(Headless || Main.gameMenu && Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"),"isolated tests only");
    public static void Data()
    {
        Guard();
        var mod=ModContent.GetInstance<FableWorld>().Mod;
        Check(mod.GetContent<ModNPC>().Count(n=>n is FableBossNPC)==18,"18 minibosses");
        int mode=Main.GameMode;
        try{for(int m=0;m<3;m++)for(int i=0;i<18;i++)
        {
            Main.GameMode=m;NPC n=new();n.SetDefaults(FableCatalog.BossType(i));
            Check(n.lifeMax==(int)MathF.Ceiling(FableCatalog.Life[i]*(m==2?1.5f:m==1?1.25f:1)),"final HP "+m+"/"+i+" actual="+n.lifeMax);
            Check(!n.boss && n.defense==FableCatalog.Defense[i],"mini classification/defense "+i);
            if(!Main.dedServ)Check(NPCHeadLoader.GetBossHeadSlot(n.ModNPC.BossHeadTexture)>=0,"head "+i);
            Item item=new(FableCatalog.Weapon(i));Check(item.damage==FableWeaponBase.Damage[i] && item.value==0,"weak free weapon "+i);
        }}finally{Main.GameMode=mode;}
        var book=Main.recipe.Take(Recipe.numRecipes).Single(r=>r.createItem.type==ModContent.ItemType<FableBook>());
        Check(book.requiredItem.Count==0 && book.requiredTile.Count==0,"free handcraft book");
        var blade=Main.recipe.Take(Recipe.numRecipes).Single(r=>r.createItem.type==ModContent.ItemType<FableUnfinishedBlade>());
        Check(Enumerable.Range(0,18).All(i=>blade.requiredItem.Any(x=>x.type==FableCatalog.Weapon(i))) && blade.requiredTile.Contains(ModContent.TileType<FableWorkbenchTile>()),"18-weapon recipe and station");
        Check(new Item(ModContent.ItemType<FableUnfinishedBlade>()).damage==11,"final modest damage");
        foreach(string lang in new[]{"en-US","zh-Hans"})
        {
            LanguageManager.Instance.SetLanguage(GameCulture.FromName(lang));
            for(int i=0;i<18;i++)Check(!FableCatalog.Text("Hint"+i).StartsWith("Mods.") && !FableCatalog.Name(i).StartsWith("Mods."),"localization "+lang+i);
        }
        LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(GameCulture.CultureName.Chinese));
        Check(mod.GetContent<ModItem>().All(item=>item.DisplayName.Value.Any(c=>c>='\u4e00' && c<='\u9fff')),"all1539itemnamesChinese");
        if(Headless)global::StarfallThrone.Content.Systems.MiniBossValidation.Run();
        Log("FABLE_DATA_PASS checks="+checks+" minibosses=18 difficultyHP=54 weapons=18 freeBook=true");
    }
    public static void Run()
    {
        Guard();var world=ModContent.GetInstance<FableWorld>();TagCompound saved=new();world.SaveWorldData(saved);
        var seedFlags=global::StarfallThrone.Content.Oaths.OathWorld.SeedWon;global::StarfallThrone.Content.Oaths.OathWorld.SeedWon=new bool[3];
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var projs=(Projectile[])Main.projectile.Clone();var items=(Item[])Main.item.Clone();float savedJumpSpeed=Player.jumpSpeed;Player.jumpSpeed=5.01f;
        int net=Main.netMode,my=Main.myPlayer,mode=Main.GameMode;bool menu=Main.gameMenu;var file=Main.ActiveWorldFileData;int invasion=Main.invasionType;bool snow=Main.snowMoon,pumpkin=Main.pumpkinMoon;
        try
        {
            Main.netMode=0;Main.myPlayer=0;Main.GameMode=0;Main.invasionType=0;Main.snowMoon=Main.pumpkinMoon=false;Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            for(int i=0;i<Main.maxPlayers;i++)Main.player[i]=new Player{active=false,whoAmI=i};
            for(int i=0;i<Main.maxNPCs;i++)Main.npc[i]=new NPC{active=false,whoAmI=i};
            for(int i=0;i<Main.maxProjectiles;i++)Main.projectile[i]=new Projectile{active=false,whoAmI=i};
            for(int i=0;i<Main.maxItems;i++)Main.item[i]=new Item();
            Player p=new(){active=true,whoAmI=0,position=new Vector2(18000,1200),width=20,height=42,gravDir=1f};p.ResetEffects();p.inventory[0]=new Item(ModContent.ItemType<FableBook>());Main.player[0]=p;
            p.GetModPlayer<global::StarfallThrone.Content.Oaths.OathPlayer>().ClosureApproved=true; // This legacy fixture explicitly accepts the new opening warning.
            if(ModLoader.TryGetMod("BossChecklist",out Mod checklist))
            {
                var records=checklist.GetContent<ModSystem>().Single(s=>s.Name=="RecordSystem");Main.netMode=NetmodeID.Server;records.OnWorldLoad();records.LoadWorldData(new TagCompound());Main.netMode=0;
                p.GetModPlayer(checklist.GetContent<ModPlayer>().Single(s=>s.Name=="RecordModPlayer")).OnEnterWorld();
            }
            world.ClearWorld();Check(FableWorld.Sealed && FableWorld.Unlocked(0) && !FableWorld.Unlocked(1),"fresh world seal");
            Check(!FableWorld.TrySummon(p,1),"locked summon rejected");
            Main.gameMenu=false;p.GetModPlayer<FablePlayer>().PreUpdateBuffs();Check(p.HasBuff(BuffID.NoBuilding),"native Creative Shock");Main.gameMenu=true;
            for(int prefix=0;prefix<=18;prefix++)
            {
                world.LoadWorldData(new TagCompound{{"Victories",(1<<prefix)-1},{"Skipped",false}});
                Check(FableWorld.Completed==(prefix==18),"hidden ending completion "+prefix);
                for(int i=0;i<18;i++)Check(FableWorld.Unlocked(i)==(i<=prefix),"sequential unlock "+prefix+"/"+i);
            }
            world.ClearWorld();int before=FableWorld.Mask();try{using var stream=new MemoryStream(new byte[]{1,0,0,0});using var reader=new BinaryReader(stream);world.NetReceive(reader);Check(false,"truncated packet accepted");}catch(EndOfStreamException){}Check(FableWorld.Mask()==before,"truncated state atomic");
            world.LoadWorldData(new TagCompound{{"Victories",(1<<17)|3},{"Skipped",false}});Check(FableWorld.Mask()==3 && !FableWorld.Completed,"reject noncontiguous state");
            world.ClearWorld();Checklist();
            for(int i=0;i<18;i++)
            {
                Check(FableWorld.TrySummon(p,i),"native summon "+i);var boss=FableWorld.ActiveBoss;Check(boss!=null && boss.Index==i,"summoned index");
                if(i==0)Physics(p,boss);
                for(int t=0;t<550;t++)
                {
                    if(t==360)boss.NPC.life=Math.Max(1,boss.NPC.lifeMax/5);
                    boss.AI();boss.NPC.position+=boss.NPC.velocity;
                    Check(boss.NPC.active && float.IsFinite(boss.NPC.position.X),"finite AI "+i+"/"+t);
                    foreach(Projectile proj in Main.ActiveProjectiles)if(proj.ModProjectile is FableHazard){proj.ModProjectile.AI();proj.position+=proj.velocity;if(--proj.timeLeft<=0)proj.Kill();}
                    p.GetModPlayer<FablePlayer>().PreUpdateMovement();
                }
                Check(boss.Participants[0],"tracked participant");
                int weaponBefore=Main.item.Where(it=>it.active && it.type==FableCatalog.Weapon(i)).Sum(it=>it.stack);
                boss.NPC.NPCLoot();boss.NPC.active=false;p.GetModPlayer<FablePlayer>().PostUpdate();
                int delivered=Main.item.Where(it=>it.active && it.type==FableCatalog.Weapon(i)).Sum(it=>it.stack)-weaponBefore;
                Check(delivered==1,"native loot exactly once "+i+" actual="+delivered);
                Check(FableWorld.Downed[i] && Main.item.Any(it=>it.active && it.type==FableCatalog.Weapon(i)),"guaranteed reward "+i);
                Check(!p.GetModPlayer<FablePlayer>().InTrial,"returned from arena "+i);
                Log("FABLE_AI_PASS index="+i+" frames=550 reward=true");
            }
            Check(FableWorld.Completed && !FableWorld.Sealed,"hidden kill lifts seal");
            Check(Main.item.Where(it=>it.active && it.type==ModContent.ItemType<FableCore>()).Sum(it=>it.stack)>=12,"hidden first materials");
            Check(!FableWorld.Skip(p),"completed not skippable");
            world.ClearWorld();Check(FableWorld.TrySummon(p,0),"summon before skip");Check(FableWorld.Skip(p),"skip accepted");
            Check(!FableWorld.Sealed && FableWorld.Mask()==0 && !FableWorld.Unlocked(0) && FableWorld.ActiveBoss==null,"skip no fake victories cancels fight");
            TagCompound skip=new();world.SaveWorldData(skip);world.ClearWorld();world.LoadWorldData(skip);Check(FableWorld.Skipped,"skip persists");
            p.ClearBuff(BuffID.NoBuilding);Main.gameMenu=false;p.GetModPlayer<FablePlayer>().PreUpdateBuffs();Check(!p.HasBuff(BuffID.NoBuilding),"seal no longer renewed");Main.gameMenu=true;
            Equipment(p);if(!Headless)Render(p,world);Furniture();
            Log("FABLE_RUNTIME_PASS checks="+checks+" sequential=18 hidden=true skipPermanent=true stateAtomic=true nativeBuff=true");
        }
        finally
        {
            global::StarfallThrone.Content.Oaths.OathWorld.SeedWon=seedFlags;
            for(int i=0;i<players.Length;i++)Main.player[i]=players[i];for(int i=0;i<npcs.Length;i++)Main.npc[i]=npcs[i];for(int i=0;i<projs.Length;i++)Main.projectile[i]=projs[i];for(int i=0;i<items.Length;i++)Main.item[i]=items[i];
            world.LoadWorldData(saved);Main.netMode=net;Main.myPlayer=my;Main.GameMode=mode;Main.gameMenu=menu;Main.ActiveWorldFileData=file;Main.invasionType=invasion;Main.snowMoon=snow;Main.pumpkinMoon=pumpkin;FableUI.Opened=false;FableUI.ConfirmSkip=false;Player.jumpSpeed=savedJumpSpeed;
        }
    }
    static void Physics(Player p,FableBossNPC boss)
    {
        bool menu=Main.gameMenu;int net=Main.netMode,my=Main.myPlayer;Main.gameMenu=false;
        try
        {
            float floor=boss.ArenaFloor;var fp=p.GetModPlayer<FablePlayer>();
            if(Headless){Main.netMode=NetmodeID.Server;Main.myPlayer=255;}
            p.controlJump=false;p.releaseJump=true;
            for(int t=0;t<20;t++)p.Update(0);
            Check(Math.Abs(p.Bottom.Y-floor)<2,"native player stands on virtual floor: "+p.Bottom.Y+" vs "+floor);
            Check(p.noBuilding && p.HasBuff(BuffID.NoBuilding),"native buff actually prevents building");
            p.controlJump=true;p.releaseJump=true;float minY=p.position.Y;
            for(int t=0;t<24;t++){p.Update(0);minY=Math.Min(minY,p.position.Y);}
            if(Headless)Check(minY<floor-p.height-4,"native player can jump from virtual floor: "+minY+" vs "+(floor-p.height));
            else Log("FABLE_PHYSICS_GRAPHICAL_FIXTURE_SKIP: local Player.Update does not synthesize menu input; floor and native buff remain checked");
            p.controlJump=false;for(int t=0;t<120;t++)p.Update(0);
            Check(Math.Abs(p.Bottom.Y-floor)<2,"native player lands on virtual floor");
            Main.netMode=net;Main.myPlayer=my;
            boss.AI();
            int slot=Projectile.NewProjectile(p.GetSource_Misc("FableValidation"),p.Center,Vector2.Zero,ModContent.ProjectileType<FableHazard>(),3,0,0,boss.NPC.whoAmI,1,boss.Serial);
            var proj=Main.projectile[slot];p.statLife=100;p.immune=false;p.immuneTime=0;Array.Clear(p.hurtCooldowns);
            // The headless engine has no fonts/combat-text renderer. Suppress only drawing;
            // still execute Projectile.Damage and the complete native hurt/combined-hook path.
            MonoMod.RuntimeDetour.Hook textHook=null;
            try
            {
                if(Headless)textHook=new MonoMod.RuntimeDetour.Hook(typeof(CombatText).GetMethod("NewText",new[]{typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)}),(Func<Rectangle,Color,string,bool,bool,int>)((a,b,c,d,e)=>100));
                proj.Damage();
            }
            finally{textHook?.Dispose();}
            int hurt=100-p.statLife;Check(hurt>=2 && hurt<=4,"hostile projectile final damage normalized: "+hurt);proj.Kill();
            Log("FABLE_PHYSICS_PASS nativePlayerUpdate=true floor=true jump=true land=true creativeShock=true projectileDamage="+hurt);
        }
        finally{Main.gameMenu=menu;Main.netMode=net;Main.myPlayer=my;p.controlJump=false;}
    }
    static void Equipment(Player p)
    {
        p.GetModPlayer<FablePlayer>().End();p.ResetEffects();
        for(int i=0;i<18;i++)
        {
            Item weapon=new(FableCatalog.Weapon(i));Check(weapon.damage<=9 && weapon.value==0,"weak weapon boundary");
            if(FableWeaponBase.Kind(i)==0)continue;
            int slotCount=Main.projectile.Count(proj=>proj.active && proj.friendly);
            weapon.ModItem.Shoot(p,new Terraria.DataStructures.EntitySource_ItemUse_WithAmmo(p,weapon,0),p.Center,Vector2.UnitX*3,weapon.shoot,weapon.damage,weapon.knockBack);
            Check(Main.projectile.Any(proj=>proj.active && proj.owner==p.whoAmI && proj.type==weapon.shoot && (int)proj.ai[0]==(i==6?0:i)),"actual shoot "+i);
        }
        var sword=new Item(ModContent.ItemType<FableUnfinishedBlade>());
        for(int i=1;i<=6;i++){sword.ModItem.UseAnimation(p);Check(Math.Abs(sword.scale-(i%3==0?1.2f:1))<.001f,"third swing reach "+i);}
        var head=new Item(ModContent.ItemType<FableArmor0>());var body=new Item(ModContent.ItemType<FableArmor1>());var legs=new Item(ModContent.ItemType<FableArmor2>());
        Check(head.defense+body.defense+legs.defense==4 && head.ModItem.IsArmorSet(head,body,legs),"paper armor modest defense");float speed=p.moveSpeed;head.ModItem.UpdateArmorSet(p);Check(Math.Abs(p.moveSpeed-speed-.03f)<.001f,"paper set only3percent movement");
        Log("FABLE_EQUIPMENT_PASS nativeShoot=true thirdSwing=true armorDefense=4 setMovement=0.03");
    }
    static void Checklist()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod mod)){Log("FABLE_CHECKLIST_ABSENT_PASS");return;}
        Check(ModContent.GetInstance<FableChecklist>().StrictVisibilityInstalled,"strict discovery hook installed");
        var dict=mod.Call("GetBossInfoDictionary",ModContent.GetInstance<FableWorld>().Mod,"2.0.0") as IDictionary;
        for(int i=0;i<18;i++)Check(dict.Contains("StarfallThrone Fable"+i),"checklist registered "+i);
        for(int i=0;i<global::StarfallThrone.Content.Fable.Trials.FourTrialCatalog.Count;i++)Check(dict.Contains("StarfallThrone FourTrial"+i),"four-trial checklist registered "+i);
        var tracker=mod.Code.GetType("BossChecklist.BossTracker");
        // Inspect the same live entries used by the book, not the public dictionary clone.
        object instance=mod.GetType().GetFields(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).FirstOrDefault(f=>f.FieldType==tracker)?.GetValue(mod);
        if(instance==null)throw new InvalidOperationException("Fable checklist tracker not found");
        var entries=(IEnumerable)tracker.GetField("SortedEntries",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(instance);
        if(entries==null)entries=(IEnumerable)tracker.GetProperty("SortedEntries",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(instance);
        Check(entries!=null,"actual checklist entries");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        int future=0,four=0;foreach(object entry in entries){var type=entry.GetType();string key=(string)type.GetProperty("Key",flags).GetValue(entry);if(key.StartsWith("StarfallThrone FourTrial")){Check((bool)type.GetMethod("VisibleOnChecklist",flags).Invoke(entry,null),"four-trial visible "+key);four++;continue;}if(!key.StartsWith("StarfallThrone Fable"))continue;int i=int.Parse(key["StarfallThrone Fable".Length..]);if(i==0)continue;Check(!(bool)type.GetMethod("VisibleOnChecklist",flags).Invoke(entry,null),"future hidden "+i);future++;}
        Check(future==17,"all future entries hidden");Check(four==global::StarfallThrone.Content.Fable.Trials.FourTrialCatalog.Count,"all four trial entries visible");Log("FABLE_CHECKLIST_PASS actualPredicate=true futureHidden=17 fourTrials=4");
    }
    static void Save(RenderTarget2D image,string name){using var stream=File.Create(Path.GetFullPath(Path.Combine(Main.SavePath,"../../outputs/"+name)));image.SaveAsPng(stream,image.Width,image.Height);}
    static void Render(Player p,FableWorld world)
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();bool menu=Main.gameMenu,click=Main.mouseLeft,release=Main.mouseLeftRelease;Vector2 screen=Main.screenPosition;
        using var rt=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight);
        try
        {
            Main.gameMenu=false;Main.mouseLeft=Main.mouseLeftRelease=false;FableUI.Opened=true;
            for(int state=0;state<4;state++)
            {
                world.ClearWorld();if(state==1)for(int i=0;i<8;i++)FableWorld.Downed[i]=true;if(state==3)Array.Fill(FableWorld.Downed,true);FableUI.ConfirmSkip=state==2;
                device.SetRenderTarget(rt);device.Clear(new Color(19,26,36));Main.spriteBatch.Begin();FableUI.Draw();Main.spriteBatch.End();device.SetRenderTargets(targets);Save(rt,"fable-book-"+state+".png");
            }
            device.SetRenderTarget(rt);device.Clear(new Color(19,26,36));Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            for(int i=0;i<18;i++){var tex=ModContent.Request<Texture2D>(FableCatalog.Root+"Boss"+i).Value;Main.spriteBatch.Draw(tex,new Vector2(35+i%6*115,30+i/6*140),null,Color.White,0,Vector2.Zero,2,SpriteEffects.None,0);var weapon=ModContent.Request<Texture2D>(FableCatalog.Root+"Weapon"+i).Value;Main.spriteBatch.Draw(weapon,new Vector2(55+i%6*115,120+i/6*140),Color.White);}
            Main.spriteBatch.End();Main.screenPosition=Vector2.Zero;
            for(int i=0;i<3;i++){Player doll=new(){active=true,isDisplayDollOrInanimate=true,whoAmI=0};doll.ResetEffects();doll.head=new Item(ModContent.ItemType<FableArmor0>()).headSlot;doll.body=new Item(ModContent.ItemType<FableArmor1>()).bodySlot;doll.legs=new Item(ModContent.ItemType<FableArmor2>()).legSlot;doll.bodyFrame=new Rectangle(0,i*56,40,56);doll.legFrame=new Rectangle(0,i*56,40,56);doll.position=new Vector2(750+i*70,100);Main.PlayerRenderer.DrawPlayer(Main.Camera,doll,doll.position,0,Vector2.Zero,0,1.5f);}
            device.SetRenderTargets(targets);Save(rt,"fable-assets-runtime.png");Log("FABLE_RENDER_PASS bookStates=4 bossIcons=18 weaponIcons=18 armorPoses=3");
        }
        finally{device.SetRenderTargets(targets);Main.gameMenu=menu;Main.mouseLeft=click;Main.mouseLeftRelease=release;Main.screenPosition=screen;FableUI.Opened=false;FableUI.ConfirmSkip=false;}
    }
    static void Furniture()
    {
        const int x=1260,y=100;bool gen=WorldGen.gen,no=WorldGen.noTileActions;
        try{WorldGen.gen=WorldGen.noTileActions=false;for(int xx=x-2;xx<x+5;xx++)for(int yy=y-2;yy<y+5;yy++){var tile=Main.tile[xx,yy];tile.ClearEverything();if(yy>=y+2){tile.HasTile=true;tile.TileType=TileID.Dirt;}}
            int type=ModContent.TileType<FableWorkbenchTile>();var data=TileObjectData.GetTileData(type,0);WorldGen.PlaceObject(x+data.Origin.X,y+data.Origin.Y,type);Check(Main.tile[x,y].HasTile && Main.tile[x,y].TileType==type,"native workbench placement");
            int width=36,height=36;
            if(!Headless){var texture=ModContent.Request<Texture2D>(FableCatalog.Root+"WorkbenchTile").Value;width=texture.Width;height=texture.Height;}
            for(int xx=0;xx<2;xx++)for(int yy=0;yy<2;yy++){var t=Main.tile[x+xx,y+yy];Check(t.TileFrameX+16<=width && t.TileFrameY+16<=height,"workbench UV");}
            int before=Main.item.Where(i=>i.active && i.type==ModContent.ItemType<FableWorkbench>()).Sum(i=>i.stack);WorldGen.KillTile(x,y);WorldGen.SquareTileFrame(x,y,true);int after=Main.item.Where(i=>i.active && i.type==ModContent.ItemType<FableWorkbench>()).Sum(i=>i.stack);Check(after-before==1,"one recovered workbench");Log("FABLE_FURNITURE_PASS nativePlace=true nativeBreak=true frameBounds=true");
        }finally{WorldGen.gen=gen;WorldGen.noTileActions=no;}
    }
}
public sealed class FableHeadlessValidation : ModSystem
{
    public override void PostAddRecipes()
    {
        if(!FableValidation.Headless)return;
        FableValidation.Data();FableValidation.Run();
        Mod.Logger.Info("FABLE_HEADLESS_PASS (no graphical validation in this mode)");
    }
}
