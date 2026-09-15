using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Fable.Trials;

public abstract class FourTrialBagBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialBag" + Index;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.BossBag[Type] = true;
        Item.ResearchUnlockCount = 1;
    }

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.maxStack = 9999;
        Item.value = 0;
        Item.rare = ItemRarityID.Expert;
        Item.expert = true;
    }

    public override bool CanRightClick() => true;

    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(FourTrialCatalog.Weapon(Index)));
        loot.Add(ItemDropRule.Common(FourTrialCatalog.Memento(Index)));
        loot.Add(ItemDropRule.Common(FourTrialCatalog.Mask(Index), 10));
    }
}

public abstract class FourTrialWeaponBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialWeapon" + Index;

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    public override void SetDefaults()
    {
        Item.width = Item.height = 26;
        Item.damage = 1;
        Item.knockBack = .1f;
        Item.value = 0;
        Item.rare = ItemRarityID.White;
        Item.useTime = Item.useAnimation = 55 + Index * 5;
        Item.autoReuse = false;
        switch (Index)
        {
            case 0:
                Item.DamageType = DamageClass.Melee;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.UseSound = SoundID.Item1;
                break;
            case 1:
                Item.DamageType = DamageClass.Ranged;
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.noMelee = true;
                Item.shoot = ModContent.ProjectileType<FourTrialSilverNeedle>();
                Item.shootSpeed = 3.1f;
                Item.UseSound = SoundID.Item5;
                break;
            case 2:
                Item.DamageType = DamageClass.Magic;
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.noMelee = true;
                Item.mana = 4;
                Item.shoot = ModContent.ProjectileType<FourTrialGoldBell>();
                Item.shootSpeed = 2.6f;
                Item.UseSound = SoundID.Item8;
                break;
            default:
                Item.DamageType = DamageClass.Summon;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.noMelee = true;
                Item.noUseGraphic = true;
                Item.shoot = ModContent.ProjectileType<FourTrialDiamondMinion>();
                Item.shootSpeed = 0;
                Item.buffType = ModContent.BuffType<FourTrialMinionBuff>();
                Item.UseSound = SoundID.Item44;
                break;
        }
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    {
        if (Index == 3)
        {
            foreach (Projectile projectile in Main.ActiveProjectiles)
                if (projectile.owner == player.whoAmI && projectile.ModProjectile is FourTrialDiamondMinion)
                    projectile.Kill();
            player.AddBuff(ModContent.BuffType<FourTrialMinionBuff>(), 3600);
            Projectile.NewProjectile(source, position, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        return false;
    }
}

public abstract class FourTrialMementoBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialMemento" + Index;

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "FourTrialMementoTile" + Index).Type);
        Item.width = Item.height = 30;
        Item.maxStack = 99;
        Item.value = 0;
        Item.rare = ItemRarityID.Blue;
    }
}

public abstract class FourTrialTrophyBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialTrophy" + Index;

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "FourTrialTrophyTile" + Index).Type);
        Item.width = Item.height = 40;
        Item.maxStack = 99;
        Item.value = 0;
        Item.rare = ItemRarityID.Orange;
    }
}

public abstract class FourTrialRelicBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialRelic" + Index;

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name, "FourTrialRelicTile" + Index).Type);
        Item.width = 40;
        Item.height = 48;
        Item.maxStack = 99;
        Item.master = true;
        Item.rare = ItemRarityID.Master;
    }
}

public abstract class FourTrialMaskBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialMask" + Index;

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    public override void SetDefaults()
    {
        Item.width = Item.height = 30;
        Item.maxStack = 1;
        Item.value = 0;
        Item.vanity = true;
        Item.rare = ItemRarityID.Green;
    }
}

public abstract class FourTrialDisplayTile : ModTile
{
    public abstract int Index { get; }
    public abstract string DisplayKind { get; }
    public override string Texture => FourTrialCatalog.Root + "Trial" + DisplayKind + "Tile" + Index;

    private int ItemType => FourTrialCatalog.Item("FourTrial" + DisplayKind + Index);

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.LavaDeath = false;
        if (DisplayKind == "Trophy")
        {
            TileObjectData.newTile.AnchorBottom = default;
            TileObjectData.newTile.AnchorWall = true;
        }
        TileObjectData.addTile(Type);
        DustType = DustID.GoldFlame;
        HitSound = SoundID.Tink;
        AddMapEntry(FourTrialCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.FourTrial" + DisplayKind + Index + ".DisplayName"));
        RegisterItemDrop(ItemType);
    }

    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2;
        Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ItemType;
    }
}

public sealed class FourTrialBag0 : FourTrialBagBase { public override int Index => 0; }
public sealed class FourTrialBag1 : FourTrialBagBase { public override int Index => 1; }
public sealed class FourTrialBag2 : FourTrialBagBase { public override int Index => 2; }
public sealed class FourTrialBag3 : FourTrialBagBase { public override int Index => 3; }

public sealed class FourTrialMemento0 : FourTrialMementoBase { public override int Index => 0; }
public sealed class FourTrialMemento1 : FourTrialMementoBase { public override int Index => 1; }
public sealed class FourTrialMemento2 : FourTrialMementoBase { public override int Index => 2; }
public sealed class FourTrialMemento3 : FourTrialMementoBase { public override int Index => 3; }

public sealed class FourTrialTrophy0 : FourTrialTrophyBase { public override int Index => 0; }
public sealed class FourTrialTrophy1 : FourTrialTrophyBase { public override int Index => 1; }
public sealed class FourTrialTrophy2 : FourTrialTrophyBase { public override int Index => 2; }
public sealed class FourTrialTrophy3 : FourTrialTrophyBase { public override int Index => 3; }

public sealed class FourTrialRelic0 : FourTrialRelicBase { public override int Index => 0; }
public sealed class FourTrialRelic1 : FourTrialRelicBase { public override int Index => 1; }
public sealed class FourTrialRelic2 : FourTrialRelicBase { public override int Index => 2; }
public sealed class FourTrialRelic3 : FourTrialRelicBase { public override int Index => 3; }

[AutoloadEquip(EquipType.Head)] public sealed class FourTrialMask0 : FourTrialMaskBase { public override int Index => 0; }
[AutoloadEquip(EquipType.Head)] public sealed class FourTrialMask1 : FourTrialMaskBase { public override int Index => 1; }
[AutoloadEquip(EquipType.Head)] public sealed class FourTrialMask2 : FourTrialMaskBase { public override int Index => 2; }
[AutoloadEquip(EquipType.Head)] public sealed class FourTrialMask3 : FourTrialMaskBase { public override int Index => 3; }

public sealed class FourTrialWeapon0 : FourTrialWeaponBase { public override int Index => 0; }
public sealed class FourTrialWeapon1 : FourTrialWeaponBase { public override int Index => 1; }
public sealed class FourTrialWeapon2 : FourTrialWeaponBase { public override int Index => 2; }
public sealed class FourTrialWeapon3 : FourTrialWeaponBase { public override int Index => 3; }

public sealed class FourTrialMementoTile0 : FourTrialDisplayTile { public override int Index => 0; public override string DisplayKind => "Memento"; }
public sealed class FourTrialMementoTile1 : FourTrialDisplayTile { public override int Index => 1; public override string DisplayKind => "Memento"; }
public sealed class FourTrialMementoTile2 : FourTrialDisplayTile { public override int Index => 2; public override string DisplayKind => "Memento"; }
public sealed class FourTrialMementoTile3 : FourTrialDisplayTile { public override int Index => 3; public override string DisplayKind => "Memento"; }

public sealed class FourTrialTrophyTile0 : FourTrialDisplayTile { public override int Index => 0; public override string DisplayKind => "Trophy"; }
public sealed class FourTrialTrophyTile1 : FourTrialDisplayTile { public override int Index => 1; public override string DisplayKind => "Trophy"; }
public sealed class FourTrialTrophyTile2 : FourTrialDisplayTile { public override int Index => 2; public override string DisplayKind => "Trophy"; }
public sealed class FourTrialTrophyTile3 : FourTrialDisplayTile { public override int Index => 3; public override string DisplayKind => "Trophy"; }

public sealed class FourTrialRelicTile0 : FourTrialDisplayTile { public override int Index => 0; public override string DisplayKind => "Relic"; }
public sealed class FourTrialRelicTile1 : FourTrialDisplayTile { public override int Index => 1; public override string DisplayKind => "Relic"; }
public sealed class FourTrialRelicTile2 : FourTrialDisplayTile { public override int Index => 2; public override string DisplayKind => "Relic"; }
public sealed class FourTrialRelicTile3 : FourTrialDisplayTile { public override int Index => 3; public override string DisplayKind => "Relic"; }
