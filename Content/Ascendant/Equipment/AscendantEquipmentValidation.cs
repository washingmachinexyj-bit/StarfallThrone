#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using global::StarfallThrone.Content.Divine;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Ascendant.Equipment;

// Called only by the parent's opt-in menu smoke harness. No build, install, world or file writes.
public static class AscendantEquipmentValidation
{
    private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("Ascendant equipment: "+label);}
    private static void Log(string text)=>ModContent.GetInstance<AscendantWeapon0>().Mod.Logger.Info(text);
    private static void Tooltip(ModItem item)=>Check(!string.IsNullOrWhiteSpace(item.Tooltip.Value)&&item.Tooltip.Value!=item.Tooltip.Key&&!item.Tooltip.Value.Contains("{$"),"resolved tooltip "+item.Name);
    public static void Data()
    {
        for(int tier=0;tier<3;tier++)for(int style=0;style<4;style++)
        {
            Player p=new();p.ResetEffects();int total=0;int[] ids={tier*6+style,tier*6+4,tier*6+5};
            for(int slot=0;slot<3;slot++)
            {
                Item item=new(AscendantCatalog.Armor(ids[slot]));p.armor[slot]=item;total+=item.defense;
                Check(item.ModItem is AscendantArmor&&(slot==0?item.headSlot>=0:slot==1?item.bodySlot>=0:item.legSlot>=0),"native armor "+ids[slot]);
                Tooltip(item.ModItem);item.ModItem.UpdateEquip(p);
                Recipe(item.type,DivineCatalog.Armor(ids[slot]),tier,slot==0?6:slot==1?10:8);
            }
            Check(total==AscendantEquipmentData.Defense[tier,style],"exact defense "+tier+"/"+style);
            Check(p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"set detection");p.armor[0].ModItem.UpdateArmorSet(p);
            Check(AscendantEquipmentData.EquippedTier(p)==tier&&p.GetModPlayer<AscendantEquipmentPlayer>().SetTier==tier,"set registration");
            p.armor[2]=new Item(ItemID.WoodGreaves);Check(AscendantEquipmentData.EquippedTier(p)==-1,"mixed armor rejection");
        }
        for(int id=0;id<12;id++)
        {
            Item item=new(AscendantCatalog.Weapon(id));Check(item.ModItem is AscendantWeapon&&item.damage==AscendantArsenal.Damage[id]&&item.DamageType==AscendantEquipmentData.Class(id%4),"weapon defaults "+id);
            Tooltip(item.ModItem);Recipe(item.type,DivineCatalog.Weapon(id),id/4,8);
            if(id%4==1)Check(item.useAmmo!=AmmoID.None,"native ammo "+id);
            if(id%4>=2)Check(item.mana>0,"native mana "+id);
            if(id%4==3)
            {Projectile q=new();q.SetDefaults(item.shoot);Check(q.minion&&q.minionSlots==(id==3?1:2)&&q.DamageType==DamageClass.Summon&&ProjectileID.Sets.MinionSacrificable[q.type],"native minion "+id);}
        }
        for(int id=0;id<3;id++)
        {
            Item item=new(AscendantCatalog.Expert(id)),old=new(DivineCatalog.Expert(id));Player p=new();p.ResetEffects();int life=p.statLifeMax2;float damage=p.GetDamage(DamageClass.Generic).Additive;
            item.ModItem.UpdateAccessory(p,false);Tooltip(item.ModItem);
            Check(item.expert&&item.accessory&&p.GetModPlayer<AscendantEquipmentPlayer>().Expert[id],"expert registration "+id);
            Check(id<2?p.statLifeMax2-life==(id==0?35:60):Math.Abs(p.GetDamage(DamageClass.Generic).Additive-damage-.12f)<.001f,"expert passive "+id);
            Check(!item.ModItem.CanAccessoryBeEquippedWith(item,old,p)&&!item.ModItem.CanAccessoryBeEquippedWith(old,item,p),"same family mutually exclusive "+id);
        }
        var state=new Player().GetModPlayer<AscendantEquipmentPlayer>();for(int i=0;i<6;i++)state.Cooldowns[i]=120+i;
        TagCompound tag=new();state.SaveData(tag);using var stream=new MemoryStream();TagIO.ToStream(tag,stream);stream.Position=0;
        var copy=new Player().GetModPlayer<AscendantEquipmentPlayer>();copy.LoadData(TagIO.FromStream(stream));
        Check(state.Cooldowns.SequenceEqual(copy.Cooldowns),"native binary save");state.ResetEffects();state.OnEnterWorld();
        Check(state.Cooldowns.SequenceEqual(copy.Cooldowns),"unequip/world entry retains cooldowns");state.UpdateDead();
        Check(state.Cooldowns.Select((x,i)=>x==copy.Cooldowns[i]||x==copy.Cooldowns[i]-1).All(x=>x),"death does not refresh cooldowns");
        Log("ASCENDANT_EQUIPMENT_DATA_PASS weapons=12 armor=18 experts=3 upgradeRecipes=true exactDefense=true cooldownPersistence=true");
    }
    private static void Recipe(int type,int original,int tier,int count)
    {
        Terraria.Recipe[] recipes=Main.recipe.Take(Terraria.Recipe.numRecipes).Where(r=>!r.Disabled&&r.createItem.type==type).ToArray();
        Check(recipes.Length==1&&recipes[0].requiredItem.Count==2&&recipes[0].requiredItem.Any(i=>i.type==original&&i.stack==1)&&
            recipes[0].requiredItem.Any(i=>i.type==AscendantCatalog.Material(tier)&&i.stack==count)&&recipes[0].requiredTile.SequenceEqual(new[]{AscendantCatalog.CraftStation(tier)}),"exact accessible upgrade recipe "+type);
        Check(recipes[0].Conditions.Count==0,"crafting remains available after challenge deadline "+type);
    }
    private readonly record struct SavedTile(int X,int Y,bool Has,byte Liquid)
    {public static SavedTile Capture(int x,int y){Tile t=Main.tile[x,y];return new(x,y,t.HasTile,t.LiquidAmount);}public void Restore(){Tile t=Main.tile[X,Y];t.HasTile=Has;t.LiquidAmount=Liquid;}}
    private sealed class Metrics
    {public int Hits,Ammo,Mana,Peak;public readonly HashSet<ModProjectile> Seen=new();public readonly HashSet<AscendantShotKind> Kinds=new();}
    public static void Run()
    {
        Check(Main.gameMenu&&!Main.dedServ&&Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"),"isolated opt-in smoke only");
        Check(Main.tile.Width>500&&Main.tile.Height>180,"initialized fixture terrain");
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var shots=(Projectile[])Main.projectile.Clone();
        var dust=(Dust[])Main.dust.Clone();var items=(Item[])Main.item.Clone();var gore=(Gore[])Main.gore.Clone();var texts=(CombatText[])Main.combatText.Clone();
        var identities=(int[,])Main.projectileIdentity.Clone();var random=Main.rand;var world=Main.ActiveWorldFileData;
        int mode=Main.netMode,myPlayer=Main.myPlayer,gameMode=Main.GameMode,mx=Main.mouseX,my=Main.mouseY;Vector2 screen=Main.screenPosition;
        var tiles=new List<SavedTile>();
        GraphicsDevice device=Main.instance.GraphicsDevice;var renderTargets=device.GetRenderTargets();var viewport=device.Viewport;var scissor=device.ScissorRectangle;
        var blend=device.BlendState;var raster=device.RasterizerState;var depth=device.DepthStencilState;var sampler=device.SamplerStates[0];
        using var render=new RenderTarget2D(device,360,240);
        using var weaponAtlas=new RenderTarget2D(device,1200,1440,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.GameMode=0;Main.rand=new UnifiedRandom(91261);Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i,active=false};
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i,active=false};
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{whoAmI=i,active=false};
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust{active=false};
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item{active=false};
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore{active=false};
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText{active=false};
            for(int x=300;x<500;x++)for(int y=70;y<180;y++){tiles.Add(SavedTile.Capture(x,y));Tile t=Main.tile[x,y];t.HasTile=false;t.LiquidAmount=0;}
            device.SetRenderTarget(weaponAtlas);device.Clear(new Color(14,21,33));device.SetRenderTarget(null);
            var kinds=new HashSet<AscendantShotKind>();
            for(int id=0;id<12;id++)
            {
                Clear();Player p=Fresh();Item item=p.inventory[0];item.SetDefaults(AscendantCatalog.Weapon(id));Targets(p);Metrics metric=new();
                int casts=id%4==3?1:id is 0 or 1 or 2 or 8 or 10?3:id==4?5:4;bool detailCaptured=false;
                for(int cast=0;cast<casts;cast++)
                {
                    Cast(p,item,metric,false,id==10?cast*75:0);
                    for(int tick=0;tick<(id%4==3?400:id is 2 or 10?24:70);tick++)
                    {
                        Step(p,metric);
                        int captureTick=id%4==0?7:id%4==3?id==3?30:35:0;
                        if(cast==0&&tick==captureTick)RenderAttack(p,id,device,render,weaponAtlas,false);
                        if(!detailCaptured&&(id%4==1||id%4==3)&&Owned().Any(q=>q.ModProjectile is AscendantShot s&&s.Secondary))
                        {RenderAttack(p,id,device,render,weaponAtlas,true);detailCaptured=true;}
                    }
                }
                if(id is 0 or 2 or 4 or 6 or 8 or 10)
                {
                    Cast(p,item,metric,true);
                    for(int tick=0;tick<180;tick++)
                    {
                        Step(p,metric);
                        if(tick==(id==0?0:id==4||id==2?12:8))
                        {RenderAttack(p,id,device,render,weaponAtlas,true);detailCaptured=true;}
                    }
                }
                Check(detailCaptured,"secondary/alternate render captured "+id);
                Check(metric.Hits>0&&metric.Seen.Count>0,"native damage weapon "+id+" hits="+metric.Hits);
                Check(metric.Peak<=96&&metric.Seen.Count<150,"bounded weapon graph "+id);
                if(item.useAmmo!=AmmoID.None)Check(metric.Ammo==casts,"native ammo cost "+id);
                if(item.mana>0)Check(metric.Mana>0,"native mana cost "+id);
                if(id==0)Check(metric.Kinds.Contains(AscendantShotKind.Branch),"crossing branch finisher");
                if(id==1)Check(metric.Kinds.Contains(AscendantShotKind.Seed),"three hit seed burst");
                if(id==2)Check(metric.Kinds.Contains(AscendantShotKind.FlowerRing),"sequential flower detonation");
                if(id==4)Check(metric.Kinds.Contains(AscendantShotKind.LawWave),"four law pressure release");
                if(id==5||id==6)Check(metric.Kinds.Contains(AscendantShotKind.Explosion),"mark detonation "+id);
                if(id==8)Check(metric.Kinds.Contains(AscendantShotKind.CutMark),"placed cut replay");
                if(id==9)Check(metric.Kinds.Contains(AscendantShotKind.CrossEcho),"empowered fourth arrow");
                if(id==10)Check(metric.Kinds.Contains(AscendantShotKind.StarLink),"star link topology");
                if(id%4==3){p.ClearBuff(item.buffType);Step(p,metric);Step(p,metric);}
                for(int tick=0;tick<620;tick++)Step(p,metric);
                Check(!Owned().Any(),"natural expiry/dismissal "+id);
                foreach(var k in metric.Kinds)kinds.Add(k);
                Log($"ASCENDANT_WEAPON_RUNTIME_PASS id={id} hits={metric.Hits} spawned={metric.Seen.Count} peak={metric.Peak} ammo={metric.Ammo} mana={metric.Mana}");
            }
            Check(kinds.Count>=14,"distinct attack states");
            string weaponsPath=Output("ascendant-weapons-runtime.png");
            device.SetRenderTarget(null);using(var output=File.Create(weaponsPath))weaponAtlas.SaveAsPng(output,weaponAtlas.Width,weaponAtlas.Height);
            Log("ASCENDANT_WEAPONS_RENDER_PASS weapons=12 views=24 contactFrames=true nativePreDraw=true path="+weaponsPath);
            Abilities(Fresh());Bounds(Fresh());RenderArmor(device,render);
            Log("ASCENDANT_EQUIPMENT_RENDER_PASS weapons=12 loadouts=12 poses=3 nativePlayerRenderer=true");
            Log("ASCENDANT_EQUIPMENT_RUNTIME_PASS weapons=12 nativeAI=true nativeDamage=true nativeAmmo=true nativeMana=true abilities=true bounded=true");
        }
        finally
        {
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(shots,Main.projectile,shots.Length);
            Array.Copy(dust,Main.dust,dust.Length);Array.Copy(items,Main.item,items.Length);Array.Copy(gore,Main.gore,gore.Length);Array.Copy(texts,Main.combatText,texts.Length);
            Array.Copy(identities,Main.projectileIdentity,identities.Length);foreach(var t in tiles)t.Restore();Main.netMode=mode;Main.myPlayer=myPlayer;Main.GameMode=gameMode;Main.rand=random;Main.ActiveWorldFileData=world;Main.screenPosition=screen;Main.mouseX=mx;Main.mouseY=my;
            device.SetRenderTargets(renderTargets);device.Viewport=viewport;device.ScissorRectangle=scissor;device.BlendState=blend;device.RasterizerState=raster;device.DepthStencilState=depth;device.SamplerStates[0]=sampler;
        }
    }
    private static Player Fresh()
    {Player p=new(){whoAmI=0,active=true,dead=false,selectedItem=0,direction=1,position=new Vector2(5600,1880)};Main.player[0]=p;p.ResetEffects();p.statLife=p.statLifeMax2=10000;p.statMana=p.statManaMax2=10000;p.maxMinions=8;p.MinionAttackTargetNPC=0;return p;}
    private static void Targets(Player p)
    {
        for(int i=0;i<3;i++){NPC n=new();n.SetDefaults(NPCID.BlueSlime);n.whoAmI=i;n.active=true;n.life=n.lifeMax=10000000;n.defense=0;n.damage=10;n.knockBackResist=0;n.width=90;n.height=150;n.Center=p.Center+new Vector2(85+i*165,0);n.noGravity=true;n.HitSound=null;n.DeathSound=null;Main.npc[i]=n;}
    }
    private static IEnumerable<Projectile> Owned()=>Main.projectile.Where(q=>q.active&&q.ModProjectile is AscendantShot or AscendantMinion);
    private static void Clear(){foreach(Projectile q in Main.ActiveProjectiles)q.active=false;}
    private static long Health()=>Main.npc.Where(n=>n.active).Sum(n=>(long)n.life);
    private static void Cast(Player p,Item item,Metrics metric,bool alt,int offset=0)
    {
        Main.screenPosition=p.Center-new Vector2(100,100);Main.mouseX=200+offset;Main.mouseY=100;p.altFunctionUse=alt?2:0;
        p.itemAnimation=p.itemAnimationMax=item.useAnimation;p.itemTime=p.itemTimeMax=item.useTime;
        Check(item.ModItem.CanUseItem(p),"usable weapon="+((AscendantWeapon)item.ModItem).Index+" alt="+alt);
        int type=item.shoot,damage=p.GetWeaponDamage(item),ammoId=0;float speed=item.shootSpeed,kb=item.knockBack;
        if(item.useAmmo!=AmmoID.None)
        {Item ammo=p.inventory[54];ammo.SetDefaults(item.useAmmo==AmmoID.Arrow?ItemID.WoodenArrow:ItemID.MusketBall);ammo.stack=999;Check(p.PickAmmo(item,out type,out speed,out damage,out kb,out ammoId),"PickAmmo");metric.Ammo+=999-ammo.stack;}
        int mana=p.statMana;if(item.mana>0)Check(p.CheckMana(item,-1,true),"CheckMana");metric.Mana+=mana-p.statMana;
        item.ModItem.Shoot(p,new EntitySource_ItemUse_WithAmmo(p,item,ammoId,"AscendantSmoke"),p.MountedCenter,(Main.MouseWorld-p.MountedCenter).SafeNormalize(Vector2.UnitX)*speed,type,damage,kb);
    }
    private static void Step(Player p,Metrics metric)
    {
        Array.Clear(p.ownedProjectileCounts);foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==0)p.ownedProjectileCounts[q.type]++;
        for(int b=0;b<p.buffType.Length;b++)if(p.buffType[b]>0)ModContent.GetModBuff(p.buffType[b])?.Update(p,ref b);
        foreach(NPC n in Main.ActiveNPCs)for(int i=0;i<n.immune.Length;i++)if(n.immune[i]>0)n.immune[i]--;
        Projectile[] live=Owned().ToArray();metric.Peak=Math.Max(metric.Peak,live.Length);
        foreach(Projectile q in live)
        {
            bool fresh=metric.Seen.Add(q.ModProjectile);for(int i=0;i<q.localNPCImmunity.Length;i++)if(q.localNPCImmunity[i]>0)q.localNPCImmunity[i]--;
            q.AI();if(!q.active)continue;
            if(q.ModProjectile is AscendantShot shot){metric.Kinds.Add(shot.Kind);if(fresh)Roundtrip(q);}
            if(q.ModProjectile.ShouldUpdatePosition())q.position+=q.velocity;
            long health=Health();q.Damage();if(Health()<health)metric.Hits++;if(q.active&&q.penetrate==0)q.Kill();
            Check(AscendantArsenal.Finite(q.position)&&AscendantArsenal.Finite(q.velocity),"finite attack");if(q.active&&--q.timeLeft<=0)q.Kill();
        }
        if(p.itemTime>0)p.itemTime--;if(p.itemAnimation>0)p.itemAnimation--;
    }
    private static void Roundtrip(Projectile q)
    {
        using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))q.ModProjectile.SendExtraAI(writer);
        byte[] bytes=stream.ToArray();stream.Position=0;Projectile copy=new();copy.SetDefaults(q.type);copy.owner=q.owner;copy.ai=(float[])q.ai.Clone();copy.velocity=q.velocity;copy.Center=q.Center;
        copy.ModProjectile.ReceiveExtraAI(new BinaryReader(stream));using var second=new MemoryStream();using(var writer=new BinaryWriter(second,System.Text.Encoding.UTF8,true))copy.ModProjectile.SendExtraAI(writer);
        Check(bytes.SequenceEqual(second.ToArray())&&copy.DamageType==q.DamageType&&copy.tileCollide==q.tileCollide,"network shot roundtrip");
    }
    private static void Equip(Player p,int tier)
    {p.armor[0]=new Item(AscendantCatalog.Armor(tier*6));p.armor[1]=new Item(AscendantCatalog.Armor(tier*6+4));p.armor[2]=new Item(AscendantCatalog.Armor(tier*6+5));p.armor[0].ModItem.UpdateArmorSet(p);}
    private static void Abilities(Player p)
    {
        Clear();Targets(p);var s=p.GetModPlayer<AscendantEquipmentPlayer>();Player.HurtInfo enemy=new(){Damage=20,DamageSource=PlayerDeathReason.ByNPC(0)};
        s.Expert[0]=true;s.PostUpdate();Check(s.Shield==50&&s.Cooldowns[0]==900,"shield50 per15sec");bool immunity=p.immune;int immunityTime=p.immuneTime;
        Check(s.ConsumableDodge(enemy)&&s.Shield==30&&p.immune==immunity&&p.immuneTime==immunityTime,"finite shield without added immunity");
        var spill=Resolve(s,60);Check(spill.Damage==30&&s.Shield==0&&s.ShieldSpeed==180&&!s.ConsumableDodge(spill),"partial absorption and shield break speed");
        s.ResetEffects();s.PostUpdate();s.Expert[0]=true;s.PostUpdate();Check(s.Shield==0,"unequip cannot regenerate shield");
        s.ResetEffects();s.Expert[2]=true;
        Check(Resolve(s,59).Damage==59&&s.Cooldowns[2]==0,"veil threshold");
        Check(Resolve(s,100).Damage==40&&s.Cooldowns[2]==1500,"veil60percent and25sec");
        Check(Resolve(s,100).Damage==100,"veil cannot retrigger");s.Cooldowns[2]=0;Check(Resolve(s,1000).Damage==820,"veil180damage cap");
        s.ResetEffects();s.Expert[1]=true;p.statLife=9000;enemy.Damage=240;s.OnHurt(enemy);s.PostHurt(enemy);
        Check(s.Refund==90&&s.RefundWait==240&&s.Cooldowns[1]==1200,"refund cap/4sec wait/20sec cooldown");
        for(int i=0;i<240;i++)s.PostUpdate();Check(p.statLife==9000,"refund waits");for(int i=0;i<360;i++)s.PostUpdate();Check(p.statLife==9090&&s.Refund==0,"gradual refund");
        s.Cooldowns[1]=0;s.PostHurt(enemy);s.OnHurt(enemy);Check(s.Refund==0,"second injury interrupts refund");
        s.Cooldowns[1]=0;s.PostHurt(new Player.HurtInfo{Damage=240,DamageSource=PlayerDeathReason.ByOther(0)});Check(s.Refund==0,"environment cannot refund");
        Check(!AscendantEquipmentPlayer.EnemyHit(p,new Player.HurtInfo{Damage=240,PvP=true,DamageSource=PlayerDeathReason.ByNPC(0)}),"PvP excluded");
        Projectile hostile=new();hostile.SetDefaults(ProjectileID.WoodenArrowFriendly);hostile.active=true;hostile.owner=0;hostile.whoAmI=0;hostile.friendly=false;hostile.hostile=true;hostile.damage=30;Main.projectile[0]=hostile;
        Player.HurtInfo shotHit=new(){Damage=30,DamageSource=PlayerDeathReason.ByProjectile(0,0)};
        Check(!AscendantEquipmentPlayer.EnemyHit(p,shotHit),"self-origin hostile shot rejected");
        hostile.GetGlobalProjectile<AscendantHostileOrigin>().OnSpawn(hostile,Main.npc[0].GetSource_FromAI());
        Check(AscendantEquipmentPlayer.EnemyHit(p,shotHit),"NPC-origin hostile shot with local owner accepted");
        hostile.trap=true;Check(!AscendantEquipmentPlayer.EnemyHit(p,shotHit),"trap never refunds");hostile.active=false;
        s.ResetEffects();Equip(p,0);s.DewCharges=3;Check(s.TrySkill()&&s.DewCharges==0&&s.DewSpeed==240&&s.Cooldowns[3]==720,"dew active consumes resources");
        Check(Owned().Count(q=>q.ModProjectile is AscendantShot a&&a.Kind==AscendantShotKind.DewSkill)==3,"three bounded dew blades");Check(!s.TrySkill(),"dew cooldown");
        s.ResetEffects();Equip(p,1);s.Heat=99;Check(!s.TrySkill(),"full heat required");s.Heat=100;Check(s.TrySkill()&&s.HeatTime==600&&s.Cooldowns[4]==2100&&s.FurnaceGuard,"furnace10sec then25sec recovery");
        Check(Resolve(s,400).Damage==340&&!s.FurnaceGuard&&Resolve(s,400).Damage==400,"one furnace guard capped60");
        s.ResetEffects();s.PostUpdate();Check(s.HeatTime==0&&s.Cooldowns[4]==2100,"unequip clears benefit not cooldown");
        Equip(p,2);Vector2 position=p.position;int life=p.statLife,mana=p.statMana;Check(s.TrySkill()&&s.MarkTime==360&&s.Cooldowns[5]==1800,"six second mark");
        p.position+=new Vector2(160,0);Check(s.TrySkill()&&p.position==position&&p.statLife==life&&p.statMana==mana&&s.ReturnPower==360&&p.immune==immunity&&p.immuneTime==immunityTime,"native return without stat rollback or immunity");
        Check(!s.CanReturn(p.position+new Vector2(1700,0))&&!s.CanReturn(new Vector2(float.NaN,0)),"finite100tile return bound");
        Log("ASCENDANT_EQUIPMENT_ABILITIES_PASS dew=true furnace=true return=true shield=true refund=true veil=true hostileOrigin=true cooldowns=true");
    }
    private static void Bounds(Player p)
    {
        Clear();Targets(p);Check(AscendantArsenal.Aim(p,new Vector2(float.NaN,0))==p.Center,"finite aim");
        Check(AscendantArsenal.Launch(p.GetSource_Misc("Smoke"),1,0,AscendantShotKind.Swing,p.Center,Vector2.UnitX,10)==-1,"remote owner cannot launch");
        int slot=AscendantArsenal.Launch(p.GetSource_Misc("Smoke"),0,0,AscendantShotKind.DewSkill,p.Center,Vector2.UnitX,10);
        Check(slot>=0&&!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,Main.projectile[slot]),"secondary cannot charge equipment");
        Check(AscendantShot.SegmentHits(new Rectangle(100,100,200,200),new Vector2(140,150),new Vector2(180,150),10),"contained line collision");
        int oldMode=Main.netMode;var s=p.GetModPlayer<AscendantEquipmentPlayer>();Equip(p,2);s.Cooldowns[5]=1000;Vector2 at=p.position;
        try
        {
            Main.netMode=NetmodeID.Server;
            using var spoof=new MemoryStream(new byte[]{0,0});AscendantEquipmentPlayer.ReceivePacket(new BinaryReader(spoof),1);
            Check(p.position==at&&s.MarkTime==0,"spoofed skill identity rejected");
            using var denied=new MemoryStream(new byte[]{0,0});AscendantEquipmentPlayer.ReceivePacket(new BinaryReader(denied),0);
            Check(p.position==at&&s.MarkTime==0&&s.Cooldowns[5]==1000,"server enforces cooldown");
            int[] cds=(int[])s.Cooldowns.Clone();
            using var truncated=new MemoryStream(new byte[]{2,0,0xE8,3});
            AscendantEquipmentPlayer.ReceivePacket(new BinaryReader(truncated),0);
            Check(s.Cooldowns.SequenceEqual(cds),"truncated cooldown snapshot is atomic");
            Main.netMode=NetmodeID.MultiplayerClient;s.DewCharges=1;s.Heat=15;
            using var truncatedResource=new MemoryStream(new byte[]{5,0,3});
            AscendantEquipmentPlayer.ReceivePacket(new BinaryReader(truncatedResource),0);
            Check(s.DewCharges==1&&s.Heat==15,"truncated resource snapshot is atomic");
            using var shorten=new MemoryStream(new byte[]{3,0,0,0,0,0,0,0,0,0,0,0,0,0});AscendantEquipmentPlayer.ReceivePacket(new BinaryReader(shorten),0);
            Check(s.Cooldowns.SequenceEqual(cds),"cooldown snapshots cannot shorten cooldown");
        }
        finally{Main.netMode=oldMode;}
        Log("ASCENDANT_EQUIPMENT_BOUNDARIES_PASS owner=true skillIdentity=true skillCooldown=true recursion=false finite=true");
    }
    private static Player.HurtInfo Resolve(AscendantEquipmentPlayer state,int damage)
    {
        Player.HurtModifiers modifiers=new(){HitDirection=1};
        // Supply the real NPC source before the equipment's final-damage callback executes.
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>info.DamageSource=PlayerDeathReason.ByNPC(0);
        state.ModifyHurt(ref modifiers);return modifiers.ToHurtInfo(damage,0,1,0,true);
    }
    private static void RenderAttack(Player p,int id,GraphicsDevice device,RenderTarget2D render,RenderTarget2D atlas,bool detail)
    {
        Vector2 old=Main.screenPosition;bool begun=false;
        try
        {
            if(!detail&&id%4==0)Check(Owned().Any(q=>q.ModProjectile is AscendantShot s&&s.Kind==AscendantShotKind.Swing&&s.CanDamage()!=false),"live swing contact frame "+id);
            if(!detail&&id%4==3)Check(Owned().Any(q=>q.ModProjectile is AscendantMinion m&&m.CanDamage()!=false),"live minion contact frame "+id);
            // A travelling minion/bolt can legitimately be outside a player-anchored 360px crop.
            // Follow the representative attack; keep native PreDraw and alpha assertions intact.
            Projectile? focus=detail?Owned().FirstOrDefault(q=>q.ModProjectile is AscendantShot shot&&shot.Secondary)
                :id%4==3?Owned().FirstOrDefault(q=>q.ModProjectile is AscendantMinion):Owned().FirstOrDefault();
            focus??=Owned().FirstOrDefault();
            device.SetRenderTarget(render);device.Clear(Color.Transparent);
            Main.screenPosition=(focus?.Center??p.Center)-new Vector2(render.Width/2f,render.Height/2f);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
            foreach(Projectile q in Owned()){Color c=Color.White;q.ModProjectile.PreDraw(ref c);}
            Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);Color[] pixels=new Color[render.Width*render.Height];render.GetData(pixels);
            Check(pixels.Count(c=>c.A>20)>8,"native attack pixels "+id);
            device.SetRenderTarget(atlas);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
            int x=id%4*300,y=id/4*480+(detail?240:0);
            Main.spriteBatch.Draw(render,new Rectangle(x,y+30,300,200),Color.White);
            Utils.DrawBorderString(Main.spriteBatch,$"{id}: "+new Item(AscendantCatalog.Weapon(id)).Name,new Vector2(x+8,y+7),AscendantEquipmentData.Color(id/4),.48f);
            Utils.DrawBorderString(Main.spriteBatch,detail?"SECONDARY / ALTERNATE":"PRIMARY / CONTACT",new Vector2(x+8,y+218),Color.LightGray,.4f);
            Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);
        }
        finally{if(begun)Main.spriteBatch.End();Main.screenPosition=old;}
    }
    private static void RenderArmor(GraphicsDevice device,RenderTarget2D render)
    {
        using var atlas=new RenderTarget2D(device,1200,720,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        device.SetRenderTarget(atlas);device.Clear(new Color(14,21,33));device.SetRenderTarget(null);
        Main.screenPosition=Vector2.Zero;
        for(int tier=0;tier<3;tier++)for(int style=0;style<4;style++)
        {
            device.SetRenderTarget(render);device.Clear(Color.Transparent);
            for(int pose=0;pose<3;pose++)
            {
                Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=pose==1?-1:1};p.ResetEffects();
                p.head=new Item(AscendantCatalog.Armor(tier*6+style)).headSlot;p.body=new Item(AscendantCatalog.Armor(tier*6+4)).bodySlot;p.legs=new Item(AscendantCatalog.Armor(tier*6+5)).legSlot;
                p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);
                Vector2 at=new(pose*110+30,60);p.position=at;Main.PlayerRenderer.DrawPlayer(Main.Camera,p,at,0,Vector2.Zero,0,1.7f);
            }
            device.SetRenderTarget(null);Color[] pixels=new Color[render.Width*render.Height];render.GetData(pixels);
            Check(pixels.Count(c=>c.A>20)>100,"native armor render "+tier+"/"+style);
            device.SetRenderTarget(atlas);bool begun=false;
            try
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
                int x=style*300,y=tier*240;Main.spriteBatch.Draw(render,new Rectangle(x,y+30,300,200),Color.White);
                Utils.DrawBorderString(Main.spriteBatch,new Item(AscendantCatalog.Armor(tier*6+style)).Name,new Vector2(x+8,y+8),AscendantEquipmentData.Color(tier),.6f);
                Main.spriteBatch.End();begun=false;
            }
            finally{if(begun)Main.spriteBatch.End();device.SetRenderTarget(null);}
        }
        string path=Output("ascendant-armor-runtime.png");using(var stream=File.Create(path))atlas.SaveAsPng(stream,atlas.Width,atlas.Height);
        Log("ASCENDANT_ARMOR_RENDER_PASS loadouts=12 poses=3 nativePlayerRenderer=true visiblePixels=true path="+path);
    }
    private static string Output(string name)
    {string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));Directory.CreateDirectory(Path.GetDirectoryName(path)!);return path;}
}
