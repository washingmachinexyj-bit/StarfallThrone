using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Bosses;

public enum VoyagePart { Cargo, NeuralNode, Hangar, Anchor, Bulkhead, Mirror, Weapon, RootNode, Geometry, Sail, Gate, Segment, RedEye, BlueEye, LeftCity, RightCity, Array, Asteroid, Drone, SeedFlower }

public sealed class VoyageDeviceNPC : ModNPC
{
    public override string Texture => VoyageCatalog.Root + "Core0";
    public int OwnerSlot => (int)NPC.ai[0] - 1;
    public VoyagePart Role => (VoyagePart)(int)NPC.ai[1];
    public int Slot => (int)NPC.ai[2];
    public int Age => (int)NPC.ai[3];
    public int Serial, Lifetime;
    public bool BudgetPart, Configured;
    public Vector2 Anchor;
    public bool Temporary => Lifetime > 0;
    public VoyageBossNPC Boss => OwnerSlot >= 0 && OwnerSlot < Main.maxNPCs && Main.npc[OwnerSlot].active &&
        Main.npc[OwnerSlot].ModNPC is VoyageBossNPC boss && boss.Serial == Serial ? boss : null;
    public bool OwnedBy(VoyageBossNPC boss) => OwnerSlot == boss.NPC.whoAmI && Serial == boss.Serial;
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 64;
        NPC.lifeMax = 10000;
        NPC.damage = 0;
        NPC.defense = 65;
        NPC.aiStyle = -1;
        NPC.noGravity = NPC.noTileCollide = true;
        NPC.knockBackResist = 0;
        NPC.value = 0;
        NPC.npcSlots = 0;
        NPC.HitSound = SoundID.NPCHit4;
        NPC.DeathSound = SoundID.NPCDeath14;
    }
    public void SizeBody()
    {
        Vector2 center = NPC.Center;
        int size = Role switch { VoyagePart.RedEye or VoyagePart.BlueEye => 120, VoyagePart.Segment => Slot == 0 ? 110 : 70,
            VoyagePart.LeftCity or VoyagePart.RightCity => 160, VoyagePart.Array => 145, VoyagePart.Drone => 44, _ => 64 };
        NPC.width = NPC.height = size;
        NPC.Center = center;
    }
    public override bool CheckActive() => false;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        VoyageBossNPC boss = Boss;
        if (boss == null || boss.Stagger > 0 || boss.Stage != VoyageStage.Attack) return false;
        if (Role == VoyagePart.Segment) return boss.ContactActive;
        bool dashEye = Role == VoyagePart.RedEye || Role == VoyagePart.BlueEye && boss.Part(0, VoyagePart.RedEye) == null;
        return dashEye && boss.Move != 1 && boss.Timer is >= 25 and < 65 && NPC.velocity.LengthSquared() > 36;
    }
    public override void AI()
    {
        if (!Configured) return;
        VoyageBossNPC boss = Boss;
        if (boss == null)
        {
            // Parent and child sync packets can be adjacent but arrive in different update frames.
            if (Main.netMode != NetmodeID.MultiplayerClient || ++NPC.localAI[0] > 90) NPC.active = false;
            return;
        }
        NPC.localAI[0] = 0;
        NPC.ai[3]++;
        NPC.timeLeft = 600;
        NPC.damage = 530 + boss.BossIndex * 14;
        NPC.GivenName = Language.GetTextValue("Mods.StarfallThrone.VoyageParts." + Role);
        NPC.dontTakeDamage = Role == VoyagePart.Array && boss.CityAlive;
        if (Temporary)
        {
            TemporaryAI(boss);
            if (Age >= Lifetime && boss.IsServer)
            {
                NPC.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            }
        }
        else
        {
            Vector2 dest = boss.DeviceDestination(this);
            bool dashEye = Role == VoyagePart.RedEye || Role == VoyagePart.BlueEye && boss.Part(0, VoyagePart.RedEye) == null;
            if (dashEye && boss.Stage == VoyageStage.Attack && boss.Move != 1 && boss.Timer is >= 25 and < 65)
            {
                if (boss.Timer == 25) NPC.velocity = (boss.Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * 20;
            }
            else
            {
                float speed = Role == VoyagePart.Segment ? 24 : 17;
                Vector2 desired = (dest - NPC.Center).SafeNormalize(Vector2.UnitY) * Math.Min(speed, NPC.Distance(dest) * .12f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, Role == VoyagePart.Segment ? .4f : .12f);
            }
        }
        NPC.rotation = Role == VoyagePart.Segment ? NPC.velocity.ToRotation() : Role == VoyagePart.Mirror ? Age * .008f : 0;
        if (boss.IsServer && Age % 60 == 0) NPC.netUpdate = true;
    }
    private void TemporaryAI(VoyageBossNPC boss)
    {
        switch (Role)
        {
            case VoyagePart.Cargo:
                NPC.velocity = Vector2.Lerp(NPC.velocity, (Anchor - NPC.Center) * .05f, .15f);
                if (Age == 90)
                {
                    if (Slot % 2 == 0) boss.Emit(VoyageHazardShape.Ring, NPC.Center, Vector2.Zero, 65, 70, 360, 0, .7f);
                    else for (int i = -1; i <= 1; i++) boss.SpawnPart(VoyagePart.Drone, i + 4, NPC.Center + new Vector2(i * 40, -30), lifetime: 210);
                }
                break;
            case VoyagePart.Drone:
                Vector2 dest = boss.Aim + new Vector2((Slot % 5 - 2) * 100, -230);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (dest - NPC.Center).SafeNormalize(Vector2.UnitY) * Math.Min(10, NPC.Distance(dest) * .06f), .08f);
                if (Age == 85) boss.Emit(VoyageHazardShape.Bolt, NPC.Center, (boss.Aim - NPC.Center).SafeNormalize(Vector2.UnitY) * 10, 60, 100, 14);
                break;
            case VoyagePart.Asteroid:
                NPC.velocity = new Vector2(MathF.Sin(Slot * 1.3f) * 1.2f, .7f);
                if (Age == 130) boss.Emit(VoyageHazardShape.Mine, NPC.Center, Vector2.Zero, 70, 20, 95, 0, .8f);
                break;
            case VoyagePart.SeedFlower:
                NPC.velocity = Vector2.Lerp(NPC.velocity, (Anchor - NPC.Center) * .04f, .15f);
                if (Age == 90)
                    for (int i = -1; i <= 1; i++)
                        boss.Emit(VoyageHazardShape.Arc, NPC.Center, new Vector2(i * 3, -8), 65, 115, 14, deviceSlot: NPC.whoAmI);
                break;
        }
    }
    public override void OnKill()
    {
        VoyageBossNPC boss = Boss;
        if (boss == null) return;
        boss.NotifyPartDestroyed(this);
        // Any hazard tied to a breakable node is cancelled when that node is broken.
        if (boss.IsServer)
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.ModProjectile is VoyageHazard h && h.OwnerSlot == boss.NPC.whoAmI && h.Serial == Serial && h.DeviceSlot == NPC.whoAmI) p.Kill();
    }
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Configured); writer.Write(Serial); writer.Write(Lifetime); writer.Write(BudgetPart); writer.Write(NPC.lifeMax);
        VoyageBossNPC.WriteVector(writer, Anchor);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Configured = reader.ReadBoolean(); Serial = reader.ReadInt32(); Lifetime = reader.ReadInt32(); BudgetPart = reader.ReadBoolean(); NPC.lifeMax = reader.ReadInt32();
        Anchor = VoyageBossNPC.ReadVector(reader);
        SizeBody();
    }
    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
    /// <summary>Dedicated anatomy sprites, or a semantic device icon. No world entity draws an entire boss-head icon.</summary>
    public string ArtKey
    {
        get
        {
            VoyageBossNPC boss = Boss;
            if (boss == null) return "Core0";
            return Role switch
            {
                VoyagePart.Segment => "Worm" + boss.BossIndex + SegmentSection(boss),
                VoyagePart.RedEye => "TwinRed",
                VoyagePart.BlueEye => "TwinBlue",
                VoyagePart.LeftCity => "ArkLeftCity",
                VoyagePart.RightCity => "ArkRightCity",
                VoyagePart.Array => "ArkArray",
                VoyagePart.Cargo => "Core0",
                VoyagePart.NeuralNode => "Expert3",
                VoyagePart.Hangar => "Core4",
                VoyagePart.Anchor => "Expert5",
                VoyagePart.Bulkhead => "Weapon25",
                VoyagePart.Mirror => "Material7",
                VoyagePart.Weapon => Slot switch { 0 => "Weapon33", 1 => "Weapon4", 2 => "Expert2", _ => "Weapon1" },
                VoyagePart.RootNode => "Core11",
                VoyagePart.Geometry => "Material12",
                VoyagePart.Sail => "Expert14",
                VoyagePart.Gate => "Expert15",
                VoyagePart.Asteroid => "Material2",
                VoyagePart.Drone => "Expert4",
                VoyagePart.SeedFlower => "Core11",
                _ => "Core" + boss.BossIndex
            };
        }
    }
    private string SegmentSection(VoyageBossNPC boss)
    {
        bool split = boss.BossIndex == 2 && (boss.BrokenMask & 0x7e) != 0;
        int first = split && Slot >= 4 ? 4 : 0;
        int last = split && Slot < 4 ? 3 : 8;
        bool before = false, after = false;
        for (int i = first; i < Slot; i++) if (boss.Part(i, VoyagePart.Segment) != null) { before = true; break; }
        for (int i = Slot + 1; i <= last; i++) if (boss.Part(i, VoyagePart.Segment) != null) { after = true; break; }
        return !before ? "Head" : !after ? "Tail" : "Body";
    }
    public override bool PreDraw(SpriteBatch batch, Vector2 screen, Color color)
    {
        VoyageBossNPC boss = Boss;
        if (boss == null) return false;
        Texture2D texture = ModContent.Request<Texture2D>(VoyageCatalog.Root + ArtKey).Value;
        Vector2 center = NPC.Center - screen;
        Color tint = Role == VoyagePart.RedEye ? new Color(255, 120, 110) : Role == VoyagePart.BlueEye ? new Color(130, 195, 255) : boss.Tint;
        if (!Temporary && Role is not VoyagePart.Segment and not VoyagePart.RedEye and not VoyagePart.BlueEye and not VoyagePart.LeftCity and not VoyagePart.RightCity and not VoyagePart.Array)
            CombatDrawing.Line(batch, boss.NPC.Center - screen, center, tint * .28f, Role == VoyagePart.Anchor ? 5 : 2);
        if (Role is VoyagePart.LeftCity or VoyagePart.RightCity)
        {
            // An articulated light bridge visually joins each city-bearing hand to the actual torso.
            float side = Role == VoyagePart.LeftCity ? -1 : 1;
            Vector2 shoulder = boss.NPC.Center + new Vector2(side * 100, -40) - screen;
            Vector2 elbow = Vector2.Lerp(shoulder, center, .58f) + new Vector2(0, 60);
            CombatDrawing.Line(batch, shoulder, elbow, new Color(66, 58, 43), 14);
            CombatDrawing.Line(batch, elbow, center + new Vector2(-side * 42, 10), new Color(66, 58, 43), 12);
            CombatDrawing.Line(batch, shoulder, elbow, tint * .6f, 4);
            CombatDrawing.Line(batch, elbow, center + new Vector2(-side * 42, 10), tint * .6f, 4);
            CombatDrawing.Circle(batch, elbow, 10, tint * .75f, 3);
        }
        DrawDeviceStructure(batch, center, tint, boss);
        if (Role == VoyagePart.Gate)
        {
            CombatDrawing.Circle(batch, center, 48, tint * .75f, 5);
            CombatDrawing.Circle(batch, center, 34, tint * .4f, 3, Age * .01f, .5f);
        }
        if (Role == VoyagePart.Cargo)
        {
            Vector2 a = center - new Vector2(NPC.width * .6f);
            float w = NPC.width * 1.2f;
            CombatDrawing.Line(batch, a, a + new Vector2(w, 0), tint * .7f, 3);
            CombatDrawing.Line(batch, a + new Vector2(w, 0), a + new Vector2(w, w), tint * .7f, 3);
            CombatDrawing.Line(batch, a + new Vector2(w, w), a + new Vector2(0, w), tint * .7f, 3);
            CombatDrawing.Line(batch, a + new Vector2(0, w), a, tint * .7f, 3);
        }
        if (Role == VoyagePart.Sail)
        {
            for (int i = -3; i <= 3; i++) CombatDrawing.Line(batch, center, center + new Vector2(i * 23, -75 + Math.Abs(i) * 6), tint * .45f, 8);
        }
        float scale = (float)NPC.width / Math.Max(texture.Width, texture.Height);
        float rotation = NPC.rotation;
        SpriteEffects effect = SpriteEffects.None;
        if (Role is VoyagePart.RedEye or VoyagePart.BlueEye)
        {
            // Source crop: red naturally faces right; blue naturally faces left.
            bool faceRight = boss.Aim.X >= NPC.Center.X;
            if (Role == VoyagePart.RedEye && !faceRight || Role == VoyagePart.BlueEye && faceRight) effect = SpriteEffects.FlipHorizontally;
            rotation = MathHelper.Clamp(NPC.velocity.Y * .008f, -.14f, .14f);
        }
        if (Role == VoyagePart.Bulkhead || Role == VoyagePart.Weapon && Slot is 0 or 3)
            rotation = (boss.Aim - NPC.Center).ToRotation();
        if (Role == VoyagePart.Weapon && Slot == 1) rotation = Age * .04f;
        if (Role == VoyagePart.Anchor) scale *= .72f;
        if (Role == VoyagePart.Sail) scale *= 1.35f;
        Color spriteColor = Color.Lerp(color, tint, BudgetPart ? .08f : .16f) * (NPC.dontTakeDamage ? .55f : 1);
        batch.Draw(texture, center, null, spriteColor, rotation, texture.Size() * .5f, scale, effect, 0);
        if (!BudgetPart || NPC.life < NPC.lifeMax)
        {
            Vector2 bar = center + new Vector2(-30, -NPC.height * .65f);
            CombatDrawing.Line(batch, bar, bar + new Vector2(60, 0), Color.Black * .7f, 5);
            CombatDrawing.Line(batch, bar, bar + new Vector2(60f * NPC.life / NPC.lifeMax, 0), tint, 3);
        }
        return false;
    }
    private void DrawDeviceStructure(SpriteBatch batch, Vector2 center, Color tint, VoyageBossNPC boss)
    {
        if (Role == VoyagePart.Hangar)
        {
            // Six-sided docking frame with three dark, distinct launch bays.
            for (int i = 0; i < 6; i++)
                CombatDrawing.Line(batch, center + (i * MathHelper.TwoPi / 6).ToRotationVector2() * 44,
                    center + ((i + 1) * MathHelper.TwoPi / 6).ToRotationVector2() * 44, tint * .75f, 5);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 bay = center + new Vector2(i * 23, 35);
                CombatDrawing.Line(batch, bay - new Vector2(8, 0), bay + new Vector2(8, 0), new Color(20, 25, 36), 15);
                CombatDrawing.Line(batch, bay - new Vector2(8, -9), bay + new Vector2(8, 9), tint * .6f, 2);
            }
        }
        if (Role == VoyagePart.Anchor)
        {
            Vector2 top = center - new Vector2(0, 35), stem = center + new Vector2(0, 35);
            CombatDrawing.Line(batch, top, stem, new Color(43, 58, 70), 12);
            CombatDrawing.Line(batch, top, stem, tint * .8f, 5);
            CombatDrawing.Circle(batch, top, 11, tint * .85f, 4);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 hook = center + new Vector2(side * 35, 15);
                CombatDrawing.Line(batch, stem, hook, tint * .8f, 7);
                CombatDrawing.Line(batch, hook, hook - new Vector2(side * 3, 20), tint * .8f, 7);
            }
        }
        if (Role == VoyagePart.NeuralNode)
            for (int i = 0; i < 3; i++)
            {
                float angle = Age * .02f + i * MathHelper.TwoPi / 3;
                CombatDrawing.Line(batch, center, center + angle.ToRotationVector2() * 40, tint * .55f, 2);
                CombatDrawing.Circle(batch, center + angle.ToRotationVector2() * 40, 4, tint * .85f, 3);
            }
        if (Role == VoyagePart.Weapon && Slot == 2)
        {
            // The magnetic arm reads as a split pincer rather than another gun or square block.
            Vector2 direction = (boss.Aim - NPC.Center).SafeNormalize(Vector2.UnitX);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 prong = center + direction.RotatedBy(side * .6f) * 40;
                CombatDrawing.Line(batch, center - direction * 24, prong, tint * .75f, 7);
                CombatDrawing.Line(batch, prong, center + direction * 54 + direction.RotatedBy(MathHelper.PiOver2) * side * 13, tint * .85f, 5);
            }
        }
        if (Role == VoyagePart.RootNode)
            for (int i = -2; i <= 2; i++)
                CombatDrawing.Line(batch, center + new Vector2(i * 6, 15), center + new Vector2(i * 20, 45 - Math.Abs(i) * 5), tint * .5f, 4);
        if (Role == VoyagePart.SeedFlower && Age > 55)
            for (int i = 0; i < 6; i++)
            {
                Vector2 petal = center + (i * MathHelper.TwoPi / 6 + Age * .003f).ToRotationVector2() * 32;
                CombatDrawing.Line(batch, center, petal, new Color(120, 190, 105) * .7f, 13);
                CombatDrawing.Circle(batch, petal, 8, tint * .7f, 4);
            }
        if (Role == VoyagePart.Mirror) CombatDrawing.Circle(batch, center, 44, tint * .35f, 2, Age * .012f, .55f);
        if (Role == VoyagePart.Geometry)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = center + (i * MathHelper.PiOver2 + MathHelper.PiOver4).ToRotationVector2() * 46;
                CombatDrawing.Line(batch, p, p + (i * MathHelper.PiOver2).ToRotationVector2() * 13, tint * .7f, 4);
            }
        }
        if (Role == VoyagePart.Drone)
        {
            float pulse = 8 + MathF.Sin(Age * .5f) * 3;
            CombatDrawing.Line(batch, center + new Vector2(-12, 10), center + new Vector2(-12, 10 + pulse), tint * .5f, 5);
            CombatDrawing.Line(batch, center + new Vector2(12, 10), center + new Vector2(12, 10 + pulse), tint * .5f, 5);
        }
    }
}
