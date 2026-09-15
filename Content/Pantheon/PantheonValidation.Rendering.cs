using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Pantheon;

public static partial class PantheonValidation
{
    static void SaveImage(RenderTarget2D image,string name)
    {
        Guard();string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));
        using FileStream stream=File.Create(path);image.SaveAsPng(stream,image.Width,image.Height);
    }
    static void Pets(Player p)
    {
        foreach(Projectile projectile in Main.projectile)projectile.active=false;
        for(int i=0;i<17;i++)
        {
            Item item=new(PantheonCatalog.Pet(i));item.ModItem.UseItem(p);int slot=p.FindBuffIndex(item.buffType);
            Check(slot>=0,"native pet use gives buff "+i);BuffLoader.GetBuff(item.buffType).Update(p,ref slot);
            Projectile pet=Main.projectile.Single(q=>q.active&&q.type==item.shoot);
            Check(pet.owner==p.whoAmI&&pet.ModProjectile.CanDamage()==false,"owned cosmetic pet "+i);
            p.ownedProjectileCounts[item.shoot]=1;BuffLoader.GetBuff(item.buffType).Update(p,ref slot);
            Check(Main.projectile.Count(q=>q.active&&q.type==item.shoot)==1,"buff does not duplicate pet "+i);
            for(int frame=0;frame<180;frame++)
            {pet.AI();pet.position+=pet.velocity;Check(float.IsFinite(pet.Center.X)&&float.IsFinite(pet.Center.Y)&&pet.velocity.Length()<=12.01f,"finite bounded pet motion "+i);}
            Check(pet.timeLeft==2,"buff maintains pet "+i);
            pet.Center=p.Center+new Vector2(2400,0);pet.AI();Check(pet.Distance(p.Center)<160,"distant pet returns "+i);
            p.ClearBuff(item.buffType);pet.AI();Check(pet.timeLeft<=2,"cleared buff expires pet "+i);
            pet.Kill();p.ownedProjectileCounts[item.shoot]=0;Check(!pet.active,"pet dismissal "+i);
            Projectile deadPet=new();deadPet.SetDefaults(item.shoot);deadPet.owner=p.whoAmI;deadPet.active=true;p.dead=true;deadPet.AI();p.dead=false;
            Check(!deadPet.active,"dead owner dismisses pet "+i);
        }
        Log("PANTHEON_PET_RUNTIME_PASS nativeBuffs=17 harmlessFollowers=17 noDuplicates=true finiteMotion=true ownerDeathDismissal=true");
    }
    static void PetRender(Player p)
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();Vector2 screen=Main.screenPosition;
        using var atlas=new RenderTarget2D(device,1020,180);bool begun=false;
        try
        {
            device.SetRenderTarget(atlas);device.Clear(Color.Transparent);Main.screenPosition=Vector2.Zero;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
            for(int i=0;i<17;i++)
            {
                Item item=new(PantheonCatalog.Pet(i));Projectile pet=new();pet.SetDefaults(item.shoot);pet.owner=p.whoAmI;pet.Center=new Vector2(30+i*60,80);pet.spriteDirection=i%2==0?1:-1;
                Color light=Color.White;Check(!pet.ModProjectile.PreDraw(ref light),"native pet PreDraw "+i);
            }
            Main.spriteBatch.End();begun=false;device.SetRenderTargets(targets);
            Color[] pixels=new Color[atlas.Width*atlas.Height];atlas.GetData(pixels);
            for(int i=0;i<17;i++)
            {
                int opaque=0;for(int y=40;y<120;y++)for(int x=i*60;x<(i+1)*60;x++)if(pixels[y*atlas.Width+x].A>20)opaque++;
                Check(opaque>30,"native pet pixels "+i);
            }
            SaveImage(atlas,"pantheon-pets-runtime.png");Log("PANTHEON_PET_RENDER_PASS nativePreDraw=17 visiblePixels=17");
        }
        finally{if(begun)Main.spriteBatch.End();device.SetRenderTargets(targets);Main.screenPosition=screen;}
    }
    static void WearRender()
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();Vector2 screen=Main.screenPosition,zoom=Main.GameViewMatrix.Zoom;
        int screenWidth=Main.screenWidth,screenHeight=Main.screenHeight;
        var viewport=device.Viewport;var scissor=device.ScissorRectangle;var blend=device.BlendState;
        var raster=device.RasterizerState;var depth=device.DepthStencilState;var sampler=device.SamplerStates[0];
        const int panelWidth=360,panelHeight=250,columns=4;
        var originalView=Main.GameViewMatrix;
        using var render=new RenderTarget2D(device,360,240);
        using var atlas=new RenderTarget2D(device,panelWidth*columns,panelHeight*5,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        bool begun=false;int verified=0;Rectangle[] portraits=new Rectangle[51];
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));device.SetRenderTarget(null);
            // PlayerRenderer uses both the camera dimensions and GameViewMatrix, not only the target's size.
            Main.screenWidth=render.Width;Main.screenHeight=render.Height;Main.screenPosition=Vector2.Zero;
            Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(device){Zoom=Vector2.One};
            Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,render.Width,render.Height));
            for(int i=0;i<17;i++)
            {
                Texture2D texture=ModContent.Request<Texture2D>(PantheonCatalog.Root+"Mask"+i+"_Head").Value;
                Check(texture.Width==40&&texture.Height==1120,"native mask head dimensions "+i);
                Color[] pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
                for(int frame=0;frame<20;frame++)Check(pixels.Skip(frame*40*56).Take(40*56).Count(c=>c.A>20)>6,"mask frame is visible "+i+"/"+frame);
                for(int pose=0;pose<3;pose++)
                {
                    Color[] baseline=RenderOne(-1,pose),worn=RenderOne(i,pose);
                    int changed=0,left=render.Width,top=render.Height,right=-1,bottom=-1;
                    for(int pixel=0;pixel<worn.Length;pixel++)
                    {
                        Color a=worn[pixel],b=baseline[pixel];if(a.A<=20)continue;
                        int x=pixel%render.Width,y=pixel/render.Width;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                        if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)+Math.Abs(a.A-b.A)>50)changed++;
                    }
                    Check(changed>20,"native mask differs from same naked pose "+i+"/"+pose+" pixels="+changed);
                    // Assert the complete native figure has margin on every side BEFORE cropping/compositing.
                    if(left<8||top<8||right>=render.Width-8||bottom>=render.Height-8)
                        SaveImage(render,"pantheon-mask-debug-"+i+"-"+pose+".png");
                    Check(left>=8&&top>=8&&right<render.Width-8&&bottom<render.Height-8&&right>=left&&bottom>=top,"native mask figure not clipped "+i+"/"+pose+" bounds="+left+","+top+","+right+","+bottom);
                    Rectangle source=new(left-4,top-4,right-left+9,bottom-top+9);
                    Check(source.Width<=112&&source.Height<=174,"mask figure fits its own pose cell "+i+"/"+pose+" bounds="+source);
                    int panelX=i%columns*panelWidth,panelY=i/columns*panelHeight;
                    Rectangle destination=new(panelX+pose*120+(120-source.Width)/2,panelY+38+(178-source.Height)/2,source.Width,source.Height);
                    Check(destination.Left>=panelX+pose*120&&destination.Right<=panelX+(pose+1)*120&&destination.Top>=panelY+38&&destination.Bottom<=panelY+216,"complete pose inside its panel "+i+"/"+pose);
                    portraits[i*3+pose]=destination;
                    device.SetRenderTarget(atlas);device.Viewport=new Viewport(0,0,atlas.Width,atlas.Height);device.ScissorRectangle=new Rectangle(0,0,atlas.Width,atlas.Height);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
                    // Copy the verified native pixels 1:1; no second player-camera transform and no downsampling.
                    Main.spriteBatch.Draw(render,destination,source,Color.White);
                    if(pose==0)Utils.DrawBorderString(Main.spriteBatch,(i+1).ToString("00")+"  "+new Item(PantheonCatalog.Mask(i)).Name,new Vector2(panelX+10,panelY+10),PantheonCatalog.Colors[i],.44f);
                    Utils.DrawBorderString(Main.spriteBatch,new[]{"站立","举臂","行走"}[pose],new Vector2(panelX+pose*120+60,panelY+225),Color.LightGray,.48f,.5f);
                    Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);verified++;
                }
            }
            Color[] atlasPixels=new Color[atlas.Width*atlas.Height];atlas.GetData(atlasPixels);
            Color background=new(19,26,36);
            for(int n=0;n<portraits.Length;n++)
            {
                Rectangle at=portraits[n];int visible=0;
                for(int y=at.Top;y<at.Bottom;y++)for(int x=at.Left;x<at.Right;x++)
                {Color c=atlasPixels[y*atlas.Width+x];if(Math.Abs(c.R-background.R)+Math.Abs(c.G-background.G)+Math.Abs(c.B-background.B)>50)visible++;}
                Check(visible>100,"final atlas contains every native pose "+n);
            }
            SaveImage(atlas,"pantheon-masks-runtime.png");Log("PANTHEON_WEAR_RENDER_PASS nativePlayerRenderer=true masks=17 poses=51 fullFrames=340 isolatedMaskComparisons="+verified+" matchedCameraViewport=true unclippedFigures=51 atlasCellsVerified=51");
        }
        finally
        {
            if(begun)Main.spriteBatch.End();Main.screenWidth=screenWidth;Main.screenHeight=screenHeight;Main.screenPosition=screen;Main.GameViewMatrix=originalView;Main.GameViewMatrix.Zoom=zoom;
            device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;device.BlendState=blend;device.RasterizerState=raster;device.DepthStencilState=depth;device.SamplerStates[0]=sampler;
        }
        Color[] RenderOne(int mask,int pose)
        {
            device.SetRenderTarget(render);device.Clear(Color.Transparent);device.Viewport=new Viewport(0,0,render.Width,render.Height);device.ScissorRectangle=new Rectangle(0,0,render.Width,render.Height);
            Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=pose==1?-1:1};p.ResetEffects();p.head=p.body=p.legs=-1;
            if(mask>=0)p.head=new Item(PantheonCatalog.Mask(mask)).headSlot;
            p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);p.position=new Vector2(130,55);
            // DrawPlayer is the inner native routine. Its normal DrawPlayerFull caller
            // initializes the immediate batch projection before it uses SpriteDrawBuffer.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,Main.GameViewMatrix.TransformationMatrix);begun=true;
            Main.PlayerRenderer.DrawPlayer(Main.Camera,p,p.position,0,Vector2.Zero,0,1.8f);
            Main.spriteBatch.End();begun=false;
            device.SetRenderTarget(null);Color[] colors=new Color[render.Width*render.Height];render.GetData(colors);return colors;
        }
    }
    readonly record struct Cell(TileTypeData Type,WallTypeData Wall,TileWallWireStateData State,LiquidData Liquid,TileWallBrightnessInvisibilityData Light)
    {
        public Cell(Tile t):this(t.Get<TileTypeData>(),t.Get<WallTypeData>(),t.Get<TileWallWireStateData>(),t.Get<LiquidData>(),t.Get<TileWallBrightnessInvisibilityData>()){}
        public void Restore(Tile t){t.Get<TileTypeData>()=Type;t.Get<WallTypeData>()=Wall;t.Get<TileWallWireStateData>()=State;t.Get<LiquidData>()=Liquid;t.Get<TileWallBrightnessInvisibilityData>()=Light;}
    }
    static void FurnitureRender()
    {
        const int left=1230,top=100,pad=8,cellWidth=24,cellHeight=25;
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();bool noActions=WorldGen.noTileActions,gen=WorldGen.gen,begun=false;
        var cells=new Cell[cellWidth*cellHeight];for(int x=0;x<cellWidth;x++)for(int y=0;y<cellHeight;y++)cells[x*cellHeight+y]=new Cell(Main.tile[left-pad+x,top-pad+y]);
        using var atlas=new RenderTarget2D(device,1000,480);
        try
        {
            WorldGen.noTileActions=WorldGen.gen=false;device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));
            for(int n=0;n<40;n++)
            {
                int itemType=n<17?PantheonCatalog.Relic(n):n<34?PantheonCatalog.Trophy(n-17):PantheonCatalog.Station(n-34);
                Item item=new(itemType);int type=item.createTile;
                for(int x=left-2;x<left+7;x++)for(int y=top-2;y<top+8;y++)
                {Tile t=Main.tile[x,y];t.ClearEverything();t.WallType=WallID.Stone;if(y>=top+4){t.HasTile=true;t.TileType=TileID.Dirt;}}
                ClearItems();TileObjectData data=TileObjectData.GetTileData(type,0);int placedY=top+4-data.Height;
                WorldGen.PlaceObject(left+data.Origin.X,placedY+data.Origin.Y,type);
                Check(Main.tile[left,placedY].HasTile&&Main.tile[left,placedY].TileType==type,"native furniture placement "+n);
                Texture2D texture=ModContent.Request<Texture2D>(TileLoader.GetTile(type).Texture).Value;
                Check(texture.Width==(n<34?54:90)&&texture.Height==(n<34?54:72),"furniture atlas size "+n);
                Color[] pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);int drawnOpaque=0;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
                for(int x=0;x<data.Width;x++)for(int y=0;y<data.Height;y++)
                {
                    Tile t=Main.tile[left+x,placedY+y];Check(t.HasTile&&t.TileType==type,"native furniture all tiles "+n);
                    Rectangle src=new(t.TileFrameX,t.TileFrameY,16,data.CoordinateHeights[y]);Check(src.Right<=texture.Width&&src.Bottom<=texture.Height,"native furniture frame bounds "+n);
                    for(int py=src.Top;py<src.Bottom;py++)for(int px=src.Left;px<src.Right;px++)if(pixels[py*texture.Width+px].A>20)drawnOpaque++;
                    Main.spriteBatch.Draw(texture,new Vector2(n%10*100+10+x*16,n/10*120+25+y*16),src,Color.White);
                }
                Main.spriteBatch.End();begun=false;Check(drawnOpaque>100,"placed furniture visible "+n);
                WorldGen.KillTile(left,placedY);WorldGen.SquareTileFrame(left,placedY,true);Check(Amount(itemType)==1,"native exactly one furniture drop "+n);
            }
            device.SetRenderTargets(targets);SaveImage(atlas,"pantheon-furniture-runtime.png");
            Log("PANTHEON_FURNITURE_RENDER_PASS nativePlace=40 nativeBreak=40 singleDrops=40 relics=17 trophies=17 stations=6 frameBounds=true");
        }
        finally
        {
            if(begun)Main.spriteBatch.End();device.SetRenderTargets(targets);WorldGen.noTileActions=noActions;WorldGen.gen=gen;
            for(int x=0;x<cellWidth;x++)for(int y=0;y<cellHeight;y++)cells[x*cellHeight+y].Restore(Main.tile[left-pad+x,top-pad+y]);
        }
    }
}
