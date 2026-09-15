#nullable enable
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Oaths.Support;

namespace StarfallThrone.Content.Oaths.Equipment;

public abstract class SupremeExpertBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeExpert"+Index;
    public override void SetDefaults(){Item.width=Item.height=38;Item.accessory=true;Item.expert=true;Item.rare=ItemRarityID.Expert;Item.value=Item.sellPrice(platinum:2);}
    public override bool CanAccessoryBeEquippedWith(Item equipped,Item incoming,Player p)
    {
        // Same-line earlier versions cannot stack their time/guard effects with the ultimate version.
        if(equipped.ModItem is SupremeExpertBase a&&incoming.ModItem is SupremeExpertBase b)return a.Index!=b.Index;
        Item other=equipped.ModItem is SupremeExpertBase?incoming:equipped;
        return !(other.ModItem is global::StarfallThrone.Content.Divine.Equipment.DivineExpert d&&d.Index==Index)&&!(other.ModItem is global::StarfallThrone.Content.Ascendant.Equipment.AscendantExpert x&&x.Index==Index);
    }
    public override void UpdateAccessory(Player p,bool hideVisual)
    {
        var state=p.GetModPlayer<OathEquipmentPlayer>();if(state.Expert[Index])return;state.Expert[Index]=true;
        if(Index==1)p.GetDamage(DamageClass.Generic)+=.12f;
        if(Index==2)p.GetDamage(DamageClass.Generic)+=.15f;
    }
}
public static class SupremeArmorData
{
    public static readonly int[] Defense={255,170,102,68,340,255};
    public static int Life(int part)=>part==4?1680:1260;
    public static float Fraction(int part)=>part==4?.4f:.3f;
}
public abstract class SupremeArmorBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeArmor"+Index;
    public override void SetDefaults(){Item.width=Item.height=38;Item.defense=SupremeArmorData.Defense[Index];Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(platinum:3);}
    public override void UpdateEquip(Player p)
    {
        float fraction=SupremeArmorData.Fraction(Index);p.statLifeMax2+=SupremeArmorData.Life(Index);p.GetDamage(DamageClass.Generic)+=1.8f*fraction;p.endurance+=.24f*fraction;
        if(Index==5){p.moveSpeed+=.30f;return;}if(Index==4)return;
        p.GetDamage(SupremeArsenal.Class(Index))+=.8f;
        switch(Index){case 0:p.GetAttackSpeed(DamageClass.Melee)+=.35f;break;case 1:p.GetCritChance(DamageClass.Ranged)+=28;break;case 2:p.statManaMax2+=650;p.manaCost*=.65f;break;case 3:p.maxMinions+=12;p.maxTurrets+=2;break;}
    }
    public override bool IsArmorSet(Item head,Item body,Item legs)=>Index<4&&body.type==OathSupport.Item("SupremeArmor4")&&legs.type==OathSupport.Item("SupremeArmor5");
    public override void UpdateArmorSet(Player p){p.noKnockback=true;p.lifeRegen+=12;p.setBonus=OathSupport.Text("ArmorSet");}
    public override void AddRecipes()
    {
        Recipe r=CreateRecipe();for(int i=0;i<3;i++)r.AddIngredient(OathCatalog.Material(i),Index==4?10:6).AddIngredient(OathCatalog.Bar(i),Index==4?8:5);
        r.AddTile(ModContent.TileType<TriuneAltarTile>()).Register();
    }
}
