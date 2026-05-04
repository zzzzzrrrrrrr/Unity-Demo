using UnityEngine;

namespace ARPGDemo
{
    [CreateAssetMenu(fileName = "SkillDefinition3D", menuName = "ARPG Demo/Skill Definition 3D")]
    public sealed class SkillDefinition3D : ScriptableObject
    {
        [SerializeField] private string skillName = "Skill";
        [SerializeField] private int inputSlot = 1;
        [SerializeField] private float cooldown = 1.5f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float range = 2f;
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float angle = 70f;
        [SerializeField] private float castDuration = 0.35f;
        [SerializeField] private Color vfxColor = Color.cyan;
        [SerializeField] private GameObject vfxPrefab = null;
        [SerializeField] private Sprite icon = null;

        public string SkillName => skillName;
        public int InputSlot => inputSlot;
        public float Cooldown => cooldown;
        public float Damage => damage;
        public float Range => range;
        public float Radius => radius;
        public float Angle => angle;
        public float CastDuration => castDuration;
        public Color VfxColor => vfxColor;
        public GameObject VfxPrefab => vfxPrefab;
        public Sprite Icon => icon;

#if UNITY_EDITOR
        private void OnValidate()
        {
            inputSlot = Mathf.Clamp(inputSlot, 1, 3);
            cooldown = Mathf.Max(0f, cooldown);
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.1f, radius);
            angle = Mathf.Clamp(angle, 1f, 180f);
            castDuration = Mathf.Max(0.05f, castDuration);
        }
#endif
    }
}
