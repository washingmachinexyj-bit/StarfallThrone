using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace StarfallThrone.Content.Ascendant.Bosses;

public abstract partial class AscendantBossNPC
{
    public Vector2 FloorNear(Vector2 point)
    {
        // Read-only standing-surface lookup; never moves or creates terrain.
        int x = Math.Clamp((int)point.X / 16, 10, Math.Max(10, Main.maxTilesX - 11));
        int y0 = Math.Clamp((int)point.Y / 16, 10, Math.Max(10, Main.maxTilesY - 11));
        for (int y = y0; y < Math.Min(Main.maxTilesY - 10, y0 + 32); y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) return WorldPoint(new Vector2(point.X, y * 16));
        }
        return WorldPoint(point + new Vector2(0, 22));
    }
    private void Miroen(Player player)
    {
        NPC.velocity *= .9f;
        if (FinalActive)
        {
            if (Timer >= 1 && Timer <= 771 && (Timer - 1) % 110 == 0) LifePetal((Timer - 1) / 110, player.Center);
            return;
        }
        if (Timer is 1 or 141) LifePair(Move, player.Center);
        if (Phase == 2 && Timer == 281) LifePair((Move + 1) % 4, player.Center, true);
        if (Phase > 0 && Timer is 150 or 195 or 240)
        {
            int mark = (Timer - 150) / 45;
            if ((BrokenMask & (1 << mark)) == 0)
            {
                Vector2 at = FloorNear(Arena + new Vector2((mark - 1) * 190, 20)) - new Vector2(0, 44);
                Emit(AscendantShape.Ring, at, Vector2.Zero, delay: 75, duration: 80, width: 10, length: 220,
                    angle: (player.Center - at).ToRotation(), gap: 1.2f, mark: 20 + mark);
            }
        }
        // Expired, unbroken cores burst once. Broken cores never emit their enhancement/burst again this round.
        if (Phase > 0 && Timer == 335)
            for (int mark = 0; mark < 3; mark++)
                if ((BrokenMask & (1 << mark)) == 0)
                {
                    Vector2 at = FloorNear(Arena + new Vector2((mark - 1) * 190, 20)) - new Vector2(0, 44);
                    Emit(AscendantShape.Ring, at, Vector2.Zero, delay: 60, duration: 40, width: 8, length: 150,
                        angle: (player.Center - at).ToRotation(), gap: 1.3f, mark: 20 + mark);
                }
        if (Phase == 2)
        {
            if (Timer is 190 or 290 or 390) NPC.velocity = Vector2.Zero;
            if (Authority && Timer is 190 or 290)
            { Origin = NPC.Center; Aim = WorldPoint(player.Center); NPC.netUpdate = true; }
            if (Timer >= 250 && Timer < 290 || Timer >= 350 && Timer < 390)
                NPC.velocity = (Aim - Origin).SafeNormalize(Vector2.UnitX) * 8;
        }
    }
    private void LifePair(int pair, Vector2 target, bool shortBeat = false)
    {
        Vector2 floor = FloorNear(target + new Vector2(0, 20));
        int duration = shortBeat ? 80 : 115;
        switch (pair)
        {
            case 0: // Dew-shell slow bead, then lightweight drifting seeds.
                Emit(AscendantShape.Roller, floor + new Vector2(-290, -13), new Vector2(3.4f, 0), duration: duration, width: 22, mark: 0);
                for (int i = 0; i < 3; i++)
                    Emit(AscendantShape.Seed, floor + new Vector2(-140 + i * 150, -220), new Vector2(i % 2 == 0 ? .65f : -.65f, 2.2f), duration: duration, width: 12, mark: 7);
                break;
            case 1: // Moss roller and committed pottery-fragment arcs.
                Emit(AscendantShape.Roller, floor + new Vector2(320, -15), new Vector2(-4, 0), duration: duration, width: 24, mark: 2);
                for (int sign = -1; sign <= 1; sign += 2)
                    Emit(AscendantShape.Drop, floor + new Vector2(sign * 140, -170), new Vector2(-sign * 1.6f, -.7f), delay: 90, duration: shortBeat ? 65 : 95, width: 16, mark: 3);
                break;
            case 2: // Three parasol spokes have visible, fixed downward bearings; no retarget on release.
                for (int i = -1; i <= 1; i++)
                    Emit(AscendantShape.Seed, floor + new Vector2(0, -210), new Vector2(i * 1.9f, 2.8f), duration: duration, width: 14, mark: 4);
                Emit(AscendantShape.Pillar, floor + new Vector2(100, 0), Vector2.Zero, delay: 95, duration: 22, width: 56, length: 20, mark: 5);
                break;
            default: // Teeth and thorns lock onto two locations, leaving a walking exit on both sides.
                for (int sign = -1; sign <= 1; sign += 2)
                    Emit(AscendantShape.Pillar, FloorNear(target + new Vector2(sign * 65, 20)), Vector2.Zero, delay: sign < 0 ? 75 : 110,
                        duration: 24, width: 26, length: 96, heavy: true, mark: 6);
                Emit(AscendantShape.Pillar, floor, Vector2.Zero, delay: 90, duration: 28, width: 60, length: 18, mark: 0);
                break;
        }
    }
    private void LifePetal(int petal, Vector2 target)
    {
        Vector2 floor = FloorNear(target + new Vector2(0, 20));
        switch (petal)
        {
            case 0: Emit(AscendantShape.Ring, floor - new Vector2(0, 80), Vector2.Zero, duration: 90, width: 10, length: 290, angle: MathHelper.PiOver2, gap: 1.2f); break;
            case 1:
                for (int i = -1; i <= 1; i++) Emit(AscendantShape.Seed, floor + new Vector2(i * 140, -180), new Vector2(.8f * i, 2.6f), duration: 75, width: 12, mark: 7);
                break;
            case 2: Emit(AscendantShape.Roller, floor + new Vector2(-310, -14), new Vector2(4, 0), duration: 110, width: 22, mark: 2); break;
            case 3:
                for (int i = -1; i <= 1; i++) Emit(AscendantShape.Drop, floor + new Vector2(i * 160, -160), new Vector2(-i, -.8f), duration: 85, width: 14, mark: 3);
                break;
            case 4:
                for (int i = -1; i <= 1; i++) Emit(AscendantShape.Seed, floor + new Vector2(0, -170), new Vector2(i * 2.1f, 2.4f), duration: 80, width: 14, mark: 4);
                break;
            case 5: Emit(AscendantShape.Ring, floor + new Vector2(-100, -80), Vector2.Zero, duration: 90, width: 10, length: 260, angle: 0, gap: 1.15f, mark: 5); break;
            case 6: Emit(AscendantShape.Pillar, floor, Vector2.Zero, duration: 26, width: 42, length: 110, heavy: true, mark: 6); break;
            case 7:
                for (int sign = -1; sign <= 1; sign += 2) Emit(AscendantShape.Pillar, floor + new Vector2(sign * 115, 0), Vector2.Zero, duration: 30, width: 34, length: 125, heavy: true, mark: 0);
                break;
        }
    }
    private void Velsa(Player player)
    {
        NPC.velocity *= .92f;
        if (Phase < 2)
        {
            if (Timer is 1 or 211) FurnaceLaw(Move, player.Center);
            if (Phase > 0 && Timer is 101 or 311) FurnaceLaw((Move + 1) % 4, player.Center);
        }
        else
        {
            if (Timer >= 1 && Timer <= 391 && (Timer - 1) % 130 == 0) FurnaceLaw((Timer - 1) / 130, player.Center, true);
            if (Timer == 401)
            {
                // Re-center once at warning creation, then the lane is fixed and moves at walking speed.
                if (Authority) { Origin = WorldPoint(player.Center); NPC.netUpdate = true; }
                Emit(AscendantShape.Corridor, Origin, Vector2.Zero, delay: 100, duration: 175, width: 12, length: 850, gap: 135, heavy: true, mark: 90);
            }
            if (Timer is 441 or 536)
            {
                // Upper then lower slash: standing / ordinary jump, not flight.
                Vector2 floor = FloorNear(Origin + new Vector2(0, 20));
                float y = Timer == 441 ? floor.Y - 82 : floor.Y - 12;
                Emit(AscendantShape.Beam, new Vector2(Origin.X - 580, y), Vector2.Zero, delay: 75, duration: 20, width: 16, length: 1160, heavy: true, mark: 90);
            }
        }
    }
    private void FurnaceLaw(int law, Vector2 target, bool brief = false)
    {
        if (law == Suppressed) return;
        Vector2 floor = FloorNear(target + new Vector2(0, 20));
        switch (law)
        {
            case 0: // Mirror slash and explicitly warned reflected repeat.
                Emit(AscendantShape.Beam, floor + new Vector2(-340, -80), Vector2.Zero, delay: 70, duration: 18, width: 18, length: 680, angle: .16f, mark: law);
                Emit(AscendantShape.Beam, floor + new Vector2(340, -80), Vector2.Zero, delay: brief ? 100 : 135, duration: 18, width: 18, length: 680, angle: MathHelper.Pi - .16f, mark: law);
                break;
            case 1: // Numbered finite candle columns, wide gaps and locked floor anchors.
                for (int i = -1; i <= 1; i++)
                    Emit(AscendantShape.Pillar, FloorNear(target + new Vector2(i * 155, 20)), Vector2.Zero, delay: 75 + (i + 1) * (brief ? 12 : 25), duration: 22, width: 32, length: 160, heavy: i == 0, mark: law);
                break;
            case 2: // Hive ranks: horizontal low/high lanes, never an unbroken vertical wall.
                int side = Round % 2 == 0 ? 1 : -1;
                for (int i = 0; i < 3; i++)
                    Emit(AscendantShape.Seed, floor + new Vector2(-side * (300 + i * 45), -(i == 1 ? 92 : 15)), new Vector2(side * 4.4f, 0),
                        delay: 75 + i * 12, duration: brief ? 90 : 115, width: 18, mark: law);
                break;
            default:
                Vector2 center = floor + new Vector2(0, -155);
                Emit(AscendantShape.Ring, center, Vector2.Zero, delay: 75, duration: brief ? 95 : 130, width: 14, length: 420,
                    angle: (target - center).ToRotation(), gap: 1.05f, heavy: true, mark: law);
                break;
        }
    }
    private void NoDay(Player player)
    {
        NPC.velocity *= .93f;
        if (FinalActive)
        {
            if (Timer >= 1 && Timer <= 641 && (Timer - 1) % 160 == 0) FateLaw((Timer - 1) / 160, player.Center, true);
            return;
        }
        int law = Phase == 2 && ChoiceResolved ? SelectedLaw : Move;
        if (Phase == 0)
        {
            if (Timer is 1 or 171) FateLaw(law, player.Center);
            return;
        }
        if (Authority && Timer == 1)
        { TraceCount = 1; Trace[0] = WorldPoint(player.Center); SpawnNode(AscendantNodeKind.Knot, 0); NPC.netUpdate = true; }
        if (Authority && Timer >= 6 && Timer <= 180 && Timer % 6 == 0 && TraceCount < Trace.Length)
        { Trace[TraceCount++] = WorldPoint(player.Center); NPC.netUpdate = true; }
        if (Timer == 180) SpawnTrace(72, 98);
        if (Timer == 190) FateLaw(law, player.Center);
        if (Timer == 300 && !KnotBroken) SpawnTrace(72, 99);
        if (Phase == 2 && Timer == 360) FateLaw((law + 1) % 5, player.Center);
    }
    private void FateLaw(int law, Vector2 target, bool brief = false)
    {
        Vector2 floor = FloorNear(target + new Vector2(0, 20));
        switch (law)
        {
            case 0: // Backburn retains a sampled route; old lines are never rebuilt from a moving target.
                if (TraceCount >= 4 && !FinalActive)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 from = Trace[i * (TraceCount - 1) / 3], to = Trace[(i + 1) * (TraceCount - 1) / 3];
                        if (Vector2.DistanceSquared(from, to) < 100) to = from + new Vector2(55, 0);
                        Emit(AscendantShape.Beam, from, Vector2.Zero, delay: 80 + i * 12, duration: 28, width: 16, length: Vector2.Distance(from, to), angle: (to - from).ToRotation(), mark: law);
                    }
                }
                else for (int i = -1; i <= 1; i++)
                    Emit(AscendantShape.Pillar, floor + new Vector2(i * 150, 0), Vector2.Zero, delay: 75 + (i + 1) * 15, duration: 28, width: 36, length: 145, mark: law);
                break;
            case 1: // Mechanical rail tells the full hitbox, not just a tiny aiming dot.
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 start = target + new Vector2(i * 185 - 140, -400);
                    Emit(AscendantShape.Beam, start, Vector2.Zero, delay: 85, duration: 25, width: 20, length: 850, angle: MathHelper.PiOver2 - .18f, heavy: i == 0, mark: law);
                }
                break;
            case 2: // Hunger mouths commit first; stepping out is sufficient.
                for (int i = -1; i <= 1; i++)
                    Emit(AscendantShape.Pillar, floor + new Vector2(i * 170, 0), Vector2.Zero, delay: 90 + (i + 1) * 12,
                        duration: 25, width: 68, length: 145, heavy: true, mark: law);
                break;
            case 3:
                Vector2 center = target + new Vector2(0, -175);
                Emit(AscendantShape.Ring, center, Vector2.Zero, delay: 85, duration: brief ? 110 : 145, width: 18, length: 620,
                    angle: (target - center).ToRotation(), gap: .9f, heavy: true, mark: law);
                break;
            default: // Tide curtains alternate low/high; every row has a deliberately wide fixed opening.
                for (int wave = 0; wave < 2; wave++)
                    for (int row = 0; row < 4; row++)
                    {
                        if (row == (wave == 0 ? 2 : 1)) continue;
                        int side = wave == 0 ? 1 : -1;
                        Emit(AscendantShape.Seed, target + new Vector2(-side * 350, (row - 1.5f) * 110), new Vector2(side * 5, 0),
                            delay: 75 + wave * 65, duration: 135, width: 28, mark: law);
                    }
                break;
        }
    }
}
