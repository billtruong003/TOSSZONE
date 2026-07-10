using BillInspector;
using UnityEngine;

namespace TossZone.Combat
{
    public enum RingElement { None = 0, Ice = 1, Fire = 2, Multi = 3, Speed = 4, Area = 5 }

    [CreateAssetMenu(menuName = "TOSSZONE/Buff Ring Config", fileName = "BuffRingConfig")]
    public class BuffRingConfig : ScriptableObject
    {
        public static readonly float[] DiameterPerTier = { 1.8f, 1.5f, 1.2f, 0.9f, 0.6f };
        // REFF (Session 17.13): old drift {1, 1.5, 2, 2.5, 3.5} double-penalized high tiers — T5 was both
        // the smallest ring (0.6m) AND the fastest, so angular difficulty scaled ~10× over T1
        // (drift/diameter 0.56 → 5.83/s). Flattened so size stays the main difficulty axis.
        public static readonly float[] DriftSpeedPerTier = { 1f, 1.4f, 1.8f, 2.2f, 2.6f };

        [BillTitle("Identity")]
        [BillRequired] public string id = "ring_ice";
        public string displayName = "Băng";
        public RingElement element = RingElement.Ice;
        public Color ringColor = Color.cyan;
        public Sprite icon;

        [BillTitle("Buff value THEO TIER (GDD §VI — index 0 = Tier 1 … index 4 = Tier 5)")]
        [BillInfoBox("Ý nghĩa theo element: Multi = số đạn (2/4/8/12/15) · Speed = hệ số vận tốc bay " +
                     "(1.2/1.4/1.6/1.8/2.0) · Area = hệ số bán kính nổ (1.25/1.5/1.75/2/2.25) · " +
                     "Ice = giây đóng băng + đời tường băng (1/1.5/2/2.5/3) · Fire = đời vùng lửa giây (1/1.5/2/2.5/3).")]
        public float[] valuePerTier = new float[5] { 1f, 1f, 1f, 1f, 1f };

        [BillTitle("Vòng đời")]
        [BillSuffix("s")] public float respawnDelay = 10f;

        public float ValueForTier(int tier)
        {
            if (valuePerTier == null || valuePerTier.Length == 0) return 1f;
            return valuePerTier[Mathf.Clamp(tier, 1, valuePerTier.Length) - 1];
        }

        public static float DiameterForTier(int tier) => DiameterPerTier[Mathf.Clamp(tier, 1, 5) - 1];
        public static float DriftSpeedForTier(int tier) => DriftSpeedPerTier[Mathf.Clamp(tier, 1, 5) - 1];

        public static Color ElementColor(RingElement element)
        {
            switch (element)
            {
                case RingElement.Ice: return new Color(0.35f, 0.9f, 1f);
                case RingElement.Fire: return new Color(1f, 0.4f, 0.1f);
                case RingElement.Multi: return new Color(1f, 0.9f, 0.2f);
                case RingElement.Speed: return new Color(0.45f, 0.75f, 1f);
                case RingElement.Area: return new Color(0.7f, 0.4f, 1f);
                default: return Color.white;
            }
        }
    }
}
