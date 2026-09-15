using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.ObjectData;
using Terraria.Enums;
using Terraria.GameContent;

namespace StarfallThrone.Content.Ecology;

public abstract class EcologyRelicBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Relic" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "EcologyRelicTile" + Index).Type);
        Item.width = 40; Item.height = 48; Item.maxStack = 99; Item.master = true; Item.rare = ItemRarityID.Master;
    }
}
public abstract class EcologyTrophyBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Trophy" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "EcologyTrophyTile" + Index).Type);
        Item.width = Item.height = 36; Item.maxStack = 99; Item.rare = ItemRarityID.Orange;
    }
}
public abstract class EcologyMaskBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Mask" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 28; Item.vanity = true; Item.rare = ItemRarityID.Green;
    }
}
public abstract class EcologyDisplayTileBase : ModTile
{
    public abstract int Index { get; }
    public virtual bool Trophy => false;
    public override string Texture => EcologyCatalog.Root + (Trophy ? "TrophyTile" : "RelicTile") + Index;
    private int Drop => Trophy ? EcologyCatalog.Trophy(Index) : EcologyCatalog.Relic(Index);
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3); TileObjectData.newTile.LavaDeath = false;
        if (Trophy) { TileObjectData.newTile.AnchorBottom = default; TileObjectData.newTile.AnchorWall = true; }
        TileObjectData.addTile(Type); DustType = DustID.GoldFlame; HitSound = SoundID.Tink;
        AddMapEntry(EcologyCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items." +
            (Trophy ? "EcologyTrophy" : "EcologyRelic") + Index + ".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(Drop); }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2; Main.LocalPlayer.cursorItemIconEnabled = true; Main.LocalPlayer.cursorItemIconID = Drop;
    }
}
public abstract class EcologyPetItemBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Pet" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.ZephyrFish);
        Item.shoot = ModContent.Find<ModProjectile>(Mod.Name, "EcologyPetProjectile" + Index).Type;
        Item.buffType = ModContent.Find<ModBuff>(Mod.Name, "EcologyPetBuff" + Index).Type;
        Item.width = Item.height = 32; Item.master = true; Item.rare = ItemRarityID.Master;
    }
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer) player.AddBuff(Item.buffType, 3600);
        return true;
    }
}
public abstract class EcologyPetBuffBase : ModBuff
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Core" + Index;
    public override void SetStaticDefaults() { Main.buffNoTimeDisplay[Type] = true; Main.vanityPet[Type] = true; }
    public override void Update(Player player, ref int buffIndex)
    {
        if (player.dead) { player.DelBuff(buffIndex); buffIndex--; return; }
        player.buffTime[buffIndex] = 18000;
        int type = ModContent.Find<ModProjectile>(Mod.Name, "EcologyPetProjectile" + Index).Type;
        if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[type] == 0)
            Projectile.NewProjectile(player.GetSource_Buff(buffIndex), player.Center, Vector2.Zero, type, 0, 0, player.whoAmI);
    }
}
public abstract class EcologyPetProjectileBase : ModProjectile
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Pet" + Index;
    public override void SetStaticDefaults() => Main.projPet[Type] = true;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 26; Projectile.aiStyle = -1; Projectile.tileCollide = false;
        Projectile.ignoreWater = true; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner];
        int buff = ModContent.Find<ModBuff>(Mod.Name, "EcologyPetBuff" + Index).Type;
        if (!p.active || p.dead || !p.HasBuff(buff)) { Projectile.Kill(); return; }
        Projectile.timeLeft = 2;
        float time = Projectile.ai[0]++ * .035f;
        // Three families: trotting-bob companions, hovering winged companions, and orbital companions.
        Vector2 offset = Index is 0 or 4 or 7
            ? new Vector2(-p.direction * 52, 3 + MathF.Abs(MathF.Sin(time * 2)) * -12)
            : Index is 5 or 8 or 9
                ? new Vector2(MathF.Cos(time) * 60, -48 + MathF.Sin(time) * 18)
                : new Vector2(-p.direction * 48, -40 + MathF.Sin(time) * 10);
        Vector2 target = p.Center + offset;
        if (Vector2.DistanceSquared(target, Projectile.Center) > 1200 * 1200)
        {
            Projectile.Center = target; Projectile.velocity = Vector2.Zero;
            if (Projectile.owner == Main.myPlayer) Projectile.netUpdate = true;
        }
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target - Projectile.Center) * .085f, .16f);
        if (Projectile.velocity.LengthSquared() > 144) Projectile.velocity = Vector2.Normalize(Projectile.velocity) * 12;
        Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * .02f, -.22f, .22f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        float scale = Math.Min(1f, 42f / Math.Max(tex.Width, tex.Height));
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, .25f),
            Projectile.rotation, tex.Size() / 2, scale, Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        return false;
    }
}
public abstract class EcologyBannerBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Banner" + Index;
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1; ItemID.Sets.KillsToBanner[Type] = Index % 4 == 3 ? 25 : 50;
        ItemID.Sets.BannerStrength[Type] = ItemID.Sets.BannerStrength[Item.BannerToItem(Item.NPCtoBanner(NPCID.Zombie))];
    }
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "EcologyBannerTile" + Index).Type);
        Item.width = 12; Item.height = 30; Item.maxStack = 99; Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 2);
    }
}
public abstract class EcologyBannerTileBase : ModTile
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "BannerTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.Height = 3; TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
        TileObjectData.newTile.Origin = new Point16(0, 0); TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom, 1, 0);
        TileObjectData.newTile.DrawYOffset = -2;
        TileObjectData.addTile(Type); DustType = DustID.Silk; HitSound = SoundID.Dig;
        AddMapEntry(EcologyCatalog.Colors[Index / 4], Language.GetText("Mods.StarfallThrone.Items.EcologyBanner" + Index + ".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(EcologyCatalog.Item("EcologyBanner" + Index)); }
    public override void NearbyEffects(int i, int j, bool closer)
    {
        if (closer || Main.dedServ) return;
        int npcType = ModContent.Find<ModNPC>(Mod.Name, "EcologyMob" + Index).Type;
        Main.SceneMetrics.NPCBannerBuff[Item.NPCtoBanner(npcType)] = true;
        Main.SceneMetrics.hasBanner = true;
    }
}
public abstract class EcologyLampBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Lamp" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "EcologyLampTile" + Index).Type);
        Item.width = 18; Item.height = 30; Item.maxStack = 9999; Item.value = Item.sellPrice(copper: 20);
    }
    public override void AddRecipes() => CreateRecipe(2).AddIngredient(EcologyCatalog.Material(Index), 2).AddIngredient(ItemID.Torch, 2)
        .AddIngredient(ItemID.Glass, 2).AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index)))
        .AddCondition(EcologyCatalog.BossCondition(Index)).Register();
}
public abstract class EcologyLampTileBase : ModTile
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "LampTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true;
        Main.tileLighted[Type] = true; Main.tileLavaDeath[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2); TileObjectData.addTile(Type);
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch); DustType = DustID.Glass; HitSound = SoundID.Shatter;
        AddMapEntry(EcologyCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.EcologyLamp" + Index + ".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(EcologyCatalog.Item("EcologyLamp" + Index)); }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 color = EcologyCatalog.Colors[Index].ToVector3() * .85f + new Vector3(.12f);
        r = color.X; g = color.Y; b = color.Z;
    }
}

