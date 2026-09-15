#nullable enable
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
using Terraria.ObjectData;

namespace StarfallThrone.Content.Oaths.Support;

public static class OathSupport
{
    public const string Root="StarfallThrone/Content/Assets/Oaths/";
    public static int Item(string name)=>ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static string Text(string key,params object[] args)=>Language.GetTextValue("Mods.StarfallThrone.OathsEquipment."+key,args);
    public static bool Encounter()=>Main.npc.Any(n=>n.active&&(n.boss||n.ModNPC is global::StarfallThrone.Content.Fable.Bosses.FableBossNPC||n.ModNPC is global::StarfallThrone.Content.NPCs.MiniBossNPC||n.ModNPC?.GetType().Name.StartsWith("SeedBoss")==true));
}
public abstract class SupremeMaterialBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeMaterial"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=36;Item.maxStack=9999;Item.material=true;Item.rare=ItemRarityID.Red;Item.value=Item.sellPrice(gold:4);}
}
public abstract class SupremeSummonBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeSummon"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=42;Item.maxStack=1;Item.consumable=false;Item.useStyle=ItemUseStyleID.HoldUp;Item.useAnimation=Item.useTime=45;Item.UseSound=SoundID.Item44;Item.rare=ItemRarityID.Red;}
    public static bool Valid(Player p,int i)=>OathCatalog.Valid(i)&&p.active&&!p.dead&&!p.ghost&&OathWorld.SupremeOpen(i)&&!OathSupport.Encounter()&&NPC.MoonLordCountdown<=0&&!Terraria.GameContent.Events.DD2Event.Ongoing&&Main.invasionType==0&&!Main.pumpkinMoon&&!Main.snowMoon;
    public override bool CanUseItem(Player p)=>Valid(p,Index)&&p.GetModPlayer<Equipment.OathEquipmentPlayer>().SummonCooldown==0;
    public static bool TrySummon(Player p,int i)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!Valid(p,i)||!p.HasItem(OathSupport.Item("SupremeSummon"+i)))return false;
        var state=p.GetModPlayer<Equipment.OathEquipmentPlayer>();if(state.SummonCooldown>0)return false;
        int type=OathCatalog.Boss(i);Vector2 at=Bosses.SupremeBossNPC.Point(p.Center+new Vector2(-420,-120));
        NPC.SpawnBoss((int)at.X,(int)at.Y,type,p.whoAmI);bool spawned=NPC.AnyNPCs(type);
        if(spawned)state.SummonCooldown=120;return spawned;
    }
    public override bool? UseItem(Player p)
    {
        if(p.whoAmI!=Main.myPlayer)return true;
        if(Main.netMode==NetmodeID.MultiplayerClient){var packet=Mod.GetPacket();packet.Write((byte)92);packet.Write((byte)Index);packet.Send();}
        else TrySummon(p,Index);return true;
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        if(Main.netMode!=NetmodeID.Server||sender<0||sender>=Main.maxPlayers||reader.BaseStream.Length-reader.BaseStream.Position!=1)return;
        int i=reader.ReadByte();if(OathCatalog.Valid(i))TrySummon(Main.player[sender],i);
    }
    public override void ModifyTooltips(List<TooltipLine> lines)=>lines.Add(new TooltipLine(Mod,"OathGate",OathSupport.Text(OathWorld.SupremeOpen(Index)?"GateReady":"GateLocked")){OverrideColor=OathWorld.SupremeOpen(Index)?Color.LightGreen:Color.Orange});
    public override void AddRecipes()=>CreateRecipe().AddIngredient(OathSupport.Item("SeedToken"+Index)).AddIngredient(OathSupport.Item("DivineMaterial"+Index),20).AddIngredient(OathSupport.Item("AscendantMaterial"+Index),15).AddIngredient(OathSupport.Item("PantheonMaterial16"),12).AddTile(ModContent.TileType<TriuneAltarTile>()).AddCondition(new Condition(Language.GetText("Mods.StarfallThrone.OathsEquipment.RequiresOath"),()=>OathWorld.SupremeOpen(Index))).Register();
}
public abstract class SupremeBagBase:ModItem
{
    public abstract int Index{get;}
    public override string Texture=>OathSupport.Root+"SupremeBag"+Index;
    public override void SetStaticDefaults(){ItemID.Sets.BossBag[Type]=true;ItemID.Sets.OpenableBag[Type]=true;Item.ResearchUnlockCount=3;}
    public override void SetDefaults(){Item.width=Item.height=40;Item.maxStack=9999;Item.expert=true;Item.rare=ItemRarityID.Expert;}
    public override bool CanRightClick()=>true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(OathCatalog.Material(Index),1,40,55));
        loot.Add(ItemDropRule.OneFromOptions(1,SupremeLoot.Weapons(Index)));
        loot.Add(ItemDropRule.Common(OathSupport.Item("SupremeExpert"+Index)));
        loot.Add(ItemDropRule.Common(OathSupport.Item("SupremeMask"+Index),7));
        loot.Add(ItemDropRule.Common(ItemID.PlatinumCoin,1,1,2));
        loot.Add(ItemDropRule.Common(ItemID.SuperHealingPotion,1,10,15));
    }
}
public static class SupremeLoot
{
    public static int[] Weapons(int i)=>Enumerable.Range(i*4,4).Select(OathCatalog.Weapon).ToArray();
    public static void Add(NPCLoot loot,int i)
    {
        var normal=new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(OathCatalog.Material(i),1,40,55));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1,Weapons(i)));
        normal.OnSuccess(ItemDropRule.Common(OathSupport.Item("SupremeMask"+i),7));loot.Add(normal);
        loot.Add(ItemDropRule.BossBag(OathSupport.Item("SupremeBag"+i)));
        loot.Add(ItemDropRule.Common(OathSupport.Item("SupremeTrophy"+i),10));
        loot.Add(ItemDropRule.MasterModeCommonDrop(OathSupport.Item("SupremeRelic"+i)));
        loot.Add(ItemDropRule.MasterModeDropOnAllPlayers(OathSupport.Item("SupremePetItem"+i),4));
    }
}
public sealed class TriuneAltarItem:ModItem
{
    public override string Texture=>OathSupport.Root+"TriuneAltarItem";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.TileType<TriuneAltarTile>());Item.width=48;Item.height=40;Item.maxStack=9999;Item.rare=ItemRarityID.Red;}
    public override void AddRecipes()=>CreateRecipe().AddIngredient(OathSupport.Item("PantheonMaterial16"),30).AddIngredient(OathSupport.Item("PantheonCore16"),3).AddIngredient(ItemID.LunarBar,20).AddTile(ModContent.Find<ModTile>("StarfallThrone","PantheonStationTile5").Type).AddCondition(new Condition(Language.GetText("Mods.StarfallThrone.OathsEquipment.RequiresAsterion"),()=>global::StarfallThrone.Content.Pantheon.PantheonWorld.Downed[16])).Register();
}
public sealed class TriuneAltarTile:ModTile
{
    public override string Texture=>OathSupport.Root+"TriuneAltarTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);TileObjectData.newTile.LavaDeath=false;TileObjectData.addTile(Type);
        AddMapEntry(new Color(226,211,158),Language.GetText("Mods.StarfallThrone.Items.TriuneAltarItem.DisplayName"));DustType=DustID.GoldFlame;
        RegisterItemDrop(ModContent.ItemType<TriuneAltarItem>());
    }
}

// Main's world-ledger grants one on the first legitimate victory. Opening consumes the
// physical cache using native right-click consumption; it never writes victory flags.
public abstract class SupremeChoiceBase:ModItem
{
    public abstract int Index{get;}
    public int SelectedClass;
    public override string Texture=>OathSupport.Root+"SupremeChoice"+Index;
    public override void SetStaticDefaults(){Item.ResearchUnlockCount=1;ItemID.Sets.OpenableBag[Type]=true;}
    public override void SetDefaults(){Item.width=Item.height=38;Item.maxStack=1;Item.rare=ItemRarityID.Red;Item.useStyle=ItemUseStyleID.HoldUp;Item.useAnimation=Item.useTime=20;Item.consumable=false;}
    public override bool? UseItem(Player p){if(p.whoAmI==Main.myPlayer)SelectedClass=(SelectedClass+1)%4;return true;}
    public override bool CanRightClick()=>true;
    public override void RightClick(Player p){if(p.whoAmI==Main.myPlayer)p.QuickSpawnItem(p.GetSource_OpenItem(Type),OathCatalog.Weapon(Index*4+Math.Clamp(SelectedClass,0,3)));}
    public override void ModifyTooltips(List<TooltipLine> lines)=>lines.Add(new TooltipLine(Mod,"Choice",OathSupport.Text("ChoiceSelected",Lang.GetItemNameValue(OathCatalog.Weapon(Index*4+Math.Clamp(SelectedClass,0,3))))){OverrideColor=OathCatalog.Colors[Index]});
    public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)=>tag["choice"]=SelectedClass;
    public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)=>SelectedClass=Math.Clamp(tag.GetInt("choice"),0,3);
    public override void NetSend(BinaryWriter w)=>w.Write((byte)Math.Clamp(SelectedClass,0,3));
    public override void NetReceive(BinaryReader r)=>SelectedClass=Math.Clamp((int)r.ReadByte(),0,3);
}
public sealed class SupremeChoice0:SupremeChoiceBase{public override int Index=>0;}
public sealed class SupremeChoice1:SupremeChoiceBase{public override int Index=>1;}
public sealed class SupremeChoice2:SupremeChoiceBase{public override int Index=>2;}
