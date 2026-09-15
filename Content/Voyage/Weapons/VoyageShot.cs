#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Weapons;

public enum VoyageShotKind
{
    Swing, Spear, Shockwave, Cargo, GelMine, Beam, Saw, OrbitRock, Pulse, Link, Arrow, Bee, Anchor,
    Clamp, PortalBurst, OrbitArrow, BlueBlade, MagnetBurst, Fleet, Seed, Bloom, Pillar, CoreRocket,
    Tide, Seeking, GuideArrow, StarBall, FlameArc, Shard, ChainSpear, Mirror, CrystalRing, Escort, Tail
}

/// <summary>Finite attack controllers. AI slots carry the stable weapon id, shape and variant.</summary>
public class VoyageShot : ModProjectile, IVoyageWeaponProjectile
{
    public int WeaponIndex => Math.Clamp((int)Projectile.ai[0], 0, 35);
    public VoyageShotKind Kind => (VoyageShotKind)(int)Projectile.ai[1];
    public bool Secondary => Projectile.ai[2] >= 100;
    public int Variant => (int)Projectile.ai[2] % 100;
    public int Age, Delay, Target = -1, TargetType, ParentSlot = -1, ParentIdentity = -1, ParentType;
    public Vector2 Anchor, Direction, Focus;
    public float Range = 300;
    public bool ImpactDone;
    public int ActiveAge => Age - Delay;
    private bool Owner => Projectile.owner == Main.myPlayer;
    private Player Player => Main.player[Projectile.owner];
    public bool Attached => Kind is VoyageShotKind.Swing or VoyageShotKind.Spear or VoyageShotKind.ChainSpear or VoyageShotKind.Clamp;
    public override string Texture => VoyageArsenal.Art(0);
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18; Projectile.friendly = true; Projectile.ignoreWater = true;
        Projectile.tileCollide = true; Projectile.penetrate = 1; Projectile.timeLeft = 150;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    private void Configure()
    {
        Vector2 center = Projectile.Center;
        Projectile.DamageType = VoyageArsenal.Class(WeaponIndex);
        if (Attached)
        {
            Projectile.timeLeft = Kind == VoyageShotKind.ChainSpear ? 42 : Kind == VoyageShotKind.Spear ? 34 : 30;
            Projectile.tileCollide = false; Projectile.penetrate = -1; Range = Kind == VoyageShotKind.Swing ? 185 : 340;
            if (Kind == VoyageShotKind.ChainSpear) { Range = 440; Projectile.localNPCHitCooldown = 10; }
            if (WeaponIndex is 28 or 32) Range = 230;
        }
        if (Kind is VoyageShotKind.Beam or VoyageShotKind.Mirror)
        { Projectile.tileCollide = false; Projectile.penetrate = -1; Delay = 12; Projectile.timeLeft = 28; Range = WeaponIndex == 33 ? 1400 : 920; }
        if (Kind == VoyageShotKind.Beam && WeaponIndex == 25) Range = 240;
        if (Kind is VoyageShotKind.Saw or VoyageShotKind.BlueBlade or VoyageShotKind.Tide or VoyageShotKind.CrystalRing)
        { Projectile.penetrate = -1; Projectile.timeLeft = 125; Projectile.localNPCHitCooldown = 24; Range = 390; }
        if (Kind == VoyageShotKind.Anchor) { Projectile.penetrate = -1; Projectile.timeLeft = 100; Projectile.localNPCHitCooldown = 26; Range = 420; }
        if (Kind is VoyageShotKind.PortalBurst or VoyageShotKind.Fleet or VoyageShotKind.Link or VoyageShotKind.Bloom)
        { Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = Kind == VoyageShotKind.Fleet ? 78 : 55; }
        if (Kind == VoyageShotKind.OrbitRock) { Projectile.tileCollide = false; Projectile.timeLeft = 105; }
        if (Kind == VoyageShotKind.OrbitArrow) Projectile.tileCollide = false;
        if (Kind == VoyageShotKind.GelMine) { Projectile.timeLeft = 110; Projectile.width = Projectile.height = 26; }
        if (Kind == VoyageShotKind.MagnetBurst) { Projectile.timeLeft = 30; Projectile.tileCollide = false; Projectile.penetrate = -1; Delay = 12; Range = 145; }
        if (Kind == VoyageShotKind.Pillar) { Projectile.timeLeft = 25; Delay = 8; Projectile.tileCollide = false; Projectile.penetrate = -1; Range = 90; }
        if (Kind == VoyageShotKind.Shockwave) { Projectile.timeLeft = 28; Projectile.penetrate = -1; Projectile.width = 30; Projectile.height = 35; }
        // The wave's broad damage arc is handled in Colliding. Its terrain core
        // remains small enough to launch while standing on a floor.
        if (Kind == VoyageShotKind.Tide) { Projectile.width = Projectile.height = 18; Projectile.timeLeft = 105; }
        if (Kind == VoyageShotKind.FlameArc) { Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 25; Range = 170; }
        if (Kind == VoyageShotKind.StarBall) { Projectile.penetrate = 3; Projectile.width = Projectile.height = 46; Projectile.timeLeft = 70; }
        if (Kind == VoyageShotKind.Tail) { Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 20; Range = 36; }
        if (Kind == VoyageShotKind.Escort) { Projectile.timeLeft = 85; Projectile.penetrate = 1; }
        Projectile.Center = center;
    }
    public override void OnSpawn(IEntitySource source)
    {
        Anchor = Focus = Projectile.Center; Direction = Projectile.velocity.SafeNormalize(Vector2.UnitX); Configure();
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Age); w.Write(Delay); w.Write(Range); w.Write(Anchor.X); w.Write(Anchor.Y); w.Write(Direction.X); w.Write(Direction.Y);
        w.Write(Focus.X); w.Write(Focus.Y); w.Write(Target); w.Write(TargetType); w.Write(ParentSlot); w.Write(ParentIdentity); w.Write(ParentType);
        w.Write(ImpactDone); w.Write(Projectile.timeLeft); w.Write(Projectile.penetrate); w.Write(Projectile.tileCollide);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        // Receiving peers do not invoke OnSpawn. Configure class and immunities before restoring state.
        Configure(); Age = r.ReadInt32(); Delay = r.ReadInt32(); Range = r.ReadSingle(); Anchor = new(r.ReadSingle(), r.ReadSingle());
        Direction = new(r.ReadSingle(), r.ReadSingle()); Focus = new(r.ReadSingle(), r.ReadSingle()); Target = r.ReadInt32(); TargetType = r.ReadInt32();
        ParentSlot = r.ReadInt32(); ParentIdentity = r.ReadInt32(); ParentType = r.ReadInt32(); ImpactDone = r.ReadBoolean();
        Projectile.timeLeft = r.ReadInt32(); Projectile.penetrate = r.ReadInt32(); Projectile.tileCollide = r.ReadBoolean();
    }
    public override bool ShouldUpdatePosition() => Age >= Delay && !Attached && Kind is not (VoyageShotKind.Beam or VoyageShotKind.Mirror or VoyageShotKind.PortalBurst or VoyageShotKind.Link or VoyageShotKind.Fleet or VoyageShotKind.Bloom or VoyageShotKind.MagnetBurst or VoyageShotKind.Pillar or VoyageShotKind.FlameArc or VoyageShotKind.Tail);
    public override bool? CanCutTiles() => false;
    public override bool? CanDamage()
    {
        if (Age < Delay || Kind is VoyageShotKind.PortalBurst or VoyageShotKind.Link or VoyageShotKind.Fleet or VoyageShotKind.Bloom) return false;
        if (Kind == VoyageShotKind.OrbitRock && Age < 38 + Variant * 7 || Kind == VoyageShotKind.GelMine && Age < 10) return false;
        if (Kind == VoyageShotKind.OrbitArrow && Age < 17) return false;
        if (Kind == VoyageShotKind.Clamp && Age < 12) return false;
        return null;
    }
    public override bool? CanHitNPC(NPC target)
    {
        if (Kind == VoyageShotKind.Pulse && Secondary && Target >= 0 && target.whoAmI != Target) return false;
        return null;
    }
    public static NPC? FindTarget(Vector2 at, float range, int preferred = -1)
    {
        bool Valid(NPC n) => n.CanBeChasedBy() && n.Distance(at) < range && Collision.CanHitLine(at, 1, 1, n.position, n.width, n.height);
        if (preferred >= 0 && preferred < Main.maxNPCs && Valid(Main.npc[preferred])) return Main.npc[preferred];
        NPC? best = null;
        foreach (NPC n in Main.ActiveNPCs) if (Valid(n)) { range = n.Distance(at); best = n; }
        return best;
    }
    public static float Clip(Vector2 start, Vector2 direction, float length, float width = 6)
    {
        float[] samples = new float[3]; Collision.LaserScan(start, direction.SafeNormalize(Vector2.UnitX), width, length, samples);
        return Math.Clamp((samples[0] + samples[1] + samples[2]) / 3, 0, length);
    }
    public static Vector2? Ground(Vector2 start, int depth = 360)
    {
        for (int y = 0; y < depth; y += 8)
        {
            Vector2 point = start + new Vector2(0, y);
            if (Collision.SolidCollision(point, 8, 8)) return point - new Vector2(0, 6);
        }
        return null;
    }
    private NPC? LockedTarget() => Target >= 0 && Target < Main.maxNPCs && Main.npc[Target].active && Main.npc[Target].type == TargetType && Main.npc[Target].CanBeChasedBy() ? Main.npc[Target] : null;
    private Projectile? Parent() => ParentSlot >= 0 && ParentSlot < Main.maxProjectiles && Main.projectile[ParentSlot].active && Main.projectile[ParentSlot].identity == ParentIdentity && Main.projectile[ParentSlot].type == ParentType && Main.projectile[ParentSlot].owner == Projectile.owner ? Main.projectile[ParentSlot] : null;
    private void Child(VoyageShotKind kind, float fraction, Vector2 at, Vector2 velocity, int delay = 0, Vector2? focus = null, int target = -1)
        => VoyageArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, WeaponIndex, kind, at, velocity, (int)(Projectile.damage * fraction), Projectile.knockBack, 100, delay, focus, target, Parent());

    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers || !Player.active || Player.dead) { Projectile.Kill(); return; }
        if (ParentSlot >= 0 && Parent() == null) { Projectile.Kill(); return; }
        Age++;
        if (Age < Delay) return;
        int age = ActiveAge;
        if (Attached)
        {
            if (Player.HeldItem.type != VoyageCatalog.Weapon(WeaponIndex)) { Projectile.Kill(); return; }
            int duration = Kind == VoyageShotKind.ChainSpear ? 42 : Kind == VoyageShotKind.Spear ? 34 : 30;
            float t = Math.Clamp(age / (float)duration, 0, 1);
            Vector2 facing = Direction;
            if (Kind == VoyageShotKind.Swing)
            {
                float arc = Variant == 1 ? -1 : 1;
                facing = Direction.RotatedBy(MathHelper.Lerp(-1.35f, 1.35f, t) * arc);
                if (WeaponIndex is 0 or 24 && Variant == 2) facing = Vector2.UnitY.RotatedBy(MathHelper.Lerp(-1.5f * Player.direction, 0, Math.Min(1, t * 1.5f)));
                Projectile.Center = Player.MountedCenter + facing * Range * (.55f + .45f * MathF.Sin(t * MathHelper.Pi));
                if (age == 20 && !Secondary)
                {
                    if (WeaponIndex is 0 or 24 && Variant == 2) GroundStrike();
                    if (WeaponIndex is 16 or 28 or 32 && Variant == 2) Child(VoyageShotKind.Beam, .7f, Player.MountedCenter, Direction);
                }
            }
            else if (Kind == VoyageShotKind.Clamp) Projectile.Center = Player.MountedCenter + Direction * 190;
            else Projectile.Center = Player.MountedCenter + Direction * Range * (.14f + .86f * MathF.Sin(t * MathHelper.Pi));
            Projectile.rotation = facing.ToRotation() + MathHelper.PiOver4;
            Player.heldProj = Projectile.whoAmI;
            return;
        }
        switch (Kind)
        {
            case VoyageShotKind.Cargo: Projectile.velocity.Y += .14f; break;
            case VoyageShotKind.GelMine: Projectile.velocity.Y = Math.Min(9, Projectile.velocity.Y + .3f); Projectile.velocity.X *= .985f; break;
            case VoyageShotKind.Shockwave: Projectile.velocity.Y = Math.Min(6, Projectile.velocity.Y + .7f); break;
            case VoyageShotKind.Saw:
                if (age is >= 25 and < 62)
                {
                    Projectile.velocity = Vector2.Zero;
                    Vector2 next = Anchor + Direction * 310 + Direction.RotatedBy((age - 25) * .12f) * 65;
                    if (Collision.CanHitLine(Projectile.Center, 1, 1, next, 1, 1)) Projectile.Center = next;
                    else Age = 63;
                }
                if (Age >= 62) ReturnTo(Player.MountedCenter, 23);
                break;
            case VoyageShotKind.BlueBlade:
                Projectile.velocity = Projectile.velocity.RotatedBy(.014f * (Direction.X >= 0 ? 1 : -1));
                if (age >= 27) ReturnTo(Player.MountedCenter, 24);
                break;
            case VoyageShotKind.CrystalRing:
                if (age >= 24) ReturnTo(Parent()?.Center ?? Player.Center, 22);
                break;
            case VoyageShotKind.OrbitRock:
                if (age < 38 + Variant * 7)
                {
                    Vector2 orbit = (Direction.ToRotation() + age * .065f).ToRotationVector2();
                    Projectile.Center = Focus + orbit * Math.Max(0, Clip(Focus, orbit, 115) - 12); Projectile.velocity = Vector2.Zero;
                }
                else if (age == 38 + Variant * 7) { Projectile.velocity = (Focus - Projectile.Center).SafeNormalize(Vector2.UnitY) * 15; Projectile.tileCollide = true; }
                else Projectile.velocity.Y += .11f;
                break;
            case VoyageShotKind.Link:
                NPC? linked = LockedTarget();
                if (linked == null || linked.Distance(Projectile.Center) > 850 || !Collision.CanHitLine(Projectile.Center, 1, 1, linked.position, linked.width, linked.height)) { Projectile.Kill(); break; }
                Focus = linked.Center;
                if (age is 8 or 16) Child(VoyageShotKind.Pulse, .24f, Projectile.Center, (Focus - Projectile.Center).SafeNormalize(Direction) * 26, target: linked.whoAmI);
                if (age >= 23) Projectile.Kill();
                break;
            case VoyageShotKind.Pulse:
                if (Secondary && Target >= 0)
                {
                    NPC? pulseTarget = LockedTarget();
                    if (pulseTarget == null || !Collision.CanHitLine(Projectile.Center, 1, 1, pulseTarget.position, pulseTarget.width, pulseTarget.height)) { Projectile.Kill(); break; }
                    if (age <= 24) TurnToward(pulseTarget, .05f);
                }
                break;
            case VoyageShotKind.Arrow: case VoyageShotKind.Seed: Projectile.velocity.Y += .018f; break;
            case VoyageShotKind.Seeking: case VoyageShotKind.Bee: case VoyageShotKind.Escort:
                if (age is >= 8 and <= 48)
                {
                    NPC? target = FindTarget(Projectile.Center, 650, Target);
                    if (target != null) TurnToward(target, Kind == VoyageShotKind.Escort ? .055f : .04f);
                }
                break;
            case VoyageShotKind.GuideArrow:
                if (age is >= 4 and <= 35 && Target >= 0)
                {
                    NPC? mark = LockedTarget();
                    if (mark != null && mark.Distance(Projectile.Center) < 750 && Collision.CanHitLine(Projectile.Center, 1, 1, mark.position, mark.width, mark.height)) TurnToward(mark, .035f);
                }
                break;
            case VoyageShotKind.Anchor:
                if (age >= 28 || Vector2.Distance(Anchor, Projectile.Center) >= Range) { Projectile.velocity = Vector2.Zero; Projectile.tileCollide = false; }
                if (age >= 46)
                {
                    Vector2 origin = Parent()?.Center ?? Player.MountedCenter;
                    Projectile.Center = Vector2.Lerp(Projectile.Center, origin, .095f);
                    if (Projectile.Distance(origin) < 24) Projectile.Kill();
                }
                break;
            case VoyageShotKind.PortalBurst:
                if (age is 14 or 22 or 30)
                {
                    Vector2 entrance = Anchor + Direction * 45;
                    Vector2 exit = Focus;
                    if (Collision.CanHitLine(Anchor, 1, 1, exit, 1, 1)) Child(VoyageShotKind.Shard, .48f, exit, Direction * 19);
                    // The entering bolt is a harmless visual; it cannot hit twice through a portal.
                }
                break;
            case VoyageShotKind.OrbitArrow:
                if (age < 17)
                {
                    Projectile.velocity = Vector2.Zero;
                    // Keep the preparation arc above a grounded shooter on either side.
                    // The old positive rotation swept into the floor before releasing.
                    float side = Direction.X < 0 ? -1 : 1;
                    Vector2 next = Focus + Direction.RotatedBy(-side * MathHelper.Lerp(2.3f, 0, age / 17f)) * 70;
                    if (!Collision.CanHitLine(Projectile.Center, 1, 1, next, 1, 1)) { Projectile.Kill(); break; }
                    Projectile.Center = next;
                    if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) Projectile.Kill();
                }
                else if (age == 17) { Projectile.velocity = Direction * 21; Projectile.tileCollide = true; }
                break;
            case VoyageShotKind.Fleet:
                if (age is 16 or 28 or 40)
                {
                    int n = (age - 16) / 12;
                    Vector2 at = Anchor + Direction * ((age - n * 12) * 6);
                    NPC? enemy = FindTarget(at, 720, Player.MinionAttackTargetNPC);
                    Vector2 shotDirection = enemy == null ? Direction.RotatedBy(MathHelper.PiOver2) : (enemy.Center - at).SafeNormalize(Direction);
                    if (Collision.CanHitLine(Anchor, 1, 1, at, 1, 1)) Child(VoyageShotKind.Escort, .5f, at, shotDirection * 14);
                }
                break;
            case VoyageShotKind.Bloom:
                if (age == 32)
                {
                    NPC? target = FindTarget(Projectile.Center, 600);
                    Vector2 aim = target == null ? -Vector2.UnitY : (target.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                    for (int n = -1; n <= 1; n++) Child(VoyageShotKind.Shard, .27f, Projectile.Center, aim.RotatedBy(n * .2f) * 14);
                }
                break;
            case VoyageShotKind.CoreRocket: Projectile.velocity *= .998f; break;
            case VoyageShotKind.Tide:
                Projectile.velocity = Direction.RotatedBy(MathF.Sin(age * .07f) * .4f) * (age < 40 ? 12 : -14);
                if (age == 40) Array.Clear(Projectile.localNPCImmunity); // Exactly one hit on each leg.
                Projectile.localNPCHitCooldown = -1;
                if (age >= 40 && Projectile.Distance(Anchor) < 35) Projectile.Kill();
                break;
        }
        Projectile.rotation = Kind is VoyageShotKind.Saw or VoyageShotKind.BlueBlade or VoyageShotKind.CrystalRing or VoyageShotKind.CoreRocket ? Projectile.rotation + .16f : Projectile.velocity.LengthSquared() > .1f ? Projectile.velocity.ToRotation() : Direction.ToRotation();
    }

    private void TurnToward(NPC target, float step)
    {
        float angle = Projectile.velocity.ToRotation();
        float change = MathHelper.Clamp(MathHelper.WrapAngle((target.Center - Projectile.Center).ToRotation() - angle), -step, step);
        Projectile.velocity = (angle + change).ToRotationVector2() * Math.Max(8, Projectile.velocity.Length());
    }
    private void ReturnTo(Vector2 destination, float speed)
    {
        Projectile.tileCollide = false;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, (destination - Projectile.Center).SafeNormalize(Vector2.Zero) * speed, .18f);
        if (Projectile.Distance(destination) < 25) Projectile.Kill();
    }
    private void GroundStrike()
    {
        if (!Owner) return;
        Vector2? ground = Ground(Player.Bottom + new Vector2(Player.direction * 70, -12), 150);
        if (ground == null) return;
        if (WeaponIndex == 0)
            for (int n = -1; n <= 1; n += 2) Child(VoyageShotKind.Shockwave, .42f, ground.Value - new Vector2(0, 20), new Vector2(n * 9, 0));
        else for (int n = 0; n < 3; n++)
        {
            Vector2 point = ground.Value + new Vector2(Player.direction * (n * 75), -24);
            Vector2? floor = Ground(point, 70);
            if (floor != null && Collision.CanHitLine(Player.Center, 1, 1, floor.Value - new Vector2(0, 15), 1, 1)) Child(VoyageShotKind.Pillar, .35f, floor.Value, Vector2.Zero, n * 7);
        }
    }
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Kind == VoyageShotKind.GelMine)
        {
            if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -Math.Abs(oldVelocity.Y) * .25f;
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * .55f;
            return false;
        }
        if (Kind == VoyageShotKind.Shockwave && Projectile.velocity.X == oldVelocity.X) { Projectile.velocity.Y = 0; return false; }
        if (Kind is VoyageShotKind.Saw or VoyageShotKind.BlueBlade or VoyageShotKind.CrystalRing)
        { Age = Math.Max(Age, 65); Projectile.tileCollide = false; Projectile.velocity = -oldVelocity * .5f; return false; }
        if (Kind == VoyageShotKind.Anchor)
        { Age = Math.Max(Age, 29); Projectile.velocity = Vector2.Zero; Projectile.tileCollide = false; return false; }
        return true;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Owner && WeaponIndex == 30 && Variant == 0 && !Secondary) Player.GetModPlayer<VoyageWeaponPlayer>().Mark(target);
        if (Owner && WeaponIndex == 6 && !Secondary && !ImpactDone)
        {
            VoyageArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, WeaponIndex, VoyageShotKind.Link,
                Player.MountedCenter, Direction, Projectile.damage, 0, 100, target: target.whoAmI);
            ImpactDone = true;
        }
        if (Kind == VoyageShotKind.Anchor && !target.boss && target.knockBackResist > 0 && target.width < 90 && Main.netMode != NetmodeID.MultiplayerClient)
        { target.velocity += (Player.Center - target.Center).SafeNormalize(Vector2.Zero) * Math.Min(2, target.knockBackResist * 2); target.netUpdate = true; }
        Impact();
    }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (Kind == VoyageShotKind.Clamp && target.Distance(Projectile.Center) < 45) modifiers.SourceDamage *= 1.4f;
    }
    public override void OnKill(int timeLeft)
    {
        if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers && Player.active && !Player.dead) Impact();
    }
    private void Impact()
    {
        if (!Owner || Secondary || ImpactDone) return;
        ImpactDone = true;
        switch (Kind)
        {
            case VoyageShotKind.Cargo:
                for (int n = -1; n <= 1; n += 2) Child(VoyageShotKind.GelMine, .28f, Projectile.Center, new Vector2(n * 5, -3));
                break;
            case VoyageShotKind.Arrow when WeaponIndex == 9:
                for (int n = -1; n <= 1; n += 2) Child(VoyageShotKind.Bee, .25f, Projectile.Center, Direction.RotatedBy(n * .65f) * 11);
                break;
            case VoyageShotKind.Seed: Child(VoyageShotKind.Bloom, 1, Projectile.Center - Direction * 8, Vector2.Zero); break;
            case VoyageShotKind.CoreRocket:
                for (int n = 0; n < 4; n++) Child(VoyageShotKind.Beam, .27f, Projectile.Center - Direction * 12, (n * MathHelper.PiOver2).ToRotationVector2());
                break;
        }
        Projectile.netUpdate = true;
    }

    private bool LineHit(Rectangle box, Vector2 start, Vector2 end, float width)
    {
        float hit = 0;
        Vector2 delta = end - start;
        Vector2 clippedEnd = start + delta.SafeNormalize(Vector2.UnitX) * Clip(start, delta, delta.Length(), width);
        return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), start, clippedEnd, width, ref hit);
    }
    private Vector2 ClampHand(int sign)
    {
        float spread = MathHelper.Lerp(140, 4, Math.Clamp((Age - 5) / 19f, 0, 1));
        return Projectile.Center + Direction.RotatedBy(MathHelper.PiOver2) * spread * sign;
    }
    private Vector2[] MirrorPath()
    {
        Vector2 normal = Direction.RotatedBy(MathHelper.PiOver2) * (Direction.X < 0 ? 1 : -1);
        Vector2 a = Anchor + Direction * 190 + normal * 75;
        Vector2 b = Anchor + Direction * 390 + normal * 25;
        return new[] { Anchor, a, b, b + Direction * 650 };
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false) return false;
        if (Kind == VoyageShotKind.Tide)
            return new Rectangle((int)Projectile.Center.X - 27, (int)Projectile.Center.Y - 35, 54, 70).Intersects(targetHitbox)
                && Collision.CanHitLine(Projectile.Center, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height);
        if (Attached && !Collision.CanHitLine(Player.MountedCenter, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height)) return false;
        if (Kind == VoyageShotKind.Clamp)
            return LineHit(targetHitbox, ClampHand(-1) - Direction * 30, ClampHand(-1) + Direction * 30, 30) || LineHit(targetHitbox, ClampHand(1) - Direction * 30, ClampHand(1) + Direction * 30, 30);
        if (Attached) return LineHit(targetHitbox, Player.MountedCenter, Projectile.Center, Kind == VoyageShotKind.ChainSpear ? 18 : 26);
        if (Kind == VoyageShotKind.Beam) return LineHit(targetHitbox, Anchor, Anchor + Direction * (Secondary && WeaponIndex is 16 or 28 or 32 ? 600 : Range), WeaponIndex == 33 ? 25 : 12);
        if (Kind == VoyageShotKind.Mirror)
        {
            Vector2[] path = MirrorPath();
            for (int n = 0; n < 3; n++)
            {
                if (LineHit(targetHitbox, path[n], path[n + 1], 12)) return true;
                if (Clip(path[n], path[n + 1] - path[n], Vector2.Distance(path[n], path[n + 1])) < Vector2.Distance(path[n], path[n + 1]) - 2) break;
            }
            return false;
        }
        if (Kind == VoyageShotKind.Anchor && Age >= 46) return LineHit(targetHitbox, Parent()?.Center ?? Player.MountedCenter, Projectile.Center, 14);
        if (Kind == VoyageShotKind.Pillar) return LineHit(targetHitbox, Anchor, Anchor - Vector2.UnitY * Range * Math.Min(1, ActiveAge / 8f), 34);
        if (Kind is VoyageShotKind.MagnetBurst or VoyageShotKind.Tail)
            return Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) <= Range && Collision.CanHitLine(Projectile.Center, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height);
        if (Kind == VoyageShotKind.FlameArc)
        {
            Vector2 delta = targetHitbox.Center.ToVector2() - Anchor;
            return Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - Direction.ToRotation())) < .8f &&
                Math.Abs(delta.Length() - Range * ActiveAge / 25f) < targetHitbox.Size().Length() / 2 + 12 &&
                Collision.CanHitLine(Anchor, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height);
        }
        // Returning weapons may cross terrain on the way back, but cannot damage through it.
        if (Kind is VoyageShotKind.Saw or VoyageShotKind.BlueBlade or VoyageShotKind.CrystalRing &&
            !Collision.CanHitLine(Parent()?.Center ?? Player.MountedCenter, 1, 1, targetHitbox.TopLeft(), targetHitbox.Width, targetHitbox.Height)) return false;
        return null;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (Age < Delay && Kind is not (VoyageShotKind.Beam or VoyageShotKind.Mirror or VoyageShotKind.MagnetBurst or VoyageShotKind.Pillar)) return false;
        Color color = VoyageArsenal.Color(WeaponIndex), drawLight = lightColor; Vector2 at = Projectile.Center - Main.screenPosition;
        void Line(Vector2 a, Vector2 b, float width = 3, float opacity = 1) => CombatDrawing.Line(Main.spriteBatch, a - Main.screenPosition, b - Main.screenPosition, color * opacity, width);
        void Circle(Vector2 point, float radius, float width = 2, float opacity = 1) => CombatDrawing.Circle(Main.spriteBatch, point - Main.screenPosition, radius, color * opacity, width);
        void Sprite(Vector2 point, float size, float rotation = 0)
        {
            Texture2D texture = ModContent.Request<Texture2D>(VoyageArsenal.Art(WeaponIndex)).Value;
            Main.EntitySpriteDraw(texture, point - Main.screenPosition, null, Color.Lerp(drawLight, Color.White, .65f), rotation, texture.Size() / 2, size / Math.Max(texture.Width, texture.Height), SpriteEffects.None);
        }
        if (Kind == VoyageShotKind.Beam)
        {
            float range = Secondary && WeaponIndex is 16 or 28 or 32 ? 600 : Range;
            Vector2 end = Anchor + Direction * Clip(Anchor, Direction, range);
            Line(Anchor, end, Age < Delay ? 2 : WeaponIndex == 33 ? 26 : 13, Age < Delay ? .35f : .8f);
            if (Age >= Delay) CombatDrawing.Line(Main.spriteBatch, Anchor - Main.screenPosition, end - Main.screenPosition, Color.White, WeaponIndex == 33 ? 8 : 3);
            return false;
        }
        if (Kind == VoyageShotKind.Mirror)
        {
            Vector2[] path = MirrorPath();
            for (int n = 0; n < 3; n++)
            {
                Vector2 delta = path[n + 1] - path[n]; float clipped = Clip(path[n], delta, delta.Length());
                Line(path[n], path[n] + delta.SafeNormalize(Direction) * clipped, Age < Delay ? 2 : 10, Age < Delay ? .35f : .9f);
                if (clipped < delta.Length() - 2) break;
                if (n < 2) { Circle(path[n + 1], 20); Line(path[n + 1] - new Vector2(10, 24), path[n + 1] + new Vector2(10, 24), 6); }
            }
            return false;
        }
        if (Kind == VoyageShotKind.Clamp)
        { for (int n = -1; n <= 1; n += 2) { Line(Player.MountedCenter, ClampHand(n), 2, .35f); Sprite(ClampHand(n), 78, Direction.ToRotation() + n * MathHelper.PiOver2); } return false; }
        if (Attached)
        {
            Line(Player.MountedCenter, Projectile.Center, Kind == VoyageShotKind.ChainSpear ? 5 : 2, .65f);
            if (Kind == VoyageShotKind.ChainSpear)
                for (int n = 1; n < 6; n++) Sprite(Vector2.Lerp(Player.MountedCenter, Projectile.Center, n / 6f), 30, Projectile.rotation + Age * .03f);
            if (Kind == VoyageShotKind.Swing && WeaponIndex is 28 or 32)
            {
                for (int n = 0; n < 7; n++) Line(Player.MountedCenter, Player.MountedCenter + (Projectile.Center - Player.MountedCenter).RotatedBy(-n * .06f), 3, .12f);
            }
            Sprite(Projectile.Center, WeaponIndex is 0 or 24 ? 88 : 72, Projectile.rotation); return false;
        }
        if (Kind == VoyageShotKind.Link) { Line(Projectile.Center, Focus, 2, .55f); Circle(Focus, 23); return false; }
        if (Kind == VoyageShotKind.Anchor) Line(Parent()?.Center ?? Player.MountedCenter, Projectile.Center, 4, .7f);
        if (Kind == VoyageShotKind.Saw) { Circle(Projectile.Center, 34, 3); Sprite(Projectile.Center, 60, Projectile.rotation); return false; }
        if (Kind == VoyageShotKind.PortalBurst)
        {
            Circle(Anchor + Direction * 45, 30, 3); Circle(Focus, 34, 3);
            int beat = (Age - 6) % 8; if (Age is >= 6 and <= 30) Line(Anchor + Direction * (beat * 6), Anchor + Direction * (beat * 6 + 12), 6);
            Line(Anchor + Direction * 45, Focus, 1, .15f); return false;
        }
        if (Kind == VoyageShotKind.Fleet)
        {
            float lane = Clip(Anchor, Direction, 360);
            Line(Anchor, Anchor + Direction * lane, 2, .3f);
            for (int n = 0; n < 3; n++)
            {
                int clock = Age - n * 12;
                if (clock is > 0 and < 55 && clock * 6 < lane) Sprite(Anchor + Direction * (clock * 6), 42, Direction.ToRotation());
            }
            return false;
        }
        if (Kind == VoyageShotKind.Bloom) { Circle(Projectile.Center, 14 + Math.Min(25, Age), 2, .7f); Sprite(Projectile.Center, 32 + Math.Min(18, Age), -.8f); return false; }
        if (Kind == VoyageShotKind.Pillar) { Line(Anchor, Anchor - Vector2.UnitY * Range * (Age < Delay ? 1 : Math.Min(1, ActiveAge / 8f)), Age < Delay ? 2 : 28, Age < Delay ? .3f : .8f); return false; }
        if (Kind is VoyageShotKind.MagnetBurst or VoyageShotKind.Tail)
        { Circle(Projectile.Center, Range, Age < Delay ? 1 : 4, .75f); if (Age >= Delay) CombatDrawing.Disc(Main.spriteBatch, at, Range, color * .12f); return false; }
        if (Kind == VoyageShotKind.FlameArc)
        { CombatDrawing.Circle(Main.spriteBatch, at, Range * ActiveAge / 25f, color * .8f, 8, Direction.ToRotation() + MathHelper.Pi, MathHelper.Pi - .8f); return false; }
        if (Kind == VoyageShotKind.Tide)
        {
            for (int n = 0; n < 9; n++)
            {
                Vector2 a = Projectile.Center + Direction.RotatedBy(MathHelper.PiOver2) * (n * 9 - 36) + Direction * MathF.Sin(n * .5f) * 22;
                Line(a - Direction * 12, a + Direction * 12, 7, .75f);
            }
            return false;
        }
        if (Kind is VoyageShotKind.CrystalRing or VoyageShotKind.StarBall) Circle(Projectile.Center, Kind == VoyageShotKind.StarBall ? 33 : 25, 4);
        if (Kind == VoyageShotKind.GelMine) Circle(Projectile.Center, 19, 2);
        if (Kind == VoyageShotKind.Pulse)
        {
            Color pulse = WeaponIndex == 17 ? Variant == 1 ? new Color(255, 100, 100) : new Color(100, 190, 255) : color;
            CombatDrawing.Line(Main.spriteBatch, at - Direction * 20, at + Direction * 12, pulse, WeaponIndex == 17 && Variant == 1 ? 10 : 5);
            return false;
        }
        if (Kind is VoyageShotKind.Shard or VoyageShotKind.Seeking or VoyageShotKind.Bee or VoyageShotKind.Arrow or VoyageShotKind.GuideArrow)
        { Line(Projectile.Center - Projectile.velocity.SafeNormalize(Direction) * 20, Projectile.Center, 4, .85f); Circle(Projectile.Center, 4, 2); return false; }
        Sprite(Projectile.Center, Kind is VoyageShotKind.Cargo or VoyageShotKind.CoreRocket or VoyageShotKind.StarBall ? 52 : 34, Projectile.rotation);
        return false;
    }
}

public sealed class VoyageSummonShot : VoyageShot
{
    public override void SetStaticDefaults() => ProjectileID.Sets.MinionShot[Type] = true;
}
