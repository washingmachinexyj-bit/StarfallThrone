#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics;

namespace StarfallThrone.Content.Pantheon.Equipment;

public static partial class PantheonEquipmentValidation
{
    private static void RenderAttack(int id,bool detail,GraphicsDevice device,RenderTarget2D render,RenderTarget2D atlas,Projectile? representative=null)
    {
        Vector2 old=Main.screenPosition,oldZoom=Main.GameViewMatrix.Zoom;bool begun=false;
        var oldTargets=device.GetRenderTargets();var oldViewport=device.Viewport;var oldScissor=device.ScissorRectangle;
        var oldBlend=device.BlendState;var oldRaster=device.RasterizerState;var oldDepth=device.DepthStencilState;var oldSampler=device.SamplerStates[0];
        try
        {
            Projectile? focus=representative??(detail?Owned().FirstOrDefault(q=>q.ModProjectile is PantheonShot s&&s.Kind!=0):Owned().FirstOrDefault(q=>q.ModProjectile.CanDamage()!=false));
            focus??=Owned().FirstOrDefault();Check(focus!=null,"live renderer focus "+id);device.SetRenderTarget(render);device.Clear(Color.Transparent);device.Viewport=new Viewport(0,0,render.Width,render.Height);
            Vector2 center=focus!.Center;if(focus.ModProjectile is PantheonShot {Kind:3} beam)center=(center+beam.End)*.5f;Main.screenPosition=center-new Vector2(render.Width/2f,render.Height/2f);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;foreach(Projectile q in Owned()){Color c=Color.White;q.ModProjectile.PreDraw(ref c);}Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);
            Color[] pixels=new Color[render.Width*render.Height];render.GetData(pixels);Check(pixels.Count(c=>c.A>20)>12,"visible native weapon "+id);
            device.SetRenderTarget(atlas);Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;
            int x=id%6*300,y=id/6*360+(detail?180:0);Main.spriteBatch.Draw(render,new Rectangle(x,y+25,300,150),Color.White);Utils.DrawBorderString(Main.spriteBatch,$"{id} "+new Item(PantheonCatalog.Weapon(id)).Name,new Vector2(x+6,y+5),PantheonCatalog.Colors[PantheonArsenal.Boss(id)],.43f);Main.spriteBatch.End();begun=false;device.SetRenderTarget(null);
        }
        finally
        {
            if(begun)Main.spriteBatch.End();Main.screenPosition=old;Main.GameViewMatrix.Zoom=oldZoom;
            device.SetRenderTargets(oldTargets);device.Viewport=oldViewport;device.ScissorRectangle=oldScissor;device.BlendState=oldBlend;device.RasterizerState=oldRaster;device.DepthStencilState=oldDepth;device.SamplerStates[0]=oldSampler;
        }
    }
    private static void RenderArmor(GraphicsDevice device,RenderTarget2D render)
    {
        using var atlas=new RenderTarget2D(device,1200,1440,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        var originalView=Main.GameViewMatrix;
        Vector2 oldPosition=Main.screenPosition,oldZoom=Main.GameViewMatrix.Zoom;
        int oldWidth=Main.screenWidth,oldHeight=Main.screenHeight;
        var oldTargets=device.GetRenderTargets();var oldViewport=device.Viewport;var oldScissor=device.ScissorRectangle;
        var oldBlend=device.BlendState;var oldRaster=device.RasterizerState;var oldDepth=device.DepthStencilState;var oldSampler=device.SamplerStates[0];
        int verified=0;string lastRenderState="";
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(14,21,33));device.SetRenderTarget(null);Main.screenPosition=Vector2.Zero;
            Main.GameViewMatrix=new SpriteViewMatrix(device){Zoom=Vector2.One};Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,render.Width,render.Height));
            Main.screenWidth=render.Width;Main.screenHeight=render.Height;
            for(int tier=0;tier<6;tier++)for(int style=0;style<4;style++)
            {
                // Render each equipment slot against the SAME naked player pose; head pixels cannot
                // satisfy a torso/legs assertion. Keep the viewport and camera matched to the target.
                for(int pose=0;pose<3;pose++)
                {
                    Color[] baseline=RenderOne(-1,pose,0);
                    for(int slot=0;slot<3;slot++)
                    {
                        int id=tier*6+(slot==0?style:slot==1?4:5);Color[] pixels=RenderOne(id,pose,slot);int changed=0,lower=0;
                        for(int i=0;i<pixels.Length;i++)
                        {
                            Color a=pixels[i],b=baseline[i];int difference=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)+Math.Abs(a.A-b.A);
                            if(difference<=50||a.A<=20)continue;changed++;int y=i/render.Width;
                            if(slot==0||y>=55+(slot==1?8:23))lower++;
                        }
                        if(id==0&&pose==0||changed<=20||lower<=12)SaveDebug(id,pose,baseline,pixels,changed,lower);
                        Check(changed>20&&lower>12,"isolated worn slot visible id="+id+" pose="+pose+" changed="+changed+" lower="+lower);verified++;
                    }
                }
                device.SetRenderTarget(render);device.Clear(Color.Transparent);device.Viewport=new Viewport(0,0,render.Width,render.Height);device.ScissorRectangle=new Rectangle(0,0,render.Width,render.Height);Main.GameViewMatrix.SetViewportOverride(device.Viewport);
                for(int pose=0;pose<3;pose++)
                {
                    Player p=DisplayPlayer(pose,new Vector2(25+pose*110,55));p.head=new Item(PantheonCatalog.Armor(tier*6+style)).headSlot;p.body=new Item(PantheonCatalog.Armor(tier*6+4)).bodySlot;p.legs=new Item(PantheonCatalog.Armor(tier*6+5)).legSlot;
                    DrawNativePlayer(p);
                }
                device.SetRenderTarget(null);device.SetRenderTarget(atlas);bool begun=false;
                try{Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);begun=true;int x=style*300,y=tier*240;Main.spriteBatch.Draw(render,new Rectangle(x,y+25,300,200),Color.White);Utils.DrawBorderString(Main.spriteBatch,new Item(PantheonCatalog.Armor(tier*6+style)).Name,new Vector2(x+8,y+5),PantheonCatalog.Colors[PantheonEquipmentData.ArmorBoss[tier]],.52f);Main.spriteBatch.End();begun=false;}finally{if(begun)Main.spriteBatch.End();device.SetRenderTarget(null);}
            }
            string path=Output("pantheon-armor-runtime.png");using(var file=File.Create(path))atlas.SaveAsPng(file,atlas.Width,atlas.Height);
            Log($"PANTHEON_ARMOR_RENDER_PASS loadouts=24 poses=3 isolatedSlotComparisons={verified} headBodyLegs=true nativePlayerRenderer=true matchedCameraViewport=true path={path}");
        }
        finally
        {
            Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;Main.screenPosition=oldPosition;Main.GameViewMatrix=originalView;
            device.SetRenderTargets(oldTargets);device.Viewport=oldViewport;device.ScissorRectangle=oldScissor;device.BlendState=oldBlend;device.RasterizerState=oldRaster;device.DepthStencilState=oldDepth;device.SamplerStates[0]=oldSampler;
        }
        Color[] RenderOne(int id,int pose,int slot)
        {
            device.SetRenderTarget(render);device.Clear(Color.Transparent);device.Viewport=new Viewport(0,0,render.Width,render.Height);device.ScissorRectangle=new Rectangle(0,0,render.Width,render.Height);Main.GameViewMatrix.SetViewportOverride(device.Viewport);Player p=DisplayPlayer(pose,new Vector2(130,55));
            if(id>=0){Item item=new(PantheonCatalog.Armor(id));if(slot==0)p.head=item.headSlot;else if(slot==1)p.body=item.bodySlot;else p.legs=item.legSlot;}
            DrawNativePlayer(p);
            lastRenderState=$"id={id} pose={pose} viewport={device.Viewport} scissor={device.ScissorRectangle} screen={Main.screenWidth}x{Main.screenHeight} screenPosition={Main.screenPosition} zoom={Main.GameViewMatrix.Zoom} UIScale={Main.UIScale}\ntransform={Main.GameViewMatrix.TransformationMatrix}\nnormalized={Main.GameViewMatrix.NormalizedTransformationmatrix}\ncameraUnscaled={Main.Camera.UnscaledPosition}/{Main.Camera.UnscaledSize} cameraScaled={Main.Camera.ScaledPosition}/{Main.Camera.ScaledSize}\n";
            foreach(EffectParameter parameter in Main.pixelShader.Parameters)if(parameter.ParameterClass==EffectParameterClass.Matrix)lastRenderState+=$"pixelShader.{parameter.Name}={parameter.GetValueMatrix()}\n";
            device.SetRenderTarget(null);Color[] colors=new Color[render.Width*render.Height];render.GetData(colors);return colors;
        }
        void SaveDebug(int id,int pose,Color[] baseline,Color[] equipped,int changed,int lower)
        {
            string prefix=$"pantheon-armor-debug-{id}-{pose}";
            SavePixels(baseline,prefix+"-baseline.png");SavePixels(equipped,prefix+"-equipped.png");
            Color[] diff=new Color[baseline.Length];int minX=render.Width,minY=render.Height,maxX=-1,maxY=-1;
            for(int i=0;i<equipped.Length;i++){Color a=equipped[i],b=baseline[i];if(a.A>20){minX=Math.Min(minX,i%render.Width);maxX=Math.Max(maxX,i%render.Width);minY=Math.Min(minY,i/render.Width);maxY=Math.Max(maxY,i/render.Width);}if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)+Math.Abs(a.A-b.A)>50)diff[i]=Color.White;}
            SavePixels(diff,prefix+"-difference.png");File.WriteAllText(Output(prefix+"-state.txt"),lastRenderState+$"changed={changed} lower={lower} figureBounds={minX},{minY}..{maxX},{maxY}\n");
            Log($"PANTHEON_ARMOR_DEBUG id={id} pose={pose} changed={changed} lower={lower} bounds={minX},{minY}..{maxX},{maxY} files={prefix}");
        }
        void SavePixels(Color[] colors,string name){using var texture=new Texture2D(device,render.Width,render.Height);texture.SetData(colors);using var file=File.Create(Output(name));texture.SaveAsPng(file,texture.Width,texture.Height);}
        void DrawNativePlayer(Player p)
        {
            // DrawPlayerInternal writes raw SpriteDrawBuffer primitives and does not Begin a batch.
            // Match native DrawPlayerFull: Immediate Begin refreshes the vertex shader projection
            // for THIS render target; an old atlas projection otherwise shrinks/clips the figure.
            var camera=Main.Camera;bool begun=false;
            try
            {
                camera.SpriteBatch.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,camera.Sampler,DepthStencilState.None,camera.Rasterizer,null,camera.GameViewMatrix.TransformationMatrix);begun=true;
                Main.PlayerRenderer.DrawPlayer(camera,p,p.position,0,Vector2.Zero,0,1.5f);
                camera.SpriteBatch.End();begun=false;
            }
            finally{if(begun)camera.SpriteBatch.End();}
        }
    }
    private static Player DisplayPlayer(int pose,Vector2 at)
    {
        Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=pose==1?-1:1};p.ResetEffects();p.head=p.body=p.legs=-1;p.position=at;p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);return p;
    }
}
