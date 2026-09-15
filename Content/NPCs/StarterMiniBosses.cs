using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace StarfallThrone.Content.NPCs;

public abstract partial class MiniBossNPC
{
    private void Dew(Player player)
    {
        switch (State)
        {
            case 0:
                Walk(player.Center.X, 0.45f);
                if (Timer >= 90) { Lock(player.Center); Change(1); }
                break;
            case 1:
                Rest();
                if (Timer >= 60)
                {
                    NPC.direction = Facing(Locked.X);
                    Shot(6, NPC.Center + new Vector2(NPC.direction * 14, -10),
                        new Vector2(NPC.direction * (Angry ? 2.1f : 1.8f), -2.7f));
                    Change(2);
                }
                break;
            case 2:
                // Wait for the droplet's entire lifetime: never combine it with a contact attack.
                Rest(); if (Timer >= 110) { Lock(player.Center); Change(3); } break;
            case 3:
                Rest();
                if (Timer >= 48) { NPC.velocity.X = Facing(Locked.X) * 2f; Change(4); }
                break;
            case 4:
                if (NPC.collideX || Timer >= 24) { NPC.velocity.X = 0; Change(5); }
                break;
            case 5: Rest(); if (Timer >= 78) Change(0); break;
        }
    }

    private void PuffSmallHop(Player player, int nextState)
    {
        // Direction is fixed at takeoff; no midair tracking.
        NPC.direction = Facing(player.Center.X);
        NPC.velocity = new Vector2(NPC.direction * 1.5f, -4.2f);
        Change(nextState);
    }

    private void Puff(Player player)
    {
        switch (State)
        {
            case 0:
                Rest(); if (Timer >= (Angry ? 14 : 24) && NPC.collideY) PuffSmallHop(player, 1); break;
            case 1:
                if (Timer > 1 && NPC.collideY || Timer >= 100) { NPC.velocity.X = 0; Change(2); }
                break;
            case 2:
                Rest(); if (Timer >= (Angry ? 14 : 24) && NPC.collideY) PuffSmallHop(player, 3); break;
            case 3:
                if (Timer > 1 && NPC.collideY || Timer >= 100)
                {
                    NPC.velocity.X = 0;
                    float distance = Math.Clamp(player.Center.X - NPC.Center.X, -60f, 60f);
                    Vector2 landing = NPC.Bottom + new Vector2(distance, 0);
                    if (FindGround(landing, NPC.width, NPC.height, out Vector2 ground)) landing = ground;
                    Lock(landing); Change(4);
                }
                break;
            case 4:
                Rest();
                if (Timer >= 54)
                {
                    NPC.velocity = new Vector2(Math.Clamp((Locked.X - NPC.Center.X) / 55f, -1.1f, 1.1f), -4.5f);
                    Change(5);
                }
                break;
            case 5:
                if (NPC.velocity.Y > 0.9f) NPC.velocity.Y = 0.9f;
                if (Math.Abs(Locked.X - NPC.Center.X) < 2 || (Locked.X - NPC.Center.X) * NPC.velocity.X < 0) NPC.velocity.X = 0;
                if (Timer > 1 && NPC.collideY || Timer >= 180) { NPC.velocity.X = 0; Change(6); }
                break;
            case 6:
                Rest();
                if (Timer == 1)
                {
                    Shot(7, NPC.Bottom + new Vector2(-12, -8), new Vector2(-1.7f, -0.35f));
                    Shot(7, NPC.Bottom + new Vector2(12, -8), new Vector2(1.7f, -0.35f));
                }
                if (Timer >= 72) Change(0);
                break;
        }
        NPC.rotation = State is 1 or 3 or 5 ? MathHelper.Clamp(NPC.velocity.X * 0.06f, -0.12f, 0.12f) : 0;
    }

    private void DrawStarterTelegraph(SpriteBatch batch, Vector2 screenPos)
    {
        if (Dormant) return;
        if (Index == 7 && State is 4 or 5)
        {
            Vector2 at = Locked - screenPos;
            Color color = new Color(244, 230, 170) * (State == 4 ? 0.5f + 0.4f * Timer / 54f : 0.9f);
            batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X - 18, (int)at.Y - 3, 36, 3), color);
            batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X - 2, (int)at.Y - 10, 4, 5), color);
        }
        if (Index == 6 && State == 1)
        {
            Texture2D drop = ModContent.Request<Texture2D>("StarfallThrone/Content/Assets/Minis/DewDrop").Value;
            Vector2 at = NPC.Top - screenPos + new Vector2(Facing(Locked.X) * 12, -7);
            Main.EntitySpriteDraw(drop, at, null, Color.White * (0.35f + Timer / 100f), 0, drop.Size() / 2f, 0.3f + Timer / 200f, SpriteEffects.None);
        }
    }
}

[AutoloadBossHead] public sealed class DewShellNPC : MiniBossNPC { public override int Index => 6; }
[AutoloadBossHead] public sealed class DandelionPuffNPC : MiniBossNPC { public override int Index => 7; }
