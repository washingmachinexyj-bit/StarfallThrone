#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Oaths.Bosses;

/// <summary>A breakable objective, not another boss health pool. Only accepted damage up to its
/// remaining budget transfers at 50%; body and every node share native attack cooldown stamps.</summary>
public sealed class SupremeNode : ModNPC
{
    public override string Texture => SupremeBossNPC.Root + "Spark";
    public bool Ready;
    public int Serial, Progress;
    public Vector2 Anchor;
    private long remotePending;
    private int halfRemainder;
    public int Mark => (int)NPC.ai[1];
    public SupremeBossNPC? Boss => (int)NPC.ai[0] - 1 is int slot && slot >= 0 && slot < Main.maxNPCs && Main.npc[slot].active
        && Main.npc[slot].ModNPC is SupremeBossNPC b && b.Ready && !b.Cancelled && b.Serial == Serial ? b : null;
    public bool OwnedBy(SupremeBossNPC b) => (int)NPC.ai[0] - 1 == b.NPC.whoAmI && Serial == b.Serial;
    public int Goal => Math.Max(1, (int)((Boss?.NPC.lifeMax ?? 240000000) * .008d));
    public bool Open => Ready && Boss is SupremeBossNPC b && b.Stage == SupremeStage.Assault && !b.IsBroken(Mark);
    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 74; NPC.lifeMax = 500000000; NPC.damage = NPC.defense = 0;
        NPC.npcSlots = NPC.value = 0; NPC.dontCountMe = true; NPC.noGravity = NPC.noTileCollide = true;
        NPC.dontTakeDamage = true; NPC.knockBackResist = 0; NPC.aiStyle = -1; NPC.HitSound = SoundID.NPCHit4;
        Ready = false; Progress = Serial = halfRemainder = 0; remotePending = 0;
    }
    public void Setup(SupremeBossNPC b, Vector2 at) { Ready = true; Serial = b.Serial; Anchor = at; NPC.Center = at; NPC.dontTakeDamage = false; }
    public override bool CheckActive() => false;
    public override bool PreKill() => false;
    public override bool CheckDead()
    {
        if (Main.netMode == NetmodeID.Server && Open) remotePending += Math.Max(0L, (long)NPC.lifeMax - NPC.life);
        NPC.life = NPC.lifeMax; return false;
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override bool? CanBeHitByItem(Player player, Item item) => !Open || Boss?.AcceptItem(player) == false ? false : null;
    public override bool? CanBeHitByProjectile(Projectile projectile) => !Open || Boss?.AcceptProjectile(projectile) == false ? false : null;
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        if (!Open || Boss is not SupremeBossNPC b) return;
        b.StampItem(player);
        if (Main.netMode == NetmodeID.SinglePlayer) { TransferCredit(player.whoAmI); Charge(damageDone); }
    }
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (!Open || Boss is not SupremeBossNPC b) return;
        b.StampProjectile(projectile);
        if (Main.netMode == NetmodeID.SinglePlayer) { TransferCredit(projectile.owner); Charge(damageDone); }
    }
    public void TransferCredit(int direct = -1)
    {
        if (Boss is not SupremeBossNPC b || !b.Authority) return;
        for (int j = 0; j < Main.maxPlayers; j++) if (NPC.playerInteraction[j]) b.NPC.playerInteraction[j] = true;
        b.Credit(direct >= 0 && direct < Main.maxPlayers ? direct : NPC.lastInteraction);
    }
    public void Charge(long amount)
    {
        if (!Open || Boss is not SupremeBossNPC b || !b.Authority || amount <= 0) return;
        int accepted = (int)Math.Min(Math.Max(0, Goal - Progress), amount);
        Progress += accepted;
        int transfer = (accepted + halfRemainder) / 2; halfRemainder = (accepted + halfRemainder) % 2;
        // Already-final damage: StrikeNPC(HitInfo), never SimpleStrikeNPC/reapply defense or exposure.
        if (transfer > 0 && b.NPC.active && b.NPC.life > 0)
        {
            NPC.HitInfo hit = new() { Damage = transfer, HitDirection = 0, Knockback = 0, HideCombatText = true };
            b.NPC.StrikeNPC(hit, noPlayerInteraction: true);
            // The isolated art-smoke fixture emulates the server branch while the
            // engine is still on the main menu, where NetMessage has no peer state.
            // A real dedicated server is never in the menu, so it keeps the native
            // strike synchronization path.
            if (Main.netMode == NetmodeID.Server && !Main.gameMenu) NetMessage.SendStrikeNPC(b.NPC, hit);
            b.NPC.netUpdate = true;
        }
        if (Progress >= Goal && b.NPC.active) b.Break(Mark);
        NPC.netUpdate = true;
    }
    public override void AI()
    {
        if (!Ready) return;
        if (Boss is not SupremeBossNPC b)
        {
            NPC.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            return;
        }
        if (Main.netMode == NetmodeID.Server && Open)
        {
            long lost = remotePending + Math.Max(0L, (long)NPC.lifeMax - NPC.life);
            if (lost > 0) { TransferCredit(); Charge(lost); }
        }
        remotePending = 0; NPC.life = NPC.lifeMax; NPC.timeLeft = 600; NPC.dontTakeDamage = !Open;
        NPC.Center = b.Index == 1 ? b.NPC.Center + new Vector2(Mark % 2 == 0 ? -190 : 190, (Mark / 2 - 1) * 100) : Anchor;
        NPC.velocity = Vector2.Zero;
    }
    public override void SendExtraAI(BinaryWriter w)
    { w.Write(Ready); w.Write(Serial); w.Write(Progress); SupremeBossNPC.Write(w, Anchor); }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        bool ready = r.ReadBoolean(); int serial = r.ReadInt32(), progress = r.ReadInt32(); Vector2 at = SupremeBossNPC.Read(r);
        Ready = ready; Serial = serial; Progress = Math.Clamp(progress, 0, 12000000); Anchor = SupremeBossNPC.Point(at);
    }
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        if (!Ready || Boss is not SupremeBossNPC b) return false;
        Vector2 at = NPC.Center - screenPos; Color c = b.Tint * (Open ? 1 : .22f);
        CombatDrawing.Line(batch, b.NPC.Center - screenPos, at, c * .3f, 3);
        if (b.Index == 0)
        {
            CombatDrawing.Disc(batch, at, 19, c * .7f);
            for (int j = 0; j < 3; j++)
            {
                Vector2 tip = at + (MathHelper.TwoPi * j / 3 - MathHelper.PiOver2).ToRotationVector2() * 34;
                CombatDrawing.Line(batch, at, tip, c, 5); CombatDrawing.Circle(batch, tip, 9, c, 3);
            }
        }
        else
        {
            for (int j = 0; j < 5; j++)
            {
                Vector2 tip = at + new Vector2((Mark % 2 == 0 ? -1 : 1) * (15 + j * 7), -30 + j * 12);
                CombatDrawing.Line(batch, at + new Vector2(0, 22), tip, c, 5);
            }
        }
        CombatDrawing.Circle(batch, at, 39, c, 2);
        if (Open)
        {
            CombatDrawing.Line(batch, at + new Vector2(-35, 48), at + new Vector2(35, 48), Color.Black, 7);
            CombatDrawing.Line(batch, at + new Vector2(-35, 48), at + new Vector2(-35 + 70 * (1 - Progress / (float)Goal), 48), c, 4);
        }
        Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.OathsCombat.Node" + b.Index), at - new Vector2(0, 56), c, .55f, .5f);
        return false;
    }
}
