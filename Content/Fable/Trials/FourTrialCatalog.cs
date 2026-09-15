using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Fable.Trials;

/// <summary>
/// The four tiny material trials are deliberately independent from the 18-chapter
/// prologue.  Their index is weakest to strongest: copper, silver, gold, diamond.
/// </summary>
public static class FourTrialCatalog
{
    public const int Count = 4;
    public const string Root = "StarfallThrone/Content/Assets/Fable/Trials/";
    public static readonly string[] Keys = { "Copper", "Silver", "Gold", "Diamond" };
    public static readonly int[] Life = { 1, 2, 4, 8 };
    public static readonly int[] Defense = { 0, 0, 1, 1 };
    public static readonly int[] Contact = { 0, 1, 1, 2 };
    public static readonly int[] Shot = { 0, 0, 1, 1 };
    public static readonly Color[] Colors =
    {
        new(181, 102, 52), new(190, 204, 215), new(232, 191, 70), new(134, 222, 255)
    };

    public static bool Valid(int index) => index >= 0 && index < Count;
    public static string Key(int index) => Keys[Valid(index) ? index : 0];
    public static string BossTexture(int index) => Root + "Trial" + Key(index);
    public static string Text(string key, params object[] args) =>
        Language.GetTextValue("Mods.StarfallThrone.Fable." + key, args);
    public static string Name(int index) =>
        Language.GetTextValue("Mods.StarfallThrone.NPCs.FourTrialBoss" + index + ".DisplayName");
    public static float Progression(int index) => -1.36f + index * .04f;

    public static int BossType(int index) => ModContent.Find<ModNPC>("StarfallThrone", "FourTrialBoss" + index).Type;
    public static int Bag(int index) => Item("FourTrialBag" + index);
    public static int Memento(int index) => Item("FourTrialMemento" + index);
    public static int Trophy(int index) => Item("FourTrialTrophy" + index);
    public static int Relic(int index) => Item("FourTrialRelic" + index);
    public static int Mask(int index) => Item("FourTrialMask" + index);
    public static int Weapon(int index) => Item("FourTrialWeapon" + index);
    public static int Item(string name) => ModContent.Find<ModItem>("StarfallThrone", name).Type;
}
