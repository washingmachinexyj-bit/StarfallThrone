using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Ascendant;

public abstract class AscendantMaterialBase : ModItem
{
    public abstract int Index {get;}
    public override string Texture=>AscendantCatalog.Root+"Material"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=32;Item.maxStack=9999;Item.material=true;Item.rare=Index==0?ItemRarityID.Green:Index==1?ItemRarityID.LightRed:ItemRarityID.Red;Item.value=Item.sellPrice(silver:10*(Index+1));}
}
public abstract class AscendantSummonBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>AscendantCatalog.Root+"Summon"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults()
    {Item.width=Item.height=40;Item.maxStack=1;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=45;Item.UseSound=SoundID.Item44;Item.rare=Index==0?ItemRarityID.Green:Index==1?ItemRarityID.LightRed:ItemRarityID.Red;}
    public static bool Valid(Player player,int index)
    {
        if(index is <0 or >=3||!player.active||player.dead||player.ghost||!AscendantWorld.Open(index)||!AscendantWorld.Prerequisites(index))return false;
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
        {ModPacket packet=Mod.GetPacket();packet.Write((byte)60);packet.Write((byte)1);packet.Write((byte)Index);packet.Send();}
        else if(Main.netMode==NetmodeID.SinglePlayer)TrySummon(player,Index);
        return true;
    }
    public static bool TrySummon(Player player,int index)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!Valid(player,index)||player.HeldItem.type!=AscendantCatalog.Summon(index))return false;
        // These flying gods do not need the vanilla randomized ground search. That search can
        // silently fail in empty sky or constrained terrain despite a valid reusable summon.
        Vector2 spawn=Bosses.AscendantBossNPC.WorldPoint(player.Center+new Vector2(-player.direction*420,-120),192);
        int type=AscendantCatalog.NPCType(index);
        NPC.SpawnBoss((int)spawn.X,(int)spawn.Y,type,player.whoAmI);
        return NPC.AnyNPCs(type);
    }
    public override void ModifyTooltips(List<TooltipLine> lines)
    {
        string key=AscendantWorld.Open(Index)?"WindowOpen":"WindowClosed";
        lines.Add(new TooltipLine(Mod,"AscendantWindow",Language.GetTextValue("Mods.StarfallThrone.Ascendant."+key)){OverrideColor=AscendantWorld.Open(Index)?Color.LightGreen:Color.IndianRed});
        lines.Add(new TooltipLine(Mod,"AscendantPrerequisites",AscendantWorld.RequirementText(Index)) {OverrideColor=AscendantWorld.Prerequisites(Index)?Color.LightGreen:Color.Orange});
    }
    public override void AddRecipes()
    {
        Recipe recipe=CreateRecipe().AddTile(AscendantCatalog.CraftStation(Index));
        recipe.AddIngredient(Divine.DivineCatalog.Material(Index),Index==0?12:Index==1?16:20);
        if(Index==0)for(int i=0;i<8;i++)recipe.AddIngredient(Systems.MiniBossData.MaterialType(i),4);
        else for(int i=Index==1?0:4;i<(Index==1?4:9);i++)recipe.AddIngredient(Ecology.EcologyCatalog.Core(i),Index==1?8:10);
        recipe.Register();
    }
}
public abstract class AscendantBagBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>AscendantCatalog.Root+"Bag"+Index;
    public override void SetStaticDefaults(){ItemID.Sets.BossBag[Type]=true;ItemID.Sets.OpenableBag[Type]=true;Item.ResearchUnlockCount=3;}
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.expert=true;Item.rare=ItemRarityID.Expert;}
    public override bool CanRightClick()=>true;
    public override void RightClick(Player player)
    {
        // The bag only requests an already earned, world-persisted entitlement. It cannot mint first wins.
        if(Main.netMode==NetmodeID.MultiplayerClient&&player.whoAmI==Main.myPlayer)
        {var packet=Mod.GetPacket();packet.Write((byte)60);packet.Write((byte)5);packet.Write((byte)Index);packet.Send();}
        else if(Main.netMode==NetmodeID.SinglePlayer)AscendantWorld.ClaimPending(player,Index);
    }
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(AscendantCatalog.Material(Index),1,44,56));
        loot.Add(ItemDropRule.OneFromOptions(1,Enumerable.Range(Index*4,4).Select(AscendantCatalog.Weapon).ToArray()));
        loot.Add(ItemDropRule.Common(AscendantCatalog.Expert(Index)));
        loot.Add(ItemDropRule.Common(AscendantCatalog.Mask(Index),7));
        loot.Add(ItemDropRule.Common(ItemID.GoldCoin,1,Index==0?3:Index==1?10:25,Index==0?5:Index==1?15:35));
        loot.Add(ItemDropRule.Common(Index==0?ItemID.LesserHealingPotion:Index==1?ItemID.HealingPotion:ItemID.GreaterHealingPotion,1,5,10));
    }
}
public static class AscendantLoot
{
    public sealed class WindowCondition : IItemDropRuleCondition
    {
        public readonly int Index;public WindowCondition(int index)=>Index=index;
        public bool CanDrop(DropAttemptInfo info)=>AscendantWorld.Open(Index)&&AscendantWorld.Prerequisites(Index);
        public bool CanShowItemDropInUI()=>true;
        public string GetConditionDescription()=>Language.GetTextValue("Mods.StarfallThrone.Ascendant.WindowCondition");
    }
    public static void Configure(NPCLoot loot,int index)
    {
        var valid=new LeadingConditionRule(new WindowCondition(index));
        valid.OnSuccess(ItemDropRule.Common(AscendantCatalog.Trophy(index),10));
        var normal=new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(AscendantCatalog.Material(index),1,36,44));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1,Enumerable.Range(index*4,4).Select(AscendantCatalog.Weapon).ToArray()));
        normal.OnSuccess(ItemDropRule.Common(AscendantCatalog.Mask(index),7));
        valid.OnSuccess(normal);
        valid.OnSuccess(ItemDropRule.BossBag(AscendantCatalog.Bag(index)));
        valid.OnSuccess(ItemDropRule.MasterModeCommonDrop(AscendantCatalog.Relic(index)));
        valid.OnSuccess(ItemDropRule.MasterModeDropOnAllPlayers(AscendantCatalog.Pet(index),4));
        loot.Add(valid);
    }
}
public abstract class AscendantCacheBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>AscendantCatalog.Root+"Cache"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.rare=ItemRarityID.Orange;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=20;}
    public override bool? UseItem(Player player){if(player.whoAmI==Main.myPlayer)AscendantChoiceUI.Open(Index,player.selectedItem);return true;}
    public override bool CanRightClick()=>true;
    public override bool ConsumeItem(Player player)=>false;
    public override void RightClick(Player player)
    {
        if(player.whoAmI!=Main.myPlayer)return;
        for(int i=0;i<58;i++)if(ReferenceEquals(player.inventory[i],Item)){AscendantChoiceUI.Open(Index,i);break;}
    }
}
