using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Pantheon.Equipment;

public static class PantheonEquipmentData
{
    public static readonly int[] ArmorBoss={0,2,5,8,11,14};
    public static readonly int[] Life={900,1100,1400,1800,2350,3100};
    public static readonly int[] BaseDefense={270,320,390,470,570,690};
    public static readonly int[] Damage={56,64,74,86,100,116},Reduction={15,16,17,18,19,20};
    public static readonly int[] SkillDuration={360,480,360,480,600,600},SkillCooldown={1200,1500,1500,1800,1800,2400};
    public static int Defense(int tier,int style)=>BaseDefense[tier]*(style switch{0=>100,1=>90,2=>82,_=>78})/100;
    public static int PieceDefense(int tier,int part)=>part==4?BaseDefense[tier]*4/10:part==5?BaseDefense[tier]*3/10:Defense(tier,part)-BaseDefense[tier]*7/10;
    public static int PieceLife(int tier,int part)=>part==4?Life[tier]*4/10:part==5?Life[tier]*3/10:Life[tier]-Life[tier]*7/10;
    public static float Fraction(int part)=>part==4?.4f:part==5?.3f:.3f;
    public static int EquippedTier(Player p)
    {if(p.armor[0].ModItem is not PantheonArmor a||a.Part>=4)return -1;return p.armor[1].type==PantheonCatalog.Armor(a.Tier*6+4)&&p.armor[2].type==PantheonCatalog.Armor(a.Tier*6+5)?a.Tier:-1;}
}
public abstract class PantheonArmor:ModItem
{
    public abstract int Index{get;}public int Tier=>Index/6;public int Part=>Index%6;
    public override string Texture=>PantheonCatalog.Root+"Armor"+Index;
    public override void SetDefaults(){Item.width=Item.height=34;Item.defense=PantheonEquipmentData.PieceDefense(Tier,Part);Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(gold:80+Tier*35);}
    public override void UpdateEquip(Player p)
    {
        float fraction=PantheonEquipmentData.Fraction(Part);p.statLifeMax2+=PantheonEquipmentData.PieceLife(Tier,Part);p.GetDamage(DamageClass.Generic)+=PantheonEquipmentData.Damage[Tier]*.01f*fraction;p.endurance+=PantheonEquipmentData.Reduction[Tier]*.01f*fraction;
        if(Part==5){p.moveSpeed+=.15f+Tier*.02f;return;}if(Part==4)return;
        p.GetDamage(PantheonArsenal.Class(Part))+=.40f+Tier*.04f;
        switch(Part){case 0:p.GetAttackSpeed(DamageClass.Melee)+=.20f+Tier*.02f;break;case 1:p.GetCritChance(DamageClass.Ranged)+=14+Tier*2;break;case 2:p.statManaMax2+=260+Tier*60;p.manaCost*=.8f-Tier*.02f;break;case 3:p.maxMinions+=5+Tier;p.maxTurrets+=2;break;}
    }
    public override bool IsArmorSet(Item head,Item body,Item legs)=>Part<4&&body.type==PantheonCatalog.Armor(Tier*6+4)&&legs.type==PantheonCatalog.Armor(Tier*6+5);
    public override void UpdateArmorSet(Player p){p.noKnockback=true;p.GetModPlayer<PantheonEquipmentPlayer>().SetTier=Tier;p.setBonus=Language.GetTextValue("Mods.StarfallThrone.PantheonEquipment.Set"+Tier);}
    public override void AddRecipes()=>CreateRecipe().AddIngredient(PantheonCatalog.Material(PantheonEquipmentData.ArmorBoss[Tier]),Part==4?18:Part==5?14:10).AddIngredient(PantheonCatalog.Essence(Tier),Part==4?6:4).AddTile(PantheonCatalog.CraftStation(Tier)).Register();
}
public abstract class PantheonExpert:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Expert"+Index;
    public override void SetDefaults(){Item.width=Item.height=34;Item.accessory=true;Item.expert=true;Item.rare=ItemRarityID.Expert;Item.value=Item.sellPrice(gold:100+Index*15);}
    public override bool CanAccessoryBeEquippedWith(Item equippedItem,Item incomingItem,Player p)=>!(equippedItem.ModItem is PantheonExpert a&&incomingItem.ModItem is PantheonExpert b&&a.Index==b.Index);
    public override void UpdateAccessory(Player p,bool hideVisual)
    {
        var state=p.GetModPlayer<PantheonEquipmentPlayer>();if(state.Expert[Index])return;state.Expert[Index]=true;
        switch(Index)
        {case 0:p.statLifeMax2+=180;break;case 1:p.GetCritChance(DamageClass.Generic)+=8;break;case 2:p.moveSpeed+=.12f;break;case 3:p.buffImmune[BuffID.Confused]=true;break;case 4:p.maxMinions++;break;case 5:p.statLifeMax2+=240;break;case 6:p.moveSpeed+=.15f;break;case 7:p.statDefense+=40;break;case 8:p.GetDamage(DamageClass.Generic)+=.10f;break;case 9:p.statDefense+=50;break;case 10:p.GetDamage(DamageClass.Generic)+=.12f;break;case 11:p.maxMinions+=2;break;case 12:p.statDefense+=80;p.noKnockback=true;break;case 13:p.moveSpeed+=.18f;break;case 14:p.GetCritChance(DamageClass.Generic)+=12;break;case 15:p.statLifeMax2+=300;break;case 16:p.statLifeMax2+=400;break;}
    }
}
