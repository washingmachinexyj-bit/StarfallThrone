using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
namespace StarfallThrone.Content.Oaths.Seeds;
public abstract class SeedTokenBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathCatalog.Root+"SeedToken"+Index;
    public override void SetDefaults(){Item.width=18;Item.height=22;Item.maxStack=9999;Item.value=0;Item.rare=ItemRarityID.White;}
}
public sealed class SeedToken0:SeedTokenBase{public override int Index=>0;}
public sealed class SeedToken1:SeedTokenBase{public override int Index=>1;}
public sealed class SeedToken2:SeedTokenBase{public override int Index=>2;}
public abstract class SeedWeaponBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathCatalog.Root+"SeedWeapon"+Index;
    public override void SetDefaults()
    {
        Item.width=20;Item.height=26;Item.damage=Index==0?1:2;Item.knockBack=.1f;Item.value=0;Item.useTime=Item.useAnimation=Index==1?45:54;Item.autoReuse=true;
        Item.DamageType=Index==0?DamageClass.Magic:Index==1?DamageClass.Melee:DamageClass.Ranged;
        Item.useStyle=Index==1?ItemUseStyleID.Swing:ItemUseStyleID.Shoot;Item.UseSound=Index==1?SoundID.Item1:SoundID.Item8;
        if(Index!=1){Item.noMelee=true;Item.shoot=ModContent.ProjectileType<SeedShot>();Item.shootSpeed=3;Item.mana=Index==0?1:0;}
    }
}
public sealed class SeedWeapon0:SeedWeaponBase{public override int Index=>0;}
public sealed class SeedWeapon1:SeedWeaponBase{public override int Index=>1;}
public sealed class SeedWeapon2:SeedWeaponBase{public override int Index=>2;}
public sealed class SeedShot:ModProjectile
{
    public override string Texture=>OathCatalog.Root+"Spark";
    public override void SetDefaults(){Projectile.width=Projectile.height=6;Projectile.friendly=true;Projectile.tileCollide=true;Projectile.timeLeft=80;Projectile.penetrate=1;}
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
    {if(source is Terraria.DataStructures.EntitySource_ItemUse use&&use.Item.ModItem is SeedWeaponBase weapon)Projectile.DamageType=weapon.Index==0?DamageClass.Magic:DamageClass.Ranged;}
}
