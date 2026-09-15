using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace StarfallThrone.Content.Pantheon;

public abstract class PantheonRelicBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Relic"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"PantheonRelicTile"+Index).Type);Item.width=40;Item.height=48;Item.maxStack=99;Item.master=true;Item.rare=ItemRarityID.Master;}
}
public abstract class PantheonTrophyBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Trophy"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"PantheonTrophyTile"+Index).Type);Item.width=Item.height=40;Item.maxStack=99;Item.rare=ItemRarityID.Orange;}
}
public abstract class PantheonMaskBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Mask"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=30;Item.vanity=true;Item.rare=ItemRarityID.Green;}
}
public abstract class PantheonDisplayTile : ModTile
{
    public abstract int Index{get;}
    public virtual bool Trophy=>false;
    public override string Texture=>PantheonCatalog.Root+(Trophy?"TrophyTile":"RelicTile")+Index;
    public int ItemType=>Trophy?PantheonCatalog.Trophy(Index):PantheonCatalog.Relic(Index);
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);TileObjectData.newTile.LavaDeath=false;
        if(Trophy){TileObjectData.newTile.AnchorBottom=default;TileObjectData.newTile.AnchorWall=true;}
        TileObjectData.addTile(Type);DustType=DustID.GoldFlame;HitSound=SoundID.Tink;
        AddMapEntry(PantheonCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items."+((Trophy?"PantheonTrophy":"PantheonRelic")+Index)+".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i,int j){yield return new Item(ItemType);}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=ItemType;}
}
public abstract class PantheonPetItemBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"PetItem"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.ZephyrFish);Item.shoot=ModContent.Find<ModProjectile>(Mod.Name,"PantheonPet"+Index).Type;
        Item.buffType=ModContent.Find<ModBuff>(Mod.Name,"PantheonPetBuff"+Index).Type;
        Item.width=Item.height=32;Item.master=true;Item.rare=ItemRarityID.Master;
    }
    public override bool? UseItem(Player player){if(player.whoAmI==Main.myPlayer)player.AddBuff(Item.buffType,3600);return true;}
}
public abstract class PantheonPetBuffBase : ModBuff
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"PetItem"+Index;
    public override void SetStaticDefaults(){Main.buffNoTimeDisplay[Type]=true;Main.vanityPet[Type]=true;}
    public override void Update(Player player,ref int buffIndex)
    {
        player.buffTime[buffIndex]=18000;int type=ModContent.Find<ModProjectile>(Mod.Name,"PantheonPet"+Index).Type;
        if(player.whoAmI==Main.myPlayer&&player.ownedProjectileCounts[type]==0)
            Projectile.NewProjectile(player.GetSource_Buff(buffIndex),player.Center,Vector2.Zero,type,0,0,player.whoAmI);
    }
}
public abstract class PantheonPetBase : ModProjectile
{
    public abstract int Index{get;}
    public override string Texture=>PantheonCatalog.Root+"Pet"+Index;
    public override void SetStaticDefaults()=>Main.projPet[Type]=true;
    public override void SetDefaults(){Projectile.width=Projectile.height=32;Projectile.aiStyle=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.penetrate=-1;Projectile.netImportant=true;}
    public override bool? CanDamage()=>false;
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];int buff=ModContent.Find<ModBuff>(Mod.Name,"PantheonPetBuff"+Index).Type;
        if(p.dead||!p.active){p.ClearBuff(buff);Projectile.Kill();return;}
        if(p.HasBuff(buff))Projectile.timeLeft=2;
        Vector2 target=p.Center+new Vector2(-p.direction*(48+Index%3*8),-42+(float)Math.Sin(Projectile.ai[0]++*.045)*8);
        if(Vector2.DistanceSquared(target,Projectile.Center)>1600*1600){Projectile.Center=target;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
        Projectile.velocity=Vector2.Lerp(Projectile.velocity,(target-Projectile.Center)*.06f,.15f);
        if(Projectile.velocity.Length()>12)Projectile.velocity=Vector2.Normalize(Projectile.velocity)*12;
        Projectile.spriteDirection=Projectile.velocity.X<0?-1:1;Projectile.rotation=Projectile.velocity.X*.015f;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D tex=TextureAssets.Projectile[Type].Value;
        Main.EntitySpriteDraw(tex,Projectile.Center-Main.screenPosition,null,Color.Lerp(lightColor,Color.White,.35f),Projectile.rotation,tex.Size()/2,.7f,Projectile.spriteDirection==-1?SpriteEffects.FlipHorizontally:SpriteEffects.None);return false;
    }
}
