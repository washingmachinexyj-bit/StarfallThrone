#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Pantheon.Equipment;

public static partial class PantheonEquipmentValidation
{
    private static void Equip(Player p,int tier){p.armor[0]=new Item(PantheonCatalog.Armor(tier*6));p.armor[1]=new Item(PantheonCatalog.Armor(tier*6+4));p.armor[2]=new Item(PantheonCatalog.Armor(tier*6+5));p.armor[0].ModItem.UpdateArmorSet(p);}
    private static Player.HurtInfo Resolve(PantheonEquipmentPlayer state,int damage)
    {Player.HurtModifiers mods=new(){HitDirection=1};mods.ModifyHurtInfo+=(ref Player.HurtInfo info)=>info.DamageSource=PlayerDeathReason.ByNPC(0);state.ModifyHurt(ref mods);return mods.ToHurtInfo(damage,0,1,0,true);}
    private static void Abilities(Player unused)
    {
        Clear();
        for(int tier=0;tier<6;tier++)
        {
            Player p=Fresh();Targets(p);Equip(p,tier);var s=p.GetModPlayer<PantheonEquipmentPlayer>();bool immune=p.immune;int time=p.immuneTime,life=p.statLife,mana=p.statMana;Vector2 pos=p.position;
            Check(s.TrySkill()&&s.ActiveTier==tier&&s.SkillTime==PantheonEquipmentData.SkillDuration[tier]&&s.Cooldowns[17+tier]==PantheonEquipmentData.SkillCooldown[tier],"server skill tier "+tier);
            Check(!s.TrySkill(),"skill cooldown tier "+tier);Check(p.position==pos&&p.statLife==life&&p.statMana==mana&&p.immune==immune&&p.immuneTime==time,"skill cannot teleport heal or add immunity "+tier);
            if(tier==0||tier==5){int expected=tier==0?300:600;Check(Resolve(s,1000).Damage==1000-expected&&s.SkillShield==0,"finite skill shield "+tier);Check(Resolve(s,1000).Damage==1000,"spent shield "+tier);}
            if(tier==2)Check(Resolve(s,1000).Damage==760,"tier2 per-hit cap240");
            int cd=s.Cooldowns[17+tier];s.ResetEffects();s.PostUpdate();Check(s.SkillTime==0&&s.SkillShield==0&&s.Cooldowns[17+tier]==cd,"unequip skill expiry "+tier);
        }
        for(int id=0;id<17;id++)
        {
            Clear();Player p=Fresh();Targets(p);var s=p.GetModPlayer<PantheonEquipmentPlayer>();new Item(PantheonCatalog.Expert(id)).ModItem.UpdateAccessory(p,false);NPC n=Main.npc[0];
            switch(id)
            {
                case 0:p.velocity=new Vector2(3,0);s.PostUpdate();Check(s.DewShield==180&&s.Cooldowns[0]==900,"dew recharge");Check(Resolve(s,500).Damage==320&&s.DewShield==0,"dew finite absorption");break;
                case 1:for(int i=0;i<10;i++)s.CountHit(n);Check(s.InsightHits==0&&Owned().Count(q=>q.ModProjectile is PantheonShot {Kind:7})==1,"ten-hit insight");s.CountHit(n);s.CountHit(Main.npc[1]);Check(s.InsightHits==1&&s.InsightTarget==1,"insight target switch");break;
                case 2:p.velocity=new Vector2(10,0);s.PostUpdate();Check(s.ShadowTime==120&&s.ShadowShield==160&&s.Cooldowns[2]==600,"dash shadow");Check(Resolve(s,500).Damage==340,"shadow cap");break;
                case 3:s.OnHurt(new Player.HurtInfo{Damage=100,DamageSource=PlayerDeathReason.ByNPC(0)});Check(s.Lucidity==180&&s.Cooldowns[3]==600,"lucidity recovery");break;
                case 4:p.statLife-=1000;for(int i=0;i<30;i++)s.CountHit(n);Check(p.statLife==19000+120&&s.Cooldowns[4]==600,"harvest heal cap");break;
                case 5:p.statLife-=1000;s.OnHurt(new Player.HurtInfo{Damage=1000,DamageSource=PlayerDeathReason.ByNPC(0)});Check(s.LanternHeal==300&&s.Cooldowns[5]==1200,"ferry delayed heal");for(int i=0;i<60;i++)s.AdvanceTimers();s.PostUpdate();Check(p.statLife==19300&&s.LanternHeal==0,"ferry return budget");break;
                case 6:s.OnHurt(new Player.HurtInfo{Damage=300,DamageSource=PlayerDeathReason.ByNPC(0)});Check(s.GateTime==180&&s.GatePosition==p.Center,"gate placed at hurt origin");float move=p.moveSpeed;s.PostUpdateEquips();Check(p.moveSpeed>move+.44f,"gate range movement");break;
                case 7:s.PostUpdate();Check(s.CrystalShield==240,"crystal recharge");Check(Resolve(s,500).Damage==260&&s.CrystalSpeed==240&&Owned().Count()==4,"crystal break retaliation");break;
                case 8:s.SinceHit=0;float damage=p.GetDamage(DamageClass.Generic).Additive;s.PostUpdateEquips();Check(Math.Abs(p.GetDamage(DamageClass.Generic).Additive-damage-.14f)<.001f,"day damage phase");s.SinceHit=600;int regen=p.lifeRegen;s.PostUpdateEquips();Check(p.lifeRegen==regen+8,"night recovery phase");break;
                case 9:Check(Resolve(s,2000).Damage==1700&&s.Cooldowns[9]==900,"scales cap300");Check(Resolve(s,2000).Damage==2000,"scales cooldown");break;
                case 10:for(int i=0;i<20;i++)s.CountHit(n);Check(s.Heat==0&&s.HeatTime==480&&s.Cooldowns[10]==1200&&Owned().Count()==4,"furnace discharge");s.CountHit(n);Check(s.Heat==0,"furnace no gain during cooldown");break;
                case 11:p.statLife-=1000;for(int i=0;i<40;i++)s.CountHit(n);Check(p.statLife==19220&&s.Cooldowns[11]==900,"seed heal cap220");break;
                case 12:for(int i=0;i<90;i++)s.PostUpdate();int defense=p.statDefense;s.PostUpdateEquips();Check(p.statDefense==defense+100,"mountain stable defense");p.velocity=new Vector2(3,0);s.PostUpdate();Check(s.StillTime==0,"mountain movement resets");break;
                case 13:p.velocity=new Vector2(8,0);for(int i=0;i<180;i++)s.PostUpdate();Check(s.StormTime==300&&s.Cooldowns[13]==900,"storm movement charge");break;
                case 14:p.velocity=new Vector2(4,0);for(int i=0;i<12;i++)s.CountHit(n);Check(s.PrismHits==0&&s.Cooldowns[14]==600&&Owned().Count()==1,"eighth color primary proc");break;
                case 15:Check(Resolve(s,1400).Damage==900&&s.Cooldowns[15]==1800,"erased name cap500");Check(Resolve(s,1400).Damage==1400,"erased name cooldown");break;
                case 16:Check(s.TrySkill()&&s.Stance==1&&s.Cooldowns[23]==90,"throne stance server selection");Check(!s.TrySkill(),"throne stance cooldown");for(int i=0;i<90;i++)s.AdvanceTimers();Check(s.TrySkill()&&s.Stance==2,"throne next stance");break;
            }
            int[] before=(int[])s.Cooldowns.Clone();s.ResetEffects();s.PostUpdate();Check(before.SequenceEqual(s.Cooldowns),"expert unequip retains cooldown "+id);
        }
        Player guard=Fresh();Targets(guard);var gs=guard.GetModPlayer<PantheonEquipmentPlayer>();for(int i=0;i<17;i++)gs.Expert[i]=true;gs.DewShield=180;gs.ShadowShield=160;gs.ShadowTime=120;gs.CrystalShield=240;Check(Resolve(gs,20).Damage==1,"stacked protection never creates immunity");
        Log("PANTHEON_EQUIPMENT_ABILITIES_PASS skills=6 experts=17 finiteShields=true cooldowns=true healsCapped=true noTeleport=true noRevive=true");
    }
    private static void Bounds(Player p)
    {
        Clear();Targets(p);var s=p.GetModPlayer<PantheonEquipmentPlayer>();Check(PantheonArsenal.Aim(p,new Vector2(float.NaN,0))==p.Center,"finite aim");Check(PantheonArsenal.Launch(p.GetSource_Misc("Smoke"),1,0,0,p.Center,Vector2.One,1)==-1,"remote launch denied");Check(PantheonArsenal.Launch(p.GetSource_Misc("Smoke"),0,36,0,p.Center,Vector2.One,1)==-1,"invalid weapon denied");
        s.Expert[1]=true;s.RecordHit(Main.npc[0],0);Check(s.InsightHits==0,"zero damage cannot charge");Main.npc[0].friendly=true;s.RecordHit(Main.npc[0],100);Check(s.InsightHits==0,"friendly cannot charge");Main.npc[0].friendly=false;s.RecordHit(Main.npc[0],100);s.RecordHit(Main.npc[0],100);Check(s.InsightHits==1,"primary hit global cadence gate");s.Expert[1]=false;
        int slot=PantheonArsenal.Launch(p.GetSource_Misc("Smoke"),0,0,7,p.Center,Vector2.One,100);Check(slot>=0&&!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(p,Main.projectile[slot]),"proc cannot recurse");
        for(int i=0;i<140;i++)PantheonArsenal.Launch(p.GetSource_Misc("Smoke"),0,0,7,p.Center,Vector2.One,100);Check(Owned().Count()==120,"global projectile cap120");Clear();
        for(int id=0;id<36;id++)
        {
            Item item=new(PantheonCatalog.Weapon(id));if(item.useAmmo!=AmmoID.None){p.inventory[54].TurnToAir();Check(!p.PickAmmo(item,out _,out _,out _,out _,out _),"no ammo denies native shot "+id);}
            if(item.mana>0){int mana=p.statMana;p.statMana=0;Check(!p.CheckMana(item,-1,false),"no mana denies native shot "+id);p.statMana=mana;}
            if(PantheonArsenal.Classes[id]==3)
            {p.AddBuff(item.buffType,2);int minion=Projectile.NewProjectile(p.GetSource_Misc("Smoke"),p.Center,Vector2.Zero,item.shoot,100,0,0);p.dead=true;Main.projectile[minion].AI();Check(!Main.projectile[minion].active,"dead owner dismisses "+id);p.dead=false;p.ClearBuff(item.buffType);}
        }
        int originalMode=Main.netMode;Equip(p,0);s.Cooldowns[17]=1000;
        try
        {
            Main.netMode=NetmodeID.Server;Read(new byte[]{0,0},1);Check(s.SkillTime==0,"spoofed identity denied");Read(new byte[]{0,0},0);Check(s.SkillTime==0&&s.Cooldowns[17]==1000,"server skill cooldown");
            int[] old=(int[])s.Cooldowns.Clone();Read(new byte[]{2,0,0,0},0);Check(old.SequenceEqual(s.Cooldowns),"truncated packet atomic");
            byte[] bad=new byte[50];bad[0]=2;bad[1]=0;bad[48]=255;bad[49]=255;Read(bad,0);Check(old.SequenceEqual(s.Cooldowns),"oversized cooldown rejected atomically");
            Main.netMode=NetmodeID.MultiplayerClient;byte[] shorter=new byte[50];shorter[0]=3;Read(shorter,0);Check(old.SequenceEqual(s.Cooldowns),"network cannot shorten cooldown");
            byte[] forged=new byte[76];forged[0]=1;Array.Copy(Guid.NewGuid().ToByteArray(),0,forged,2,16);forged[22]=9;Read(forged,0);Check(s.SkillTime==0,"invalid active tier rejected");Read(new byte[]{1,0,0,100,0},0);Check(s.SkillTime==0,"truncated active state atomic");
        }
        finally{Main.netMode=originalMode;}
        void Read(byte[] bytes,int sender){using var ms=new MemoryStream(bytes);PantheonEquipmentPlayer.ReceivePacket(new BinaryReader(ms),sender);}
        // A malicious projectile state must not install non-finite coordinates or change its damage class.
        slot=PantheonArsenal.Launch(p.GetSource_Misc("Smoke"),0,2,3,p.Center,Vector2.Zero,100,12,p.Center+new Vector2(100,0));var shot=(PantheonShot)Main.projectile[slot].ModProjectile;Vector2 end=shot.End;
        using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){writer.Write(true);writer.Write(5);writer.Write((ushort)0);writer.Write(float.NaN);writer.Write(0f);for(int i=0;i<4;i++)writer.Write(0f);writer.Write(false);writer.Write((byte)0);}stream.Position=0;shot.ReceiveExtraAI(new BinaryReader(stream));Check(shot.End==end,"nonfinite network endpoint denied");}
        Log("PANTHEON_EQUIPMENT_BOUNDARIES_PASS owner=true ammo=true mana=true deathDismissal=true skillIdentity=true cooldownAuthority=true truncatedAtomic=true finite=true cap120=true recursion=false");
    }
}
