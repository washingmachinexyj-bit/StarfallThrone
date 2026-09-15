using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Divine.Bosses;

public sealed class DivineBrazier : ModNPC
{
    public override string Texture => DivineCatalog.Root + "Material1";
    public int Serial, Progress;
    public int Side => (int)NPC.ai[1];
    public int Goal => Main.masterMode ? 1000 : Main.expertMode ? 800 : 600;
    public bool Filled => Progress >= Goal;
    public DivineBossNPC Boss => (int)NPC.ai[0] - 1 is int slot && slot >= 0 && slot < Main.maxNPCs && Main.npc[slot].active && Main.npc[slot].ModNPC is DivineBossNPC b && b.Serial == Serial && b.Index == 1 ? b : null;
    public bool Open => Boss is DivineBossNPC b && !Filled && b.Stagger == 0 && b.Stage == DivineStage.Attack && b.Timer < 190;
    public bool OwnedBy(DivineBossNPC b) => (int)NPC.ai[0] - 1 == b.NPC.whoAmI && Serial == b.Serial;
    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawModifiers draw = new() { Hide = true };
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, draw);
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = 76; NPC.height = 58; NPC.lifeMax = 1000000; NPC.defense = 0; NPC.damage = 0; NPC.value = 0;
        NPC.aiStyle = -1; NPC.noGravity = NPC.noTileCollide = true; NPC.knockBackResist = 0; NPC.npcSlots = 0;
        NPC.dontCountMe = true; NPC.HitSound = SoundID.NPCHit4; NPC.dontTakeDamage = true;
    }
    public override bool CheckActive() => false;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override void AI()
    {
        DivineBossNPC boss = Boss;
        if (boss == null) { NPC.active = false; return; }
        NPC.Center = DivineBossNPC.WorldPoint(boss.Arena + new Vector2(Side == 0 ? -380 : 380, -30));
        NPC.velocity = Vector2.Zero; NPC.timeLeft = 600; NPC.life = NPC.lifeMax; NPC.dontTakeDamage = !Open;
    }
    public override bool CheckDead() { NPC.life = NPC.lifeMax; if (Open) Charge(Goal); return false; }
    public void Charge(int amount)
    {
        DivineBossNPC boss = Boss;
        if (boss == null || !boss.Authority || !Open || amount <= 0) return;
        Progress = Math.Min(Goal, Progress + amount); NPC.netUpdate = true;
        boss.TryStagger();
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => Charge(damageDone);
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => Charge(damageDone);
    public override void SendExtraAI(BinaryWriter w) { w.Write(Serial); w.Write(Progress); }
    public override void ReceiveExtraAI(BinaryReader r) { Serial = r.ReadInt32(); Progress = Math.Clamp(r.ReadInt32(), 0, Goal); }
    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        DivineBossNPC boss = Boss; if (boss == null) return false;
        Vector2 at = NPC.Center - screenPos;
        Color c = Filled ? Color.White : Open ? Color.Gold : new Color(112, 87, 65);
        Vector2 hinge = boss.NPC.Center - screenPos + new Vector2(Side == 0 ? -50 : 50, -30);
        if (boss.Stagger == 0)
            for (int i = 0; i < 12; i++)
            {
                Vector2 link = Vector2.Lerp(hinge, at - new Vector2(0, 20), i / 11f);
                CombatDrawing.Circle(batch, link, 5, c * .5f, 2);
            }
        // Native primitive bowl/chain is encounter anatomy, not a substitute for the generated boss sprite.
        CombatDrawing.Line(batch, at + new Vector2(-36, -12), at + new Vector2(-23, 24), c, 8);
        CombatDrawing.Line(batch, at + new Vector2(-23, 24), at + new Vector2(23, 24), c, 8);
        CombatDrawing.Line(batch, at + new Vector2(23, 24), at + new Vector2(36, -12), c, 8);
        Texture2D fire = TextureAssets.Npc[Type].Value;
        batch.Draw(fire, at - new Vector2(0, Open ? 12 : 0), null, c, 0, fire.Size() / 2, Open ? 1.4f : .8f, SpriteEffects.None, 0);
        CombatDrawing.Line(batch, at + new Vector2(-34, 38), at + new Vector2(34, 38), Color.Black, 7);
        CombatDrawing.Line(batch, at + new Vector2(-34, 38), at + new Vector2(-34 + 68 * Progress / (float)Goal, 38), c, 4);
        Utils.DrawBorderString(batch, (Side + 1).ToString(), at - new Vector2(5, 52), c, .8f);
        return false;
    }
}

public abstract partial class DivineBossNPC
{
    public void SpawnBrazier(int side)
    {
        if (!Authority) return;
        int slot = NPC.NewNPC(NPC.GetSource_FromAI(), (int)Arena.X, (int)Arena.Y, ModContent.NPCType<DivineBrazier>(), ai0: NPC.whoAmI + 1, ai1: side);
        if (slot < 0 || slot >= Main.maxNPCs) return;
        ((DivineBrazier)Main.npc[slot].ModNPC).Serial = Serial; Main.npc[slot].netUpdate = true;
    }
    public DivineBrazier Brazier(int side)
    {
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is DivineBrazier b && b.OwnedBy(this) && b.Side == side) return b;
        return null;
    }
    public void AddOffering(int side, int amount) => Brazier(side)?.Charge(amount);
    public void TryStagger()
    {
        if (!Authority || Stagger > 0 || Brazier(0)?.Filled != true || Brazier(1)?.Filled != true) return;
        Stagger = 240; ClearHazards(); NPC.velocity = Vector2.Zero; NPC.netUpdate = true;
    }
    public void ResetBraziers()
    {
        if (!Authority) return;
        for (int i = 0; i < 2; i++) if (Brazier(i) is DivineBrazier b) { b.Progress = 0; b.NPC.netUpdate = true; }
    }
}
