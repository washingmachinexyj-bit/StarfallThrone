using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Bosses;

public abstract partial class VoyageBossNPC
{
    public bool CityAlive => Part(0, VoyagePart.LeftCity) != null || Part(1, VoyagePart.RightCity) != null;
    public bool IsBroken(int slot) => slot is >= 0 and < 32 && (BrokenMask & (1u << slot)) != 0;
    public int BudgetPartsAlive()
    {
        int count = 0;
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is VoyageDeviceNPC d && d.OwnedBy(this) && d.BudgetPart) count++;
        return count;
    }
    public NPC Part(int slot, VoyagePart? role = null)
    {
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is VoyageDeviceNPC d && d.OwnedBy(this) && d.Slot == slot && (!role.HasValue || d.Role == role)) return n;
        return null;
    }
    public int PartsOfRole(VoyagePart role)
    {
        int count = 0;
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is VoyageDeviceNPC d && d.OwnedBy(this) && d.Role == role) count++;
        return count;
    }
    public void NotifyPartDestroyed(VoyageDeviceNPC part)
    {
        if (!IsServer) return;
        if (!part.Temporary && part.Slot is >= 0 and < 32) BrokenMask |= 1u << part.Slot;
        // Taking apart a machine has an immediate, visible effect, in addition to lost attacks.
        if (part.Role is VoyagePart.Anchor or VoyagePart.NeuralNode or VoyagePart.Geometry or VoyagePart.Gate ||
            BossIndex == 10 && part.Slot is 2 or 5)
            Stagger = Math.Max(Stagger, 65);
        NPC.netUpdate = true;
    }
    private bool InitializeAnatomy()
    {
        int total = AggregateLifeMaximum;
        switch (BossIndex)
        {
            case 2:
            case 10:
                // The controller itself adds NO health: 20% head + eight 10% sections.
                int small = total / 10;
                for (int i = 0; i < 9; i++) SpawnPart(VoyagePart.Segment, i, NPC.Center - new Vector2(i * 78, 0), i == 0 ? total - small * 8 : small, true);
                NPC.dontTakeDamage = true;
                break;
            case 3: Persistent(VoyagePart.NeuralNode, 4, .018f); break;
            case 4: Persistent(VoyagePart.Hangar, 2, .025f); break;
            case 5: Persistent(VoyagePart.Anchor, 2, .022f); break;
            case 6: Persistent(VoyagePart.Bulkhead, 3, .018f); break;
            case 7: Persistent(VoyagePart.Mirror, 3, .018f); break;
            case 8:
                SpawnPart(VoyagePart.RedEye, 0, NPC.Center + new Vector2(-280, 0), total / 2, true);
                SpawnPart(VoyagePart.BlueEye, 1, NPC.Center + new Vector2(280, 0), total - total / 2, true);
                NPC.dontTakeDamage = true;
                break;
            case 9: Persistent(VoyagePart.Weapon, 4, .024f); break;
            case 11: Persistent(VoyagePart.RootNode, 4, .018f); break;
            case 12: Persistent(VoyagePart.Geometry, 4, .022f); break;
            case 14: Persistent(VoyagePart.Sail, 3, .018f); break;
            case 15: Persistent(VoyagePart.Gate, 3, .02f); break;
            case 16:
                int left = (int)(2000000 * HealthScale), right = (int)(2000000 * HealthScale), array = (int)(2500000 * HealthScale);
                SpawnPart(VoyagePart.LeftCity, 0, NPC.Center + new Vector2(-320, 40), left, true);
                SpawnPart(VoyagePart.RightCity, 1, NPC.Center + new Vector2(320, 40), right, true);
                SpawnPart(VoyagePart.Array, 2, NPC.Center + new Vector2(0, -220), array, true);
                // Preserve total exactly even when multiplayer scaling rounds fractional integers.
                NPC.lifeMax = total - left - right - array;
                NPC.life = NPC.lifeMax;
                NPC.dontTakeDamage = true;
                break;
        }
        int required = BossIndex switch { 2 or 10 => 9, 3 or 9 or 11 or 12 => 4, 4 or 5 or 8 => 2, 6 or 7 or 14 or 15 or 16 => 3, _ => 0 };
        int created = 0;
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is VoyageDeviceNPC d && d.OwnedBy(this)) created++;
        if (created == required) return true;
        // Never silently erase millions of health when the global NPC table is full.
        Cleanup();
        NPC.lifeMax = NPC.life = total;
        NPC.dontTakeDamage = true;
        return false;
    }
    private void Persistent(VoyagePart role, int count, float fraction)
    {
        for (int i = 0; i < count; i++) SpawnPart(role, i, NPC.Center + (MathHelper.TwoPi * i / count).ToRotationVector2() * 260,
            (int)(AggregateLifeMaximum * fraction), false);
    }
    public NPC SpawnPart(VoyagePart role, int slot, Vector2 at, int health = 0, bool budget = false, int lifetime = 0)
    {
        if (!IsServer || PartsOfRole(role) >= (role == VoyagePart.Segment ? 9 : 8)) return null;
        int owned = 0;
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is VoyageDeviceNPC p && p.OwnedBy(this)) owned++;
        if (owned >= 20) return null;
        int id = NPC.NewNPC(NPC.GetSource_FromAI(), (int)at.X, (int)at.Y, ModContent.NPCType<VoyageDeviceNPC>(),
            ai0: NPC.whoAmI + 1, ai1: (int)role, ai2: slot);
        if (id < 0 || id >= Main.maxNPCs) return null;
        NPC part = Main.npc[id];
        VoyageDeviceNPC device = (VoyageDeviceNPC)part.ModNPC;
        device.Serial = Serial;
        device.Anchor = at;
        device.BudgetPart = budget;
        device.Lifetime = lifetime;
        device.Configured = true;
        part.Center = at;
        part.lifeMax = Math.Max(2000, health > 0 ? health : (int)(AggregateLifeMaximum * .009f));
        part.life = part.lifeMax;
        device.SizeBody();
        part.netUpdate = true;
        return part;
    }
    public Vector2 DeviceDestination(VoyageDeviceNPC part)
    {
        int slot = part.Slot;
        float cycle = Main.GameUpdateCount % 36000;
        float orbit = cycle * .008f + slot * MathHelper.TwoPi / 4;
        float side = slot % 2 == 0 ? -1 : 1;
        switch (part.Role)
        {
            case VoyagePart.Segment:
                bool split = BossIndex == 2 && (BrokenMask & 0x7e) != 0;
                int lower = split && slot >= 4 ? 4 : 0;
                NPC prior = null;
                for (int i = slot - 1; i >= lower; i--) if ((prior = Part(i, VoyagePart.Segment)) != null) break;
                if (BossIndex == 10 && Stage == VoyageStage.Attack && Move == 2 && slot % 3 == 1)
                    return Aim + new Vector2((slot - 4) * 90, -200 - MathF.Sin(Timer * .025f + slot) * 60);
                if (prior != null)
                    return prior.Center - (prior.velocity.LengthSquared() > 1 ? prior.velocity : NPC.velocity).SafeNormalize(Vector2.UnitX) * 82;
                if (lower == 4) return Aim + new Vector2(MathF.Cos(Timer * .023f) * 420, MathF.Sin(Timer * .023f) * 240);
                return NPC.Center;
            case VoyagePart.RedEye:
            case VoyagePart.BlueEye:
                if (Stage == VoyageStage.Attack && Move == 1)
                    return Aim + (Timer * .016f + (slot == 0 ? 0 : MathHelper.Pi)).ToRotationVector2() * 380;
                return NPC.Center + new Vector2(side * 280, slot == 0 ? 50 : -70);
            case VoyagePart.LeftCity: return NPC.Center + new Vector2(-320, 30 + MathF.Sin(cycle * .016f) * 18);
            case VoyagePart.RightCity: return NPC.Center + new Vector2(320, 30 - MathF.Sin(cycle * .016f) * 18);
            case VoyagePart.Array: return NPC.Center + new Vector2(0, -220);
            case VoyagePart.Hangar: return NPC.Center + new Vector2(side * 160, 40);
            case VoyagePart.Weapon:
                return NPC.Center + new Vector2(side * (190 + (slot >= 2 ? 65 : 0)), slot < 2 ? -85 : 115);
            case VoyagePart.Bulkhead: return NPC.Center + new Vector2(0, (slot - 1) * 185);
            case VoyagePart.NeuralNode: return NPC.Center + orbit.ToRotationVector2() * 230;
            case VoyagePart.Mirror:
                return NPC.Center + (cycle * .005f + slot * MathHelper.TwoPi / 3).ToRotationVector2() * (Move == 0 && Stage == VoyageStage.Attack ? 320 : 230);
            case VoyagePart.Sail: return NPC.Center + new Vector2((slot - 1) * 230, 190 + (slot == 1 ? 60 : 0));
            case VoyagePart.Anchor: return Arena + new Vector2(side * 440, 170);
            case VoyagePart.RootNode: return Arena + new Vector2((slot - 1.5f) * 250, 170 + (slot % 2) * 60);
            case VoyagePart.Geometry:
                return Arena + new Vector2(((slot + (int)NPC.ai[2]) % 4 - 1.5f) * 220, slot % 2 == 0 ? 130 : -190);
            case VoyagePart.Gate:
                return Phase == 2 ? Arena + (cycle * .004f + slot * MathHelper.TwoPi / 3).ToRotationVector2() * 380 :
                    Arena + new Vector2((slot - 1) * 420, slot % 2 == 0 ? -220 : 160);
            default: return part.Anchor;
        }
    }
}
