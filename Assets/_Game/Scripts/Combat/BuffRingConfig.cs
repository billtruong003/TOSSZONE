using BillInspector;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>Ring element / type (matches design §16).</summary>
    public enum RingElement { None = 0, Ice = 1, Fire = 2, Multi = 3, Speed = 4, Shield = 5 }

    /// <summary>
    /// Designer data for ONE buff-ring type. Five types from design §16:
    /// Băng (Ice), Lửa (Fire), Đạn Mưa (Multi), Tốc Độ (Speed), Chắn Đạn (Shield).
    /// The ring grants its buff to the first player whose projectile passes through it.
    /// Stack limit = 3 per element.
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Buff Ring Config", fileName = "BuffRingConfig")]
    public class BuffRingConfig : ScriptableObject
    {
        [BillTitle("Identity")]
        [BillRequired] public string id = "ring_ice";
        public string displayName = "Băng";
        public RingElement element = RingElement.Ice;
        public Color ringColor = Color.cyan;
        public Sprite icon;

        [BillTitle("Buff values")]
        [BillInfoBox("Giá trị áp lên NetworkProjectile khi đi xuyên qua. Stacks lên tối đa 3.")]
        [BillSlider(1, 3)] public int multiplier = 1;          // Đạn Mưa: số đạn spawn thêm
        [BillSlider(0.5f, 3f)] public float velocityScale = 1f; // Tốc Độ
        [BillSlider(0.5f, 3f)] public float areaScale = 1f;     // mở rộng hitRadius
        public bool shieldSelf = false;                          // Chắn Đạn: chủ nhân được shield

        [BillTitle("Vòng đời")]
        [BillSuffix("s")] public float respawnDelay = 10f;
        [BillSuffix("s")] public float driftAmplitude = 0.2f;
        [BillSuffix("s")] public float driftPeriod = 3f;
    }
}
