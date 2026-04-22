using GameMain.GameLogic.Data;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Pure view-controller for character detail panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterInfoPanelController : MonoBehaviour
    {
        [SerializeField] private Text characterNameText;
        [SerializeField] private Text redHealthText;
        [SerializeField] private Text blueArmorText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Text initialWeapon1Text;
        [SerializeField] private Text initialWeapon2Text;
        [SerializeField] private Text statusText;

        public void BindView(
            Text nameLabel,
            Text redHealthLabel,
            Text blueArmorLabel,
            Text energyLabel,
            Text skillNameLabel,
            Text skillDescriptionLabel,
            Text weapon1Label,
            Text weapon2Label,
            Text statusLabel)
        {
            characterNameText = nameLabel;
            redHealthText = redHealthLabel;
            blueArmorText = blueArmorLabel;
            energyText = energyLabel;
            skillNameText = skillNameLabel;
            skillDescriptionText = skillDescriptionLabel;
            initialWeapon1Text = weapon1Label;
            initialWeapon2Text = weapon2Label;
            statusText = statusLabel;
        }

        public void ShowCharacter(CharacterData data)
        {
            if (data == null)
            {
                SetText(characterNameText, "Character: --");
                SetText(redHealthText, "Red Health: --");
                SetText(blueArmorText, "Blue Armor: --");
                SetText(energyText, "Energy: --");
                SetText(skillNameText, "Skill: --");
                SetText(skillDescriptionText, "--");
                SetText(initialWeapon1Text, "Initial Weapon 1: --");
                SetText(initialWeapon2Text, "Initial Weapon 2: --");
                return;
            }

            SetText(characterNameText, "Character: " + SafeString(data.characterName));
            SetText(redHealthText, "Red Health: " + data.redHealth);
            SetText(blueArmorText, "Blue Armor: " + data.blueArmor);
            SetText(energyText, "Energy: " + data.energy);
            SetText(skillNameText, "Skill: " + SafeString(data.skillName));
            SetText(skillDescriptionText, SafeString(data.skillDescription));
            SetText(initialWeapon1Text, "Initial Weapon 1: " + SafeString(data.initialWeapon1));
            SetText(initialWeapon2Text, "Initial Weapon 2: " + SafeString(data.initialWeapon2));
        }

        public void SetStatus(string message)
        {
            SetText(statusText, message);
        }

        private static string SafeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
