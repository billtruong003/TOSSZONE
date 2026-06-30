using BillInspector;
using UnityEngine;

namespace TossZone.Minigame
{
    /// <summary>
    /// Designer data for ONE minigame. The game is a Gorilla-Tag-style hub with many minigames; the Arena throw
    /// mode (<c>02_Arena</c>) is the first. Make a <see cref="MinigameDef"/> asset per minigame and list them in
    /// the <see cref="MinigameManager"/> catalog (or drop them in a <c>Resources/Minigames</c> folder for
    /// auto-load). See <c>Docs/M4_Gameplay_Design.md</c> (hub → minigames).
    /// </summary>
    [CreateAssetMenu(menuName = "TOSSZONE/Minigame Def", fileName = "MinigameDef")]
    public class MinigameDef : ScriptableObject
    {
        [BillTitle("Identity")]
        [BillRequired] public string id = "arena";
        public string displayName = "Arena";
        [TextArea] public string description;
        public Sprite icon;

        [BillTitle("Scene + players")]
        [BillInfoBox("Scene name (must be in Build Settings) loaded when entering this minigame.")]
        [BillRequired] public string sceneName = "02_Arena";
        public int minPlayers = 1;
        public int maxPlayers = 8;

        [BillTitle("Vũ khí (catalog Arsenal)")]
        [BillInfoBox("Danh sách WeaponConfig dùng trong minigame này. Index 0 = Rock (miễn phí, vô hạn). WristWeaponSelector hiển thị list này.")]
        public TossZone.Combat.WeaponConfig[] weaponCatalog;

        [BillTitle("Vòng lặp trận (round rules)")]
        [BillInfoBox("BO: 1 = first to win 1 round; 3 = best-of-3; 5 = best-of-5.")]
        [BillSlider(1, 5)] public int bestOf = 1;
        [BillSuffix("s")] public float roundDuration = 120f;
    }
}
