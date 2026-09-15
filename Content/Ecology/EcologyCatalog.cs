using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology;

public static class EcologyCatalog
{
    public const int Count=10;
    public const string Root="StarfallThrone/Content/Assets/Ecology/";
    public static readonly string[] Keys={"Mistmirror","Waxsleep","Amberhive","Knellbone","Backburn","Wildmachine","Hungerbloom","SunkenSun","RainbowTide","Formless"};
    public static readonly string[] Names={"雾镜林地","蜡眠荒庭","蜜珀蚀林","鸣骸墓原","逆烬熔原","失控械土","永饥花庭","沉日遗境","虹渊潮境","无相星壤"};
    public static readonly string[] BossNames={"雾冠巡狩者","千烛合葬者","琥珀育巢者","空钟送葬者","逆燃熔心","荒原总机","万胃繁花","沉日守陵兽","虹渊织潮者","无相巡星者"};
    public static readonly string[] OreNames={"雾镜晶矿","蜡髓矿","蜜珀矿","鸣骸矿","逆烬矿","自律磁矿","饥生矿","沉日矿","虹潮晶矿","无相晶矿"};
    public static readonly Color[] Colors={new(165,219,233),new(232,203,132),new(232,169,53),new(206,208,190),new(222,98,63),new(103,166,176),new(181,108,132),new(215,165,70),new(164,157,244),new(137,163,222)};
    public static readonly int[] Lives={3200,6000,7800,10000,26000,52000,85000,120000,160000,220000};
    public static readonly int[] Defenses={10,16,18,20,26,36,46,52,56,62};
    public static readonly int[] Damage={28,40,46,52,70,92,112,126,140,150};
    public static readonly int[] HeavyDamage={38,54,62,72,96,128,156,172,190,205};
    public static readonly int[] PickRequirements={45,55,60,65,100,180,200,210,215,225};
    public static readonly int[] ToolPower={50,60,65,70,110,190,210,220,225,230};
    public static bool Valid(int i)=>i>=0&&i<Count;
    public static int Item(string name)=>ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static int Tile(string name)=>ModContent.Find<ModTile>("StarfallThrone",name).Type;
    public static int NPCType(int i)=>ModContent.Find<ModNPC>("StarfallThrone","EcologyBoss"+i).Type;
    public static int Material(int i,int slot=0)=>Item("EcologyMaterial"+(i*4+slot));
    public static int Core(int i)=>Item("EcologyCore"+i);
    public static int Summon(int i)=>Item("EcologySummon"+i);
    public static int Bag(int i)=>Item("EcologyBag"+i);
    public static int Relic(int i)=>Item("EcologyRelic"+i);
    public static int Trophy(int i)=>Item("EcologyTrophy"+i);
    public static int Mask(int i)=>Item("EcologyMask"+i);
    public static int Expert(int i)=>Item("EcologyExpert"+i);
    public static int Pet(int i)=>Item("EcologyPetItem"+i);
    public static int Weapon(int i)=>Item("EcologyWeapon"+i);
    public static int Armor(int i)=>Item("EcologyArmor"+i);
    public static int Ore(int i)=>Item("EcologyOre"+i);
    public static int Bar(int i)=>Item("EcologyBar"+i);
    public static int Component(int i)=>Item("EcologyComponent"+i);
    public static int OreTile(int i)=>Tile("EcologyOreTile"+i);
    public static int TerrainTile(int i,int kind=0)=>Tile("EcologyTerrain"+i+"_"+kind);
    public static int StationTile(int i)=>Tile("EcologyStationTile"+i);
    public static int StationItem(int i)=>Item("EcologyStation"+i);
    public static int Station(int i)=>i<4?0:i==9?6:2;
    public static int GearStation(int i)=>i switch{2=>1,5=>3,7=>4,8=>5,9=>7,_=>Station(i)};
    public static bool VanillaUnlocked(int i)=>i switch
    {
        0=>NPC.downedBoss1,1=>NPC.downedBoss2,2=>NPC.downedQueenBee,3=>NPC.downedBoss3,
        4=>Main.hardMode,5=>NPC.downedMechBoss1&&NPC.downedMechBoss2&&NPC.downedMechBoss3,
        6=>NPC.downedPlantBoss,7=>NPC.downedGolemBoss,8=>NPC.downedFishron&&NPC.downedEmpressOfLight,
        9=>NPC.downedMoonlord,_=>false
    };
    public static Condition BossCondition(int i)=>new(Language.GetText("Mods.StarfallThrone.EcologyWorld.Unlock"+i),()=>VanillaUnlocked(i));
    public static Condition DownedCondition(int i)=>new(Language.GetText("Mods.StarfallThrone.EcologyWorld.Defeat"+i),()=>Valid(i)&&EcologyWorld.Downed[i]);
    public static string Text(string key,params object[] args)=>Language.GetTextValue("Mods.StarfallThrone.EcologyWorld."+key,args);
}
