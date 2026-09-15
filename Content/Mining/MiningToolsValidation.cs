using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.GameContent;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Voyage;

namespace StarfallThrone.Content.Mining;

/// <summary>Callable only in the opt-in throwaway main-menu test client, never a user's loaded world.</summary>
public static class MiningToolsValidation
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Mining tools validation: " + message); checks++; }
    private static void Log(string message) => ModContent.GetInstance<MiningNativeHooks>().Mod.Logger.Info(message);
    private static void Guard() => Check(Main.gameMenu && Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"), "isolated menu guard");
    public static void Data()
    {
        Guard();
        Mod mod = ModContent.GetInstance<MiningNativeHooks>().Mod;
        Check(mod.GetContent<MiningTool>().Count() == 13, "thirteen new tool IDs, existing drill preserved");
        Check(mod.GetContent<MiningAccessory>().Count() == 5, "five functional accessories");
        Check(mod.GetContent<MiningSurvey>().Count() == 4, "four survey/boundary tools");
        Check(mod.GetContent<MiningUtilityItem>().Count() == 15, "fifteen utility furniture items");
        Check(mod.GetContent<MiningSeed>().Count() == 13, "thirteen finite seeds");
        foreach (ModItem content in mod.GetContent<ModItem>().Where(item => item is MiningTool or MiningAccessory or MiningSurvey or MiningUtilityItem or MiningSeed))
        {
            Check(Language.Exists("Mods.StarfallThrone.Items." + content.Name + ".DisplayName"), "localized name " + content.Name);
            Check(Language.Exists("Mods.StarfallThrone.Items." + content.Name + ".Tooltip"), "localized tooltip " + content.Name);
            Check(Main.recipe.Take(Recipe.numRecipes).Any(r => !r.Disabled && r.createItem.type == content.Type), "recipe " + content.Name);
        }
        for (int ore = 0; ore < 13; ore++)
        {
            int type = MiningCatalog.ItemType("MiningSeed" + ore);
            Recipe seed = Main.recipe.Take(Recipe.numRecipes).First(r => !r.Disabled && r.createItem.type == type);
            Check(!seed.requiredItem.Any(i => i.type == MiningCatalog.OreType(ore) || i.type == MiningCatalog.BarType(ore)), "seed no circular ore dependency " + ore);
            Check(seed.Conditions.Count > 0, "seed world gate " + ore);
            var crystal = TileLoader.GetTile(MiningCrystalTile.TypeFor(ore));
            Check(crystal.MinPick == MiningCatalog.PickRequirements[ore] && !crystal.CanExplode(0, 0) && !crystal.CanReplace(0, 0, TileID.Dirt), "crystal bypass guards " + ore);
        }
        for (int tool = 0; tool < 14; tool++)
        {
            Check(!MiningPlayer.ShapeTargets(tool, 0, 0, 0).Any(), "area OFF " + tool);
            for (int mode = 1; mode < MiningTool.ModeCount(tool); mode++)
            {
                Point[] targets = MiningPlayer.ShapeTargets(tool, mode, 20, 20).ToArray();
                Check(targets.Length <= 14 && targets.Distinct().Count() == targets.Length && !targets.Contains(new Point(20, 20)), "bounded unique area " + tool + "/" + mode);
            }
        }
        var original = new MiningCrystallizerEntity();
        original.LoadData(new TagCompound { ["JobsV1"] = new[] { 2, 12 }, ["WorkV1"] = 6000 });
        TagCompound save = new(); original.SaveData(save);
        var restored = new MiningCrystallizerEntity(); restored.LoadData(save);
        Check(restored.PendingCount == 2 && restored.CurrentOre == 2 && Math.Abs(restored.ProgressFraction - 6000f / 7200f) < .0001f, "TE save roundtrip");
        var malformed = new MiningCrystallizerEntity();
        malformed.LoadData(new TagCompound { ["JobsV1"] = new[] { -1, 3, 99, 4, 5 }, ["WorkV1"] = int.MaxValue });
        Check(malformed.PendingCount == 2 && malformed.CurrentOre == 3 && malformed.ProgressFraction == 1, "TE bounded malformed save");
        foreach (byte[] payload in new[] { Array.Empty<byte>(), new byte[] { 0 }, new byte[] { 1, 0 }, new byte[] { 2 }, new byte[] { 255 } })
        { using BinaryReader reader = new(new MemoryStream(payload)); MiningPlayer.ReceivePacket(reader, -1); }
        Log("MINING_TOOLS_DATA_PASS checks=" + checks + " items=50 seeds=13 noCircularSeedRecipes=13 boundedModes=14 TEroundtrip=1");
    }
    public static void Run()
    {
        Guard();
        LinePixelBoundsChecks();
        int previousNet = Main.netMode, previousId = Main.myPlayer, previousSpawnX = Main.spawnTileX, previousSpawnY = Main.spawnTileY;
        Player previousPlayer = Main.player[0];
        Item[] oldItems = (Item[])Main.item.Clone();
        bool oldActions = WorldGen.noTileActions, oldGen = WorldGen.gen;
        bool[] oldMini = (bool[])MiniBossWorld.Downed.Clone(), oldVoyage = (bool[])VoyageWorld.Downed.Clone();
        bool[] oldPrimordial = (bool[])PrimordialWorld.Downed.Clone(), oldStarfall = (bool[])StarfallWorld.Downed.Clone();
        MiningWorld world = ModContent.GetInstance<MiningWorld>(); TagCompound oldWorld = new(); world.SaveWorldData(oldWorld);
        const int x = 1040, y = 150;
        Check(WorldGen.InWorld(x + 20, y + 20, 5), "test arena exists");
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.spawnTileX = Main.spawnTileY = 30;
            WorldGen.noTileActions = WorldGen.gen = false;
            world.ClearWorld();
            Player player = new() { active = true, dead = false, whoAmI = 0, position = new Vector2((x - 3) * 16, (y - 1) * 16) };
            Main.player[0] = player; player.ResetEffects(); player.selectedItem = 0; player.controlUseItem = true;
            for (int a = x - 20; a <= x + 25; a++) for (int b = y - 15; b <= y + 20; b++) Main.tile[a, b].ClearEverything();
            ClearItems();
            AccessoryChecks(player);
            PlayerPersistenceChecks(player, x, y);
            AreaChecks(player, x, y);
            CrystalChecks(player, x, y);
            VatChecks(player, x + 10, y);
            FurnitureChecks(player, x + 10, y + 10);
            PlatformChecks(player, x, y + 8);
            PacketChecks(player, x, y);
            Log("MINING_TOOLS_RUNTIME_PASS checks=" + checks + " areaActualMining=1 protectedBounds=1 crystalThresholds=13 crystalDrops=52 TEjobsConserved=1 serverAuthority=1 accessoriesNonStack=1 cushion=1");
        }
        finally
        {
            if (TileEntity.ByPosition.TryGetValue(new Point16(x + 10, y), out TileEntity entity) && entity is MiningCrystallizerEntity)
                ModContent.GetInstance<MiningCrystallizerEntity>().Kill(x + 10, y);
            Main.netMode = previousNet; Main.myPlayer = previousId; Main.player[0] = previousPlayer;
            Main.spawnTileX = previousSpawnX; Main.spawnTileY = previousSpawnY;
            WorldGen.noTileActions = oldActions; WorldGen.gen = oldGen;
            MiniBossWorld.Downed = oldMini; VoyageWorld.Downed = oldVoyage; PrimordialWorld.Downed = oldPrimordial; StarfallWorld.Downed = oldStarfall;
            world.LoadWorldData(oldWorld);
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = oldItems[i];
        }
    }
    private static void LinePixelBoundsChecks()
    {
        var device = Main.instance.GraphicsDevice;
        var previousTargets = device.GetRenderTargets();
        using var probe = new RenderTarget2D(device, 128, 128);
        Color[] pixels = new Color[128 * 128];
        int totalVisible = 0;
        try
        {
            // These are real GPU pixels from the same helper called by the furniture
            // renderer, not return values or a CPU reimplementation of its geometry.
            for (int rotation = 0; rotation < 4; rotation++)
            {
                device.SetRenderTarget(probe); device.Clear(Color.Transparent);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                MiningUtilityTile.DrawWaymarkerArrow(Main.spriteBatch, new Vector2(64, 64), rotation);
                Main.spriteBatch.End();
                device.SetRenderTargets(previousTargets);
                probe.GetData(pixels);
                Rectangle expected = rotation % 2 == 0 ? new Rectangle(54, 57, 21, 15) : new Rectangle(57, 54, 15, 21);
                int visible = 0, minX = 128, minY = 128, maxX = -1, maxY = -1;
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    if (pixels[y * 128 + x].A == 0) continue;
                    Check(expected.Contains(x, y), "GPU arrow overflow rotation=" + rotation + " pixel=" + x + "," + y);
                    visible++; minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
                Check(visible >= 20 && visible <= 110, "GPU arrow must be visible and compact rotation=" + rotation + " pixels=" + visible);
                Check((rotation % 2 == 0 ? maxX - minX : maxY - minY) >= 14, "GPU arrow retains16pixel shaft rotation=" + rotation);
                totalVisible += visible;
            }

            // The shared helper also draws construction outlines. Verify an actual
            // rectangle stays hollow and within two pixels of its requested edges.
            device.SetRenderTarget(probe); device.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            Vector2 a = new(32, 40), b = new(80, 40), c = new(80, 72), d = new(32, 72);
            MiningUtilityTile.DrawLine(Main.spriteBatch, a, b, Color.Cyan, 2);
            MiningUtilityTile.DrawLine(Main.spriteBatch, b, c, Color.Cyan, 2);
            MiningUtilityTile.DrawLine(Main.spriteBatch, c, d, Color.Cyan, 2);
            MiningUtilityTile.DrawLine(Main.spriteBatch, d, a, Color.Cyan, 2);
            Main.spriteBatch.End(); device.SetRenderTargets(previousTargets); probe.GetData(pixels);
            Rectangle outer = new(30, 38, 53, 37), interior = new(35, 43, 42, 26);
            int borderPixels = 0;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                if (pixels[y * 128 + x].A == 0) continue;
                Check(outer.Contains(x, y) && !interior.Contains(x, y), "GPU outline overflow/fill at " + x + "," + y);
                borderPixels++;
            }
            Check(borderPixels >= 200 && borderPixels <= 400, "GPU outline visible bounded perimeter pixels=" + borderPixels);
            Check(pixels[40 * 128 + 56].A > 0 && pixels[56 * 128 + 80].A > 0
                && pixels[72 * 128 + 56].A > 0 && pixels[56 * 128 + 32].A > 0, "GPU outline all four sides present");
            Log("MINING_LINE_PIXEL_BOUNDS_PASS arrowRotations=4 arrowVisiblePixels=" + totalVisible
                + " outlinePixels=" + borderPixels + " explicitSource=1x1 noOutsidePixels=1");
        }
        finally { device.SetRenderTargets(previousTargets); }
    }
    private static void ClearItems() { for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item(); }
    private static void Put(int x, int y, int type)
    { Tile tile = Main.tile[x, y]; tile.ClearEverything(); tile.HasTile = true; tile.TileType = (ushort)type; }
    private static void AccessoryChecks(Player player)
    {
        MiningPlayer state = player.GetModPlayer<MiningPlayer>(); state.ResetEffects(); player.pickSpeed = 1; player.blockRange = 0;
        foreach (int index in new[] { 0, 1, 4 }) new Item(MiningCatalog.ItemType("MiningAccessory" + index)).ModItem.UpdateAccessory(player, false);
        state.PostUpdateEquips(); Check(Math.Abs(player.pickSpeed - .85f) < .0001f && player.blockRange == 3 && state.AccessoryMagnet, "gather upgrades nonstacking");
        player.pickSpeed = 1; player.blockRange = 0;
        var drop = new Item(ItemID.StoneBlock); var tag = drop.GetGlobalItem<MiningExtractedItem>();
        MiningPlayer.ExtractionOwner = 0;
        tag.OnSpawn(drop, new EntitySource_TileBreak(1040, 150)); MiningPlayer.ExtractionOwner = -1;
        int range = 0; tag.GrabRange(drop, player, ref range); Check(range == 240, "own-extracted mineral magnet");
        var loot = new Item(ItemID.GoldCoin); var lootTag = loot.GetGlobalItem<MiningExtractedItem>();
        lootTag.OnSpawn(loot, player.GetSource_Misc("MiningValidation")); range = 0; lootTag.GrabRange(loot, player, ref range);
        Check(range == 0 && lootTag.Owner == -1, "no generic loot magnet");
    }
    private static void AreaChecks(Player player, int x, int y)
    {
        player.inventory[0] = new Item(MiningCatalog.ToolType(11));
        MiningPlayer state = player.GetModPlayer<MiningPlayer>(); state.Modes[11] = 1;
        state.SelectionActive = true; state.Selection = new Rectangle(x - 1, y - 1, 2, 3);
        foreach (Point p in MiningPlayer.ShapeTargets(11, 1, x, y)) Put(p.X, p.Y, TileID.Stone);
        for (int i = 0; i < 10; i++) state.MineExtra(x, y);
        Check(!Main.tile[x - 1, y - 1].HasTile && !Main.tile[x, y - 1].HasTile, "area actually mines ordinary stone");
        Check(Main.tile[x + 1, y - 1].HasTile, "area selection boundary preserves outside");
        state.SelectionActive = false;
        Put(x - 1, y, MiningCatalog.OreTileType(12));
        for (int i = 0; i < 10; i++) state.MineExtra(x, y);
        Check(Main.tile[x - 1, y].HasTile, "490 power cannot mine550 genesis ore");
        Put(x - 1, y + 1, TileID.Stone); Put(x - 2, y + 1, TileID.Containers);
        state.MineExtra(x, y); Check(Main.tile[x - 1, y + 1].HasTile, "protect furniture adjacent supports");
        Main.tile[x - 2, y + 1].ClearEverything();
        Check(!MiningPlayer.SafeExtra(player, -100, y) && !MiningPlayer.SafeExtra(player, x + 100, y), "bounds and reach checks");
        for (int a = x - 3; a <= x + 3; a++) for (int b = y - 3; b <= y + 3; b++) Main.tile[a, b].ClearEverything();
        state.Modes[11] = 0;
    }
    private static void PlayerPersistenceChecks(Player player, int x, int y)
    {
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        state.Modes[13] = 3; state.SelectedOre = 9;
        state.SelectionActive = true; state.Selection = new Rectangle(x, y, 12, 8);
        TagCompound tag = new(); state.SaveData(tag);
        var clone = new MiningPlayer(); clone.LoadData(tag);
        Check(clone.Modes[13] == 3 && clone.SelectedOre == 9 && clone.SelectionActive && clone.Selection == state.Selection, "player saved tool modes/filter/boundary");
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) state.WriteState(writer);
            stream.Position = 0; var synced = new MiningPlayer();
            using var reader = new BinaryReader(stream);
            Check(synced.ReadState(reader) && synced.Modes[13] == 3 && synced.SelectedOre == 9 && synced.Selection == state.Selection && stream.Position == stream.Length,
                "bounded player state codec roundtrip");
        }
        using (var stream = new MemoryStream(new byte[] { 13 })) using (var reader = new BinaryReader(stream))
            Check(!state.ReadState(reader) && state.SelectedOre == 9, "truncated state rejected without partial mutation");
        state.Modes[13] = 0; state.SelectedOre = 0; state.SelectionActive = false;
    }
    private static void CrystalChecks(Player player, int x, int y)
    {
        for (int ore = 0; ore < 13; ore++)
        {
            ClearItems(); player.hitTile = new HitTile();
            player.inventory[0] = new Item(MiningCatalog.ToolType(ore));
            Put(x, y, MiningCrystalTile.TypeFor(ore));
            for (int hit = 0; hit < 20; hit++) player.PickTile(x, y, MiningCatalog.PickRequirements[ore] - 1);
            Check(Main.tile[x, y].HasTile, "underpowered crystal reject " + ore);
            for (int hit = 0; hit < 40 && Main.tile[x, y].HasTile; hit++) player.PickTile(x, y, MiningCatalog.PickRequirements[ore]);
            Check(!Main.tile[x, y].HasTile, "crystal native mining " + ore);
            Check(Main.item.Where(i => i.active && i.type == MiningCatalog.OreType(ore)).Sum(i => i.stack) == 4, "crystal exactly four ore " + ore);
        }
    }
    private static void VatChecks(Player player, int x, int y)
    {
        for (int dx = 0; dx < 3; dx++) Put(x + dx, y + 3, TileID.Stone);
        int type = MiningUtilityTile.TypeFor(12); TileObjectData data = TileObjectData.GetTileData(type, 0);
        WorldGen.PlaceObject(x + data.Origin.X, y + data.Origin.Y, type, mute: true);
        Check(Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type, "native crystallizer placement");
        if (!TileEntity.ByPosition.ContainsKey(new Point16(x, y))) ModContent.GetInstance<MiningCrystallizerEntity>().Place(x, y);
        var vat = (MiningCrystallizerEntity)TileEntity.ByPosition[new Point16(x, y)];
        player.position = new Vector2((x - 3) * 16, y * 16); player.inventory[0] = new Item(MiningCatalog.ItemType("MiningSeed0"), 3);
        MiniBossWorld.Downed[0] = false;
        vat.Interact(player); Check(vat.PendingCount == 0 && player.inventory[0].stack == 3, "locked seed cannot load");
        MiniBossWorld.Downed[0] = true;
        vat.Interact(player); Check(vat.PendingCount == 1 && player.inventory[0].stack == 2, "server-side paid loading");
        vat.Interact(player); Check(vat.PendingCount == 1 && player.inventory[0].stack == 2, "full queue rejects without consumption");
        TagCompound save = new(); vat.SaveData(save); var loaded = new MiningCrystallizerEntity(); loaded.LoadData(save);
        Check(loaded.PendingCount == 1, "active-job save preserves payment");
        Main.netMode = NetmodeID.MultiplayerClient; vat.Interact(player); vat.Update();
        Check(vat.PendingCount == 1 && player.inventory[0].stack == 2, "client cannot consume or grow"); Main.netMode = NetmodeID.SinglePlayer;
        Put(x, y - 2, TileID.Stone);
        for (int tick = 0; tick < 1800; tick++) vat.Update();
        Check(vat.PendingCount == 1 && Main.tile[x, y - 2].TileType == TileID.Stone, "blocked output cannot overwrite");
        Main.tile[x, y - 2].ClearEverything();
        Check(vat.TryCommit() && vat.PendingCount == 0, "finite job commits exactly once");
        Check(!vat.TryCommit(), "completed job cannot duplicate");
        int crystals = 0;
        for (int dx = 0; dx < 3; dx++) for (int dy = -2; dy <= -1; dy++)
            if (Main.tile[x + dx, y + dy].HasTile && Main.tile[x + dx, y + dy].TileType == MiningCrystalTile.TypeFor(0)) crystals++;
        Check(crystals == 6, "six outputs equals24ore");
        ClearItems(); vat.OnKill();
        Check(!Main.item.Any(i => i.active && i.type == MiningCatalog.ItemType("MiningSeed0")), "completed seed never refunded");
        vat.Interact(player); Check(vat.PendingCount == 1 && player.inventory[0].stack == 1, "next job payment");
        vat.OnKill(); vat.OnKill();
        Check(Main.item.Where(i => i.active && i.type == MiningCatalog.ItemType("MiningSeed0")).Sum(i => i.stack) == 1, "repeated destroy cannot double refund");
        ModContent.GetInstance<MiningCrystallizerEntity>().Kill(x, y);
    }
    private static void PlatformChecks(Player player, int x, int y)
    {
        Put(x, y, MiningUtilityTile.TypeFor(13));
        player.position = new Vector2(x * 16, y * 16 - player.height - 3); player.velocity = new Vector2(0, 8);
        player.controlDown = false; player.fallStart = 0;
        player.GetModPlayer<MiningPlayer>().PreUpdateMovement();
        Check(player.fallStart == (int)(player.position.Y / 16), "cushion resets fall before landing");
        Main.tile[x, y].ClearEverything();
    }
    private static void FurnitureChecks(Player player, int x, int y)
    {
        var device = Main.instance.GraphicsDevice;
        var targets = device.GetRenderTargets();
        Vector2 oldScreen = Main.screenPosition; bool oldDraw = Main.drawToScreen;
        using var atlas = new RenderTarget2D(device, 750, 280);
        Main.drawToScreen = true;
        try
        {
        device.SetRenderTarget(atlas); device.Clear(new Color(24, 29, 39));
        for (int index = 0; index < 15; index++)
        {
            for (int a = x - 2; a < x + 6; a++) for (int b = y - 3; b < y + 6; b++) Main.tile[a, b].ClearEverything();
            for (int a = x - 2; a < x + 6; a++) Put(a, y + 3, TileID.Stone);
            ClearItems(); player.position = new Vector2((x - 3) * 16, y * 16);
            int type = MiningUtilityTile.TypeFor(index);
            if (index < 13)
            {
                TileObjectData data = TileObjectData.GetTileData(type, 0);
                WorldGen.PlaceObject(x + data.Origin.X, y + data.Origin.Y, type, mute: true);
            }
            else WorldGen.PlaceTile(x, y, type, mute: true, forced: true);
            Check(Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type, "native furniture placement " + index);
            if (index is 0 or 11)
            {
                MiningUtilityTile.Interact(player, x, y);
                Check(Main.tile[x, y].TileFrameX / 54 == 1, "functional furniture state " + index);
            }
            Vector2 cell = new(index % 5 * 150 + 12, index / 5 * 90 + 12);
            Main.screenPosition = new Vector2(x * 16, y * 16) - cell;
            ModTile tile = TileLoader.GetTile(type);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            int size = index < 13 ? 3 : 1;
            for (int dx = 0; dx < size; dx++) for (int dy = 0; dy < size; dy++)
                Check(!tile.PreDraw(x + dx, y + dy, Main.spriteBatch), "custom world renderer " + index);
            // Adjacent full-color inventory sprite makes differences between world and item art visible.
            Texture2D itemTexture = TextureAssets.Item[MiningUtilityTile.ItemFor(index)].Value;
            Main.spriteBatch.Draw(itemTexture, cell + new Vector2(72, 6), Color.White);
            Main.spriteBatch.End();
            WorldGen.KillTile(x, y); WorldGen.SquareTileFrame(x, y, true);
            Check(Main.item.Where(i => i.active && i.type == MiningUtilityTile.ItemFor(index)).Sum(i => i.stack) == 1, "single furniture item drop " + index);
            Check(Main.item.Where(i => i.active).Sum(i => i.stack) == 1, "no unintended furniture drops " + index);
        }
        device.SetRenderTargets(targets);
        string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "mining-utility-world-runtime.png"));
        using (FileStream stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
        Log("MINING_UTILITY_RENDER_PASS actualPlaced=15 actualPreDraw=15 singleBreakDrops=15 statefulFurniture=2");
        }
        finally { device.SetRenderTargets(targets); Main.screenPosition = oldScreen; Main.drawToScreen = oldDraw; }
    }
    private static void PacketChecks(Player player, int x, int y)
    {
        Main.netMode = NetmodeID.Server;
        Put(x, y, TileID.Stone); player.inventory[0] = new Item(ItemID.Wood); player.active = true;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) { writer.Write((byte)1); writer.Write((short)x); writer.Write((short)y); }
            stream.Position = 0; using var reader = new BinaryReader(stream); MiningPlayer.ReceivePacket(reader, 0);
        }
        Check(Main.tile[x, y].HasTile, "network mining without real pick rejected");
        var tag = new Item(ItemID.StoneBlock).GetGlobalItem<MiningExtractedItem>();
        using (var stream = new MemoryStream(new byte[] { 0, 0 })) using (var reader = new BinaryReader(stream)) tag.NetReceive(new Item(ItemID.StoneBlock), reader);
        Check(tag.Owner == -1, "client cannot forge extracted ownership");
        Main.netMode = NetmodeID.SinglePlayer;
    }
}
