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
    }
}
