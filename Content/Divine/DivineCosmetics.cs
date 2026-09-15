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

namespace StarfallThrone.Content.Divine;

public abstract class DivineRelicBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Relic"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"DivineRelicTile"+Index).Type);Item.width=40;Item.height=48;Item.maxStack=99;Item.master=true;Item.rare=ItemRarityID.Master;}
}
public abstract class DivineTrophyBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Trophy"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"DivineTrophyTile"+Index).Type);Item.width=Item.height=40;Item.maxStack=99;Item.rare=ItemRarityID.Orange;}
}
public abstract class DivineMaskBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Mask"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=30;Item.vanity=true;Item.rare=ItemRarityID.Green;}
}
public abstract class DivineDisplayTile : ModTile
{
    public abstract int Index{get;}
    public virtual bool Trophy=>false;
    public virtual bool Monument=>false;
    public override string Texture=>DivineCatalog.Root+(Monument?"MonumentTile":(Trophy?"TrophyTile":"RelicTile")+Index);
    public int ItemType=>Monument?ModContent.ItemType<DivineMonument>():Trophy?DivineCatalog.Trophy(Index):DivineCatalog.Relic(Index);
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);TileObjectData.newTile.LavaDeath=false;
        if(Trophy){TileObjectData.newTile.AnchorBottom=default;TileObjectData.newTile.AnchorWall=true;}
        TileObjectData.addTile(Type);DustType=DustID.GoldFlame;HitSound=SoundID.Tink;
        AddMapEntry(DivineCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items."+(Monument?"DivineMonument":(Trophy?"DivineTrophy":"DivineRelic")+Index)+".DisplayName"));
    }
    public override IEnumerable<Item> GetItemDrops(int i,int j){yield return new Item(ItemType);}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=ItemType;}
}
public sealed class DivineMonument : ModItem
{
    public override string Texture=>DivineCatalog.Root+"Monument";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.TileType<DivineMonumentTile>());Item.width=Item.height=48;Item.maxStack=99;Item.rare=ItemRarityID.Red;}
    // Display-only recipe remains craftable after the three windows have closed.
    public override void AddRecipes()=>CreateRecipe().AddIngredient(DivineCatalog.Material(0),3).AddIngredient(DivineCatalog.Material(1),3).AddIngredient(DivineCatalog.Material(2),3).AddIngredient(ItemID.StoneBlock,30).AddTile(TileID.WorkBenches).Register();
}
public sealed class DivineMonumentTile : DivineDisplayTile{public override int Index=>2;public override bool Monument=>true;}
public sealed class DivineEpochTitle : ModItem
{
    public override string Texture=>DivineCatalog.Root+"Monument";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=32;Item.accessory=true;Item.vanity=true;Item.rare=ItemRarityID.Red;}
    public override void UpdateVanity(Player player)=>player.GetModPlayer<DivineVisualPlayer>().Title=true;
    public override void UpdateAccessory(Player player,bool hideVisual){if(!hideVisual)UpdateVanity(player);}
}
public abstract class DivineHaloBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Halo"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=32;Item.accessory=true;Item.vanity=true;Item.rare=ItemRarityID.LightPurple;}
    public override void UpdateVanity(Player player)=>player.GetModPlayer<DivineChallengePlayer>().HaloIndex=Index;
    public override void UpdateAccessory(Player player,bool hideVisual){if(!hideVisual)UpdateVanity(player);}
}
public sealed class DivineVisualPlayer : ModPlayer
{
    public bool Title;
    public override void ResetEffects()=>Title=false;
}
public sealed class DivineHaloLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition()=>new AfterParent(PlayerDrawLayers.Head);
    public override bool GetDefaultVisibility(PlayerDrawSet info)=>info.drawPlayer.GetModPlayer<DivineChallengePlayer>().HaloIndex>=0&&!info.drawPlayer.dead&&!info.drawPlayer.invis;
    protected override void Draw(ref PlayerDrawSet info)
    {
        Player player=info.drawPlayer;int index=player.GetModPlayer<DivineChallengePlayer>().HaloIndex;if(index<0||info.shadow>0)return;
        Texture2D texture=ModContent.Request<Texture2D>(DivineCatalog.Root+"Halo"+index).Value;
        Vector2 at=info.Position-Main.screenPosition+new Vector2(player.width/2,-9)+player.headPosition;
        info.DrawDataCache.Add(new DrawData(texture,at,null,Color.White,0,texture.Size()/2,new Vector2(1.25f,.65f),SpriteEffects.None,0));
    }
}
public sealed class DivineTitleDrawing : ModSystem
{
    public override void PostDrawInterface(SpriteBatch batch)
    {
        if(Main.gameMenu)return;
        foreach(Player player in Main.ActivePlayers)
        {
            if(player.dead||player.invis||!player.GetModPlayer<DivineVisualPlayer>().Title)continue;
            Vector2 at=Vector2.Transform(player.Top-Main.screenPosition-new Vector2(0,34),Main.GameViewMatrix.TransformationMatrix)/Main.UIScale;
            Utils.DrawBorderString(batch,Language.GetTextValue("Mods.StarfallThrone.Divine.Title"),at,Color.Gold,.65f,.5f);
        }
    }
}
public abstract class DivinePetItemBase : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"PetItem"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.ZephyrFish);Item.shoot=ModContent.Find<ModProjectile>(Mod.Name,"DivinePet"+Index).Type;
        Item.buffType=ModContent.Find<ModBuff>(Mod.Name,"DivinePetBuff"+Index).Type;
        Item.width=Item.height=32;Item.master=true;Item.rare=ItemRarityID.Master;
    }
    public override bool? UseItem(Player player){if(player.whoAmI==Main.myPlayer)player.AddBuff(Item.buffType,3600);return true;}
}
public abstract class DivinePetBuffBase : ModBuff
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"PetItem"+Index;
    public override void SetStaticDefaults(){Main.buffNoTimeDisplay[Type]=true;Main.vanityPet[Type]=true;}
    public override void Update(Player player,ref int buffIndex)
    {
        player.buffTime[buffIndex]=18000;int type=ModContent.Find<ModProjectile>(Mod.Name,"DivinePet"+Index).Type;
        if(player.whoAmI==Main.myPlayer&&player.ownedProjectileCounts[type]==0)
            Projectile.NewProjectile(player.GetSource_Buff(buffIndex),player.Center,Vector2.Zero,type,0,0,player.whoAmI);
    }
}
public abstract class DivinePetBase : ModProjectile
{
    public abstract int Index{get;}
    public override string Texture=>DivineCatalog.Root+"Pet"+Index;
    public override void SetStaticDefaults()=>Main.projPet[Type]=true;
    public override void SetDefaults(){Projectile.width=Projectile.height=32;Projectile.aiStyle=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.penetrate=-1;Projectile.netImportant=true;}
    public override bool? CanDamage()=>false;
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];int buff=ModContent.Find<ModBuff>(Mod.Name,"DivinePetBuff"+Index).Type;
        if(p.dead||!p.active){p.ClearBuff(buff);Projectile.Kill();return;}
        if(p.HasBuff(buff))Projectile.timeLeft=2;
        Vector2 target=p.Center+new Vector2(-p.direction*(48+Index*8),-42+(float)Math.Sin(Projectile.ai[0]++*.045)*8);
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
