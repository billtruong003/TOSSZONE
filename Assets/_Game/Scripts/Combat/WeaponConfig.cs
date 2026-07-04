using BillInspector;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>How the player obtains/keeps a weapon. Wallet resets $0 each round (per Combat design §13).</summary>
    public enum AcquireMode { BuyOnce, PayPerUse }

    /// <summary>How the weapon gets into the hand.</summary>
    public enum HandSource { AppearInHand, GrabFromHologram }

    /// <summary>How the weapon fires.</summary>
    public enum FireMode { ThrowBallistic, ProjectileLaunch, Hitscan, Melee }

    /// <summary>
    /// Designer data for ONE combat weapon — the configurable seam (add a weapon = author an asset, no code).
    /// Generalizes <see cref="TossZone.Throwing.ThrowConfig"/> across the arsenal
    /// (Rock / Gun / Grenade / Bazooka / BigBoom / LandMine / Sword). See <c>Docs/Combat_Minigame_Design.md</c> §5.
    /// Wire <see cref="heldPrefab"/> to an MS_WP_* prefab; the Rock is weapon #0 (cost 0, infinite).
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Weapon Config", fileName = "WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [BillTitle("Identity")]
        [BillRequired] public string id = "rock";
        public string displayName = "Rock";
        public Sprite icon;

        [BillTitle("Economy + unlock (ví reset $0 mỗi hiệp)")]
        [BillInfoBox("BuyOnce = trả cost 1 lần, giữ + đạn vô hạn tới hết hiệp. PayPerUse = trả cost MỖI LẦN NẠP " +
                     "(1 băng = magazine viên), hết đạn tự mua tiếp khi đủ tiền.")]
        [BillSlider(0, 30)] public int cost = 0;
        public AcquireMode acquireMode = AcquireMode.BuyOnce;
        [BillSuffix("s")] [BillInfoBox("Có sẵn từ giây thứ mấy trong hiệp (escalation). 0 = từ đầu.")]
        public float unlockTime = 0f;
        [BillSuffix("s")] public float cooldown = 0.4f;
        [BillInfoBox("PayPerUse: số viên mỗi băng (mỗi lần trả cost). BuyOnce/Rock: 0 = vô hạn.")]
        public int magazine = 0;

        [BillTitle("Đưa vào tay")]
        [BillInfoBox("AppearInHand = grip là hiện trong tay (ball). GrabFromHologram = grab ra từ hologram (súng).")]
        public HandSource handSource = HandSource.AppearInHand;
        public GameObject heldPrefab;
        [BillInfoBox("Offset cầm tay (cosmetic): pivot mỗi asset MS_WP_* mỗi khác nên model gắn thô vào cổ tay " +
                     "sẽ lệch/ngược — chỉnh 3 giá trị này cho từng vũ khí tới khi nhìn đúng.")]
        public Vector3 holdPositionOffset = Vector3.zero;
        public Vector3 holdRotationOffset = Vector3.zero;
        [BillSlider(0.1f, 3f)] public float holdScale = 1f;

        [BillTitle("Bắn + damage")]
        [BillInfoBox("ThrowBallistic = ném (peak-velocity). ProjectileLaunch = bắn projectile. Hitscan = raycast. Melee = kiếm.")]
        public FireMode fireMode = FireMode.ThrowBallistic;
        [BillInfoBox("ThrowBallistic: tái dùng ThrowConfig (peak-velocity + ballistic). Trống = dùng default.")]
        public TossZone.Throwing.ThrowConfig throwConfig;
        [BillInfoBox("ProjectileLaunch / Hitscan: prefab NetworkProjectile bắn ra.")]
        public GameObject projectilePrefab;
        [BillSuffix("m/s")] public float muzzleSpeed = 12f;
        [BillSuffix("shot/s")] public float fireRate = 2f;
        [BillInfoBox("Số cục máu trừ mỗi hit.")]
        public int damage = 1;
        [BillSuffix("m")] [BillInfoBox("0 = trúng trực tiếp. >0 = nổ AoE bán kính này.")]
        public float aoeRadius = 0f;

        /// <summary>Firing should deduct money each shot (vs free once owned).</summary>
        public bool IsPayPerUse => acquireMode == AcquireMode.PayPerUse;

        [BillTitle("Đạn bay (T20)")]
        [BillInfoBox("Model viên ĐẠN BAY (cosmetic đắp lên projectile, cả local lẫn remote). Trống = dùng " +
                     "heldPrefab (Grenade/BigBoom/Mìn/Đá bay đúng hình cầm tay). Gun/Bazooka gán riêng " +
                     "(MS_WP_Gun_Bullet / MS_WP_Rocket — viên bay khác vật cầm tay).")]
        public GameObject projectileVisual;
        [BillSlider(0.05f, 3f)] public float projectileVisualScale = 1f;

        /// <summary>The prefab whose look the flying projectile copies (explicit override, else the held model).</summary>
        public GameObject ProjectileVisualPrefab => projectileVisual != null ? projectileVisual : heldPrefab;

        [BillTitle("Bom Chữ X (GDD §V)")]
        [BillInfoBox("crossFireZones = khi nổ tạo 2 vệt lửa hộp vuông góc (chữ X). Ai đi qua mất 1 mạng/lần " +
                     "(reuse BuffZone Fire). Kích thước GDD: rộng 1.1m × dài 47% sâu sân.")]
        public bool crossFireZones = false;
        [BillSuffix("m")] public float crossZoneWidth = 1.1f;
        [BillSuffix("m")] public float crossZoneLength = 5.64f;
        [BillSuffix("s")] public float crossZoneSeconds = 3f;

        [BillTitle("Hành vi đặc biệt")]
        [BillInfoBox("fuseDelay > 0 = LandMine: arm + delay trước khi nổ AoE.")]
        [BillSlider(0f, 10f)] public float fuseDelay = 0f;
        [BillSuffix("m/s²")]
        [BillInfoBox("projectileGravity: arc cong xuống (Bazooka ~9.8). 0 = thẳng.")]
        public float projectileGravity = 0f;
        [BillInfoBox("laserSight = hiện dot/line từ nòng (Gun).")]
        public bool laserSight = false;
        [BillInfoBox("attacksPlayers = false → không damage người (Kiếm deflect-only).")]
        public bool attacksPlayers = true;
        [BillInfoBox("canDeflect = dùng để chém deflect đạn bay (Kiếm).")]
        public bool canDeflect = false;
        [BillInfoBox("isUncatchable = đạn KHÔNG bắt được (đạn súng, Power throw tím).")]
        public bool isUncatchable = false;
    }
}
