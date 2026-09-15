#nullable enable
using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using StarfallThrone.Content.Oaths.Support;

namespace StarfallThrone.Content.Oaths.Equipment;

public static class SupremeArsenal
{
    public static readonly int[] Damage={345000,300000,350000,280000,360000,320000,380000,300000,400000,365000,415000,340000};
    public static readonly int[] Speed={30,26,46,30,32,24,42,30,30,26,44,30};
    public static DamageClass Class(int i)=>(i%4) switch{0=>DamageClass.Melee,1=>DamageClass.Ranged,2=>DamageClass.Magic,_=>DamageClass.Summon};
    public static bool Finite(Vector2 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);
    public static Vector2 Aim(Player p,Vector2 desired,float range=800)=>global::StarfallThrone.Content.Pantheon.Equipment.PantheonArsenal.Aim(p,desired,range);
    public static int Launch(IEntitySource source,int owner,int weapon,int kind,Vector2 at,Vector2 velocity,int damage,int variant=0)
    {
        if(owner!=Main.myPlayer||owner<0||owner>=Main.maxPlayers||weapon<0||weapon>=12||kind<0||kind>3||!Finite(at)||!Finite(velocity))return -1;
        int count=0;foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==owner&&q.ModProjectile is SupremeShot)count++;
        if(count>=64)return -1;
        int slot=Projectile.NewProjectile(source,at,velocity,ModContent.ProjectileType<SupremeShot>(),Math.Clamp(damage,1,20000000),4,owner,weapon,kind,variant);
        if(slot<Main.maxProjectiles){((SupremeShot)Main.projectile[slot].ModProjectile).Configure();Main.projectile[slot].netUpdate=true;}return slot;
    }
    public static bool Fire(SupremeWeaponBase item,Player p,EntitySource_ItemUse_WithAmmo source,Vector2 at,Vector2 velocity,int damage)
    {
        if(p.whoAmI!=Main.myPlayer)return false;
        int id=item.Index;var state=p.GetModPlayer<OathEquipmentPlayer>();Vector2 dir=velocity.SafeNormalize(new Vector2(p.direction,0)),aim=Aim(p,Main.MouseWorld);
        bool alt=p.altFunctionUse==2;
        void Shot(int kind,float power=1,Vector2? origin=null,Vector2? speed=null,int variant=0)=>Launch(source,p.whoAmI,id,kind,origin??at,speed??dir*24,(int)(damage*power),variant);
        if(alt)
        {
            switch(id)
            {
                case 2:
                    int charges=state.DewCharges;state.DewCharges=0;
                    for(int n=0;n<charges;n++)Shot(1,1.25f,p.Center+new Vector2(0,-20-n*16),(aim-(p.Center+new Vector2(0,-20-n*16))).SafeNormalize(dir)*(22+n*2));break;
                case 4:case 8:
                    int charge=id==4?state.EmberCharges:state.FateCharges;
                    if(charge>=3){if(id==4)state.EmberCharges=0;else state.FateCharges=0;Shot(id==4?0:1,id==4?2f:2.2f,speed:id==4?dir:dir*28,variant:1);}break;
                case 6:state.FireMode=(state.FireMode+1)%4;break;
                case 10:state.FateMode=(state.FateMode+1)%3;break;
                case 7:if(state.CommandCooldown==0){state.CommandTime=120;state.CommandCooldown=600;}break;
            }
            return false;
        }
        if(id%4==3)
        {
            p.AddBuff(item.Item.buffType,2);
            // Court is one principal servant, with decorative satellites rather than duplicate full-damage minions.
            if(id==11)foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==p.whoAmI&&q.ModProjectile is SupremeMinion m&&m.Index==2)q.Kill();
            int slot=Projectile.NewProjectile(source,Aim(p,Main.MouseWorld,400),Vector2.Zero,item.Item.shoot,damage,4,p.whoAmI);
            if(slot<Main.maxProjectiles)Main.projectile[slot].originalDamage=item.Item.damage;return false;
        }
        switch(id)
        {
            case 0:case 4:case 8:Shot(0,speed:dir);break;
            case 1:
                for(int n=-1;n<=1;n++){Vector2 origin=Aim(p,aim+new Vector2(n*75,-180));Shot(1,.5f,origin,(aim-origin).SafeNormalize(dir)*25);}break;
            case 2:state.DewCharges=Math.Min(3,state.DewCharges+1);break;
            case 5:state.Heat=Math.Min(10,state.Heat+1);state.HeatIdle=0;state.HeatDamage=damage;state.HeatDirection=dir;Shot(1,1+state.Heat*.04f);break;
            case 6:
                switch(state.FireMode){case 0:Shot(1,1.2f,speed:dir*18);break;case 1:for(int n=-1;n<=1;n++)Shot(1,.45f,speed:dir.RotatedBy(n*.16)*22);break;case 2:Shot(2,1.6f,speed:dir);break;case 3:Shot(3,1.4f,aim,Vector2.Zero);break;}break;
            case 9:Shot(1,1+Math.Min(10,state.Precision)*.03f,speed:dir*32);break;
            case 10:
                switch(state.FateMode){case 0:Shot(1,1.3f,speed:dir*25);break;case 1:for(int n=-1;n<=1;n++)Shot(1,.5f,speed:dir.RotatedBy(n*.12)*23);break;case 2:Shot(2,1.7f,speed:dir);break;}break;
        }
        return false;
    }
}
public abstract class SupremeWeaponBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeWeapon"+Index;
    public override void SetStaticDefaults(){if(Index%4==3){ItemID.Sets.GamepadWholeScreenUseRange[Type]=true;ItemID.Sets.StaffMinionSlotsRequired[Type]=Index==11?4:2;}}
    public override void SetDefaults()
    {
        int style=Index%4;Item.width=Item.height=48;Item.damage=SupremeArsenal.Damage[Index];Item.DamageType=SupremeArsenal.Class(Index);
        Item.useStyle=ItemUseStyleID.Shoot;Item.useTime=Item.useAnimation=SupremeArsenal.Speed[Index];Item.autoReuse=true;Item.noMelee=true;Item.noUseGraphic=style==0;
        Item.shoot=ModContent.ProjectileType<SupremeShot>();Item.shootSpeed=24;Item.knockBack=4;Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(platinum:3);
        Item.useAmmo=style==1?(Index==1?AmmoID.Arrow:AmmoID.Bullet):AmmoID.None;Item.mana=style==2?36:style==3?30:0;
        Item.UseSound=style==0?SoundID.Item1:style==1?SoundID.Item11:SoundID.Item20;
        if(style==3){Item.shoot=ModContent.Find<ModProjectile>(Mod.Name,"SupremeMinion"+(Index/4)).Type;Item.buffType=ModContent.Find<ModBuff>(Mod.Name,"SupremeMinionBuff"+(Index/4)).Type;}
    }
    public override bool AltFunctionUse(Player p)=>Index is 2 or 4 or 6 or 7 or 8 or 10;
    public override bool CanUseItem(Player p)
    {
        bool alt=p.altFunctionUse==2;Item.mana=alt?0:Index%4==2?36:Index%4==3?30:0;
        var state=p.GetModPlayer<OathEquipmentPlayer>();
        if(alt){if(Index==2&&state.DewCharges==0||Index==4&&state.EmberCharges<3||Index==8&&state.FateCharges<3||Index==7&&state.CommandCooldown>0)return false;}
        else if(Index==2&&state.DewCharges>=3)return false;
        if(Index%4==0)foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==p.whoAmI&&q.ModProjectile is SupremeShot s&&s.Kind==0)return false;
        return true;
    }
    public override bool Shoot(Player p,EntitySource_ItemUse_WithAmmo source,Vector2 position,Vector2 velocity,int type,int damage,float knockback)=>SupremeArsenal.Fire(this,p,source,position,velocity,damage);
    public override void AddRecipes()=>CreateRecipe().AddIngredient(OathCatalog.Material(Index/4),12).AddIngredient(OathCatalog.Bar(Index/4),6).AddTile(OathCatalog.Station(Index/4)).Register();
}
