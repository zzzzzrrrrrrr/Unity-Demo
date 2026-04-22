using GameMain.Builtin.Sound;
using UnityEngine;

namespace GameMain.GameLogic.Tools
{
    /// <summary>
    /// Optional runtime asset hook points so placeholder visuals/audio can be replaced without changing logic scripts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimePresentationBindings : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] private Sprite playerSprite;
        [SerializeField] private Sprite bossSprite;

        [Header("Projectile & Arena")]
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private Sprite combatZoneSprite;
        [SerializeField] private Sprite centerMatSprite;
        [SerializeField] private Sprite borderSprite;
        [SerializeField] private Sprite obstacleSprite;

        [Header("HUD")]
        [SerializeField] private Sprite hudPanelSprite;
        [SerializeField] private Sprite hudBarBackgroundSprite;
        [SerializeField] private Sprite hudBarFillSprite;
        [SerializeField] private Sprite dodgeSkillIconSprite;
        [SerializeField] private Sprite buttonSprite;

        [Header("Audio")]
        [SerializeField] private AudioClipBindings audioClipBindingsOverride;

        public Sprite PlayerSprite => playerSprite;

        public Sprite BossSprite => bossSprite;

        public Sprite ProjectileSprite => projectileSprite;

        public Sprite FloorSprite => floorSprite;

        public Sprite CombatZoneSprite => combatZoneSprite;

        public Sprite CenterMatSprite => centerMatSprite;

        public Sprite BorderSprite => borderSprite;

        public Sprite ObstacleSprite => obstacleSprite;

        public Sprite HudPanelSprite => hudPanelSprite;

        public Sprite HudBarBackgroundSprite => hudBarBackgroundSprite;

        public Sprite HudBarFillSprite => hudBarFillSprite;

        public Sprite DodgeSkillIconSprite => dodgeSkillIconSprite;

        public Sprite ButtonSprite => buttonSprite;

        public AudioClipBindings AudioClipBindingsOverride => audioClipBindingsOverride;
    }
}
