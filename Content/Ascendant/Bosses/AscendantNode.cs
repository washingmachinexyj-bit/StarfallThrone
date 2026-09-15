using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ascendant.Bosses;

public enum AscendantNodeKind { Dew, Furnace, Knot }

public sealed class AscendantNode : ModNPC
{
    public override string Texture => "Terraria/Images/MagicPixel";
    public AscendantNodeKind Kind => (AscendantNodeKind)(int)NPC.ai[2];
    public int Mark => (int)NPC.ai[1];
    public int Serial, Progress;
    public bool Ready;
    public Vector2 Anchor;
    public AscendantBossNPC Boss => (int)NPC.ai[0] - 1 is int slot && slot >= 0 && slot < Main.maxNPCs
        && Main.npc[slot].active && Main.npc[slot].ModNPC is AscendantBossNPC b && b.Serial == Serial && !b.Cancelled ? b : null;
    public bool OwnedBy(AscendantBossNPC boss) => (int)NPC.ai[0] - 1 == boss.NPC.whoAmI && Serial == boss.Serial;
    public int Goal => (int)((Kind == AscendantNodeKind.Dew ? 300 : Kind == AscendantNodeKind.Furnace ? 1600 : 6500) * (Main.masterMode ? 1.9f : Main.expertMode ? 1.5f : 1));
    public bool Filled => Progress >= Goal;
    public bool Open => Ready && Boss is AscendantBossNPC boss && AscendantWorld.Open(boss.Index) && !Filled && !boss.FinalActive
        && boss.Stage == AscendantStage.Attack && (Kind == AscendantNodeKind.Dew ? boss.Timer < 335
            : Kind == AscendantNodeKind.Knot ? boss.Timer < 330 && !boss.KnotBroken
            : boss.Phase > 0 && boss.Suppressed < 0 && boss.Timer < (boss.Phase == 2 ? 440 : 330) && boss.FurnaceActive(Mark));
    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 66; NPC.lifeMax = 1000000; NPC.damage = NPC.defense = 0; NPC.value = 0;
        NPC.aiStyle = -1; NPC.noGravity = NPC.noTileCollide = NPC.dontCountMe = NPC.dontTakeDamage = true;
        NPC.knockBackResist = 0; NPC.npcSlots = 0; NPC.HitSound = SoundID.NPCHit4;
        Progress = Serial = 0; Ready = false; Anchor = Vector2.Zero;
    }
    public override bool CheckActive() => false;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool PreKill() => false;
    public override bool CheckDead()
    { NPC.life = NPC.lifeMax; if (Open) Charge(Goal); return false; }
    public override void AI()
    {
        if (!Ready) return;
        AscendantBossNPC boss = Boss;
        if (boss == null || !AscendantWorld.Open(boss.Index))
        {
            NPC.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            return;
        }
        NPC.Center = Anchor; NPC.velocity = Vector2.Zero; NPC.timeLeft = 600; NPC.life = NPC.lifeMax; NPC.dontTakeDamage = !Open;
    }
    public void Charge(int amount)
    {
        AscendantBossNPC boss = Boss;
        if (boss == null || !boss.Authority || !Open || amount <= 0) return;
        Progress = Math.Min(Goal, Progress + amount); NPC.netUpdate = true;
        if (Filled) boss.BreakNode(Kind, Mark);
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => Charge(damageDone);
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => Charge(damageDone);
    public override void SendExtraAI(BinaryWriter writer)
    { writer.Write(Ready); writer.Write(Serial); writer.Write(Progress); AscendantBossNPC.WriteVector(writer, Anchor); }
    public override void ReceiveExtraAI(BinaryReader reader)
    { Ready = reader.ReadBoolean(); Serial = reader.ReadInt32(); Progress = Math.Clamp(reader.ReadInt32(), 0, Goal); Anchor = AscendantBossNPC.WorldPoint(AscendantBossNPC.ReadVector(reader)); }
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        AscendantBossNPC boss = Boss; if (!Ready || boss == null) return false;
        Vector2 at = NPC.Center - screenPos; Color c = Filled ? Color.White * .4f : Open ? boss.Tint : boss.Tint * .4f;
        CombatDrawing.Line(batch, at, boss.NPC.Center - screenPos, c * .25f, 2);
        if (Kind == AscendantNodeKind.Furnace)
        {
            CombatDrawing.Line(batch, at + new Vector2(-31, 5), at + new Vector2(-22, 28), c, 5);
            CombatDrawing.Line(batch, at + new Vector2(-22, 28), at + new Vector2(22, 28), c, 5);
            CombatDrawing.Line(batch, at + new Vector2(22, 28), at + new Vector2(31, 5), c, 5);
            AscendantGlyph.Draw(batch, at - new Vector2(0, 8), Mark, 24, c);
        }
        else if (Kind == AscendantNodeKind.Dew)
        {
            CombatDrawing.Disc(batch, at, 22, c * .35f); CombatDrawing.Circle(batch, at, 27, c, 3);
            AscendantGlyph.Draw(batch, at, 0, 17, c);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * .5f + i * MathHelper.TwoPi / 3;
                CombatDrawing.Circle(batch, at + angle.ToRotationVector2() * 10, 24, c, 2);
            }
        }
        if (Open || Filled)
        {
            Vector2 bar = at + new Vector2(-32, 39);
            CombatDrawing.Line(batch, bar, bar + new Vector2(64, 0), Color.Black, 7);
            CombatDrawing.Line(batch, bar, bar + new Vector2(64 * Progress / (float)Goal, 0), c, 4);
        }
        string key = Kind == AscendantNodeKind.Dew ? "Dew" : Kind == AscendantNodeKind.Knot ? "Knot" : "Furnace" + Mark;
        Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.AscendantCombat." + key), at - new Vector2(0, 54), c, .65f, .5f);
        return false;
    }
}

public abstract partial class AscendantBossNPC
{
    public bool FurnaceActive(int law)
    {
        if (Index != 1 || Stage != AscendantStage.Attack) return false;
        if (Phase == 2) return law == Math.Min(3, Math.Max(0, Timer - 1) / 130);
        return law == Move || Phase > 0 && law == (Move + 1) % 4;
    }
    public AscendantNode SpawnNode(AscendantNodeKind kind, int mark)
    {
        if (!Authority || !Ready || Cancelled) return null;
        Vector2 point = kind switch
        {
            AscendantNodeKind.Dew => FloorNear(Arena + new Vector2((mark - 1) * 190, 20)) - new Vector2(0, 44),
            AscendantNodeKind.Furnace => FloorNear(Arena + new Vector2((mark - 1.5f) * 180, 20)) - new Vector2(0, 48),
            _ => Arena + new Vector2(0, -80)
        };
        point = WorldPoint(point);
        int slot = NPC.NewNPC(NPC.GetSource_FromAI(), (int)point.X, (int)point.Y, ModContent.NPCType<AscendantNode>(), ai0: NPC.whoAmI + 1, ai1: mark, ai2: (int)kind);
        if (slot < 0 || slot >= Main.maxNPCs) return null;
        var node = (AscendantNode)Main.npc[slot].ModNPC;
        node.Serial = Serial; node.Ready = true; node.Anchor = point; node.NPC.Center = point; node.NPC.netUpdate = true;
        return node;
    }
    public void BreakNode(AscendantNodeKind kind, int mark)
    {
        if (!Authority || Cancelled || Stage != AscendantStage.Attack) return;
        if (kind == AscendantNodeKind.Dew && Index == 0 && mark is >= 0 and < 3 && (BrokenMask & (1 << mark)) == 0)
        { BrokenMask |= 1 << mark; ClearHazards(20 + mark); Expose(1.20f, 300); }
        else if (kind == AscendantNodeKind.Furnace && Index == 1 && Suppressed < 0 && FurnaceActive(mark))
        { Suppressed = mark; ClearHazards(mark); Expose(1.25f, 360); }
        else if (kind == AscendantNodeKind.Knot && Index == 2 && !KnotBroken)
        { KnotBroken = true; ClearHazards(99); Expose(1.20f, 240); }
        NPC.netUpdate = true;
    }
}
