using BillInspector;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Feel levers for the throw (see <c>Docs/Throw_Mechanic_Spec.md</c>). BALLISTIC model: the ball launches
    /// with the player's real hand-release velocity and flies a true parabola under <see cref="gravity"/> — it
    /// goes exactly where + how hard you throw (accurate aim). Low gravity = flat/straight shot; high gravity =
    /// lob. Still <c>BillTween</c>-driven (deterministic, network-friendly), just replaying real ballistics
    /// instead of a designer arc curve.
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Throw Config", fileName = "ThrowConfig")]
    public class ThrowConfig : ScriptableObject
    {
        [BillTitle("Trigger (forward swing → fire at PEAK speed)")]
        [BillInfoBox("Giữ grip + vung tay tới trước. Lực & hướng lấy ở ĐỈNH cú vung (không phải lúc tay duỗi hết) rồi mượt qua vài frame → ném đều, đúng hướng, đủ lực. Vung mạnh hơn = bắn mạnh hơn. Đo theo THÂN (đã trừ joystick).")]
        [BillSuffix("m/s")] [BillInfoBox("Tay lùi nhanh hơn mức này = 'lên đạn' lại cho cú ném kế (reset đỉnh).")]
        public float windBackSpeed = 0.35f;
        [BillSuffix("m/s")] [BillInfoBox("Tốc độ ĐỈNH tối thiểu để tính là 1 cú ném (thấp = dễ kích hoạt).")]
        public float vMinFire = 1.0f;
        [BillSlider(0.3f, 0.95f)] [BillInfoBox("BẮN khi tốc độ tụt còn x lần đỉnh = điểm buông tay. Thấp = buông chắc/trễ; cao = nhạy/sớm.")]
        public float releaseDrop = 0.7f;
        [BillSuffix("s")] public float cooldown = 0.35f;

        [BillTitle("Ballistic flight (real throw velocity + gravity)")]
        [BillSuffix("m/s²")] [BillInfoBox("THẲNG ↔ VÒNG: thấp (~3-5) = bay thẳng/căng; cao (~9.8) = vòng cung lob.")]
        public float gravity = 5f;
        [BillInfoBox("Nhân vận tốc tay → flick nhẹ vẫn bay mạnh (1 = đúng tốc độ tay thật).")]
        public float velocityScale = 1.3f;
        [BillSuffix("m/s")] [BillInfoBox("Sàn tốc độ phóng (flick nhẹ vẫn bay chừng này).")]
        public float minLaunchSpeed = 4f;
        [BillSuffix("m/s")] public float maxLaunchSpeed = 12f;
        [BillInfoBox("Layer mặt đất để tính điểm chạm (set = Floor nếu chân bám nhầm).")]
        public LayerMask groundMask = ~0;

        [BillTitle("Juice")]
        [BillSuffix("m")] public float heldBallScale = 0.09f;
        [BillSlider(0f, 1f)] public float hapticWind = 0.15f;
        [BillSlider(0f, 1f)] public float hapticRelease = 0.85f;
        [BillSlider(0f, 1f)] public float hapticImpact = 0.5f;
        public Color ballColor = new Color(0.96f, 0.83f, 0.24f);
        [BillInfoBox("Bill.Audio keys (optional — no crash if the clip isn't in the AudioLibrary).")]
        public string throwSfx = "throw";
        public string impactSfx = "impact";
    }
}
