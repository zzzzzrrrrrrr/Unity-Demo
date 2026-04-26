using System;
using System.Collections.Generic;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.UI;
using GameMain.GameLogic.Visual;
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Builds and maintains a lightweight authoring-friendly character select room.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectSceneBootstrap : MonoBehaviour
    {
        private const int MaxCharacterSlots = 3;
        private const string BlueBoundarySpriteAssetPath = "Assets/Sprite/Room/Floor/SnowMountain/Wall.png";
        private const float PortalFlipbookFps = 10f;
        private static Sprite whiteSprite;
        private static Font uiFont;

        [SerializeField] private string sceneName = "CharacterSelectScene";
        [SerializeField] private string resourcesPath = "CharacterSelect";
        [SerializeField] private string runSceneName = "RunScene";
        [SerializeField] private Camera worldCamera;

        [Header("Scene Refs")]
        [SerializeField] private Transform rootContainer;
        [SerializeField] private CharacterSelectTarget[] authoredTargets;
        [SerializeField] private CharacterSelectPortalController portalController;
        [Header("Legacy Compatibility")]
        [SerializeField] private CharacterSelectAvatarController avatarController;
        [SerializeField] private Transform avatarSpawnPoint;

        [Header("Runtime Logic Refs")]
        [SerializeField] private CharacterSelectionController selectionController;
        [SerializeField] private CharacterInfoPanelController infoPanelController;
        [SerializeField] private CharacterSelectConfirmController confirmController;

        [Header("UI Refs")]
        [SerializeField] private Text characterNameText;
        [SerializeField] private Text redHealthText;
        [SerializeField] private Text blueArmorText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Text initialWeapon1Text;
        [SerializeField] private Text initialWeapon2Text;
        [SerializeField] private Text statusText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;

        private bool runtimeInitialized;

        private void OnEnable()
        {
            if (!IsTargetScene())
            {
                return;
            }

            if (!Application.isPlaying)
            {
                EnsureMainCamera();
                EnsureEventSystem();
                EnsureLayout();
                runtimeInitialized = false;
                return;
            }

            EnsureMainCamera();
            EnsureEventSystem();
            EnsureLayout();
            InitializeRuntime();
        }

        private void OnDisable()
        {
            runtimeInitialized = false;
        }

        public void EnsureEditorLayout()
        {
            if (!IsTargetScene())
            {
                return;
            }

            EnsureMainCamera();
            EnsureEventSystem();
            EnsureLayout();
        }

        private bool IsTargetScene()
        {
            var ownerScene = gameObject.scene;
            if (ownerScene.IsValid())
            {
                return string.Equals(ownerScene.name, sceneName, System.StringComparison.Ordinal);
            }

            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                   string.Equals(activeScene.name, sceneName, System.StringComparison.Ordinal);
        }

        private void EnsureLayout()
        {
            var rootObject = ResolveOrCreateRootObject();
            rootContainer = rootObject.transform;

            EnsureRoom(rootContainer);
            EnsureCharacterShowcase(rootContainer);
            EnsurePortal(rootContainer);
            DisableLegacySelectionAvatar(rootContainer);
            EnsureUi(rootContainer);

            selectionController = GetOrAddComponent<CharacterSelectionController>(rootObject);
            selectionController.SetWorldCamera(worldCamera);

            confirmController = GetOrAddComponent<CharacterSelectConfirmController>(rootObject);
        }

        private GameObject ResolveOrCreateRootObject()
        {
            if (rootContainer != null)
            {
                return rootContainer.gameObject;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                var rootObjects = activeScene.GetRootGameObjects();
                for (var i = 0; i < rootObjects.Length; i++)
                {
                    var candidate = rootObjects[i];
                    if (candidate != null &&
                        string.Equals(candidate.name, "CharacterSelectRoot", System.StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            return FindOrCreateChild(transform, "CharacterSelectRoot");
        }

        private void InitializeRuntime()
        {
            if (runtimeInitialized)
            {
                return;
            }

            RemoveNonCharacterSelectElements();
            EnsureLayout();

            if (selectionController == null || infoPanelController == null || confirmController == null)
            {
                Debug.LogWarning("CharacterSelect runtime init aborted: missing core controller references.", this);
                return;
            }

            portalController?.Configure(runSceneName, KeyCode.E);
            portalController?.SetPortalEnabled(false);

            var characters = LoadCharacters();
            selectionController.SetSelectionLocked(false);
            selectionController.ClearTargets();
            for (var i = 0; i < authoredTargets.Length; i++)
            {
                var target = authoredTargets[i];
                if (target == null)
                {
                    continue;
                }

                var bodyRenderer = target.GetComponent<SpriteRenderer>();
                var ringRenderer = FindChildRenderer(target.transform, "SelectionRing");
                var data = i < characters.Count ? characters[i] : null;
                target.Setup(data, bodyRenderer, ringRenderer);
                selectionController.RegisterTarget(target);
            }

            infoPanelController.BindView(
                characterNameText,
                redHealthText,
                blueArmorText,
                energyText,
                skillNameText,
                skillDescriptionText,
                initialWeapon1Text,
                initialWeapon2Text,
                statusText);

            confirmController.Bind(
                selectionController,
                infoPanelController,
                confirmButton,
                confirmButtonLabel,
                null,
                portalController);

            selectionController.SelectionChanged -= infoPanelController.ShowCharacter;
            selectionController.SelectionChanged += infoPanelController.ShowCharacter;
            RunSessionContext.Clear();
            infoPanelController.ShowCharacter(null);
            infoPanelController.SetDetailsVisible(false);
            infoPanelController.SetStatus(characters.Count > 0 ? "请选择一个角色。" : "未找到角色数据。请在 Resources/CharacterSelect 下添加 CharacterData。");

            runtimeInitialized = true;
        }

        private static SpriteRenderer FindChildRenderer(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            var child = parent.Find(childName);
            return child != null ? child.GetComponent<SpriteRenderer>() : null;
        }

        private static void RemoveNonCharacterSelectElements()
        {
            DestroyAllOfType<MenuPresetPanelController>();
            DestroyAllOfType<BattleHudController>();
            DestroyAllOfType<BattlePausePanelController>();
            DestroyAllOfType<ResultPanelController>();
            DestroyAllOfType<RoleSelectionPanelController>();
            DestroyAllOfType<DebugPanelController>();

            DestroyObjectsByName("MenuPresetPanel");
            DestroyObjectsByName("BattleHud");
            DestroyObjectsByName("BattlePausePanel");
            DestroyObjectsByName("ResultPanel");
            DestroyObjectsByName("RoleSelectionPanel");
            DestroyObjectsByName("DebugPanel");
        }

        private void EnsureRoom(Transform parent)
        {
            var room = FindOrCreateChild(parent, "Room");

            ConfigureWorldSprite(
                FindOrCreateChild(room.transform, "Floor"),
                new Vector3(43f, 27f, 1f),
                new Color(0.085f, 0.12f, 0.17f, 1f),
                -34,
                Vector3.zero,
                false,
                "Assets/Sprite/Room/Floor/floor1.png");
            ConfigureWorldVisual(FindOrCreateChild(room.transform, "InnerMat"), new Vector3(34f, 19.5f, 1f), new Color(0.145f, 0.19f, 0.255f, 0.94f), -33, new Vector3(0f, -0.2f, 0f), false);
            EnsureFloorTiles(room.transform);

            ConfigureWall(FindOrCreateChild(room.transform, "WallTopLeft"), new Vector3(19.5f, 0.9f, 1f), new Vector3(-11.4f, 12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallTopRight"), new Vector3(19.5f, 0.9f, 1f), new Vector3(11.4f, 12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallBottom"), new Vector3(42.4f, 0.9f, 1f), new Vector3(0f, -12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallLeft"), new Vector3(0.9f, 25.5f, 1f), new Vector3(-21.2f, 0f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallRight"), new Vector3(0.9f, 25.5f, 1f), new Vector3(21.2f, 0f, 0f));

            var roomTitle = FindOrCreateChild(room.transform, "RoomTitlePlate");
            ConfigureWorldVisual(roomTitle, new Vector3(12f, 0.5f, 1f), new Color(0.23f, 0.31f, 0.42f, 0.85f), -27, new Vector3(0f, 11f, 0f), false);
            DisableWorldText(roomTitle.transform, "RoomTitleText");
            EnsureRoomDecor(room.transform);
        }

        private static void ConfigureWall(GameObject wallObject, Vector3 scale, Vector3 localPosition)
        {
            ConfigureWorldSprite(wallObject, scale, new Color(0.54f, 0.78f, 1f, 0.96f), -28, localPosition, true, BlueBoundarySpriteAssetPath);
        }

        private static void EnsureFloorTiles(Transform room)
        {
            var tileRoot = FindOrCreateChild(room, "FloorTileGrid");
            for (var y = -2; y <= 2; y++)
            {
                for (var x = -4; x <= 4; x++)
                {
                    var index = (y + 2) * 9 + (x + 4);
                    var tile = FindOrCreateChild(tileRoot.transform, "Tile_" + index);
                    var color = (x + y) % 2 == 0
                        ? new Color(0.17f, 0.23f, 0.31f, 0.76f)
                        : new Color(0.13f, 0.18f, 0.25f, 0.76f);
                    ConfigureWorldSprite(
                        tile,
                        new Vector3(3.52f, 2.52f, 1f),
                        color,
                        -32,
                        new Vector3(x * 3.6f, y * 2.6f - 0.35f, 0f),
                        false,
                        "Assets/Sprite/Room/Floor/tiles.png");
                }
            }
        }

        private static void EnsureRoomDecor(Transform room)
        {
            var decorRoot = FindOrCreateChild(room, "LobbyDecor");
            CreateDecor(decorRoot.transform, "LeftDesk", "Assets/Sprite/Room/Item/Desk.png", new Vector3(-15.8f, 7.9f, 0f), new Vector3(1.7f, 1.7f, 1f), new Color(0.68f, 0.78f, 0.9f, 1f));
            CreateDecor(decorRoot.transform, "RightDesk", "Assets/Sprite/Room/Item/DeskItem.png", new Vector3(15.8f, 7.6f, 0f), new Vector3(1.5f, 1.5f, 1f), new Color(0.72f, 0.84f, 0.98f, 1f));
            CreateDecor(decorRoot.transform, "LeftSupplyBox", "Assets/Sprite/Room/Item/TreasureBox/objects_common_0.png", new Vector3(-16.2f, -7.9f, 0f), new Vector3(1.4f, 1.4f, 1f), new Color(0.66f, 0.78f, 0.9f, 1f));
            CreateDecor(decorRoot.transform, "RightSupplyBox", "Assets/Sprite/Room/Item/TreasureBox/objects_common_3.png", new Vector3(16.4f, -7.5f, 0f), new Vector3(1.35f, 1.35f, 1f), new Color(0.72f, 0.82f, 0.94f, 1f));
            HideDecor(decorRoot.transform, "BlueCoinDisplay");
            CreateDecor(decorRoot.transform, "TerminalGear", "Assets/Sprite/Room/Item/common_room_item_20.png", new Vector3(11.7f, 9.2f, 0f), new Vector3(1.2f, 1.2f, 1f), new Color(0.68f, 0.86f, 1f, 1f));
            CreateDecor(decorRoot.transform, "TrainingPadLeft", null, new Vector3(-9.4f, -5.65f, 0f), new Vector3(3.5f, 0.24f, 1f), new Color(0.46f, 0.62f, 0.82f, 0.86f));
            CreateDecor(decorRoot.transform, "TrainingPadRight", null, new Vector3(9.4f, -5.65f, 0f), new Vector3(3.5f, 0.24f, 1f), new Color(0.46f, 0.62f, 0.82f, 0.86f));
        }

        private static void CreateDecor(Transform parent, string name, string spritePath, Vector3 localPosition, Vector3 scale, Color color)
        {
            ConfigureWorldSprite(FindOrCreateChild(parent, name), scale, color, -14, localPosition, false, spritePath);
        }

        private static void HideDecor(Transform parent, string name)
        {
            var child = parent != null ? parent.Find(name) : null;
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void EnsureCharacterShowcase(Transform parent)
        {
            var showcaseRoot = FindOrCreateChild(parent, "CharacterShowcase");
            showcaseRoot.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            showcaseRoot.transform.localRotation = Quaternion.identity;
            showcaseRoot.transform.localScale = Vector3.one;

            var stage = FindOrCreateChild(showcaseRoot.transform, "Stage");
            ConfigureInvisibleAnchor(stage, new Vector3(0f, -2.05f, 0f));
            DisableWorldText(stage.transform, "StageLabel");

            var slotRoot = FindOrCreateChild(showcaseRoot.transform, "Slots");
            var positions = new[]
            {
                new Vector3(-6.4f, -1.35f, 0f),
                new Vector3(0f, -0.72f, 0f),
                new Vector3(6.4f, -1.35f, 0f)
            };
            var targets = new CharacterSelectTarget[MaxCharacterSlots];
            var characters = LoadCharacters();
            for (var i = 0; i < MaxCharacterSlots; i++)
            {
                var data = i < characters.Count ? characters[i] : null;
                var slot = FindOrCreateChild(slotRoot.transform, "CharacterSlot_" + i);
                slot.transform.localPosition = positions[i];
                slot.transform.localRotation = Quaternion.identity;
                slot.transform.localScale = Vector3.one;

                var pedestal = FindOrCreateChild(slot.transform, "Pedestal");
                ConfigureWorldVisual(pedestal, new Vector3(3.1f, 0.36f, 1f), new Color(0.38f, 0.52f, 0.7f, 0.96f), -17, new Vector3(0f, -0.7f, 0f), false);
                DisableWorldText(pedestal.transform, "RoleNameLabel");

                var actor = FindOrCreateChild(slot.transform, "Actor");
                actor.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                actor.transform.localRotation = Quaternion.identity;
                actor.transform.localScale = i == 1 ? new Vector3(1.25f, 1.25f, 1f) : new Vector3(1.1f, 1.1f, 1f);

                var bodyRenderer = GetOrAddComponent<SpriteRenderer>(actor);
                var roleSprite = LoadRoleDisplaySprite(data, i);
                if (roleSprite != null)
                {
                    bodyRenderer.sprite = roleSprite;
                }
                else if (ShouldAssignGeneratedRoleSprite(bodyRenderer))
                {
                    bodyRenderer.sprite = GetWhiteSprite();
                }

                if (bodyRenderer.sprite != null && (IsPlaceholderSprite(bodyRenderer.sprite) || roleSprite == null))
                {
                    bodyRenderer.color = GetActorPlaceholderColor(i);
                }
                else
                {
                    bodyRenderer.color = Color.white;
                }

                bodyRenderer.sortingOrder = -16;

                var collider = GetOrAddComponent<CircleCollider2D>(actor);
                collider.isTrigger = false;
                collider.radius = 0.74f;

                var actorController = GetOrAddComponent<CharacterSelectAvatarController>(actor);
                actorController.SetControllable(false);
                actorController.SetMoveSpeed(8f);
                actorController.ConfigureDodge(3f, 0.15f, 0.65f, KeyCode.Space);
                actorController.ConfigureLobbyLoadout(
                    data,
                    ResolveLobbyWeaponSprite(data != null ? data.initialWeapon1 : string.Empty),
                    ResolveLobbyWeaponSprite(data != null ? data.initialWeapon2 : string.Empty));
                ConfigureRolePreview(actor, bodyRenderer, actorController, data);
                ConfigureRangerRollPreview(actor, bodyRenderer, actorController, data);

                var ring = FindOrCreateChild(actor.transform, "SelectionRing");
                ring.transform.localPosition = new Vector3(0f, -1.02f, 0f);
                ring.transform.localRotation = Quaternion.identity;
                ring.transform.localScale = new Vector3(2.1f, 0.24f, 1f);
                var ringRenderer = GetOrAddComponent<SpriteRenderer>(ring);
                if (!HasCustomSprite(ringRenderer))
                {
                    ringRenderer.sprite = GetWhiteSprite();
                    ringRenderer.color = new Color(1f, 0.87f, 0.32f, 0.96f);
                }

                ringRenderer.sortingOrder = -17;
                ringRenderer.enabled = false;

                var target = GetOrAddComponent<CharacterSelectTarget>(actor);
                target.Setup(data, bodyRenderer, ringRenderer);
                var ambientHint = GetOrAddComponent<CharacterSelectAmbientHint>(slot);
                ambientHint.Configure(GetRoleHint(data, i), new Vector3(0f, 2.75f, 0f), 2.65f);
                targets[i] = target;
            }

            authoredTargets = targets;
        }

        private void EnsurePortal(Transform parent)
        {
            var portal = FindOrCreateChild(parent, "RunScenePortal");
            ConfigureWorldSprite(
                portal,
                new Vector3(2.3f, 2.3f, 1f),
                new Color(0.42f, 0.7f, 1f, 0.8f),
                -15,
                new Vector3(0f, 8.35f, 0f),
                false,
                "Assets/Sprite/Room/Teleport/transfer_gate_0.png");
            ConfigurePortalFlipbook(portal);

            var trigger = GetOrAddComponent<CircleCollider2D>(portal);
            trigger.isTrigger = true;
            trigger.radius = 0.78f;

            portalController = GetOrAddComponent<CharacterSelectPortalController>(portal);
            portalController.Configure(runSceneName, KeyCode.E);
            portalController.SetPortalEnabled(false);

            var portalFrame = FindOrCreateChild(portal.transform, "PortalFrame");
            ConfigureWorldVisual(portalFrame, new Vector3(2.8f, 0.32f, 1f), new Color(0.2f, 0.34f, 0.5f, 0.62f), -16, new Vector3(0f, -0.98f, 0f), false);
            DisableWorldText(portal.transform, "PortalLabel");
        }

        private void DisableLegacySelectionAvatar(Transform parent)
        {
            var avatarObject = FindOrCreateChild(parent, "SelectionAvatar");
            var avatarBody = avatarObject.GetComponent<Rigidbody2D>();
            if (avatarBody != null)
            {
                avatarBody.velocity = Vector2.zero;
                avatarBody.angularVelocity = 0f;
                avatarBody.simulated = false;
            }

            var avatarCollider = avatarObject.GetComponent<Collider2D>();
            if (avatarCollider != null)
            {
                avatarCollider.enabled = false;
            }

            avatarController = avatarObject.GetComponent<CharacterSelectAvatarController>();
            if (avatarController != null)
            {
                avatarController.SetControllable(false);
            }

            avatarObject.SetActive(false);

            var spawnPointObject = FindOrCreateChild(parent, "AvatarSpawnPoint");
            spawnPointObject.transform.localPosition = new Vector3(0f, -6.1f, 0f);
            spawnPointObject.transform.localRotation = Quaternion.identity;
            spawnPointObject.transform.localScale = Vector3.one;
            avatarSpawnPoint = spawnPointObject.transform;
        }

        private void EnsureUi(Transform parent)
        {
            var canvasObject = FindOrCreateUiChild(parent, "CharacterSelectCanvas");
            var canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var leftPanel = CreatePanel(
                canvasObject.transform,
                "LeftStatsPanel",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 28f),
                new Vector2(430f, 360f),
                new Color(0.045f, 0.075f, 0.105f, 0.9f));

            var rightPanel = CreatePanel(
                canvasObject.transform,
                "RightInfoPanel",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-28f, 28f),
                new Vector2(500f, 430f),
                new Color(0.045f, 0.075f, 0.105f, 0.9f));

            var bottomPanel = CreatePanel(
                canvasObject.transform,
                "BottomPanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 20f),
                new Vector2(920f, 180f),
                new Color(0.045f, 0.075f, 0.105f, 0.86f));

            var topHint = EnsureBoxText(
                canvasObject.transform,
                "TopHintText",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(980f, 52f),
                29,
                TextAnchor.MiddleCenter);
            topHint.text = "准备大厅 / 角色选择";
            topHint.color = new Color(0.92f, 0.96f, 1f, 0.98f);

            var leftTitle = EnsurePanelText(leftPanel.transform, "LeftTitle", 22f, 38f, 27, TextAnchor.UpperLeft);
            leftTitle.text = "属性";
            leftTitle.color = new Color(0.88f, 0.95f, 1f, 1f);

            characterNameText = EnsurePanelText(leftPanel.transform, "CharacterNameText", 70f, 38f, 26, TextAnchor.UpperLeft);
            redHealthText = EnsurePanelText(leftPanel.transform, "RedHealthText", 120f, 34f, 24, TextAnchor.UpperLeft);
            blueArmorText = EnsurePanelText(leftPanel.transform, "BlueArmorText", 162f, 34f, 24, TextAnchor.UpperLeft);
            energyText = EnsurePanelText(leftPanel.transform, "EnergyText", 204f, 34f, 24, TextAnchor.UpperLeft);

            var rightTitle = EnsurePanelText(rightPanel.transform, "RightTitle", 22f, 38f, 27, TextAnchor.UpperLeft);
            rightTitle.text = "技能与初始武器";
            rightTitle.color = new Color(0.88f, 0.95f, 1f, 1f);

            skillNameText = EnsurePanelText(rightPanel.transform, "SkillNameText", 72f, 34f, 24, TextAnchor.UpperLeft);
            skillDescriptionText = EnsurePanelText(rightPanel.transform, "SkillDescriptionText", 114f, 160f, 22, TextAnchor.UpperLeft);
            skillDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            initialWeapon1Text = EnsurePanelText(rightPanel.transform, "InitialWeapon1Text", 296f, 38f, 23, TextAnchor.UpperLeft);
            initialWeapon2Text = EnsurePanelText(rightPanel.transform, "InitialWeapon2Text", 340f, 38f, 23, TextAnchor.UpperLeft);

            confirmButton = EnsureButton(
                bottomPanel.transform,
                "ConfirmSelectionButton",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -20f),
                new Vector2(320f, 58f),
                "确认选择",
                new Color(0.19f, 0.56f, 0.36f, 0.96f),
                out confirmButtonLabel);

            statusText = EnsureBoxText(
                bottomPanel.transform,
                "StatusText",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(860f, 76f),
                21,
                TextAnchor.MiddleCenter);
            statusText.color = new Color(0.86f, 0.95f, 1f, 0.98f);
            statusText.text = "请选择一个角色。";

            infoPanelController = GetOrAddComponent<CharacterInfoPanelController>(canvasObject);
            infoPanelController.BindDetailPanels(leftPanel, rightPanel);
            infoPanelController.SetDetailsVisible(false);
        }

        private List<CharacterData> LoadCharacters()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnsureDefaultCharacterAssets();
            }
#endif
            var result = new List<CharacterData>(MaxCharacterSlots);
            var loaded = Resources.LoadAll<CharacterData>(resourcesPath);
            if (loaded != null)
            {
                for (var i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null)
                    {
                        result.Add(loaded[i]);
                        if (result.Count >= MaxCharacterSlots)
                        {
                            return result;
                        }
                    }
                }
            }

            result.Add(CreateRuntimeCharacter(
                "ranger",
                "游侠",
                24,
                6,
                100,
                "战术翻滚",
                "快速翻滚，并获得短暂无敌窗口。",
                "Pulse Carbine",
                "Burst Revolver",
                new Color(0.35f, 0.78f, 1f, 1f)));
            result.Add(CreateRuntimeCharacter(
                "guardian",
                "守卫",
                34,
                14,
                80,
                "壁垒姿态",
                "暂时获得伤害减免并恢复护甲。",
                "Heavy Shotgun",
                "Shock Hammer",
                new Color(0.38f, 0.9f, 0.5f, 1f)));
            result.Add(CreateRuntimeCharacter(
                "operator",
                "特勤",
                18,
                4,
                140,
                "超频",
                "提升射速，但会额外消耗能量。",
                "Needle SMG",
                "Rail Pistol",
                new Color(0.98f, 0.72f, 0.38f, 1f)));

            return result;
        }

        private static CharacterData CreateRuntimeCharacter(
            string id,
            string name,
            int redHealth,
            int blueArmor,
            int energy,
            string skillName,
            string skillDescription,
            string weapon1,
            string weapon2,
            Color worldTint)
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterId = id;
            data.characterName = name;
            data.redHealth = redHealth;
            data.blueArmor = blueArmor;
            data.energy = energy;
            data.skillName = skillName;
            data.skillDescription = skillDescription;
            data.initialWeapon1 = weapon1;
            data.initialWeapon2 = weapon2;
            data.worldTint = worldTint;
            return data;
        }

        private void EnsureMainCamera()
        {
            var targetCamera = worldCamera != null ? worldCamera : Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }

            if (targetCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                targetCamera = cameraObject.AddComponent<Camera>();
            }

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = 12.8f;
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.backgroundColor = new Color(0.085f, 0.115f, 0.155f, 1f);
            worldCamera = targetCamera;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Sprite GetWhiteSprite()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var builtinSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                if (builtinSprite != null)
                {
                    return builtinSprite;
                }
            }
#endif

            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "CharacterSelect_WhitePixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "CharacterSelect_WhitePixelSprite";
            return whiteSprite;
        }

        private static void ConfigureWorldVisual(
            GameObject target,
            Vector3 scale,
            Color color,
            int sortingOrder,
            Vector3 localPosition,
            bool solidCollider)
        {
            var renderer = GetOrAddComponent<SpriteRenderer>(target);
            if (!HasCustomSprite(renderer))
            {
                renderer.sprite = GetWhiteSprite();
                renderer.color = color;
            }

            renderer.sortingOrder = sortingOrder;

            target.transform.localScale = scale;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;

            if (solidCollider)
            {
                var collider = GetOrAddComponent<BoxCollider2D>(target);
                collider.isTrigger = false;
                collider.size = Vector2.one;

                var body = GetOrAddComponent<Rigidbody2D>(target);
                body.bodyType = RigidbodyType2D.Static;
                body.simulated = true;
                body.gravityScale = 0f;
            }
        }

        private static void ConfigureInvisibleAnchor(GameObject target, Vector3 localPosition)
        {
            target.transform.localScale = Vector3.one;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;

            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static void ConfigureWorldSprite(
            GameObject target,
            Vector3 scale,
            Color color,
            int sortingOrder,
            Vector3 localPosition,
            bool solidCollider,
            string editorSpritePath)
        {
            ConfigureWorldVisual(target, scale, color, sortingOrder, localPosition, solidCollider);
            var renderer = GetOrAddComponent<SpriteRenderer>(target);
            var sprite = LoadEditorSprite(editorSpritePath);
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }

            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void DisableWorldText(Transform parent, string name)
        {
            if (parent == null)
            {
                return;
            }

            var textTransform = parent.Find(name);
            if (textTransform == null)
            {
                return;
            }

            var meshRenderer = textTransform.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            var textMesh = textTransform.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = string.Empty;
            }
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var panel = FindOrCreateUiChild(parent, name);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = GetOrAddComponent<Image>(panel);
            image.color = color;
            return panel;
        }

        private static Text EnsurePanelText(
            Transform parent,
            string name,
            float top,
            float height,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = FindOrCreateUiChild(parent, name);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -top);
            rect.sizeDelta = new Vector2(-40f, height);

            var text = GetOrAddComponent<Text>(textObject);
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (string.IsNullOrWhiteSpace(text.text))
            {
                text.text = "--";
            }

            return text;
        }

        private static Text EnsureBoxText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = FindOrCreateUiChild(parent, name);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = GetOrAddComponent<Text>(textObject);
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (string.IsNullOrWhiteSpace(text.text))
            {
                text.text = "--";
            }

            return text;
        }

        private static Button EnsureButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            string labelText,
            Color color,
            out Text label)
        {
            var buttonObject = FindOrCreateUiChild(parent, name);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = GetOrAddComponent<Image>(buttonObject);
            image.color = color;
            var button = GetOrAddComponent<Button>(buttonObject);

            var labelObject = FindOrCreateUiChild(buttonObject.transform, "Label");
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            label = GetOrAddComponent<Text>(labelObject);
            label.font = GetUiFont();
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = labelText;

            return button;
        }

        private static void DestroyAllOfType<T>() where T : Component
        {
            var all = UnityEngine.Object.FindObjectsOfType<T>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    UnityEngine.Object.Destroy(all[i].gameObject);
                }
            }
        }

        private static void DestroyObjectsByName(string targetName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var item = all[i];
                if (item == null || item.name != targetName)
                {
                    continue;
                }

                var go = item.gameObject;
                if (!go.scene.IsValid())
                {
                    continue;
                }

                UnityEngine.Object.Destroy(go);
            }
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject FindOrCreateUiChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static Sprite LoadRoleDisplaySprite(CharacterData data, int index)
        {
            var idleFrames = LoadRoleFlipbookFrames(data, true);
            return idleFrames.Length > 0 ? idleFrames[0] : null;
        }

        private static Sprite ResolveLobbyWeaponSprite(string weaponLabel)
        {
            if (string.IsNullOrWhiteSpace(weaponLabel))
            {
                return null;
            }

            var compact = weaponLabel.Replace(" ", string.Empty);
            var direct = Resources.Load<Sprite>("Images/Weapon/" + compact);
            if (direct != null)
            {
                return direct;
            }

            switch (weaponLabel.Trim())
            {
                case "Pulse Carbine":
                    return Resources.Load<Sprite>("Images/Weapon/AssaultRifle");
                case "Burst Revolver":
                    return Resources.Load<Sprite>("Images/Weapon/DesertEagle");
                case "Heavy Shotgun":
                    return Resources.Load<Sprite>("Images/Weapon/GrenadePistol");
                case "Shock Hammer":
                    return Resources.Load<Sprite>("Images/Weapon/Shield");
                case "Needle SMG":
                    return Resources.Load<Sprite>("Images/Weapon/NextNextNextGenSMG") ??
                           Resources.Load<Sprite>("Images/Weapon/UZI");
                case "Rail Pistol":
                    return Resources.Load<Sprite>("Images/Weapon/BlueFireGatling") ??
                           Resources.Load<Sprite>("Images/Weapon/P250Pistol");
                default:
                    return Resources.Load<Sprite>("Images/Weapon/AssaultRifle");
            }
        }

        private static bool ShouldAssignGeneratedRoleSprite(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return true;
            }

            if (IsPlaceholderSprite(renderer.sprite))
            {
                return true;
            }

            var spriteName = renderer.sprite.name;
            return !string.IsNullOrEmpty(spriteName) &&
                   spriteName.IndexOf("Roll", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ConfigureRangerRollPreview(GameObject actor, SpriteRenderer bodyRenderer, CharacterSelectAvatarController actorController, CharacterData data)
        {
            if (actor == null || actorController == null)
            {
                return;
            }

            if (!IsRangerData(data))
            {
                actorController.SetRollPreviewAnimator(null);
                var staleAnimator = actor.GetComponent<PlayerRollSpriteAnimator>();
                if (staleAnimator != null)
                {
                    DestroyComponent(staleAnimator);
                }

                return;
            }

            var rollAnimator = GetOrAddComponent<PlayerRollSpriteAnimator>(actor);
            rollAnimator.SetTargetRenderer(bodyRenderer);
            rollAnimator.CaptureDefaultSprite();
            actorController.SetRollPreviewAnimator(rollAnimator);
        }

        private static void ConfigureRolePreview(GameObject actor, SpriteRenderer bodyRenderer, CharacterSelectAvatarController actorController, CharacterData data)
        {
            if (actor == null || bodyRenderer == null)
            {
                return;
            }

            var idleFrames = LoadRoleFlipbookFrames(data, true);
            var walkFrames = LoadRoleFlipbookFrames(data, false);
            if (idleFrames.Length > 0 && idleFrames[0] != null)
            {
                bodyRenderer.sprite = idleFrames[0];
                bodyRenderer.color = Color.white;
            }

            var preview = GetOrAddComponent<CharacterPreviewAnimator>(actor);
            preview.Configure(
                bodyRenderer,
                actorController,
                idleFrames,
                walkFrames,
                8f,
                true);
        }

        private static void ConfigurePortalFlipbook(GameObject portal)
        {
            if (portal == null)
            {
                return;
            }

            var renderer = GetOrAddComponent<SpriteRenderer>(portal);
            var frames = LoadPortalFlipbookFrames();
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            var animator = GetOrAddComponent<SpriteFlipbookAnimator>(portal);
            animator.Configure(renderer, frames, PortalFlipbookFps, true);
        }

        private static Sprite[] LoadPortalFlipbookFrames()
        {
            var frames = new List<Sprite>(8);
            for (var i = 0; i <= 7; i++)
            {
                var frame = LoadEditorSprite("Assets/Sprite/Room/Teleport/transfer_gate_" + i + ".png");
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames.ToArray();
        }

        private static Sprite[] LoadRoleFlipbookFrames(CharacterData data, bool idle)
        {
            if (data == null)
            {
                return Array.Empty<Sprite>();
            }

            var id = data.characterId;
            if (string.Equals(id, "guardian", System.StringComparison.OrdinalIgnoreCase))
            {
                return LoadEditorSpriteFrames(idle ? "Assets/Sprite/Character/Knight/1/Idle_Sheet.png" : "Assets/Sprite/Character/Knight/1/Run_Sheet.png");
            }

            if (string.Equals(id, "operator", System.StringComparison.OrdinalIgnoreCase))
            {
                return LoadEditorSpriteFrames(idle ? "Assets/Sprite/Character/Assassin/Idle_Sheet.png" : "Assets/Sprite/Character/Assassin/Run_Sheet.png");
            }

            if (string.Equals(id, "ranger", System.StringComparison.OrdinalIgnoreCase))
            {
                return LoadEditorSpriteFrames(idle ? "Assets/Sprite/Character/Ranger/1/Idle_Sheet.png" : "Assets/Sprite/Character/Ranger/1/Run_Sheet.png");
            }

            return Array.Empty<Sprite>();
        }

        private static Sprite[] LoadEditorSpriteFrames(string assetPath)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return Array.Empty<Sprite>();
            }

            var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath.Trim());
            if (assets == null || assets.Length == 0)
            {
                return Array.Empty<Sprite>();
            }

            var frames = new List<Sprite>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    frames.Add(sprite);
                }
            }

            frames.Sort(CompareSpritesBySheetRect);
            return frames.ToArray();
#else
            return Array.Empty<Sprite>();
#endif
        }

#if UNITY_EDITOR
        private static int CompareSpritesBySheetRect(Sprite left, Sprite right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var yCompare = right.rect.y.CompareTo(left.rect.y);
            return yCompare != 0 ? yCompare : left.rect.x.CompareTo(right.rect.x);
        }
#endif

        private static bool IsRangerData(CharacterData data)
        {
            if (data == null)
            {
                return false;
            }

            return string.Equals(data.characterId, "ranger", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(data.characterName, "游侠", System.StringComparison.Ordinal);
        }

        private static string GetRoleHint(CharacterData data, int index)
        {
            if (data != null)
            {
                if (string.Equals(data.characterId, "guardian", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "稳扎稳打才是关键。";
                }

                if (string.Equals(data.characterId, "operator", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "火力窗口很短，节奏要快。";
                }

                if (string.Equals(data.characterId, "ranger", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "想试试更灵活的打法吗？";
                }
            }

            return index == 0 ? "先看看这套配置。" : "靠近后再确认选择。";
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private static bool HasCustomSprite(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var sprite = renderer.sprite;
            if (sprite == null)
            {
                return false;
            }

            return !IsPlaceholderSprite(sprite);
        }

        private static bool IsPlaceholderSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return true;
            }

            return string.Equals(sprite.name, "CharacterSelect_WhitePixelSprite", System.StringComparison.Ordinal) ||
                   string.Equals(sprite.name, "CharacterSelect_WhitePixel", System.StringComparison.Ordinal) ||
                   string.Equals(sprite.name, "Point", System.StringComparison.Ordinal);
        }

        private static Color GetActorPlaceholderColor(int index)
        {
            switch (index)
            {
                case 0:
                    return new Color(0.34f, 0.78f, 1f, 1f);
                case 1:
                    return new Color(0.44f, 0.9f, 0.52f, 1f);
                default:
                    return new Color(1f, 0.7f, 0.34f, 1f);
            }
        }

        private static Font GetUiFont()
        {
            if (uiFont != null)
            {
                return uiFont;
            }

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            return uiFont;
        }

        private static Sprite LoadEditorSprite(string assetPath)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath.Trim());
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private void EnsureDefaultCharacterAssets()
        {
            var resourcesRoot = "Assets/Resources";
            var targetFolder = resourcesRoot + "/" + resourcesPath;
            if (!AssetDatabase.IsValidFolder(resourcesRoot))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                AssetDatabase.CreateFolder(resourcesRoot, resourcesPath);
            }

            EnsureDefaultCharacterAsset(
                targetFolder + "/RangerCharacterData.asset",
                "ranger",
                "游侠",
                24,
                6,
                100,
                "战术翻滚",
                "快速翻滚，并获得短暂无敌窗口。",
                "Pulse Carbine",
                "Burst Revolver",
                new Color(0.35f, 0.78f, 1f, 1f));

            EnsureDefaultCharacterAsset(
                targetFolder + "/GuardianCharacterData.asset",
                "guardian",
                "守卫",
                34,
                14,
                80,
                "壁垒姿态",
                "暂时获得伤害减免并恢复护甲。",
                "Heavy Shotgun",
                "Shock Hammer",
                new Color(0.38f, 0.9f, 0.5f, 1f));

            EnsureDefaultCharacterAsset(
                targetFolder + "/OperatorCharacterData.asset",
                "operator",
                "特勤",
                18,
                4,
                140,
                "超频",
                "提升射速，但会额外消耗能量。",
                "Needle SMG",
                "Rail Pistol",
                new Color(0.98f, 0.72f, 0.38f, 1f));

            AssetDatabase.SaveAssets();
        }

        private static void EnsureDefaultCharacterAsset(
            string assetPath,
            string characterId,
            string characterName,
            int redHealth,
            int blueArmor,
            int energy,
            string skillName,
            string skillDescription,
            string weapon1,
            string weapon2,
            Color worldTint)
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            if (data == null)
            {
                data = CreateRuntimeCharacter(
                    characterId,
                    characterName,
                    redHealth,
                    blueArmor,
                    energy,
                    skillName,
                    skillDescription,
                    weapon1,
                    weapon2,
                    worldTint);
                AssetDatabase.CreateAsset(data, assetPath);
                return;
            }

            data.characterId = characterId;
            data.characterName = characterName;
            data.redHealth = redHealth;
            data.blueArmor = blueArmor;
            data.energy = energy;
            data.skillName = skillName;
            data.skillDescription = skillDescription;
            data.initialWeapon1 = weapon1;
            data.initialWeapon2 = weapon2;
            data.worldTint = worldTint;
            EditorUtility.SetDirty(data);
        }
#endif
    }
}

