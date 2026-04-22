using GameMain.GameLogic.Data;
using UnityEngine;

namespace GameMain.GameLogic.Run
{
    /// <summary>
    /// Runtime cache of selected character data for the current run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunCharacterRuntimeState : MonoBehaviour
    {
        [SerializeField] private CharacterData sourceCharacterData;
        [SerializeField] private string characterId;
        [SerializeField] private string characterName;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Sprite selectedActorSprite;
        [SerializeField] private Color worldTint = Color.white;
        [SerializeField] private int redHealth;
        [SerializeField] private int blueArmor;
        [SerializeField] private int energy;
        [SerializeField] private string skillName;
        [SerializeField] private string skillDescription;
        [SerializeField] private string initialWeapon1;
        [SerializeField] private string initialWeapon2;
        [SerializeField] private float weapon1FireInterval;
        [SerializeField] private float weapon1ProjectileSpeed;
        [SerializeField] private float weapon1ProjectileDamage;
        [SerializeField] private float weapon1ProjectileLifetime;
        [SerializeField] private float weapon2FireInterval;
        [SerializeField] private float weapon2ProjectileSpeed;
        [SerializeField] private float weapon2ProjectileDamage;
        [SerializeField] private float weapon2ProjectileLifetime;

        public CharacterData SourceCharacterData => sourceCharacterData;
        public string CharacterId => characterId;
        public string CharacterName => characterName;
        public Sprite Portrait => portrait;
        public Sprite SelectedActorSprite => selectedActorSprite;
        public Color WorldTint => worldTint;
        public int RedHealth => redHealth;
        public int BlueArmor => blueArmor;
        public int Energy => energy;
        public string SkillName => skillName;
        public string SkillDescription => skillDescription;
        public string InitialWeapon1 => initialWeapon1;
        public string InitialWeapon2 => initialWeapon2;
        public float Weapon1FireInterval => weapon1FireInterval;
        public float Weapon1ProjectileSpeed => weapon1ProjectileSpeed;
        public float Weapon1ProjectileDamage => weapon1ProjectileDamage;
        public float Weapon1ProjectileLifetime => weapon1ProjectileLifetime;
        public float Weapon2FireInterval => weapon2FireInterval;
        public float Weapon2ProjectileSpeed => weapon2ProjectileSpeed;
        public float Weapon2ProjectileDamage => weapon2ProjectileDamage;
        public float Weapon2ProjectileLifetime => weapon2ProjectileLifetime;

        public void Apply(CharacterData characterData)
        {
            Apply(characterData, null);
        }

        public void Apply(CharacterData characterData, Sprite actorSprite)
        {
            sourceCharacterData = characterData;
            selectedActorSprite = actorSprite;
            if (characterData == null)
            {
                characterId = string.Empty;
                characterName = string.Empty;
                portrait = null;
                worldTint = Color.white;
                redHealth = 0;
                blueArmor = 0;
                energy = 0;
                skillName = string.Empty;
                skillDescription = string.Empty;
                initialWeapon1 = string.Empty;
                initialWeapon2 = string.Empty;
                weapon1FireInterval = 0f;
                weapon1ProjectileSpeed = 0f;
                weapon1ProjectileDamage = 0f;
                weapon1ProjectileLifetime = 0f;
                weapon2FireInterval = 0f;
                weapon2ProjectileSpeed = 0f;
                weapon2ProjectileDamage = 0f;
                weapon2ProjectileLifetime = 0f;
                return;
            }

            characterId = characterData.characterId;
            characterName = characterData.characterName;
            portrait = characterData.portrait;
            worldTint = characterData.worldTint;
            redHealth = characterData.redHealth;
            blueArmor = characterData.blueArmor;
            energy = characterData.energy;
            skillName = characterData.skillName;
            skillDescription = characterData.skillDescription;
            initialWeapon1 = characterData.initialWeapon1;
            initialWeapon2 = characterData.initialWeapon2;
            weapon1FireInterval = characterData.weapon1FireInterval;
            weapon1ProjectileSpeed = characterData.weapon1ProjectileSpeed;
            weapon1ProjectileDamage = characterData.weapon1ProjectileDamage;
            weapon1ProjectileLifetime = characterData.weapon1ProjectileLifetime;
            weapon2FireInterval = characterData.weapon2FireInterval;
            weapon2ProjectileSpeed = characterData.weapon2ProjectileSpeed;
            weapon2ProjectileDamage = characterData.weapon2ProjectileDamage;
            weapon2ProjectileLifetime = characterData.weapon2ProjectileLifetime;
        }
    }
}
