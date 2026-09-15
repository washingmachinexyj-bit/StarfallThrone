#nullable enable
using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Oaths.Bosses;

public abstract partial class SupremeBossNPC
{
    public void SpawnObjectives()
    {
        if (!Authority || Index == 2 || Index == 0 && Phase != 1 || Index == 1 && Phase < 2) return;
        int count = Index == 0 ? 3 : 1;
        for (int j = 0; j < count; j++)
        {
            int mark = Index == 0 ? j : Round % 6;
            Vector2 at = Point(Arena + new Vector2((j - (count - 1) / 2f) * 260, -100));
            int slot = NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y, ModContent.NPCType<SupremeNode>(), ai0: NPC.whoAmI + 1, ai1: mark);
            if (slot >= 0 && slot < Main.maxNPCs && Main.npc[slot].ModNPC is SupremeNode node)
            { node.Setup(this, at); node.NPC.netUpdate = true; }
        }
    }
    public SupremeHazard? Hazard(SupremeShape shape, Vector2 from, Vector2 velocity, int damage, int delay = 75, int duration = 100,
        float width = 14, float length = 700, float angle = 0, float gap = 1.05f, int mark = -1)
    {
        if (!Authority || !Ready || Cancelled || IsBroken(mark)) return null;
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is SupremeHazard existing && existing.OwnedBy(this)) count++;
        if (count >= 72) return null;
        from = Point(from);
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), from, velocity, ModContent.ProjectileType<SupremeHazard>(),
            EngineDamage(damage, Difficulty), 0, Main.myPlayer, NPC.whoAmI + 1, 0, (float)shape);
        if (slot < 0 || slot >= Main.maxProjectiles || Main.projectile[slot].ModProjectile is not SupremeHazard h) return null;
        h.Setup(this, delay, duration, width, length, angle, gap, mark); return h;
    }
    private float Toward(Vector2 at, Vector2 target) => (target - at).SafeNormalize(Vector2.UnitY).ToRotation();
    private void Ring(Vector2 at, Player target, int mark = -1, int damage = 2500, int delay = 90, float length = 780)
        => Hazard(SupremeShape.Ring, at, Vector2.Zero, damage, delay, 140, 15, length, Toward(at, target.Center), 1.15f, mark);
    private void Beam(Vector2 a, Vector2 b, int damage, int delay = 90, int duration = 26, int mark = -1)
        => Hazard(SupremeShape.Beam, a, Vector2.Zero, damage, delay, duration, 18, Vector2.Distance(a, b), Toward(a, b), mark: mark);
    private void Roots(Player player, int mark)
    {
        float x = player.Center.X;
        // A 200px central passage remains; a stationary target is not enclosed on both axes.
        for (int j = -2; j <= 2; j++) if (j != 0)
            Beam(new Vector2(x + j * 160, Arena.Y + 340), new Vector2(x + j * 160, Arena.Y - 380), 2600, 90, 30, mark);
    }
    private void ReturnDrops(Player player, int mark)
    {
        float gap = Toward(Arena, player.Center);
        for (int j = 0; j < 8; j++)
        {
            float a = gap + MathHelper.TwoPi * j / 8;
            if (MathF.Abs(MathHelper.WrapAngle(a - gap)) < .7f) continue;
            Vector2 at = Arena + a.ToRotationVector2() * 700;
            Hazard(SupremeShape.Star, at, (Arena - at).SafeNormalize(Vector2.UnitY) * 6, 2400, 90, 105, 20, mark: mark);
        }
    }
    private void Echo(Player player, int damage = 3300)
    {
        // A finite three-second server history is frozen into at most six telegraphed segments.
        for (int k = 0; k < 6; k++)
        {
            Vector2 a = History[179 - k * 25], b = History[154 - k * 25];
            if (Vector2.DistanceSquared(a, b) > 1600) Beam(a, b, damage, 100 + k * 8, 22);
        }
        Hazard(SupremeShape.Pulse, Point(player.Center), Vector2.Zero, damage, 110, 28, 22, 100);
    }
    private void Curtain(float side, float gapY, int damage, int delay = 100)
        => Hazard(SupremeShape.Wall, Arena + new Vector2(side * 700, 0), new Vector2(-side * 5, 0), damage,
            delay, 230, 18, 650, gapY, 220);
    public void Perform(Player player)
    {
        if (Index == 0) Miroen(player);
        else if (Index == 1) Velsa(player);
        else NoDay(player);
    }
    private void Miroen(Player player)
    {
        int t = Timer;
        if (Phase == 0)
        {
            if (t == 15 || t == 210) Ring(Point(Arena + new Vector2(t == 15 ? -180 : 180, -60)), player, damage: 2200);
            if (t == 80) Roots(player, -1);
            if (t == 260) ReturnDrops(player, -1);
        }
        else if (Phase == 1)
        {
            if (t == 15 || t == 205) Ring(Arena - new Vector2(260, 100), player, 0, 2500);
            if (t == 100 || t == 290) Roots(player, 1);
            if (t == 180) ReturnDrops(player, 2);
        }
        else if (Phase == 2)
        {
            if (t == 15) Curtain(Round % 2 == 0 ? 1 : -1, (Round % 3 - 1) * 150, 2800);
            if (t == 155 || t == 290) Ring(Arena, player, damage: 2700, delay: 95, length: 680);
        }
        else
        {
            // The First Rain: one committed drop, then three separated, visibly gapped waves.
            if (t == 15)
                Hazard(SupremeShape.Star, Arena - new Vector2(0, 500), Vector2.UnitY * 5, 3600, 120, 100, 44);
            if (t == 180 || t == 240 || t == 300) Ring(Arena, player, damage: 3200, delay: 90, length: 780);
        }
    }
    private void Velsa(Player player)
    {
        int t = Timer;
        if (Phase == 0)
        {
            if (t == 15 || t == 180)
            {
                Aim = Point(player.Center); NPC.netUpdate = true;
                for (int j = -1; j <= 1; j++)
                {
                    Vector2 a = Arena + new Vector2(j * 260, -330);
                    Hazard(SupremeShape.Star, a, (Aim - a).SafeNormalize(Vector2.UnitY) * 5, 2600, 75, 95, 18);
                    Beam(Aim + new Vector2(j * 180, -300), Aim + new Vector2(j * 180 + 130, 300), 2900, 190, 24);
                }
            }
            if (t == 290) Ring(Arena, player, damage: 2900, delay: 95, length: 740);
        }
        else if (Phase == 1)
        {
            // Exactly one law per assault; recovery separates contradictory movement demands.
            switch (Round % 4)
            {
                case 0: // Ash: staggered sweeps and long pauses.
                    if (t == 30 || t == 200)
                        for (int j = 0; j < 3; j++) Beam(Arena + new Vector2(-650, -200 + j * 190), Arena + new Vector2(650, -200 + j * 190), 2900, 95 + j * 24, 18);
                    break;
                case 1: // Blaze: old positions ignite; keep moving.
                    if (t >= 30 && t <= 300 && t % 90 == 30)
                        Hazard(SupremeShape.Pulse, player.Center, Vector2.Zero, 3100, 90, 55, 20, 150);
                    break;
                case 2: // Fracture: alternating diagonal lanes.
                    if (t == 30 || t == 190)
                        for (int j = -1; j <= 1; j++) Beam(Arena + new Vector2(-600, j * 220 - 180), Arena + new Vector2(600, j * 220 + 180), 3100, 100, 35);
                    break;
                case 3: // Ember: save movement for a marked moving passage.
                    if (t == 30) Curtain(Round % 8 < 4 ? -1 : 1, (Round % 3 - 1) * 140, 3300, 120);
                    break;
            }
        }
        else
        {
            int mark = Round % 6;
            if (IsBroken(mark)) return; // A broken lit wing interrupts the complete attack, not only existing shots.
            if (Phase == 2)
            {
                if (t == 45 || t == 205)
                    for (int j = 0; j < 5; j++)
                    {
                        float a = Toward(NPC.Center, player.Center) + (j - 2) * .30f;
                        Hazard(SupremeShape.Star, NPC.Center, a.ToRotationVector2() * 7, 3300, 100, 120, 20, mark: mark);
                    }
                if (t == 260) Ring(Arena, player, mark, 3700);
            }
            else
            {
                // Three successive walls, never opposite closing walls at the same instant.
                if (t == 20 || t == 125 || t == 230)
                {
                    Hazard(SupremeShape.Wall, Arena + new Vector2(-650, 0), new Vector2(7, 0), 4300,
                        105, 190, 20, 650, (t == 125 ? 1 : -1) * 110, 250, mark);
                }
            }
        }
    }
    private void NoDay(Player player)
    {
        int t = Timer;
        if (Phase == 0)
        {
            if (t == 15 || t == 215) Ring(Point(Arena + new Vector2((t == 15 ? -1 : 1) * 220, -80)), player, damage: 3000);
            if (t == 80)
                for (int j = -2; j <= 2; j++)
                    Beam(Arena + new Vector2(j * 210, -500), Arena + new Vector2(j * 210, 500), 3300,
                        90 + (j & 1) * 75, 26, j & 1);
        }
        else if (Phase == 1)
        {
            if (t == 30 || t == 240) Echo(player);
            if (t == 145) Ring(Arena, player, damage: 3400, delay: 100, length: 630);
        }
        else if (Phase == 2)
        {
            if (Round % 3 == 0)
            {
                if (t == 30 || t == 230) Ring(Arena, player, damage: 3700);
                if (t == 140) Hazard(SupremeShape.Pulse, History[100], Vector2.Zero, 3000, 100, 25, 20, 90);
            }
            else if (Round % 3 == 1)
            {
                if (t == 30) Curtain(1, 0, 3800, 110);
                if (t == 250) Echo(player, 3100);
            }
            else
            {
                if (t == 30 || t == 240) Echo(player, 3800);
                if (t == 150) Ring(Arena, player, damage: 3100, delay: 100, length: 550);
            }
        }
        else
        {
            // Three discrete trials. An extra recovery follows all three; no scripted execution.
            if (t == 15 || t == 75) Ring(Arena, player, damage: 4200, delay: 90, length: 550);
            if (t == 245) Echo(player, 4600);
            if (t == 440) Ring(Arena, player, damage: 5000, delay: 90, length: -650);
        }
    }
}
