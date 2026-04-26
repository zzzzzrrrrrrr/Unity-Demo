using GameMain.GameLogic.CharacterSelect;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Meta;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Run;
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Equipment and backpack panel. PlayerController remains the only runtime weapon truth owner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentPanel : BasePanel
    {
        [SerializeField] private CharacterSelectionController selectionController;
        [SerializeField] private Text roleNameText;
        [SerializeField] private Text coinText;
        [SerializeField] private Text weaponOneText;
        [SerializeField] private Text weaponTwoText;
        [SerializeField] private Image roleImage;
        [SerializeField] private Image weaponOneImage;
        [SerializeField] private Image weaponTwoImage;
        [SerializeField] private Button weaponOneButton;
        [SerializeField] private Button weaponTwoButton;
        [SerializeField] private Button closeButton;

        private CharacterData currentCharacter;
        private PlayerController runtimePlayer;
        private readonly Button[] backpackSlotButtons = new Button[16];
        private readonly Image[] backpackSlotBackgrounds = new Image[16];
        private readonly Image[] backpackSlotIcons = new Image[16];
        private readonly Text[] backpackSlotTexts = new Text[16];
        private float nextRuntimeRefreshTime;

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnEnable()
        {
            BindEvents();
            ResolveSelectionController();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnDestroy()
        {
            if (selectionController != null)
            {
                selectionController.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void Update()
        {
            if (runtimePlayer == null || !IsVisible || Time.unscaledTime < nextRuntimeRefreshTime)
            {
                return;
            }

            nextRuntimeRefreshTime = Time.unscaledTime + 0.15f;
            Refresh();
        }

        public static EquipmentPanel Create(Transform parent)
        {
            var panel = MetaUiFactory.CreatePanel(parent, "EquipmentPanel", Vector2.zero, new Vector2(820f, 620f), new Color(0.04f, 0.065f, 0.095f, 0.97f));
            panel.transform.SetAsLastSibling();
            var canvasGroup = MetaUiFactory.GetOrAddComponent<CanvasGroup>(panel);
            var controller = MetaUiFactory.GetOrAddComponent<EquipmentPanel>(panel);
            controller.BindPanelRoot(panel, canvasGroup);
            controller.BuildView(panel.transform);
            controller.ResolveSelectionController();
            controller.Refresh();
            controller.Hide();
            return controller;
        }

        public void BindSelectionController(CharacterSelectionController controller)
        {
            if (selectionController == controller)
            {
                return;
            }

            if (selectionController != null)
            {
                selectionController.SelectionChanged -= OnSelectionChanged;
            }

            selectionController = controller;
            if (selectionController != null)
            {
                selectionController.SelectionChanged += OnSelectionChanged;
            }

            Refresh();
        }

        public void BindRuntimePlayer(PlayerController player)
        {
            runtimePlayer = player;
            if (RunSessionContext.HasSelectedCharacter)
            {
                currentCharacter = RunSessionContext.SelectedCharacterData;
            }

            Refresh();
        }

        public override void Show()
        {
            transform.SetAsLastSibling();
            base.Show();
            Refresh();
        }

        private void BuildView(Transform root)
        {
            MetaUiFactory.CreateText(root, "TitleText", "装备 / 背包", new Vector2(0f, 260f), new Vector2(720f, 42f), 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            roleImage = MetaUiFactory.CreateImage(root, "RoleImage", null, new Vector2(-290f, 160f), new Vector2(96f, 120f), new Color(0.54f, 0.82f, 1f, 1f));
            roleNameText = MetaUiFactory.CreateText(root, "RoleNameText", "当前角色：-", new Vector2(-94f, 188f), new Vector2(330f, 34f), 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            coinText = MetaUiFactory.CreateText(root, "CoinText", "蓝币 0", new Vector2(-94f, 150f), new Vector2(330f, 30f), 20, TextAnchor.MiddleLeft, FontStyle.Normal);

            MetaUiFactory.CreateText(root, "EquipTitle", "装备槽来自 PlayerController，点击槽位切换当前武器。", new Vector2(0f, 94f), new Vector2(700f, 30f), 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            weaponOneButton = CreateWeaponSlot(root, "WeaponOneSlot", new Vector2(-190f, 28f), out weaponOneImage, out weaponOneText);
            weaponTwoButton = CreateWeaponSlot(root, "WeaponTwoSlot", new Vector2(190f, 28f), out weaponTwoImage, out weaponTwoText);

            MetaUiFactory.CreateText(root, "BackpackTitle", "背包武器：点击装备到当前槽位", new Vector2(0f, -84f), new Vector2(700f, 30f), 20, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateBackpackGrid(root);
            closeButton = MetaUiFactory.CreateButton(root, "CloseButton", "关闭", new Vector2(300f, -254f), new Vector2(160f, 42f));
            BindEvents();
        }

        private static Button CreateWeaponSlot(Transform root, string name, Vector2 position, out Image icon, out Text label)
        {
            var slot = MetaUiFactory.CreatePanel(root, name, position, new Vector2(310f, 92f), new Color(0.07f, 0.105f, 0.14f, 1f));
            icon = MetaUiFactory.CreateImage(slot.transform, "Icon", null, new Vector2(-105f, 0f), new Vector2(58f, 58f), Color.white);
            label = MetaUiFactory.CreateText(slot.transform, "Label", "--", new Vector2(46f, 0f), new Vector2(190f, 58f), 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            return MetaUiFactory.GetOrAddComponent<Button>(slot);
        }

        private void CreateBackpackGrid(Transform root)
        {
            const int columns = 4;
            const int rows = 4;
            var start = new Vector2(-144f, -138f);
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var index = y * columns + x;
                    var position = start + new Vector2(x * 96f, -y * 48f);
                    var slot = MetaUiFactory.CreatePanel(root, "BackpackSlot_" + index, position, new Vector2(76f, 38f), new Color(0.08f, 0.12f, 0.16f, 0.92f));
                    backpackSlotBackgrounds[index] = slot.GetComponent<Image>();
                    backpackSlotButtons[index] = MetaUiFactory.GetOrAddComponent<Button>(slot);
                    backpackSlotIcons[index] = MetaUiFactory.CreateImage(slot.transform, "Icon", null, new Vector2(-18f, 0f), new Vector2(24f, 24f), Color.white);
                    backpackSlotTexts[index] = MetaUiFactory.CreateText(slot.transform, "Label", "空", new Vector2(14f, 0f), new Vector2(44f, 28f), 14, TextAnchor.MiddleLeft, FontStyle.Normal);
                }
            }
        }

        private void ResolveSelectionController()
        {
            if (selectionController != null)
            {
                return;
            }

            BindSelectionController(FindObjectOfType<CharacterSelectionController>());
        }

        private void OnSelectionChanged(CharacterData selected)
        {
            currentCharacter = selected;
            if (selected != null)
            {
                PlayerProfileService.SetSelectedRoleId(selected.characterId);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (runtimePlayer != null)
            {
                RefreshRuntimePlayerView();
                return;
            }

            if (selectionController != null && selectionController.SelectedCharacterData != null)
            {
                currentCharacter = selectionController.SelectedCharacterData;
            }

            var characterName = currentCharacter != null ? currentCharacter.characterName : "--";
            SetText(roleNameText, "当前角色：" + characterName);
            SetText(coinText, "蓝币 " + PlayerProfileService.GetCoins());

            var portrait = currentCharacter != null ? currentCharacter.portrait : null;
            if (roleImage != null)
            {
                roleImage.sprite = portrait;
                roleImage.color = portrait != null ? Color.white : (currentCharacter != null ? currentCharacter.worldTint : new Color(0.54f, 0.82f, 1f, 1f));
            }

            var weapon1 = currentCharacter != null ? currentCharacter.initialWeapon1 : "--";
            var weapon2 = currentCharacter != null ? currentCharacter.initialWeapon2 : "--";
            SetText(weaponOneText, weapon1);
            SetText(weaponTwoText, weapon2);
            SetWeaponIcon(weaponOneImage, weapon1);
            SetWeaponIcon(weaponTwoImage, weapon2);
        }

        private void RefreshRuntimePlayerView()
        {
            var runtimeState = runtimePlayer != null ? runtimePlayer.GetComponent<RunCharacterRuntimeState>() : null;
            var selected = RunSessionContext.SelectedCharacterData;
            if (selected != null)
            {
                currentCharacter = selected;
            }

            var characterName = runtimeState != null && !string.IsNullOrWhiteSpace(runtimeState.CharacterName)
                ? runtimeState.CharacterName
                : (currentCharacter != null ? currentCharacter.characterName : "--");
            SetText(roleNameText, "当前角色：" + characterName);
            SetText(coinText, "蓝币 " + PlayerProfileService.GetCoins());

            var portrait = runtimeState != null && runtimeState.Portrait != null
                ? runtimeState.Portrait
                : (currentCharacter != null ? currentCharacter.portrait : null);
            if (roleImage != null)
            {
                roleImage.sprite = portrait;
                roleImage.color = portrait != null ? Color.white : (currentCharacter != null ? currentCharacter.worldTint : new Color(0.54f, 0.82f, 1f, 1f));
            }

            var equippedSlots = runtimePlayer.GetEquippedWeaponSlotRuntimeSnapshots();
            var inventorySlots = runtimePlayer.GetWeaponInventorySnapshots();
            ApplyRuntimeWeaponSlot(weaponOneButton, weaponOneImage, weaponOneText, equippedSlots.Length > 0 ? equippedSlots[0] : default);
            ApplyRuntimeWeaponSlot(weaponTwoButton, weaponTwoImage, weaponTwoText, equippedSlots.Length > 1 ? equippedSlots[1] : default);
            RefreshBackpackSlots(inventorySlots);
        }

        private void ApplyRuntimeWeaponSlot(Button button, Image icon, Text label, PlayerController.WeaponSlotRuntimeSnapshot snapshot)
        {
            var displayName = snapshot.HasWeapon ? snapshot.Label : "--";
            var activeText = snapshot.IsActive ? " [当前]" : string.Empty;
            SetText(
                label,
                "槽位 " + (snapshot.SlotIndex + 1) + activeText + "\n" +
                displayName + "\n" +
                "伤害 " + snapshot.ProjectileDamage.ToString("0.#") + " / 间隔 " + snapshot.FireInterval.ToString("0.##"));
            SetWeaponIcon(icon, displayName, snapshot.VisualSprite);
            var background = button != null ? button.GetComponent<Image>() : null;
            if (background != null)
            {
                background.color = snapshot.IsActive
                    ? new Color(0.16f, 0.36f, 0.5f, 1f)
                    : new Color(0.07f, 0.105f, 0.14f, 1f);
            }
        }

        private void RefreshBackpackSlots(PlayerController.WeaponSlotRuntimeSnapshot[] slots)
        {
            for (var i = 0; i < backpackSlotTexts.Length; i++)
            {
                var hasSlot = slots != null && i < slots.Length && slots[i].HasWeapon;
                if (hasSlot)
                {
                    var snapshot = slots[i];
                    SetText(backpackSlotTexts[i], snapshot.Label);
                    SetWeaponIcon(backpackSlotIcons[i], snapshot.Label, snapshot.VisualSprite);
                    SetBackpackSlotColor(i, true);
                }
                else
                {
                    SetText(backpackSlotTexts[i], "空");
                    if (backpackSlotIcons[i] != null)
                    {
                        backpackSlotIcons[i].sprite = null;
                        backpackSlotIcons[i].color = new Color(0.32f, 0.48f, 0.58f, 0.35f);
                    }

                    SetBackpackSlotColor(i, false);
                }
            }
        }

        private void SetBackpackSlotColor(int index, bool active)
        {
            if (index < 0 || index >= backpackSlotBackgrounds.Length || backpackSlotBackgrounds[index] == null)
            {
                return;
            }

            backpackSlotBackgrounds[index].color = active
                ? new Color(0.18f, 0.42f, 0.56f, 0.98f)
                : new Color(0.08f, 0.12f, 0.16f, 0.92f);
        }

        private static void SetWeaponIcon(Image image, string weaponName, Sprite preferredSprite = null)
        {
            if (image == null)
            {
                return;
            }

            var sprite = preferredSprite != null ? preferredSprite : LoadWeaponSprite(weaponName);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0.32f, 0.68f, 0.86f, 1f);
        }

        private static Sprite LoadWeaponSprite(string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/BadPistol");
            }

            var lower = weaponName.ToLowerInvariant();
            if (lower.Contains("laser") || lower.Contains("lance"))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/IceBreaker");
            }

            if (lower.Contains("pulse") || lower.Contains("carbine") || lower.Contains("smg"))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/NextNextNextGenSMG");
            }

            if (lower.Contains("gatling"))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/BlueFireGatling");
            }

            if (lower.Contains("arc") || lower.Contains("shotgun"))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/PKP");
            }

            if (lower.Contains("light") || lower.Contains("blaster"))
            {
                return MetaUiFactory.LoadSprite("Images/Weapon/UZI");
            }

            var compact = weaponName.Replace(" ", string.Empty).Replace("/", string.Empty).Replace("-", string.Empty);
            return MetaUiFactory.LoadSprite("Images/Weapon/" + compact) ?? MetaUiFactory.LoadSprite("Images/Weapon/BadPistol");
        }

        private void BindEvents()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }

            BindEquippedSlotButton(weaponOneButton, 0);
            BindEquippedSlotButton(weaponTwoButton, 1);
            for (var i = 0; i < backpackSlotButtons.Length; i++)
            {
                BindInventorySlotButton(backpackSlotButtons[i], i);
            }
        }

        private void UnbindEvents()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }

            UnbindSlotButton(weaponOneButton);
            UnbindSlotButton(weaponTwoButton);
            for (var i = 0; i < backpackSlotButtons.Length; i++)
            {
                UnbindSlotButton(backpackSlotButtons[i]);
            }
        }

        private void BindEquippedSlotButton(Button button, int slotIndex)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = button.GetComponent<Graphic>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => TrySelectRuntimeWeaponSlot(slotIndex));
        }

        private void BindInventorySlotButton(Button button, int inventoryIndex)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = button.GetComponent<Graphic>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => TryEquipInventoryWeapon(inventoryIndex));
        }

        private static void UnbindSlotButton(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void TryEquipInventoryWeapon(int inventoryIndex)
        {
            if (runtimePlayer == null || inventoryIndex < 0)
            {
                return;
            }

            if (runtimePlayer.TryEquipInventoryWeapon(inventoryIndex))
            {
                Refresh();
            }
        }

        private void TrySelectRuntimeWeaponSlot(int slotIndex)
        {
            if (runtimePlayer == null || slotIndex < 0 || slotIndex > 1)
            {
                return;
            }

            if (runtimePlayer.TrySelectWeaponSlot(slotIndex))
            {
                Refresh();
            }
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}

