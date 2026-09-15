#nullable enable
using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Equipment;

public static class EcologyArsenal
{
    public static int Buff(int biome)=>ModContent.GetInstance<EcologyWeapon0>().Mod.Find<ModBuff>("EcologyMinionBuff"+biome).Type;
    public static int Minion(int biome)=>ModContent.GetInstance<EcologyWeapon0>().Mod.Find<ModProjectile>("EcologyMinion"+biome).Type;
    public static Vector2 Aim(Player p,Vector2 requested,float range=560)
    {
        if(!float.IsFinite(requested.X)||!float.IsFinite(requested.Y))return p.Center;
        Vector2 d=requested-p.Center;float length=Math.Min(range,d.Length());d=d.SafeNormalize(Vector2.UnitX);
        for(;length>16;length-=16)
        {Vector2 at=p.Center+d*length;if(Collision.CanHitLine(p.Center,1,1,at,1,1)&&!Collision.SolidCollision(at-new Vector2(12),24,24))return at;}
        return p.Center;
    }
    public static int Launch(IEntitySource source,int owner,int weapon,EcologyShotKind kind,Vector2 at,Vector2 velocity,int damage,float knockback=2,int variant=0,Vector2? focus=null,bool secondary=false)
    {
        if(owner!=Main.myPlayer||owner<0||owner>=Main.maxPlayers||weapon<0||weapon>=40||
            !float.IsFinite(at.X)||!float.IsFinite(at.Y)||!float.IsFinite(velocity.X)||!float.IsFinite(velocity.Y))return -1;
        int count=0;foreach(Projectile p in Main.ActiveProjectiles)if(p.owner==owner && p.ModProjectile is EcologyShot)count++;
        if(count>=80)return -1;
        int type=weapon%4==3?ModContent.ProjectileType<EcologySummonShot>():ModContent.ProjectileType<EcologyShot>();
        int slot=Projectile.NewProjectile(source,at,velocity,type,Math.Max(1,damage),knockback,owner,weapon,(int)kind,variant);
        if(slot>=Main.maxProjectiles)return -1;
        var shot=(EcologyShot)Main.projectile[slot].ModProjectile;shot.Focus=focus??at;shot.ExplicitSecondary=secondary;
        shot.Projectile.DamageType=kind==EcologyShotKind.EquipmentProc?DamageClass.Generic:EcologyEquipmentData.Class(weapon%4);
        shot.Projectile.netUpdate=true;return slot;
    }
    public static void LimitNodes(Player p,int id,int limit)
    {
        int count=0;Projectile? oldest=null;
        foreach(Projectile q in Main.ActiveProjectiles)
            if(q.owner==p.whoAmI&&q.ModProjectile is EcologyShot s&&s.Weapon==id&&s.Kind==EcologyShotKind.Node)
            {count++;if(oldest==null||q.timeLeft<oldest.timeLeft)oldest=q;}
        if(count>=limit)oldest?.Kill();
    }
    public static bool Fire(EcologyWeapon w,Player p,EntitySource_ItemUse_WithAmmo source,Vector2 at,Vector2 velocity,int damage,float kb)
    {
        if(p.whoAmI!=Main.myPlayer)return false;
        int id=w.Index,b=id/4;Vector2 d=velocity.SafeNormalize(new Vector2(p.direction,0));var state=p.GetModPlayer<EcologyEquipmentPlayer>();
        void Shot(EcologyShotKind kind,float scale=1,Vector2? speed=null,int variant=0,Vector2? pos=null,Vector2? focus=null)
            =>Launch(source,p.whoAmI,id,kind,pos??at,speed??d*14,(int)(damage*scale),kb,variant,focus);
        if(id%4==3)
        {
            p.AddBuff(w.Item.buffType,2);
            int slot=Projectile.NewProjectile(source,Aim(p,Main.MouseWorld,480),Vector2.Zero,w.Item.shoot,damage,kb,p.whoAmI);
            if(slot<Main.maxProjectiles)Main.projectile[slot].originalDamage=w.Item.damage;
            return false;
        }
        switch(id)
        {
            case 0:Shot(EcologyShotKind.Swing,speed:d);break;
            case 1:Shot(EcologyShotKind.MirrorArrow,speed:d*15);break;
            case 2:
                Shot(EcologyShotKind.MirrorBolt,.65f,d.RotatedBy(-.16f)*12,0);
                Shot(EcologyShotKind.MirrorBolt,.65f,d.RotatedBy(.16f)*12,1);break;
            case 4:Shot(EcologyShotKind.Thrust,speed:d);break;
            case 5:Shot(EcologyShotKind.WaxArrow,speed:d*13);break;
            case 6:LimitNodes(p,id,4);Shot(EcologyShotKind.Node,speed:Vector2.Zero,pos:Aim(p,Main.MouseWorld,400));break;
            case 8:Shot(EcologyShotKind.Swing,speed:d,variant:state.Combo++%2);break;
            case 9:
                for(int i=-1;i<=1;i++)Shot(EcologyShotKind.Needle,.46f,d.RotatedBy(i*.13f)*15);break;
            case 10:LimitNodes(p,id,3);Shot(EcologyShotKind.Node,speed:Vector2.Zero,pos:Aim(p,Main.MouseWorld,480));break;
            case 12:Shot(EcologyShotKind.Swing,speed:d);break;
            case 13:Shot(EcologyShotKind.SoundBolt,speed:d*13);break;
            case 14:
                for(int i=0;i<3;i++)Shot(EcologyShotKind.SoundRing,.48f,d*7,variant:i*9);break;
            case 16:Shot(EcologyShotKind.Thrust,speed:d);break;
            case 17:Shot(EcologyShotKind.HeatBullet,speed:d*(state.Cadence%2==0?20:13),variant:state.Cadence++%2);break;
            case 18:
                Vector2 focus=Aim(p,Main.MouseWorld,480);
                for(int i=-1;i<=1;i++){Vector2 pos=Aim(p,focus+new Vector2(i*42,70),600);Shot(EcologyShotKind.RisingFlame,.55f,new Vector2(i*.25f,-8),pos:pos);}break;
            case 20:Shot(EcologyShotKind.Gear,speed:d*15);break;
            case 21:Shot(EcologyShotKind.Rivet,speed:d*19,variant:state.Cadence++%3);break;
            case 22:LimitNodes(p,id,2);Shot(EcologyShotKind.Node,speed:Vector2.Zero,pos:Aim(p,Main.MouseWorld,540));break;
            case 24:Shot(EcologyShotKind.Vine,speed:d);break;
            case 25:Shot(EcologyShotKind.Seed,speed:d*15);break;
            case 26:LimitNodes(p,id,2);Shot(EcologyShotKind.Node,speed:Vector2.Zero,pos:Aim(p,Main.MouseWorld,540));break;
            case 28:Shot(EcologyShotKind.SunDisc,speed:d*17);break;
            case 29:Shot(EcologyShotKind.SunSlug,speed:d*21);break;
            case 30:
                Vector2 center=Aim(p,Main.MouseWorld,540);
                Shot(EcologyShotKind.SunArray,speed:Vector2.Zero,pos:center,focus:at);break;
            case 32:Shot(EcologyShotKind.TideBlade,speed:d*13);break;
            case 33:Shot(EcologyShotKind.TideArrow,speed:d*18);break;
            case 34:Shot(EcologyShotKind.Bubble,speed:d*8);break;
            case 36:Shot(EcologyShotKind.OrbitBlade,speed:d);break;
            case 37:Shot(EcologyShotKind.PhaseRound,speed:d*19);break;
            case 38:
                Vector2 exit=Aim(p,Main.MouseWorld,600);
                Shot(EcologyShotKind.Gate,speed:d,pos:at,focus:exit);break;
        }
        return false;
    }
}

public abstract class EcologyWeapon:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Weapon"+Index;
    public override void SetStaticDefaults()
    {
        if(Index%4==3){ItemID.Sets.GamepadWholeScreenUseRange[Type]=true;ItemID.Sets.StaffMinionSlotsRequired[Type]=1;}
    }
    public override void SetDefaults()
    {
        int b=Index/4,style=Index%4;
        Item.width=Item.height=44;Item.damage=EcologyEquipmentData.Damage[Index];Item.DamageType=EcologyEquipmentData.Class(style);
        Item.knockBack=style==0?5:2;Item.useStyle=ItemUseStyleID.Shoot;Item.noMelee=true;Item.noUseGraphic=style==0;
        Item.useTime=Item.useAnimation=EcologyEquipmentData.UseTimes[Index];Item.autoReuse=true;Item.shootSpeed=16;
        Item.shoot=ModContent.ProjectileType<EcologyShot>();Item.rare=EcologyEquipmentData.Rarity(b);Item.value=Item.sellPrice(gold:1+b);
        Item.useAmmo=style==1?(b is 4 or 5 or 7 or 9?AmmoID.Bullet:AmmoID.Arrow):AmmoID.None;
        Item.mana=style==2?7+b:style==3?10:0;
        Item.UseSound=style==0?SoundID.Item1:style==1?(Item.useAmmo==AmmoID.Bullet?SoundID.Item11:SoundID.Item5):SoundID.Item20;
        if(Index==21){Item.useAnimation=21;Item.reuseDelay=18;}
        if(style==3){Item.buffType=EcologyArsenal.Buff(b);Item.shoot=EcologyArsenal.Minion(b);Item.useStyle=ItemUseStyleID.Swing;Item.UseSound=SoundID.Item44;}
    }
    public override bool CanUseItem(Player p)
    {
        if(Index%4!=0)return true;
        foreach(Projectile q in Main.ActiveProjectiles)
            if(q.owner==p.whoAmI&&q.ModProjectile is EcologyShot s&&s.Weapon==Index&&!s.Secondary)return false;
        return true;
    }
    public override bool Shoot(Player p,EntitySource_ItemUse_WithAmmo source,Vector2 position,Vector2 velocity,int type,int damage,float knockback)
        =>EcologyArsenal.Fire(this,p,source,position,velocity,damage,knockback);
    public override void AddRecipes()
    {
        int b=Index/4;
        CreateRecipe().AddIngredient(EcologyCatalog.Core(b),16).AddIngredient(EcologyCatalog.Material(b,Index%4),12)
            .AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(b))).AddCondition(EcologyCatalog.BossCondition(b)).AddCondition(EcologyCatalog.DownedCondition(b)).Register();
        CreateRecipe().AddIngredient(EcologyCatalog.Bar(b),10).AddIngredient(EcologyCatalog.Component(b),4).AddIngredient(EcologyCatalog.Core(b),6)
            .AddTile(EcologyCatalog.StationTile(EcologyCatalog.GearStation(b))).AddCondition(EcologyCatalog.BossCondition(b)).AddCondition(EcologyCatalog.DownedCondition(b)).Register();
    }
}
public sealed class EcologyWeapon0:EcologyWeapon {public override int Index=>0;}
public sealed class EcologyWeapon1:EcologyWeapon {public override int Index=>1;}
public sealed class EcologyWeapon2:EcologyWeapon {public override int Index=>2;}
public sealed class EcologyWeapon3:EcologyWeapon {public override int Index=>3;}
public sealed class EcologyWeapon4:EcologyWeapon {public override int Index=>4;}
public sealed class EcologyWeapon5:EcologyWeapon {public override int Index=>5;}
public sealed class EcologyWeapon6:EcologyWeapon {public override int Index=>6;}
public sealed class EcologyWeapon7:EcologyWeapon {public override int Index=>7;}
public sealed class EcologyWeapon8:EcologyWeapon {public override int Index=>8;}
public sealed class EcologyWeapon9:EcologyWeapon {public override int Index=>9;}
public sealed class EcologyWeapon10:EcologyWeapon {public override int Index=>10;}
public sealed class EcologyWeapon11:EcologyWeapon {public override int Index=>11;}
public sealed class EcologyWeapon12:EcologyWeapon {public override int Index=>12;}
public sealed class EcologyWeapon13:EcologyWeapon {public override int Index=>13;}
public sealed class EcologyWeapon14:EcologyWeapon {public override int Index=>14;}
public sealed class EcologyWeapon15:EcologyWeapon {public override int Index=>15;}
public sealed class EcologyWeapon16:EcologyWeapon {public override int Index=>16;}
public sealed class EcologyWeapon17:EcologyWeapon {public override int Index=>17;}
public sealed class EcologyWeapon18:EcologyWeapon {public override int Index=>18;}
public sealed class EcologyWeapon19:EcologyWeapon {public override int Index=>19;}
public sealed class EcologyWeapon20:EcologyWeapon {public override int Index=>20;}
public sealed class EcologyWeapon21:EcologyWeapon {public override int Index=>21;}
public sealed class EcologyWeapon22:EcologyWeapon {public override int Index=>22;}
public sealed class EcologyWeapon23:EcologyWeapon {public override int Index=>23;}
public sealed class EcologyWeapon24:EcologyWeapon {public override int Index=>24;}
public sealed class EcologyWeapon25:EcologyWeapon {public override int Index=>25;}
public sealed class EcologyWeapon26:EcologyWeapon {public override int Index=>26;}
public sealed class EcologyWeapon27:EcologyWeapon {public override int Index=>27;}
public sealed class EcologyWeapon28:EcologyWeapon {public override int Index=>28;}
public sealed class EcologyWeapon29:EcologyWeapon {public override int Index=>29;}
public sealed class EcologyWeapon30:EcologyWeapon {public override int Index=>30;}
public sealed class EcologyWeapon31:EcologyWeapon {public override int Index=>31;}
public sealed class EcologyWeapon32:EcologyWeapon {public override int Index=>32;}
public sealed class EcologyWeapon33:EcologyWeapon {public override int Index=>33;}
public sealed class EcologyWeapon34:EcologyWeapon {public override int Index=>34;}
public sealed class EcologyWeapon35:EcologyWeapon {public override int Index=>35;}
public sealed class EcologyWeapon36:EcologyWeapon {public override int Index=>36;}
public sealed class EcologyWeapon37:EcologyWeapon {public override int Index=>37;}
public sealed class EcologyWeapon38:EcologyWeapon {public override int Index=>38;}
public sealed class EcologyWeapon39:EcologyWeapon {public override int Index=>39;}

