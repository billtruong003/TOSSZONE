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
        /// <summary>Bitmask of BuyOnce weapon slots owned this round (bit i = catalog index i).</summary>
        [Networked] public int OwnedMask { get; set; }
        /// <summary>Currently equipped catalog index (-1 = rock / default).</summary>
        [Networked] public int EquippedIndex { get; set; }
        /// <summary>Ammo remaining for PayPerUse weapons.</summary>
        [Networked] public int Ammo { get; set; }

        /// <summary>All live PlayerCombat instances on this client — polled by ArenaManager to check alive count.</summary>
        public static readonly System.Collections.Generic.List<PlayerCombat> AllInstances
            = new System.Collections.Generic.List<PlayerCombat>();

        /// <summary>True for real players; false for bots (DummyAvatar). Set by the owning component.</summary>
        public bool IsPlayer { get; set; } = true;

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
            AllInstances.Add(this);
            if (HasStateAuthority)
            {
                Local = this;
                if (Health <= 0) Health = MaxHealth;
                if (EquippedIndex == 0) EquippedIndex = -1;   // 0 = default int, use -1 for "no override"
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            AllInstances.Remove(this);
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

        /// <summary>Authority: reset for a new round (called by ArenaManager).</summary>
        public void ResetForRound()
        {
            if (!HasStateAuthority) return;
            Health = MaxHealth;
            Money = 0;
            OwnedMask = 0;
            EquippedIndex = -1;
            Ammo = 0;
            _incomeAccum = 0f;
            if (!Bill.IsReady) return;
            Bill.Events.Fire(new MoneyChangedEvent { Money = 0 });
            Bill.Events.Fire(new WeaponResetEvent());
        }

        /// <summary>Authority: buy a BuyOnce weapon slot — deducts cost, sets ownership bit.</summary>
        public bool TryBuyWeapon(int slotIndex, int cost)
        {
            if (!HasStateAuthority || Money < cost) return false;
            Money -= cost;
            OwnedMask |= (1 << slotIndex);
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
            return true;
        }

        public bool OwnsWeapon(int slotIndex) => (OwnedMask & (1 << slotIndex)) != 0;

        /// <summary>Authority: equip a weapon slot (index into the per-minigame catalog).</summary>
        public void EquipWeapon(int slotIndex) { if (HasStateAuthority) EquippedIndex = slotIndex; }

        /// <summary>Authority: consume 1 ammo unit. Returns false if out of ammo.</summary>
        public bool UseAmmo()
        {
            if (!HasStateAuthority || Ammo <= 0) return false;
            Ammo--;
            return true;
        }

        private void AddMoney(int amount)
        {
            Money += amount;
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
        }
    }
}
#endif
