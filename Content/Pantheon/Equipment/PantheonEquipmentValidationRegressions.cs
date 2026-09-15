#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Pantheon.Equipment;

public static partial class PantheonEquipmentValidation
{
    private static void ShieldSnapshotRegression()
    {
        Clear();int oldMode=Main.netMode;
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Player serverPlayer=Fresh();Equip(serverPlayer,0);var server=serverPlayer.GetModPlayer<PantheonEquipmentPlayer>();server.Expert[16]=true;
            Check(server.TrySkill()&&server.SkillSerial==1&&server.SkillShield==300,"new cast has serial and shield");byte[] first=Snapshot(server);
            Player clientPlayer=Fresh();Targets(clientPlayer);Equip(clientPlayer,0);var client=clientPlayer.GetModPlayer<PantheonEquipmentPlayer>();Main.netMode=NetmodeID.MultiplayerClient;Receive(first);
            Check(client.SkillSerial==server.SkillSerial&&client.SkillEpoch==server.SkillEpoch&&client.SkillShield==300,"new cast accepted over real packet parser");
            Check(Resolve(client,201).Damage==1&&client.SkillShield==100,"local native hurt consumes partial shield");
            for(int i=0;i<90;i++){server.AdvanceTimers();client.AdvanceTimers();}
            Main.netMode=NetmodeID.SinglePlayer;Check(server.TrySkill()&&server.SkillSerial==1&&server.Stance==2&&server.SkillShield==300,"stance retains cast and stale server shield");
            Main.netMode=NetmodeID.MultiplayerClient;Receive(Snapshot(server));Check(client.SkillShield==100&&client.Stance==2,"spent shield survives stance packet");
            for(int i=0;i<20;i++)client.AdvanceTimers();int remaining=client.SkillTime;Receive(Snapshot(server));Check(client.SkillShield==100&&client.SkillTime==remaining,"SyncPlayer snapshot cannot refill or extend cast");
            Resolve(client,1000);Check(client.SkillShield==0,"shield fully consumed");Receive(Snapshot(server));Check(client.SkillShield==0,"repeated sync cannot restore fully spent shield");
            client.UpdateDead();Receive(Snapshot(server));Check(client.SkillShield==0&&client.SkillTime==0,"same cast cannot resurrect after transient clear");Equip(clientPlayer,0);
            Main.netMode=NetmodeID.SinglePlayer;for(int i=0;i<1200;i++)server.AdvanceTimers();Check(server.TrySkill()&&server.SkillSerial==2,"genuine later cast advances serial");
            Main.netMode=NetmodeID.MultiplayerClient;Receive(Snapshot(server));Check(client.SkillShield==300&&client.SkillSerial==2,"new cast can grant its new shield");Resolve(client,201);Receive(first);Check(client.SkillShield==100&&client.SkillSerial==2,"older cast cannot overwrite new spent shield");
            byte[] wrongEpoch=Snapshot(server);Array.Copy(Guid.NewGuid().ToByteArray(),0,wrongEpoch,2,16);Receive(wrongEpoch);Check(client.SkillShield==100&&client.SkillSerial==2,"foreign epoch denied atomically");
            Log("PANTHEON_SHIELD_SYNC_PASS nativeHurt=true stanceSwitch=true repeatedSync=true elapsedTime=true deathClear=true newCastOnly=true oldSerialRejected=true epoch=true");
        }
        finally{Main.netMode=oldMode;}
        byte[] Snapshot(PantheonEquipmentPlayer s){using var ms=new MemoryStream();using(var writer=new BinaryWriter(ms,System.Text.Encoding.UTF8,true)){writer.Write((byte)1);writer.Write((byte)0);s.WriteSkillSnapshot(writer);}return ms.ToArray();}
        void Receive(byte[] bytes){using var ms=new MemoryStream(bytes);PantheonEquipmentPlayer.ReceivePacket(new BinaryReader(ms),256);}
    }
    private static void MeleeTimingRegression()
    {
        int cases=0;
        // Invoke the engine's animation calculation, rather than reimplementing its rounding rules.
        MethodInfo? nativeAnimation=typeof(Player).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .FirstOrDefault(m=>m.Name=="ApplyItemAnimation"&&m.GetParameters().Length>0&&m.GetParameters()[0].ParameterType==typeof(Item));
        Check(nativeAnimation!=null,"native ApplyItemAnimation available");
        for(int id=0;id<36;id++)if(PantheonArsenal.Classes[id]==0)foreach(float speed in new[]{.65f,1.75f})
        {
            Clear();Player p=Fresh();Targets(p);Item item=p.inventory[0];item.SetDefaults(PantheonCatalog.Weapon(id));p.GetAttackSpeed(DamageClass.Melee)+=speed-1;
            object?[] args=nativeAnimation!.GetParameters().Select((parameter,index)=>index==0?(object)item:parameter.HasDefaultValue?parameter.DefaultValue:Type.Missing).ToArray();nativeAnimation.Invoke(p,args);
            int duration=p.itemAnimationMax;Check(duration>=2&&duration<=240&&(speed>1?duration<item.useAnimation:duration>item.useAnimation),"native speed changes animation "+id+"/"+speed);
            Metrics metric=new();Cast(p,item,metric,duration);Projectile held=Owned().Single(q=>q.ModProjectile is PantheonShot {Kind:0});var blade=(PantheonShot)held.ModProjectile;
            Check(blade.SwingDuration==duration&&held.timeLeft==duration,"cast captures native duration "+id);Roundtrip(held);Check(!item.ModItem.CanUseItem(p),"swing overlap remains denied "+id);
            for(int frame=0;frame<duration;frame++)Step(p,metric);
            Check(!held.active&&item.ModItem.CanUseItem(p),"swing expires at effective animation "+id+"/"+speed);Check(metric.Hits>0&&metric.Seen.Count>1,"native hit and followup retained "+id+"/"+speed);
            // A second native cast must not wait for the weapon's unmodified base useAnimation.
            Cast(p,item,metric,duration);Check(Owned().Any(q=>q.ModProjectile is PantheonShot {Kind:0}),"immediate next swing "+id);cases++;
        }
        Clear();Log($"PANTHEON_MELEE_TIMING_PASS weapons=9 speedCases={cases} nativeAnimation=true nativeAI=true nativeDamage=true synchronizedDuration=true noBaseDurationLock=true");
    }
}
