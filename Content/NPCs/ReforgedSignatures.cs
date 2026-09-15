using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.NPCs;

public abstract partial class ReforgedBossNPC
{
    private void Emit(HazardShape shape, Vector2 at, Vector2 velocity, int delay = 18, int duration = 100, float size = 0, float length = 600, float turn = 0, float gap = .65f)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        int live = 0;
        foreach (Projectile q in Main.ActiveProjectiles) if (q.ModProjectile is CombatHazard && q.ai[0] == NPC.whoAmI + 1) live++;
        if (live >= (Early ? 28 : 64)) return;
        int engineMultiplier = Main.masterMode ? 6 : Main.expertMode ? 4 : 2;
        float desired = Early ? PrimordialBossData.AttackDamage[EncounterId] * (Main.masterMode ? 3 : Main.expertMode ? 2 : 1) : NPC.damage * .65f;
        int id = Projectile.NewProjectile(NPC.GetSource_FromAI(), at, velocity, ModContent.ProjectileType<CombatHazard>(),
            Math.Max(1, (int)(desired / engineMultiplier)), 0, Main.myPlayer, NPC.whoAmI + 1, EncounterId, (int)shape);
        if (id < Main.maxProjectiles)
            ((CombatHazard)Main.projectile[id].ModProjectile).Setup(delay, duration, size == 0 ? (Early ? 14 : 20) : size, length, turn, gap);
    }
    private Vector2 Direction(Vector2 from) => (Aim - from).SafeNormalize(Vector2.UnitX);
    private void Fan(Vector2 at, int count, float speed, float spread, HazardShape shape = HazardShape.Bolt, int delay = 18)
    {
        float angle = Direction(at).ToRotation();
        for (int i = 0; i < count; i++) Emit(shape, at, (angle + MathHelper.Lerp(-spread, spread, count == 1 ? .5f : i / (float)(count - 1))).ToRotationVector2() * speed, delay);
    }
    private void Arc(int count, float speed, float lift, HazardShape shape = HazardShape.Gravity)
    { for (int i = 0; i < count; i++) Emit(shape, NPC.Center, new Vector2(Side * (speed + i * .7f), -lift + i * .35f)); }
    private void Halo(Vector2 center, float radius, float gapAngle, int delay = 30)
    { Emit(HazardShape.Ring, center, gapAngle.ToRotationVector2(), delay, 100, length: radius, gap: Early ? .8f : .55f); }
    private void Beam(Vector2 at, Vector2 direction, float length, float turn = 0, int delay = 42, int duration = 60, float width = 0)
    { Emit(HazardShape.Beam, at, direction.SafeNormalize(Vector2.UnitX), delay, duration, width == 0 ? (Early ? 12 : 22) : width, length, turn); }
    private Vector2 Floor(Vector2 at)
    {
        int x = (int)at.X / 16, y = (int)at.Y / 16 - 2;
        for (int j = y; j < y + 60; j++) if (WorldGen.InWorld(x, j, 10) && WorldGen.SolidTile(x, j)) return new Vector2(at.X, j * 16);
        return at + new Vector2(0, 48);
    }
    private void Pillars(int count, float spacing, float height, int delay = 42)
    {
        for (int i = 0; i < count; i++)
            Emit(HazardShape.Pillar, Floor(Aim + new Vector2((i - (count - 1) / 2f) * spacing, 0)), Vector2.Zero, delay + i * 9, 22, Early ? 26 : 42, height);
    }
    private void Rain(int count, float spacing, int delay = 36, HazardShape shape = HazardShape.Gravity)
    { for (int i = 0; i < count; i++) Emit(shape, Aim + new Vector2((i - (count - 1) / 2f) * spacing, Early ? -250 : -480), Vector2.UnitY * (Early ? 3.5f : 7), delay + i * 7); }
    private void Cross(Vector2 at, float length, float angle = 0, int delay = 42)
    { for (int i = 0; i < 4; i++) Beam(at, (angle + i * MathHelper.PiOver2).ToRotationVector2(), length, delay: delay, duration: 35); }
    private void Mines(int count, float radius, int delay = 48)
    {
        for (int i = 0; i < count; i++)
        {
            float a = MathHelper.TwoPi * i / count;
            Emit(HazardShape.Mine, Aim + a.ToRotationVector2() * radius, Vector2.Zero, delay + i * 5, 65, Early ? 28 : 44);
        }
    }
    private void Spiral(int count, float speed, float turn)
    { for (int i = 0; i < count; i++) Emit(HazardShape.Spiral, NPC.Center, (MathHelper.TwoPi * i / count).ToRotationVector2() * speed, turn: turn); }

    protected void TickSignature(Player p)
    {
        int m = Move, phase = CombatPhase;
        if (!IsContactMove()) Rest();
        // Each row is a separate encounter script, not a reskinned global fan/auto-dash.
        switch (EncounterId)
        {
            case 0: // Resin: readable ground hop, sticky arcs, short sap rush.
                if (m == 0) { Hop(8.5f, 2.8f); if (T == 52) Pillars(2, 100, 24); }
                else if (m == 1) { if (T == 1 || phase == 2 && T == 48) Arc(3, 2.2f, 5); }
                else { Dash(1.15f, 28); if (T == 40) Emit(HazardShape.Mine, Floor(Origin) - new Vector2(0, 18), Vector2.Zero, 40, 48, 26); }
                break;
            case 1: // Copper: shoulder charge, ricochet shrapnel, vulnerable shell stance.
                if (m == 0) Dash(1.6f, 36);
                else if (m == 1) { if (T == 1) Arc(3 + phase, 3, 4, HazardShape.Bounce); }
                else if (T == 48) { Fan(NPC.Center, 5, 4, .8f, HazardShape.Bounce); }
                break;
            case 2: // Scorpion: short claw lunge, staggered sand columns, two-sided tail pinch.
                if (m == 0) { Dash(1.2f, 18); if (T == 24) Beam(NPC.Center, new Vector2(Side, 0), 180, delay: 32, duration: 15); }
                else if (m == 1 && T == 1) Pillars(3, 112, 90);
                else if (m == 2 && T == 1) { Fan(Aim + new Vector2(-220, -60), 2, 4, .18f, delay: 50); Fan(Aim + new Vector2(220, -60), 2, 4, .18f, delay: 68); }
                break;
            case 3: // Frost: committed leap, broad short breath, alternating low ice ridges.
                if (m == 0) Hop(9.5f, 4.2f);
                else if (m == 1 && (T == 1 || T == 30)) Fan(NPC.Center, 5, 4.2f, .5f, HazardShape.Wave);
                else if (m == 2 && T == 1) Pillars(4, 100, 48, 48);
                break;
            case 4: // Mire: heavy frog leap, straight tongue, slow bubble field.
                if (m == 0) { Hop(11, 3.6f); if (T == 68) Halo(NPC.Center, 200, -MathHelper.PiOver2); }
                else if (m == 1 && T == 1) Beam(NPC.Center, Direction(NPC.Center), 280, delay: 45, duration: 20, width: 16);
                else if (m == 2 && T % 32 == 1) Fan(NPC.Center, 3, 2.5f, .7f, HazardShape.Wave, 26);
                break;
            case 5: // Wax: joust, diagonal wax cross, delayed honeycomb seals.
                if (m == 0) Dash(1.7f, 34);
                else if (m == 1 && T == 1) Cross(Aim, 240, MathHelper.PiOver4, 55);
                else if (m == 2 && T == 1) Mines(4, 125, 55);
                break;
            case 6: // Weaver: lash, root maze with walkable gaps, descending pods.
                if (m == 0) { Dash(.85f, 20); if (T == 28) Beam(NPC.Center, Direction(NPC.Center), 230, delay: 36, duration: 18); }
                else if (m == 1 && T == 1) Pillars(4, 128, 112, 48);
                else if (m == 2 && T == 1) Rain(4, 110, 42, HazardShape.Wave);
                break;
            case 7: // Gravebell: directional gap ring, key pairs, slow pendulum beam.
                if (m == 0 && T == 1) Halo(NPC.Center, 470, Direction(NPC.Center).ToRotation());
                else if (m == 1 && (T == 1 || T == 38)) { Fan(NPC.Center + new Vector2(-65, 0), 1, 6, 0); Fan(NPC.Center + new Vector2(65, 0), 1, 6, 0); }
                else if (m == 2 && T == 1) Beam(NPC.Center, Direction(NPC.Center).RotatedBy(-.5f), 480, .01f, 54, 90);
                break;
            case 8: // Meteor: gravity dive, finite spiral, independently warned meteor impacts.
                if (m == 0) { Dash(1.7f, 30); if (T == 40) Halo(NPC.Center, 230, -MathHelper.PiOver2); }
                else if (m == 1 && T == 1) Spiral(6, 3.8f, .018f);
                else if (m == 2 && T == 1) Rain(5, 110, 48);
                break;
            case 9: // Dawn: lunging star edge, crossing lances, two staggered opening rings.
                if (m == 0) { Dash(1.55f, 25); if (T == 48) Fan(NPC.Center, 3, 5.5f, .35f); }
                else if (m == 1 && T == 1) { Beam(Aim + new Vector2(-230, -90), Vector2.UnitX, 460, delay: 50, duration: 30); Beam(Aim + new Vector2(100, -260), Vector2.UnitY, 480, delay: 70, duration: 25); }
                else if (m == 2 && (T == 1 || T == 55)) Halo(NPC.Center, 520, Direction(NPC.Center).ToRotation() + (T == 1 ? 0 : .7f));
                break;
            case 10: // Emperor: royal stomp, staggered gel rain, low gel walls.
                if (m == 0) { Hop(15, 6.5f); if (T == 85) Pillars(4, 150, 72); }
                else if (m == 1 && T == 1) Rain(7, 130, 40);
                else if (m == 2 && T == 1) { Pillars(5, 170, 125, 55); Arc(4, 4, 9); }
                break;
            case 11: // Eye: visible sweep, locked dash, intersecting retinal rays.
                if (m == 0 && T == 1) Beam(NPC.Center, Direction(NPC.Center).RotatedBy(-.55f), 1400, .012f, 48, 90);
                else if (m == 1) Dash(1.8f, 28);
                else if (m == 2 && T == 1) { Beam(Aim + new Vector2(-550, -160), Vector2.UnitX, 1100, delay: 50); Beam(Aim + new Vector2(150, -550), Vector2.UnitY, 1100, delay: 75); }
                break;
            case 12: // Void worm: committed pass through terrain, rising drill, void satellites.
                if (m == 0) Dash(1.9f, 42);
                else if (m == 1) { if (T == 1) { NPC.velocity = new Vector2(Side * 5, -11); Pillars(3, 220, 180, 55); } if (T > 45) Rest(); }
                else if (m == 2 && T == 1) { Mines(5, 270, 50); Halo(NPC.Center, 720, Direction(NPC.Center).ToRotation()); }
                break;
            case 13: // Mind: mirrored emitters, heart cross, ring with a fixed escape sector.
                if (m == 0 && T == 1) for (int s = -1; s <= 1; s += 2) Fan(Aim + new Vector2(s * 430, -170), 3, 7, .22f, HazardShape.Homing, 55);
                else if (m == 1 && T == 1) Cross(Aim, 600, MathHelper.PiOver4, 60);
                else if (m == 2 && (T == 1 || T == 54)) Halo(Aim + new Vector2(0, -220), 850, MathHelper.PiOver2, 45);
                break;
            case 14: // Bee: strafing stings, honey cells, aggressive straight pursuit.
                if (m == 0) { if (T == 1) NPC.velocity = new Vector2(Side * 12, 0); if (T % 22 == 1) Emit(HazardShape.Bolt, NPC.Center, Vector2.UnitY * 7); if (T > 65) Rest(); }
                else if (m == 1 && T == 1) Mines(6, 240, 55);
                else if (m == 2) { Dash(1.8f, 34); if (T == 60) Fan(NPC.Center, 5, 7, .75f); }
                break;
            case 15: // Judge: bone scythes, falling sentence blades, bounded spin.
                if (m == 0 && (T == 1 || T == 38)) Fan(NPC.Center, 4, 7, .6f, HazardShape.Spiral);
                else if (m == 1 && T == 1) Rain(6, 145, 50, HazardShape.Bolt);
                else if (m == 2) { if (T == 1) NPC.velocity = Direction(NPC.Center) * 8; NPC.rotation += .25f; if (T % 30 == 1) Spiral(4, 6, .022f); if (T > 75) Rest(); }
                break;
            case 16: // Wall: one-sided procession, alternating horizontal lanes, three eye turrets.
                if (m == 0) { if (T == 1) NPC.velocity = Vector2.UnitX * 5; if (T % 30 == 1) Fan(NPC.Center + new Vector2(0, (T - 45) * 3), 1, 9, 0); }
                else if (m == 1 && T == 1) for (int i = -2; i <= 2; i++) { if (i == (phase == 2 ? 1 : 0)) continue; Beam(Aim + new Vector2(-650, i * 145), Vector2.UnitX, 1350, delay: 55 + (i + 2) * 5, duration: 45); }
                else if (m == 2 && T == 1) for (int i = -1; i <= 1; i++) Fan(NPC.Center + new Vector2(0, i * 145), 3, 8, .18f, delay: 45 + (i + 1) * 15);
                break;
            case 17: // Dynasty: airborne crystal plunge, prismatic rain, opening crown rings.
                if (m == 0) { Dash(1.6f, 26); if (T == 45) Fan(NPC.Center, 7, 7, 1.1f, HazardShape.Bounce); }
                else if (m == 1 && T == 1) Rain(7, 150, 48, HazardShape.Bolt);
                else if (m == 2 && (T == 1 || T == 52)) Halo(NPC.Center, 920, Direction(NPC.Center).ToRotation() + (T == 1 ? -.5f : .5f));
                break;
            case 18: // Twins: paired convergence, parallel dual rays, lateral swap dash.
                if (m == 0) { Dash(1.7f, 30); if (T == 1) Fan(Origin + new Vector2(0, 190), 3, 11, .18f, delay: 30); }
                else if (m == 1 && T == 1) { Beam(NPC.Center + new Vector2(0, -100), Direction(NPC.Center), 1500, .004f, 52); Beam(NPC.Center + new Vector2(0, 100), Direction(NPC.Center), 1500, -.004f, 52); }
                else if (m == 2) { Dash(1.65f, 38); if (T == 55) Cross(Aim + new Vector2(0, -170), 580, .3f, 50); }
                break;
            case 19: // Prime: aimed cross, spiral saw orbit, ordered artillery.
                if (m == 0 && T == 1) Cross(NPC.Center, 1250, Direction(NPC.Center).ToRotation() + .3f, 55);
                else if (m == 1 && (T == 1 || T == 42)) Spiral(8, 6, T == 1 ? .02f : -.02f);
                else if (m == 2 && T == 1) { Rain(5, 190, 52); Mines(3, 270, 75); }
                break;
            case 20: // Steel worm: rail drill, magnetic nodes, separated vertical rails.
                if (m == 0) Dash(2, 46);
                else if (m == 1 && T == 1) { Mines(6, 320, 55); Spiral(5, 5.5f, -.015f); }
                else if (m == 2 && T == 1) for (int i = -2; i <= 2; i++) Beam(Aim + new Vector2(i * 230, -650), Vector2.UnitY, 1300, delay: 50 + (i + 2) * 10, duration: 32);
                break;
            case 21: // Flower: curling petals, rooted garden, thorny pursuit.
                if (m == 0 && (T == 1 || T == 48)) Spiral(7, 6, .025f);
                else if (m == 1 && T == 1) { Pillars(5, 190, 240, 60); Mines(3, 260, 75); }
                else if (m == 2) { Dash(1.7f, 34); if (T == 56) Fan(NPC.Center, 7, 8, 1.2f, HazardShape.Wave); }
                break;
            case 22: // Temple: opposed fists, golden sweep, staggered ground faults.
                if (m == 0 && T == 1) { Fan(Aim + new Vector2(-550, 0), 1, 12, 0, delay: 58); Fan(Aim + new Vector2(550, 0), 1, 12, 0, delay: 80); }
                else if (m == 1 && T == 1) Beam(NPC.Top, Direction(NPC.Top).RotatedBy(-.6f), 1500, .01f, 60, 105, 28);
                else if (m == 2 && T == 1) Pillars(7, 180, 210, 55);
                break;
            case 23: // Duke: separately telegraphed chained dashes, tidal spiral, tornado corridors.
                if (m == 0) { Dash(1.9f, 25); if (T == 65) Fan(NPC.Center, 5, 10, .45f, HazardShape.Wave, 24); }
                else if (m == 1 && (T == 1 || T == 42)) Spiral(7, 7.5f, -.024f);
                else if (m == 2 && T == 1) Pillars(4, 260, 500, 62);
                break;
            case 24: // Corona: lance rain, measured rotating rays, light-star orbit.
                if (m == 0 && T == 1) Rain(8, 155, 52, HazardShape.Bolt);
                else if (m == 1 && T == 1) { Beam(NPC.Center, Direction(NPC.Center).RotatedBy(-.7f), 1550, .012f, 58, 100); Beam(NPC.Center, Direction(NPC.Center).RotatedBy(.7f), 1550, -.012f, 78, 80); }
                else if (m == 2 && T == 1) { Mines(7, 340, 60); Fan(NPC.Center, 5, 7, .8f, HazardShape.Homing, 38); }
                break;
            case 25: // Pontiff: four ritual anchors, two-stage radial ritual, diagonal edict.
                if (m == 0 && T == 1) for (int i = 0; i < 4; i++) Fan(Aim + (MathHelper.PiOver4 + i * MathHelper.PiOver2).ToRotationVector2() * 420, 2, 8, .14f, delay: 55 + i * 12);
                else if (m == 1 && (T == 1 || T == 55)) Halo(NPC.Center, 1200, MathHelper.PiOver2 + (T == 1 ? -.55f : .55f), 45);
                else if (m == 2 && T == 1) Cross(Aim, 850, MathHelper.PiOver4, 65);
                break;
            case 26: // End Moon: hands converge, central deathray, lunar collapse with an exit.
                if (m == 0 && T == 1) { Fan(NPC.Center + new Vector2(-240, 110), 4, 9, .45f, HazardShape.Homing, 55); Fan(NPC.Center + new Vector2(240, 110), 4, 9, .45f, HazardShape.Homing, 75); }
                else if (m == 1 && T == 1) Beam(NPC.Center, Direction(NPC.Center).RotatedBy(-.8f), 1650, .014f, 65, 110, 32);
                else if (m == 2 && T == 1) { Halo(Aim + new Vector2(0, -230), 1300, MathHelper.PiOver2, 60); Mines(6, 420, 70); }
                break;
        }
    }
}
