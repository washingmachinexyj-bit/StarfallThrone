using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Voyage.Equipment;

public static class VoyageEquipmentExpandedValidation
{
    // Ordered snapshot: defense, life, mana, generic, melee, ranged, magic, summon,
    // melee speed, ranged crit, move speed, reduction, regen, minions, sentries, mana cost, ammo, flight.
    private static Player TestPlayer()
    {
        Player p=new Player {active=true,dead=false,whoAmI=Main.myPlayer<Main.maxPlayers && Main.myPlayer>=0?Main.myPlayer:0};
        ResetFrame(p); return p;
    }
    private static void ResetFrame(Player p)
    {
        p.ResetEffects();
        // These belong to adjacent vanilla frame stages rather than ModPlayer.ResetEffects.
        p.lifeRegen=0; p.statLifeMax2=p.statLifeMax; p.statManaMax2=p.statManaMax;
    }
    private static float[] Stats(Player p)
    {
        var s=p.GetModPlayer<VoyageEquipmentPlayer>();
        return new float[]{p.statDefense,p.statLifeMax2,p.statManaMax2,p.GetDamage(DamageClass.Generic).Additive,
            p.GetDamage(DamageClass.Melee).Additive,p.GetDamage(DamageClass.Ranged).Additive,p.GetDamage(DamageClass.Magic).Additive,
            p.GetDamage(DamageClass.Summon).Additive,p.GetAttackSpeed(DamageClass.Melee),p.GetCritChance(DamageClass.Ranged),
            p.moveSpeed,p.endurance,p.lifeRegen,p.maxMinions,p.maxTurrets,p.manaCost,s.AmmoSaving,s.FlightBonus};
    }
    private static void Delta(Player p,float[] before,float[] expected,string label)
    {
        float[] after=Stats(p);
        for(int i=0;i<after.Length;i++) Check(Math.Abs(after[i]-before[i]-expected[i])<.001f,label+" stat="+i+" got="+(after[i]-before[i])+" expected="+expected[i]);
    }
    public static void Validate()
    {
        StatsAndReset(); Cooldowns(); Shields(); Origins();
        ModContent.GetInstance<VoyageEquipmentKeys>().Mod.Logger.Info("VOYAGE_EQUIPMENT_EXPANDED_PASS accessory-stats=28 armor-class=24 frame-reset=52 emblem=5 timer-death-save=16 shield-budget=450 secondary-owner=pass projectile-roundtrip=4");
    }
    private static void StatsAndReset()
    {
        for(int expert=0;expert<2;expert++)
            for(int id=0;id<(expert==1?17:11);id++)
            {
                Player p=TestPlayer(); float[] before=Stats(p),d=new float[18];
                Item item=new Item(expert==1?VoyageCatalog.Expert(id):VoyageEquipmentData.Accessory(id));
                if(expert==1) switch(id)
                {
                    case 0:d[1]=120;break; case 2:d[10]=.18f;d[17]=.12f;break; case 3:d[3]=.15f;break;
                    case 4:d[13]=2;d[7]=.20f;break; case 5:d[0]=20;break; case 8:d[5]=.18f;break;
                    case 9:d[14]=1;break; case 10:d[4]=.18f;d[8]=.08f;break; case 11:d[1]=200;break;
                    case 12:d[0]=28;d[11]=.08f;break; case 13:d[10]=.25f;d[17]=.20f;break;
                    case 14:d[6]=.22f;d[15]=-.12f;break; case 15:d[13]=1;d[14]=1;d[7]=.20f;break;
                    case 16:d[1]=300;d[11]=.10f;break;
                }
                else switch(id)
                {
                    case 0:d[10]=.18f;d[17]=.12f;break;case 1:d[5]=.16f;d[16]=.15f;break;
                    case 2:d[4]=.16f;d[8]=.08f;break;case 3:d[6]=.16f;d[2]=100;break;
                    case 4:d[13]=1;d[7]=.14f;break;case 5:d[1]=160;d[12]=4;break;
                    case 6:d[0]=18;d[11]=.05f;break;case 9:d[1]=200;d[0]=20;d[11]=.06f;d[12]=4;break;
                    case 10:d[10]=.18f;break;
                }
                for(int frame=0;frame<2;frame++)
                {
                    if(frame>0)ResetFrame(p);
                    item.ModItem.UpdateAccessory(p,false);
                    Delta(p,before,d,"Accessory "+expert+"/"+id+" frame"+frame);
                    var s=p.GetModPlayer<VoyageEquipmentPlayer>();
                    Check((expert==1?s.Expert:s.Ordinary)[id],"Accessory flag");
                    Check(s.Hover==(expert==1?id==7:id==10),"Hover flag");
                    Check(p.noKnockback==(expert==1 && id==5),"Knockback flag");
                }
                ResetFrame(p);Delta(p,before,new float[18],"Accessory removal");
                var reset=p.GetModPlayer<VoyageEquipmentPlayer>();
                Check(!reset.Expert.Any(x=>x)&&!reset.Ordinary.Any(x=>x)&&!reset.Hover,"Accessory flags reset");
            }
        int[] life={320,380,450,530,620,720},gen={20,26,32,38,44,50},dr={8,9,10,11,12,14},cls={20,22,25,28,32,36},
            mana={80,100,120,150,180,220},crit={6,7,8,9,10,12},minions={2,2,3,3,4,4},sentries={0,1,1,1,1,2};
        int[,] defense={{120,108,98,94},{140,126,114,110},{162,146,132,126},{188,170,154,146},{216,196,178,168},{244,222,202,190}};
        for(int tier=0;tier<6;tier++)for(int style=0;style<4;style++)
        {
            Player p=TestPlayer();float[] before=Stats(p),d=new float[18];
            d[0]=defense[tier,style];d[1]=life[tier];d[3]=gen[tier]*.01f;d[11]=dr[tier]*.01f;d[4+style]=cls[tier]*.01f;
            if(style==0)d[8]=.08f+tier*.02f;
            if(style==1){d[9]=crit[tier];d[16]=.10f+tier*.02f;}
            if(style==2){d[2]=mana[tier];d[15]=-.08f-tier*.02f;}
            if(style==3){d[13]=minions[tier];d[14]=sentries[tier];}
            for(int frame=0;frame<2;frame++)
            {
                if(frame>0)ResetFrame(p);
                int[] parts={tier*6+style,tier*6+4,tier*6+5};
                for(int n=0;n<3;n++){p.armor[n]=new Item(VoyageEquipmentData.Armor(parts[n]));p.statDefense+=p.armor[n].defense;p.armor[n].ModItem.UpdateEquip(p);}
                p.armor[0].ModItem.UpdateArmorSet(p);Delta(p,before,d,"Armor "+tier+"/"+style+" frame"+frame);
            }
            foreach(int slot in new[]{0,1,2})p.armor[slot].TurnToAir();
            ResetFrame(p);Delta(p,before,new float[18],"Armor removal");
            Check(p.GetModPlayer<VoyageEquipmentPlayer>().SetTier==-1&&!p.noKnockback,"Set flag removal");
        }
        for(int style=0;style<4;style++)
        {
            Player p=TestPlayer();p.armor[0]=new Item(VoyageEquipmentData.Armor(24+style));
            float[] before=Stats(p),d=new float[18];d[4+style]=.20f;
            if(style==0)d[8]=.08f;if(style==1)d[16]=.15f;if(style==2)d[2]=100;if(style==3)d[13]=1;
            new Item(VoyageEquipmentData.Accessory(8)).ModItem.UpdateAccessory(p,false);Delta(p,before,d,"Emblem "+style);
        }
        int[] types={VoyageEquipmentData.Accessory(7),VoyageCatalog.Expert(7),VoyageEquipmentData.Accessory(10)},times={270,330,360};
        float[] speeds={11.5f,12,14};bool[] hover={false,true,true};
        for(int i=0;i<3;i++)
        {
            Item wing=new Item(types[i]);var data=ArmorIDs.Wing.Sets.Stats[wing.wingSlot];
            Check(data.FlyTime==times[i]&&Math.Abs(data.AccRunSpeedOverride-speeds[i])<.001f&&
                Math.Abs(data.AccRunAccelerationMult-3)<.001f&&data.HasDownHoverStats==hover[i],"Wing full metadata "+i);
            Check(EquipLoader.GetEquipTexture(EquipType.Wings,wing.wingSlot).Texture==wing.ModItem.Texture+"_Wings","Wing texture binding");
            float a=0,b=0,c=0,d=0,e=0;wing.ModItem.VerticalWingSpeeds(TestPlayer(),ref a,ref b,ref c,ref d,ref e);
            Check(a>0&&b>0&&c>0&&d>0&&e>0,"Wing ascent hooks");
        }
        Player passive=TestPlayer();var state=passive.GetModPlayer<VoyageEquipmentPlayer>();float[] old=Stats(passive),extra=new float[18];
        state.Expert[5]=state.Expert[11]=true;state.AnchorTime=120;state.NoHurtTicks=360;extra[11]=.08f;extra[12]=12;
        state.PostUpdateEquips();Delta(passive,old,extra,"Timed defense/regen active");
        ResetFrame(passive);old=Stats(passive);state.PostUpdateEquips();Delta(passive,old,new float[18],"Timed defense/regen unequipped");
    }
    private static void Cooldowns()
    {
        var s=TestPlayer().GetModPlayer<VoyageEquipmentPlayer>();for(int i=0;i<16;i++)s.Cooldowns[i]=360+i*100;
        int[] original=s.Cooldowns.ToArray();s.NoHurtTicks=567;s.Expert[0]=s.Ordinary[0]=true;s.SetTier=5;s.AmmoSaving=.2f;s.FlightBonus=.12f;s.Hover=true;
        s.ResetEffects();Check(s.Cooldowns.SequenceEqual(original)&&!s.Expert[0]&&!s.Ordinary[0]&&s.SetTier==-1&&s.AmmoSaving==0&&s.FlightBonus==0&&!s.Hover,"Unequip preserves16 timers");
        TagCompound tag=new TagCompound();s.SaveData(tag);s.Cooldowns[0]=1;
        Check(tag.GetIntArray("voyageEquipmentCooldowns")[0]==original[0],"Save uses detached array");
        using var bytes=new MemoryStream();TagIO.ToStream(tag,bytes,false);bytes.Position=0;
        var copy=TestPlayer().GetModPlayer<VoyageEquipmentPlayer>();copy.LoadData(TagIO.FromStream(bytes,false));
        Check(copy.Cooldowns.SequenceEqual(original)&&copy.NoHurtTicks==567,"Binary TagIO save/load16 timers");
        copy.PreUpdate();int[] once=copy.Cooldowns.ToArray();for(int i=0;i<16;i++)Check(once[i]==original[i]-1,"Cooldown tick "+i);
        copy.Expert[6]=true;copy.SetTier=5;copy.GelShield=220;copy.ArkShield=450;copy.ArkTime=300;copy.FortifyTime=30;
        copy.UpdateDead();Check(copy.Cooldowns.SequenceEqual(once),"Death preserves timers without double tick");
        Check(!copy.Expert.Any(x=>x)&&copy.SetTier==-1&&copy.EffectiveShield==0&&copy.ArkTime==0&&copy.FortifyTime==0,"Death clears transient effects");
        copy.UpdateDead();Check(copy.Cooldowns.SequenceEqual(once),"Repeated death hook same frame");
        var clone=TestPlayer().GetModPlayer<VoyageEquipmentPlayer>();copy.CopyClientState(clone);copy.Cooldowns[0]=3;
        Check(clone.Cooldowns[0]==once[0],"Client-state array does not alias");
        var empty=TestPlayer().GetModPlayer<VoyageEquipmentPlayer>();empty.LoadData(new TagCompound());Check(empty.Cooldowns.All(x=>x==0),"Legacy save");
        empty.LoadData(new TagCompound{{"voyageEquipmentCooldowns",new int[]{-5,9000}},{"voyageShieldRecharge",2000}});
        Check(empty.Cooldowns[0]==0&&empty.Cooldowns[1]==7200&&empty.NoHurtTicks==720,"Bad save values bounded");
    }
    private static Player.HurtInfo Resolve(VoyageEquipmentPlayer state,int damage,int direction=0)
    {
        Player.HurtModifiers m=new Player.HurtModifiers {HitDirection=direction};state.ModifyHurt(ref m);return m.ToHurtInfo(damage,0,1,0,true);
    }
    private static void Shields()
    {
        Player p=TestPlayer();var s=p.GetModPlayer<VoyageEquipmentPlayer>();s.Expert[0]=true;s.SetTier=5;s.GelShield=220;s.ArkShield=450;s.ArkTime=300;
        Check(s.EffectiveShield==450,"Maximum shield not670");
        var first=Resolve(s,300);Check(s.ConsumableDodge(first)&&s.EffectiveShield==150&&s.GelShield==0,"Full hit consumes both overlapping capacities");
        var second=Resolve(s,200);Check(second.Damage==50&&!s.ConsumableDodge(second)&&s.EffectiveShield==0,"500 damage leaks50, not additive shielding");
        s.GelShield=220;s.ArkShield=450;s.ArkTime=0;Check(s.EffectiveShield==220,"Expired Ark ignored");
        s.ResetEffects();Check(s.EffectiveShield==0,"Unequipped shields ignored");s.PostUpdate();Check(s.GelShield==0&&s.ArkShield==0,"Unequip clears stored capacities");
        s.SetTier=4;s.FortifyTime=30;s.FortifyDirection=Vector2.UnitX;
        var rear=Resolve(s,1000,1);Check(rear.Damage==1000,"Directional guard does not block rear hit");s.OnHurt(rear);Check(s.FortifyTime==30,"Rear hit does not consume successful-guard window");
        var hit=Resolve(s,1000,-1);Check(hit.Damage is >=599 and <=600,"40% forward fortification damage calculation");s.OnHurt(hit);Check(s.FortifyTime==0,"Guard consumed");
        s.Expert[12]=true;s.SteadyReady=true;hit=Resolve(s,1000);Check(hit.Damage is >=849 and <=850,"15% steadiness damage calculation");s.OnHurt(hit);
        Check(!s.SteadyReady&&s.Cooldowns[VoyageEquipmentPlayer.SteadySlot]==480,"Steady cooldown");
        s.ResetEffects();s.Expert[6]=true;p.statLife=0;bool sound=true,dust=true;PlayerDeathReason reason=PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("isolated equipment test"));
        Check(!s.PreKill(1000,1,false,ref sound,ref dust,ref reason)&&p.statLife==1&&s.Cooldowns[7]==7200,"Lethal protection once");
        p.statLife=0;Check(s.PreKill(1000,1,false,ref sound,ref dust,ref reason),"Lethal protection cooldown");
    }
    private static Projectile Shot(int type,int owner)
    {
        Projectile p=new Projectile();p.SetDefaults(type);p.owner=owner;p.active=true;p.friendly=true;p.hostile=false;return p;
    }
    private static void Origins()
    {
        Player p=TestPlayer();var state=p.GetModPlayer<VoyageEquipmentPlayer>();
        Projectile primary=Shot(ModContent.ProjectileType<VoyageShot>(),p.whoAmI);primary.ai[0]=6;primary.ai[1]=(int)VoyageShotKind.Pulse;primary.ai[2]=0;
        Check(!VoyageEquipmentProjectileOrigin.Secondary(primary),"Weapon primary");
        Projectile proc=Shot(ModContent.ProjectileType<VoyageShot>(),p.whoAmI);proc.ai[0]=6;proc.ai[1]=(int)VoyageShotKind.Shard;proc.ai[2]=100;
        // LaunchProc writes variant100. Reproduce its exact projectile metadata without global spawning.
        Check(((IVoyageWeaponProjectile)proc.ModProjectile).Secondary&&VoyageEquipmentProjectileOrigin.Secondary(proc),"LaunchProc contract");
        Check(VoyageEquipmentProjectileOrigin.CanTrigger(p,primary)==(Main.netMode!=NetmodeID.Server&&p.whoAmI==Main.myPlayer),"Runtime authority guard");
        {
            Check(VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Primary own hit");
            Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,proc),"Secondary blocked");
            primary.owner=(p.whoAmI+1)%Main.maxPlayers;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Remote owner blocked");
            primary.owner=p.whoAmI;primary.trap=true;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Trap blocked");primary.trap=false;
            primary.npcProj=true;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"NPC shot blocked");primary.npcProj=false;
            primary.hostile=true;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Hostile shot blocked");primary.hostile=false;
            p.dead=true;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Dead owner blocked");p.dead=false;
            p.active=false;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Inactive owner blocked");p.active=true;
            int owner=p.whoAmI;p.whoAmI=-1;primary.owner=-1;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Negative owner rejected");
            p.whoAmI=Main.maxPlayers;primary.owner=Main.maxPlayers;Check(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,primary),"Out-of-range owner rejected");p.whoAmI=owner;primary.owner=owner;
        }
        foreach(int type in new[]{ModContent.ProjectileType<VoyageObserver>(),ModContent.ProjectileType<VoyageAnchorSentry>()})
            Check(VoyageEquipmentProjectileOrigin.Secondary(Shot(type,p.whoAmI)),"Ally Secondary contract");
        Projectile child=Shot(ProjectileID.WoodenArrowFriendly,p.whoAmI);
        child.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().OnSpawn(child,proc.GetSource_FromThis());Check(VoyageEquipmentProjectileOrigin.Secondary(child),"Secondary vanilla child");
        Projectile grandchild=Shot(ProjectileID.WoodenArrowFriendly,p.whoAmI);
        grandchild.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().OnSpawn(grandchild,child.GetSource_FromThis());Check(VoyageEquipmentProjectileOrigin.Secondary(grandchild),"Secondary grandchild");
        Projectile sentry=Shot(ModContent.ProjectileType<VoyageAnchorSentry>(),p.whoAmI),sentryChild=Shot(ProjectileID.WoodenArrowFriendly,p.whoAmI);
        sentryChild.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().OnSpawn(sentryChild,sentry.GetSource_FromThis());
        Check(sentryChild.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromSentry,"Sentry child flag");
        state.Expert[9]=true;NPC.HitModifiers damage=new NPC.HitModifiers();state.ModifyHitNPCWithProj(sentryChild,new NPC(),ref damage);
        Check(Math.Abs(damage.SourceDamage.ApplyTo(100)-125)<.001f,"Actual sentry child25% bonus");
        Projectile other=Shot(ProjectileID.WoodenArrowFriendly,(p.whoAmI+1)%Main.maxPlayers);other.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().OnSpawn(other,sentry.GetSource_FromThis());
        Check(!other.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromSentry&&VoyageEquipmentProjectileOrigin.Secondary(other),"Cross-owner ancestry rejected");
        for(int style=0;style<4;style++)
        {
            Projectile effect=Shot(ModContent.ProjectileType<VoyageEquipmentPulse>(),p.whoAmI);effect.ai[0]=2;effect.ai[1]=style;effect.ModProjectile.OnSpawn(p.GetSource_Misc("EquipmentTest"));effect.timeLeft=173;
            Check(VoyageEquipmentProjectileOrigin.Secondary(effect),"Own effect secondary");
            using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))effect.ModProjectile.SendExtraAI(writer);
            Projectile received=Shot(ModContent.ProjectileType<VoyageEquipmentPulse>(),p.whoAmI);received.ai[0]=2;received.ai[1]=style;bytes.Position=0;
            using(var reader=new BinaryReader(bytes,System.Text.Encoding.UTF8,true))received.ModProjectile.ReceiveExtraAI(reader);
            Check(received.timeLeft==173&&received.DamageType==VoyageEquipmentData.Class(style)&&!received.tileCollide&&received.penetrate==-1&&received.ModProjectile.CanDamage()==false,"Proc network restoration "+style);
            Projectile equipmentChild=Shot(ProjectileID.WoodenArrowFriendly,p.whoAmI);equipmentChild.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().OnSpawn(equipmentChild,effect.GetSource_FromThis());
            Check(equipmentChild.GetGlobalProjectile<VoyageEquipmentProjectileOrigin>().FromEquipment&&VoyageEquipmentProjectileOrigin.Secondary(equipmentChild),"Equipment descendants excluded");
        }
        Check(VoyageEquipmentPulse.Spawn(p,new Vector2(float.NaN,0),Vector2.Zero,100,0,0,-1)==-1,"Nonfinite proc rejected");
        Check(VoyageEquipmentPulse.Spawn(p,Vector2.Zero,Vector2.Zero,100,9,0,-1)==-1,"Invalid proc kind rejected");
        Check(VoyageEquipmentPulse.Spawn(p,Vector2.Zero,Vector2.Zero,100,0,9,-1)==-1,"Invalid proc class rejected");
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("Voyage expanded equipment validation: "+message);}
}
