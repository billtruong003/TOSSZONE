#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// A "rain" of projectiles as DATA, not GameObjects (see Docs/Burst_Projectile_System_Design.md). When a
    /// throw is multiplied by a Multi ring (×12, stacking), we spawn ONE networked <see cref="Burst"/> describing
    /// the whole cloud instead of N NetworkObjects. Flight is the analytic ballistic formula
    /// <c>p(t) = origin + v0·t + ½·g·t²</c> with a seeded per-projectile spread, so every client derives identical
    /// positions from the tiny replicated burst — no per-projectile sync. Rendering is local + GPU-instanced
    /// (<see cref="ProjectileBurstRenderer"/>); only the burst spawn and per-projectile resolve (hit/catch/deflect)
    /// cross the wire.
    ///
    /// Dead-mask: each burst carries a small NETWORKED bitmask (<see cref="DeadMaskBits"/> bits) marking
    /// individually-resolved projectiles — hit, caught, or deflected. This replaces an authority-only local
    /// HashSet used in the first MVP pass, which only stopped the AUTHORITY from re-hitting a projectile but left
    /// it still rendering forever on every OTHER client (a "ghost bullet" that already dealt damage). The mask is
    /// deliberately small relative to <see cref="MaxProjectilesPerBurst"/> — realistic rain sizes (RC_Multi
    /// multiplier ~40) are far under it; projectiles beyond the mask still hit-test correctly, they just can't be
    /// individually erased from render once resolved (acceptable: they still despawn when the whole burst expires).
    /// </summary>
    public class ProjectileBurstSystem : NetworkBehaviour
    {
        public static ProjectileBurstSystem Instance { get; private set; }

        public const int MaxBursts = 32;
        public const int MaxProjectilesPerBurst = 4096;   // hard cap per burst (design §7)
        public const int DeadMaskBits = 256;              // 4 x ulong — individually-trackable per burst

        [SerializeField] private float _baseSpeed = 7f;
        [SerializeField] private float _spreadDegrees = 22f;
        [SerializeField] private float _lifetime = 4f;
        [SerializeField] private float _hitRadius = 0.35f;
        [SerializeField] private int _damage = 1;
        [Tooltip("Per-tick, per-burst cap on how many projectiles are checked against rings — the cone is " +
                 "spatially clustered so a sample is representative; avoids an O(count) scan at 4096.")]
        [SerializeField] private int _ringCheckSampleCount = 24;

        [Networked, Capacity(MaxBursts)] private NetworkArray<Burst> Bursts => default;

        public struct Burst : INetworkStruct
        {
            public NetworkBool Active;
            public Vector3 Origin;
            public Vector3 Dir;
            public int Count;
            public int Seed;
            public float Gravity;
            public int SpawnTick;
            public int Element;
            public int RingsApplied;
            public PlayerRef Shooter;
            public ulong Dead0, Dead1, Dead2, Dead3;   // bit i set = projectile i resolved (hit/caught/deflected)
        }

        // ── Dead-mask helpers ─────────────────────────────────────────────────────────
        public static bool IsDead(in Burst b, int i)
        {
            if (i < 0 || i >= DeadMaskBits) return false;   // beyond the mask: never individually erasable
            ulong word = i < 64 ? b.Dead0 : i < 128 ? b.Dead1 : i < 192 ? b.Dead2 : b.Dead3;
            return (word & (1UL << (i % 64))) != 0;
        }

        private static void SetDeadBit(ref Burst b, int i)
        {
            if (i < 0 || i >= DeadMaskBits) return;
            ulong bit = 1UL << (i % 64);
            if (i < 64) b.Dead0 |= bit;
            else if (i < 128) b.Dead1 |= bit;
            else if (i < 192) b.Dead2 |= bit;
            else b.Dead3 |= bit;
        }

        /// <summary>Authority: mark projectile <paramref name="i"/> of burst <paramref name="slot"/> resolved
        /// (stops rendering + hit-testing it on every client). Used by hit detection here, and by
        /// catch (<see cref="TossZone.Combat.CatchController"/>) / deflect (sword) via their confirm RPCs.</summary>
        public void MarkDead(int slot, int i)
        {
            if (!HasStateAuthority || slot < 0 || slot >= Bursts.Length) return;
            Burst b = Bursts.Get(slot);
            if (!b.Active) return;
            SetDeadBit(ref b, i);
            Bursts.Set(slot, b);
        }

        /// <summary>Any client asks the burst's authority (the scene NetworkObject's authority = master in
        /// Shared Mode) to resolve a catch: mark the projectile dead. Ammo is NOT granted here — Shared Mode
        /// means only the CATCHER's own client can write their own PlayerCombat.Ammo, so the caller (this
        /// client, immediately after firing this RPC) grants it locally/optimistically. Lightweight anti-cheat:
        /// reject if the projectile isn't actually near the claimed catch point (latency slack ~2m).</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCatch(int slot, int i, Vector3 claimedPos)
        {
            if (slot < 0 || slot >= Bursts.Length) return;
            Burst b = Bursts.Get(slot);
            if (!b.Active || IsDead(b, i)) return;
            Vector3 p = ProjectilePosition(b, i, BurstElapsed(b));
            if ((p - claimedPos).sqrMagnitude > 4f) return;   // ~2m slack for network latency
            SetDeadBit(ref b, i);
            Bursts.Set(slot, b);
        }

        public override void Spawned() => Instance = this;
        public override void Despawned(NetworkRunner runner, bool hasState) { if (Instance == this) Instance = null; }

        // ── Spawn (authority) ───────────────────────────────────────────────────────────
        /// <summary>Authority spawns a rain burst. Returns the slot, or -1 if full / not authority.</summary>
        public int SpawnBurst(Vector3 origin, Vector3 dir, int count, float gravity, int element,
            PlayerRef shooter, int ringsApplied = 1)
        {
            if (!HasStateAuthority) return -1;
            count = Mathf.Clamp(count, 1, DeadMaskBits);
            for (int i = 0; i < Bursts.Length; i++)
            {
                if (Bursts.Get(i).Active) continue;
                Bursts.Set(i, new Burst
                {
                    Active = true,
                    Origin = origin,
                    Dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward,
                    Count = count,
                    Seed = Mathf.Abs(origin.GetHashCode() ^ (Runner.Tick.Raw * 2654435761u).GetHashCode()) & 0x7FFFFFFF,
                    Gravity = gravity,
                    SpawnTick = Runner.Tick,
                    Element = element,
                    RingsApplied = Mathf.Clamp(ringsApplied, 1, TossZone.Throwing.NetworkProjectile.MaxRingStack),
                    Shooter = shooter,
                });
                return i;
            }
            return -1;
        }

        // ── Deterministic flight (shared by authority hit-test + local render) ───────────
        public float BaseSpeed => _baseSpeed;
        public float SpreadDegrees => _spreadDegrees;

        /// <summary>Live bursts snapshot for the renderer.</summary>
        public NetworkArray<Burst> ActiveBursts => Bursts;

        public float BurstElapsed(in Burst b) => (Runner != null) ? (Runner.Tick - b.SpawnTick) * Runner.DeltaTime : 0f;

        /// <summary>World position of projectile <paramref name="i"/> of burst <paramref name="b"/> at elapsed t.</summary>
        public Vector3 ProjectilePosition(in Burst b, int i, float t)
        {
            Vector3 v0 = ProjectileVelocity(b, i);
            return b.Origin + v0 * t + 0.5f * (Vector3.down * b.Gravity) * (t * t);
        }

        public Vector3 ProjectileVelocity(in Burst b, int i)
        {
            // Seeded cone spread around Dir — deterministic per (seed, i) so all clients agree.
            float u1 = Hash01(b.Seed, i * 2);
            float u2 = Hash01(b.Seed, i * 2 + 1);
            float ang = Mathf.Deg2Rad * _spreadDegrees * Mathf.Sqrt(u1); // sqrt = uniform over cone area
            float az = u2 * Mathf.PI * 2f;
            Vector3 dir = ConeDirection(b.Dir, ang, az);
            return dir * _baseSpeed;
        }

        private static Vector3 ConeDirection(Vector3 axis, float polar, float azimuth)
        {
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 tangent = Vector3.Normalize(Vector3.Cross(up, axis));
            Vector3 bitangent = Vector3.Cross(axis, tangent);
            float sp = Mathf.Sin(polar);
            Vector3 offset = (tangent * Mathf.Cos(azimuth) + bitangent * Mathf.Sin(azimuth)) * sp;
            return (axis * Mathf.Cos(polar) + offset).normalized;
        }

        private static float Hash01(int seed, int i)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)(i * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777215f;
        }

        // ── Authority: flight expiry + hit detection ─────────────────────────────────────
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Cache once per tick, not per burst — there are normally 0-2 active bursts and 3-5 rings, so this
            // is cheap regardless, but no reason to redo it per burst in the same tick.
            BuffRing[] rings = null;

            for (int s = 0; s < Bursts.Length; s++)
            {
                Burst b = Bursts.Get(s);
                if (!b.Active) continue;

                float t = BurstElapsed(b);
                if (t >= _lifetime)
                {
                    b.Active = false;
                    Bursts.Set(s, b);   // Dead bits reset for free next SpawnBurst (fresh struct literal = 0)
                    continue;
                }

                // Stacking (T7): a burst passing through a Multi ring multiplies Count (e.g. 12x12x12). Bursts
                // have no collider so they can't trigger BuffRing.OnTriggerEnter normally — sample a subset of
                // projectile positions against each live Multi ring's center instead.
                if (b.Count < DeadMaskBits && b.RingsApplied < TossZone.Throwing.NetworkProjectile.MaxRingStack)
                {
                    rings ??= FindObjectsByType<BuffRing>(FindObjectsSortMode.None);
                    if (TryStackThroughRing(ref b, t, rings, out BuffRing consumedRing))
                    {
                        Bursts.Set(s, b);
                        consumedRing.TryConsumeByBurst();
                    }
                }

                // Hit test each projectile vs real players (cheap distance check; cap the per-tick scan AND
                // the dead-mask range — beyond DeadMaskBits still hit-tests, just can't be individually erased).
                int scan = Mathf.Min(b.Count, MaxProjectilesPerBurst);
                bool dirty = false;
                for (int i = 0; i < scan; i++)
                {
                    if (IsDead(b, i)) continue;
                    Vector3 p = ProjectilePosition(b, i, t);

                    foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                    {
                        if (!pc.IsPlayer || pc.Object == null) continue;
                        if (pc.Object.InputAuthority == b.Shooter) continue;   // don't hit the shooter
                        if (pc.Health <= 0) continue;
                        Vector3 chest = pc.transform.position + Vector3.up * 1.0f;
                        if ((p - chest).sqrMagnitude <= _hitRadius * _hitRadius)
                        {
                            SetDeadBit(ref b, i);
                            dirty = true;
                            pc.RPC_TakeHit(_damage, p, b.Shooter);
                            break;
                        }
                    }
                }
                if (dirty) Bursts.Set(s, b);
            }
        }

        /// <summary>T7 stacking: sample up to <see cref="_ringCheckSampleCount"/> live projectile positions of
        /// <paramref name="b"/> against every active Multi ring; on the first pass-through, multiply Count
        /// (clamped to <see cref="DeadMaskBits"/>) and report which ring to consume. Pass-through = the pellet's
        /// last-tick→this-tick segment crosses the ring's PLANE within its opening radius — a center-distance
        /// check triggered on pellets merely flying past the ring's face (PT-03). One stack per tick per burst —
        /// the ring is removed from the scene by the caller before the next tick can re-trigger.</summary>
        private bool TryStackThroughRing(ref Burst b, float t, BuffRing[] rings, out BuffRing consumedRing)
        {
            consumedRing = null;
            if (rings == null || rings.Length == 0) return false;

            int scan = Mathf.Min(b.Count, DeadMaskBits, _ringCheckSampleCount);
            float tPrev = Mathf.Max(0f, t - (Runner != null ? Runner.DeltaTime : 0.02f));
            if (tPrev >= t) return false;

            for (int r = 0; r < rings.Length; r++)
            {
                BuffRing ring = rings[r];
                if (ring == null || ring.Object == null || !ring.Object.IsValid || ring.IsConsumed
                    || ring.Element != RingElement.Multi) continue;
                Vector3 ringPos = ring.transform.position;
                Vector3 planeN = ring.transform.forward;
                float openSq = ring.OpeningRadius * ring.OpeningRadius;

                for (int i = 0; i < scan; i++)
                {
                    if (IsDead(b, i)) continue;
                    Vector3 p1 = ProjectilePosition(b, i, t);
                    Vector3 p0 = ProjectilePosition(b, i, tPrev);
                    float d1 = Vector3.Dot(p1 - ringPos, planeN);
                    float d0 = Vector3.Dot(p0 - ringPos, planeN);
                    if (d0 * d1 > 0f || Mathf.Approximately(d0, d1)) continue;
                    Vector3 cross = Vector3.Lerp(p0, p1, d0 / (d0 - d1));
                    if ((cross - ringPos).sqrMagnitude > openSq) continue;

                    b.Count = Mathf.Min(b.Count * Mathf.Max(2, ring.StackMultiplier), DeadMaskBits);
                    b.RingsApplied++;
                    consumedRing = ring;
                    return true;
                }
            }
            return false;
        }

        // ── Local queries (catch / deflect) — call from a client, then RPC the result to authority ──────────
        /// <summary>Find the closest LIVE projectile within <paramref name="radius"/> of <paramref name="point"/>,
        /// across all active bursts. Used by <see cref="CatchController"/> (T4). Local/read-only — the caller
        /// RPCs <paramref name="burstSlot"/>/<paramref name="projIndex"/> to the authority to confirm + mark dead.</summary>
        public bool TryConsumeNear(Vector3 point, float radius, out int burstSlot, out int projIndex)
        {
            burstSlot = -1; projIndex = -1;
            float bestSq = radius * radius;
            bool found = false;
            for (int s = 0; s < Bursts.Length; s++)
            {
                Burst b = Bursts.Get(s);
                if (!b.Active) continue;
                float t = BurstElapsed(b);
                int scan = Mathf.Min(b.Count, DeadMaskBits);   // only individually-trackable indices are catchable
                for (int i = 0; i < scan; i++)
                {
                    if (IsDead(b, i)) continue;
                    float sq = (ProjectilePosition(b, i, t) - point).sqrMagnitude;
                    if (sq <= bestSq) { bestSq = sq; burstSlot = s; projIndex = i; found = true; }
                }
            }
            return found;
        }

        /// <summary>Find LIVE projectiles whose current position is within <paramref name="radius"/> of the
        /// segment <paramref name="a"/>-<paramref name="b"/> (a sword sweep). Used by the sword deflect path
        /// (T5). Returns up to <paramref name="maxResults"/> hits into the provided buffers.</summary>
        public int TryDeflectAlong(Vector3 a, Vector3 b, float radius, int[] outSlots, int[] outIndices, int maxResults)
        {
            int found = 0;
            Vector3 ab = b - a;
            float abLenSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            float rSq = radius * radius;

            for (int s = 0; s < Bursts.Length && found < maxResults; s++)
            {
                Burst burst = Bursts.Get(s);
                if (!burst.Active) continue;
                float t = BurstElapsed(burst);
                int scan = Mathf.Min(burst.Count, DeadMaskBits);
                for (int i = 0; i < scan && found < maxResults; i++)
                {
                    if (IsDead(burst, i)) continue;
                    Vector3 p = ProjectilePosition(burst, i, t);
                    float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq);
                    Vector3 closest = a + ab * u;
                    if ((p - closest).sqrMagnitude <= rSq)
                    {
                        outSlots[found] = s;
                        outIndices[found] = i;
                        found++;
                    }
                }
            }
            return found;
        }

        /// <summary>Authority: resolve a deflect — mark the projectile dead in its burst and spawn a normal
        /// pooled single <see cref="TossZone.Throwing.NetworkProjectile"/> with the new (bounced) velocity, so it
        /// can go on to hit its own targets (e.g. the original shooter). Splits the projectile OUT of the mass
        /// burst into individual data, matching the design doc's deflect model.</summary>
        public void ResolveDeflect(int slot, int i, Vector3 newVelocity, PlayerRef newShooter,
            Fusion.NetworkObject singleProjectilePrefab)
        {
            if (!HasStateAuthority || slot < 0 || slot >= Bursts.Length) return;
            Burst b = Bursts.Get(slot);
            if (!b.Active || IsDead(b, i)) return;
            float t = BurstElapsed(b);
            Vector3 pos = ProjectilePosition(b, i, t);
            SetDeadBit(ref b, i);
            Bursts.Set(slot, b);

            if (singleProjectilePrefab == null) return;
            Fusion.NetworkObject obj = Runner.Spawn(singleProjectilePrefab, pos, Quaternion.LookRotation(newVelocity), newShooter);
            if (obj != null && obj.TryGetComponent(out TossZone.Throwing.NetworkProjectile np))
            {
                np.Shooter = newShooter;
                np.Launch(newVelocity, 0f, _damage);
            }
        }
    }
}
#endif
