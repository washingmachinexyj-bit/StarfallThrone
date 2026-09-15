#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.ObjectData;
using Terraria.GameContent;
namespace StarfallThrone.Content.Oaths.Support;
public abstract class SupremeRelicBase:ModItem
{
 public abstract int Index{get;}
 public override string Texture=>OathSupport.Root+"SupremeRelic"+Index;
 public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
 public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"SupremeRelicTile"+Index).Type);Item.width=40;Item.height=48;Item.maxStack=99;Item.master=true;Item.rare=ItemRarityID.Master;}
}
public abstract class SupremeTrophyBase:ModItem
{
 public abstract int Index{get;}
 public override string Texture=>OathSupport.Root+"SupremeTrophy"+Index;
 public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
 public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.Find<ModTile>(Mod.Name,"SupremeTrophyTile"+Index).Type);Item.width=Item.height=40;Item.maxStack=99;Item.rare=ItemRarityID.Orange;}
}
public abstract class SupremeMaskBase:ModItem
{
 public abstract int Index{get;}
 public override string Texture=>OathSupport.Root+"SupremeMask"+Index;
 public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
 public override void SetDefaults(){Item.width=Item.height=30;Item.vanity=true;Item.rare=ItemRarityID.Green;}
}
public abstract class SupremeDisplayTile:ModTile
{
 public abstract int Index{get;} public virtual bool Trophy=>false;
 public override string Texture=>OathSupport.Root+(Trophy?"SupremeTrophyTile":"SupremeRelicTile")+Index;
 public int ItemType=>OathSupport.Item((Trophy?"SupremeTrophy":"SupremeRelic")+Index);
 public override void SetStaticDefaults()
 {
  Main.tileFrameImportant[Type]=true;Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
  TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);TileObjectData.newTile.LavaDeath=false;
  if(Trophy){TileObjectData.newTile.AnchorBottom=default;TileObjectData.newTile.AnchorWall=true;}
  TileObjectData.addTile(Type);DustType=DustID.GoldFlame;HitSound=SoundID.Tink;
  AddMapEntry(OathCatalog.Colors[Index],Language.GetText("Mods.StarfallThrone.Items."+(Trophy?"SupremeTrophy":"SupremeRelic")+Index+".DisplayName"));
  RegisterItemDrop(ItemType);
 }
 public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=ItemType;}
}
public abstract class SupremePetItemBase:ModItem
{
 public abstract int Index{get;}public override string Texture=>OathSupport.Root+"SupremePetItem"+Index;
 public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
 public override void SetDefaults(){Item.CloneDefaults(ItemID.ZephyrFish);Item.shoot=ModContent.Find<ModProjectile>(Mod.Name,"SupremePet"+Index).Type;Item.buffType=ModContent.Find<ModBuff>(Mod.Name,"SupremePetBuff"+Index).Type;Item.width=Item.height=32;Item.master=true;Item.rare=ItemRarityID.Master;}
 public override bool? UseItem(Player p){if(p.whoAmI==Main.myPlayer)p.AddBuff(Item.buffType,3600);return true;}
}
public abstract class SupremePetBuffBase:ModBuff
{
 public abstract int Index{get;}public override string Texture=>OathSupport.Root+"SupremePetItem"+Index;
 public override void SetStaticDefaults(){Main.buffNoTimeDisplay[Type]=true;Main.vanityPet[Type]=true;}
 public override void Update(Player p,ref int buffIndex){p.buffTime[buffIndex]=18000;int type=ModContent.Find<ModProjectile>(Mod.Name,"SupremePet"+Index).Type;if(p.whoAmI==Main.myPlayer&&p.ownedProjectileCounts[type]==0)Projectile.NewProjectile(p.GetSource_Buff(buffIndex),p.Center,Vector2.Zero,type,0,0,p.whoAmI);}
}
public abstract class SupremePetBase:ModProjectile
{
 public abstract int Index{get;}public override string Texture=>OathSupport.Root+"SupremePet"+Index;
 public override void SetStaticDefaults()=>Main.projPet[Type]=true;
 public override void SetDefaults(){Projectile.width=Projectile.height=24;Projectile.aiStyle=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.penetrate=-1;Projectile.netImportant=true;}
 public override bool? CanDamage()=>false;
 public override void AI(){
  if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
  Player p=Main.player[Projectile.owner];int buff=ModContent.Find<ModBuff>(Mod.Name,"SupremePetBuff"+Index).Type;
  if(!p.active||p.dead){p.ClearBuff(buff);Projectile.Kill();return;}if(p.HasBuff(buff))Projectile.timeLeft=2;
  Vector2 target=p.Center+new Vector2(-p.direction*52,-48+(float)Math.Sin(Projectile.ai[0]++*.045f)*6);
  if(Vector2.DistanceSquared(target,Projectile.Center)>1600*1600){Projectile.Center=target;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
  Projectile.velocity=Vector2.Lerp(Projectile.velocity,(target-Projectile.Center)*.07f,.16f);
  if(Projectile.velocity.Length()>12)Projectile.velocity=Projectile.velocity.SafeNormalize(Vector2.Zero)*12;
  Projectile.rotation=Projectile.velocity.X*.02f;Projectile.spriteDirection=p.direction;
 }
 public override bool PreDraw(ref Color lightColor){var tex=TextureAssets.Projectile[Type].Value;Main.EntitySpriteDraw(tex,Projectile.Center-Main.screenPosition,null,Color.Lerp(lightColor,Color.White,.45f),Projectile.rotation,tex.Size()/2,1,Projectile.spriteDirection<0?SpriteEffects.FlipHorizontally:SpriteEffects.None);return false;}
}
public sealed class SupremeMaterial0:SupremeMaterialBase{public override int Index=>0;}
public sealed class SupremeSummon0:SupremeSummonBase{public override int Index=>0;}
public sealed class SupremeBag0:SupremeBagBase{public override int Index=>0;}
public sealed class SupremeRelic0:SupremeRelicBase{public override int Index=>0;}
public sealed class SupremeTrophy0:SupremeTrophyBase{public override int Index=>0;}
[AutoloadEquip(EquipType.Head)]public sealed class SupremeMask0:SupremeMaskBase{public override int Index=>0;}
public sealed class SupremePetItem0:SupremePetItemBase{public override int Index=>0;}
public sealed class SupremePetBuff0:SupremePetBuffBase{public override int Index=>0;}
public sealed class SupremePet0:SupremePetBase{public override int Index=>0;}
public sealed class SupremeRelicTile0:SupremeDisplayTile{public override int Index=>0;}
public sealed class SupremeTrophyTile0:SupremeDisplayTile{public override int Index=>0;public override bool Trophy=>true;}
public sealed class SupremeMaterial1:SupremeMaterialBase{public override int Index=>1;}
public sealed class SupremeSummon1:SupremeSummonBase{public override int Index=>1;}
public sealed class SupremeBag1:SupremeBagBase{public override int Index=>1;}
public sealed class SupremeRelic1:SupremeRelicBase{public override int Index=>1;}
public sealed class SupremeTrophy1:SupremeTrophyBase{public override int Index=>1;}
[AutoloadEquip(EquipType.Head)]public sealed class SupremeMask1:SupremeMaskBase{public override int Index=>1;}
public sealed class SupremePetItem1:SupremePetItemBase{public override int Index=>1;}
public sealed class SupremePetBuff1:SupremePetBuffBase{public override int Index=>1;}
public sealed class SupremePet1:SupremePetBase{public override int Index=>1;}
public sealed class SupremeRelicTile1:SupremeDisplayTile{public override int Index=>1;}
public sealed class SupremeTrophyTile1:SupremeDisplayTile{public override int Index=>1;public override bool Trophy=>true;}
public sealed class SupremeMaterial2:SupremeMaterialBase{public override int Index=>2;}
public sealed class SupremeSummon2:SupremeSummonBase{public override int Index=>2;}
public sealed class SupremeBag2:SupremeBagBase{public override int Index=>2;}
public sealed class SupremeRelic2:SupremeRelicBase{public override int Index=>2;}
public sealed class SupremeTrophy2:SupremeTrophyBase{public override int Index=>2;}
[AutoloadEquip(EquipType.Head)]public sealed class SupremeMask2:SupremeMaskBase{public override int Index=>2;}
public sealed class SupremePetItem2:SupremePetItemBase{public override int Index=>2;}
public sealed class SupremePetBuff2:SupremePetBuffBase{public override int Index=>2;}
public sealed class SupremePet2:SupremePetBase{public override int Index=>2;}
public sealed class SupremeRelicTile2:SupremeDisplayTile{public override int Index=>2;}
public sealed class SupremeTrophyTile2:SupremeDisplayTile{public override int Index=>2;public override bool Trophy=>true;}

