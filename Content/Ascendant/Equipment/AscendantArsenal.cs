#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Divine;
using global::StarfallThrone.Content.Divine.Equipment;

namespace StarfallThrone.Content.Ascendant.Equipment;

public static class AscendantArsenal
{
    public static readonly int[] Damage = {32,28,35,24,96,51,76,68,340,180,205,150};
    public static string Art(int id) => AscendantCatalog.Root + "Weapon" + id;
    public static Vector2 Aim(Player p, Vector2 position, float range = 600) => DivineArsenal.Aim(p,position,range);
    public static int Buff(int tier) => tier switch { 0 => ModContent.BuffType<AscendantMinionBuff0>(),1 => ModContent.BuffType<AscendantMinionBuff1>(),_ => ModContent.BuffType<AscendantMinionBuff2>() };
    public static int Minion(int tier) => tier switch { 0 => ModContent.ProjectileType<AscendantMinion0>(),1 => ModContent.ProjectileType<AscendantMinion1>(),_ => ModContent.ProjectileType<AscendantMinion2>() };
    public static List<AscendantShot> Owned(Player p, int weapon, AscendantShotKind kind)
    {
        var result = new List<AscendantShot>();
        foreach (Projectile q in Main.ActiveProjectiles)
            if (q.owner == p.whoAmI && q.ModProjectile is AscendantShot s && s.Weapon == weapon && s.Kind == kind) result.Add(s);
        result.Sort((a,b) => a.Projectile.timeLeft.CompareTo(b.Projectile.timeLeft));
        return result;
    }
    public static int Launch(IEntitySource source, int owner, int weapon, AscendantShotKind kind, Vector2 at, Vector2 velocity,
        int damage, float knockback = 2, int variant = 0, Vector2? focus = null)
    {
        if (owner != Main.myPlayer || owner < 0 || owner >= Main.maxPlayers || weapon < 0 || weapon > 11 || !Finite(at) || !Finite(velocity)) return -1;
        int count = 0;
        foreach (Projectile q in Main.ActiveProjectiles) if (q.owner == owner && q.ModProjectile is AscendantShot) count++;
        if (count >= 96) return -1;
        int type = weapon%4 == 3 ? ModContent.ProjectileType<AscendantSummonShot>() : ModContent.ProjectileType<AscendantShot>();
        int slot = Projectile.NewProjectile(source,at,velocity,type,Math.Max(1,damage),knockback,owner,weapon,(int)kind,variant);
        if (slot >= Main.maxProjectiles) return -1;
        var shot = (AscendantShot)Main.projectile[slot].ModProjectile;
        shot.Focus = focus ?? at; shot.Projectile.DamageType = kind == AscendantShotKind.DewSkill ? DamageClass.Generic : AscendantEquipmentData.Class(weapon%4);
        shot.Projectile.netUpdate = true; return slot;
    }
    public static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    public static bool Fire(AscendantWeapon item, Player p, EntitySource_ItemUse_WithAmmo source, Vector2 at, Vector2 velocity, int damage, float knockback)
    {
        if (p.whoAmI != Main.myPlayer) return false;
        int id = item.Index; var state = p.GetModPlayer<AscendantEquipmentPlayer>();
        Vector2 aim = Aim(p,Main.MouseWorld,id == 0 ? 160 : 720), dir = velocity.SafeNormalize(new Vector2(p.direction,0));
        bool alt = p.altFunctionUse == 2;
        void Shot(AscendantShotKind kind, float multiplier = 1, Vector2? pos = null, Vector2? speed = null, int variant = 0, Vector2? focus = null)
            => Launch(source,p.whoAmI,id,kind,pos ?? at,speed ?? velocity,(int)(damage*multiplier),knockback,variant,focus);
        if (id%4 == 3)
        {
            p.AddBuff(item.Item.buffType,2);
            int slot = Projectile.NewProjectile(source,Aim(p,Main.MouseWorld,480),Vector2.Zero,item.Item.shoot,damage,knockback,p.whoAmI);
            if (slot < Main.maxProjectiles) Main.projectile[slot].originalDamage = item.Item.damage;
            return false;
        }
        switch (id)
        {
            case 0:
                if (alt)
                {
                    if (state.BranchCharges < 3) break;
                    state.BranchCharges = 0;
                    for (int i = -1; i <= 1; i += 2) Shot(AscendantShotKind.Branch,.85f,aim + new Vector2(i*75,-30),new Vector2(-i*8,3));
                }
                else Shot(AscendantShotKind.Swing,speed:dir,variant:state.SlashCombo++%3);
                break;
            case 1: Shot(AscendantShotKind.Arrow,speed:dir*16); break;
            case 2:
                var flowers = Owned(p,2,AscendantShotKind.Flower);
                if (alt)
                {
                    for (int i = 0; i < flowers.Count; i++)
                    { AscendantShot f = flowers[i]; f.Trigger(i*10); }
                }
                else
                {
                    if (flowers.Count >= 3) flowers[0].Projectile.Kill();
                    Shot(AscendantShotKind.Flower,pos:aim,speed:Vector2.Zero);
                }
                break;
            case 4:
                if (alt)
                {
                    if (state.FurnacePressure < 100) break;
                    state.FurnacePressure = 0;
                    for (int law = 0; law < 4; law++) Shot(AscendantShotKind.LawWave,.72f,p.Center,dir,law,aim);
                }
                else Shot(AscendantShotKind.Swing,speed:dir);
                break;
            case 5: Shot(AscendantShotKind.Bullet,speed:dir*22,variant:state.GunCadence++%4096); break;
            case 6:
                if (alt)
                {
                    foreach (NPC npc in Main.ActiveNPCs)
                    {
                        if (!npc.CanBeChasedBy() || Vector2.DistanceSquared(p.Center,npc.Center) > 720*720 || !Collision.CanHitLine(p.Center,1,1,npc.Center,1,1)) continue;
                        var marks = npc.GetGlobalNPC<AscendantTargetMarks>(); int mask = marks.TakeLaws(p.whoAmI); if (mask == 0) continue;
                        int count = 0; for (int i=0;i<4;i++) if ((mask & (1<<i)) != 0) count++;
                        Shot(AscendantShotKind.Explosion,.45f*count,npc.Center,Vector2.Zero,mask);
                    }
                }
                else { int law=state.MagicCadence++%4; Shot(AscendantShotKind.LawBolt,speed:dir*(law == 2 ? 11 : 16),variant:law); }
                break;
            case 8:
                if (alt)
                {
                    var marks=Owned(p,8,AscendantShotKind.CutMark);
                    for(int i=0;i<marks.Count;i++) marks[i].Trigger(i*8);
                }
                else Shot(AscendantShotKind.Swing,speed:dir);
                break;
            case 9: Shot(AscendantShotKind.Arrow,speed:dir*26,variant:state.BowReady?1:0); state.BowReady=false; break;
            case 10:
                var stars=Owned(p,10,AscendantShotKind.Star);
                if(alt)
                {
                    for(int i=0;i+1<stars.Count;i++)
                    {
                        Vector2 a=stars[i].Projectile.Center,b=stars[i+1].Projectile.Center;
                        if(Collision.CanHitLine(a,1,1,b,1,1)) Shot(AscendantShotKind.StarLink,1.10f,a,Vector2.Zero,focus:b);
                    }
                    foreach(var star in stars) star.Projectile.Kill();
                }
                else { if(stars.Count>=5)stars[0].Projectile.Kill(); Shot(AscendantShotKind.Star,pos:aim,speed:Vector2.Zero); }
                break;
        }
        return false;
    }
}

public abstract class AscendantWeapon : ModItem
{
    public abstract int Index { get; }
    public override string Texture => AscendantArsenal.Art(Index);
    public override void SetStaticDefaults()
    {
        if(Index%4==3){ItemID.Sets.GamepadWholeScreenUseRange[Type]=true;ItemID.Sets.StaffMinionSlotsRequired[Type]=Index==3?1:2;}
    }
    public override void SetDefaults()
    {
        Item.width=Item.height=48;Item.damage=AscendantArsenal.Damage[Index];Item.knockBack=Index%4==0?5:2;
        Item.DamageType=AscendantEquipmentData.Class(Index%4);Item.useStyle=ItemUseStyleID.Shoot;Item.noMelee=true;
        Item.useTime=Item.useAnimation=Index switch{0=>21,1=>25,2=>24,4=>34,5=>10,6=>27,8=>28,9=>23,10=>20,_=>30};
        Item.autoReuse=true;Item.shootSpeed=16;Item.shoot=ModContent.ProjectileType<AscendantShot>();Item.noUseGraphic=Index%4==0;
        Item.useAmmo=Index is 1 or 9?AmmoID.Arrow:Index==5?AmmoID.Bullet:AmmoID.None;
        Item.mana=Index==2?9:Index==6?14:Index==10?16:Index%4==3?10:0;
        Item.UseSound=Index%4==0?SoundID.Item1:Index%4==1?Index==5?SoundID.Item11:SoundID.Item5:SoundID.Item20;
        Item.rare=Index<4?ItemRarityID.Orange:Index<8?ItemRarityID.Pink:ItemRarityID.Red;Item.value=Item.sellPrice(gold:4+Index/4*8);
        if(Index==5){Item.useTime=6;Item.useAnimation=24;Item.reuseDelay=18;}
        if(Index%4==3){Item.shoot=AscendantArsenal.Minion(Index/4);Item.buffType=AscendantArsenal.Buff(Index/4);Item.useStyle=ItemUseStyleID.Swing;Item.UseSound=SoundID.Item44;}
    }
    public override bool AltFunctionUse(Player p) => Index is 0 or 2 or 4 or 6 or 8 or 10;
    public override void ModifyManaCost(Player p,ref float reduce,ref float mult)
    {if(p.altFunctionUse==2)mult*=Index==2?4f/9:Index==6?28f/14:Index==10?30f/16:1;}
    public override bool CanUseItem(Player p)
    {
        var s=p.GetModPlayer<AscendantEquipmentPlayer>();
        if(Index%4==0&&AscendantArsenal.Owned(p,Index,AscendantShotKind.Swing).Count>0)return false;
        if(p.altFunctionUse!=2)return true;
        return Index switch{
            0=>s.BranchCharges>=3,4=>s.FurnacePressure>=100,
            2=>AscendantArsenal.Owned(p,2,AscendantShotKind.Flower).Exists(x=>!x.Triggered),
            8=>AscendantArsenal.Owned(p,8,AscendantShotKind.CutMark).Exists(x=>!x.Triggered),
            10=>AscendantArsenal.Owned(p,10,AscendantShotKind.Star).Count>=2,_=>true};
    }
    public override bool Shoot(Player p,EntitySource_ItemUse_WithAmmo source,Vector2 position,Vector2 velocity,int type,int damage,float knockback)
        =>AscendantArsenal.Fire(this,p,source,position,velocity,damage,knockback);
    public override void AddRecipes()=>CreateRecipe().AddIngredient(DivineCatalog.Weapon(Index)).AddIngredient(AscendantCatalog.Material(Index/4),8).AddTile(AscendantCatalog.CraftStation(Index/4)).Register();
}
public sealed class AscendantWeapon0:AscendantWeapon{public override int Index=>0;}
public sealed class AscendantWeapon1:AscendantWeapon{public override int Index=>1;}
public sealed class AscendantWeapon2:AscendantWeapon{public override int Index=>2;}
public sealed class AscendantWeapon3:AscendantWeapon{public override int Index=>3;}
public sealed class AscendantWeapon4:AscendantWeapon{public override int Index=>4;}
public sealed class AscendantWeapon5:AscendantWeapon{public override int Index=>5;}
public sealed class AscendantWeapon6:AscendantWeapon{public override int Index=>6;}
public sealed class AscendantWeapon7:AscendantWeapon{public override int Index=>7;}
public sealed class AscendantWeapon8:AscendantWeapon{public override int Index=>8;}
public sealed class AscendantWeapon9:AscendantWeapon{public override int Index=>9;}
public sealed class AscendantWeapon10:AscendantWeapon{public override int Index=>10;}
public sealed class AscendantWeapon11:AscendantWeapon{public override int Index=>11;}
