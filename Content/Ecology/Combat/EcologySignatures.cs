using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Combat;

public abstract partial class EcologyBossNPC
{
    public EcologyHazard Emit(EcologyShape shape, Vector2 point, Vector2 velocity, int delay = 36, int duration = 110, float width = 16, Vector2? end = null, bool heavy = false, float turn = 0)
    {
        if (!Authority) return null;
        int count = 0; foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is EcologyHazard h && h.OwnedBy(this)) count++;
        if (count >= 64) return null;
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), WorldPoint(point), velocity, ModContent.ProjectileType<EcologyHazard>(), EngineDamage(heavy), 0, Main.myPlayer, NPC.whoAmI + 1, 0, (int)shape);
        if (slot < 0 || slot >= Main.maxProjectiles || Main.projectile[slot].ModProjectile is not EcologyHazard shot) return null;
        shot.Setup(NPC, Serial, Index, delay, duration, width, end ?? point + new Vector2(350, 0), turn); return shot;
    }
    public void Fan(Vector2 from, Vector2 to, int count, float spread, float speed, int delay = 36, bool heavy = false)
    {
        for (int i = 0; i < count; i++)
        { float a = count == 1 ? 0 : (i / (float)(count - 1) - .5f) * spread; Emit(EcologyShape.Shard, from, (to - from).SafeNormalize(Vector2.UnitY).RotatedBy(a) * speed, delay, 100, 13, heavy: heavy); }
    }
    public EcologyPart Part(int kind, Vector2 point, Vector2 offset, int duration = 210, Vector2? exit = null)
    {
        if (!Authority) return null;
        int count = 0; foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is EcologyPart p && p.OwnedBy(this)) count++;
        if (count >= 10) return null;
        int slot = NPC.NewNPC(NPC.GetSource_FromAI(), (int)point.X, (int)point.Y, ModContent.NPCType<EcologyPart>(), ai0: NPC.whoAmI + 1, ai1: kind);
        if (slot < 0 || slot >= Main.maxNPCs || Main.npc[slot].ModNPC is not EcologyPart part) return null;
        part.NPC.Center = Bound(point); part.Setup(this, kind, offset, duration, exit ?? point);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: slot);
        return part;
    }
    private void Beam(Vector2 from, Vector2 to, int delay = 45, int duration = 28, int width = 16, bool heavy = false)
        => Emit(EcologyShape.Beam, from, Vector2.Zero, delay, duration, width, to, heavy);
    private void Ring(Vector2 at, int delay = 35, int duration = 100, int radius = 450, bool heavy = false)
        => Emit(EcologyShape.Ring, at, Vector2.Zero, delay, duration, 12, at + new Vector2(radius, 0), heavy);
    private void RunSignature(Player player)
    {
        switch (Index)
        {
            case 0: Mistmirror(player); break;
            case 1: Waxsleep(player); break;
            case 2: Amberhive(player); break;
            case 3: Knellbone(player); break;
            case 4: Backburn(player); break;
            case 5: Wildmachine(player); break;
            case 6: Hungerbloom(player); break;
            case 7: SunkenSun(player); break;
            case 8: RainbowTide(player); break;
            case 9: Formless(player); break;
        }
    }
    private void Mistmirror(Player player)
    {
        switch (Move)
        {
            case 0:
                if (Timer == 1) Dash(Aim, Phase == 0 ? 12 : 16);
                if (Timer > 28) NPC.velocity *= .94f;
                if (Phase > 0 && Timer == 75 && Authority) { Mark = Bound(player.Center); Beam(NPC.Center, Mark, 35, 14, 8); NPC.netUpdate = true; }
                if (Phase > 0 && Timer == 110) Dash(Mark, 17);
                break;
            case 1:
                Fly(Bound(Aim + new Vector2(180, -120)), 6);
                // Crosshatches are committed to the old position, never track the player during detonation.
                if (Timer is 1 or 55 or 110)
                {
                    float shift = (Timer / 55 - 1) * 85;
                    Beam(Aim + new Vector2(-220, shift - 90), Aim + new Vector2(220, shift + 90), 48, 22, 12, true);
                    if (Phase > 0) Beam(Aim + new Vector2(-220, shift + 90), Aim + new Vector2(220, shift - 90), 63, 20, 10);
                }
                break;
            case 2:
                NPC.velocity *= .92f;
                if (Timer % 35 == 1)
                    for (int i = -2; i <= 2; i++)
                        if (i != (Timer / 35 % 3 - 1)) Emit(EcologyShape.Shard, Bound(Aim + new Vector2(i * 80, -230)), new Vector2(Phase * .6f, 6), 38, 75, 11);
                break;
            case 3:
                if (Timer == 1 && Authority)
                {
                    Mark = Bound(Aim + new Vector2(Phase == 0 ? 200 : -200, -100));
                    Beam(NPC.Center, Mark, 45, 10, 6); NPC.netUpdate = true;
                }
                if (Timer == 48 && Authority) { NPC.Center = Mark; NPC.netUpdate = true; Expose(50); }
                if (Timer == 95) Fan(NPC.Center, Aim, 5 + Phase * 2, 1.3f, 6, 36);
                break;
        }
    }
    private void Waxsleep(Player player)
    {
        NPC.velocity *= .92f;
        switch (Move)
        {
            case 0:
                if (Timer == 1 && Authority)
                { Shell = Math.Min(Shell, NPC.lifeMax / (Phase == 0 ? 8 : 16)); NPC.netUpdate = true; }
                if (Timer is 15 or 80 or 145) Fan(NPC.Center, Aim, 4 + Phase, 1.5f, 4.5f, 45);
                break;
            case 1:
                if (Timer is 1 or 90)
                    for (int i = -3; i <= 3; i++) if (i != (Timer == 1 ? -1 : 1))
                    {
                        Vector2 point = Bound(Aim + new Vector2(i * 80, 80));
                        Emit(EcologyShape.Pillar, point, Vector2.Zero, 55, 28 + Phase * 10, 24, point - new Vector2(0, 170));
                    }
                break;
            case 2:
                if (Timer < 35) Fly(Bound(Aim - new Vector2(0, 190)), 12);
                if (Timer == 36) Dash(Aim + new Vector2(0, 70), 15);
                if (Timer == 58) { NPC.velocity *= .1f; Ring(NPC.Center, 35, 90, 330, true); Expose(75); }
                break;
            case 3:
                Fly(Bound(Aim + new Vector2(MathF.Sin(Timer * .025f) * 180, -180)), 7);
                if (Timer % 45 == 1)
                { Emit(EcologyShape.Bubble, NPC.Center, new Vector2(-3, 2), 35, 110, 19); Emit(EcologyShape.Bubble, NPC.Center, new Vector2(3, 2), 35, 110, 19); }
                break;
        }
    }
    private void Amberhive(Player player)
    {
        switch (Move)
        {
            case 0:
                NPC.velocity *= .9f;
                if (Timer == 1)
                {
                    Part(0, NPC.Center + new Vector2(0, -65), new Vector2(0, -65), 170);
                    for (int i = 0; i < (Phase == 0 ? 3 : 2); i++) Part(9, NPC.Center + new Vector2((i - 1) * 50, 40), Vector2.Zero, 120);
                }
                if (Timer == 110) Fan(NPC.Center, Aim, 5, .9f, 7, 40);
                break;
            case 1:
                Fly(Bound(Aim + new Vector2(-160, -120)), 7);
                if (Timer is 1 or 75) for (int i = -1; i <= 1; i++) Part(5, Bound(Aim + new Vector2(i * 110, 30)), Vector2.Zero, 65);
                break;
            case 2:
                NPC.velocity *= .87f;
                if (Timer is 1 or 80)
                {
                    int side = Timer == 1 ? -1 : 1;
                    Vector2 edge = NPC.Center + new Vector2(side * 210, 50);
                    Beam(edge, NPC.Center + new Vector2(-side * 70, -100), 50, 20, 30, true);
                }
                break;
            case 3:
                if (Timer == 1) Dash(Aim, 12 + Phase * 4);
                if (Timer > 30) NPC.velocity *= .9f;
                if (Timer is 55 or 125) Fan(NPC.Center, Aim, 5 + Phase * 2, 1.5f, 6, 35);
                break;
        }
    }
    private void Knellbone(Player player)
    {
        NPC.velocity *= .92f;
        switch (Move)
        {
            case 0:
                if (Timer == 1) Ring(Aim + new Vector2(0, 90), 40, 120, 440);
                if (Timer == 65) for (int i = -2; i <= 2; i++) if (i != 0) Emit(EcologyShape.Shard, Bound(Aim + new Vector2(i * 85, -220)), new Vector2(0, 6), 45, 90, 12);
                if (Timer == 130) Beam(Aim - new Vector2(300, 0), Aim + new Vector2(300, 0), 45, 25, 18);
                break;
            case 1:
                if (Timer is 1 or 95)
                {
                    float slope = Timer == 1 ? -1 : 1;
                    Beam(NPC.Center + new Vector2(-230, -80 * slope), NPC.Center + new Vector2(230, 80 * slope), 65 - Phase * 10, 25, 35, true);
                }
                break;
            case 2:
                if (Timer is 1 or 85)
                {
                    int side = Timer == 1 ? -1 : 1;
                    for (int i = 0; i < 3 + Phase; i++) Emit(EcologyShape.Wave, Bound(Aim + new Vector2(side * 300, (i - 1) * 80)), new Vector2(-side * 6, 0), 50 + i * 6, 100, 18);
                }
                break;
            case 3:
                if (Timer % 60 == 1) { Ring(NPC.Center, 38, 100, 400); Expose(45); }
                break;
        }
    }
    private void Backburn(Player player)
    {
        switch (Move)
        {
            case 0:
                if (Timer == 1) { Dash(Aim, 10 + Phase * 3); Part(1, NPC.Center + new Vector2(0, -65), new Vector2(0, -65), 170); }
                if (Timer > 36) NPC.velocity *= .9f;
                if (Timer == 65) for (int side = -1; side <= 1; side += 2) Emit(EcologyShape.Wave, NPC.Center + new Vector2(0, 50), new Vector2(side * 7, 0), 40, 85, 20);
                break;
            case 1:
                Expose(2); Fly(Bound(Aim + new Vector2(200, -160)), 12);
                if (Timer % 45 == 1)
                    for (int i = -2; i <= 2; i++) if (i != Timer / 45 % 3 - 1)
                    { Vector2 p = Bound(Aim + new Vector2(i * 95, 120)); Emit(EcologyShape.Pillar, p, Vector2.Zero, 45, 30, 26, p - new Vector2(0, 300)); }
                break;
            case 2:
                Expose(2); NPC.velocity *= .85f;
                if (Timer is 1 or 80)
                    for (int i = -1; i <= 1; i++) Ring(Bound(Aim + new Vector2(i * 170, 60)), 60, 65, 160 + Phase * 30, true);
                break;
            case 3:
                Fly(Bound(Aim + new Vector2(-180, -80)), 5);
                if (Timer == 1) Part(1, NPC.Center + new Vector2(0, -60), new Vector2(0, -60), 160);
                if (Timer % 60 == 1) Fan(NPC.Center, Aim, 3 + Phase * 2, 1.2f, 5, 45);
                break;
        }
    }
    private void Wildmachine(Player player)
    {
        switch (Move)
        {
            case 0:
                if (Timer == 1) Dash(new Vector2(Aim.X, NPC.Center.Y), 10 + Phase * 2);
                if (Timer > 45) NPC.velocity *= .92f;
                if (Timer is 50 or 110 or 170) for (int s = -1; s <= 1; s += 2) Emit(EcologyShape.Wave, NPC.Center + new Vector2(0, 65), new Vector2(s * 8, 0), 36, 90, 16);
                break;
            case 1:
                NPC.velocity *= .9f;
                if (Timer == 1) { Part(6, Bound(Aim + new Vector2(-180, -100)), Vector2.Zero, 210); Part(6, Bound(Aim + new Vector2(180, 100)), Vector2.Zero, 210); }
                if (Timer % 50 == 10) Fan(NPC.Center, Aim, 5 + Phase * 2, 1.7f, 6, 40);
                break;
            case 2:
                NPC.velocity *= .88f;
                if (Timer == 1) { Part(2, NPC.Center + new Vector2(-80, -30), new Vector2(-80, -30), 145); Part(2, NPC.Center + new Vector2(80, -30), new Vector2(80, -30), 145); }
                if (Timer == 45) { Beam(Aim + new Vector2(-250, -140), Aim + new Vector2(250, -140), 50, 50, 20, true); Beam(Aim + new Vector2(-250, 140), Aim + new Vector2(250, 140), 65, 35, 20, true); }
                break;
            case 3:
                Expose(2); Fly(Bound(Aim - new Vector2(0, 200)), 12);
                if (Timer % 60 == 1) Fan(NPC.Center, Aim, 7, 2.1f, 7 + Phase, 40);
                break;
        }
    }
    private void Hungerbloom(Player player)
    {
        NPC.velocity *= .9f;
        switch (Move)
        {
            case 0:
                if (Timer == 1) for (int i = -1; i <= 1; i++) Part(3, NPC.Center + new Vector2(i * 85, -45), new Vector2(i * 85, -45), 200);
                if (PartsBroken >= 2) { Expose(2); Fly(Bound(Aim + new Vector2(180, -80)), 9); }
                break;
            case 1:
                if (Timer == 1)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p = Bound(Aim + new Vector2(side * 180, 0));
                        EcologyPart root = Part(7, p, Vector2.Zero, 170);
                        EcologyHazard wall = Emit(EcologyShape.Beam, p - new Vector2(0, 100), Vector2.Zero, 65, 100, 18, p + new Vector2(0, 100), true);
                        if (wall != null && root != null) { wall.PartSlot = root.NPC.whoAmI; wall.Projectile.netUpdate = true; }
                    }
                if (Timer == 90) Fan(NPC.Center, Aim, 3, .8f, 6, 50);
                break;
            case 2:
                if (Timer is 1 or 90) for (int i = -1; i <= 1; i++) Part(5, Bound(Aim + new Vector2(i * 110, 30)), Vector2.Zero, 70);
                break;
            case 3:
                Expose(2); Fly(Bound(Aim + new Vector2(MathF.Sin(Timer * .02f) * 220, -100)), 10);
                if (Timer % 60 == 1) Fan(NPC.Center, Aim, 5 + Phase * 2, 1.6f, 6, 38);
                break;
        }
    }
    private void SunkenSun(Player player)
    {
        switch (Move)
        {
            case 0:
                NPC.velocity *= .9f;
                if (Timer == 1)
                {
                    Vector2 a = Bound(Aim + new Vector2(-230, 100)), b = Bound(Aim + new Vector2(230, 100)), c = Bound(Aim - new Vector2(0, 180));
                    Beam(a, b, 65, 25, 18); Beam(b, c, 85, 25, 18); Beam(c, a, 105, 25, 18);
                }
                break;
            case 1:
                if (Timer == 1) Dash(Aim, 15 + Phase * 2);
                if (Timer > 30) NPC.velocity *= .9f;
                if (Timer == 80) { Expose(90); Part(4, NPC.Center + new Vector2(0, -60), new Vector2(0, -60), 100); }
                break;
            case 2:
                Fly(Bound(Aim + new Vector2(160, -180)), 9);
                if (Timer % 50 == 1) for (int i = -1; i <= 1; i++) Emit(EcologyShape.Bubble, Bound(Aim + new Vector2(i * 150, -250)), new Vector2(i * .5f, 3), 45, 120, 26, heavy: true);
                break;
            case 3:
                Expose(2); NPC.velocity *= .86f;
                if (Timer % 65 == 1) Fan(NPC.Center, Aim, Phase == 0 ? 5 : 7, 1.9f, 8, 42);
                break;
        }
    }
    private void RainbowTide(Player player)
    {
        switch (Move)
        {
            case 0:
                Fly(Bound(Aim + new Vector2(MathF.Cos(Timer * .022f) * 260, MathF.Sin(Timer * .022f) * 140 - 50)), 13 + Phase * 2, .18f);
                if (Timer % 50 == 1) Emit(EcologyShape.Bubble, NPC.Center, (Aim - NPC.Center).SafeNormalize(Vector2.UnitY) * 3, 45, 130, 22);
                break;
            case 1:
                Fly(Bound(Aim - new Vector2(0, 200)), 8);
                if (Timer is 1 or 100)
                    for (int i = -3; i <= 3; i++) if (i != (Timer == 1 ? -1 : 1))
                    { Vector2 dir = Vector2.UnitY.RotatedBy(i * .25f); Beam(NPC.Center, NPC.Center + dir * 480, 60, 30, 14); }
                break;
            case 2:
                NPC.velocity *= .9f;
                if (Timer is 1 or 110)
                {
                    Vector2 prism = Bound(Aim + new Vector2(Timer == 1 ? -150 : 150, -50));
                    // A stationary bubble makes both segments visible for 65 ticks before refraction fires.
                    Emit(EcologyShape.Bubble, prism, Vector2.Zero, 65, 35, 24);
                    Beam(NPC.Center, prism, 65, 25, 16);
                    Vector2 bend = (Aim - prism).SafeNormalize(Vector2.UnitY).RotatedBy(Phase == 0 ? .2f : -.2f);
                    Beam(prism, prism + bend * 400, 65, 25, 16, true);
                }
                break;
            case 3:
                NPC.velocity *= .88f;
                if (Timer is 1 or 110)
                    for (int i = -2; i <= 2; i++) if (i != (Timer == 1 ? 0 : 1))
                        Emit(EcologyShape.Wave, Bound(Aim + new Vector2(-300, i * 80)), new Vector2(4 + Phase, 0), 50, 140, 24);
                break;
        }
    }
    private void Formless(Player player)
    {
        switch (Move)
        {
            case 0:
                Fly(Bound(Arena + (Timer * .025f).ToRotationVector2() * 180), 15, .18f);
                if (Timer % 55 == 1) Fan(NPC.Center, Aim, 3 + Phase * 2, 1.2f, 8, 45);
                break;
            case 1:
                NPC.velocity *= .9f;
                if (Timer == 1)
                {
                    Mark = Bound(Aim + new Vector2(-180, -40)); Origin = Bound(Aim + new Vector2(180, -40));
                    Part(8, Mark, Vector2.Zero, 210, Origin); Part(8, Origin, Vector2.Zero, 210, Mark);
                    NPC.netUpdate = true;
                }
                if (Timer is 20 or 90 or 160) Fan(NPC.Center, Mark, 3, .18f, 6, 35);
                break;
            case 2:
                NPC.velocity *= .9f;
                if (Timer == 1)
                    for (int i = -1; i <= 1; i += 2)
                    {
                        Vector2 p = Bound(Aim + new Vector2(i * 190, 30)); EcologyPart armor = Part(7, p, Vector2.Zero, 180);
                        EcologyHazard hazard = Emit(EcologyShape.Beam, p - new Vector2(0, 90), Vector2.Zero, 65, 110, 24, p + new Vector2(0, 90));
                        if (armor != null && hazard != null) { hazard.PartSlot = armor.NPC.whoAmI; hazard.Projectile.netUpdate = true; }
                    }
                if (Timer == 100) { Expose(100); Fan(NPC.Center, Aim, 5, 1.4f, 8, 45); }
                break;
            case 3:
                if (Timer < 45) Fly(Bound(Arena - new Vector2(0, 180)), 13);
                if (Timer == 45 && Authority) { Mark = Bound(player.Center); Beam(NPC.Center, Mark, 50, 16, 10); NPC.netUpdate = true; }
                if (Timer == 95) Dash(Mark, 18 + Phase * 2);
                if (Timer > 122) { NPC.velocity *= .9f; Expose(2); }
                if (Timer == 140) Ring(NPC.Center, 45, 95, 380, true);
                break;
        }
    }
}
