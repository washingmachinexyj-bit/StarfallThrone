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

namespace StarfallThrone.Content.Ecology.Combat;

/// <summary>Finite, lootless encounter objects. Kind: hive/radiator/drone/mouth/plate/capsule/magnet/root/gate/guard.</summary>
public sealed class EcologyPart : ModNPC
{
    public override string Texture => EcologyCatalog.Root + "Core0";
    public int Serial, Biome, Age, Duration = 220;
    public int Kind => (int)NPC.ai[1];
    public int OwnerSlot => (int)NPC.ai[0] - 1;
    public Vector2 Offset, Exit;
    public bool Ready;
    public EcologyBossNPC Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active && Main.npc[OwnerSlot].ModNPC is EcologyBossNPC b && b.Serial == Serial && !b.Cancelled ? b : null;
    public bool OwnedBy(EcologyBossNPC boss) => OwnerSlot == boss.NPC.whoAmI && Serial == boss.Serial;
    public override void SetStaticDefaults() { Main.npcFrameCount[Type] = 1; NPCID.Sets.NPCBestiaryDrawModifiers value = new() { Hide = true }; NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value); }
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 44; NPC.noGravity = NPC.noTileCollide = true; NPC.aiStyle = -1; NPC.lifeMax = 150;
        NPC.damage = NPC.defense = 0; NPC.knockBackResist = 0; NPC.npcSlots = 0; NPC.dontCountMe = true;
        NPC.value = 0; NPC.HitSound = SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath6; Ready = false; Age = 0;
    }
    public void Setup(EcologyBossNPC boss, int kind, Vector2 offset, int duration, Vector2 exit)
    {
        Serial = boss.Serial; Biome = boss.Index; Offset = offset; Duration = duration; Exit = exit;
        NPC.life = NPC.lifeMax = Math.Max(80, boss.NPC.lifeMax / (kind == 9 ? 180 : 65)); NPC.ai[1] = kind;
        NPC.dontTakeDamage = kind == 8; Ready = true; NPC.netUpdate = true;
    }
    public override bool CheckActive() => false;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool PreKill() => false; // No loot or normal kill tally from breakable battlefield props.
    public override bool CheckDead()
    {
        if (Boss is EcologyBossNPC b && b.Authority) b.PartBroken(Kind);
        NPC.active = false;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
        return false;
    }
    public override void AI()
    {
        if (!Ready) return;
        EcologyBossNPC boss = Boss;
        if (boss == null) { NPC.active = false; return; }
        NPC.timeLeft = 600; Age++;
        if (Kind <= 4) NPC.Center = boss.Bound(boss.NPC.Center + Offset.RotatedBy(MathF.Sin(Age * .016f) * .2f));
        if (Kind == 9 && boss.NPC.HasValidTarget)
        {
            Vector2 to = Main.player[boss.NPC.target].Center;
            if (Age < 40) NPC.velocity *= .9f;
            else if (Age == 40) NPC.velocity = (to - NPC.Center).SafeNormalize(Vector2.UnitX) * 7;
            if (Age == 70 && boss.Authority) boss.Emit(EcologyShape.Shard, NPC.Center, NPC.velocity, 30, 65, 12);
        }
        if (boss.Authority && Kind == 3 && Age % 80 == 40 && boss.NPC.HasValidTarget)
            boss.Fan(NPC.Center, Main.player[boss.NPC.target].Center, 3, .38f, 5, 40);
        if (Age < Duration) return;
        if (boss.Authority)
        {
            if (Kind == 5) boss.Emit(EcologyShape.Pool, NPC.Center, Vector2.Zero, 30, 120, 65);
            // Repair drones recharge a finite armor shell, never heal the boss's life.
            if (Kind == 2) { boss.Shell = Math.Min(boss.NPC.lifeMax / 30, boss.Shell + boss.NPC.lifeMax / 120); boss.NPC.netUpdate = true; }
            NPC.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
        }
    }
    public override void SendExtraAI(BinaryWriter w)
    { w.Write(Ready); w.Write(Serial); w.Write(Biome); w.Write(Age); w.Write(Duration); EcologyBossNPC.Write(w, Offset); EcologyBossNPC.Write(w, Exit); }
    public override void ReceiveExtraAI(BinaryReader r)
    { Ready = r.ReadBoolean(); Serial = r.ReadInt32(); Biome = Math.Clamp(r.ReadInt32(), 0, 9); Age = r.ReadInt32(); Duration = r.ReadInt32(); Offset = new Vector2(r.ReadSingle(), r.ReadSingle()); Exit = EcologyBossNPC.Read(r); }
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        Color tint = EcologyCatalog.Colors[Biome]; Vector2 at = NPC.Center - screenPos;
        Texture2D tex = ModContent.Request<Texture2D>(EcologyCatalog.Root + "Core" + Biome).Value;
        batch.Draw(tex, at, null, Color.Lerp(drawColor, tint, .4f), Age * (Kind == 6 ? .035f : .008f), tex.Size() / 2, 48f / Math.Max(tex.Width, tex.Height), SpriteEffects.None, 0);
        CombatDrawing.Circle(batch, at, Kind == 7 ? 30 : 25, tint * .7f, Kind == 8 ? 4 : 2);
        if (Kind == 8) CombatDrawing.Line(batch, at, Exit - screenPos, tint * .3f, 1);
        if (!NPC.dontTakeDamage) CombatDrawing.Line(batch, at + new Vector2(-20, 32), at + new Vector2(-20 + 40f * NPC.life / NPC.lifeMax, 32), Color.White, 3);
        return false;
    }
}
