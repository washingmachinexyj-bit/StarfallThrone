using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Fable.Equipment;

public abstract class FableWeaponBase : ModItem
{
    public abstract int Index{get;}
    public static readonly int[] Damage={3,4,4,3,4,5,5,5,5,6,6,4,7,7,5,8,8,9};
    public static readonly int[] Speed={39,42,30,72,36,36,45,36,33,42,39,66,39,48,66,48,36,39};
    public static int Kind(int i)=>i is 0 or 4 or 8 or 10?1:i is 1 or 7 or 13 or 15?2:i is 3 or 11 or 14?3:i==6?4:0;
    public override string Texture=>FableCatalog.Root+"Weapon"+Index;
    public override void SetDefaults()
    {
        Item.width=Item.height=26;Item.damage=Damage[Index];Item.useTime=Item.useAnimation=Speed[Index];Item.value=0;Item.knockBack=1.5f;Item.autoReuse=true;
        int kind=Kind(Index);Item.DamageType=kind==1?DamageClass.Ranged:kind==2?DamageClass.Magic:kind is 3 or 4?DamageClass.Summon:DamageClass.Melee;
        Item.useStyle=kind==0?ItemUseStyleID.Swing:ItemUseStyleID.Shoot;
        if(kind is 1 or 2){Item.noMelee=true;Item.shoot=ModContent.ProjectileType<FableShot>();Item.shootSpeed=kind==1?4:3.5f;Item.mana=kind==2?(Index<10?2:3):0;Item.UseSound=SoundID.Item1;}
        if(kind==3){Item.noMelee=true;Item.mana=3;Item.shoot=ModContent.ProjectileType<FableMinion>();Item.shootSpeed=0;Item.buffType=ModContent.BuffType<FableCompanionBuff>();Item.UseSound=SoundID.Item44;}
        if(kind==4){Item.noMelee=true;Item.noUseGraphic=true;Item.shoot=ModContent.ProjectileType<FableWhip>();Item.shootSpeed=3;Item.DamageType=DamageClass.SummonMeleeSpeed;}
    }
    public override bool Shoot(Player player,EntitySource_ItemUse_WithAmmo source,Vector2 pos,Vector2 velocity,int type,int damage,float knockback)
    {
        if(Kind(Index)==3)
        {
            foreach(Projectile p in Main.ActiveProjectiles)if(p.owner==player.whoAmI && p.ModProjectile is FableMinion)p.Kill();
            player.AddBuff(ModContent.BuffType<FableCompanionBuff>(),3600);
        }
        Projectile.NewProjectile(source,pos,velocity,type,damage,knockback,player.whoAmI,Index==6?0:Index);
        return false;
    }
}

public sealed class FableWeapon0 : FableWeaponBase {public override int Index=>0;}

public sealed class FableWeapon1 : FableWeaponBase {public override int Index=>1;}

public sealed class FableWeapon2 : FableWeaponBase {public override int Index=>2;}

public sealed class FableWeapon3 : FableWeaponBase {public override int Index=>3;}

public sealed class FableWeapon4 : FableWeaponBase {public override int Index=>4;}

public sealed class FableWeapon5 : FableWeaponBase {public override int Index=>5;}

public sealed class FableWeapon6 : FableWeaponBase {public override int Index=>6;}

public sealed class FableWeapon7 : FableWeaponBase {public override int Index=>7;}

public sealed class FableWeapon8 : FableWeaponBase {public override int Index=>8;}

public sealed class FableWeapon9 : FableWeaponBase {public override int Index=>9;}

public sealed class FableWeapon10 : FableWeaponBase {public override int Index=>10;}

public sealed class FableWeapon11 : FableWeaponBase {public override int Index=>11;}

public sealed class FableWeapon12 : FableWeaponBase {public override int Index=>12;}

public sealed class FableWeapon13 : FableWeaponBase {public override int Index=>13;}

public sealed class FableWeapon14 : FableWeaponBase {public override int Index=>14;}

public sealed class FableWeapon15 : FableWeaponBase {public override int Index=>15;}

public sealed class FableWeapon16 : FableWeaponBase {public override int Index=>16;}

public sealed class FableWeapon17 : FableWeaponBase {public override int Index=>17;}

public sealed class PracticeSword : ModItem
{
    public override string Texture=>FableCatalog.Root+"PracticeSword";
    public override void SetDefaults(){Item.width=Item.height=24;Item.damage=3;Item.DamageType=DamageClass.Melee;Item.useStyle=ItemUseStyleID.Swing;Item.useTime=Item.useAnimation=36;Item.knockBack=1;Item.autoReuse=true;Item.value=0;}
}
public sealed class FableCore : ModItem
{
    public override string Texture=>FableCatalog.Root+"Core";
    public override void SetDefaults(){Item.width=Item.height=20;Item.maxStack=9999;Item.value=0;Item.material=true;}
}
public sealed class FableWorkbench : ModItem
{
    public override string Texture=>FableCatalog.Root+"Workbench";
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.TileType<FableWorkbenchTile>());Item.width=28;Item.height=22;Item.value=0;}
    public override void AddRecipes()=>CreateRecipe().AddIngredient<FableCore>(6).AddCondition(new Condition(Terraria.Localization.Language.GetText("Mods.StarfallThrone.Fable.CompleteRequired"),()=>FableWorld.Completed)).Register();
}
public sealed class FableWorkbenchTile : ModTile
{
    public override string Texture=>FableCatalog.Root+"WorkbenchTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileTable[Type]=true;Main.tileLavaDeath[Type]=true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);TileObjectData.newTile.CoordinateHeights=new[]{16,16};TileObjectData.addTile(Type);
        AdjTiles=new int[]{TileID.WorkBenches};AddMapEntry(new Color(195,180,150),Terraria.Localization.Language.GetText("Mods.StarfallThrone.Items.FableWorkbench.DisplayName"));
        RegisterItemDrop(ModContent.ItemType<FableWorkbench>());
    }
}
public abstract class FableArmorBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>FableCatalog.Root+"Armor"+Index;
    public override void SetDefaults(){Item.width=Item.height=24;Item.defense=Index==1?2:1;Item.value=0;}
    public override bool IsArmorSet(Item head,Item body,Item legs)=>head.type==ModContent.ItemType<FableArmor0>() && body.type==ModContent.ItemType<FableArmor1>() && legs.type==ModContent.ItemType<FableArmor2>();
    public override void UpdateArmorSet(Player p){p.moveSpeed+=.03f;p.setBonus=FableCatalog.Text("ArmorSet");}
    public override void AddRecipes()=>CreateRecipe().AddIngredient<FableCore>(Index==1?5:3).AddTile<FableWorkbenchTile>().Register();
}
[AutoloadEquip(EquipType.Head)] public sealed class FableArmor0:FableArmorBase{public override int Index=>0;}
[AutoloadEquip(EquipType.Body)] public sealed class FableArmor1:FableArmorBase{public override int Index=>1;}
[AutoloadEquip(EquipType.Legs)] public sealed class FableArmor2:FableArmorBase{public override int Index=>2;}

public abstract class FableUpgradeBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>FableCatalog.Root+"Upgrade"+Index;
    public override void SetDefaults()
    {
        Item.width=Item.height=26;Item.damage=Index==0?10:Index==1?6:Index==2?10:6;Item.useTime=Item.useAnimation=Index==0?36:Index==1?45:Index==2?48:66;
        Item.DamageType=Index==0?DamageClass.Melee:Index==1?DamageClass.Ranged:Index==2?DamageClass.Magic:DamageClass.Summon;
        Item.useStyle=Index==0?ItemUseStyleID.Swing:ItemUseStyleID.Shoot;Item.knockBack=2;Item.value=0;Item.autoReuse=true;
        if(Index>0){Item.noMelee=true;Item.shoot=Index==3?ModContent.ProjectileType<FableMinion>():ModContent.ProjectileType<FableShot>();Item.shootSpeed=4;}
        if(Index==1)Item.useAmmo=AmmoID.Arrow;
        if(Index>=2)Item.mana=3;
        if(Index==3)Item.buffType=ModContent.BuffType<FableCompanionBuff>();
    }
    public override bool Shoot(Player p,EntitySource_ItemUse_WithAmmo source,Vector2 pos,Vector2 velocity,int type,int damage,float knockback)
    {
        if(Index==3){foreach(Projectile proj in Main.ActiveProjectiles)if(proj.owner==p.whoAmI && proj.ModProjectile is FableMinion)proj.Kill();p.AddBuff(ModContent.BuffType<FableCompanionBuff>(),3600);}
        Projectile.NewProjectile(source,pos,velocity,Index==3?ModContent.ProjectileType<FableMinion>():ModContent.ProjectileType<FableShot>(),damage,knockback,p.whoAmI,18+Index);
        return false;
    }
    public override void AddRecipes()=>CreateRecipe().AddIngredient(FableCatalog.Weapon(Index==0?17:Index==1?10:Index==2?15:14)).AddIngredient<FableCore>(5).AddTile<FableWorkbenchTile>().Register();
}
public sealed class FableUpgrade0:FableUpgradeBase{public override int Index=>0;}
public sealed class FableUpgrade1:FableUpgradeBase{public override int Index=>1;}
public sealed class FableUpgrade2:FableUpgradeBase{public override int Index=>2;}
public sealed class FableUpgrade3:FableUpgradeBase{public override int Index=>3;}
public sealed class FableUnfinishedBlade : ModItem
{
    private int swings;
    public override string Texture=>FableCatalog.Root+"UnfinishedBlade";
    public override void SetDefaults(){Item.width=Item.height=28;Item.damage=11;Item.DamageType=DamageClass.Melee;Item.useStyle=ItemUseStyleID.Swing;Item.useTime=Item.useAnimation=39;Item.knockBack=2;Item.value=0;Item.autoReuse=true;}
    public override void UseAnimation(Player p)=>Item.scale=++swings%3==0?1.2f:1;
    public override void AddRecipes(){Recipe r=CreateRecipe().AddIngredient<FableCore>(8).AddTile<FableWorkbenchTile>();for(int i=0;i<18;i++)r.AddIngredient(FableCatalog.Weapon(i));r.Register();}
}
