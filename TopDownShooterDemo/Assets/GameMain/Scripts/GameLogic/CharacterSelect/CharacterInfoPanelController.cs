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
                SetText(characterNameText, "角色：--");
                SetText(redHealthText, "生命值：--");
                SetText(blueArmorText, "护甲：--");
                SetText(energyText, "能量：--");
                SetText(skillNameText, "技能：--");
                SetText(skillDescriptionText, "--");
                SetText(initialWeapon1Text, "初始武器 1：--");
                SetText(initialWeapon2Text, "初始武器 2：--");
                return;
            }

            SetText(characterNameText, "角色：" + SafeString(data.characterName));
            SetText(redHealthText, "生命值：" + data.redHealth);
            SetText(blueArmorText, "护甲：" + data.blueArmor);
            SetText(energyText, "能量：" + data.energy);
            SetText(skillNameText, "技能：" + SafeString(data.skillName));
            SetText(skillDescriptionText, SafeString(data.skillDescription));
            SetText(initialWeapon1Text, "初始武器 1：" + LocalizeWeaponDisplayName(data.initialWeapon1));
            SetText(initialWeapon2Text, "初始武器 2：" + LocalizeWeaponDisplayName(data.initialWeapon2));
        }

        public void SetStatus(string message)
        {
            SetText(statusText, message);
        }

        private static string SafeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value;
        }

        private static string LocalizeWeaponDisplayName(string value)
        {
            switch (SafeString(value))
            {
                case "Pulse Carbine":
                    return "脉冲卡宾枪";
                case "Burst Revolver":
                    return "连发左轮";
                case "Heavy Shotgun":
                    return "重型霰弹枪";
                case "Shock Hammer":
                    return "震击锤";
                case "Needle SMG":
                    return "针刺冲锋枪";
                case "Rail Pistol":
                    return "电磁手枪";
                default:
                    return SafeString(value);
            }
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
