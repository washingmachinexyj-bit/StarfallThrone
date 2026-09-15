using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Fable.Bosses;

namespace StarfallThrone.Content.Fable;

public sealed class FablePlayer : ModPlayer
{
    public int EncounterSlot=-1,RequestCooldown,SkipWindow,RestTicks;
    private Vector2 returnPoint, arenaCenter;
    private float arenaFloor;
    private int joinGrace,notice;
    private bool restricted;
    public bool InTrial => EncounterSlot>=0 && EncounterSlot<Main.maxNPCs && (joinGrace>0 || Main.npc[EncounterSlot].active && (Main.npc[EncounterSlot].ModNPC is FableBossNPC {Cancelled:false} or global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC {Cancelled:false}));
    public bool Restricted => InTrial && restricted;
    public static bool CleanEquipment(Player p)
    {
        for(int i=0;i<10;i++) if(!p.armor[i].IsAir) return false;
        return !p.mount.Active;
    }
    public void Begin(int slot,Vector2 center,float floor,bool send=true,Vector2? originalPosition=null)
    {
        if(EncounterSlot<0) returnPoint=originalPosition??Player.position;
        EncounterSlot=slot; arenaCenter=center; arenaFloor=floor; joinGrace=Main.netMode==NetmodeID.MultiplayerClient?90:0; restricted=FableWorld.Sealed;
        if(Player.whoAmI==Main.myPlayer)FableUI.Opened=false;
        Player.Teleport(new Vector2(center.X-100-Player.width/2,floor-Player.height),TeleportationStyleID.RodOfDiscord);
        Player.velocity=Vector2.Zero;Player.fallStart=(int)(Player.position.Y/16); RestTicks=0;
        if(Main.netMode==NetmodeID.Server && send)
        {
            NetMessage.SendData(MessageID.TeleportEntity,-1,-1,null,0,Player.whoAmI,Player.position.X,Player.position.Y,TeleportationStyleID.RodOfDiscord);
            ModPacket packet=Mod.GetPacket();packet.Write((byte)80);packet.Write((byte)10);packet.Write((short)slot);packet.Write(center.X);packet.Write(center.Y);packet.Write(floor);packet.Write(returnPoint.X);packet.Write(returnPoint.Y);packet.Send(Player.whoAmI);
        }
    }
    public void End()
    {
        if(EncounterSlot<0)return;
        int oldSlot=EncounterSlot;EncounterSlot=-1; joinGrace=0;restricted=false;
        if(!Player.dead && !Player.ghost && !Collision.SolidCollision(returnPoint,Player.width,Player.height))
        {
            Player.Teleport(returnPoint,TeleportationStyleID.RodOfDiscord);Player.velocity=Vector2.Zero;Player.fallStart=(int)(Player.position.Y/16);
            if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.TeleportEntity,-1,-1,null,0,Player.whoAmI,Player.position.X,Player.position.Y,TeleportationStyleID.RodOfDiscord);
        }
        if(Main.netMode==NetmodeID.Server){ModPacket packet=Mod.GetPacket();packet.Write((byte)80);packet.Write((byte)11);packet.Write((short)oldSlot);packet.Send(Player.whoAmI);}
    }
    public override void OnEnterWorld(){EncounterSlot=-1;notice=0;joinGrace=0;RestTicks=0;FableUI.Opened=false;global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Opened=false;}
    public override void PreUpdateBuffs()
    {
        if(!Main.gameMenu && FableWorld.Sealed && !Player.dead) Player.AddBuff(BuffID.NoBuilding,2,quiet:true);
        // Never DelBuff: an original Old One's Army source may still own a longer NoBuilding duration.
    }
    public override void PostUpdateBuffs()
    {
        if(Main.gameMenu || !FableWorld.Sealed || Player.dead)return;
        // Vanilla ties this buff's update to the Old One's Army. Keep the native icon and
        // explicitly apply its building flag for our independent world-owned seal.
        Player.buffImmune[BuffID.NoBuilding]=false;
        Player.AddBuff(BuffID.NoBuilding,2,quiet:true);
        Player.noBuilding=true;
    }
    public override bool CanUseItem(Item item)
    {
        if(!Restricted)return true;
        return item.type==ModContent.ItemType<FableBook>() || FableCatalog.StarterWeapon(item);
    }
    public override void PostUpdateEquips()
    {
        if(!Restricted)return;
        Player.statLifeMax2=100;Player.statManaMax2=20;Player.statLife=Math.Min(Player.statLife,100);Player.statMana=Math.Min(Player.statMana,20);
        Player.statDefense=Player.DefenseStat.Default;Player.endurance=0;Player.maxMinions=1;Player.maxTurrets=1;
        Player.moveSpeed=1;Player.accRunSpeed=3;Player.maxRunSpeed=3;Player.jumpSpeedBoost=0;Player.wingTime=0;Player.rocketTime=0;
        foreach(var damage in new[]{DamageClass.Generic,DamageClass.Melee,DamageClass.Ranged,DamageClass.Magic,DamageClass.Summon})
        {Player.GetDamage(damage)=StatModifier.Default;Player.GetAttackSpeed(damage)=1;Player.GetCritChance(damage)=damage==DamageClass.Generic?0:4;Player.GetArmorPenetration(damage)=0;}
    }
    public override void PreUpdateMovement()
    {
        if(!InTrial)return;
        if(Restricted && !CleanEquipment(Player))
        { if(Main.netMode!=NetmodeID.MultiplayerClient){if(Main.npc[EncounterSlot].ModNPC is FableBossNPC b)b.CancelEncounter();if(Main.npc[EncounterSlot].ModNPC is global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC s)s.CancelEncounter();} return; }
        float left=arenaCenter.X-192,right=arenaCenter.X+192-Player.width;
        Player.position.X=MathHelper.Clamp(Player.position.X,left,right);
        if(Player.position.Y<arenaFloor-200){Player.position.Y=arenaFloor-200;Player.velocity.Y=Math.Max(0,Player.velocity.Y);}
        if(Player.Bottom.Y+Player.velocity.Y>=arenaFloor && Player.velocity.Y>=0)
        {Player.position.Y=arenaFloor-Player.height;Player.velocity.Y=0;Player.fallStart=(int)(Player.position.Y/16);}
    }
    public override void PostUpdate()
    {
        if(RequestCooldown>0)RequestCooldown--;if(SkipWindow>0)SkipWindow--;if(joinGrace>0)joinGrace--;
        if(EncounterSlot>=0 && !InTrial)End();
        if(Player.whoAmI==Main.myPlayer && !Main.gameMenu && ++notice==90 && FableWorld.Sealed)
        {Main.NewText(FableCatalog.Text("Intro"),Color.Wheat);FableUI.Opened=true;}
        if(RestTicks>0)
        {
            if(FableWorld.AnyEncounter || Player.velocity.LengthSquared()>.1f || Player.dead){RestTicks=0;return;}
            RestTicks++;
            if(RestTicks>180 && RestTicks%15==0 && Main.netMode!=NetmodeID.MultiplayerClient)
            {Player.statLife=Math.Min(Math.Min(100,Player.statLifeMax2),Player.statLife+2);if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.PlayerLifeMana,-1,-1,null,Player.whoAmI);}
            if(Player.statLife>=Math.Min(100,Player.statLifeMax2))RestTicks=0;
        }
    }
    public override void PostUpdateRunSpeeds()
    {
        if(!Restricted)return;
        Player.accRunSpeed=Player.maxRunSpeed=3;Player.runAcceleration=.08f;Player.runSlowdown=.2f;
    }
    public override void UpdateDead(){EncounterSlot=-1;joinGrace=0;RestTicks=0;}
    public override bool CanBeHitByNPC(NPC npc,ref int cooldownSlot)=>!InTrial || npc.ModNPC is FableBossNPC or global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC;
    public override bool CanBeHitByProjectile(Projectile proj)=>!InTrial || proj.ModProjectile is FableHazard or global::StarfallThrone.Content.Oaths.Seeds.SeedHazard;
}

public sealed class FableBook : ModItem
{
    public override string Texture=>FableCatalog.Root+"Book";
    public override void SetDefaults(){Item.width=24;Item.height=28;Item.value=0;Item.maxStack=1;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=20;Item.consumable=false;}
    public override bool? UseItem(Player player){if(player.whoAmI==Main.myPlayer)FableUI.Opened=true;return true;}
    public override void AddRecipes()=>CreateRecipe().AddCondition(new Condition(Terraria.Localization.Language.GetText("Mods.StarfallThrone.Fable.NeedBook"),()=>!FableWorld.HasBook(Main.LocalPlayer))).Register();
}
