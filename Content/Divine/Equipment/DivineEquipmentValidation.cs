#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Divine.Equipment;

/// <summary>Only the main opt-in menu smoke harness calls these methods. Never runs a world or launches a client.</summary>
public static class DivineEquipmentValidation
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Divine equipment: " + message); }
    private static void Log(string line) => ModContent.GetInstance<DivineWeapon0>().Mod.Logger.Info(line);
    public static void Data()
    {
        for (int tier = 0; tier < 3; tier++) for (int style = 0; style < 4; style++)
        {
            Player p = new(); p.ResetEffects(); p.statDefense = new Player.DefenseStat();
            int[] ids = { tier * 6 + style, tier * 6 + 4, tier * 6 + 5 };
            for (int slot = 0; slot < 3; slot++)
            {
                Item item = new(DivineCatalog.Armor(ids[slot])); p.armor[slot] = item;
                Check(item.ModItem is DivineArmor && (slot == 0 ? item.headSlot >= 0 : slot == 1 ? item.bodySlot >= 0 : item.legSlot >= 0), "native armor slot " + ids[slot]);
                p.statDefense += item.defense; item.ModItem.UpdateEquip(p);
                Check(item.ModItem.Tooltip.Value.Length > 5, "armor ability localization " + ids[slot]);
                CheckRecipe(item.type, tier, slot == 0 ? 14 : slot == 1 ? 24 : 16);
            }
            Check(p.armor[0].ModItem.IsArmorSet(p.armor[0], p.armor[1], p.armor[2]), "set detection");
            p.armor[0].ModItem.UpdateArmorSet(p);
            Check(p.statDefense == DivineEquipmentData.Defense[tier, style] && p.GetModPlayer<DivineEquipmentPlayer>().SetTier == tier, "exact armor total " + tier + "/" + style);
        }
        for (int id = 0; id < 12; id++)
        {
            Item item = new(DivineCatalog.Weapon(id)); Check(item.ModItem is DivineWeapon && item.damage == DivineArsenal.Damage[id], "weapon registration/damage " + id);
            Check(item.DamageType == DivineEquipmentData.Class(id % 4) && item.noMelee && item.shoot > 0, "native class/projectile " + id);
            Check(item.ModItem.Tooltip.Value.Length > 20, "weapon tooltip " + id);
            if (id % 4 == 1) Check(item.useAmmo != AmmoID.None, "native ammo " + id);
            if (id % 4 is 2 or 3) Check(item.mana > 0, "native mana " + id);
            if (id % 4 == 3)
            {
                Projectile q = new(); q.SetDefaults(item.shoot);
                Check(q.minion && q.DamageType == DamageClass.Summon && q.minionSlots == (id == 3 ? 1 : 2) && ProjectileID.Sets.MinionSacrificable[q.type] && item.buffType == DivineArsenal.Buff(id / 4), "native minion " + id);
            }
            CheckRecipe(item.type, id / 4, 18);
        }
        for (int id = 0; id < 3; id++)
        {
            Item item = new(DivineCatalog.Expert(id)); Player p = new(); p.ResetEffects(); int life = p.statLifeMax2;
            float damage = p.GetDamage(DamageClass.Generic).Additive; item.ModItem.UpdateAccessory(p, false);
            Check(item.expert && item.accessory && p.GetModPlayer<DivineEquipmentPlayer>().Expert[id] && item.ModItem.Tooltip.Value.Length > 30, "expert registration " + id);
            Check(id < 2 ? p.statLifeMax2 - life == (id == 0 ? 20 : 40) : Math.Abs(p.GetDamage(DamageClass.Generic).Additive - damage - .08f) < .0001, "expert passive " + id);
        }
        var state = new Player().GetModPlayer<DivineEquipmentPlayer>();
        for (int i = 0; i < 5; i++) state.Cooldowns[i] = 100 + i;
        TagCompound tag = new(); state.SaveData(tag); state.ResetEffects();
        using var stream = new MemoryStream(); TagIO.ToStream(tag, stream); stream.Position = 0;
        var copy = new Player().GetModPlayer<DivineEquipmentPlayer>(); copy.LoadData(TagIO.FromStream(stream));
        Check(state.Cooldowns.SequenceEqual(copy.Cooldowns), "binary cooldown save/load");
        int[] before = (int[])state.Cooldowns.Clone(); state.UpdateDead();
        Check(state.Cooldowns.Select((x,i) => x == before[i] || x == before[i] - 1).All(x => x), "death must not reset cooldown");
        state.LoadData(new TagCompound { { "divineEquipmentCooldowns", new[] { -10, int.MaxValue } } });
        Check(state.Cooldowns[0] == 0 && state.Cooldowns[1] == 3600, "invalid save bounds");
        Log("DIVINE_EQUIPMENT_DATA_PASS weapons=12 armor=18 loadouts=12 experts=3 native-slots=true exact-defense=true recipes=true cooldown-save=true");
    }
    private static void CheckRecipe(int type, int tier, int count)
    {
        Recipe[] recipes = Main.recipe.Take(Recipe.numRecipes).Where(r => !r.Disabled && r.createItem.type == type).ToArray();
        Check(recipes.Length == 1 && recipes[0].requiredItem.Count == 1 && recipes[0].requiredItem[0].type == DivineCatalog.Material(tier) &&
            recipes[0].requiredItem[0].stack == count && recipes[0].requiredTile.SequenceEqual(new[] { DivineCatalog.CraftStation(tier) }), "material-only post-victory recipe " + type);
    }
    private sealed class Metrics
    {
        public int Hits, SecondaryHits, Spawned, Ammo, Mana, Peak, Net;
        public readonly HashSet<ModProjectile> Seen = new();
        public readonly HashSet<DivineShotKind> Shapes = new();
    }
    private readonly record struct SavedTile(int X, int Y, ushort Type, bool Has, bool Actuated, bool Half, SlopeType Slope, byte Liquid)
    {
        public static SavedTile Capture(int x, int y) { Tile t = Main.tile[x,y]; return new(x,y,t.TileType,t.HasTile,t.IsActuated,t.IsHalfBlock,t.Slope,t.LiquidAmount); }
        public void Restore() { Tile t = Main.tile[X,Y]; t.TileType=Type;t.HasTile=Has;t.IsActuated=Actuated;t.IsHalfBlock=Half;t.Slope=Slope;t.LiquidAmount=Liquid; }
    }
    public static void Run()
    {
        Check(Main.gameMenu && !Main.dedServ && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated main-menu opt-in only");
        Check(Main.tile.Width > 500 && Main.tile.Height > 180, "initialized fixture terrain");
        var players = (Player[])Main.player.Clone(); var npcs = (NPC[])Main.npc.Clone(); var shots = (Projectile[])Main.projectile.Clone();
        var dust = (Dust[])Main.dust.Clone(); var items = (Item[])Main.item.Clone(); var gore = (Gore[])Main.gore.Clone(); var text = (CombatText[])Main.combatText.Clone();
        var identities = (int[,])Main.projectileIdentity.Clone(); var random = Main.rand; var worldFile = Main.ActiveWorldFileData;
        int mode = Main.netMode, myPlayer = Main.myPlayer, gameMode = Main.GameMode, mx = Main.mouseX, my = Main.mouseY;
        Vector2 screen = Main.screenPosition; var savedTiles = new List<SavedTile>();
        GraphicsDevice device = Main.instance.GraphicsDevice; var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
        var blend = device.BlendState; var raster = device.RasterizerState; var depth = device.DepthStencilState; var sampler = device.SamplerStates[0];
        using var atlas = new RenderTarget2D(device, 1200, 960, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clip = new RasterizerState { ScissorTestEnable = true }; bool batch = false;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.GameMode = 0; Main.rand = new UnifiedRandom(80312);
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("", false);
            for (int i=0;i<Main.player.Length;i++) Main.player[i]=new Player{whoAmI=i,active=false};
            for (int i=0;i<Main.npc.Length;i++) Main.npc[i]=new NPC{whoAmI=i,active=false};
            for (int i=0;i<Main.projectile.Length;i++) Main.projectile[i]=new Projectile{whoAmI=i,active=false};
            for (int i=0;i<Main.dust.Length;i++) Main.dust[i]=new Dust{active=false};
            for (int i=0;i<Main.item.Length;i++) Main.item[i]=new Item{active=false};
            for (int i=0;i<Main.gore.Length;i++) Main.gore[i]=new Gore{active=false};
            for (int i=0;i<Main.combatText.Length;i++) Main.combatText[i]=new CombatText{active=false};
            for (int x=300;x<500;x++) for (int y=70;y<180;y++)
            { savedTiles.Add(SavedTile.Capture(x,y)); Tile t=Main.tile[x,y];t.HasTile=false;t.IsActuated=false;t.IsHalfBlock=false;t.Slope=SlopeType.Solid;t.LiquidAmount=0; }
            device.SetRenderTarget(atlas); device.Clear(new Color(15,20,32));
            var shapes = new HashSet<DivineShotKind>();
            for (int id = 0; id < 12; id++)
            {
                Clear(); Player p = Fresh(); Item item = p.inventory[0]; item.SetDefaults(DivineCatalog.Weapon(id)); Dummies(p); SetAim(p);
                if (id == 4) Equip(p, 1); // Prove a real Projectile.Damage hit, not only direct hook calls, builds furnace heat.
                Metrics metric = new(); int casts = id == 0 ? 3 : id == 4 ? 5 : id == 5 ? 6 : id is 1 or 10 ? 4 : id is 6 or 8 or 9 ? 2 : 1;
                bool captured = false;
                for (int cast=0;cast<casts;cast++)
                {
                    Cast(p,item,metric);
                    int duration = id % 4 == 3 ? 250 : id == 10 ? 25 : id == 2 ? 160 : 160;
                    for (int tick=0;tick<duration;tick++)
                    {
                        p.channel = id == 2 && tick < 65; Step(p, metric);
                        if (!captured && tick == (id is 1 or 5 or 6 ? 1 : id == 2 ? 72 : id % 4 == 3 ? 39 : 8))
                        { DrawCell(id,p,item,metric,device,clip,ref batch); captured=true; }
                    }
                    if (id == 1)
                    {
                        var bow = p.GetModPlayer<DivineEquipmentPlayer>();
                        Check(bow.ArrowTarget == 0 && bow.ArrowCount == (cast + 1) % 3 && bow.ArrowReady == (cast == 2),
                            $"single-target bow cadence cast={cast} target={bow.ArrowTarget} count={bow.ArrowCount} ready={bow.ArrowReady}");
                        Check(Main.npc[1].life == Main.npc[1].lifeMax && Main.npc[2].life == Main.npc[2].lifeMax,
                            "native one-penetration arrow must not hit the back-row targets");
                    }
                }
                if (id is 4 or 10)
                {
                    if (id == 4) Check(p.GetModPlayer<DivineEquipmentPlayer>().Pressure == 100 && p.GetModPlayer<DivineEquipmentPlayer>().Heat > 0, "five melee hits build actual furnace pressure and armor heat");
                    p.altFunctionUse=2; Cast(p,item,metric); p.altFunctionUse=0;
                    for(int tick=0;tick<180;tick++) Step(p,metric);
                    if (id == 4) Check(p.GetModPlayer<DivineEquipmentPlayer>().Pressure==0 && metric.Shapes.Contains(DivineShotKind.HeatWave), "right-click spends pressure");
                }
                if (id % 4 == 3)
                {
                    p.ClearBuff(item.buffType); Step(p,metric); Step(p,metric);
                    Check(!Shots().Any(q=>q.ModProjectile is DivineMinion), "dismissal kills minion " + id);
                }
                else for(int tick=0;tick<180;tick++) Step(p,metric);
                Check(captured && metric.Hits>0 && metric.Spawned>0, $"weapon {id} actual hit/render (hits={metric.Hits})");
                Check(metric.Peak<=30 && metric.Spawned<=80, "bounded weapon graph " + id);
                if(item.useAmmo!=AmmoID.None) Check(metric.Ammo==casts, "one ammunition per shot "+id);
                if(item.mana>0) Check(metric.Mana>0, "native mana payment "+id);
                if(id==0) Check(metric.Shapes.Contains(DivineShotKind.Crescent), "third actual melee hit crescent");
                if(id==1) Check(metric.Shapes.Contains(DivineShotKind.FallingDew), "empowered arrow drops");
                if(id==2) Check(metric.Shapes.Contains(DivineShotKind.WaterRing), "release water ring");
                if(id is 5 or 6) Check(metric.Shapes.Contains(DivineShotKind.Explosion), "mark/light-heavy explosion "+id);
                if(id==8) Check(metric.Shapes.Contains(DivineShotKind.DelayedCut), "delayed slash actual proc");
                if(id==9) Check(metric.Shapes.Contains(DivineShotKind.Echo), "fixed echo actual proc");
                Check(!Shots().Any(q=>q.ModProjectile is DivineShot or DivineMinion), "finite cleanup "+id);
                foreach(var shape in metric.Shapes) shapes.Add(shape);
                Log($"DIVINE_WEAPON_RUNTIME_PASS id={id} hits={metric.Hits} secondary={metric.SecondaryHits} spawned={metric.Spawned} peak={metric.Peak} ammo={metric.Ammo} mana={metric.Mana} net={metric.Net}");
            }
            string output=Output("divine-weapons-runtime.png"); using(var stream=File.Create(output)) atlas.SaveAsPng(stream,atlas.Width,atlas.Height);
            Log("DIVINE_WEAPONS_RENDER_PASS weapons=12 path="+output);
            Abilities(Fresh()); Boundaries(Fresh()); Wear(device);
            Check(shapes.Count>=13,"distinct native attack shapes");
            Log("DIVINE_EQUIPMENT_RUNTIME_PASS weapons=12 nativeAI=true nativeDamage=true nativeAmmo=true nativeMana=true activeSkills=true shieldRefund=true bounds=true");
        }
        finally
        {
            if(batch) Main.spriteBatch.End();
            Array.Copy(players,Main.player,players.Length);Array.Copy(npcs,Main.npc,npcs.Length);Array.Copy(shots,Main.projectile,shots.Length);
            Array.Copy(dust,Main.dust,dust.Length);Array.Copy(items,Main.item,items.Length);Array.Copy(gore,Main.gore,gore.Length);Array.Copy(text,Main.combatText,text.Length);
            Array.Copy(identities,Main.projectileIdentity,identities.Length);foreach(var tile in savedTiles)tile.Restore();
            Main.netMode=mode;Main.myPlayer=myPlayer;Main.GameMode=gameMode;Main.rand=random;Main.ActiveWorldFileData=worldFile;Main.screenPosition=screen;Main.mouseX=mx;Main.mouseY=my;
            device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;device.BlendState=blend;device.RasterizerState=raster;device.DepthStencilState=depth;device.SamplerStates[0]=sampler;
        }
    }
    private static Player Fresh()
    {
        Player p=new(){whoAmI=0,active=true,dead=false,selectedItem=0,direction=1,position=new Vector2(5600,1880)};
        Main.player[0]=p;p.ResetEffects();p.statLife=p.statLifeMax2=10000;p.statMana=p.statManaMax2=10000;p.maxMinions=8;p.MinionAttackTargetNPC=0;
        return p;
    }
    private static void Clear() { foreach(Projectile q in Main.ActiveProjectiles)q.active=false; }
    private static void Dummies(Player p)
    {
        for(int i=0;i<3;i++)
        {
            NPC n=new();n.SetDefaults(NPCID.BlueSlime);n.whoAmI=i;n.active=true;n.life=n.lifeMax=10000000;n.defense=0;n.damage=1;n.knockBackResist=0;
            n.width=90;n.height=150;n.Center=p.Center+new Vector2(85+i*165,0);n.noGravity=true;n.HitSound=null;n.DeathSound=null;Main.npc[i]=n;
        }
    }
    private static void SetAim(Player p) { Main.screenPosition=p.Center-new Vector2(100,100);Main.mouseX=200;Main.mouseY=100; }
    private static void Cast(Player p,Item item,Metrics metric)
    {
        SetAim(p);p.itemAnimation=p.itemAnimationMax=item.useAnimation;p.itemTime=p.itemTimeMax=item.useTime;p.channel=item.channel;
        Check(item.ModItem.CanUseItem(p),"usable cast "+item.type);
        int type=item.shoot,damage=p.GetWeaponDamage(item),ammoId=0;float speed=item.shootSpeed,kb=item.knockBack;
        if(item.useAmmo!=AmmoID.None)
        {
            Item ammo=p.inventory[54];ammo.SetDefaults(item.useAmmo==AmmoID.Arrow?ItemID.WoodenArrow:ItemID.MusketBall);ammo.stack=999;
            Check(p.PickAmmo(item,out type,out speed,out damage,out kb,out ammoId),"native ammunition selection");metric.Ammo+=999-ammo.stack;
        }
        int mana=p.statMana;if(item.mana>0)Check(p.CheckMana(item,-1,true),"native mana deduction");metric.Mana+=mana-p.statMana;
        item.ModItem.UseItem(p);
        item.ModItem.Shoot(p,new EntitySource_ItemUse_WithAmmo(p,item,ammoId,"DivineEquipmentSmoke"),p.MountedCenter,
            (Main.MouseWorld-p.MountedCenter).SafeNormalize(Vector2.UnitX)*speed,type,damage,kb);
    }
    private static IEnumerable<Projectile> Shots()=>Main.projectile.Where(q=>q.active);
    private static long Health()=>Main.npc.Where(n=>n.active).Sum(n=>(long)n.life);
    private static void Step(Player p,Metrics metric)
    {
        Array.Clear(p.ownedProjectileCounts);foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==0)p.ownedProjectileCounts[q.type]++;
        for(int b=0;b<p.buffType.Length;b++)if(p.buffType[b]>0)ModContent.GetModBuff(p.buffType[b])?.Update(p,ref b);
        foreach(NPC n in Main.ActiveNPCs)for(int i=0;i<n.immune.Length;i++)if(n.immune[i]>0)n.immune[i]--;
        Projectile[] live=Shots().Where(q=>q.ModProjectile is DivineShot or DivineMinion).ToArray();metric.Peak=Math.Max(metric.Peak,live.Length);
        foreach(Projectile q in live)
        {
            bool fresh=metric.Seen.Add(q.ModProjectile);if(fresh)metric.Spawned++;
            for(int i=0;i<q.localNPCImmunity.Length;i++)if(q.localNPCImmunity[i]>0)q.localNPCImmunity[i]--;
            q.AI();if(!q.active)continue;
            if(q.ModProjectile is DivineShot s)
            {
                metric.Shapes.Add(s.Kind);
                if(fresh){Roundtrip(q);metric.Net++;}
                Check(s.Kind!=DivineShotKind.Charge || s.CanDamage()==false,"charge cannot deal contact damage");
            }
            if(q.ModProjectile.ShouldUpdatePosition())
            {
                Vector2 wanted=q.velocity,moved=q.tileCollide?Collision.TileCollision(q.position,wanted,q.width,q.height):wanted;q.position+=moved;
                if(wanted!=moved){q.velocity=moved;if(q.ModProjectile.OnTileCollide(wanted))q.Kill();}
            }
            if(!q.active)continue;
            long before=Health();q.Damage();if(Health()<before){metric.Hits++;if(q.ModProjectile is DivineShot hit && hit.Secondary)metric.SecondaryHits++;}
            // Projectile.Damage invokes OnHitNPC and decrements penetration, but vanilla's outer
            // Projectile.Update owns the subsequent penetrate==0 despawn. This fixture supplies that
            // outer loop: without it one-hit arrows incorrectly keep flying into later dummy targets,
            // continually resetting the bow's same-target combo (and bullets also over-penetrate).
            if(q.active && q.penetrate==0)q.Kill();
            Check(float.IsFinite(q.Center.X)&&float.IsFinite(q.Center.Y)&&q.timeLeft<=18000,"finite simulation");
            if(q.active&&--q.timeLeft<=0)q.Kill();
        }
        if(p.itemTime>0)p.itemTime--;if(p.itemAnimation>0)p.itemAnimation--;
    }
    private static void Roundtrip(Projectile q)
    {
        using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))q.ModProjectile.SendExtraAI(writer);
        byte[] expected=bytes.ToArray();bytes.Position=0;Projectile copy=new();copy.SetDefaults(q.type);copy.owner=q.owner;copy.ai=(float[])q.ai.Clone();copy.velocity=q.velocity;
        copy.ModProjectile.ReceiveExtraAI(new BinaryReader(bytes));
        using var again=new MemoryStream();using(var writer=new BinaryWriter(again,System.Text.Encoding.UTF8,true))copy.ModProjectile.SendExtraAI(writer);
        Check(expected.SequenceEqual(again.ToArray())&&copy.DamageType==q.DamageType&&copy.tileCollide==q.tileCollide&&copy.penetrate==q.penetrate&&copy.timeLeft==q.timeLeft,"projectile network roundtrip");
    }
    private static void Equip(Player p,int tier)
    {
        p.armor[0]=new Item(DivineCatalog.Armor(tier*6));p.armor[1]=new Item(DivineCatalog.Armor(tier*6+4));p.armor[2]=new Item(DivineCatalog.Armor(tier*6+5));
        p.armor[0].ModItem.UpdateArmorSet(p);
    }
    private static Player.HurtInfo Resolve(DivineEquipmentPlayer s,int damage)
    { Player.HurtModifiers modifiers=new(){HitDirection=1};s.ModifyHurt(ref modifiers);return modifiers.ToHurtInfo(damage,0,1,0,true); }
    private static void Abilities(Player p)
    {
        Clear();Dummies(p);var s=p.GetModPlayer<DivineEquipmentPlayer>();
        s.Expert[0]=true;s.PostUpdate();Check(s.Shield==30&&s.Cooldowns[0]==1200,"shield initialization");
        bool immunity=p.immune;int immuneTime=p.immuneTime;var small=Resolve(s,20);
        Check(s.ConsumableDodge(small)&&s.Shield==10&&p.immune==immunity&&p.immuneTime==immuneTime,"finite shield no added immunity");
        var large=Resolve(s,30);Check(large.Damage==20&&!s.ConsumableDodge(large)&&s.Shield==0,"partial shield leakage");
        s.ResetEffects();s.PostUpdate();s.Expert[0]=true;s.PostUpdate();Check(s.Shield==0,"unequip does not refill shield");
        s.ResetEffects();s.Expert[2]=true;
        Check(Resolve(s,59).Damage==59&&s.Cooldowns[2]==0,"doom minimum threshold");
        Check(Resolve(s,100).Damage==60&&s.Cooldowns[2]==2100,"doom forty percent");
        Check(Resolve(s,100).Damage==100,"doom cooldown");s.Cooldowns[2]=0;Check(Resolve(s,1000).Damage==880,"doom cap120");
        s.ResetEffects();s.Expert[1]=true;p.statLife=9000;
        Player.HurtInfo hurt=new(){Damage=240,DamageSource=PlayerDeathReason.ByNPC(0)};
        s.OnHurt(hurt);s.PostHurt(hurt);Check(s.Refund==60&&s.RefundWait==360&&s.Cooldowns[1]==1200,"hostile hit refund cap");
        for(int i=0;i<360;i++)s.PostUpdate();Check(p.statLife==9000,"refund waits six seconds");
        for(int i=0;i<360;i++)s.PostUpdate();Check(p.statLife==9060&&s.Refund==0,"gradual native life refund");
        s.Cooldowns[1]=0;s.PostHurt(hurt);s.OnHurt(hurt);Check(s.Refund==0,"subsequent hit cancels refund");
        s.Cooldowns[1]=0;s.PostHurt(new Player.HurtInfo{Damage=240,DamageSource=PlayerDeathReason.ByOther(0)});Check(s.Refund==0,"environment cannot heal");
        p.statLife=0;s.PostHurt(hurt);Check(s.Refund==0,"fatal damage cannot heal");p.statLife=9000;
        s.ResetEffects();Equip(p,1);s.Heat=99;Check(!s.TrySkill(),"heat must be full");s.Heat=100;
        Check(s.TrySkill()&&s.Heat==0&&s.HeatTime==480&&s.Cooldowns[3]==1920,"eight seconds then24 cooldown");
        float old=p.GetDamage(DamageClass.Generic).Additive;s.PostUpdateEquips();Check(Math.Abs(p.GetDamage(DamageClass.Generic).Additive-old-.18f)<.001,"active18percent");
        Check(!s.TrySkill(),"no skill retrigger");s.ResetEffects();s.PostUpdate();Check(s.HeatTime==0&&s.Cooldowns[3]==1920,"unequip clears benefit not cooldown");
        Equip(p,2);Vector2 original=p.position;int life=p.statLife,mana=p.statMana;immuneTime=p.immuneTime;immunity=p.immune;
        Check(s.TrySkill()&&s.MarkTime==240&&s.Cooldowns[4]==2700,"mark current position");p.position+=new Vector2(180,0);
        Check(s.TrySkill()&&p.position==original&&p.statLife==life&&p.statMana==mana&&p.immuneTime==immuneTime&&p.immune==immunity,"native teleport no rollback/immunity");
        Check(!s.TrySkill(),"mark cooldown");Check(!s.CanReturn(p.position+new Vector2(1700,0)),"return100tile bound");
        Check(!s.CanReturn(new Vector2(float.NaN,0))&&!s.CanReturn(new Vector2(-1,0)),"warp finite/world bounds");
        Guid world=s.MarkWorld;s.MarkWorld=Guid.NewGuid();Check(!s.CanReturn(p.position),"no cross-world return");s.MarkWorld=world;
        Tile tile=Main.tile[(int)p.Center.X/16,(int)p.Center.Y/16];tile.HasTile=true;tile.TileType=TileID.Stone;
        try{Check(!s.CanReturn(p.position),"solid destination rejected");}finally{tile.HasTile=false;}
        s.ResetEffects();s.SetTier=0;s.Calm=480;int slot=DivineArsenal.Launch(p.GetSource_Misc("DivineProcTest"),0,0,DivineShotKind.Swing,p.Center,Vector2.UnitX,25);
        Projectile primary=Main.projectile[slot];s.OnHitNPCWithProj(primary,Main.npc[0],new NPC.HitInfo{Damage=25},25);
        Check(s.Calm==0&&Shots().Any(q=>q.ModProjectile is DivineShot d&&d.Kind==DivineShotKind.DewProc),"calm produces fixed shot");
        Projectile proc=Shots().First(q=>q.ModProjectile is DivineShot d&&d.Kind==DivineShotKind.DewProc);int before=Shots().Count();s.Calm=480;
        s.OnHitNPCWithProj(proc,Main.npc[0],new NPC.HitInfo{Damage=18},18);proc.ModProjectile.OnHitNPC(Main.npc[0],new NPC.HitInfo{Damage=18},18);
        Check(s.Calm==480&&Shots().Count()==before&&!VoyageEquipmentProjectileOrigin.CanTrigger(p,proc),"secondary cannot recursively charge either equipment family");
        // Real Player.Hurt path, in addition to exact modifier arithmetic above.
        s.ResetEffects();s.Expert[0]=true;s.Shield=30;p.immune=false;p.immuneTime=0;Array.Clear(p.hurtCooldowns);p.statLife=9000;
        p.Hurt(PlayerDeathReason.ByNPC(0),100,1);
        Check(p.statLife<9000&&p.statLife>8800&&s.Shield==0,"native Player.Hurt invokes partial shield");
        Log("DIVINE_EQUIPMENT_ABILITIES_PASS shield=30 refund=60 doom-cap=120 heat=true nativeTeleport=true nativeHurt=true noRecursiveProcs=true");
    }
    private static void Boundaries(Player p)
    {
        Clear();Dummies(p);var s=p.GetModPlayer<DivineEquipmentPlayer>();var source=p.GetSource_Misc("DivineBoundaries");
        int old=Main.myPlayer;Main.myPlayer=1;
        try{Check(DivineArsenal.Launch(source,0,1,DivineShotKind.Arrow,p.Center,Vector2.UnitX,10)==-1,"remote cannot spawn owner attack");}finally{Main.myPlayer=old;}
        Check(DivineArsenal.Launch(source,0,1,DivineShotKind.Arrow,new Vector2(float.NaN),Vector2.Zero,10)==-1,"NaN spawn rejected");
        for(int i=0;i<80;i++)Check(DivineArsenal.Launch(source,0,1,DivineShotKind.Arrow,p.Center,Vector2.UnitX,10)>=0,"bounded spawn budget");
        Check(DivineArsenal.Launch(source,0,1,DivineShotKind.Arrow,p.Center,Vector2.UnitX,10)==-1,"80shot hard cap");Clear();
        int slot=DivineArsenal.Launch(source,0,8,DivineShotKind.DelayedCut,Main.npc[0].Center,Vector2.UnitX,100);var cut=(DivineShot)Main.projectile[slot].ModProjectile;
        for(int i=0;i<20;i++){cut.Projectile.AI();Check(cut.CanDamage()==false,"cut telegraph harmless");}cut.Projectile.AI();Check(cut.CanDamage()!=false,"cut becomes live");
        int prior=Shots().Count();cut.OnHitNPC(Main.npc[0],new NPC.HitInfo{Damage=100},100);Check(Shots().Count()==prior,"cut cannot selfreplicate");Clear();
        Equip(p,2);s.Cooldowns[4]=1000;Vector2 position=p.position;int net=Main.netMode;
        using var transport = new PacketFixture(s.Mod);
        try
        {
            Main.netMode=NetmodeID.Server;
            Packet(new byte[]{0,0},1);Check(p.position==position&&s.Cooldowns[4]==1000,"spoofed player request");
            Packet(new byte[]{0,0},0);Check(p.position==position&&s.MarkTime==0,"server enforces cooldown");
            using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){writer.Write((byte)2);writer.Write((byte)0);for(int i=0;i<5;i++)writer.Write((ushort)0);}
            Packet(stream.ToArray(),0);Check(s.Cooldowns[4]==1000,"client cannot shorten cooldown");
            using var heat=new MemoryStream();using(var writer=new BinaryWriter(heat,System.Text.Encoding.UTF8,true)){writer.Write((byte)4);writer.Write((byte)0);writer.Write((short)0);writer.Write(999999);}
            Packet(heat.ToArray(),0);Check(s.Heat==0,"client cannot award heat without equipped valid attack");
            Packet(new byte[]{0,255},0);Check(p.position==position,"invalid identity ignored");
            Equip(p,1);
            // Preserve the failing fixture exactly; diagnostics below are read-only.
            Projectile witness=new();witness.SetDefaults(ModContent.ProjectileType<DivineShot>());witness.active=true;witness.owner=0;witness.identity=731;
            witness.ai[0]=4;witness.ai[1]=(float)DivineShotKind.Swing;witness.damage=76;witness.Center=p.Center;
            ((DivineShot)witness.ModProjectile).Age=8;Main.projectile[0]=witness;
            SegmentGeometry(p,witness);
            NPC heatTarget=Main.npc[0];int armorTier=DivineEquipmentData.EquippedTier(p);
            bool primary=VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,witness);
            bool? canDamage=witness.ModProjectile.CanDamage();
            Rectangle tolerance=heatTarget.Hitbox;tolerance.Inflate(48,48);
            bool? customCollision=witness.ModProjectile.Colliding(witness.Hitbox,tolerance);
            bool collision=customCollision??witness.Hitbox.Intersects(tolerance);
            bool los=Collision.CanHitLine(witness.Center,1,1,heatTarget.Center,1,1);
            var heatStamp=typeof(DivineEquipmentPlayer).GetField("lastHeatTick",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s);
            bool budget=heatStamp is ulong stamp&&(stamp==ulong.MaxValue||Main.GameUpdateCount-stamp>=6);
            var gates=new List<(string Name,bool Pass,string Values)>
            {
                ("server",Main.netMode==NetmodeID.Server,$"mode={Main.netMode} sender=0 identity=0"),
                ("player",p.active&&!p.dead&&ReferenceEquals(Main.player[0],p),$"active={p.active} dead={p.dead} who={p.whoAmI} registered={ReferenceEquals(Main.player[0],p)}"),
                ("targetChaseable",heatTarget.CanBeChasedBy(),$"active={heatTarget.active} chaseable={heatTarget.chaseable} lifeMax={heatTarget.lifeMax} dontTakeDamage={heatTarget.dontTakeDamage} friendly={heatTarget.friendly} immortal={heatTarget.immortal}"),
                ("armorTier",armorTier==1,$"tier={armorTier} slots={p.armor[0].type}/{p.armor[1].type}/{p.armor[2].type}"),
                ("owner",witness.active&&witness.owner==0&&witness.identity==731&&witness.damage>0&&Shots().Any(q=>ReferenceEquals(q,witness)),$"active={witness.active} owner={witness.owner} identity={witness.identity} damage={witness.damage}"),
                ("primary",primary,$"primary={primary} friendly={witness.friendly} hostile={witness.hostile} npcProj={witness.npcProj} trap={witness.trap} secondary={VoyageEquipmentProjectileOrigin.Secondary(witness)}"),
                ("CanDamage",canDamage!=false,$"value={canDamage?.ToString()??"null"} age={((DivineShot)witness.ModProjectile).Age} kind={((DivineShot)witness.ModProjectile).Kind}"),
                ("collision",collision,$"custom={customCollision?.ToString()??"null"} projectileBox={witness.Hitbox} tolerance={tolerance} mountedCenter={p.MountedCenter} center={p.Center} segmentEnd={p.MountedCenter+witness.rotation.ToRotationVector2()*122} rotation={witness.rotation}"),
                ("LOS",los,$"value={los} from={witness.Center} to={heatTarget.Center}"),
                ("HeatTime",s.HeatTime==0,$"Heat={s.Heat} HeatTime={s.HeatTime}"),
                ("initialHeat",s.Heat==0,$"Heat={s.Heat}"),
                ("awardBudget",budget,$"lastHeatTick={heatStamp} currentTick={Main.GameUpdateCount}")
            };
            foreach(var gate in gates)Log($"DIVINE_HEAT_GATE {gate.Name} pass={gate.Pass} {gate.Values}");
            foreach(var gate in gates)Check(gate.Pass,$"heat pre-packet {gate.Name}: {gate.Values}");
            using var valid=new MemoryStream();using(var writer=new BinaryWriter(valid,System.Text.Encoding.UTF8,true)){writer.Write((byte)4);writer.Write((byte)0);writer.Write((short)0);writer.Write(witness.identity);}
            Packet(valid.ToArray(),0);Check(s.Heat==5,"validated server primary-hit heat evidence");
            Packet(valid.ToArray(),0);Check(s.Heat==5,"server rate-limits repeated heat evidence");
            Check(transport.Frames.Count == 2, "exactly cooldown and heat server acknowledgements");
            Check(transport.Frames.All(frame => frame.Length >= 8 && BitConverter.ToUInt16(frame,0) == frame.Length &&
                frame[2] == 250 && frame[3] == 0 && frame[4] == 41 && frame[6] == 0), "native ModPacket framing and identity");
            Check(transport.Frames[0][5] == 3 && transport.Frames[1][5] == 5 && transport.Frames[1][7] == 5,
                "server serialized cooldown snapshot and validated heat value");
        }
        finally{Main.netMode=net;}
        p.dead=true;int minion=Projectile.NewProjectile(source,p.Center,Vector2.Zero,DivineArsenal.Minion(0),17,0,0);Main.projectile[minion].AI();Check(!Main.projectile[minion].active,"dead owner minion cleanup");p.dead=false;
        Log("DIVINE_EQUIPMENT_BOUNDARIES_PASS owner=true cooldownPackets=true invalidHeat=true warp=true telegraph=true finite=true");
    }
    private static void Packet(byte[] bytes,int sender){using var stream=new MemoryStream(bytes);DivineEquipmentPlayer.ReceivePacket(new BinaryReader(stream),sender);}

    private static void SegmentGeometry(Player p,Projectile swing)
    {
        Vector2 start=p.MountedCenter;
        Rectangle Box(float x,float y,int width,int height)=>new((int)start.X+(int)x,(int)start.Y+(int)y,width,height);
        var shot=(DivineShot)swing.ModProjectile;
        Check(shot.Colliding(swing.Hitbox,Box(-20,-60,180,120))==true,"swing segment wholly contained in large target");
        Check(shot.Colliding(swing.Hitbox,Box(50,-20,20,40))==true,"swing crossing narrow target edges");
        Check(shot.Colliding(swing.Hitbox,Box(160,80,30,30))==false,"swing clear miss retains finite reach");
        Projectile cut=new();cut.SetDefaults(ModContent.ProjectileType<DivineShot>());cut.owner=p.whoAmI;cut.ai[0]=8;cut.ai[1]=(float)DivineShotKind.DelayedCut;cut.Center=start;
        var delayed=(DivineShot)cut.ModProjectile;delayed.Age=21;
        Check(delayed.Colliding(cut.Hitbox,Box(-100,-60,200,120))==true,"delayed cut wholly contained in large target");
        Check(delayed.Colliding(cut.Hitbox,Box(-10,-20,20,40))==true,"delayed cut crossing target edges");
        Check(delayed.Colliding(cut.Hitbox,Box(110,60,30,30))==false,"delayed cut clear miss retains finite reach");
        delayed.Age=20;Check(delayed.Colliding(cut.Hitbox,Box(-100,-60,200,120))==false,"contained delayed cut still harmless during telegraph");
        Log($"DIVINE_SEGMENT_GEOMETRY_PASS swing=contained/edge/miss delayed=contained/edge/miss/telegraph mountedCenter={p.MountedCenter}");
    }

    /// <summary>
    /// Main-menu clients do not have a negotiated Mod.NetID. This fixture supplies the same native
    /// registration table and captures ModPacket.Send through ISocket entirely in memory. It neither
    /// patches production handlers nor opens a listener/connection. Every borrowed global is restored.
    /// </summary>
    private sealed class PacketFixture : IDisposable
    {
        private readonly Mod mod;
        private readonly FieldInfo idField, modsField;
        private readonly object? priorId, priorMods;
        private readonly RemoteClient[] clients;
        private readonly MessageBuffer[] buffers;
        public readonly List<byte[]> Frames = new();
        public PacketFixture(Mod owner)
        {
            Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "packet fixture opt-in only");
            mod=owner;
            idField=typeof(Mod).GetField("netID",BindingFlags.Instance|BindingFlags.NonPublic) ?? throw new InvalidOperationException("Mod.netID test API changed");
            Type netType=typeof(Mod).Assembly.GetType("Terraria.ModLoader.ModNet") ?? throw new InvalidOperationException("ModNet test API changed");
            modsField=netType.GetField("netMods",BindingFlags.Static|BindingFlags.NonPublic) ?? throw new InvalidOperationException("ModNet.netMods test API changed");
            priorId=idField.GetValue(owner);priorMods=modsField.GetValue(null);
            clients=(RemoteClient[])Netplay.Clients.Clone();buffers=(MessageBuffer[])NetMessage.buffer.Clone();
            try
            {
                idField.SetValue(mod,(short)0);modsField.SetValue(null,new[]{mod});
                // A menu client has null buffer entries because no network session initialized them.
                // Give native ModPacket.Send complete temporary buffers, preserving all original
                // references (including nulls) rather than mutating or merely skipping missing ones.
                for(int i=0;i<NetMessage.buffer.Length;i++)
                    NetMessage.buffer[i]=new MessageBuffer{whoAmI=i,broadcast=i==0};
                for(int i=0;i<Netplay.Clients.Length;i++)
                {
                    Netplay.Clients[i]=new RemoteClient{Id=i,Socket=new MemorySocket(Frames,i==0)};
                }
            }
            catch{Dispose();throw;}
        }
        public void Dispose()
        {
            idField.SetValue(mod,priorId);modsField.SetValue(null,priorMods);
            Array.Copy(clients,Netplay.Clients,clients.Length);
            Array.Copy(buffers,NetMessage.buffer,buffers.Length);
        }
    }
    private sealed class MemorySocket : Terraria.Net.Sockets.ISocket
    {
        private readonly List<byte[]> frames;private readonly bool connected;
        public MemorySocket(List<byte[]> frames,bool connected){this.frames=frames;this.connected=connected;}
        public void Close() { }
        public bool IsConnected()=>connected;
        public void Connect(Terraria.Net.RemoteAddress address)=>throw new InvalidOperationException("No real connections in packet fixture");
        public void AsyncSend(byte[] data,int offset,int size,Terraria.Net.Sockets.SocketSendCallback callback,object state)
        { byte[] frame=new byte[size];Array.Copy(data,offset,frame,0,size);frames.Add(frame);callback?.Invoke(state); }
        public void AsyncReceive(byte[] data,int offset,int size,Terraria.Net.Sockets.SocketReceiveCallback callback,object state)
            =>throw new InvalidOperationException("No external receive in packet fixture");
        public bool IsDataAvailable()=>false;
        public void SendQueuedPackets() { }
        public bool StartListening(Terraria.Net.Sockets.SocketConnectionAccepted callback)=>throw new InvalidOperationException("No real listener in packet fixture");
        public void StopListening() { }
        public Terraria.Net.RemoteAddress GetRemoteAddress()=>new Terraria.Net.TcpAddress(System.Net.IPAddress.Loopback,0);
    }
    private static string Output(string name)
    { string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));Directory.CreateDirectory(Path.GetDirectoryName(path)!);return path; }
    private static void DrawCell(int id,Player p,Item item,Metrics metric,GraphicsDevice device,RasterizerState clip,ref bool batch)
    {
        Rectangle cell=new(id%4*300,id/4*320,300,320);device.ScissorRectangle=cell;Main.screenPosition=p.Center-new Vector2(cell.X+60,cell.Y+155);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,clip);batch=true;
        foreach(Projectile q in Main.ActiveProjectiles)if(q.ModProjectile is DivineShot or DivineMinion){Color color=Color.White;q.ModProjectile.PreDraw(ref color);}
        Texture2D texture=ModContent.Request<Texture2D>(DivineArsenal.Art(id)).Value;
        Main.spriteBatch.Draw(texture,new Vector2(cell.X+25,cell.Y+25),null,Color.White,0,Vector2.Zero,.8f,SpriteEffects.None,0);
        Utils.DrawBorderString(Main.spriteBatch,item.Name,new Vector2(cell.X+12,cell.Y+280),DivineEquipmentData.Color(id/4),.55f);
        Main.spriteBatch.End();batch=false;SetAim(p);
    }
    private static void Wear(GraphicsDevice device)
    {
        using var atlas=new RenderTarget2D(device,1200,600);device.SetRenderTarget(atlas);device.Clear(new Color(16,22,36));Main.screenPosition=Vector2.Zero;
        for(int id=0;id<36;id++)
        {
            int tier=id/12,style=id/3%4,pose=id%3;Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=pose==1?-1:1};p.ResetEffects();
            p.head=new Item(DivineCatalog.Armor(tier*6+style)).headSlot;p.body=new Item(DivineCatalog.Armor(tier*6+4)).bodySlot;p.legs=new Item(DivineCatalog.Armor(tier*6+5)).legSlot;
            p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);
            Vector2 at=new(id%12*100+35,id/12*200+50);p.position=at;Main.PlayerRenderer.DrawPlayer(Main.Camera,p,at,0,Vector2.Zero,0,1.7f);
        }
        string output=Output("divine-armor-runtime.png");using(var stream=File.Create(output))atlas.SaveAsPng(stream,atlas.Width,atlas.Height);
        Log("DIVINE_ARMOR_RENDER_PASS loadouts=12 poses=3 nativePlayerRenderer=true path="+output);
    }
}
