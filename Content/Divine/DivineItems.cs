using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Divine;

public abstract class DivineMaterialBase : ModItem
{
    public abstract int Index {get;}
    public override string Texture=>DivineCatalog.Root+"Material"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=32;Item.maxStack=9999;Item.material=true;Item.rare=Index==0?ItemRarityID.Green:Index==1?ItemRarityID.LightRed:ItemRarityID.Red;Item.value=Item.sellPrice(silver:10*(Index+1));}
}
public abstract class DivineSummonBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Summon"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults()
    {Item.width=Item.height=40;Item.maxStack=1;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=45;Item.UseSound=SoundID.Item44;Item.rare=Index==0?ItemRarityID.Green:Index==1?ItemRarityID.LightRed:ItemRarityID.Red;}
    public static bool Valid(Player player,int index)
    {
        if(index is <0 or >=3||!player.active||player.dead||player.ghost||!DivineWorld.Open(index))return false;
        // Pillars are not bosses. NoDay is intentionally usable while distant pillars remain.
        foreach(NPC npc in Main.ActiveNPCs)if(npc.boss||npc.ModNPC is global::StarfallThrone.Content.NPCs.MiniBossNPC)return false;
        if(index==0)return Main.dayTime&&player.ZoneOverworldHeight&&!player.ZoneDesert&&!player.ZoneSnow&&!player.ZoneJungle&&!player.ZoneCorrupt&&!player.ZoneCrimson&&!player.ZoneHallow&&!player.ZoneBeach;
        if(index==1)return player.ZoneUnderworldHeight;
        return !Main.dayTime&&(player.ZoneOverworldHeight||player.ZoneSkyHeight)&&NPC.MoonLordCountdown<=0
            &&!player.ZoneTowerSolar&&!player.ZoneTowerVortex&&!player.ZoneTowerNebula&&!player.ZoneTowerStardust;
    }
    public override bool CanUseItem(Player player)=>Valid(player,Index);
    public override bool? UseItem(Player player)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient&&player.whoAmI==Main.myPlayer)
        {ModPacket packet=Mod.GetPacket();packet.Write((byte)40);packet.Write((byte)1);packet.Write((byte)Index);packet.Send();}
        else if(Main.netMode==NetmodeID.SinglePlayer)TrySummon(player,Index);
        return true;
    }
    public static bool TrySummon(Player player,int index)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!Valid(player,index)||player.HeldItem.type!=DivineCatalog.Summon(index))return false;
        // These flying gods do not need the vanilla randomized ground search. That search can
        // silently fail in empty sky or constrained terrain despite a valid reusable summon.
        Vector2 spawn=Bosses.DivineBossNPC.WorldPoint(player.Center+new Vector2(-player.direction*420,-120),192);
        int type=DivineCatalog.NPCType(index);
        NPC.SpawnBoss((int)spawn.X,(int)spawn.Y,type,player.whoAmI);
        return NPC.AnyNPCs(type);
    }
    public override void ModifyTooltips(List<TooltipLine> lines)
    {
        bool seed=global::StarfallThrone.Content.Oaths.OathWorld.SeedWon[Index];
        lines.Add(new TooltipLine(Mod,"SeedOath",global::StarfallThrone.Content.Oaths.OathCatalog.Text(seed?"SeedMet":"SeedMissing")){OverrideColor=seed?Color.LightGreen:Color.IndianRed});
        string key=DivineWorld.Open(Index)?"WindowOpen":"WindowClosed";
        lines.Add(new TooltipLine(Mod,"DivineWindow",Language.GetTextValue("Mods.StarfallThrone.Divine."+key)){OverrideColor=DivineWorld.Open(Index)?Color.LightGreen:Color.IndianRed});
    }
    public override void AddRecipes()
    {
        Recipe recipe=CreateRecipe().AddTile(DivineCatalog.CraftStation(Index));
        if(Index==0)recipe.AddIngredient(ItemID.Gel,30).AddIngredient(ItemID.Glass,8).AddRecipeGroup("StarfallThrone:DivineGold",6).AddIngredient(ItemID.FallenStar,3);
        else if(Index==1)recipe.AddIngredient(ItemID.HellstoneBar,12).AddIngredient(ItemID.Obsidian,20).AddIngredient(ItemID.Bone,30).AddIngredient(ItemID.FallenStar,5);
        else recipe.AddRecipeGroup("StarfallThrone:DivineFragment",20).AddIngredient(ItemID.Ectoplasm,12).AddIngredient(ItemID.ChlorophyteBar,10);
        recipe.Register();
    }
}
public sealed class DivineRecipeGroups : ModSystem
{
    public override void AddRecipeGroups()
    {
        RecipeGroup.RegisterGroup("StarfallThrone:DivineGold",new RecipeGroup(()=>Language.GetTextValue("Mods.StarfallThrone.Divine.AnyGold"),ItemID.GoldBar,ItemID.PlatinumBar));
        RecipeGroup.RegisterGroup("StarfallThrone:DivineFragment",new RecipeGroup(()=>Language.GetTextValue("Mods.StarfallThrone.Divine.AnyFragment"),ItemID.FragmentSolar,ItemID.FragmentVortex,ItemID.FragmentNebula,ItemID.FragmentStardust));
    }
}
public abstract class DivineBagBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Bag"+Index;
    public override void SetStaticDefaults(){ItemID.Sets.BossBag[Type]=true;ItemID.Sets.OpenableBag[Type]=true;Item.ResearchUnlockCount=3;}
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.expert=true;Item.rare=ItemRarityID.Expert;}
    public override bool CanRightClick()=>true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(DivineCatalog.Material(Index),1,30,40));
        loot.Add(ItemDropRule.OneFromOptions(1,Enumerable.Range(Index*4,4).Select(DivineCatalog.Weapon).ToArray()));
        loot.Add(ItemDropRule.Common(DivineCatalog.Expert(Index)));
        loot.Add(ItemDropRule.Common(DivineCatalog.Mask(Index),7));
        loot.Add(ItemDropRule.Common(ItemID.GoldCoin,1,Index==0?3:Index==1?10:25,Index==0?5:Index==1?15:35));
        loot.Add(ItemDropRule.Common(Index==0?ItemID.LesserHealingPotion:Index==1?ItemID.HealingPotion:ItemID.GreaterHealingPotion,1,5,10));
    }
}
public static class DivineLoot
{
    public sealed class WindowCondition : IItemDropRuleCondition
    {
        public readonly int Index;public WindowCondition(int index)=>Index=index;
        public bool CanDrop(DropAttemptInfo info)=>DivineWorld.Open(Index);
        public bool CanShowItemDropInUI()=>true;
        public string GetConditionDescription()=>Language.GetTextValue("Mods.StarfallThrone.Divine.WindowCondition");
    }
    public static void Configure(NPCLoot loot,int index)
    {
        var valid=new LeadingConditionRule(new WindowCondition(index));
        valid.OnSuccess(ItemDropRule.Common(DivineCatalog.Trophy(index),10));
        var normal=new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(DivineCatalog.Material(index),1,24,32));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1,Enumerable.Range(index*4,4).Select(DivineCatalog.Weapon).ToArray()));
        normal.OnSuccess(ItemDropRule.Common(DivineCatalog.Mask(index),7));
        valid.OnSuccess(normal);
        valid.OnSuccess(ItemDropRule.BossBag(DivineCatalog.Bag(index)));
        valid.OnSuccess(ItemDropRule.MasterModeCommonDrop(DivineCatalog.Relic(index)));
        valid.OnSuccess(ItemDropRule.MasterModeDropOnAllPlayers(DivineCatalog.Pet(index),4));
        loot.Add(valid);
    }
}
public abstract class DivineCacheBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Cache"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.rare=ItemRarityID.Orange;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=20;}
    public override bool? UseItem(Player player){if(player.whoAmI==Main.myPlayer)DivineChoiceUI.Open(Index,player.selectedItem);return true;}
    public override bool CanRightClick()=>true;
    public override bool ConsumeItem(Player player)=>false;
    public override void RightClick(Player player)
    {
        if(player.whoAmI!=Main.myPlayer)return;
        for(int i=0;i<58;i++)if(ReferenceEquals(player.inventory[i],Item)){DivineChoiceUI.Open(Index,i);break;}
    }
}
