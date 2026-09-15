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

namespace StarfallThrone.Content.Pantheon.Equipment;

public static partial class PantheonEquipmentValidation
{
    private static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("Pantheon equipment: "+name);}
    private static void Log(string message)=>ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().Logger.Info(message);
    public static void Data()
    {
        Check(PantheonArsenal.Damage.Length==36&&PantheonArsenal.Classes.Length==36,"36 exact weapon definitions");
        for(int id=0;id<36;id++)
        {
            Item item=new(PantheonCatalog.Weapon(id));Check(item.ModItem is PantheonWeapon&&item.damage==PantheonArsenal.Damage[id]&&item.DamageType==PantheonArsenal.Class(PantheonArsenal.Classes[id]),"weapon defaults "+id);Tooltip(item);
            int boss=PantheonArsenal.Boss(id),tier=PantheonCatalog.Tier(boss);Recipe(item.type,boss,tier,12,4);
            if(PantheonArsenal.Classes[id]==1)Check(item.useAmmo!=AmmoID.None,"native ammo "+id);
            if(PantheonArsenal.Classes[id]>=2)Check(item.mana>0,"native mana "+id);
            if(PantheonArsenal.Classes[id]==3){Projectile q=new();q.SetDefaults(item.shoot);Check(q.minion&&q.minionSlots==2&&q.DamageType==DamageClass.Summon&&ProjectileID.Sets.MinionSacrificable[q.type],"native minion slots "+id);}
        }
        for(int tier=0;tier<6;tier++)for(int style=0;style<4;style++)
        {
            Player p=new();p.ResetEffects();int life=p.statLifeMax2,total=0;int[] ids={tier*6+style,tier*6+4,tier*6+5};
            for(int slot=0;slot<3;slot++){Item item=new(PantheonCatalog.Armor(ids[slot]));p.armor[slot]=item;total+=item.defense;Check(slot==0?item.headSlot>=0:slot==1?item.bodySlot>=0:item.legSlot>=0,"native equip slot "+ids[slot]);Tooltip(item);item.ModItem.UpdateEquip(p);Recipe(item.type,PantheonEquipmentData.ArmorBoss[tier],tier,slot==0?10:slot==1?18:14,slot==1?6:4);}
            Check(total==PantheonEquipmentData.Defense(tier,style)&&p.statLifeMax2-life==PantheonEquipmentData.Life[tier],"armor totals "+tier+"/"+style);
            Check(p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"matching set");p.armor[0].ModItem.UpdateArmorSet(p);Check(PantheonEquipmentData.EquippedTier(p)==tier&&p.GetModPlayer<PantheonEquipmentPlayer>().SetTier==tier,"set registration");p.armor[2]=new Item(ItemID.WoodGreaves);Check(PantheonEquipmentData.EquippedTier(p)==-1,"mixed set rejected");
        }
        for(int id=0;id<17;id++){Item item=new(PantheonCatalog.Expert(id));Player p=new();p.ResetEffects();item.ModItem.UpdateAccessory(p,false);Tooltip(item);Check(item.expert&&item.accessory&&p.GetModPlayer<PantheonEquipmentPlayer>().Expert[id],"expert "+id);Check(!item.ModItem.CanAccessoryBeEquippedWith(item,item,p),"duplicate accessory "+id);}
        var state=new Player().GetModPlayer<PantheonEquipmentPlayer>();for(int i=0;i<24;i++)state.Cooldowns[i]=100+i;state.Stance=2;TagCompound tag=new();state.SaveData(tag);using var stream=new MemoryStream();TagIO.ToStream(tag,stream);stream.Position=0;var copy=new Player().GetModPlayer<PantheonEquipmentPlayer>();copy.LoadData(TagIO.FromStream(stream));Check(copy.Cooldowns.SequenceEqual(state.Cooldowns)&&copy.Stance==2,"binary cooldown save");state.ResetEffects();state.OnEnterWorld();Check(copy.Cooldowns.SequenceEqual(state.Cooldowns),"unequip/world entry persistence");state.UpdateDead();Check(state.Cooldowns.Select((x,i)=>x==copy.Cooldowns[i]||x==copy.Cooldowns[i]-1).All(x=>x),"death persistence");
        Log("PANTHEON_EQUIPMENT_DATA_PASS weapons=36 armor=36 loadouts=24 experts=17 recipes=72 nativeSlots=true cooldownPersistence=true");
    }
    private static void Tooltip(Item item)=>Check(item.ModItem.Tooltip.Value.Length>24&&item.Name!=item.ModItem.Name,"localized name and detailed tooltip "+item.type);
    private static void Recipe(int type,int boss,int tier,int material,int essence)
    {
        var recipes=Main.recipe.Take(Terraria.Recipe.numRecipes).Where(r=>!r.Disabled&&r.createItem.type==type).ToArray();Check(recipes.Length==1,"single gear recipe "+type);var r=recipes[0];Check(r.requiredItem.Count==2&&r.requiredItem.Any(i=>i.type==PantheonCatalog.Material(boss)&&i.stack==material)&&r.requiredItem.Any(i=>i.type==PantheonCatalog.Essence(tier)&&i.stack==essence)&&r.requiredTile.SequenceEqual(new[]{PantheonCatalog.CraftStation(tier)}),"exact gear recipe "+type);Check(PantheonCatalog.Tier(boss)==tier,"material accessible grade "+type);
    }
    private readonly record struct SavedTile(int X,int Y,bool Has,byte Liquid)
    {public static SavedTile Capture(int x,int y){Tile t=Main.tile[x,y];return new(x,y,t.HasTile,t.LiquidAmount);}public void Restore(){Tile t=Main.tile[X,Y];t.HasTile=Has;t.LiquidAmount=Liquid;}}
    private sealed class Metrics{public int Hits,Ammo,Mana,Peak;public readonly HashSet<ModProjectile> Seen=new();public readonly HashSet<int> Kinds=new();}
    public static void Run()
    {
        Check(Main.gameMenu&&!Main.dedServ&&Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"),"isolated opt-in smoke only");Check(Main.tile.Width>500&&Main.tile.Height>190,"initialized fixture map");
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var shots=(Projectile[])Main.projectile.Clone();var dust=(Dust[])Main.dust.Clone();var items=(Item[])Main.item.Clone();var gore=(Gore[])Main.gore.Clone();var texts=(CombatText[])Main.combatText.Clone();var identities=(int[,])Main.projectileIdentity.Clone();var random=Main.rand;var world=Main.ActiveWorldFileData;
        int mode=Main.netMode,me=Main.myPlayer,gameMode=Main.GameMode,mx=Main.mouseX,my=Main.mouseY,sw=Main.screenWidth,sh=Main.screenHeight;Vector2 screen=Main.screenPosition,zoom=Main.GameViewMatrix.Zoom;
        var tiles=new List<SavedTile>();GraphicsDevice device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;var scissor=device.ScissorRectangle;var blend=device.BlendState;var raster=device.RasterizerState;var depth=device.DepthStencilState;var sampler=device.SamplerStates[0];
        using var render=new RenderTarget2D(device,360,240);using var atlas=new RenderTarget2D(device,1800,2160,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.GameMode=0;Main.rand=new UnifiedRandom(913360);Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);Main.GameViewMatrix.Zoom=Vector2.One;
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i,active=false};for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i,active=false};for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{whoAmI=i,active=false};for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust{active=false};for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item{active=false};for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore{active=false};for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText{active=false};
            for(int x=300;x<500;x++)for(int y=65;y<190;y++){tiles.Add(SavedTile.Capture(x,y));Tile tile=Main.tile[x,y];tile.HasTile=false;tile.LiquidAmount=0;}
            device.SetRenderTarget(atlas);device.Clear(new Color(14,21,33));device.SetRenderTarget(null);
            for(int id=0;id<36;id++)
            {
                Clear();Player p=Fresh();Targets(p);Item item=p.inventory[0];item.SetDefaults(PantheonCatalog.Weapon(id));Metrics metric=new();int casts=PantheonArsenal.Classes[id]==3?1:3;bool primary=false,detail=false;
                for(int cast=0;cast<casts;cast++)
                {
                    Cast(p,item,metric);
                    for(int tick=0;tick<(PantheonArsenal.Classes[id]==3?400:220);tick++)
                    {
                        // Sample after native AI/movement but BEFORE Damage can consume a one-hit
                        // projectile. Fast arrows and their on-hit children may live only one tick.
                        Step(p,metric,Capture);
                        // OnHitNPC may have just created a child not present in Step's initial array.
                        foreach(Projectile q in Owned().ToArray())Capture(q);
                        void Capture(Projectile q)
                        {
                            bool damaging=q.ModProjectile.CanDamage()!=false;
                            if(!primary&&damaging){RenderAttack(id,false,device,render,atlas,q);primary=true;return;}
                            if(!primary||detail)return;
                            bool followup=q.ModProjectile is PantheonShot shot&&(shot.Secondary||shot.Kind==2||shot.Kind==3&&PantheonArsenal.Classes[id]==0);
                            bool later=(q.ModProjectile is PantheonMinion&&tick>=30)||(q.ModProjectile is PantheonShot s&&s.Kind is 3 or 4&&tick>=18);
                            // Weapons without a child graph still get a real second cast/variant.
                            bool needsChild=id is 0 or 1 or 4 or 6 or 8 or 10 or 12 or 14 or 16 or 20 or 21 or 22 or 24 or 25 or 28 or 32 or 33;
                            if(followup||later||!needsChild&&cast>0&&damaging){RenderAttack(id,true,device,render,atlas,q);detail=true;}
                        }
                    }
                }
                Check(primary&&detail,"two native weapon views "+id);Check(metric.Hits>0&&metric.Seen.Count>0,"native damage "+id);Check(metric.Peak<=130&&metric.Seen.Count<300,"bounded graph "+id);
                if(item.useAmmo!=AmmoID.None)Check(metric.Ammo==casts,"native ammo consumed "+id);if(item.mana>0)Check(metric.Mana>0,"native mana consumed "+id);
                if(PantheonArsenal.Classes[id]==3){Check(Owned().Any(q=>q.minion),"minion persists "+id);p.ClearBuff(item.buffType);Step(p,metric);Step(p,metric);Check(!Owned().Any(q=>q.minion),"native buff dismissal "+id);}
                for(int tick=0;tick<500;tick++)Step(p,metric);Check(!Owned().Any(),"natural cleanup "+id);
                Log($"PANTHEON_WEAPON_RUNTIME_PASS id={id} hits={metric.Hits} spawned={metric.Seen.Count} kinds={string.Join(',',metric.Kinds)} ammo={metric.Ammo} mana={metric.Mana} peak={metric.Peak}");
            }
            device.SetRenderTarget(null);using(var file=File.Create(Output("pantheon-weapons-runtime.png")))atlas.SaveAsPng(file,atlas.Width,atlas.Height);Log("PANTHEON_WEAPONS_RENDER_PASS weapons=36 views=72 nativePreDraw=true liveAttackFrames=true");
            Abilities(Fresh());Bounds(Fresh());ShieldSnapshotRegression();MeleeTimingRegression();RenderArmor(device,render);
            Log("PANTHEON_EQUIPMENT_RUNTIME_PASS weapons=36 nativeAI=true nativeDamage=true ammo=true mana=true minionDismissal=true cooldowns=true");
        }
        finally
        {
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(shots,Main.projectile,shots.Length);Array.Copy(dust,Main.dust,dust.Length);Array.Copy(items,Main.item,items.Length);Array.Copy(gore,Main.gore,gore.Length);Array.Copy(texts,Main.combatText,texts.Length);Array.Copy(identities,Main.projectileIdentity,identities.Length);foreach(var tile in tiles)tile.Restore();Main.netMode=mode;Main.myPlayer=me;Main.GameMode=gameMode;Main.rand=random;Main.ActiveWorldFileData=world;Main.screenPosition=screen;Main.mouseX=mx;Main.mouseY=my;Main.screenWidth=sw;Main.screenHeight=sh;Main.GameViewMatrix.Zoom=zoom;
            device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;device.BlendState=blend;device.RasterizerState=raster;device.DepthStencilState=depth;device.SamplerStates[0]=sampler;
        }
    }
    private static Player Fresh(){Player p=new(){whoAmI=0,active=true,dead=false,selectedItem=0,direction=1,position=new Vector2(5600,1880)};Main.player[0]=p;p.ResetEffects();p.statLife=p.statLifeMax2=20000;p.statMana=p.statManaMax2=20000;p.maxMinions=12;p.MinionAttackTargetNPC=0;return p;}
    private static void Targets(Player p){for(int i=0;i<3;i++){NPC n=new();n.SetDefaults(NPCID.BlueSlime);n.whoAmI=i;n.active=true;n.life=n.lifeMax=1000000000;n.defense=0;n.damage=100;n.knockBackResist=0;n.width=120;n.height=170;n.Center=p.Center+new Vector2(100+i*150,0);n.noGravity=true;n.HitSound=null;n.DeathSound=null;Main.npc[i]=n;}}
    private static IEnumerable<Projectile> Owned()=>Main.projectile.Where(q=>q.active&&q.ModProjectile is PantheonShot or PantheonMinion);
    private static void Clear(){foreach(Projectile q in Main.ActiveProjectiles)q.active=false;}
    private static long Health()=>Main.npc.Where(n=>n.active).Sum(n=>(long)n.life);
    private static void Cast(Player p,Item item,Metrics metric,int? effectiveAnimation=null)
    {
        Main.screenPosition=p.Center-new Vector2(100,100);Main.mouseX=350;Main.mouseY=100;p.itemAnimation=p.itemAnimationMax=effectiveAnimation??item.useAnimation;p.itemTime=p.itemTimeMax=effectiveAnimation??item.useTime;Check(item.ModItem.CanUseItem(p),"usable weapon "+((PantheonWeapon)item.ModItem).Index);
        int type=item.shoot,damage=p.GetWeaponDamage(item),ammoId=0;float speed=item.shootSpeed,kb=item.knockBack;
        if(item.useAmmo!=AmmoID.None){Item ammo=p.inventory[54];ammo.SetDefaults(item.useAmmo==AmmoID.Arrow?ItemID.WoodenArrow:ItemID.MusketBall);ammo.stack=999;Check(p.PickAmmo(item,out type,out speed,out damage,out kb,out ammoId),"PickAmmo");metric.Ammo+=999-ammo.stack;}
        int mana=p.statMana;if(item.mana>0)Check(p.CheckMana(item,-1,true),"CheckMana");metric.Mana+=mana-p.statMana;item.ModItem.Shoot(p,new EntitySource_ItemUse_WithAmmo(p,item,ammoId,"PantheonSmoke"),p.MountedCenter,(Main.MouseWorld-p.MountedCenter).SafeNormalize(Vector2.UnitX)*speed,type,damage,kb);
    }
    private static void Step(Player p,Metrics metric,Action<Projectile>? beforeDamage=null)
    {
        Array.Clear(p.ownedProjectileCounts);foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==0)p.ownedProjectileCounts[q.type]++;for(int b=0;b<p.buffType.Length;b++)if(p.buffType[b]>0)ModContent.GetModBuff(p.buffType[b])?.Update(p,ref b);
        foreach(NPC n in Main.ActiveNPCs)for(int i=0;i<n.immune.Length;i++)if(n.immune[i]>0)n.immune[i]--;Projectile[] live=Owned().ToArray();metric.Peak=Math.Max(metric.Peak,live.Length);
        foreach(Projectile q in live)
        {
            bool fresh=metric.Seen.Add(q.ModProjectile);for(int i=0;i<q.localNPCImmunity.Length;i++)if(q.localNPCImmunity[i]>0)q.localNPCImmunity[i]--;q.AI();if(!q.active)continue;if(q.ModProjectile is PantheonShot s){metric.Kinds.Add(s.Kind);if(fresh)Roundtrip(q);}if(q.ModProjectile.ShouldUpdatePosition())q.position+=q.velocity;beforeDamage?.Invoke(q);long health=Health();q.Damage();if(Health()<health)metric.Hits++;if(q.active&&q.penetrate==0)q.Kill();Check(PantheonArsenal.Finite(q.position)&&PantheonArsenal.Finite(q.velocity),"finite motion");if(q.active&&--q.timeLeft<=0)q.Kill();
        }
        if(p.itemTime>0)p.itemTime--;if(p.itemAnimation>0)p.itemAnimation--;
    }
    private static void Roundtrip(Projectile q)
    {
        using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))q.ModProjectile.SendExtraAI(writer);byte[] bytes=stream.ToArray();stream.Position=0;Projectile copy=new();copy.SetDefaults(q.type);copy.owner=q.owner;copy.ai=(float[])q.ai.Clone();copy.velocity=q.velocity;copy.Center=q.Center;copy.ModProjectile.ReceiveExtraAI(new BinaryReader(stream));using var second=new MemoryStream();using(var writer=new BinaryWriter(second,System.Text.Encoding.UTF8,true))copy.ModProjectile.SendExtraAI(writer);Check(bytes.SequenceEqual(second.ToArray())&&copy.DamageType==q.DamageType,"network projectile roundtrip");
    }
    private static string Output(string name){string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));Directory.CreateDirectory(Path.GetDirectoryName(path)!);return path;}
}
