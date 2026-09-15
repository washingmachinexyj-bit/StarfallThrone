using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.NPCs;

// Only the 27 full bosses inherit this director. Miniboss AI and equipment are untouched.
public abstract partial class ReforgedBossNPC : ModNPC
{
    public abstract int EncounterId { get; }
    public int CombatState => (int)NPC.ai[0]; // approach, windup, attack, recovery, phase break
    public int CombatTimer => (int)NPC.ai[1];
    public int CombatPhase => (int)NPC.ai[3];
    public int Move => (int)NPC.ai[2] % (CombatPhase == 0 ? 2 : 3);
    protected Vector2 Aim, Origin;
    protected int Side = 1;
    protected bool Early => EncounterId < 10;
    protected bool Grounded => CombatProfiles.Grounded(EncounterId);
    protected float Speed => CombatProfiles.Speed(EncounterId, CombatPhase);
    protected int T => CombatTimer;
    public bool ContactWindow => CombatState == 2 && IsContactMove() && NPC.velocity.LengthSquared() > 2.25f;

    protected void ConfigureBody()
    {
        NPC.noGravity = !Grounded;
        NPC.noTileCollide = EncounterId is 12 or 20;
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ContactWindow;
    public override bool? CanFallThroughPlatforms() => NPC.HasValidTarget && Main.player[NPC.target].Top.Y > NPC.Bottom.Y + 80;
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (EncounterId == 1 && Move == 2 && CombatState == 2) modifiers.FinalDamage *= .65f;
        if (CombatState == 3) modifiers.FinalDamage *= 1.12f;
    }
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Aim.X); writer.Write(Aim.Y); writer.Write(Origin.X); writer.Write(Origin.Y); writer.Write((sbyte)Side);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Aim = new(reader.ReadSingle(), reader.ReadSingle()); Origin = new(reader.ReadSingle(), reader.ReadSingle()); Side = reader.ReadSByte() < 0 ? -1 : 1;
    }
    private void Change(int state) { NPC.ai[0] = state; NPC.ai[1] = 0; NPC.netUpdate = true; }
    protected void ClearAttacks()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is CombatHazard && (int)p.ai[0] == NPC.whoAmI + 1) p.Kill();
    }
    public override void AI()
    {
        if (!NPC.HasValidTarget) NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 4200)
        {
            ClearAttacks(); NPC.velocity.Y -= .12f; NPC.timeLeft = Math.Min(NPC.timeLeft, 60); return;
        }
        Player p = Main.player[NPC.target];
        NPC.timeLeft = 600; ConfigureBody(); NPC.dontTakeDamage = false;
        int phase = CombatProfiles.Phase(NPC, EncounterId);
        if (phase > CombatPhase)
        {
            NPC.ai[3] = phase; ClearAttacks(); Change(4);
            if (!Main.dedServ) CombatText.NewText(NPC.Hitbox, CombatProfiles.Color(EncounterId),
                Language.GetTextValue("Mods.StarfallThrone.Combat.Phase" + phase), true);
        }
        NPC.ai[1]++;
        switch (CombatState)
        {
            case 0:
                Approach(p);
                if (T >= (NPC.ai[2] == 0 ? 90 : 48))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    { Aim = p.Center; Origin = NPC.Center; Side = Aim.X >= NPC.Center.X ? 1 : -1; }
                    Change(1);
                }
                break;
            case 1:
                Rest();
                if (T >= CombatProfiles.Windup(EncounterId)) { Origin = NPC.Center; Change(2); }
                break;
            case 2:
                TickSignature(p);
                if (T >= CombatProfiles.ActiveTime(EncounterId, Move)) Change(3);
                break;
            case 3:
                Rest();
                if (T >= CombatProfiles.Recovery(EncounterId)) { ClearAttacks(); NPC.ai[2]++; Change(0); }
                break;
            case 4: Rest(); if (T >= 75) Change(0); break;
        }
        NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
        if (!(EncounterId == 15 && Move == 2 && CombatState == 2))
            NPC.rotation = Grounded ? 0 : MathHelper.Clamp(NPC.velocity.X * .014f, -.25f, .25f);
    }
    protected void Rest() { NPC.velocity.X *= .87f; if (!Grounded) NPC.velocity.Y *= .87f; }
    protected void Fly(Vector2 point, float speed)
    {
        Vector2 delta = point - NPC.Center;
        NPC.velocity = Vector2.Lerp(NPC.velocity, delta.SafeNormalize(Vector2.Zero) * Math.Min(speed, delta.Length() / 12f), .10f);
    }
    private void Approach(Player p)
    {
        int side = NPC.Center.X <= p.Center.X ? -1 : 1;
        if (Grounded)
        {
            float x = p.Center.X + side * (Early ? 190 : 360);
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, MathHelper.Clamp((x - NPC.Center.X) / 50f, -Speed * .5f, Speed * .5f), .08f);
            if (NPC.collideX && NPC.collideY) NPC.velocity.Y = -7;
            return;
        }
        Vector2 offset = EncounterId switch
        {
            7 => new(side * 220, -180), 8 => new(side * 260, -260), 9 => new(side * 200, -190),
            11 => new(side * 420, -130), 12 or 20 => new(side * 560, 160),
            13 => new(side * 260, -320), 14 => new(side * 450, -220),
            15 => new(side * 360, -270), 16 => new(-520, 0),
            17 => new(side * 380, -260), 18 => new(side * 450, -90),
            19 => new(side * 330, -320), 21 => new(side * 390, -190),
            23 => new(side * 500, -120), 24 => new(side * 250, -420),
            25 => new(0, -420), _ => new(side * 380, -300)
        };
        Fly(p.Center + offset, Speed);
    }
    private bool IsContactMove() => EncounterId switch
    {
        0 => Move is 0 or 2,
        3 or 4 or 10 or 17 => Move == 0,
        1 or 5 or 8 or 9 or 12 or 20 or 23 => Move == 0,
        2 or 6 => Move == 0,
        11 => Move == 1, 14 => Move is 0 or 2,
        15 => Move == 2, 16 => Move == 0,
        18 => Move is 0 or 2, 21 => Move == 2, _ => false
    };
    protected void Dash(float multiplier = 1.4f, int until = 30)
    {
        if (T == 1) NPC.velocity = (Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * Speed * multiplier;
        if (Grounded) NPC.velocity.Y = Math.Max(NPC.velocity.Y, 0);
        if (T >= until || NPC.collideX) Rest();
    }
    protected void Hop(float lift, float horizontal)
    {
        if (T == 1) NPC.velocity = new Vector2(Side * horizontal, -lift);
        if (T > 12 && NPC.collideY) NPC.velocity.X *= .8f;
    }
    protected void DrawCombatTelegraph(SpriteBatch batch, Vector2 screenPos)
    {
        if (CombatState != 1) return;
        Color c = CombatProfiles.Color(EncounterId) * .7f;
        CombatDrawing.Circle(batch, Aim - screenPos, Early ? 20 : 32, c, 2);
        if (IsContactMove()) CombatDrawing.Line(batch, NPC.Center - screenPos, Aim - screenPos, c * .7f, 2);
        Vector2 at = NPC.Top - screenPos - new Vector2(25, 18);
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X, (int)at.Y, 50, 4), Color.Black * .7f);
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X, (int)at.Y, (int)(50f * T / CombatProfiles.Windup(EncounterId)), 4), c);
    }
}
