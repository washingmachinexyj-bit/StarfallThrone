#nullable enable
using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Pantheon.Equipment;

public static class PantheonArsenal
{
    public static readonly int[] Damage={52000,54000,62000,51000,68000,66000,70000,61000,77000,73000,85000,70000,95000,88000,97000,81000,112000,102000,120000,100000,143000,130000,145000,122000,168000,154000,178000,147000,215000,198000,225000,190000,285000,260000,278000,230000};
    public static readonly int[] Classes={0,1,2,3,0,2,1,3,0,1,2,3,0,2,1,3,0,1,2,3,0,2,1,3,0,1,2,3,0,2,1,3,0,1,2,3};
    public static readonly int[] UseTimes={26,20,38,30,30,42,27,30,28,26,32,30,32,36,19,30,26,16,36,30,38,40,26,30,42,30,34,30,30,32,28,30,30,22,38,30};
    public static readonly int[] Summoners={3,7,11,15,19,23,27,31,35};
    public static int Boss(int id)=>id<32?id/2:16;
    public static DamageClass Class(int style)=>style switch{0=>DamageClass.Melee,1=>DamageClass.Ranged,2=>DamageClass.Magic,_=>DamageClass.Summon};
    public static int Minion(int weapon)=>ModContent.Find<ModProjectile>("StarfallThrone","PantheonMinion"+Boss(weapon)).Type;
    public static int Buff(int weapon)=>ModContent.Find<ModBuff>("StarfallThrone","PantheonMinionBuff"+Boss(weapon)).Type;
    public static bool Finite(Vector2 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);
    public static Vector2 Aim(Player p,Vector2 desired,float range=700)
    {
        if(!Finite(desired))return p.Center;
        Vector2 delta=desired-p.Center;float length=Math.Min(delta.Length(),range);Vector2 direction=delta.SafeNormalize(new Vector2(p.direction,0));
        Vector2 safe=p.Center;for(float d=8;d<=length;d+=8){Vector2 next=p.Center+direction*d;if(Collision.SolidCollision(next-new Vector2(6),12,12))break;safe=next;}return safe;
    }
    public static NPC? Target(Vector2 at,int owner,float range=900)
    {
        if(owner<0||owner>=Main.maxPlayers)return null;
        Player p=Main.player[owner];bool Valid(NPC n)=>n.CanBeChasedBy()&&Vector2.DistanceSquared(at,n.Center)<range*range&&Collision.CanHitLine(at,1,1,n.position,n.width,n.height);
        if(p.MinionAttackTargetNPC>=0&&p.MinionAttackTargetNPC<Main.maxNPCs&&Valid(Main.npc[p.MinionAttackTargetNPC]))return Main.npc[p.MinionAttackTargetNPC];
        NPC? best=null;float distance=range*range;foreach(NPC n in Main.ActiveNPCs){float d=Vector2.DistanceSquared(at,n.Center);if(d<distance&&Valid(n)){best=n;distance=d;}}return best;
    }
    public static int Launch(IEntitySource source,int owner,int weapon,int kind,Vector2 at,Vector2 velocity,int damage,int variant=0,Vector2? end=null)
    {
        if(owner!=Main.myPlayer||owner<0||owner>=Main.maxPlayers||weapon<0||weapon>=36||kind<0||kind>7||!Finite(at)||!Finite(velocity))return -1;
        int count=0;foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==owner&&q.ModProjectile is PantheonShot)count++;
        if(count>=120)return -1;
        int slot=Projectile.NewProjectile(source,at,velocity,ModContent.ProjectileType<PantheonShot>(),Math.Clamp(damage,1,20000000),3,owner,weapon,kind,variant);
        if(slot>=Main.maxProjectiles)return -1;
        var shot=(PantheonShot)Main.projectile[slot].ModProjectile;shot.End=end??at;
        if(kind==0)shot.SwingDuration=Math.Clamp(Main.player[owner].itemAnimationMax>0?Main.player[owner].itemAnimationMax:UseTimes[weapon],2,240);
        shot.Configure();shot.Projectile.netUpdate=true;return slot;
    }
    public static bool Fire(PantheonWeapon item,Player p,EntitySource_ItemUse_WithAmmo source,Vector2 at,Vector2 velocity,int damage)
    {
        if(p.whoAmI!=Main.myPlayer)return false;int id=item.Index;
        Vector2 direction=velocity.SafeNormalize(new Vector2(p.direction,0)),aim=Aim(p,Main.MouseWorld);int cast=p.GetModPlayer<PantheonEquipmentPlayer>().Casts[id]++%4096;
        void Shot(int kind,float power=1,Vector2? where=null,Vector2? speed=null,int variant=0,Vector2? end=null)=>Launch(source,p.whoAmI,id,kind,where??at,speed??direction*20,(int)(damage*power),variant,end);
        if(Classes[id]==3)
        {
            p.AddBuff(item.Item.buffType,2);int slot=Projectile.NewProjectile(source,Aim(p,Main.MouseWorld,480),Vector2.Zero,item.Item.shoot,damage,3,p.whoAmI);
            if(slot<Main.maxProjectiles)Main.projectile[slot].originalDamage=item.Item.damage;return false;
        }
        switch(id)
        {
            case 0:case 8:case 16:case 28:case 32:Shot(0,speed:direction,variant:cast%3);break;
            case 4:Shot(0,speed:direction,variant:3);break;
            case 12:Shot(0,speed:direction);Shot(3,.7f,aim-new Vector2(0,100),Vector2.UnitY,20,aim+new Vector2(0,100));break;
            case 20:case 24:Shot(0,speed:direction,variant:4);break;
            case 1:Shot(1,variant:cast%3);break;
            case 2:Shot(3,1.5f,aim-direction*100,Vector2.Zero,18,Aim(p,aim+direction*380,950));break;
            case 5:LimitFields(p,id,2);Shot(4,.35f,aim,Vector2.Zero);break;
            case 6:Shot(1);Shot(2,.65f,at,direction*16,24);break;
            case 9:for(int i=-1;i<=1;i++)Shot(1,.5f,speed:direction.RotatedBy(i*.16f)*18,variant:i+1);break;
            case 10:LimitFields(p,id,3);Shot(4,.45f,aim,Vector2.Zero);break;
            case 13:Shot(3,.85f,aim-new Vector2(110,100),Vector2.Zero,12,aim+new Vector2(110,100));Shot(3,.85f,aim+new Vector2(110,-100),Vector2.Zero,24,aim+new Vector2(-110,100));break;
            case 14:Shot(1,speed:direction*24);break;
            case 17:Shot(1,cast%2==0?.7f:1.25f,speed:direction*(cast%2==0?30:13),variant:cast%2);break;
            case 18:for(int i=-1;i<=1;i++)Shot(3,.65f,aim+new Vector2(i*100,-170),Vector2.Zero,12+(i+1)*10,aim+new Vector2(i*100,120));break;
            case 21:LimitFields(p,id,2);Shot(4,.45f,aim,Vector2.Zero);break;
            case 22:Shot(1);break;
            case 25:Shot(1,speed:direction*10);break;
            case 26:Shot(4,.5f,at,direction*5);break;
            case 29:for(int i=-2;i<=2;i++)Shot(1,.35f,speed:direction.RotatedBy(i*.13f)*18,variant:i+2);break;
            case 30:for(int i=0;i<3;i++)Shot(2,.55f,at,direction*24,10+i*12);break;
            case 33:for(int i=-1;i<=1;i++)Shot(1,.55f,speed:direction.RotatedBy(i*.10f)*26,variant:i+1);break;
            case 34:
                for(int i=0;i<3;i++){Vector2 a=Aim(p,aim+(i*MathHelper.TwoPi/3).ToRotationVector2()*115,850),b=Aim(p,aim+((i+1)*MathHelper.TwoPi/3).ToRotationVector2()*115,850);Shot(3,.6f,a,Vector2.Zero,12+i*8,b);}break;
        }
        return false;
    }
    private static void LimitFields(Player p,int id,int max)
    {int count=0;Projectile? oldest=null;foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==p.whoAmI&&q.ModProjectile is PantheonShot s&&s.Weapon==id&&s.Kind==4){count++;if(oldest==null||q.timeLeft<oldest.timeLeft)oldest=q;}if(count>=max)oldest?.Kill();}
}

public abstract class PantheonWeapon:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Weapon"+Index;
    public override void SetStaticDefaults(){if(PantheonArsenal.Classes[Index]==3){ItemID.Sets.GamepadWholeScreenUseRange[Type]=true;ItemID.Sets.StaffMinionSlotsRequired[Type]=2;}}
    public override void SetDefaults()
    {
        int style=PantheonArsenal.Classes[Index];Item.width=Item.height=48;Item.damage=PantheonArsenal.Damage[Index];Item.DamageType=PantheonArsenal.Class(style);
        Item.useStyle=style==3?ItemUseStyleID.Swing:ItemUseStyleID.Shoot;Item.useTime=Item.useAnimation=PantheonArsenal.UseTimes[Index];Item.autoReuse=true;Item.noMelee=true;Item.noUseGraphic=style==0;
        Item.shoot=ModContent.ProjectileType<PantheonShot>();Item.shootSpeed=20;Item.knockBack=3;Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(gold:80+PantheonArsenal.Boss(Index)*12);
        Item.useAmmo=style==1?(Index is 17 or 25 or 30 or 33?AmmoID.Bullet:AmmoID.Arrow):AmmoID.None;Item.mana=style==2?18+PantheonCatalog.Tier(PantheonArsenal.Boss(Index))*3:style==3?20:0;
        Item.UseSound=style==0?SoundID.Item1:style==1?Item.useAmmo==AmmoID.Arrow?SoundID.Item5:SoundID.Item11:style==3?SoundID.Item44:SoundID.Item20;
        if(style==3){Item.shoot=PantheonArsenal.Minion(Index);Item.buffType=PantheonArsenal.Buff(Index);}
    }
    public override bool CanUseItem(Player p)
    {if(PantheonArsenal.Classes[Index]!=0)return true;foreach(Projectile q in Main.ActiveProjectiles)if(q.owner==p.whoAmI&&q.ModProjectile is PantheonShot s&&s.Weapon==Index&&s.Kind==0)return false;return true;}
    public override bool Shoot(Player p,EntitySource_ItemUse_WithAmmo source,Vector2 position,Vector2 velocity,int type,int damage,float knockback)=>PantheonArsenal.Fire(this,p,source,position,velocity,damage);
    public override void AddRecipes(){int boss=PantheonArsenal.Boss(Index),tier=PantheonCatalog.Tier(boss);CreateRecipe().AddIngredient(PantheonCatalog.Material(boss),12).AddIngredient(PantheonCatalog.Essence(tier),4).AddTile(PantheonCatalog.CraftStation(tier)).Register();}
}
