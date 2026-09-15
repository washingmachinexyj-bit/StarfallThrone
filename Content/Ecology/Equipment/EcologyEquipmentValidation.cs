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
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Ecology.Equipment;

// Main explicitly invokes Data after recipe registration, and Run only in its isolated menu smoke client.
// This class never launches a build/game, loads a real world, or writes files.
public static class EcologyEquipmentValidation
{
    private static void Check(bool yes,string label){if(!yes)throw new InvalidOperationException("Ecology equipment: "+label);}
    private static void Log(string value)=>ModContent.GetInstance<EcologyWeapon0>().Mod.Logger.Info(value);
    private static void CheckTooltip(ModItem item,string label)
    {
        string value=item.Tooltip.Value;
        // CJK descriptions convey complete mechanics in fewer characters than English.
        // Check real resolved descriptive text, not an English-sized length threshold or digit count:
        // complete English descriptions may legitimately spell out "two" or "three".
        Check(!string.IsNullOrWhiteSpace(value)&&value!=item.Tooltip.Key&&!value.Contains("{$")&&value.Any(char.IsLetter),label+" resolved description");
    }
    public static void Data()
    {
        for(int b=0;b<10;b++)
        {
            for(int style=0;style<4;style++)
            {
                Player p=new();p.ResetEffects();int total=0;
                int[] ids={b*6+style,b*6+4,b*6+5};
                for(int slot=0;slot<3;slot++)
                {
                    Item item=new(EcologyCatalog.Armor(ids[slot]));p.armor[slot]=item;total+=item.defense;
                    Check(item.ModItem is EcologyArmor&&item.defense==EcologyEquipmentData.PieceDefense(b,ids[slot]%6),"armor defense "+ids[slot]);
                    Check(slot==0?item.headSlot>=0:slot==1?item.bodySlot>=0:item.legSlot>=0,"native armor slot "+ids[slot]);
                    Texture2D equip=ModContent.Request<Texture2D>(item.ModItem.Texture+(slot==0?"_Head":slot==1?"_Body":"_Legs")).Value;
                    Check(equip.Width==(slot==1?360:40)&&equip.Height==(slot==1?224:1120),"native equip sheet dimensions "+ids[slot]);
                    item.ModItem.UpdateEquip(p);CheckTooltip(item.ModItem,"armor tooltip "+ids[slot]);
                    Recipe[] recipes=Recipes(item.type);
                    Check(recipes.Length==1&&recipes[0].requiredTile.Contains(EcologyCatalog.StationTile(EcologyCatalog.GearStation(b)))&&recipes[0].Conditions.Count>=2,"advanced armor recipe "+ids[slot]);
                    Check(recipes[0].requiredItem.Any(i=>i.type==EcologyCatalog.Bar(b))&&recipes[0].requiredItem.Any(i=>i.type==EcologyCatalog.Component(b)),"ore uses "+ids[slot]);
                }
                Check(p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"armor set detection");
                p.armor[0].ModItem.UpdateArmorSet(p);
                Check(EcologyEquipmentData.EquippedBiome(p)==b&&p.GetModPlayer<EcologyEquipmentPlayer>().SetBiome==b,"armor set registration");
                if(style==0)Check(total==EcologyEquipmentData.MeleeDefense[b],"melee defense total "+b);
                p.armor[2]=new Item(ItemID.WoodGreaves);
                Check(EcologyEquipmentData.EquippedBiome(p)==-1&&!p.armor[0].ModItem.IsArmorSet(p.armor[0],p.armor[1],p.armor[2]),"mixed set rejected");
            }
            Item expert=new(EcologyCatalog.Expert(b));Player ep=new();ep.ResetEffects();expert.ModItem.UpdateAccessory(ep,false);
            Check(expert.ModItem is EcologyExpert&&expert.expert&&expert.accessory&&ep.GetModPlayer<EcologyEquipmentPlayer>().Expert[b],"expert "+b);
            CheckTooltip(expert.ModItem,"expert tooltip "+b);
        }
        for(int i=0;i<40;i++)
        {
            Item w=new(EcologyCatalog.Weapon(i));
            Check(w.ModItem is EcologyWeapon&&w.damage==EcologyEquipmentData.Damage[i]&&w.DamageType==EcologyEquipmentData.Class(i%4)&&w.noMelee&&w.shoot>0,"weapon defaults "+i);
            CheckTooltip(w.ModItem,"weapon tooltip "+i);
            if(i%4==1)Check(w.useAmmo!=AmmoID.None,"ammo "+i);
            if(i%4>=2)Check(w.mana>0,"mana "+i);
            if(i%4==3)
            {
                Projectile q=new();q.SetDefaults(w.shoot);
                Check(q.ModProjectile is EcologyMinion&&q.minion&&q.minionSlots==1&&q.DamageType==DamageClass.Summon&&ProjectileID.Sets.MinionSacrificable[q.type]&&w.buffType==EcologyArsenal.Buff(i/4),"native minion "+i);
            }
            Recipe[] recipes=Recipes(w.type);
            Check(recipes.Length==2&&recipes.All(r=>r.Conditions.Count>=2),"two bounded weapon recipes "+i);
            Check(recipes.Any(r=>r.requiredItem.Any(t=>t.type==EcologyCatalog.Core(i/4)&&t.stack==16)&&!r.requiredItem.Any(t=>t.type==EcologyCatalog.Bar(i/4))),"direct core craft has no ore cycle "+i);
        }
        var state=new Player().GetModPlayer<EcologyEquipmentPlayer>();for(int i=0;i<state.Cooldowns.Length;i++)state.Cooldowns[i]=100+i;
        state.Assault=true;TagCompound tag=new();state.SaveData(tag);
        using var stream=new MemoryStream();TagIO.ToStream(tag,stream);stream.Position=0;
        var loaded=new Player().GetModPlayer<EcologyEquipmentPlayer>();loaded.LoadData(TagIO.FromStream(stream));
        Check(state.Cooldowns.SequenceEqual(loaded.Cooldowns)&&loaded.Assault,"cooldowns and stance native save");
        state.ResetEffects();Check(state.Cooldowns.SequenceEqual(loaded.Cooldowns),"unequip retains cooldowns");
        loaded.LoadData(new TagCompound{{"ecologyGearCooldowns",new[]{-5,int.MaxValue}}});
        Check(loaded.Cooldowns[0]==0&&loaded.Cooldowns[1]==3600&&loaded.Cooldowns[2]==0,"malformed save bounded");
        Log("ECOLOGY_EQUIPMENT_DATA_PASS weapons=40 armor=60 loadouts=40 experts=10 recipes=140 cooldown-save=true");
    }
    private static Recipe[] Recipes(int type)=>Main.recipe.Take(Recipe.numRecipes).Where(r=>!r.Disabled&&r.createItem.type==type).ToArray();
    private readonly record struct SavedTile(int X,int Y,bool Has,byte Liquid)
    {
        public static SavedTile Capture(int x,int y){Tile t=Main.tile[x,y];return new(x,y,t.HasTile,t.LiquidAmount);}
        public void Restore(){Tile t=Main.tile[X,Y];t.HasTile=Has;t.LiquidAmount=Liquid;}
    }
    private sealed class Metrics
    {
        public int Hits,Peak,Ammo,Mana;
        public readonly HashSet<ModProjectile> Seen=new();
        public readonly HashSet<EcologyShotKind> Kinds=new();
    }
    public static void Run()
    {
        Check(Main.gameMenu&&!Main.dedServ&&Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"),"isolated opt-in menu only");
        Check(Main.tile.Width>500&&Main.tile.Height>180,"native fixture tile grid");
        var players=(Player[])Main.player.Clone();var npcs=(NPC[])Main.npc.Clone();var shots=(Projectile[])Main.projectile.Clone();
        var dust=(Dust[])Main.dust.Clone();var items=(Item[])Main.item.Clone();var gore=(Gore[])Main.gore.Clone();var texts=(CombatText[])Main.combatText.Clone();
        var identities=(int[,])Main.projectileIdentity.Clone();var random=Main.rand;var worldFile=Main.ActiveWorldFileData;
        int mode=Main.netMode,myPlayer=Main.myPlayer,gameMode=Main.GameMode,mx=Main.mouseX,my=Main.mouseY;Vector2 screen=Main.screenPosition;
        var tiles=new List<SavedTile>();
        GraphicsDevice device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;var scissor=device.ScissorRectangle;
        var blend=device.BlendState;var raster=device.RasterizerState;var depth=device.DepthStencilState;var sampler=device.SamplerStates[0];
        using var render=new RenderTarget2D(device,360,240);
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.GameMode=0;Main.rand=new UnifiedRandom(90943);Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("",false);
            for(int i=0;i<Main.player.Length;i++)Main.player[i]=new Player{whoAmI=i,active=false};
            for(int i=0;i<Main.npc.Length;i++)Main.npc[i]=new NPC{whoAmI=i,active=false};
            for(int i=0;i<Main.projectile.Length;i++)Main.projectile[i]=new Projectile{whoAmI=i,active=false};
            for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust{active=false};
            for(int i=0;i<Main.item.Length;i++)Main.item[i]=new Item{active=false};
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore{active=false};
            for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText{active=false};
            for(int x=300;x<500;x++)for(int y=70;y<180;y++)
            {tiles.Add(SavedTile.Capture(x,y));Tile t=Main.tile[x,y];t.HasTile=false;t.LiquidAmount=0;}
            var allKinds=new HashSet<EcologyShotKind>();
            for(int id=0;id<40;id++)
            {
                ClearShots();Player p=Fresh();Item item=p.inventory[0];item.SetDefaults(EcologyCatalog.Weapon(id));Targets(p);
                Metrics metric=new();Cast(p,item,metric);
                for(int n=0;n<420;n++){Step(p,metric);if(n==0)RenderAttacks(p,id,device,render);}
                Check(metric.Hits>0&&metric.Seen.Count>0,"native damage weapon "+id+" hits="+metric.Hits);
                Check(metric.Peak<=24&&metric.Seen.Count<=70,"bounded projectile graph "+id);
                if(item.useAmmo!=AmmoID.None)Check(metric.Ammo==1,"native one ammo per cast "+id);
                if(item.mana>0)Check(metric.Mana>0,"native mana payment "+id);
                if(id%4==3){p.ClearBuff(item.buffType);Step(p,metric);Step(p,metric);}
                for(int n=0;n<180;n++)Step(p,metric);
                Check(!OwnedShots().Any(),"expiry/dismissal "+id);
                foreach(EcologyShotKind kind in metric.Kinds)allKinds.Add(kind);
                Log($"ECOLOGY_WEAPON_RUNTIME_PASS id={id} hits={metric.Hits} spawned={metric.Seen.Count} peak={metric.Peak} ammo={metric.Ammo} mana={metric.Mana}");
            }
            Check(allKinds.Count>=25,"distinct attack geometry");
            GeometryAndBounds(Fresh());Abilities(Fresh());RenderArmor(device,render);
            Log("ECOLOGY_EQUIPMENT_RENDER_PASS weapons=40 armor=60 loadouts=40 poses=3 nativePlayerRenderer=true");
            Log("ECOLOGY_EQUIPMENT_RUNTIME_PASS weapons=40 nativeAI=true nativeDamage=true nativeAmmo=true nativeMana=true bounded=true abilities=true");
        }
        finally
        {
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(shots,Main.projectile,shots.Length);
            Array.Copy(dust,Main.dust,dust.Length);Array.Copy(items,Main.item,items.Length);Array.Copy(gore,Main.gore,gore.Length);Array.Copy(texts,Main.combatText,texts.Length);
            Array.Copy(identities,Main.projectileIdentity,identities.Length);foreach(var t in tiles)t.Restore();
            Main.netMode=mode;Main.myPlayer=myPlayer;Main.GameMode=gameMode;Main.rand=random;Main.ActiveWorldFileData=worldFile;Main.screenPosition=screen;Main.mouseX=mx;Main.mouseY=my;
            device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;device.BlendState=blend;device.RasterizerState=raster;device.DepthStencilState=depth;device.SamplerStates[0]=sampler;
        }
    }
    private static Player Fresh()
    {
        Player p=new(){whoAmI=0,active=true,dead=false,selectedItem=0,direction=1,position=new Vector2(5600,1880)};
        Main.player[0]=p;p.ResetEffects();p.statLife=p.statLifeMax2=10000;p.statMana=p.statManaMax2=10000;p.maxMinions=8;p.MinionAttackTargetNPC=0;return p;
    }
    private static void Targets(Player p)
    {
        for(int i=0;i<3;i++)
        {
            NPC n=new();n.SetDefaults(NPCID.BlueSlime);n.whoAmI=i;n.active=true;n.life=n.lifeMax=10000000;n.defense=0;n.damage=1;n.knockBackResist=0;
            n.width=90;n.height=150;n.Center=p.Center+new Vector2(85+i*165,0);n.noGravity=true;n.HitSound=null;n.DeathSound=null;Main.npc[i]=n;
        }
    }
    private static void ClearShots(){foreach(Projectile q in Main.ActiveProjectiles)q.active=false;}
    private static IEnumerable<Projectile> OwnedShots()=>Main.projectile.Where(q=>q.active&&q.ModProjectile is EcologyShot or EcologyMinion);
    private static long Health()=>Main.npc.Where(n=>n.active).Sum(n=>(long)n.life);
    private static void Cast(Player p,Item item,Metrics metric)
    {
        Main.screenPosition=p.Center-new Vector2(100,100);Main.mouseX=200;Main.mouseY=100;
        p.itemAnimation=p.itemAnimationMax=item.useAnimation;p.itemTime=p.itemTimeMax=item.useTime;
        Check(item.ModItem.CanUseItem(p),"weapon usable");
        int type=item.shoot,damage=p.GetWeaponDamage(item),ammoId=0;float speed=item.shootSpeed,kb=item.knockBack;
        if(item.useAmmo!=AmmoID.None)
        {
            Item ammo=p.inventory[54];ammo.SetDefaults(item.useAmmo==AmmoID.Arrow?ItemID.WoodenArrow:ItemID.MusketBall);ammo.stack=999;
            Check(p.PickAmmo(item,out type,out speed,out damage,out kb,out ammoId),"native PickAmmo");metric.Ammo=999-ammo.stack;
        }
        int mana=p.statMana;if(item.mana>0)Check(p.CheckMana(item,-1,true),"native mana");metric.Mana=mana-p.statMana;
        item.ModItem.Shoot(p,new EntitySource_ItemUse_WithAmmo(p,item,ammoId,"EcologyEquipmentSmoke"),p.MountedCenter,
            (Main.MouseWorld-p.MountedCenter).SafeNormalize(Vector2.UnitX)*speed,type,damage,kb);
    }
    private static void Step(Player p,Metrics metric)
    {
        Array.Clear(p.ownedProjectileCounts);foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==0)p.ownedProjectileCounts[q.type]++;
        for(int b=0;b<p.buffType.Length;b++)if(p.buffType[b]>0)ModContent.GetModBuff(p.buffType[b])?.Update(p,ref b);
        foreach(NPC n in Main.ActiveNPCs)for(int i=0;i<n.immune.Length;i++)if(n.immune[i]>0)n.immune[i]--;
        Projectile[] live=OwnedShots().ToArray();metric.Peak=Math.Max(metric.Peak,live.Length);
        foreach(Projectile q in live)
        {
            bool fresh=metric.Seen.Add(q.ModProjectile);
            for(int i=0;i<q.localNPCImmunity.Length;i++)if(q.localNPCImmunity[i]>0)q.localNPCImmunity[i]--;
            q.AI();if(!q.active)continue;
            if(q.ModProjectile is EcologyShot s){metric.Kinds.Add(s.Kind);if(fresh)Roundtrip(q);}
            if(q.ModProjectile.ShouldUpdatePosition())
            {
                Vector2 wanted=q.velocity,moved=q.tileCollide?Collision.TileCollision(q.position,wanted,q.width,q.height):wanted;q.position+=moved;
                if(wanted!=moved){q.velocity=moved;if(q.ModProjectile.OnTileCollide(wanted))q.Kill();}
            }
            if(!q.active)continue;long hp=Health();q.Damage();if(Health()<hp)metric.Hits++;
            // Native Projectile.Update performs this after Damage, which only decrements penetration.
            if(q.active&&q.penetrate==0)q.Kill();
            Check(float.IsFinite(q.Center.X)&&float.IsFinite(q.Center.Y),"finite projectile");
            if(q.active&&--q.timeLeft<=0)q.Kill();
        }
        if(p.itemTime>0)p.itemTime--;if(p.itemAnimation>0)p.itemAnimation--;
    }
    private static void Roundtrip(Projectile q)
    {
        using var memory=new MemoryStream();using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))q.ModProjectile.SendExtraAI(writer);
        byte[] original=memory.ToArray();memory.Position=0;
        Projectile copy=new();copy.SetDefaults(q.type);copy.owner=q.owner;copy.ai=(float[])q.ai.Clone();copy.velocity=q.velocity;copy.Center=q.Center;
        copy.ModProjectile.ReceiveExtraAI(new BinaryReader(memory));
        using var second=new MemoryStream();using(var writer=new BinaryWriter(second,System.Text.Encoding.UTF8,true))copy.ModProjectile.SendExtraAI(writer);
        Check(original.SequenceEqual(second.ToArray())&&copy.DamageType==q.DamageType&&copy.tileCollide==q.tileCollide,"shot net roundtrip");
    }
    private static void RenderAttacks(Player p,int id,GraphicsDevice device,RenderTarget2D render)
    {
        Vector2 oldScreen=Main.screenPosition;bool begun=false;
        try
        {
            device.SetRenderTarget(render);device.Clear(Color.Transparent);Main.screenPosition=p.Center-new Vector2(90,120);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
            foreach(Projectile q in OwnedShots()){Color c=Color.White;q.ModProjectile.PreDraw(ref c);}
            Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);
            Color[] pixels=new Color[render.Width*render.Height];render.GetData(pixels);
            Check(pixels.Count(c=>c.A>20)>8,"native projectile pixels "+id);
        }
        finally{if(begun)Main.spriteBatch.End();Main.screenPosition=oldScreen;}
    }
    private static void RenderArmor(GraphicsDevice device,RenderTarget2D render)
    {
        Main.screenPosition=Vector2.Zero;
        for(int b=0;b<10;b++)for(int style=0;style<4;style++)
        {
            device.SetRenderTarget(render);device.Clear(Color.Transparent);
            for(int pose=0;pose<3;pose++)
            {
                Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=pose==1?-1:1};p.ResetEffects();
                p.head=new Item(EcologyCatalog.Armor(b*6+style)).headSlot;p.body=new Item(EcologyCatalog.Armor(b*6+4)).bodySlot;p.legs=new Item(EcologyCatalog.Armor(b*6+5)).legSlot;
                p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);
                Vector2 at=new(pose*110+30,60);p.position=at;Main.PlayerRenderer.DrawPlayer(Main.Camera,p,at,0,Vector2.Zero,0,1.7f);
            }
            device.SetRenderTarget(null);Color[] pixels=new Color[render.Width*render.Height];render.GetData(pixels);
            Check(pixels.Count(c=>c.A>20)>100,"native armor render "+b+"/"+style);
        }
    }
    private static void GeometryAndBounds(Player p)
    {
        ClearShots();Rectangle large=new(100,100,200,200);
        Check(EcologyShot.Segment(large,new Vector2(140,160),new Vector2(180,160),12),"contained segment");
        Check(!EcologyShot.Segment(large,new Vector2(10,10),new Vector2(40,10),12),"segment miss");
        Vector2 aim=EcologyArsenal.Aim(p,new Vector2(float.NaN,0));Check(aim==p.Center,"nonfinite aim");
        Check(Vector2.Distance(EcologyArsenal.Aim(p,p.Center+new Vector2(5000,0)),p.Center)<=561,"aim cap");
        Check(EcologyArsenal.Launch(p.GetSource_Misc("Smoke"),1,0,EcologyShotKind.Swing,p.Center,Vector2.UnitX,10)==-1,"remote owner cannot spawn");
        int slot=EcologyArsenal.Launch(p.GetSource_Misc("Smoke"),0,0,EcologyShotKind.EquipmentProc,p.Center,Vector2.UnitX,10);
        Check(slot>=0&&!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,Main.projectile[slot]),"equipment proc cannot recurse");
        var burst=new Projectile();burst.SetDefaults(ModContent.ProjectileType<EcologyShot>());burst.owner=0;burst.ai[0]=5;burst.ai[1]=(float)EcologyShotKind.Burst;burst.Center=p.Center;burst.AI();
        Check(burst.ModProjectile.CanDamage()==false,"wax warning harmless");
        for(int i=0;i<30;i++)burst.AI();Check(burst.ModProjectile.CanDamage()!=false,"wax warning becomes damage");
        Log("ECOLOGY_EQUIPMENT_BOUNDARIES_PASS geometry=true owner=true no-recursion=true telegraph=true network=true");
    }
    private static void Abilities(Player p)
    {
        var state=p.GetModPlayer<EcologyEquipmentPlayer>();state.Expert[8]=true;
        float before=p.GetDamage(DamageClass.Generic).Additive;state.PostUpdateEquips();Check(p.moveSpeed>.19f,"flow speed");
        Check(state.TrySwitchStance()&&state.Assault&&!state.TrySwitchStance(),"stance and switch cooldown");
        state.PostUpdateEquips();Check(Math.Abs(p.GetDamage(DamageClass.Generic).Additive-before-.12f)<.001f,"assault damage");
        state.ResetEffects();Check(state.Cooldowns[20]==120&&!state.TrySwitchStance(),"unequip cannot reset/switch");
        state.Cooldowns[7]=300;state.UpdateDead();Check(state.Cooldowns[7]>=299,"death retains defense cooldown");
        Targets(p);NPC target=Main.npc[0];p.dead=false;state.Expert[6]=true;p.statLife=9000;
        state.RecordHit(target,10);Check(p.statLife==9004&&state.Cooldowns[6]==300,"bounded life-on-hit");
        state.RecordHit(target,10);Check(p.statLife==9004,"same tick cannot heal twice");
        Log("ECOLOGY_EQUIPMENT_ABILITIES_PASS stance=true cooldowns=true healing=true");
    }
}
