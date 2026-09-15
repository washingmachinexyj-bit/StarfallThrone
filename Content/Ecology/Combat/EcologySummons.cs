using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Combat;

public abstract class EcologySummonBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Summon" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 36; Item.maxStack = 1; Item.consumable = false;
        Item.useStyle = ItemUseStyleID.HoldUp; Item.useAnimation = Item.useTime = 45;
        Item.UseSound = SoundID.Item44; Item.rare = Index < 4 ? ItemRarityID.Orange : Index < 9 ? ItemRarityID.Pink : ItemRarityID.Red;
    }
    public static bool NeedsNight(int index) => index is 0 or 3 or 9;
    public static bool Valid(Player player, int index)
    {
        if (index < 0 || index >= EcologyCatalog.Count || !player.active || player.dead || player.ghost || !EcologyCatalog.VanillaUnlocked(index)) return false;
        if (NeedsNight(index) && Main.dayTime || EcologyWorld.ActiveBiome(player, true, 600) != index) return false;
        foreach (NPC n in Main.ActiveNPCs) if (n.boss) return false;
        return true;
    }
    public override bool CanUseItem(Player player) => Valid(player, Index);
    public override bool? UseItem(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI == Main.myPlayer)
        { ModPacket p = Mod.GetPacket(); p.Write((byte)51); p.Write((byte)Index); p.Send(); }
        else if (Main.netMode == NetmodeID.SinglePlayer) TrySummon(player, Index);
        return true;
    }
    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        int index = reader.ReadByte();
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
        TrySummon(Main.player[sender], index);
    }
    public static bool TrySummon(Player player, int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !Valid(player, index) || player.HeldItem.type != EcologyCatalog.Summon(index)) return false;
        EcologyRegion region = EcologyWorld.RegionAt(player.Center.ToTileCoordinates());
        if (region == null) return false;
        Vector2 spot = ClampToRegion(player.Center + new Vector2(-player.direction * 300, -150), region.Bounds);
        int type = EcologyCatalog.NPCType(index);
        NPC.SpawnBoss((int)spot.X, (int)spot.Y, type, player.whoAmI);
        return NPC.AnyNPCs(type);
    }
    public static Vector2 ClampToRegion(Vector2 spot, Rectangle bounds)
        => new(Math.Clamp(spot.X, bounds.Left * 16 + 80, bounds.Right * 16 - 80), Math.Clamp(spot.Y, bounds.Top * 16 + 80, bounds.Bottom * 16 - 80));
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe().AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index))).AddCondition(EcologyCatalog.BossCondition(Index));
        switch (Index)
        {
            case 0: r.AddIngredient(EcologyCatalog.Material(0), 12).AddIngredient(EcologyCatalog.Material(0, 1), 6).AddIngredient(ItemID.Glass, 10); break;
            case 1: r.AddIngredient(EcologyCatalog.Material(1), 18).AddIngredient(EcologyCatalog.Material(1, 1), 8).AddIngredient(ItemID.Torch, 5); break;
            case 2: r.AddIngredient(EcologyCatalog.Material(2), 12).AddIngredient(EcologyCatalog.Material(2, 1), 8).AddIngredient(ItemID.BeeWax, 5); break;
            case 3: r.AddIngredient(EcologyCatalog.Material(3), 15).AddIngredient(EcologyCatalog.Material(3, 2), 8).AddRecipeGroup(RecipeGroupID.IronBar, 6); break;
            case 4: r.AddIngredient(EcologyCatalog.Material(4, 1), 12).AddIngredient(EcologyCatalog.Material(4), 15).AddIngredient(ItemID.HellstoneBar, 6); break;
            case 5: r.AddIngredient(EcologyCatalog.Material(5), 18).AddIngredient(EcologyCatalog.Material(5, 2), 12).AddIngredient(ItemID.HallowedBar, 6); break;
            case 6: r.AddIngredient(EcologyCatalog.Material(6, 2), 12).AddIngredient(EcologyCatalog.Material(6, 1), 15).AddIngredient(ItemID.ChlorophyteBar, 6); break;
            case 7: r.AddIngredient(EcologyCatalog.Material(7), 15).AddIngredient(EcologyCatalog.Material(7, 1), 20).AddIngredient(ItemID.ChlorophyteBar, 6); break;
            case 8: r.AddIngredient(EcologyCatalog.Material(8), 12).AddIngredient(EcologyCatalog.Material(8, 1), 12).AddIngredient(EcologyCatalog.Material(8, 2), 15).AddIngredient(ItemID.PearlstoneBlock, 20); break;
            case 9: r.AddIngredient(EcologyCatalog.Material(9, 2), 15).AddIngredient(EcologyCatalog.Material(9, 1), 12).AddIngredient(ItemID.LunarBar, 4); break;
        }
        r.Register();
    }
}
