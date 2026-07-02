#if PHOTON_FUSION && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using BillGameCore;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Dev-only cheat commands for testing combat without grinding the real economy/gesture flow (T17 2-player
    /// test support). Registers into the existing <see cref="CheatConsole"/> (backtick `~` to open — needs a
    /// keyboard, so this helps PC/ParrelSync testing; a standalone Quest headset has no keyboard unless one is
    /// paired). Compiled out of release builds by CheatConsole's own guard, which this mirrors.
    /// </summary>
    public static class CombatCheats
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            if (!Bill.IsReady) { Bill.Events.SubscribeOnce<GameReadyEvent>(_ => RegisterNow()); return; }
            RegisterNow();
        }

        private static void RegisterNow()
        {
            CheatConsole cc = ServiceLocator.Get<CheatConsole>();
            if (cc == null) return;

            cc.Register<int>("money", amount =>
            {
                PlayerCombat local = PlayerCombat.Local;
                if (local == null || !local.HasStateAuthority) return;
                local.AddMoneyCheat(amount);
            }, "Give the local player money (testing only)");

            cc.Register("unlockall", () =>
            {
                PlayerCombat local = PlayerCombat.Local;
                if (local == null || !local.HasStateAuthority) return;
                WeaponConfig[] cat = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
                if (cat == null) return;
                for (int i = 0; i < cat.Length; i++) local.OwnCheat(i);
            }, "Own every weapon in the current catalog (testing only)");

            cc.Register<int>("equip", index =>
            {
                PlayerCombat local = PlayerCombat.Local;
                if (local == null || !local.HasStateAuthority) return;
                local.EquipWeapon(index);
            }, "Equip catalog slot index directly, bypassing buy/unlock-time gates (testing only)");

            cc.Register("heal", () =>
            {
                PlayerCombat local = PlayerCombat.Local;
                if (local != null && local.HasStateAuthority) local.HealCheat();
            }, "Restore the local player to full health (testing only)");

            // Button panel (DevCombatPanel) — spawned here instead of living in a scene so release builds
            // (where the class is compiled out) have no missing-script references.
            if (Object.FindFirstObjectByType<DevCombatPanel>() == null)
            {
                var go = new GameObject("[DevCombatPanel]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<DevCombatPanel>();
            }
        }
    }
}
#endif
