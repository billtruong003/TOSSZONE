#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Per-player networked combat state (health + money) for the arena minigame. One per player, on the same
    /// NetworkObject as <see cref="TossZone.Player.NetworkAvatar"/>.
    ///
    /// Shared Mode (see <c>Docs/Fusion_Shared_Mode_Gotchas.md</c>): each client has StateAuthority over its OWN
    /// avatar, so a player writes their OWN <see cref="Health"/>. An incoming hit is applied via
    /// <see cref="RPC_TakeHit"/> — the shooter's projectile invokes it on all clients, but only the victim's
    /// StateAuthority writes Health (everyone fires the juice event). Money ticks up passively + on landed hits and
    /// resets to $0 each round. All feedback goes through <c>Bill.Events</c> so UI/juice stay decoupled.
    /// </summary>
    public class PlayerCombat : NetworkBehaviour
    {
        public const int MaxHealth = 5;

        [Networked] public int Health { get; set; }
        [Networked] public int Money { get; set; }

        /// <summary>The local player's own combat state (the one we hold authority over). Survives scene loads
        /// (Fusion's player-object registry does NOT — gotchas §6). Mirrors <see cref="TossZone.Player.NetworkAvatar.Local"/>.</summary>
        public static PlayerCombat Local { get; private set; }

        [Header("Economy (ví reset $0 mỗi hiệp)")]
        [Tooltip("Passive income per second.")]
        [SerializeField] private float _incomePerSecond = 1f;
        [Tooltip("Money rewarded to the shooter per landed hit.")]
        [SerializeField] private int _hitReward = 10;

        public int HitReward => _hitReward;

        private float _incomeAccum;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                Local = this;
                if (Health <= 0) Health = MaxHealth;   // init; guard so a re-Spawned object keeps a live value
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            // Passive income — only the authority (the local owner) writes the networked wallet.
            _incomeAccum += _incomePerSecond * Runner.DeltaTime;
            if (_incomeAccum >= 1f)
            {
                int add = (int)_incomeAccum;
                _incomeAccum -= add;
                AddMoney(add);
            }
        }

        /// <summary>Apply an incoming hit. The shooter's projectile invokes this on all clients; only the victim's
        /// StateAuthority writes Health, everyone fires <see cref="PlayerHitEvent"/> for local VFX/haptic/UI.</summary>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_TakeHit(int damage, Vector3 point, PlayerRef shooter)
        {
            int remaining = Health;
            if (HasStateAuthority)
            {
                remaining = Mathf.Max(0, Health - damage);
                Health = remaining;
            }
            if (!Bill.IsReady) return;
            Bill.Events.Fire(new PlayerHitEvent
            {
                Damage = damage,
                RemainingHealth = remaining,
                Point = point,
                IsLocalVictim = HasStateAuthority
            });
            if (HasStateAuthority && remaining <= 0)
                Bill.Events.Fire(new PlayerDiedEvent { IsLocal = true });
        }

        /// <summary>Authority (the shooter): reward this player for a landed hit.</summary>
        public void RewardHit()
        {
            if (HasStateAuthority) AddMoney(_hitReward);
        }

        /// <summary>Authority: reset for a new round (called by the minigame manager).</summary>
        public void ResetForRound()
        {
            if (!HasStateAuthority) return;
            Health = MaxHealth;
            _incomeAccum = 0f;
            Money = 0;
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
        }

        private void AddMoney(int amount)
        {
            Money += amount;
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
        }
    }
}
#endif
