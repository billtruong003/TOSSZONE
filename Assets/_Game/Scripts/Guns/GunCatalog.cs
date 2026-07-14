using BillInspector;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>
    /// weaponId -> GunConfig lookup. Loaded from Resources so any client (in particular a victim's
    /// AvatarWeaponSync, task 1.3.1) can resolve damage from a bare weaponId byte without needing a live
    /// reference exchanged over the network — see Docs/Gun_System_Architecture.md §3 ("GunCatalog: weaponId
    /// -> GunConfig (shop + validator + proxy visual tra chung)").
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Guns/Gun Catalog", fileName = "GunCatalog")]
    public class GunCatalog : ScriptableObject
    {
        private const string ResourcePath = "Guns/GunCatalog";

        [BillRequired] public GunConfig[] configs = new GunConfig[0];

        private static GunCatalog _default;

        /// <summary>Lazily loaded singleton from Resources/Guns/GunCatalog.asset. Null if the asset is missing
        /// (logs once, callers must null-check — this is a content/setup error, not something to silently
        /// default around).</summary>
        public static GunCatalog Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<GunCatalog>(ResourcePath);
                if (_default == null)
                    Debug.LogError("[GunCatalog] Missing Resources/" + ResourcePath + ".asset");
                return _default;
            }
        }

        public GunConfig Get(byte weaponId)
        {
            for (int i = 0; i < configs.Length; i++)
                if (configs[i] != null && configs[i].weaponId == weaponId) return configs[i];
            return null;
        }

        public int ResolveDamage(byte weaponId, float distance, bool isHead)
        {
            GunConfig cfg = Get(weaponId);
            return cfg != null ? cfg.ResolveDamage(distance, isHead) : 0;
        }
    }
}
