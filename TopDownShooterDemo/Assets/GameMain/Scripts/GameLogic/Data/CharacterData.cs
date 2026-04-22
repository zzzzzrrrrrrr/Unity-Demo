using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Data-driven role configuration used by character select scene.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "GameMain/Data/Character Data")]
    public sealed class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public string characterId = "Ranger";
        public string characterName = "Ranger";
        public Sprite portrait;
        public Color worldTint = new Color(0.38f, 0.78f, 1f, 1f);

        [Header("Core Stats")]
        [Min(1)] public int redHealth = 20;
        [Min(0)] public int blueArmor = 8;
        [Min(0)] public int energy = 100;

        [Header("Skill")]
        public string skillName = "Combat Roll";
        [TextArea(2, 4)] public string skillDescription = "Fast dodge with short invulnerability.";
        [Header("Skill Runtime Params")]
        public KeyCode dodgeKey = KeyCode.Space;
        [Min(0.1f)] public float dodgeDistance = 2.8f;
        [Min(0.05f)] public float dodgeDuration = 0.18f;
        [Min(0.1f)] public float dodgeCooldown = 1.6f;
        public bool dodgeInvulnerable = true;
        [Range(0f, 0.95f)] public float dodgeDamageReduction = 0.75f;

        [Header("Initial Weapons")]
        public string initialWeapon1 = "Light Blaster";
        public string initialWeapon2 = "Arc Shotgun";
        [Header("Weapon 1 Runtime Params")]
        [Min(0.01f)] public float weapon1FireInterval = 0.16f;
        [Min(0.1f)] public float weapon1ProjectileSpeed = 20f;
        [Min(0f)] public float weapon1ProjectileDamage = 12f;
        [Min(0.1f)] public float weapon1ProjectileLifetime = 3.2f;
        [Header("Weapon 2 Runtime Params")]
        [Min(0.01f)] public float weapon2FireInterval = 0.34f;
        [Min(0.1f)] public float weapon2ProjectileSpeed = 16f;
        [Min(0f)] public float weapon2ProjectileDamage = 22f;
        [Min(0.1f)] public float weapon2ProjectileLifetime = 3.2f;
    }
}
