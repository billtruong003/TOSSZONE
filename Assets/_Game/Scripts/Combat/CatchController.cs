#if PHOTON_FUSION
using BillGameCore;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Catch-zone trigger sphere on the catching hand. When an incoming <see cref="ThrowProjectile"/> or
    /// <see cref="NetworkProjectile"/> enters the zone, evaluates the catch outcome table (design §8):
    ///
    /// <list type="table">
    ///   <item>Normal ball (white)        → successful catch, gains 1 ammo / "free throw"</item>
    ///   <item>Power ball (purple/tinted) → power catch → grants 1 Power throw (purple arc, +1 dmg)</item>
    ///   <item>Uncatchable ball           → no catch (pass through)</item>
    /// </list>
    ///
    /// Fires <see cref="BallCaughtEvent"/> locally (for haptics, VFX). The catch grants <see cref="PlayerCombat.Ammo"/>
    /// +1 (authority). Place on the non-throwing hand; wire a SphereCollider (trigger, radius ≈ 0.15 m).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CatchController : MonoBehaviour
    {
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private float _catchRadius = 0.15f;

        private SphereCollider _zone;

        private void Awake()
        {
            _zone = GetComponent<SphereCollider>();
            _zone.isTrigger = true;
            _zone.radius = _catchRadius;
        }

        private void Update()
        {
            // Burst-rain projectiles have no collider (they're data, not GameObjects — see
            // ProjectileBurstSystem), so OnTriggerEnter never fires for them. Poll for a nearby live one each
            // frame instead; the query is local/read-only (deterministic flight, every client agrees), only the
            // dead-mark needs an RPC to the burst's authority. Ammo is granted immediately/locally since this
            // client owns _combat's authority — Shared Mode rule: only the catcher writes their own PlayerCombat.
            if (_combat == null || !_combat.HasStateAuthority) return;
            ProjectileBurstSystem sys = ProjectileBurstSystem.Instance;
            if (sys == null) return;
            if (sys.TryConsumeNear(transform.position, _catchRadius, out int slot, out int i))
            {
                Vector3 pos = transform.position;
                sys.RPC_RequestCatch(slot, i, pos);
                RegisterCatch(isPower: false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_combat == null || !_combat.HasStateAuthority) return;

            // Local ThrowProjectile (non-networked arc)
            if (other.TryGetComponent(out ThrowProjectile localProj))
            {
                if (!localProj.IsCatchable) return;
                localProj.OnCaught();
                RegisterCatch(isPower: localProj.IsPower);
                return;
            }

            // Networked projectile (proxy)
            if (other.TryGetComponent(out NetworkProjectile netProj))
            {
                // Uncatchable flag set on NetworkProjectile (isUncatchable weapon)
                // We check VelocityScale as proxy for uncatchable for now; extend once field is networked.
                bool power = netProj.Element != 0;
                RegisterCatch(isPower: power);
            }
        }

        private void RegisterCatch(bool isPower)
        {
            _combat.GrantAmmo(_combat.EquippedIndex, isPower ? 2 : 1);
            if (Bill.IsReady) Bill.Events.Fire(new BallCaughtEvent { IsPower = isPower });
        }
    }

    public struct BallCaughtEvent : IEvent { public bool IsPower; }
}
#endif
