#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;
namespace StarfallThrone.Content.Pantheon.Bosses;

/// <summary>Breakable tactical objectives and true damage-forwarding multipart bodies; neither drops loot.</summary>
public sealed class PantheonNode : ModNPC
{
    public override string Texture => "Terraria/Images/MagicPixel";
    public int Mark => (int)NPC.ai[1];
    public bool Part => NPC.ai[2] == 1;
    public int Serial, Progress;
    public bool Ready;
    private long pendingRemoteDamage;
    public Vector2 Anchor;
    public PantheonBossNPC? Boss => (int)NPC.ai[0] - 1 is int slot && slot >= 0 && slot < Main.maxNPCs && Main.npc[slot].active
        && Main.npc[slot].ModNPC is PantheonBossNPC b && b.Serial == Serial && b.Ready && !b.Cancelled ? b : null;
    public bool OwnedBy(PantheonBossNPC b) => (int)NPC.ai[0] - 1 == b.NPC.whoAmI && Serial == b.Serial;
    public int Goal => Math.Max(1, (int)((Boss?.NPC.lifeMax ?? 12000000) * .004f));
    public bool Open => Ready && Boss is PantheonBossNPC b && (Part || b.Stage == PantheonStage.Attack && !b.IsBroken(Mark)
        && (b.Index != 9 || b.Chosen < 0));
    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 70; NPC.lifeMax = 500_000_000; NPC.damage = NPC.defense = 0;
        NPC.value = 0; NPC.npcSlots = 0; NPC.dontCountMe = true; NPC.aiStyle = -1;
        NPC.noGravity = NPC.noTileCollide = NPC.dontTakeDamage = true; NPC.knockBackResist = 0;
        NPC.HitSound = SoundID.NPCHit4; Serial = Progress = 0; Ready = false;
        pendingRemoteDamage = 0;
    }
    public override bool CheckActive() => false;
    public override bool PreKill() => false;
    public override bool CheckDead()
    {
        // A proxy cannot die independently; preserve even a lethal native remote hit before resetting it.
        if (Main.netMode == NetmodeID.Server && Open)
            pendingRemoteDamage += Math.Max(0L, (long)NPC.lifeMax - NPC.life);
        NPC.life = NPC.lifeMax; return false;
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool? CanBeHitByItem(Player player, Item item) => !Open || Part && Boss?.AcceptItem(player) == false ? false : null;
    public override bool? CanBeHitByProjectile(Projectile projectile) => !Open || Part && Boss?.AcceptProjectile(projectile) == false ? false : null;
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override void AI()
    {
        if (!Ready) return;
        var boss = Boss;
        if (boss == null)
        {
            NPC.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            return;
        }
        if (Main.netMode == NetmodeID.Server && (NPC.life < NPC.lifeMax || pendingRemoteDamage > 0) && Open)
        {
            long lost = pendingRemoteDamage + Math.Max(0L, (long)NPC.lifeMax - NPC.life);
            TransferCredit();
            if (Part) boss.ForwardDamage(lost); else Charge((int)Math.Min(Goal, lost));
            pendingRemoteDamage = 0;
            NPC.netUpdate = true;
        }
        NPC.timeLeft = 600; NPC.life = NPC.lifeMax; NPC.dontTakeDamage = !Open; NPC.velocity = Vector2.Zero;
        if (Part)
        {
            if (boss.Index is 2 or 10)
            {
                if (boss.Index == 10 && boss.Phase == 2 && Mark >= 5)
                    NPC.Center = boss.Arena + new Vector2(MathF.Sin((boss.Age - (Mark - 5) * 8) * .023f) * 300,
                        MathF.Cos((boss.Age - (Mark - 5) * 8) * .023f) * 160);
                else NPC.Center = boss.Spine[Math.Min(59, (Mark + 1) * 5)];
                NPC.rotation = Mark == 0 ? boss.NPC.rotation : (boss.Spine[Math.Min(59, Mark * 5)] - NPC.Center).ToRotation();
            }
            else if (boss.Index == 8)
            {
                float angle = boss.Age * (boss.Move == 1 ? -.013f : .013f) + Mark * MathHelper.Pi;
                float radius = boss.Move == 2 && boss.Stage == PantheonStage.Attack ? Math.Max(80, 270 - boss.Timer * .4f) : 270;
                NPC.Center = boss.Arena + angle.ToRotationVector2() * new Vector2(radius, radius * .55f) - new Vector2(0, 100);
            }
            else NPC.Center = boss.NPC.Center + new Vector2((Mark % 2 == 0 ? -1 : 1) * (boss.Index == 16 ? 150 : 130), Mark / 2 * 110 - 30);
        }
        else if (boss.Index == 4)
            NPC.Center = Vector2.Lerp(Anchor, boss.Origin, Math.Clamp((boss.Timer - 90) / 330f, 0, 1));
        else if (boss.Index == 8)
            NPC.Center = boss.PartPosition(Mark);
        else NPC.Center = Anchor;
    }
    public void Charge(int amount)
    {
        var boss = Boss; if (boss == null || !boss.Authority || !Open || Part || amount <= 0) return;
        Progress = (int)Math.Min(Goal, (long)Progress + amount); NPC.netUpdate = true;
        if (Progress >= Goal) boss.Break(Mark);
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        var boss = Boss; if (boss == null || Part && !boss.AcceptItem(player)) return;
        if (Part) boss.StampItem(player);
        if (Main.netMode == NetmodeID.SinglePlayer) { TransferCredit(player.whoAmI); if (Part) boss.ForwardDamage(damageDone); else Charge(damageDone); }
    }
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        var boss = Boss; if (boss == null || Part && !boss.AcceptProjectile(projectile)) return;
        if (Part) boss.StampProjectile(projectile);
        if (Main.netMode == NetmodeID.SinglePlayer) { TransferCredit(projectile.owner); if (Part) boss.ForwardDamage(damageDone); else Charge(damageDone); }
    }
    public void TransferCredit(int directPlayer = -1)
    {
        var boss = Boss; if (boss == null || !boss.Authority) return;
        for (int i = 0; i < Main.maxPlayers; i++)
            if (NPC.playerInteraction[i] || i == directPlayer) boss.NPC.playerInteraction[i] = true;
        int last = directPlayer >= 0 && directPlayer < Main.maxPlayers ? directPlayer : NPC.lastInteraction;
        if (last >= 0 && last < Main.maxPlayers) boss.NPC.lastInteraction = last;
    }
    public override void SendExtraAI(BinaryWriter w)
    { w.Write(Ready); w.Write(Serial); w.Write(Progress); PantheonBossNPC.Write(w, Anchor); }
    public override void ReceiveExtraAI(BinaryReader r)
    { Ready = r.ReadBoolean(); Serial = r.ReadInt32(); Progress = Math.Max(0, r.ReadInt32()); Anchor = PantheonBossNPC.Point(PantheonBossNPC.Read(r)); }
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        var boss = Boss; if (!Ready || boss == null) return false;
        Vector2 at = NPC.Center - screenPos; Color tint = boss.Tint;
        if (Part)
        {
            Texture2D tex = TextureAssets.Npc[boss.Type].Value;
            if (boss.Index == 8)
            {
                Rectangle source = new(Mark * tex.Width / 2, 0, tex.Width / 2, tex.Height);
                batch.Draw(tex, at, source, Color.White * .95f, MathF.Sin(boss.Age * .02f) * .045f, source.Size() / 2, .9f, SpriteEffects.None, 0);
            }
            else if (boss.Index is 2 or 10)
            {
                Vector2 prev = Mark == 0 ? boss.NPC.Center : boss.PartPosition(Mark - 1);
                CombatDrawing.Line(batch, prev - screenPos, at, tint * .32f, 38);
                float taper = 1 - Math.Max(0, Mark - 6) * .13f;
                CombatDrawing.Disc(batch, at, (boss.Index == 2 ? 30 : 25) * taper, new Color(30, 29, 44));
                // Confirmed against the actual 256px source images: use only a small scale/fog patch,
                // never the complete S-shaped creature. Spine orientation and tail taper animate it.
                Rectangle source = SegmentSource(boss.Index);
                batch.Draw(tex, at, source, Color.White * .95f, NPC.rotation + MathHelper.PiOver2,
                    source.Size() / 2, 1.22f * taper, SpriteEffects.None, 0);
                PantheonMotif.Draw(batch, at, boss.Index, (boss.Index == 2 ? 34 : 30) * taper, tint * .46f);
                if (boss.Index == 10) CombatDrawing.Line(batch, at - new Vector2(19, 6) * taper, at + new Vector2(19, 6) * taper, Color.Orange * .48f, 3);
            }
            else
            {
                CombatDrawing.Line(batch, boss.NPC.Center - screenPos, at, tint * .38f, 12);
                PantheonMotif.Draw(batch, at, boss.Index + Mark, 40, tint);
                CombatDrawing.Circle(batch, at, 20, Color.White * .7f, 3);
            }
            return false;
        }
        Color c = Open ? tint : tint * .22f;
        if (boss.Index is 11 or 12)
        {
            CombatDrawing.Line(batch, at - new Vector2(23, -35), at - new Vector2(23, 35), c, 6);
            CombatDrawing.Line(batch, at + new Vector2(23, -35), at + new Vector2(23, 35), c, 6);
        }
        if (boss.Index == 1)
        {
            CombatDrawing.Line(batch, at - new Vector2(15, 35), at + new Vector2(15, 35), c, 7);
            CombatDrawing.Line(batch, at + new Vector2(15, -35), at - new Vector2(15, -35), c, 7);
        }
        PantheonMotif.Draw(batch, at, boss.Index, 27, c);
        if (Open)
        {
            CombatDrawing.Circle(batch, at, 37, c * .7f, 2);
            CombatDrawing.Line(batch, at + new Vector2(-29, 43), at + new Vector2(29, 43), Color.Black, 7);
            CombatDrawing.Line(batch, at + new Vector2(-29, 43), at + new Vector2(-29 + 58 * Progress / (float)Goal, 43), c, 4);
        }
        Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.PantheonCombat.Node" + boss.Index) + " " + (Mark + 1),
            at - new Vector2(0, 53), c, .5f, .5f);
        return false;
    }
    public static Rectangle SegmentSource(int bossIndex) => bossIndex == 2 ? new Rectangle(45, 110, 50, 50) : new Rectangle(54, 112, 52, 48);
}

public abstract partial class PantheonBossNPC
{
    public int PartCount => Index is 2 or 10 ? 10 : Index is 5 or 6 or 8 ? 2 : Index == 9 ? 4 : Index == 16 ? 3 : 0;
    public int CounterCount => Index == 3 ? 1 : Index is 5 or 6 or 8 ? 2 : Index == 9 ? 4 : Index == 14 ? 7 : 3;
    public PantheonNode? SpawnNode(int mark, Vector2 point, bool part = false)
    {
        if (!Authority || !Ready || Cancelled) return null;
        int count = 0; foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is PantheonNode existing && existing.OwnedBy(this)) count++;
        if (count >= 17) return null;
        point = Point(point);
        int slot = NPC.NewNPC(NPC.GetSource_FromAI(), (int)point.X, (int)point.Y, ModContent.NPCType<PantheonNode>(),
            ai0: NPC.whoAmI + 1, ai1: mark, ai2: part ? 1 : 0);
        if (slot < 0 || slot >= Main.maxNPCs) return null;
        var node = (PantheonNode)Main.npc[slot].ModNPC;
        node.Ready = true; node.Serial = Serial; node.Anchor = point; node.NPC.Center = point; node.NPC.netUpdate = true;
        return node;
    }
    private void SpawnParts() { for (int i = 0; i < PartCount; i++) SpawnNode(i, NPC.Center, true); }
    public void CreateCounterplay()
    {
        if (!Authority) return;
        for (int i = 0; i < CounterCount; i++)
        {
            Vector2 point = Arena + new Vector2((i - (CounterCount - 1) / 2f) * 165, Index is 11 or 12 ? 45 : -30);
            if (Index is 1 or 7 or 14) point = Arena + (i * MathHelper.TwoPi / CounterCount - MathHelper.PiOver2).ToRotationVector2() * new Vector2(250, 160);
            if (Index == 3) point = NPC.Center + new Vector2(0, 90);
            if (Index == 4) point = Arena + new Vector2((i - 1) * 320, 130);
            SpawnNode(i, point);
        }
    }
    public Vector2 NodePosition(int mark)
    {
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is PantheonNode node && node.OwnedBy(this) && !node.Part && node.Mark == mark) return n.Center;
        return Arena + new Vector2((mark - 1) * 165, -30);
    }
    public Vector2 PartPosition(int mark)
    {
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is PantheonNode node && node.OwnedBy(this) && node.Part && node.Mark == mark) return n.Center;
        return NPC.Center;
    }
}
