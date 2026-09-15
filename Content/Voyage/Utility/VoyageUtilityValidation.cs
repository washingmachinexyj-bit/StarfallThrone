using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Voyage.Utility;

/// <summary>Runs only in the isolated validation client, never modifies a player's real cargo/world.</summary>
public static class VoyageUtilityValidation
{
    public static void Validate() => ValidateData();
    public static void ValidateData()
    {
        Mod mod = ModContent.GetInstance<global::StarfallThrone.StarfallThrone>();
        Require(mod.GetContent<VoyageUtilityItem>().Count() == 9, "utility item count");
        Require(mod.GetContent<VoyageFurnitureItem>().Count() == 85, "furniture item count");
        for (int i = 0; i < 9; i++)
        {
            int type = VoyageCatalog.Item("VoyageUtility" + i);
            Require(Language.Exists("Mods.StarfallThrone.Items.VoyageUtility" + i + ".Tooltip"), "utility tooltip " + i);
            Require(Enumerable.Range(0, Recipe.numRecipes).Any(n => Main.recipe[n].createItem.type == type
                && !Main.recipe[n].Disabled && Main.recipe[n].requiredTile.Contains(
                    global::StarfallThrone.Content.Mining.MiningRecipes.ExpectedStationForItem(new Item(type)))), "utility stage recipe " + i);
        }
        for (int i = 0; i < 85; i++)
        {
            Item item = ContentSamples.ItemsByType[VoyageCatalog.Item("VoyageFurniture" + i)];
            Require(item.createTile >= 0, "furniture placement " + i);
            Require(Language.Exists("Mods.StarfallThrone.Items.VoyageFurniture" + i + ".Tooltip"), "furniture tooltip " + i);
            Recipe recipe = Enumerable.Range(0, Recipe.numRecipes).Select(n => Main.recipe[n])
                .First(r => r.createItem.type == item.type);
            Require(recipe.requiredItem.Count(x => !x.IsAir) == 1
                && recipe.requiredItem.First(x => !x.IsAir).type == VoyageCatalog.Material(i / 5), "furniture material-only recipe " + i);
            Require(recipe.createItem.stack == (i % 5 >= 3 ? 25 : 1), "furniture recipe yield " + i);
            if (i % 5 < 3)
            {
                TileObjectData data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
                Require(data.Width == 3 && data.Height == 3, "decor footprint " + i);
            }
            else if (i % 5 == 3) Require(TileID.Sets.Platforms[item.createTile], "native platform geometry " + i);
            else Require(Main.tileSolid[item.createTile], "solid construction block " + i);
        }
        VoyageUtilityPlayer first = new(); first.Initialize();
        VoyageUtilityPlayer second = new(); second.Initialize();
        first.Cargo[0] = new Item(ItemID.DirtBlock, 327);
        first.Cargo[39] = new Item(VoyageCatalog.Material(16), 19);
        TagCompound save = new(); first.SaveData(save); second.LoadData(save);
        Require(second.Cargo.Length == 40 && second.Cargo[0].stack == 327 && second.Cargo[39].stack == 19, "cargo save/load counts");
        Require(second.Cargo[39].type == VoyageCatalog.Material(16) && second.Cargo[1].IsAir, "cargo save/load types");
        first.Cargo[0].stack = 1;
        Require(second.Cargo[0].stack == 327 && !ReferenceEquals(first.Cargo, second.Cargo), "cargo character isolation");
        Item drill = ContentSamples.ItemsByType[ModContent.ItemType<VoyageUtility2>()];
        Require(drill.pick == 350 && drill.channel && drill.shoot == ModContent.ProjectileType<VoyageEngineeringDrill>(), "drill definition");
        Require(Main.lightPet[ModContent.BuffType<VoyageSearchlightBuff>()], "light-pet classification");
        Require(ProjectileID.Sets.LightPet[ModContent.ProjectileType<VoyageSearchlightPet>()], "pet projectile classification");
        // Empty and truncated messages must be ignored without throwing or altering state.
        foreach (byte[] payload in new[] { Array.Empty<byte>(), new byte[] { 1 }, new byte[] { 2, 0 }, new byte[] { 255 } })
        {
            using BinaryReader reader = new(new MemoryStream(payload));
            VoyageUtilityPlayer.ReceivePacket(reader, -1);
        }
        mod.Logger.Info("VOYAGE_UTILITY_DATA_PASS utility=9 furniture=85 cargoSlots=40 recipes=94 isolatedSave=1 boundedPackets=1");
    }

    public static void Run()
    {
        if (!Main.gameMenu || !Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"))
            throw new InvalidOperationException("Utility simulation is restricted to the isolated main-menu smoke client.");
        if (Main.dedServ) throw new InvalidOperationException("Utility rendering requires the validation client.");
        int previousId = Main.myPlayer, previousNet = Main.netMode, previousInvasion = Main.invasionType;
        int previousSpawnX = Main.spawnTileX, previousSpawnY = Main.spawnTileY;
        bool previousPumpkin = Main.pumpkinMoon, previousSnow = Main.snowMoon, previousInventory = Main.playerInventory;
        bool previousDraw = Main.drawToScreen;
        Vector2 previousScreen = Main.screenPosition;
        Player previousPlayer = Main.player[0];
        NPC previousNPC = Main.npc[0];
        bool[] activeNPCs = Main.npc.Select(n => n.active).ToArray();
        Item[] previousItems = (Item[])Main.item.Clone();
        var device = Main.instance.GraphicsDevice;
        var previousTargets = device.GetRenderTargets();
        using RenderTarget2D atlas = new(device, 1000, 1400);
        Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer; Main.invasionType = 0;
        Main.pumpkinMoon = Main.snowMoon = false; Main.drawToScreen = true;
        Main.spawnTileX = 620; Main.spawnTileY = 90;
        Player player = new() { active = true, dead = false, whoAmI = 0, position = new Vector2(624 * 16, 87 * 16) };
        Main.player[0] = player;
        player.ResetEffects(); player.selectedItem = 0;
        foreach (NPC npc in Main.npc) npc.active = false;
        for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
        // Throwaway main-menu arena, never a loaded world. Same explicit smoke guard as combat tests.
        for (int x = 600; x < 650; x++) for (int y = 55; y < 100; y++)
        {
            Tile tile = Main.tile[x, y]; tile.ClearEverything(); tile.WallType = WallID.Stone;
            if (y >= 90) { tile.HasTile = true; tile.TileType = TileID.Dirt; }
        }
        try
        {
            StorageTransfers(player);
            RecallAndRepair(player);
            device.SetRenderTarget(atlas); device.Clear(Color.Transparent);
            int cell = 0;
            foreach (int utility in new[] { 0, 3, 5, 7, 8 })
            {
                Item item = new(VoyageCatalog.Item("VoyageUtility" + utility));
                PlaceDrawBreak(item, cell++, player);
            }
            for (int i = 0; i < 85; i++)
                PlaceDrawBreak(new Item(VoyageCatalog.Item("VoyageFurniture" + i)), cell++, player);
            // Verify the other two ark frame styles also retain exactly one item drop.
            for (int phase = 1; phase <= 2; phase++)
                PlaceDrawBreak(new Item(ModContent.ItemType<VoyageUtility8>()), cell++, player, phase);
            for (int i = 0; i < 17; i++)
            {
                PlaceDrawBreak(new Item(VoyageCatalog.Relic(i)), cell++, player);
                PlaceDrawBreak(new Item(VoyageCatalog.Trophy(i)), cell++, player);
            }
            PlaceDrawBreak(new Item(ModContent.ItemType<VoyageWorkbench>()), cell++, player);
            for (int i = 1; i <= 6; i++)
            {
                if (i is 3 or 5) continue;
                Item item = new(VoyageCatalog.Item("VoyageUtility" + i));
                Vector2 center = new(cell % 10 * 100 + 50, cell / 10 * 100 + 40);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                VoyageFurnitureDrawing.Icon(Main.spriteBatch, "Utility" + i, center, 48, Color.White);
                Main.spriteBatch.End();
                cell++;
            }
            device.SetRenderTargets(previousTargets);
            Color[] pixels = new Color[atlas.Width * atlas.Height]; atlas.GetData(pixels);
            for (int id = 0; id < cell; id++)
            {
                int colored = 0;
                for (int x = id % 10 * 100 + 16; x < id % 10 * 100 + 84; x++)
                for (int y = id / 10 * 100 + 6; y < id / 10 * 100 + 72; y++)
                    if (pixels[y * 1000 + x].A > 32) colored++;
                Require(colored >= 16, "empty actual draw cell " + id);
            }
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "voyage-utility-runtime.png"));
            using (FileStream stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            Log("VOYAGE_UTILITY_RUNTIME_PASS placed=127 drawCells=131 breakDrops=127 relics=17 trophies=17 workbench=1 recallCases=8 repairRefresh=1 cargoConservation=1");
        }
        finally
        {
            VoyageUtilityUI.Close();
            device.SetRenderTargets(previousTargets);
            Main.myPlayer = previousId; Main.netMode = previousNet; Main.invasionType = previousInvasion;
            Main.spawnTileX = previousSpawnX; Main.spawnTileY = previousSpawnY;
            Main.pumpkinMoon = previousPumpkin; Main.snowMoon = previousSnow; Main.playerInventory = previousInventory;
            Main.drawToScreen = previousDraw; Main.screenPosition = previousScreen;
            Main.player[0] = previousPlayer; Main.npc[0] = previousNPC;
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i].active = activeNPCs[i];
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = previousItems[i];
        }
    }

    private static void StorageTransfers(Player player)
    {
        VoyageUtilityPlayer storage = player.GetModPlayer<VoyageUtilityPlayer>();
        for (int i = 0; i < 50; i++) player.inventory[i] = new Item();
        storage.Initialize();
        storage.Cargo[0] = new Item(ItemID.DirtBlock, 100);
        player.inventory[10] = new Item(ItemID.DirtBlock, 36);
        player.inventory[11] = new Item(ItemID.DirtBlock, 50) { favorited = true };
        storage.QuickStackCargo();
        Require(storage.Cargo[0].stack == 136 && player.inventory[10].IsAir && player.inventory[11].stack == 50,
            "explicit quick stack preserves favorites and item count");
        storage.Withdraw(0); storage.Withdraw(0);
        Require(storage.Cargo[0].IsAir && player.inventory.Take(50).Where(i => i.type == ItemID.DirtBlock).Sum(i => i.stack) == 186,
            "repeated withdrawal cannot duplicate items");
        storage.Cargo[39] = new Item(VoyageCatalog.Material(16), 19);
        TagCompound saved = new(); storage.SaveData(saved);
        VoyageUtilityPlayer restored = new(); restored.LoadData(saved);
        Require(restored.Cargo[39].stack == 19 && !ReferenceEquals(restored.Cargo[39], storage.Cargo[39]), "storage save roundtrip retains independent item instances");
    }

    private static void RecallAndRepair(Player player)
    {
        var state = player.GetModPlayer<VoyageUtilityPlayer>();
        player.inventory[0] = new Item(ModContent.ItemType<VoyageUtility4>());
        player.channel = true; player.dead = false;
        Vector2 initial = player.position;
        state.RequestRecall(); player.position += new Vector2(3, 0); state.PostUpdate();
        Require(state.RecallTicks == 0, "recall movement interruption");
        player.position = initial; state.RequestRecall(); state.PostHurt(new Player.HurtInfo { Damage = 10 });
        Require(state.RecallTicks == 0, "recall damage interruption");
        state.RequestRecall(); player.dead = true; state.UpdateDead(); player.dead = false;
        Require(state.RecallTicks == 0, "recall death interruption");
        state.RequestRecall(); player.channel = false;
        for (int i = 0; i < 15; i++) state.PostUpdate();
        Require(state.RecallTicks == 0, "recall release interruption");
        player.channel = true; state.RequestRecall(); player.inventory[0] = new Item(ItemID.DirtBlock); state.PostUpdate();
        Require(state.RecallTicks == 0, "recall held item interruption");
        player.inventory[0] = new Item(ModContent.ItemType<VoyageUtility4>());
        Main.npc[0] = new NPC { active = true, boss = true };
        state.RequestRecall(); Require(state.RecallTicks == 0, "recall cannot start during a boss");
        Main.npc[0].active = false; state.RequestRecall(); Main.invasionType = 1; state.PostUpdate(); Main.invasionType = 0;
        Require(state.RecallTicks == 0, "recall encounter interruption");
        player.statLifeMax = player.statLifeMax2 = 500;
        player.statLife = 123; player.immune = false; player.immuneTime = 0;
        state.RequestRecall();
        for (int i = 0; i < 119; i++) state.PostUpdate();
        Require(player.position == initial && state.RecallTicks == 120, "recall must finish its full two-second channel");
        state.PostUpdate();
        Require(state.RecallTicks == 0 && player.position != initial && player.statLife == 123 && !player.immune && player.immuneTime == 0,
            "successful recall grants neither healing nor immunity");
        Require(!Collision.SolidCollision(player.position, player.width, player.height), "recall destination is not solid");
        int type = ModContent.BuffType<VoyageRepairBuff>();
        player.AddBuff(type, 100, quiet: true); player.AddBuff(type, 36000, quiet: true);
        Require(player.buffType.Count(t => t == type) == 1 && player.buffTime[Array.IndexOf(player.buffType, type)] == 36000,
            "repair station refreshes one buff slot");
        var buff = ModContent.GetInstance<VoyageRepairBuff>();
        for (int tick = 0; tick < 3; tick++)
        {
            player.ResetEffects(); player.statDefense += 100; player.statManaMax2 = 200;
            int slot = Array.IndexOf(player.buffType, type); buff.Update(player, ref slot);
            Require(player.statDefense == 110 && player.statManaMax2 == 240 && player.statLife == 123,
                $"repair stats after reset: defense={player.statDefense}, mana={player.statManaMax2}, life={player.statLife}");
        }
    }

    private static void PlaceDrawBreak(Item item, int cell, Player player, int phase = 0)
    {
        const int left = 620, top = 87;
        for (int x = left - 2; x <= left + 4; x++) for (int y = top - 2; y < top + 3; y++)
        { Main.tile[x, y].ClearEverything(); Main.tile[x, y].WallType = WallID.Stone; }
        for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
        TileObjectData data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
        bool multi = data != null && data.Width > 1;
        // All samples share a floor at y=90; a two-tile-high workbench needs
        // one temporary support row beneath this three-tile-high fixture.
        if (multi && data.Height == 2)
            for (int dx = 0; dx < data.Width; dx++)
            { Tile support = Main.tile[left + dx, top + 2]; support.HasTile = true; support.TileType = TileID.Dirt; }
        if (multi)
            WorldGen.PlaceObject(left + data!.Origin.X, top + data.Origin.Y, item.createTile, mute: true, style: item.placeStyle);
        else WorldGen.PlaceTile(left, top, item.createTile, mute: true, forced: true, style: item.placeStyle);
        Require(Main.tile[left, top].HasTile && Main.tile[left, top].TileType == item.createTile, "actual placement " + item.Name);
        int w = multi ? data!.Width : 1, h = multi ? data!.Height : 1;
        if (phase > 0)
        {
            player.position = new Vector2(left * 16, top * 16);
            for (int i = 0; i < phase; i++) VoyageUtilityPlayer.RequestFurniture(2, left, top);
            Require(Main.tile[left, top].TileFrameX / 54 == phase, "ark display phase change");
        }
        Vector2 center = new(cell % 10 * 100 + 50, cell / 10 * 100 + 40);
        Main.screenPosition = new Vector2(left * 16 + w * 8, top * 16 + h * 8) - center;
        ModTile tileType = TileLoader.GetTile(item.createTile);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        for (int dx = 0; dx < w; dx++) for (int dy = 0; dy < h; dy++)
        {
            Tile part = Main.tile[left + dx, top + dy];
            Require(part.HasTile && part.TileType == item.createTile, "complete multi-tile placement " + item.Name);
            if (multi)
            {
                Require(part.TileFrameX % 54 == dx * 18 && part.TileFrameY % 54 == dy * 18, "multi-tile frame range " + item.Name);
                Require(TileObjectData.GetTileStyle(part) == (phase > 0 ? phase : item.placeStyle), "multi-tile style " + item.Name);
            }
            if (tileType.PreDraw(left + dx, top + dy, Main.spriteBatch))
            {
                Texture2D texture = TextureAssets.Tile[item.createTile].Value;
                Require(part.TileFrameX >= 0 && part.TileFrameY >= 0 && part.TileFrameX + 16 <= texture.Width && part.TileFrameY + 16 <= texture.Height,
                    "native construction atlas frame " + item.Name);
                var draw = new Terraria.DataStructures.TileDrawInfo { tileLight = Color.White };
                tileType.DrawEffects(left + dx, top + dy, Main.spriteBatch, ref draw);
                Main.spriteBatch.Draw(texture, new Vector2((left + dx) * 16, (top + dy) * 16) - Main.screenPosition,
                    new Rectangle(part.TileFrameX, part.TileFrameY, 16, 16), draw.tileLight);
            }
            tileType.PostDraw(left + dx, top + dy, Main.spriteBatch);
        }
        Main.spriteBatch.End();
        // Exercise the engine's entire multi-tile destruction path, not just GetItemDrops.
        int cargoBefore = player.GetModPlayer<VoyageUtilityPlayer>().Cargo.Where(i => !i.IsAir).Sum(i => i.stack);
        WorldGen.KillTile(left + (multi ? 1 : 0), top + (multi ? 1 : 0));
        WorldGen.SquareTileFrame(left, top, true);
        Item[] drops = Main.item.Where(i => i.active && i.type == item.type).ToArray();
        Require(drops.Sum(i => i.stack) == 1, "exactly one item per real tile break " + item.Name + ": " + drops.Sum(i => i.stack));
        Require(Main.item.Where(i => i.active).Sum(i => i.stack) == 1, "no extra item types on furniture break " + item.Name);
        Require(player.GetModPlayer<VoyageUtilityPlayer>().Cargo.Where(i => !i.IsAir).Sum(i => i.stack) == cargoBefore, "breaking furniture does not touch personal cargo");
    }

    private static void Log(string message) => ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().Logger.Info(message);
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Voyage utility validation failed: " + label);
    }
}
