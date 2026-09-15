using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Ecology;

public static class EcologyConversion
{
    public static bool Safe(int x,int y)
    {
        if(!WorldGen.InWorld(x,y,20))return false;
        Tile t=Main.tile[x,y];
        // Background walls are deliberately left untouched, but they must not
        // prevent the solid block in front of them from being converted. The
        // old check treated every underground block as protected because most
        // of those blocks have a background wall.
        if(t.LiquidAmount!=0||t.RedWire||t.BlueWire||t.GreenWire||t.YellowWire||t.HasActuator||t.IsActuated)return false;
        foreach(var p in global::StarfallThrone.Content.Mining.MiningWorld.ProtectedRegions)if(p.Contains(x,y))return false;
        // Protect the furniture/container tile itself. Do not scan neighboring
        // tiles: a torch, chest or platform next to a wall should not make the
        // surrounding ordinary terrain look non-convertible.
        if(t.HasTile&&Main.tileFrameImportant[t.TileType])return false;
        return true;
    }
    private static int VanillaKind(int type)=>type switch
    {
        TileID.Stone or TileID.Ebonstone or TileID.Crimstone or TileID.Pearlstone=>0,
        TileID.Dirt or TileID.Mud=>1,
        TileID.Grass or TileID.CorruptGrass or TileID.CrimsonGrass or TileID.HallowedGrass or TileID.JungleGrass or TileID.MushroomGrass=>2,
        TileID.Sand or TileID.Ebonsand or TileID.Crimsand or TileID.Pearlsand=>3,
        TileID.IceBlock or TileID.CorruptIce or TileID.HallowedIce or TileID.FleshIce or TileID.SnowBlock=>4,
        _=>-1
    };
    public static bool Convertible(Tile t,int biome,bool restore,out int result)
    {
        result=-1;if(!t.HasTile)return false;
        if(restore)
        {
            if(TileLoader.GetTile(t.TileType) is not EcologyTerrain own||own.Index!=biome)return false;
            result=EcologyTerrain.OriginalTiles[own.Kind];return true;
        }
        int kind=VanillaKind(t.TileType);
        if(kind<0)return false;result=EcologyCatalog.TerrainTile(biome,kind);return true;
    }
    public static int Convert(Player player,Point point,int biome,bool restore,int radius)
    {
        if(!EcologyWorld.Editable||!EcologyPlayer.Reach(player,point,960))return 0;
        var region=EcologyWorld.RegionAt(point);
        if(!EcologyWorld.CanEdit(player,region)||(!restore&&region.Biome!=biome)||!EcologyCatalog.VanillaUnlocked(region.Biome))return 0;
        int targetBiome=region.Biome;
        int changed=0;radius=Math.Clamp(radius,0,5);
        for(int x=point.X-radius;x<=point.X+radius;x++)for(int y=point.Y-radius;y<=point.Y+radius;y++)
        {
            if(!region.Bounds.Contains(x,y)||!Safe(x,y)||!Convertible(Main.tile[x,y],targetBiome,restore,out int tileType))continue;
            Main.tile[x,y].TileType=(ushort)tileType;FrameAndSync(x,y);changed++;
        }
        region.TileCount=Math.Max(0,region.TileCount+(restore?-changed:changed));return changed;
    }
    public static void FrameAndSync(int x,int y)
    {WorldGen.SquareTileFrame(x,y);if(Main.netMode==NetmodeID.Server)NetMessage.SendTileSquare(-1,x,y,1);}
}
public abstract class EcologyCatalyst : ModItem
{
    public abstract int Index{get;}
    public override string Texture=>EcologyCatalog.Root+"Catalyst"+Index;
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=25;
    public override void SetDefaults(){Item.width=Item.height=24;Item.maxStack=9999;Item.material=true;Item.value=Item.buyPrice(silver:15+Index*5);}
}
public abstract class EcologyPowder : ModItem
{
    public abstract int Index{get;}
    public virtual bool Solution=>false;
    public bool Restore=>Index<0;
    public override string Texture=>EcologyCatalog.Root+(Restore?(Solution?"RestoreSolution":"RestorePowder"):(Solution?"Solution":"Powder")+Index);
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=99;
    public override void SetDefaults()
    {
        Item.width=Item.height=24;Item.maxStack=9999;Item.useStyle=ItemUseStyleID.Swing;Item.useTime=Item.useAnimation=12;Item.autoReuse=true;
        Item.consumable=false;Item.UseSound=SoundID.Item1;Item.value=5;
        if(Solution){Item.ammo=AmmoID.Solution;Item.shoot=ModContent.ProjectileType<EcologySolutionJet>()-ProjectileID.PureSpray;Item.shootSpeed=0;Item.consumable=true;}
    }
    // Let UseItem report the precise reason for a rejected target. Returning
    // false here made the item appear completely broken whenever the pointer
    // was outside a bound region (and also depended on the current input zoom
    // state before the item-use tick).
    public override bool CanUseItem(Player player)=>!Solution;
    public override void PickAmmo(Item weapon,Player player,ref int type,ref float speed,ref StatModifier damage,ref float knockback)
    {if(Solution){type=ModContent.ProjectileType<EcologySolutionJet>();speed=7;}}
    public override bool? UseItem(Player player)
    {if(!Solution&&player.whoAmI==Main.myPlayer)EcologyPlayer.Request(4,EcologyPlayer.CursorTile(player));return true;}
    public override void AddRecipes()
    {
        var r=CreateRecipe(Solution?50:40).AddIngredient(ItemID.BottledWater,Solution?5:2).AddTile(EcologyCatalog.StationTile(Solution?2:0));
        if(Restore)r.AddIngredient(ItemID.Daybloom,1);else r.AddIngredient(EcologyCatalog.Item("EcologyCatalyst"+Index)).AddCondition(EcologyCatalog.BossCondition(Index));
        if(!Solution)r.AddIngredient(ItemID.StoneBlock,10);
        r.Register();
    }
}
public sealed class EcologyRestorePowder : EcologyPowder{public override int Index=>-1;}
public sealed class EcologyRestoreSolution : EcologyPowder{public override int Index=>-1;public override bool Solution=>true;}
public sealed class EcologySprayCan : ModItem
{
    public override string Texture=>EcologyCatalog.Root+"SprayCan";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=36;Item.useStyle=ItemUseStyleID.Shoot;Item.useTime=Item.useAnimation=10;Item.autoReuse=true;Item.useAmmo=AmmoID.Solution;Item.shoot=ProjectileID.PureSpray;Item.shootSpeed=7;Item.noMelee=true;Item.UseSound=SoundID.Item13;Item.rare=ItemRarityID.LightRed;}
    public override bool CanUseItem(Player player)
        =>player.ChooseAmmo(Item)?.ModItem is EcologyPowder {Solution:true};
    public override bool? CanChooseAmmo(Item ammo,Player player)=>ammo.ModItem is EcologyPowder {Solution:true};
    public override bool Shoot(Player player,Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,Vector2 position,Vector2 velocity,int type,int damage,float knockback)
    {
        // Do not rely solely on vanilla's solution projectile-offset path:
        // some tML input paths pass the offset through without preserving the
        // custom ModItem source. The dedicated applicator always emits the
        // authoritative jet and still lets tML consume exactly one ammo item.
        EcologyPowder solution=ItemLoader.GetItem(source.AmmoItemIdUsed) as EcologyPowder;
        if(solution is not {Solution:true})solution=player.ChooseAmmo(Item)?.ModItem as EcologyPowder;
        if(solution is {Solution:true})
            Projectile.NewProjectile(source,position,velocity,ModContent.ProjectileType<EcologySolutionJet>(),damage,knockback,player.whoAmI,solution.Index,solution.Restore?1f:0f);
        return false;
    }
    public override void AddRecipes()=>CreateRecipe().AddIngredient(ItemID.HellstoneBar,8).AddIngredient(ItemID.Glass,10).AddIngredient(ItemID.EmptyBucket).AddTile(EcologyCatalog.StationTile(2)).AddCondition(Condition.Hardmode).Register();
}
public sealed class EcologySolutionJet : ModProjectile
{
    public override string Texture=>EcologyCatalog.Root+"RestoreSolution";
    // Conversion is requested by the local owner and revalidated by the server against active jet,
    // bounds, held applicator and the jet's spawn-time solution identity.
    public int Biome=-2;public bool Restore;
    public override void SetDefaults(){Projectile.width=Projectile.height=8;Projectile.timeLeft=35;Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.extraUpdates=1;}
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
    {
        if(source is Terraria.DataStructures.EntitySource_ItemUse_WithAmmo ammo&&ItemLoader.GetItem(ammo.AmmoItemIdUsed) is EcologyPowder {Solution:true} p)
        {Biome=p.Index;Restore=p.Restore;Projectile.netUpdate=true;}
    }
    public override void SendExtraAI(System.IO.BinaryWriter writer){writer.Write((sbyte)Biome);writer.Write(Restore);}
    public override void ReceiveExtraAI(System.IO.BinaryReader reader){Biome=reader.ReadSByte();Restore=reader.ReadBoolean();}
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers)return;
        var owner=Main.player[Projectile.owner];
        // Carry the selected solution in the projectile itself. Some vanilla
        // applicators do not preserve a custom ammo id through every firing
        // path, especially when a client shoots a Clentaminator-style weapon.
        if(Biome==-2&&Projectile.ai[0]>=-1f&&Projectile.ai[0]<10f)
        {Biome=(int)Projectile.ai[0];Restore=Projectile.ai[1]!=0f;Projectile.netUpdate=true;}
        // Some vanilla firing paths do not preserve the custom ammo source on
        // every side of a multiplayer shot. Recover it once from the same
        // ammo selector used by the applicator instead of silently producing
        // an inert projectile.
        if(Biome==-2&&owner.ChooseAmmo(owner.HeldItem)?.ModItem is EcologyPowder {Solution:true} selected)
        {Biome=selected.Index;Restore=selected.Restore;Projectile.netUpdate=true;}
        if(Biome< -1||Biome>=10)return;
        if(!owner.active||owner.dead)return;
        // Native solution consumption is owned by the shooting player, as with vanilla Clentaminator.
        if(EcologyWorld.Authority)EcologyConversion.Convert(owner,Projectile.Center.ToTileCoordinates(),Biome,Restore,1);
        if(!Main.dedServ){var d=Dust.NewDustPerfect(Projectile.Center,DustID.TintableDustLighted,Projectile.velocity*.05f,120,Restore?Color.White:EcologyCatalog.Colors[Biome],.8f);d.noGravity=true;}
    }
}
public sealed class EcologyVanillaLoot : GlobalNPC
{
    public static int BiomeForNPC(int type)=>type switch
    {NPCID.EyeofCthulhu=>0,NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail or NPCID.BrainofCthulhu=>1,NPCID.QueenBee=>2,NPCID.SkeletronHead=>3,NPCID.WallofFlesh=>4,NPCID.TheDestroyer or NPCID.Retinazer or NPCID.Spazmatism or NPCID.SkeletronPrime=>5,NPCID.Plantera=>6,NPCID.Golem=>7,NPCID.DukeFishron or NPCID.HallowBoss=>8,NPCID.MoonLordCore=>9,_=>-1};
    public sealed class CatalystCondition : IItemDropRuleCondition
    {
        private readonly int index;private readonly bool bag;
        public CatalystCondition(int i,bool bag=false){index=i;this.bag=bag;}
        public bool CanDrop(DropAttemptInfo info)
        {
            if(index==8)return bag?EcologyCatalog.VanillaUnlocked(8):(info.npc.type==NPCID.DukeFishron&&NPC.downedEmpressOfLight)||(info.npc.type==NPCID.HallowBoss&&NPC.downedFishron);
            if(!bag&&index==1&&info.npc.type!=NPCID.BrainofCthulhu)return info.npc.boss;
            if(!bag&&info.npc.type==NPCID.Retinazer)return !NPC.AnyNPCs(NPCID.Spazmatism);
            if(!bag&&info.npc.type==NPCID.Spazmatism)return !NPC.AnyNPCs(NPCID.Retinazer);
            return true;
        }
        public bool CanShowItemDropInUI()=>true;
        public string GetConditionDescription()=>EcologyCatalog.Text(index==8?"PairGate":"CatalystDrop");
    }
    public static void AddRewards(IItemDropRule root,int index)
    {root.OnSuccess(ItemDropRule.Common(EcologyCatalog.Item("EcologyCatalyst"+index),1,6,10));root.OnSuccess(ItemDropRule.Common(EcologyCatalog.Item("EcologyPowder"+index),1,40,60));}
    public override void ModifyNPCLoot(NPC npc,NPCLoot loot)
    {
        int i=BiomeForNPC(npc.type);if(i<0)return;
        var normal=new LeadingConditionRule(new Conditions.NotExpert());var gate=new LeadingConditionRule(new CatalystCondition(i));AddRewards(gate,i);normal.OnSuccess(gate);loot.Add(normal);
    }
    public override void ModifyShop(NPCShop shop)
    {
        if(shop.NpcType!=NPCID.Dryad)return;
        for(int i=0;i<10;i++){shop.Add(EcologyCatalog.Item("EcologyCatalyst"+i),EcologyCatalog.BossCondition(i));shop.Add(EcologyCatalog.Item("EcologyPowder"+i),EcologyCatalog.BossCondition(i));}
    }
}
public sealed class EcologyVanillaBagLoot : GlobalItem
{
    public static int BiomeForBag(int type)=>type switch
    {ItemID.EyeOfCthulhuBossBag=>0,ItemID.EaterOfWorldsBossBag or ItemID.BrainOfCthulhuBossBag=>1,ItemID.QueenBeeBossBag=>2,ItemID.SkeletronBossBag=>3,ItemID.WallOfFleshBossBag=>4,ItemID.DestroyerBossBag or ItemID.TwinsBossBag or ItemID.SkeletronPrimeBossBag=>5,ItemID.PlanteraBossBag=>6,ItemID.GolemBossBag=>7,ItemID.FishronBossBag or ItemID.FairyQueenBossBag=>8,ItemID.MoonLordBossBag=>9,_=>-1};
    public override void ModifyItemLoot(Item item,ItemLoot loot)
    {int i=BiomeForBag(item.type);if(i<0)return;var gate=new LeadingConditionRule(new EcologyVanillaLoot.CatalystCondition(i,true));EcologyVanillaLoot.AddRewards(gate,i);loot.Add(gate);}
}
