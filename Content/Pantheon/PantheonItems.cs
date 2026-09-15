using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Pantheon;

public abstract class PantheonMaterialBase : ModItem
{
    public abstract int Index{get;}
    public virtual bool Core=>false;
    public override string Texture=>PantheonCatalog.Root+(Core?"Core":"Material")+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=Core?5:25;
    public override void SetDefaults(){Item.width=Item.height=32;Item.maxStack=9999;Item.material=true;Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(gold:Core?4:1);}
}
public abstract class PantheonCoreBase : PantheonMaterialBase {public override bool Core=>true;}
public abstract class PantheonSummonBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Summon"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=1;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=45;Item.UseSound=SoundID.Item44;Item.rare=ItemRarityID.Red;}
    public static bool Location(Player p,int i)=>i switch
    {
        0 or 9 or 12=>p.ZoneOverworldHeight,
        1 or 15=>!Main.dayTime&&p.ZoneOverworldHeight,
        2=>p.ZoneDirtLayerHeight||p.ZoneRockLayerHeight,
        3 or 5 or 8=>!Main.dayTime,
        4 or 11=>p.ZoneJungle,
        6=>p.ZoneUnderworldHeight,
        7=>p.ZoneHallow&&p.ZoneOverworldHeight,
        10=>p.ZoneOverworldHeight||p.ZoneDirtLayerHeight||p.ZoneRockLayerHeight,
        13=>p.ZoneBeach,
        14 or 16=>p.ZoneOverworldHeight||p.ZoneSkyHeight,
        _=>false
    };
    public static bool Valid(Player p,int i)=>i>=0&&i<17&&p!=null&&p.active&&!p.dead&&!p.ghost&&PantheonWorld.CanSummon(i)
        &&!PantheonCatalog.AnyEncounter()&&NPC.MoonLordCountdown<=0&&!Terraria.GameContent.Events.DD2Event.Ongoing&&Location(p,i);
    public override bool CanUseItem(Player p)=>Valid(p,Index)&&p.GetModPlayer<PantheonSummonPlayer>().Cooldown==0;
    public override bool? UseItem(Player p)
    {
        if(p.whoAmI==Main.myPlayer&&Main.netMode==NetmodeID.MultiplayerClient)
        {ModPacket packet=Mod.GetPacket();packet.Write((byte)70);packet.Write((byte)Index);packet.Send();}
        else if(Main.netMode==NetmodeID.SinglePlayer)PantheonWorld.TrySummon(Index,p.whoAmI);
        return true;
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        // Sender identity is supplied by tML, never by a client payload. A request contains one index only.
        if(Main.netMode!=NetmodeID.Server||sender<0||sender>=Main.maxPlayers||reader.BaseStream.Length-reader.BaseStream.Position!=1)return;
        int i=reader.ReadByte();if(i<17)PantheonWorld.TrySummon(i,sender);
    }
    public override void ModifyTooltips(List<TooltipLine> lines)
    {
        string required=Index==0?Language.GetTextValue("Mods.StarfallThrone.NPCs.CelestialArkNPC.DisplayName"):Language.GetTextValue("Mods.StarfallThrone.Pantheon.Name"+(Index-1));
        lines.Add(new TooltipLine(Mod,"PantheonGate",Language.GetTextValue("Mods.StarfallThrone.Pantheon.Requires",required)) {OverrideColor=PantheonWorld.CanSummon(Index)?Color.LightGreen:Color.Orange});
    }
    public override void AddRecipes()
    {
        Recipe r=CreateRecipe().AddTile(PantheonCatalog.CraftStation(PantheonCatalog.Tier(Index)));
        if(Index==0)r.AddIngredient(Voyage.VoyageCatalog.Material(16),12).AddIngredient(Voyage.VoyageCatalog.Core(16),2).AddIngredient(Mining.MiningCatalog.BarType(12),6);
        else r.AddIngredient(PantheonCatalog.Material(Index-1),12).AddIngredient(PantheonCatalog.Core(Index-1),2).AddIngredient(PantheonRecipes.ThemeIngredients[Index],PantheonRecipes.ThemeAmounts[Index]);
        r.Register();
    }
}
public abstract class PantheonBagBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Bag"+Index;
    public override void SetStaticDefaults(){ItemID.Sets.BossBag[Type]=true;ItemID.Sets.OpenableBag[Type]=true;Item.ResearchUnlockCount=3;}
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.expert=true;Item.rare=ItemRarityID.Expert;}
    public override bool CanRightClick()=>true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(PantheonCatalog.Material(Index),1,44,56));
        loot.Add(ItemDropRule.Common(PantheonCatalog.Core(Index),1,2,2));
        loot.Add(ItemDropRule.OneFromOptions(1,PantheonLoot.Weapons(Index)));
        loot.Add(ItemDropRule.Common(PantheonCatalog.Expert(Index)));
        loot.Add(ItemDropRule.Common(PantheonCatalog.Mask(Index),7));
        loot.Add(ItemDropRule.Common(ItemID.GoldCoin,1,25+Index*2,35+Index*2));
        loot.Add(ItemDropRule.Common(ItemID.SuperHealingPotion,1,5,10));
    }
}
public static class PantheonLoot
{
    public static int[] Weapons(int i)=>Enumerable.Range(i*2,i==16?4:2).Select(PantheonCatalog.Weapon).ToArray();
    public sealed class ProgressCondition : IItemDropRuleCondition
    {
        readonly int index;public ProgressCondition(int i)=>index=i;
        public bool CanDrop(DropAttemptInfo info)=>PantheonWorld.CanSummon(index);
        public bool CanShowItemDropInUI()=>true;
        public string GetConditionDescription()=>Language.GetTextValue("Mods.StarfallThrone.Pantheon.LootCondition");
    }
    public static void Add(NPCLoot loot,int i)
    {
        var valid=new LeadingConditionRule(new ProgressCondition(i));
        var normal=new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(PantheonCatalog.Material(i),1,36,44));
        normal.OnSuccess(ItemDropRule.Common(PantheonCatalog.Core(i),1,2,2));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1,Weapons(i)));
        normal.OnSuccess(ItemDropRule.Common(PantheonCatalog.Mask(i),7));valid.OnSuccess(normal);
        valid.OnSuccess(ItemDropRule.BossBag(PantheonCatalog.Bag(i)));
        valid.OnSuccess(ItemDropRule.Common(PantheonCatalog.Trophy(i),10));
        valid.OnSuccess(ItemDropRule.MasterModeCommonDrop(PantheonCatalog.Relic(i)));
        valid.OnSuccess(ItemDropRule.MasterModeDropOnAllPlayers(PantheonCatalog.Pet(i),4));loot.Add(valid);
    }
    public static void OnVictory(NPC npc,int i)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||i<0||i>=17||npc.type!=PantheonCatalog.BossType(i)||!PantheonWorld.CanSummon(i)||npc.life>0)return;
        PantheonWorld.SetDowned(i);
    }
}
