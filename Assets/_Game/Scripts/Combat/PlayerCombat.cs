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
        [Networked] private TickTimer FrozenTimer { get; set; }

        public bool IsFrozen => Object != null && Object.IsValid && Runner != null
            && !FrozenTimer.ExpiredOrNotRunning(Runner);

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
            // T17 fix: gating on HasStateAuthority alone raced with DummyAvatar.Spawned() setting IsPlayer=false
            // on a SIBLING component — Fusion doesn't order Spawned() across NetworkBehaviours on the same
            // object, so PlayerCombat.Spawned() could run first and wrongly claim Local for the scene dummy
            // (both it and a real player have HasStateAuthority=true in solo/master testing). InputAuthority is
            // set by Fusion at spawn time and is already correct by the time ANY component's Spawned() runs —
            // scene objects like DummyAvatar always have InputAuthority == None (same distinguishing signal
            // NetworkProjectile's hit-test already relies on), so this is order-independent.
            if (HasStateAuthority && Object.InputAuthority != PlayerRef.None)
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
                if (damage > 0) FrozenTimer = default;
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

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_Freeze(float seconds)
        {
            if (HasStateAuthority && Health > 0)
                FrozenTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Clamp(seconds, 0.1f, 10f));
            if (Bill.IsReady)
                Bill.Events.Fire(new PlayerFrozenEvent { Seconds = seconds, IsLocalVictim = HasStateAuthority });
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
            FrozenTimer = default;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>T17 cheat-console support (see CombatCheats.cs) — grants money with no economy checks.
        /// Testing only; compiled out of release builds.</summary>
        public void AddMoneyCheat(int amount) { if (HasStateAuthority) AddMoney(amount); }

        /// <summary>T17 cheat-console support — owns a weapon slot with no cost deducted. Testing only.</summary>
        public void OwnCheat(int slotIndex) { if (HasStateAuthority) OwnedMask |= (1 << slotIndex); }

        /// <summary>T17 cheat-console support — full heal without the round reset ResetForRound would drag in
        /// (money/weapons kept). Testing only.</summary>
        public void HealCheat() { if (HasStateAuthority) Health = MaxHealth; }
#endif

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
