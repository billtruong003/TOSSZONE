using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// T20 — shared spawner for PROJECTILE cosmetics: a stripped visual copy of a weapon's flying-shot model
    /// (<see cref="WeaponConfig.ProjectileVisualPrefab"/>) parented under a projectile. Same strip rules as
    /// HandWeapon.SpawnHeldVisual (MS_WP_* prefabs are live AutoHand props — instantiate under an INACTIVE
    /// holder so nothing Awakes, strip behaviours/colliders/rigidbody, then activate) but WITHOUT the per-hand
    /// hold offsets — projectiles want the raw model at the pivot, scaled by
    /// <see cref="WeaponConfig.projectileVisualScale"/>.
    /// </summary>
    public static class WeaponVisuals
    {
        /// <summary>Spawn the flying-shot cosmetic for <paramref name="cfg"/> under <paramref name="parent"/>.
        /// Returns null when the config has no model wired.</summary>
        public static GameObject SpawnProjectileVisual(WeaponConfig cfg, Transform parent)
        {
            GameObject prefab = cfg != null ? cfg.ProjectileVisualPrefab : null;
            if (prefab == null) return null;

            var holder = new GameObject("ProjectileVisual(" + cfg.id + ")");
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);

            GameObject model = Object.Instantiate(prefab, holder.transform);
            foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            if (model.TryGetComponent(out Rigidbody rb)) Object.DestroyImmediate(rb);

            if (!Mathf.Approximately(cfg.projectileVisualScale, 1f))
                model.transform.localScale *= Mathf.Max(0.01f, cfg.projectileVisualScale);

            holder.SetActive(true);
            return holder;
        }
    }
}
