using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Minimal role profile data for menu selection and confirmation page.
    /// </summary>
    [CreateAssetMenu(fileName = "RoleSelectionProfileData", menuName = "GameMain/Data/Role Selection Profile")]
    public sealed class RoleSelectionProfileData : ScriptableObject
    {
        [Header("Identity")]
        public string roleId = "Ranger";
        public string displayName = "Ranger";
        public Sprite displaySprite;

        [Header("Core Stats")]
        [Min(1)] public int redHealth = 20;
        [Min(0)] public int blueArmor = 10;
        [Min(0)] public int energy = 100;

        [Header("Skill")]
        public string skillName = "Combat Roll";
        [TextArea(2, 4)] public string skillDescription = "Fast dodge with short invulnerability.";

        [Header("Initial Weapons")]
        public string primaryWeaponName = "Light Blaster";
        [TextArea(1, 3)] public string primaryWeaponDescription = "Balanced rapid fire pistol.";
        public string secondaryWeaponName = "Arc Shotgun";
        [TextArea(1, 3)] public string secondaryWeaponDescription = "Short-range burst with high stagger.";
    }
}
