using BillInspector;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>How the trigger maps to fire events. Burst is deferred past P0 (see Gun_System_Architecture.md
    /// §3 — subclass only when the fire-loop SHAPE changes, semi/auto/burst are config on the same class).</summary>
    public enum GunFireMode { Semi, Auto }

    /// <summary>
    /// Designer data for ONE gun — the configurable seam for the AR/SMG/Pistol roster (Gun_System_Architecture.md
    /// §3). Deliberately separate from <see cref="TossZone.Combat.WeaponConfig"/>, which is the older
    /// party-game throw/economy config (rock/grenade/buff-ring fields do not belong here). P0 simplification:
    /// FxSet/SoundSet are NOT split into their own ScriptableObjects yet — pool/audio keys live directly on this
    /// asset since there is only one weapon; revisit if/when skin swapping needs per-skin FX overrides.
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Guns/Gun Config", fileName = "GunConfig")]
    public class GunConfig : ScriptableObject
    {
        [BillTitle("Identity")]
        [BillRequired] public string id = "ak74";
        [BillInfoBox("Index into GunCatalog.configs — must match the array slot this asset is placed in.")]
        public byte weaponId;
        public string displayName = "AR";

        [BillTitle("Fire")]
        public GunFireMode fireMode = GunFireMode.Auto;
        [BillSuffix("rpm")] public float roundsPerMinute = 600f;
        public int magazineSize = 30;
        [BillSuffix("s")] public float reloadSeconds = 1.8f;
        [BillInfoBox("Hitscan spread half-angle in degrees while standing still and aiming steady. P0 keeps " +
                     "this a single constant; per-shot bloom growth is a 1.1.2+ refinement, not required for " +
                     "the placeholder loop.")]
        [BillSlider(0f, 10f)] public float spreadDegrees = 1.5f;

        [BillTitle("Damage (victim resolves this from GunCatalog — see Docs/Gun_System_Architecture.md §3/§7)")]
        public int bodyDamage = 16;
        [BillSlider(1f, 4f)] public float headshotMultiplier = 2f;
        [BillSuffix("m")] public float range = 40f;
        [BillInfoBox("Damage multiplier at max range vs. point-blank. 1 = no falloff (P0 default).")]
        [BillSlider(0f, 1f)] public float falloffAtMaxRange = 1f;

        [BillTitle("View")]
        [BillInfoBox("Wrapper prefab under Assets/_Game/Art/Weapons/P0/ — NOT the raw vendor model. Must have " +
                     "a child transform named exactly \"MuzzleAnchor\".")]
        public GameObject modelPrefab;

        [BillTitle("FX/SFX keys (Bill.Pool / Bill.Audio)")]
        public string tracerPoolKey = "gun_ak74_tracer";
        public string muzzleFlashPoolKey = "gun_ak74_muzzle";
        public string impactPoolKey = "gun_ak74_impact";
        public string fireSoundKey = "gun_ak74_fire";
        public string dryFireSoundKey = "gun_dryfire";
        public string reloadSoundKey = "gun_ak74_reload";

        public float FireIntervalSeconds => roundsPerMinute > 0f ? 60f / roundsPerMinute : 0.1f;

        public int ResolveDamage(float distance, bool isHead)
        {
            float t = range > 0f ? Mathf.Clamp01(distance / range) : 0f;
            float falloff = Mathf.Lerp(1f, falloffAtMaxRange, t);
            float dmg = bodyDamage * falloff;
            if (isHead) dmg *= headshotMultiplier;
            return Mathf.RoundToInt(dmg);
        }
    }
}
