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
        public const float IncomePerSecond = 2f;
        public const int KillReward = 5;
        public const int CompensationPerLife = 10;
        public const float InvulnSeconds = 3f;
        public const int BountyPerKill = 2;

        /// <summary>D3 (owner-locked 2026-07-15): v0.3-P0 health is 100 HP — death at HP ≤ 0, respawn resets
        /// to full (task 1.3.2). <see cref="MaxLives"/> below stays as the legacy party-mode lives knob
        /// (ArenaManager.NetMaxLives economy/round sizing) but no longer seeds <see cref="Health"/>.</summary>
        public const int MaxHealth = 100;

        public static int MaxLives { get; set; } = 5;

        public static int LivesForPlayerCount(int realPlayers)
        {
            int perTeam = Mathf.Max(1, (realPlayers + 1) / 2);
            // UXH-01 (Session 17.13): 1v1 was 7 lives but HealthUI only has 5 pips — health 6-7 rendered
            // identically to 5. Capped at 5 to match the UI (and keep 1v1 rounds short for VR sessions).
            return perTeam <= 3 ? 5 : 4;
        }

        [Networked] public int Health { get; set; }
        [Networked] public int Money { get; set; }
        /// <summary>Bitmask of BuyOnce weapon slots owned this round (bit i = catalog index i).</summary>
        [Networked] public int OwnedMask { get; set; }
        /// <summary>Currently equipped catalog index (-1 = rock / default).</summary>
        [Networked] public int EquippedIndex { get; set; }
        [Networked, Capacity(16)] private NetworkArray<int> AmmoSlots => default;
        [Networked] private TickTimer FrozenTimer { get; set; }
        [Networked] private TickTimer InvulnTimer { get; set; }
        [Networked] public int Bounty { get; set; }

        public bool IsFrozen => Object != null && Object.IsValid && Runner != null
            && !FrozenTimer.ExpiredOrNotRunning(Runner);

        public bool IsInvulnerable => Object != null && Object.IsValid && Runner != null
            && !InvulnTimer.ExpiredOrNotRunning(Runner);

        /// <summary>All live PlayerCombat instances on this client — polled by ArenaManager to check alive count.</summary>
        public static readonly System.Collections.Generic.List<PlayerCombat> AllInstances
            = new System.Collections.Generic.List<PlayerCombat>();

        /// <summary>True for real players; false for bots (DummyAvatar). Set by the owning component.</summary>
        public bool IsPlayer { get; set; } = true;

        /// <summary>The local player's own combat state (the one we hold authority over). Survives scene loads
        /// (Fusion's player-object registry does NOT — gotchas §6). Mirrors <see cref="TossZone.Player.NetworkAvatar.Local"/>.</summary>
        public static PlayerCombat Local { get; private set; }

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
            _incomeAccum += IncomePerSecond * Runner.DeltaTime;
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
            if (IsInvulnerable) return;

            int previous = Health;
            int remaining = Mathf.Max(0, previous - damage);
            int livesLost = Mathf.Max(0, previous - remaining);
            int bounty = Bounty;

            if (HasStateAuthority)
            {
                Health = remaining;
                if (livesLost > 0)
                {
                    FrozenTimer = default;
                    if (IsPlayer) InvulnTimer = TickTimer.CreateFromSeconds(Runner, InvulnSeconds);
                    Bounty = 0;
                    AddMoney(CompensationPerLife * livesLost);
                }
            }

            if (livesLost > 0) RewardShooterLocal(shooter, livesLost, bounty);

            if (!Bill.IsReady) return;
            Bill.Events.Fire(new PlayerHitEvent
            {
                Damage = damage,
                RemainingHealth = remaining,
                Point = point,
                IsLocalVictim = HasStateAuthority
            });
            if (HasStateAuthority && remaining <= 0 && previous > 0)
                Bill.Events.Fire(new PlayerDiedEvent { IsLocal = true });
        }

        /// <summary>Task 1.3.2 — the ONE validated gun-damage entry point (D3: 100 HP). Called ONLY by the
        /// victim's own <c>AvatarWeaponSync</c> AFTER a ShotClaim passed the §7 validator, so it runs on the
        /// victim's State Authority (Shared Mode: we write our own Health). Deliberately NOT an RPC — the wire
        /// seam is the validated RPC_SubmitShotClaim; a raw damage RPC would reopen the trust hole 1.3.1
        /// closed. No money/bounty/score here: kill attribution is task 1.3.3. Death fires exactly once (the
        /// previous&gt;0 → 0 transition); NetworkAvatar.HandleRespawn picks up Health ≤ 0 unchanged.</summary>
        public bool ApplyValidatedDamage(int damage, PlayerRef shooter, Vector3 point)
        {
            if (!HasStateAuthority || damage <= 0 || Health <= 0 || IsInvulnerable) return false;

            int previous = Health;
            int remaining = Mathf.Max(0, previous - damage);   // acceptance: Health never negative
            Health = remaining;

            if (!Bill.IsReady) return true;
            Bill.Events.Fire(new PlayerHitEvent
            {
                Damage = damage,
                RemainingHealth = remaining,
                Point = point,
                IsLocalVictim = true,
            });
            if (remaining <= 0 && previous > 0)
                Bill.Events.Fire(new PlayerDiedEvent { IsLocal = true });
            return true;
        }

        private void RewardShooterLocal(PlayerRef shooter, int livesLost, int victimBounty)
        {
            if (shooter == PlayerRef.None || Local == null || Local == this) return;
            if (Local.Object == null || !Local.Object.IsValid || Local.Object.InputAuthority != shooter) return;
            Local.AddMoney(KillReward * livesLost + victimBounty);
            Local.Bounty += BountyPerKill * livesLost;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_Freeze(float seconds)
        {
            if (HasStateAuthority && Health > 0)
            {
                float incoming = Mathf.Clamp(seconds, 0.1f, 10f);
                float remaining = FrozenTimer.ExpiredOrNotRunning(Runner) ? 0f : (FrozenTimer.RemainingTime(Runner) ?? 0f);
                if (incoming > remaining)
                    FrozenTimer = TickTimer.CreateFromSeconds(Runner, incoming);
            }
            if (Bill.IsReady)
                Bill.Events.Fire(new PlayerFrozenEvent { Seconds = seconds, IsLocalVictim = HasStateAuthority });
        }

        /// <summary>Authority: reset for a new round (called by ArenaManager).</summary>
        public void ResetForRound()
        {
            if (!HasStateAuthority) return;
            Health = MaxHealth;
            Money = 0;
            OwnedMask = 0;
            EquippedIndex = 0;
            for (int i = 0; i < AmmoSlots.Length; i++) AmmoSlots.Set(i, 0);
            Bounty = 0;
            FrozenTimer = default;
            InvulnTimer = default;
            _incomeAccum = 0f;
            if (!Bill.IsReady) return;
            Bill.Events.Fire(new MoneyChangedEvent { Money = 0 });
            Bill.Events.Fire(new WeaponResetEvent());
        }

        /// <summary>Authority: refill health (mid-round respawn) — wallet and owned weapons persist.
        /// 1.3.2: respawn ARMS spawn protection (instead of clearing it) so claims raced across the
        /// death/respawn window reject as SpawnProtected in the HitValidator (§7). Dummies stay
        /// unprotected (IsPlayer gate, same as RPC_TakeHit) so the shooting range keeps flowing.</summary>
        public void RestoreLives()
        {
            if (!HasStateAuthority) return;
            Health = MaxHealth;
            FrozenTimer = default;
            InvulnTimer = IsPlayer ? TickTimer.CreateFromSeconds(Runner, InvulnSeconds) : default;
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

        public int AmmoFor(int slotIndex)
            => slotIndex >= 0 && slotIndex < AmmoSlots.Length ? AmmoSlots.Get(slotIndex) : 0;

        /// <summary>Authority: pay <paramref name="cost"/> for one magazine of a PayPerUse weapon.</summary>
        public bool TryBuyAmmo(int slotIndex, int cost, int magazine)
        {
            if (!HasStateAuthority || slotIndex < 0 || slotIndex >= AmmoSlots.Length || Money < cost) return false;
            Money -= cost;
            AmmoSlots.Set(slotIndex, AmmoSlots.Get(slotIndex) + Mathf.Max(1, magazine));
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
            return true;
        }

        public void GrantAmmo(int slotIndex, int amount)
        {
            if (!HasStateAuthority || slotIndex < 0 || slotIndex >= AmmoSlots.Length) return;
            AmmoSlots.Set(slotIndex, AmmoSlots.Get(slotIndex) + amount);
        }

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

        /// <summary>Authority: consume 1 ammo unit of a slot. Returns false if out of ammo.</summary>
        public bool UseAmmo(int slotIndex)
        {
            if (!HasStateAuthority || slotIndex < 0 || slotIndex >= AmmoSlots.Length) return false;
            int ammo = AmmoSlots.Get(slotIndex);
            if (ammo <= 0) return false;
            AmmoSlots.Set(slotIndex, ammo - 1);
            return true;
        }

        /// <summary>Authority: consume 1 ammo, auto-buying a fresh magazine when empty. False = broke + empty.</summary>
        public bool UseOrBuyAmmo(int slotIndex, WeaponConfig cfg)
        {
            if (cfg == null || !cfg.IsPayPerUse || CombatSession.TrainingModeActive) return true;
            if (UseAmmo(slotIndex)) return true;
            return TryBuyAmmo(slotIndex, cfg.cost, cfg.magazine) && UseAmmo(slotIndex);
        }

        private void AddMoney(int amount)
        {
            Money += amount;
            if (Bill.IsReady) Bill.Events.Fire(new MoneyChangedEvent { Money = Money });
        }
    }
}
#endif
