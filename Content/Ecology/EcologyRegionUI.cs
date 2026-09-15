using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ecology;

public sealed class EcologyAnchor : ModItem
{
    public override string Texture=>EcologyCatalog.Root+"Anchor";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.DefaultToPlaceableTile(ModContent.TileType<EcologyAnchorTile>());Item.width=Item.height=32;Item.maxStack=99;Item.rare=ItemRarityID.Blue;}
    public override void AddRecipes()=>CreateRecipe().AddIngredient(ItemID.StoneBlock,30).AddIngredient(ItemID.Glass,10).AddRecipeGroup(RecipeGroupID.IronBar,3).AddTile(EcologyCatalog.StationTile(0)).AddCondition(EcologyCatalog.BossCondition(0)).Register();
}
public sealed class EcologyAnchorTile : ModTile
{
    public override string Texture=>EcologyCatalog.Root+"AnchorTile";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type]=Main.tileNoAttach[Type]=true;Main.tileLavaDeath[Type]=false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);TileObjectData.newTile.LavaDeath=false;TileObjectData.addTile(Type);
        AddMapEntry(Color.CadetBlue,Language.GetText("Mods.StarfallThrone.Items.EcologyAnchor.DisplayName"));
    }
    public static Point TopLeft(int i,int j){Tile t=Main.tile[i,j];return new(i-t.TileFrameX/18%2,j-t.TileFrameY/18%2);}
    public override IEnumerable<Item> GetItemDrops(int i,int j){yield return new Item(ModContent.ItemType<EcologyAnchor>());}
    public override bool RightClick(int i,int j){EcologyRegionUI.Open(TopLeft(i,j));return true;}
    public override void MouseOver(int i,int j){Main.LocalPlayer.noThrow=2;Main.LocalPlayer.cursorItemIconEnabled=true;Main.LocalPlayer.cursorItemIconID=ModContent.ItemType<EcologyAnchor>();}
}
public sealed class EcologyBoundaryScanner : ModItem
{
    public override string Texture=>EcologyCatalog.Root+"BoundaryScanner";
    public override void SetStaticDefaults()=>Item.ResearchUnlockCount=1;
    public override void SetDefaults(){Item.width=Item.height=32;Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=15;Item.rare=ItemRarityID.Blue;}
    public override bool? UseItem(Player p){if(p.whoAmI==Main.myPlayer)EcologyRegionUI.SelectCorner(EcologyPlayer.CursorTile(p));return true;}
    public override void AddRecipes()=>CreateRecipe().AddIngredient(ItemID.Glass,8).AddRecipeGroup(RecipeGroupID.IronBar,3).AddTile(EcologyCatalog.StationTile(0)).Register();
}
public sealed class EcologyRegionUI : ModSystem
{
    public static bool Visible,Ecology=true;
    public static int Biome;
    public static Point Anchor;
    public static Rectangle Bounds;
    private static Point? first;
    public static void Open(Point anchor)
    {
        Anchor=anchor;Visible=true;first=null;
        var existing=EcologyWorld.ByAnchor(anchor);
        Biome=existing?.Biome??0;Ecology=existing?.Ecology??true;
        Bounds=existing?.Bounds??new Rectangle(anchor.X-50,anchor.Y-30,100,60);
    }
    public static void Close(){Visible=false;first=null;}
    public override void OnWorldUnload()=>Close();
    public static void SelectCorner(Point p)
    {
        if(!Visible){Main.NewText(EcologyCatalog.Text("SelectAnchor"),Color.LightCyan);return;}
        if(first is not Point a){first=p;Main.NewText(EcologyCatalog.Text("FirstCorner"),Color.LightCyan);return;}
        Bounds=new(Math.Min(a.X,p.X),Math.Min(a.Y,p.Y),Math.Abs(a.X-p.X)+1,Math.Abs(a.Y-p.Y)+1);first=null;
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int i=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");layers.Insert(i<0?layers.Count:i,new LegacyGameInterfaceLayer("StarfallThrone: Ecology regions",Draw,InterfaceScaleType.UI));
    }
    private static void Label(string text,float x,float y,Color color,float scale=.78f)=>Utils.DrawBorderString(Main.spriteBatch,text,new Vector2(x,y),color,scale);
    private static bool Button(string text,int x,int y,int w=130)
    {
        Rectangle rect=new(x,y,w,28);bool hover=rect.Contains(StarfallUI.MouseUiPoint);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,rect,hover?new Color(60,97,112):new Color(29,52,63));Label(text,x+7,y+5,Color.White,.68f);
        if(hover){Main.LocalPlayer.mouseInterface=true;if(Main.mouseLeft&&Main.mouseLeftRelease){Main.mouseLeftRelease=false;return true;}}return false;
    }
    private static bool Draw()
    {
        if(Main.gameMenu||Main.LocalPlayer.dead)return true;
        var current=EcologyWorld.RegionAt(Main.LocalPlayer.Center.ToTileCoordinates());
        if(current!=null&&(Main.LocalPlayer.HeldItem.ModItem is EcologyPowder or EcologyBoundaryScanner))
            Label(EcologyCatalog.Text("RegionStatus",EcologyCatalog.Names[current.Biome],current.TileCount,current.Ecology?EcologyCatalog.Text("EcologyMode"):EcologyCatalog.Text("DisplayMode"),EcologyWorld.PendingOre[current.Biome]),20,(int)StarfallUI.ScreenSizeUi.Y-55,EcologyCatalog.Colors[current.Biome],.72f);
        if(!Visible)return true;
        if(!EcologyPlayer.Reach(Main.LocalPlayer,Anchor,960)||!EcologyWorld.AnchorValid(Anchor)){Close();return true;}
        int x=22,y=230;
        var panel=new Rectangle(x,y,430,320);
        if(panel.Contains(StarfallUI.MouseUiPoint))Main.LocalPlayer.mouseInterface=true;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,panel,new Color(12,23,34,238));
        Label(EcologyCatalog.Text("PanelTitle"),x+12,y+10,Color.LightCyan);
        Label(EcologyCatalog.Names[Biome],x+12,y+42,EcologyCatalog.Colors[Biome]);
        if(Button("◀",x+300,y+36,45))Biome=(Biome+9)%10;
        if(Button("▶",x+355,y+36,45))Biome=(Biome+1)%10;
        Label(EcologyCatalog.Text("Unlock"+Biome),x+12,y+76,EcologyCatalog.VanillaUnlocked(Biome)?Color.LightGreen:Color.IndianRed,.66f);
        if(Biome==8)Label(EcologyCatalog.Text("PairStatus",NPC.downedFishron?"✓":"×",NPC.downedEmpressOfLight?"✓":"×"),x+12,y+99,Color.LightGray,.64f);
        Label(EcologyCatalog.Text("Bounds",Bounds.Width,Bounds.Height),x+12,y+125,EcologyWorld.ValidBounds(Bounds,Anchor)?Color.White:Color.IndianRed,.68f);
        if(Button(EcologyCatalog.Text("Narrower"),x+12,y+151,70))Resize(-20,0);if(Button(EcologyCatalog.Text("Wider"),x+87,y+151,70))Resize(20,0);
        if(Button(EcologyCatalog.Text("Shorter"),x+167,y+151,70))Resize(0,-20);if(Button(EcologyCatalog.Text("Taller"),x+242,y+151,70))Resize(0,20);
        if(Button(EcologyCatalog.Text(Ecology?"EcologyMode":"DisplayMode"),x+12,y+186,150))Ecology=!Ecology;
        if(Button(EcologyCatalog.Text("Confirm"),x+172,y+186,110))EcologyPlayer.Request(1,Anchor);
        if(Button(EcologyCatalog.Text("Close"),x+292,y+186,100))Close();
        Label(EcologyCatalog.Text("PanelHint"),x+12,y+227,Color.Silver,.60f);
        if(Button(EcologyCatalog.Text("Unbind"),x+12,y+267,180))EcologyPlayer.Request(2,Anchor);
        return true;
    }
    private static void Resize(int w,int h)
    {int width=Math.Clamp(Bounds.Width+w,30,180),height=Math.Clamp(Bounds.Height+h,20,140);Bounds=new(Anchor.X-width/2,Anchor.Y-height/2,width,height);}
    public override void PostDrawTiles()
    {
        if(Main.dedServ||Main.gameMenu)return;
        if(!Visible&&Main.LocalPlayer.HeldItem.ModItem is not EcologyBoundaryScanner)return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,Main.GameViewMatrix.TransformationMatrix);
        foreach(var r in EcologyWorld.Regions)Outline(r.Bounds,EcologyCatalog.Colors[r.Biome]*.5f);
        if(Visible)Outline(first is Point p?new Rectangle(Math.Min(p.X,Player.tileTargetX),Math.Min(p.Y,Player.tileTargetY),Math.Abs(p.X-Player.tileTargetX)+1,Math.Abs(p.Y-Player.tileTargetY)+1):Bounds,Color.White);
        Main.spriteBatch.End();
    }
    private static void Outline(Rectangle tile,Color c)
    {
        int x=tile.X*16-(int)Main.screenPosition.X,y=tile.Y*16-(int)Main.screenPosition.Y,w=tile.Width*16,h=tile.Height*16;
        var px=TextureAssets.MagicPixel.Value;Main.spriteBatch.Draw(px,new Rectangle(x,y,w,2),c);Main.spriteBatch.Draw(px,new Rectangle(x,y+h,w,2),c);Main.spriteBatch.Draw(px,new Rectangle(x,y,2,h),c);Main.spriteBatch.Draw(px,new Rectangle(x+w,y,2,h),c);
    }
}
