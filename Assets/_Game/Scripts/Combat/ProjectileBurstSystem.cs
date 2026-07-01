#if PHOTON_FUSION
using System.Collections.Generic;
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
    /// (<see cref="ProjectileBurstRenderer"/>); only the burst spawn and hit RPCs cross the wire.
    ///
    /// MVP scope: data + deterministic flight + authority hit → RPC damage + instanced render. Per-projectile
    /// dead-mask (for catch/deflect visual removal) and ring wiring are follow-ups; here the authority just
    /// avoids double-hitting a projectile via a LOCAL set (authority-only, so no networking needed for that).
    /// </summary>
    public class ProjectileBurstSystem : NetworkBehaviour
    {
        public static ProjectileBurstSystem Instance { get; private set; }

        public const int MaxBursts = 32;
        public const int MaxProjectilesPerBurst = 4096;   // hard cap per burst (design §7)

        [SerializeField] private float _baseSpeed = 7f;
        [SerializeField] private float _spreadDegrees = 22f;
        [SerializeField] private float _lifetime = 4f;
        [SerializeField] private float _hitRadius = 0.35f;
        [SerializeField] private int _damage = 1;

        [Networked, Capacity(MaxBursts)] private NetworkArray<Burst> Bursts => default;

        // authority-only: projectiles already resolved (hit) this life, so we don't double-hit. Keyed burstSlot*BIG+i.
        private readonly HashSet<long> _resolved = new();

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
            public PlayerRef Shooter;
        }

        public override void Spawned() => Instance = this;
        public override void Despawned(NetworkRunner runner, bool hasState) { if (Instance == this) Instance = null; }

        // ── Spawn (authority) ───────────────────────────────────────────────────────────
        /// <summary>Authority spawns a rain burst. Returns the slot, or -1 if full / not authority.</summary>
        public int SpawnBurst(Vector3 origin, Vector3 dir, int count, float gravity, int element, PlayerRef shooter)
        {
            if (!HasStateAuthority) return -1;
            count = Mathf.Clamp(count, 1, MaxProjectilesPerBurst);
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

            for (int s = 0; s < Bursts.Length; s++)
            {
                Burst b = Bursts.Get(s);
                if (!b.Active) continue;

                float t = BurstElapsed(b);
                if (t >= _lifetime)
                {
                    b.Active = false;
                    Bursts.Set(s, b);
                    // clear this slot's resolved entries
                    _resolved.RemoveWhere(k => k / MaxProjectilesPerBurst == s);
                    continue;
                }

                // Hit test each projectile vs real players (cheap distance check; cap the per-tick scan).
                int scan = Mathf.Min(b.Count, MaxProjectilesPerBurst);
                for (int i = 0; i < scan; i++)
                {
                    long key = (long)s * MaxProjectilesPerBurst + i;
                    if (_resolved.Contains(key)) continue;
                    Vector3 p = ProjectilePosition(b, i, t);

                    foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                    {
                        if (!pc.IsPlayer || pc.Object == null) continue;
                        if (pc.Object.InputAuthority == b.Shooter) continue;   // don't hit the shooter
                        if (pc.Health <= 0) continue;
                        Vector3 chest = pc.transform.position + Vector3.up * 1.0f;
                        if ((p - chest).sqrMagnitude <= _hitRadius * _hitRadius)
                        {
                            _resolved.Add(key);
                            pc.RPC_TakeHit(_damage, p, b.Shooter);
                            break;
                        }
                    }
                }
            }
        }
    }
}
#endif
