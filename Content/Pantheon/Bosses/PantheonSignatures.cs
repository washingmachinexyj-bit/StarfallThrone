using System;
using Microsoft.Xna.Framework;
using Terraria;
namespace StarfallThrone.Content.Pantheon.Bosses;

public abstract partial class PantheonBossNPC
{
    private void Perform(Player player)
    {
        switch (Index)
        {
            case 0: Aelion(player); break; case 1: Ophir(player); break; case 2: Uroth(player); break;
            case 3: Velmora(player); break; case 4: Melith(player); break; case 5: Neroth(player); break;
            case 6: Acheron(player); break; case 7: Ilythia(player); break; case 8: Dyad(player); break;
            case 9: Valther(player); break; case 10: Khaldran(player); break; case 11: Ephyra(player); break;
            case 12: Oranth(player); break; case 13: Thalor(player); break; case 14: Iriselle(player); break;
            case 15: Agnostos(player); break; case 16: Asterion(player); break;
        }
    }
    // Elastic descent, three distinct primordial forms, then inward recollection.
    private void Aelion(Player p)
    {
        if (Move == 0)
        {
            if (Timer == 1 || Timer == 210) Dash(Arena + new Vector2(Timer == 1 ? -100 : 100, 100));
            if (Timer == 95 || Timer == 305) Ring(NPC.Center, Phase == 2 ? 530 : 380, -MathHelper.PiOver2);
        }
        else if (Move == 1)
        {
            if (Timer == 1) Emit(PantheonShape.Star, NodePosition(0), new Vector2(6, 0), width: 30, mark: 0);
            if (Timer == 100) Beam(NodePosition(1), p.Center + new Vector2(0, 200), 1);
            if (Timer == 200) Fan(NodePosition(2), p.Center, 3 + Phase, .26f, mark: 2, arc: true);
        }
        else if (Timer == 1 || Timer == 190)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = Arena + (i * MathHelper.TwoPi / 3 + .3f).ToRotationVector2() * 430;
                Emit(PantheonShape.Arc, at, (Arena - at).SafeNormalize(Vector2.UnitX) * 4.4f, width: 25, length: 60, angle: .035f, mark: i);
            }
        }
    }
    // Committed eye aim and real cancellable mirror-to-mirror beam segments.
    private void Ophir(Player p)
    {
        if (Move == 0 && (Timer == 1 || Timer == 180)) Beam(NPC.Center, p.Center + (p.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 250, width: 34);
        if (Move == 1 && Timer == 1)
        {
            Vector2 a = NPC.Center;
            for (int i = 0; i < 3; i++) { Vector2 z = NodePosition(i); Beam(a, z, i, 75 + i * 60); a = z; }
            Beam(a, p.Center, 2, 255);
        }
        if (Move == 2)
        {
            if (Timer == 1) Dash(p.Center + new Vector2(p.direction * 170, 0));
            if (Timer == 140) { Beam(NPC.Center + new Vector2(-180, 0), Arena + new Vector2(-90, 260), 0); Beam(NPC.Center + new Vector2(180, 0), Arena + new Vector2(90, 260), 1); }
            if (Timer == 270 && Phase > 0) Fan(NPC.Center, p.Center, 3, .35f);
        }
    }
    // Long articulated serpent traverses committed rifts, then encloses a gapped interior.
    private void Uroth(Player p)
    {
        if (Move == 0)
        {
            if (Timer == 1 || Timer == 210) Dash(Arena + new Vector2(Timer == 1 ? 370 : -370, 70));
            if (Timer == 115 || Timer == 315) Fan(NPC.Center, p.Center, 3 + Phase, .3f, 5);
        }
        else if (Move == 1)
        {
            float angle = Timer * .025f; Fly(Arena + angle.ToRotationVector2() * new Vector2(340, 180), 16);
            if (Timer == 1 && Chosen < 0) Ring(Arena, -440, MathHelper.PiOver2, 20, duration: 210);
            if (Timer == 235 && Chosen < 0) Ring(Arena, -360, -MathHelper.PiOver2, 20, duration: 110);
        }
        else
        {
            Fly(Arena + new Vector2(MathF.Sin(Timer * .017f) * 320, -200), 13);
            if (Timer == 1 || Timer == 145 || Timer == 290)
                for (int i = 0; i < 3; i++) Emit(PantheonShape.Arc, NodePosition(i) - new Vector2(0, 200), new Vector2((i - 1) * 1.5f, 4), length: 40, angle: .02f, width: 30, mark: i);
        }
    }
    // Illusions have no collision; the true, rhythmically pulsing thought can always be attacked.
    private void Velmora(Player p)
    {
        if (Timer == 1 || Timer == 170)
        {
            if (!IsBroken(0))
                for (int i = -1; i <= 1; i++) Emit(PantheonShape.Echo, Arena + new Vector2(i * 250, -150), (Aim - NPC.Center).SafeNormalize(Vector2.UnitY) * 5, duration: 120, width: 22, damage: 0, mark: 0);
            if (Move == 0) Fan(NPC.Center, p.Center, 3, .3f);
            else if (Move == 1)
            {
                Vector2 from = Arena + new Vector2(Timer == 1 ? -320 : 320, -150);
                Fan(from, Aim, 4, .24f, 6, arc: true);
            }
            else
            {
                Beam(NPC.Center, Aim + new Vector2(0, 230));
                if (Phase > 0 && !IsBroken(0)) Emit(PantheonShape.Echo, Arena + new Vector2(250, -180), new Vector2(-5, 3), width: 26, damage: 0, mark: 0);
            }
        }
        if (Move == 1 && Timer == 290 && !IsBroken(0)) Fan(Arena - new Vector2(-320, 150), Aim, 3, .3f, 4, mark: 0);
    }
    // Supply drones are targetable and actually move; surviving shipments become harvest hazards.
    private void Melith(Player p)
    {
        if (Move == 0 && (Timer == 1 || Timer == 160))
            for (int i = 0; i < 3; i++) Fan(NodePosition(i), p.Center, 2 + Phase, .3f, 4, i, true);
        if (Move == 1)
        {
            Fly(Arena + new Vector2(MathF.Sin(Timer * .016f) * 180, -240), 7);
            if (Timer == 100 || Timer == 255) Fan(NPC.Center, p.Center, 3, .38f, 6);
        }
        if (Move == 2 && Timer is 1 or 105 or 210)
        {
            int i = Timer / 105;
            Fan(NodePosition(i), NodePosition(i) - new Vector2(0, 300), 5, .34f, 5, i, true);
        }
        if (Timer == 320)
            for (int i = 0; i < 3; i++) if (!IsBroken(i)) Fan(NPC.Center, p.Center, 2, .24f, 7, i);
    }
    // River has low bounded current; ferries and oar are independent attacks, not body contact.
    private void Neroth(Player p)
    {
        if (Move == 0 && (Timer == 1 || Timer == 180))
        {
            for (int s = -1; s <= 1; s += 2)
                Emit(PantheonShape.Star, Arena + new Vector2(-s * 440, s * 90), new Vector2(s * 5, 0), width: 38, duration: 150, mark: s < 0 ? 0 : 1);
        }
        if (Move == 1 && (Timer == 1 || Timer == 210))
        {
            Vector2 point = p.Center;
            for (int i = 0; i < 4; i++)
            {
                Vector2 from = point + (i * MathHelper.PiOver2).ToRotationVector2() * 340;
                Emit(PantheonShape.Arc, from, (point - from).SafeNormalize(Vector2.UnitX) * 3.8f, length: 55, angle: .03f, width: 24, mark: i % 2);
            }
        }
        if (Move == 2)
        {
            if (Timer == 1) Beam(Arena + new Vector2(-420, 80), Arena + new Vector2(420, 80), 0, width: 36);
            if (Timer == 150) Dash(p.Center + new Vector2(180, 0));
            if (Timer == 265) Ring(NPC.Center, 320, MathHelper.PiOver2, 1);
        }
    }
    // Moving finite gate reverses by move; widening the opening is a genuine objective reward.
    private void Acheron(Player p)
    {
        int direction = Move == 1 ? -1 : 1;
        if (Timer > 75) Fly(Arena + new Vector2(direction * Math.Clamp((Timer - 75) * 1.8f - 270, -270, 270), 0), 5);
        if (Timer == 1 || Timer == 210)
        {
            Emit(PantheonShape.Wall, Arena + new Vector2(-direction * 440, Move == 2 ? (Timer == 1 ? -90 : 90) : 0),
                new Vector2(direction * 3.3f, 0), duration: 190, width: 32, length: 680, gap: Chosen >= 0 ? 185 : 110, mark: Timer == 1 ? 0 : 1);
        }
        if (Timer == 135 && Move == 2)
            for (int i = 0; i < 2; i++) Fan(PartPosition(i), p.Center, 3, .3f, 5, i);
    }
    // Directed graph edges are tied to their breakable vertex. Destroying one removes both incident edges.
    private void Ilythia(Player p)
    {
        if (Move == 0 && Timer == 1)
            for (int i = 0; i < 3; i++) Beam(NodePosition(i), NodePosition((i + 1) % 3), i, 100 + i * 20, 22, 75);
        if (Move == 1 && Timer is 1 or 115 or 230)
        {
            int i = Timer / 115; if (!IsBroken(i)) Dash(NodePosition(i));
            Fan(NodePosition(i), p.Center, 2 + Phase, .35f, 6, i);
        }
        if (Move == 2 && Timer == 1)
            for (int i = 0; i < 3; i++)
            {
                Vector2 from = NodePosition(i), next = NodePosition((i + 1) % 3);
                Emit(PantheonShape.Star, from, (next - from).SafeNormalize(Vector2.UnitX) * 5, 75 + i * 50, width: 30, mark: i);
            }
    }
    // The two independently orbiting, damageable eyes exchange offense and support; eclipse can be cancelled.
    private void Dyad(Player p)
    {
        if (Move != 2)
        {
            int active = Move == 0 ? 0 : 1, support = 1 - active;
            if (Timer == 1 || Timer == 180)
                Fan(PartPosition(active), p.Center, active == 0 ? 3 : 4, .3f, 7, active, active == 1);
            if (Timer == 90 || Timer == 260)
            {
                if (support == 1) Ring(PartPosition(support), 310, MathHelper.PiOver2, support);
                else Beam(Arena + new Vector2(-350, 110), Arena + new Vector2(350, 110), support);
            }
        }
        else if (Timer == 1 && Chosen < 0)
        {
            Ring(Arena - new Vector2(0, 100), 610, MathHelper.PiOver2, 20, 150, 180);
            Beam(Arena + new Vector2(-350, -230), Arena + new Vector2(350, -230), 0, 90);
        }
    }
    private void Law(int law, Player p)
    {
        if (IsBroken(law)) return;
        switch (law)
        {
            case 0: Beam(NodePosition(0), Arena + new Vector2(270, 100), 0, width: 40); break;
            case 1:
                Beam(Arena + new Vector2(-270, -230), Arena + new Vector2(-120, 160), 1);
                Beam(Arena + new Vector2(270, -230), Arena + new Vector2(120, 160), 1); break;
            case 2: Ring(NodePosition(2), 380, -MathHelper.PiOver2, 2); break;
            case 3: Emit(PantheonShape.Column, p.Center + new Vector2(0, 180), Vector2.Zero, width: 70, length: 510, damage: 1.4f, mark: 3); break;
        }
    }
    private void Valther(Player p)
    {
        if (Timer == 90) { Law(Move, p); if (Phase > 0) Law((Move + 1) % 4, p); }
        if (Timer == 255) { Law((Move + 2) % 4, p); if (Phase == 2) Law((Move + 3) % 4, p); }
    }
    // Segmented dragon with counter-rotating second spine in phase three and ventable heat nodes.
    private void Khaldran(Player p)
    {
        Fly(Arena + new Vector2(MathF.Sin(Timer * .018f) * 340, MathF.Cos(Timer * .018f) * 140 - 100), 15);
        if (Move == 0 && Timer is 1 or 120 or 240)
        {
            int mark = Timer / 120;
            Fan(PartPosition(mark * 3), p.Center, 3 + Phase, .26f, 6, mark);
        }
        if (Move == 1 && Timer == 1)
            for (int i = 0; i < 3; i++) Emit(PantheonShape.Column, Arena + new Vector2((i - 1) * 230, 150), Vector2.Zero,
                90 + i * 75, 35, 80, 440, damage: 1.4f, mark: i);
        if (Move == 2 && Timer is 1 or 190)
        {
            for (int i = 0; i < 3; i++) Beam(NodePosition(i), NodePosition(i) + new Vector2((i - 1) * 150, -350), i);
            if (Phase == 2) Fan(PartPosition(7), p.Center, 3, .4f, 5);
        }
    }
    // Keep seed husks as beam cover, or break them before they hatch: an actual tactical tradeoff.
    private void Ephyra(Player p)
    {
        if (Move == 0 && (Timer == 1 || Timer == 190)) Fan(NPC.Center, p.Center, 5 + Phase, .3f, 5, arc: true);
        if (Move == 1)
        {
            if (Timer == 1 || Timer == 145) Beam(NPC.Center, p.Center + new Vector2(0, 230), width: 40, duration: 60);
            if (Timer == 275) for (int i = 0; i < 3; i++) Fan(NodePosition(i), p.Center, 3, .4f, 6, i, true);
        }
        if (Move == 2 && Timer is 1 or 155 or 270)
        {
            int i = Timer == 1 ? 0 : Timer == 155 ? 1 : 2;
            Emit(PantheonShape.Column, NodePosition(i) + new Vector2(0, 100), Vector2.Zero, width: 42, length: 400, mark: i);
        }
    }
    private void Oranth(Player p)
    {
        if (Move == 0 && Timer == 1)
            for (int i = 0; i < 3; i++) Emit(PantheonShape.Column, Arena + new Vector2((i - 1) * 230, 140), Vector2.Zero,
                90 + i * 75, 35, 95, 430, damage: 1.4f, mark: i);
        if (Move == 1 && Timer is 1 or 170)
        {
            Beam(NPC.Center, p.Center + new Vector2(0, 240), width: 42, duration: 60);
            if (Timer == 170) for (int i = 0; i < 3; i++) Emit(PantheonShape.Star, NodePosition(i), new Vector2((1 - i) * 4, -2), width: 45, mark: i);
        }
        if (Move == 2)
        {
            if (Timer == 1) Emit(PantheonShape.Gravity, NodePosition(1), Vector2.Zero, duration: 240, length: 330, damage: 0, mark: 1);
            if (Timer == 130 || Timer == 275) Fan(NPC.Center, p.Center, 4, .28f, 5);
        }
    }
    private void Thalor(Player p)
    {
        if (Move == 0)
        {
            if (Timer is 1 or 190) Dash(p.Center + new Vector2(Timer == 1 ? 230 : -230, -40));
            if (Timer is 108 or 298) Ring(NPC.Center, 350, MathHelper.PiOver2);
        }
        if (Move == 1 && Timer is 1 or 200)
            for (int sign = -1; sign <= 1; sign += 2)
                Emit(PantheonShape.Wall, Arena + new Vector2(sign * 430, Timer == 1 ? 60 : -60), new Vector2(-sign * 3.8f, 0),
                    duration: 155, width: 24, length: 580, gap: 120, mark: sign == -1 ? 0 : 2);
        if (Move == 2)
        {
            if (Timer == 1 && Chosen < 0) Emit(PantheonShape.Gravity, Arena, Vector2.Zero, duration: 250, length: 400, damage: 0, mark: 20);
            if (Timer is 100 or 255) Fan(NPC.Center, p.Center, 5, .23f, 8, arc: true);
        }
    }
    private void Spectrum(int color, Player p)
    {
        if (IsBroken(color)) return;
        Vector2 at = NodePosition(color);
        switch (color)
        {
            case 0: Fan(at, p.Center, 3, .32f, 6, color); break;
            case 1: Beam(at, p.Center + new Vector2(150, 100), color); break;
            case 2: Ring(at, 300, MathHelper.PiOver2, color, duration: 85); break;
            case 3: Emit(PantheonShape.Column, Arena + new Vector2(-160, 160), Vector2.Zero, duration: 35, width: 50, length: 450, mark: color); break;
            case 4: Fan(at, p.Center, 3, .35f, 5, color, true); break;
            case 5: Emit(PantheonShape.Wall, Arena + new Vector2(-350, 0), new Vector2(4, 0), duration: 95, width: 22, length: 550, gap: 125, mark: color); break;
            case 6: Beam(at, Arena + new Vector2(-230, 150), color); Beam(at, Arena + new Vector2(230, 150), color); break;
        }
    }
    private void Iriselle(Player p)
    {
        if (Move == 0 && Timer is 1 or 110 or 220) Spectrum((Timer / 110 + Round) % 7, p);
        else if (Move == 1 && Timer is 1 or 210) { Spectrum((Round + Timer / 200) % 7, p); if (Phase > 0) Spectrum((Round + 3 + Timer / 200) % 7, p); }
        else if (Move == 2 && Timer >= 1 && Timer <= 631 && (Timer - 1) % 105 == 0) Spectrum((Timer - 1) / 105, p);
    }
    private void Agnostos(Player p)
    {
        if (Move == 0 && Timer is 1 or 180)
        {
            int borrowed = (Round + (Timer == 1 ? 0 : 1)) % 3;
            if (borrowed == 0) Ring(Arena, 420, MathHelper.PiOver2, 0);
            if (borrowed == 1) Fan(NPC.Center, p.Center, 4, .3f, 5, 1, true);
            if (borrowed == 2) Beam(Arena + new Vector2(-340, -180), Arena + new Vector2(220, 130), 2);
        }
        if (Move == 1 && Timer is 95 or 200 or 305)
        {
            int order = ((Timer - 95) / 105 + Math.Max(0, Chosen)) % 3;
            Vector2 from = NodePosition(order); Beam(from, Arena + new Vector2((order - 1) * 240, 200), order);
        }
        if (Move == 2 && Timer == 1)
        {
            // Fully visible written strokes; deleting a page cancels that stroke and its successor.
            Vector2[] path = { Arena + new Vector2(-350, -200), Arena + new Vector2(-130, 160), Arena + new Vector2(150, -150), Arena + new Vector2(350, 150) };
            for (int i = 0; i < 3; i++) Beam(path[i], path[i + 1], i, 90 + i * 85, 28, 45);
        }
    }
    private void AuthorityLaw(int law, Player p)
    {
        if (IsBroken(law)) return;
        if (law == 0)
        {
            for (int i = -1; i <= 1; i += 2) Emit(PantheonShape.Column, Arena + new Vector2(i * 220, 170), Vector2.Zero, width: 55, length: 400, mark: 0, duration: 65);
        }
        else if (law == 1)
        {
            Beam(PartPosition(0), Arena + new Vector2(-70, 250), 1, width: 32);
            Beam(PartPosition(1), Arena + new Vector2(70, 250), 1, width: 32);
        }
        else Ring(Arena, 530, MathHelper.PiOver2, 2, duration: 150);
    }
    private void Asterion(Player p)
    {
        if (Phase > 0) Fly(Arena + new Vector2(MathF.Sin(Timer * .01f) * 150, -230), 7);
        if (Finale)
        {
            // Once per encounter, no health lock. Killing during the ritual remains valid.
            if (Timer is 1 or 241 or 481) AuthorityLaw((Timer - 1) / 240, p);
            if (Timer == 701) Ring(Arena, 620, MathHelper.PiOver2, delay: 100, duration: 85);
            return;
        }
        if (Timer == 90 || Timer == 290)
        {
            int law = (Move + Math.Max(0, Chosen) + (Timer == 90 ? 0 : 1)) % 3;
            AuthorityLaw(law, p);
            if (Phase > 0) AuthorityLaw((law + 1) % 3, p);
        }
    }
}
