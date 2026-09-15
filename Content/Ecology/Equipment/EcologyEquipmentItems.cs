using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Equipment;

public static class EcologyEquipmentData
{
    // Optional branch gear: above its native milestone, never late Voyage power.
    public static readonly int[] Damage = {21,17,23,12, 28,22,29,17, 33,25,34,20, 39,31,40,24, 55,38,47,30, 76,48,61,42, 93,57,76,51, 111,66,90,60, 125,75,103,69, 165,94,132,86};
    public static readonly int[] MeleeDefense = {12,17,20,24,35,48,58,64,69,78};
    public static readonly int[] ChestDefense = {5,7,8,10,14,19,23,25,27,30};
    public static readonly int[] LegDefense = {3,4,5,6,9,12,14,16,17,19};
    public static readonly int[] UseTimes = {25,30,32,30, 29,30,35,30, 27,32,38,30, 32,30,36,30, 28,25,33,30, 25,7,30,30, 28,27,36,30, 30,32,40,30, 25,25,34,30, 26,25,35,30};
    public static readonly int[] SetDamage = {12,18,22,26,36,46,56,65,74,95};
    public static DamageClass Class(int style) => style switch {0=>DamageClass.Melee,1=>DamageClass.Ranged,2=>DamageClass.Magic,_=>DamageClass.Summon};
    public static int Rarity(int biome) => biome < 2 ? ItemRarityID.Blue : biome < 4 ? ItemRarityID.Orange : biome < 6 ? ItemRarityID.Pink : biome < 9 ? ItemRarityID.Yellow : ItemRarityID.Red;
    public static int PieceDefense(int biome,int part) => part == 4 ? ChestDefense[biome] : part == 5 ? LegDefense[biome] :
        Math.Max(1,MeleeDefense[biome]-ChestDefense[biome]-LegDefense[biome]-(part == 0 ? 0 : part == 1 ? Math.Max(1,biome/2+1) : part == 2 ? Math.Max(2,biome+1) : Math.Max(3,biome+2)));
    public static float HeadDamage(int biome) => .04f + biome*.01f;
    public static int MinionSlots(int biome) => biome < 4 ? 1 : biome < 7 ? 2 : 3;
    public static int EquippedBiome(Player p)
    {
        if(p.armor[0].ModItem is not EcologyArmor head || head.Part >= 4)return -1;
        return p.armor[1].type == EcologyCatalog.Armor(head.Biome*6+4) && p.armor[2].type == EcologyCatalog.Armor(head.Biome*6+5) ? head.Biome : -1;
    }
}

public abstract class EcologyArmor : ModItem
{
    public abstract int Index { get; }
    public int Biome => Index/6;
    public int Part => Index%6;
    public override string Texture => EcologyCatalog.Root+"Armor"+Index;
    public override void SetDefaults()
    {
        Item.width=Item.height=32; Item.defense=EcologyEquipmentData.PieceDefense(Biome,Part);
        Item.rare=EcologyEquipmentData.Rarity(Biome);Item.value=Item.sellPrice(silver:50+Biome*60);
    }
    public override void UpdateEquip(Player p)
    {
        if(Part==4){p.GetDamage(DamageClass.Generic)+=.02f+(Biome>=4?.02f:0);return;}
        if(Part==5){p.moveSpeed+=.05f+Biome*.005f;return;}
        p.GetDamage(EcologyEquipmentData.Class(Part))+=EcologyEquipmentData.HeadDamage(Biome);
        if(Part==0)p.GetAttackSpeed(DamageClass.Melee)+=.04f+Biome*.005f;
        else if(Part==1)p.GetCritChance(DamageClass.Ranged)+=3+Biome/2;
        else if(Part==2){p.statManaMax2+=20+Biome*10;p.manaCost*=.95f;}
        else p.maxMinions+=EcologyEquipmentData.MinionSlots(Biome);
    }
    public override bool IsArmorSet(Item head,Item body,Item legs) => Part<4 && body.type==EcologyCatalog.Armor(Biome*6+4) && legs.type==EcologyCatalog.Armor(Biome*6+5);
    public override void UpdateArmorSet(Player p)
    {
        p.GetModPlayer<EcologyEquipmentPlayer>().SetBiome=Biome;
        p.setBonus=Language.GetTextValue("Mods.StarfallThrone.EcologyEquipment.Set"+Biome);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(EcologyCatalog.Bar(Biome),Part==4?16:Part==5?12:10)
        .AddIngredient(EcologyCatalog.Component(Biome),Part==4?4:3).AddIngredient(EcologyCatalog.Core(Biome),Part==4?6:4)
        .AddTile(EcologyCatalog.StationTile(EcologyCatalog.GearStation(Biome))).AddCondition(EcologyCatalog.BossCondition(Biome))
        .AddCondition(EcologyCatalog.DownedCondition(Biome)).Register();
}

public abstract class EcologyExpert : ModItem
{
    public abstract int Index {get;}
    public override string Texture => EcologyCatalog.Root+"Expert"+Index;
    public override void SetDefaults()
    {Item.width=Item.height=32;Item.accessory=true;Item.expert=true;Item.rare=ItemRarityID.Expert;Item.value=Item.sellPrice(gold:2+Index);}
    public override void UpdateAccessory(Player p,bool hideVisual)
    {
        p.GetModPlayer<EcologyEquipmentPlayer>().Expert[Index]=true;
        switch(Index)
        {
            case 0:p.moveSpeed+=.08f;break;
            case 1:p.statDefense+=3;break;
            case 2:p.GetDamage(DamageClass.Summon)+=.07f;break;
            case 3:p.GetCritChance(DamageClass.Generic)+=5;break;
            case 4:p.buffImmune[BuffID.OnFire]=true;p.buffImmune[BuffID.Burning]=true;break;
            case 5:p.maxTurrets++;p.GetDamage(DamageClass.Summon)+=.06f;break;
            case 6:p.statLifeMax2+=30;break;
            case 7:p.statDefense+=6;break;
            case 9:p.GetDamage(DamageClass.Generic)+=.07f;break;
        }
    }
}
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor0 : EcologyArmor { public override int Index => 0; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor1 : EcologyArmor { public override int Index => 1; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor2 : EcologyArmor { public override int Index => 2; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor3 : EcologyArmor { public override int Index => 3; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor4 : EcologyArmor { public override int Index => 4; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor5 : EcologyArmor { public override int Index => 5; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor6 : EcologyArmor { public override int Index => 6; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor7 : EcologyArmor { public override int Index => 7; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor8 : EcologyArmor { public override int Index => 8; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor9 : EcologyArmor { public override int Index => 9; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor10 : EcologyArmor { public override int Index => 10; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor11 : EcologyArmor { public override int Index => 11; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor12 : EcologyArmor { public override int Index => 12; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor13 : EcologyArmor { public override int Index => 13; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor14 : EcologyArmor { public override int Index => 14; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor15 : EcologyArmor { public override int Index => 15; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor16 : EcologyArmor { public override int Index => 16; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor17 : EcologyArmor { public override int Index => 17; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor18 : EcologyArmor { public override int Index => 18; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor19 : EcologyArmor { public override int Index => 19; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor20 : EcologyArmor { public override int Index => 20; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor21 : EcologyArmor { public override int Index => 21; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor22 : EcologyArmor { public override int Index => 22; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor23 : EcologyArmor { public override int Index => 23; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor24 : EcologyArmor { public override int Index => 24; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor25 : EcologyArmor { public override int Index => 25; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor26 : EcologyArmor { public override int Index => 26; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor27 : EcologyArmor { public override int Index => 27; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor28 : EcologyArmor { public override int Index => 28; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor29 : EcologyArmor { public override int Index => 29; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor30 : EcologyArmor { public override int Index => 30; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor31 : EcologyArmor { public override int Index => 31; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor32 : EcologyArmor { public override int Index => 32; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor33 : EcologyArmor { public override int Index => 33; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor34 : EcologyArmor { public override int Index => 34; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor35 : EcologyArmor { public override int Index => 35; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor36 : EcologyArmor { public override int Index => 36; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor37 : EcologyArmor { public override int Index => 37; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor38 : EcologyArmor { public override int Index => 38; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor39 : EcologyArmor { public override int Index => 39; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor40 : EcologyArmor { public override int Index => 40; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor41 : EcologyArmor { public override int Index => 41; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor42 : EcologyArmor { public override int Index => 42; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor43 : EcologyArmor { public override int Index => 43; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor44 : EcologyArmor { public override int Index => 44; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor45 : EcologyArmor { public override int Index => 45; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor46 : EcologyArmor { public override int Index => 46; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor47 : EcologyArmor { public override int Index => 47; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor48 : EcologyArmor { public override int Index => 48; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor49 : EcologyArmor { public override int Index => 49; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor50 : EcologyArmor { public override int Index => 50; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor51 : EcologyArmor { public override int Index => 51; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor52 : EcologyArmor { public override int Index => 52; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor53 : EcologyArmor { public override int Index => 53; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor54 : EcologyArmor { public override int Index => 54; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor55 : EcologyArmor { public override int Index => 55; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor56 : EcologyArmor { public override int Index => 56; }
[AutoloadEquip(EquipType.Head)] public sealed class EcologyArmor57 : EcologyArmor { public override int Index => 57; }
[AutoloadEquip(EquipType.Body)] public sealed class EcologyArmor58 : EcologyArmor { public override int Index => 58; }
[AutoloadEquip(EquipType.Legs)] public sealed class EcologyArmor59 : EcologyArmor { public override int Index => 59; }
public sealed class EcologyExpert0 : EcologyExpert { public override int Index => 0; }
public sealed class EcologyExpert1 : EcologyExpert { public override int Index => 1; }
public sealed class EcologyExpert2 : EcologyExpert { public override int Index => 2; }
public sealed class EcologyExpert3 : EcologyExpert { public override int Index => 3; }
public sealed class EcologyExpert4 : EcologyExpert { public override int Index => 4; }
public sealed class EcologyExpert5 : EcologyExpert { public override int Index => 5; }
public sealed class EcologyExpert6 : EcologyExpert { public override int Index => 6; }
public sealed class EcologyExpert7 : EcologyExpert { public override int Index => 7; }
public sealed class EcologyExpert8 : EcologyExpert { public override int Index => 8; }
public sealed class EcologyExpert9 : EcologyExpert { public override int Index => 9; }

