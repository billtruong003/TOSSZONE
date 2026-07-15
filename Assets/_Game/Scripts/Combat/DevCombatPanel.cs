#if PHOTON_FUSION && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using UnityEngine;
using UnityEngine.InputSystem;

namespace TossZone.Combat
{
    /// <summary>
    /// Dev-only on-screen button panel for combat testing (T17) — one click per weapon instead of typing
    /// console commands (the CheatConsole TextField had a UIToolkit typing crash; buttons sidestep text input
    /// entirely). Spawned automatically by <see cref="CombatCheats"/> — no scene object, so release builds
    /// (where this class doesn't compile) get no missing-script leftovers. IMGUI renders on the desktop
    /// Game view / mirror only, not inside the headset — it's an Editor/desktop testing aid.
    /// F1 toggles visibility.
    /// </summary>
    public class DevCombatPanel : MonoBehaviour
    {
        private bool _open = true;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) _open = !_open;
        }

        private void OnGUI()
        {
            if (!_open) return;
            PlayerCombat combat = PlayerCombat.Local;
            if (combat == null || !combat.HasStateAuthority) return;
            WeaponConfig[] catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            if (catalog == null) return;   // not in a combat minigame yet

            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 540), GUI.skin.box);
            GUILayout.Label("DEV Weapons — F1 ẩn/hiện | equipped: " + combat.EquippedIndex);

            if (GUILayout.Button((combat.EquippedIndex < 0 ? "► " : "") + "Rock (mặc định)"))
                combat.EquipWeapon(-1);

            for (int i = 0; i < catalog.Length; i++)
            {
                WeaponConfig cfg = catalog[i];
                if (cfg == null) continue;
                bool equipped = combat.EquippedIndex == i;
                if (GUILayout.Button((equipped ? "► " : "") + i + ". " + cfg.displayName + "  [" + cfg.fireMode + "]"))
                {
                    combat.OwnCheat(i);      // bypass economy so a click always works
                    combat.EquipWeapon(i);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("HP " + combat.Health + "/" + PlayerCombat.MaxHealth + "   $" + combat.Money
                + "   Ammo " + combat.AmmoFor(combat.EquippedIndex));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+$100")) combat.AddMoneyCheat(100);
            if (GUILayout.Button("Heal")) combat.HealCheat();
            if (GUILayout.Button("+5 Ammo")) combat.GrantAmmo(combat.EquippedIndex, 5);
            GUILayout.EndHorizontal();

            // Dummy control — driver toggle + full despawn. Only the state authority (master) can actually
            // drive these; on a non-master client the buttons are shown disabled instead of silently no-op'ing.
            DummyBotDriver bot = FindFirstObjectByType<DummyBotDriver>();
            if (bot != null)
            {
                bool isAuthority = bot.Object != null && bot.Object.IsValid && bot.Object.HasStateAuthority;
                GUI.enabled = isAuthority;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(bot.enabled ? "Dummy bot: ON" : "Dummy bot: OFF"))
                    bot.enabled = !bot.enabled;
                if (GUILayout.Button("Xoá Dummy") && bot.Runner != null)
                    bot.Runner.Despawn(bot.Object);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
                if (!isAuthority) GUILayout.Label("(chỉ master điều khiển được dummy)");
            }
            GUILayout.EndArea();
        }
    }
}
#endif
