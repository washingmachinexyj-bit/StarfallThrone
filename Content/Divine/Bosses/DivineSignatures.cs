using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace StarfallThrone.Content.Divine.Bosses;

public abstract partial class DivineBossNPC
{
    private Vector2 FloorNear(Vector2 at)
    {
        // Read terrain only. Use a nearby standing surface/platform, not a damaging arena floor or tile edits.
        int x = Math.Clamp((int)at.X / 16, 10, Math.Max(10, Main.maxTilesX - 11));
        int y0 = Math.Clamp((int)at.Y / 16, 10, Math.Max(10, Main.maxTilesY - 41));
        for (int y = y0; y < Math.Min(Main.maxTilesY - 10, y0 + 36); y++)
        {
            Tile t = Main.tile[x, y];
            if (t.HasUnactuatedTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])) return WorldPoint(new Vector2(at.X, y * 16));
        }
        return WorldPoint(at + new Vector2(0, 26));
    }
    private void Miroen(Player p)
    {
        NPC.velocity *= .9f;
        if (Move == 0)
        {
            if (Timer is 1 or 51 or 101)
            {
                Vector2 target = Timer == 101 ? p.Center : Aim;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 v = (target - NPC.Center).SafeNormalize(Vector2.UnitY) * 3.8f + new Vector2(i * 1.4f, -2.8f);
                    Emit(DivineShape.Drop, NPC.Center + new Vector2(i * 24, 0), v, delay: 45, duration: 100, width: 13);
                }
            }
            if (Phase > 0 && Timer is 15 or 70 or 125)
                Emit(DivineShape.Pillar, FloorNear(p.Bottom), Vector2.Zero, delay: 75, duration: 24, width: 30, length: 104);
        }
        else if (Move == 1)
        {
            if (Timer is 1 or 56 or 111)
            {
                int beat = (Timer - 1) / 55;
                float direction = Main.expertMode && beat % 2 == 1 ? -1 : 1;
                Vector2 floor = FloorNear(Aim + new Vector2(0, 20));
                // Low / overhead / low: ordinary jump clears the 18px low blade, standing clears the upper blade.
                float elevation = beat == 1 ? 90 : 16;
                Emit(DivineShape.Crown, new Vector2(Aim.X - direction * 360, floor.Y - elevation), new Vector2(direction * 4.6f, 0), delay: 55, duration: 160, width: 18);
            }
        }
        else if (Phase < 2)
        {
            if (Timer == 1)
            {
                Vector2 floor = FloorNear(Aim + new Vector2(0, 20));
                Emit(DivineShape.Pillar, floor, Vector2.Zero, delay: 75, duration: 24, width: 64, length: 70, heavy: true);
                for (int sign = -1; sign <= 1; sign += 2)
                    Emit(DivineShape.Wave, floor - new Vector2(0, 10), new Vector2(sign * 4, 0), delay: 100, duration: 130, width: 16);
            }
        }
        else
        {
            if (Timer == 1)
            {
                // Four numbered fixed columns, at least 42 ticks apart, maximum one active at a time.
                int[] order = Main.masterMode ? new[] { 2, 0, 3, 1 } : new[] { 0, 1, 2, 3 };
                for (int i = 0; i < 4; i++)
                {
                    Vector2 floor = FloorNear(Aim + new Vector2((order[i] - 1.5f) * 110, 20));
                    Emit(DivineShape.Pillar, floor, Vector2.Zero, delay: 60 + i * 42, duration: 20, width: 30, length: 130, mark: i + 1);
                }
            }
            // Aiming lines precede each committed low fly-by. Contact is disabled outside these two runs.
            if (Authority && Timer is 145 or 200)
            {
                Aim = WorldPoint(p.Center); Origin = NPC.Center;
                Emit(DivineShape.Beam, Origin, Vector2.Zero, delay: 30, duration: 1, width: 2, length: 0, angle: (Aim - Origin).ToRotation());
                NPC.netUpdate = true;
            }
            if (Timer is 175 or 230) NPC.velocity = (Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * 7;
            else if (Timer >= 175 && Timer < 190 || Timer >= 230 && Timer < 245) NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitX) * 7;
        }
    }
    private void Velsa(Player p)
    {
        NPC.velocity *= .92f;
        if (Phase > 0 && Timer == 1)
        {
            float sideOffset = Main.expertMode ? 440 : 300;
            for (int side = 0; side < 2; side++)
                Emit(DivineShape.Offering, WorldPoint(Arena + new Vector2(side == 0 ? -sideOffset : sideOffset, 10)), Vector2.Zero, delay: 30, duration: 150, width: 50, mark: side);
        }
        if (Move == 0)
        {
            if (Timer is 1 or 76 or 151)
            {
                Vector2 lockAt = p.Center;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 from = WorldPoint(new Vector2(lockAt.X + i * 140, lockAt.Y - 460));
                    Emit(DivineShape.Beam, from, Vector2.Zero, delay: 65, duration: 22, width: 20, length: 780, angle: MathHelper.PiOver2, heavy: i == 0);
                }
            }
        }
        else if (Move == 1)
        {
            if (Timer is 1 or 61 or 121)
            {
                int beat = (Timer - 1) / 60;
                float sign = ((int)NPC.ai[2] / 3 % 2) == 0 ? 1 : -1;
                Vector2 baseAt = WorldPoint(Arena + new Vector2(-sign * 510, 30 - beat * 65));
                Emit(DivineShape.Wave, baseAt, new Vector2(sign * 6, 0), delay: 65, duration: 170, width: 32);
                if (Phase == 2 && beat == 2)
                    Emit(DivineShape.Pillar, WorldPoint(Arena + new Vector2(0, 100)), Vector2.Zero, delay: 105, duration: 35, width: 120, length: 600, heavy: true);
            }
        }
        else
        {
            if (Timer == 1 || Phase == 2 && Timer == 116)
            {
                float gapX = Math.Clamp(p.Center.X, Arena.X - 280, Arena.X + 280);
                float y = Arena.Y + (Timer == 1 ? -60 : 100);
                // Two chain spans leave a 170px opening. Warning is stationary; damage never chases the gap.
                Emit(DivineShape.Beam, new Vector2(Arena.X - 620, y), Vector2.Zero, delay: 90, duration: 55, width: 24, length: Math.Max(0, gapX - 85 - (Arena.X - 620)));
                Emit(DivineShape.Beam, new Vector2(gapX + 85, y), Vector2.Zero, delay: 90, duration: 55, width: 24, length: Math.Max(0, Arena.X + 620 - gapX - 85));
            }
            if (Phase == 2 && Timer == 65)
                for (int i = -3; i <= 3; i++)
                    if (i != 0) Emit(DivineShape.Drop, Arena + new Vector2(i * 130, -420), new Vector2(0, 4), delay: 80, duration: 120, width: 20);
        }
    }
    private void NoDay(Player p)
    {
        NPC.velocity *= .93f;
        if (Move == 0 || Move == 2 && Phase == 0 || Move == 3 && Phase < 2)
        {
            if (Timer is 1 or 86)
            {
                Vector2 target = p.Center;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 from = WorldPoint(target + new Vector2(i * 220, -560));
                    Emit(DivineShape.Beam, from, Vector2.Zero, delay: 70 + (i + 1) * 12, duration: 25, width: 24, length: 1000, angle: MathHelper.PiOver2);
                }
                // The gaze commits at spawn. No beam continuously tracks a player while active.
                Emit(DivineShape.Beam, NPC.Center, Vector2.Zero, delay: 110, duration: 25, width: 30, length: 1300, angle: (target - NPC.Center).ToRotation(), heavy: true);
            }
        }
        else if (Move == 1)
        {
            if (Timer is 1 or 86)
                Emit(DivineShape.Ring, Arena, Vector2.Zero, delay: 65, duration: 150, width: 12, length: 1050,
                    angle: (p.Center - Arena).ToRotation(), gap: .85f, turn: Phase == 3 ? .004f : .002f);
        }
        else if (Move == 2)
        {
            if (Authority && Timer <= 120 && (Timer == 1 || Timer % 5 == 0) && TraceCount < 25)
            {
                Vector2 point = WorldPoint(p.Center);
                // Bound recorded spatial extent; very large external teleports cannot make world-spanning lines.
                if (TraceCount > 0 && Vector2.Distance(point, Trace[TraceCount - 1]) > 160)
                    point = Trace[TraceCount - 1] + (point - Trace[TraceCount - 1]).SafeNormalize(Vector2.UnitX) * 160;
                Trace[TraceCount++] = point; NPC.netUpdate = true;
            }
            if (Timer == 121 && Authority)
            {
                SpawnTrace(65);
                if (Main.masterMode) SpawnTrace(115);
                if (Phase == 3)
                    Emit(DivineShape.Ring, Arena, Vector2.Zero, delay: 95, duration: 150, width: 12, length: 950, angle: (p.Center - Arena).ToRotation(), gap: 1.0f);
            }
        }
        else
        {
            // Three numbered, fully disclosed safe bearings; all attacks are already taught in prior moves.
            if (Timer == 1)
            {
                for (int beat = 0; beat < 3; beat++)
                {
                    float gap = -MathHelper.PiOver2 + (beat - 1) * .95f;
                    Emit(DivineShape.Ring, Arena, Vector2.Zero, delay: 90 + beat * 75, duration: 78, width: 12, length: 720, angle: gap, gap: 1.0f, mark: beat + 1);
                }
            }
            if (Phase >= 2 && Timer == 90)
            {
                SpawnTrace(90);
                for (int i = -1; i <= 1; i += 2)
                    Emit(DivineShape.Beam, Arena + new Vector2(i * 420, -430), Vector2.Zero, delay: 80, duration: 22, width: 20, length: 820, angle: MathHelper.PiOver2);
            }
        }
    }
    public Projectile SpawnTrace(int delay)
    {
        if (TraceCount < 2) return null;
        Projectile shot = Emit(DivineShape.Trace, Trace[0], Vector2.Zero, delay: delay, duration: 84, width: 20, mark: delay > 100 ? 2 : 1);
        if (shot?.ModProjectile is DivineHazard h)
        {
            h.Count = TraceCount; Array.Copy(Trace, h.Points, TraceCount); shot.netUpdate = true;
        }
        return shot;
    }
}
