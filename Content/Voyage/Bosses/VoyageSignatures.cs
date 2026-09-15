using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace StarfallThrone.Content.Voyage.Bosses;

public abstract partial class VoyageBossNPC
{
    private void Ray(Vector2 from, Vector2 to, int delay = 70, int duration = 35, float width = 12,
        int device = -1, float turn = 0, float damage = 1.15f, bool finite = false)
        => Emit(VoyageHazardShape.Beam, from, Vector2.Zero, delay, duration, width, (to - from).ToRotation(), damage,
            device, finite ? Vector2.Distance(from, to) : 1250, turn);
    private void Fan(Vector2 from, Vector2 to, int count = 3, float spread = .24f, float speed = 10, int delay = 65, int device = -1,
        VoyageHazardShape shape = VoyageHazardShape.Bolt)
    {
        Vector2 direction = (to - from).SafeNormalize(Vector2.UnitY);
        for (int i = 0; i < count; i++) Emit(shape, from, direction.RotatedBy((i - (count - 1) * .5f) * spread) * speed, delay, 140, 14, deviceSlot: device);
    }
    private void OpenRing(Vector2 from, float gapAngle, int delay = 75, float radius = 540, int device = -1)
        => Emit(VoyageHazardShape.Ring, from, Vector2.Zero, delay, 90, radius, gapAngle, .95f, device, gap: .8f);
    private Vector2 Floor(Vector2 at)
    {
        int x = Math.Clamp((int)at.X / 16, 10, Main.maxTilesX - 11);
        int start = Math.Clamp((int)at.Y / 16, 10, Main.maxTilesY - 50);
        for (int y = start; y < start + 40; y++)
            if (Main.tile[x, y].HasUnactuatedTile && Main.tileSolid[Main.tile[x, y].TileType]) return new Vector2(at.X, y * 16);
        return at + new Vector2(0, 150);
    }
    private void Pod(Vector2 target, int slot)
    {
        NPC pod = SpawnPart(VoyagePart.Cargo, slot, target - new Vector2(0, 280), lifetime: 245);
        if (pod?.ModNPC is VoyageDeviceNPC p) { p.Anchor = target; pod.netUpdate = true; }
    }
    private void Hop(int start, int duration, Vector2 destination, float height)
    {
        if (Timer < start || Timer >= start + duration) return;
        // Each hop's final coordinates are frozen from Aim/Origin before the attack.
        float t = (Timer - start + 1f) / duration;
        Vector2 begin = start == 0 ? Origin : Origin + new Vector2((start / duration) * 220 * Math.Sign(Aim.X - Origin.X + .01f), 0);
        Vector2 next = Vector2.Lerp(begin, destination, t) - Vector2.UnitY * (MathF.Sin(t * MathHelper.Pi) * height);
        NPC.velocity = next - NPC.Center;
    }
    private void TickSignature(Player player)
    {
        int t = Timer;
        switch (BossIndex)
        {
            case 0: // A swallowed freight port: cargo can be disarmed, then three committed parabolic hops.
                if (Move == 0)
                {
                    Rest();
                    if (t == 1) Pod(Floor(Aim + new Vector2(-190, 10)), 0);
                    if (t == 35) Pod(Floor(Aim + new Vector2(210, 10)), 1);
                }
                else if (Move == 1)
                {
                    float side = Math.Sign(Aim.X - Origin.X + .01f);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 destination = Origin + new Vector2((i + 1) * 220 * side, 0);
                        if (t == 1) Emit(VoyageHazardShape.Mine, destination + new Vector2(0, 60), Vector2.Zero, 70 + i * 45, 14, 70, damageScale: .7f);
                        Hop(i * 45, 45, destination, Phase > 0 ? 180 : 220);
                    }
                    if (t >= 135) Rest();
                }
                else
                {
                    Rest();
                    if (t == 1)
                    {
                        Emit(VoyageHazardShape.GravityField, Aim + new Vector2(-220, 20), Vector2.Zero, 65, 240, 150);
                        Emit(VoyageHazardShape.GravityField, Aim + new Vector2(220, 20), Vector2.Zero, 65, 240, 150);
                        Pod(Floor(Aim + new Vector2(0, 20)), 2);
                    }
                }
                break;
            case 1: // Tracking memories, near/far focus, and a blind-sector charge.
                if (Move != 2 || t < 65 || t >= 97) Rest();
                if (Move == 0 && t == 1)
                    for (int i = 0; i < Trace.Length; i++) Ray(Trace[i] - new Vector2(0, 440), Trace[i], 65 + i * 9, 16, 15);
                if (Move == 1 && t == 1)
                {
                    if (Vector2.Distance(Origin, Aim) < 460) Fan(Origin, Aim, 5, .24f, 9, 70);
                    else Ray(Origin, Aim, 75, 40, 19, damage: 1.45f);
                }
                if (Move == 2)
                {
                    if (t == 1) OpenRing(Origin, (Origin - Aim).ToRotation(), 65, 520);
                    Dash(65, 32, 19 + Phase, Aim);
                }
                break;
            case 2: // Independent segment budgets; broken middle armour creates a second shorter moving leader.
                if (Move == 0)
                {
                    Dash(1, 60, 17 + Phase, Aim);
                    if (t == 1) Emit(VoyageHazardShape.Lane, Origin, Vector2.Zero, 65, 25, 28, (Aim - Origin).ToRotation(), .65f, length: 1200);
                }
                else if (Move == 1)
                {
                    Fly(Aim + new Vector2(MathF.Sin(t * .02f) * 420, -250), 11);
                    if (t == 1)
                        for (int i = 1; i < 9; i += 2)
                        {
                            NPC segment = Part(i, VoyagePart.Segment);
                            if (segment != null) SpawnPart(VoyagePart.Asteroid, i, segment.Center, lifetime: 230);
                        }
                }
                else
                {
                    if (t < 95 || t >= 135) Rest();
                    if (t == 1) Emit(VoyageHazardShape.Current, Aim, Vector2.Zero, 65, 80, 280, turn: .001f);
                    Dash(95, 40, 21, Aim);
                }
                break;
            case 3: // Damageable neural graph: source-linked projectiles disappear when a node dies.
                Rest();
                if (Move == 0)
                {
                    for (int i = 0; i < 4; i++) if (t == 1 + i * 20)
                    {
                        NPC node = Part(i, VoyagePart.NeuralNode);
                        if (node != null) Fan(node.Center, Aim, 2, .15f, 10, 65, node.whoAmI);
                    }
                }
                else if (Move == 1 && t == 1)
                {
                    // The two visual decoys have no hitboxes or contact damage; the connected brain fires.
                    Ray(Origin, Aim, 80, 35, 18);
                }
                else if (Move == 2 && t == 1)
                {
                    NPC node = Part((int)NPC.ai[2] / 3 % 4, VoyagePart.NeuralNode);
                    if (node != null) Emit(VoyageHazardShape.Mine, node.Center, Vector2.Zero, 100, 32, 235, damageScale: 1.45f, deviceSlot: node.whoAmI);
                    else OpenRing(Origin, (Aim - Origin).ToRotation(), 80, 440);
                }
                if (PartsOfRole(VoyagePart.NeuralNode) == 0 && t == 85) Fan(NPC.Center, Aim, 3, .2f, 9);
                break;
            case 4: // Carrier hangars, finite drone formations, fuel-trail diving.
                if (Move == 0)
                {
                    Rest();
                    if (t == 1)
                        for (int h = 0; h < 2; h++)
                        {
                            NPC hangar = Part(h, VoyagePart.Hangar);
                            if (hangar != null)
                                for (int i = 0; i < 3; i++) SpawnPart(VoyagePart.Drone, h * 3 + i, hangar.Center + new Vector2(i * 30, 20), lifetime: 225);
                        }
                    if (PartsOfRole(VoyagePart.Hangar) == 0 && t == 1) Ray(Origin, Aim, 85, 30, 25, damage: 1.3f);
                }
                else if (Move == 1)
                {
                    Rest();
                    if (t == 1)
                        for (int h = 0; h < 2; h++) if (Part(h, VoyagePart.Hangar) != null) Pod(Floor(Aim + new Vector2(h == 0 ? -230 : 230, 10)), h + 1);
                }
                else
                {
                    Dash(1, 45, 19, Aim);
                    if (t is 20 or 40 or 60) Emit(VoyageHazardShape.Mine, NPC.Center, Vector2.Zero, 85, 30, 90, damageScale: 1.05f);
                }
                break;
            case 5: // Tensioned anchor cables and an actual disarm/imbalance reward.
                if (Move != 1 || t < 1 || t >= 36) Rest();
                if (Move == 0 && t == 1)
                    for (int i = 0; i < 2; i++)
                    {
                        NPC anchor = Part(i, VoyagePart.Anchor);
                        if (anchor != null) Ray(Origin, anchor.Center, 75, 65, 13, anchor.whoAmI, finite: true);
                    }
                if (Move == 1)
                {
                    NPC anchor = Part(((int)NPC.ai[2] / 3) % 2, VoyagePart.Anchor);
                    if (anchor != null) Dash(1, 35, 18, anchor.Center);
                    else if (t == 1) Fan(Origin, Aim, 3, .32f, 8);
                }
                if (Move == 2 && t == 1)
                {
                    NPC anchor = Part(0, VoyagePart.Anchor) ?? Part(1, VoyagePart.Anchor);
                    if (anchor != null) Ray(anchor.Center, Aim, 80, 65, 20, anchor.whoAmI, .005f, 1.4f);
                    else OpenRing(Origin, (Aim - Origin).ToRotation(), 75, 450);
                }
                break;
            case 6: // Open-door corridor. No body contact wall: a marked lane always remains traversable.
                Fly(Origin + new Vector2(Math.Sign(Aim.X - Origin.X + .01f) * t * .8f, 0), 4);
                if (Move == 0 && t == 1)
                {
                    int safe = (int)NPC.ai[2] / 3 % 5;
                    for (int lane = 0; lane < 5; lane++) if (lane != safe)
                    {
                        Vector2 from = new(Origin.X, Aim.Y + (lane - 2) * 160);
                        Ray(from, from + new Vector2(Math.Sign(Aim.X - Origin.X + .01f), 0), 85, 45, 33, damage: 1.3f);
                    }
                }
                if (Move == 1)
                    for (int i = 0; i < 3; i++) if (t == 1 + i * 32)
                    {
                        NPC gun = Part(i, VoyagePart.Bulkhead);
                        if (gun != null) Fan(gun.Center, Aim, 2, .16f, 11, 65, gun.whoAmI);
                    }
                if (Move == 2 && t == 1)
                {
                    Emit(VoyageHazardShape.Current, Aim + new Vector2(0, 150), Vector2.Zero, 70, 120, 270,
                        Math.Sign(Aim.X - Origin.X + .01f) > 0 ? 0 : MathHelper.Pi);
                    Ray(new Vector2(Origin.X, Aim.Y - 220), new Vector2(Aim.X, Aim.Y - 220), 95, 40, 25);
                    Ray(new Vector2(Origin.X, Aim.Y + 250), new Vector2(Aim.X, Aim.Y + 250), 125, 40, 25);
                }
                break;
            case 7: // Orbital crystals and fully previewed, source-linked folded optical paths.
                Rest();
                if (Move == 0 && t == 1)
                    for (int i = 0; i < 3; i++)
                    {
                        NPC mirror = Part(i, VoyagePart.Mirror);
                        if (mirror != null) OpenRing(mirror.Center, (Aim - mirror.Center).ToRotation(), 70 + i * 25, 330, mirror.whoAmI);
                    }
                if (Move == 1 && t == 1)
                {
                    NPC mirror = Part((int)NPC.ai[2] / 3 % 3, VoyagePart.Mirror);
                    if (mirror != null)
                    {
                        Ray(Origin, mirror.Center, 85, 35, 13, mirror.whoAmI, finite: true);
                        Ray(mirror.Center, Aim, 85, 35, 13, mirror.whoAmI);
                    }
                    else Fan(Origin, Aim, 3, .3f, 8);
                }
                if (Move == 2 && t == 1)
                    for (int i = 0; i < 3; i++)
                    {
                        NPC mirror = Part(i, VoyagePart.Mirror);
                        if (mirror == null) continue;
                        Vector2 outside = Aim + (i * MathHelper.TwoPi / 3).ToRotationVector2() * 400;
                        Ray(mirror.Center, outside, 65, 18, 12, mirror.whoAmI, finite: true);
                        Ray(outside, Origin, 115, 25, 12, mirror.whoAmI, finite: true);
                    }
                break;
            case 8: // Independent twins. Blue fires AFTER the red dash; the survivor inherits one reduced attack.
                Rest();
                NPC red = Part(0, VoyagePart.RedEye), blue = Part(1, VoyagePart.BlueEye);
                if (Move == 0 && t == 50)
                {
                    NPC source = blue ?? red;
                    if (source != null) Ray(source.Center, Aim, 70, 35, blue != null ? 17 : 11, source.whoAmI);
                }
                if (Move == 1 && t == 1)
                {
                    if (red != null && blue != null) Ray(red.Center, blue.Center, 85, 35, 12, red.whoAmI, finite: true);
                    else if ((red ?? blue) is NPC survivor) OpenRing(survivor.Center, (Aim - survivor.Center).ToRotation(), 80, 500, survivor.whoAmI);
                }
                if (Move == 2 && t == 70)
                {
                    NPC source = blue ?? red;
                    if (source != null) Fan(source.Center, Aim, blue != null ? 3 : 2, .25f, 12, 65, source.whoAmI);
                }
                break;
            case 9: // Four destructible, non-respawning arms. Only two are scheduled each round.
                Rest();
                if (t == 1)
                {
                    int first = ((int)NPC.ai[2]) % 4;
                    ActivateWeapon(first, 70);
                    ActivateWeapon((first + 1) % 4, 100);
                    if (PartsOfRole(VoyagePart.Weapon) == 0) Ray(Origin, Aim, 90, 30, 18);
                }
                break;
            case 10: // Armoured train sections share a fixed encounter budget but have separate health pools.
                if (Move == 0)
                {
                    Dash(1, 65, IsBroken(2) || IsBroken(5) ? 13 : 20, Aim);
                    if (t == 1) Ray(Origin, Aim, 80, 35, 22, damage: 1.45f);
                }
                else if (Move == 1)
                {
                    Fly(Aim + new Vector2(0, -280), 8);
                    for (int group = 0; group < 3; group++) if (t == 1 + group * 32)
                        for (int j = 0; j < 2; j++)
                        {
                            NPC section = Part(group * 3 + j, VoyagePart.Segment);
                            if (section != null) Fan(section.Center, Aim, 1, 0, 12, 65, section.whoAmI);
                        }
                }
                else
                {
                    Fly(Aim + new Vector2(-380, -180), 8);
                    if (t == 60)
                        for (int i = 1; i < 9; i += 3)
                        {
                            NPC escort = Part(i, VoyagePart.Segment);
                            if (escort != null) Fan(escort.Center, Aim, 2, .24f, 11, 65, escort.whoAmI);
                        }
                }
                break;
            case 11: // Root connections are real breakable lines, not permanent tile edits.
                if (Move != 2 || Phase == 0 || t < 115 || t >= 140) Rest();
                if (Move == 0 && t == 1)
                    for (int i = 0; i < 3; i++)
                    {
                        NPC a = Part(i, VoyagePart.RootNode), b = Part(i + 1, VoyagePart.RootNode);
                        if (a != null && b != null) Ray(a.Center, b.Center, 80 + i * 18, 50, 13, a.whoAmI, finite: true);
                    }
                if (Move == 1 && t == 1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        NPC node = Part(i, VoyagePart.RootNode);
                        if (node != null)
                        {
                            NPC seed = SpawnPart(VoyagePart.SeedFlower, i, node.Center - new Vector2(0, 170), lifetime: 260);
                            if (seed?.ModNPC is VoyageDeviceNPC flower)
                            {
                                flower.Anchor = Floor(Aim + new Vector2((i - 1.5f) * 190, 20)) - new Vector2(0, 35);
                                seed.netUpdate = true;
                            }
                        }
                    }
                }
                if (Move == 2)
                {
                    if (t == 1) Ray(Origin, Aim, 90, 50, 34, damage: 1.4f);
                    if (Phase > 0) Dash(115, 25, 14, Aim);
                }
                break;
            case 12: // Geometric blocks rearrange BETWEEN attacks; ordered stamps preserve a route.
                Rest();
                if (Move == 0 && t == 1)
                    for (int i = 0; i < 4; i++)
                    {
                        NPC block = Part(i, VoyagePart.Geometry);
                        if (block != null) Ray(block.Center, Aim + new Vector2((i - 1.5f) * 130, 0), 75 + i * 12, 25, 17, block.whoAmI, finite: true);
                    }
                if (Move == 1 && t == 1)
                    for (int i = 0; i < 5; i++) if (i != (int)NPC.ai[2] / 3 % 5)
                        Emit(VoyageHazardShape.Pillar, Floor(Aim + new Vector2((i - 2) * 190, 30)), Vector2.Zero, 80 + i * 12, 30, 42,
                            damageScale: 1.4f, length: 370);
                if (Move == 2 && t is 1 or 35 or 69)
                    Ray(Origin, Aim + new Vector2((t / 34 - 1) * 220, 0), 75, 25, 24, damage: 1.4f);
                break;
            case 13: // Predatory committed dashes leave finite delayed wakes, which the inhale later clears.
                if (Move == 0)
                {
                    Dash(1, 48, 23, Aim);
                    if (t is 15 or 35 or 55)
                        Emit(VoyageHazardShape.Lane, NPC.Center, Vector2.Zero, 80, 70, 20, (Aim - Origin).ToRotation(), .95f, length: 180);
                }
                else if (Move == 1)
                {
                    Fly(Aim + new Vector2(MathF.Cos(t * .018f) * 420, -160), 12);
                    if (t == 1)
                    {
                        Emit(VoyageHazardShape.Current, Aim + new Vector2(-250, 0), Vector2.Zero, 65, 140, 200, turn: MathHelper.PiOver2);
                        Emit(VoyageHazardShape.Current, Aim + new Vector2(250, 0), Vector2.Zero, 65, 140, 200, turn: -MathHelper.PiOver2);
                    }
                }
                else
                {
                    Rest();
                    if (t == 1)
                    {
                        ClearHazards();
                        Emit(VoyageHazardShape.Current, Origin, Vector2.Zero, 65, 60, 400, turn: .001f);
                    }
                    if (t == 65) OpenRing(Origin, (Aim - Origin).ToRotation(), 75, 650);
                }
                break;
            case 14: // Reflective sails, remembered butterfly paths, then a sequential solar-wind fan.
                Rest();
                if (Move == 0 && t == 1)
                {
                    Vector2 last = Origin;
                    bool found = false;
                    for (int i = 0; i < 3; i++)
                    {
                        NPC sail = Part(i, VoyagePart.Sail);
                        if (sail == null) continue;
                        Ray(last, sail.Center, 95, 28, 12, sail.whoAmI, finite: true);
                        last = sail.Center;
                        found = true;
                    }
                    if (found) Ray(last, Aim, 95, 28, 12);
                    else Fan(Origin, Aim, 3, .27f, 11);
                }
                if (Move == 1 && t == 1)
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 a = Aim + new Vector2(-430, (i - 1.5f) * 130), b = Aim + new Vector2(430, (1.5f - i) * 90);
                        Ray(a, b, 95 + i * 14, 22, 13, finite: true);
                    }
                if (Move == 2 && t == 1)
                    for (int i = -2; i <= 2; i++)
                    {
                        float angle = (Aim - Origin).ToRotation() + i * .28f;
                        Emit(VoyageHazardShape.Beam, Origin, Vector2.Zero, 80 + (i + 2) * 18, 22, 17, angle, 1.25f);
                    }
                break;
            case 15: // Ordered real star gates. Broken gates cancel beams, collapse a ritual, and stagger the navigator.
                Rest();
                if (Move == 0 && t == 1)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        NPC gate = Part(i, VoyagePart.Gate);
                        if (gate != null) Ray(gate.Center, Aim, 75 + i * 35, 25, 16, gate.whoAmI);
                    }
                }
                if (Move == 1 && t == 1)
                {
                    float baseAngle = (Aim - Origin).ToRotation();
                    Emit(VoyageHazardShape.Beam, Origin, Vector2.Zero, 85, 45, 16, baseAngle - .35f, 1.2f, turn: .009f);
                }
                if (Move == 2 && t == 1)
                {
                    NPC gate = Part(((int)NPC.ai[2] / 3) % 3, VoyagePart.Gate);
                    if (gate != null) Emit(VoyageHazardShape.Mine, gate.Center, Vector2.Zero, 110, 32, 270, damageScale: 1.4f, deviceSlot: gate.whoAmI);
                    else OpenRing(Origin, (Aim - Origin).ToRotation(), 85, 600);
                }
                if (PartsOfRole(VoyagePart.Gate) == 0 && t == 70) Fan(Origin, Aim, 3, .23f, 10);
                break;
            case 16: TickArk(); break;
        }
    }
    private void ActivateWeapon(int slot, int delay)
    {
        NPC arm = Part(slot, VoyagePart.Weapon);
        if (arm == null) return;
        switch (slot)
        {
            case 0: Ray(arm.Center, Aim, delay, 35, 19, arm.whoAmI, damage: 1.35f); break;
            case 1: Fan(arm.Center, Aim, 4, .3f, 9, delay, arm.whoAmI, VoyageHazardShape.Wave); break;
            case 2:
                Emit(VoyageHazardShape.Current, Aim + new Vector2(-180, 30), Vector2.Zero, delay, 85, 160, turn: .001f, deviceSlot: arm.whoAmI);
                Emit(VoyageHazardShape.Mine, Aim + new Vector2(190, 30), Vector2.Zero, delay + 25, 28, 100, deviceSlot: arm.whoAmI);
                break;
            case 3: Fan(arm.Center, Aim, 3, .2f, 10, delay, arm.whoAmI, VoyageHazardShape.Arc); break;
        }
    }
    private void TickArk()
    {
        int t = Timer;
        Rest();
        if (Phase == 0)
        {
            NPC left = Part(0, VoyagePart.LeftCity), right = Part(1, VoyagePart.RightCity);
            if (Move == 0 && t == 1 && left != null)
                for (int i = 0; i < 3; i++) SpawnPart(VoyagePart.Drone, i, left.Center + new Vector2(i * 40, 0), lifetime: 230);
            if (Move == 1 && t == 1 && right != null)
            {
                Ray(right.Center, Aim + new Vector2(-160, 0), 95, 30, 24, right.whoAmI, damage: 1.35f);
                Ray(right.Center, Aim + new Vector2(160, 0), 140, 30, 24, right.whoAmI, damage: 1.35f);
            }
            if (Move == 2 && t == 1)
            {
                NPC city = left ?? right;
                if (city != null) OpenRing(city.Center, (Aim - city.Center).ToRotation(), 95, 650, city.whoAmI);
            }
            // Destroyed city never respawns; its surviving sibling gains one bounded support attack.
            if ((left == null || right == null) && t == 95 && (left ?? right) is NPC survivor)
                Fan(survivor.Center, Aim, 3, .25f, 11, 65, survivor.whoAmI);
        }
        else if (Phase == 1)
        {
            NPC array = Part(2, VoyagePart.Array);
            if (array == null) return;
            if (Move == 0 && t == 1)
                for (int i = -2; i <= 2; i++) if (i != 0)
                    Ray(Aim + new Vector2(i * 190, -500), Aim + new Vector2(i * 190, 0), 90 + (i + 2) * 17, 28, 24, array.whoAmI);
            if (Move == 1 && t == 1)
                Ray(array.Center, Aim, 110, 65, 30, array.whoAmI, .0035f, 1.4f);
            if (Move == 2 && t == 1)
            {
                Vector2 portal = Aim + new Vector2(-420, -150);
                Ray(array.Center, portal, 95, 35, 17, array.whoAmI, finite: true);
                Ray(portal, Aim, 95, 35, 17, array.whoAmI);
            }
        }
        else
        {
            if (Move == 0 && t == 1)
            {
                Ray(Origin + new Vector2(-150, 0), Aim + new Vector2(-180, 0), 100, 45, 25, damage: 1.35f);
                Ray(Origin + new Vector2(150, 0), Aim + new Vector2(180, 0), 150, 45, 25, damage: 1.35f);
            }
            if (Move == 1 && t == 1)
            {
                OpenRing(Origin, (Aim - Origin).ToRotation(), 90, 730);
                for (int i = -1; i <= 1; i += 2)
                    Emit(VoyageHazardShape.Mine, Aim + new Vector2(i * 360, 90), Vector2.Zero, 145, 30, 130, damageScale: 1.2f);
            }
            if (Move == 2 && t == 1)
            {
                // Final voyage: five sequential rows leave a broad, gently curving 300px safe route.
                for (int row = 0; row < 5; row++)
                {
                    float y = Aim.Y - 240 + row * 120;
                    float safeX = Aim.X + MathF.Sin(row * .7f) * 140;
                    Emit(VoyageHazardShape.Lane, new Vector2(Aim.X - 720, y), Vector2.Zero, 110 + row * 27, 25, 22, 0, 1.4f,
                        length: safeX - 150 - (Aim.X - 720));
                    Emit(VoyageHazardShape.Lane, new Vector2(safeX + 150, y), Vector2.Zero, 110 + row * 27, 25, 22, 0, 1.4f,
                        length: Aim.X + 720 - (safeX + 150));
                }
            }
        }
    }
}
