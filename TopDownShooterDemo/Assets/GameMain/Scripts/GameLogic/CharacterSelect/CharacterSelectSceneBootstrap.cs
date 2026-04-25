using System.Collections.Generic;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.UI;
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
            selectionController.SelectFirstAvailable();

            if (selectionController.SelectedCharacterData == null)
            {
                infoPanelController.ShowCharacter(null);
                infoPanelController.SetStatus("未找到角色数据。请在 Resources/CharacterSelect 下添加 CharacterData。");
            }
            else
            {
                infoPanelController.SetStatus("左键选择角色 → 点击确认选择 → 使用 WASD 控制角色 → 在传送门处按 E 进入战斗。");
            }

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

            ConfigureWorldVisual(FindOrCreateChild(room.transform, "Floor"), new Vector3(42f, 26f, 1f), new Color(0.08f, 0.1f, 0.14f, 1f), -30, Vector3.zero, false);
            ConfigureWorldVisual(FindOrCreateChild(room.transform, "InnerMat"), new Vector3(33f, 19f, 1f), new Color(0.14f, 0.18f, 0.24f, 0.92f), -29, new Vector3(0f, 0.1f, 0f), false);

            ConfigureWall(FindOrCreateChild(room.transform, "WallTopLeft"), new Vector3(19.5f, 0.8f, 1f), new Vector3(-11.4f, 12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallTopRight"), new Vector3(19.5f, 0.8f, 1f), new Vector3(11.4f, 12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallBottom"), new Vector3(42.4f, 0.8f, 1f), new Vector3(0f, -12.7f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallLeft"), new Vector3(0.8f, 25.5f, 1f), new Vector3(-21.2f, 0f, 0f));
            ConfigureWall(FindOrCreateChild(room.transform, "WallRight"), new Vector3(0.8f, 25.5f, 1f), new Vector3(21.2f, 0f, 0f));

            var roomTitle = FindOrCreateChild(room.transform, "RoomTitlePlate");
            ConfigureWorldVisual(roomTitle, new Vector3(12f, 0.5f, 1f), new Color(0.23f, 0.31f, 0.42f, 0.85f), -27, new Vector3(0f, 11f, 0f), false);
        }

        private static void ConfigureWall(GameObject wallObject, Vector3 scale, Vector3 localPosition)
        {
            ConfigureWorldVisual(wallObject, scale, new Color(0.24f, 0.34f, 0.46f, 0.96f), -28, localPosition, true);
        }

        private void EnsureCharacterShowcase(Transform parent)
        {
            var showcaseRoot = FindOrCreateChild(parent, "CharacterShowcase");
            showcaseRoot.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            showcaseRoot.transform.localRotation = Quaternion.identity;
            showcaseRoot.transform.localScale = Vector3.one;

            var stage = FindOrCreateChild(showcaseRoot.transform, "Stage");
            ConfigureWorldVisual(stage, new Vector3(19f, 0.4f, 1f), new Color(0.22f, 0.3f, 0.4f, 0.92f), -18, new Vector3(0f, -1.4f, 0f), false);

            var slotRoot = FindOrCreateChild(showcaseRoot.transform, "Slots");
            var positions = new[] { -6.8f, 0f, 6.8f };
            var targets = new CharacterSelectTarget[MaxCharacterSlots];
            var characters = LoadCharacters();
            for (var i = 0; i < MaxCharacterSlots; i++)
            {
                var slot = FindOrCreateChild(slotRoot.transform, "CharacterSlot_" + i);
                slot.transform.localPosition = new Vector3(positions[i], -1.2f, 0f);
                slot.transform.localRotation = Quaternion.identity;
                slot.transform.localScale = Vector3.one;

                var pedestal = FindOrCreateChild(slot.transform, "Pedestal");
                ConfigureWorldVisual(pedestal, new Vector3(2.6f, 0.28f, 1f), new Color(0.32f, 0.4f, 0.5f, 0.94f), -17, new Vector3(0f, -0.62f, 0f), false);

                var actor = FindOrCreateChild(slot.transform, "Actor");
                actor.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                actor.transform.localRotation = Quaternion.identity;
                actor.transform.localScale = new Vector3(0.95f, 1.4f, 1f);

                var bodyRenderer = GetOrAddComponent<SpriteRenderer>(actor);
                if (!HasCustomSprite(bodyRenderer))
                {
                    bodyRenderer.sprite = GetWhiteSprite();
                }

                if (bodyRenderer.sprite != null && IsPlaceholderSprite(bodyRenderer.sprite))
                {
                    bodyRenderer.color = GetActorPlaceholderColor(i);
                }

                bodyRenderer.sortingOrder = -16;

                var collider = GetOrAddComponent<CircleCollider2D>(actor);
                collider.isTrigger = false;
                collider.radius = 0.74f;

                var actorController = GetOrAddComponent<CharacterSelectAvatarController>(actor);
                actorController.SetControllable(false);
                actorController.SetMoveSpeed(4.8f);

                var ring = FindOrCreateChild(actor.transform, "SelectionRing");
                ring.transform.localPosition = new Vector3(0f, -1.02f, 0f);
                ring.transform.localRotation = Quaternion.identity;
                ring.transform.localScale = new Vector3(1.9f, 0.22f, 1f);
                var ringRenderer = GetOrAddComponent<SpriteRenderer>(ring);
                if (!HasCustomSprite(ringRenderer))
                {
                    ringRenderer.sprite = GetWhiteSprite();
                    ringRenderer.color = new Color(1f, 0.87f, 0.32f, 0.96f);
                }

                ringRenderer.sortingOrder = -17;
                ringRenderer.enabled = false;

                var target = GetOrAddComponent<CharacterSelectTarget>(actor);
                var data = i < characters.Count ? characters[i] : null;
                target.Setup(data, bodyRenderer, ringRenderer);
                targets[i] = target;
            }

            authoredTargets = targets;
        }

        private void EnsurePortal(Transform parent)
        {
            var portal = FindOrCreateChild(parent, "RunScenePortal");
            ConfigureWorldVisual(portal, new Vector3(1.5f, 1.8f, 1f), new Color(0.42f, 0.58f, 0.74f, 0.52f), -15, new Vector3(0f, 8.8f, 0f), false);

            var trigger = GetOrAddComponent<CircleCollider2D>(portal);
            trigger.isTrigger = true;
            trigger.radius = 0.52f;

            portalController = GetOrAddComponent<CharacterSelectPortalController>(portal);
            portalController.Configure(runSceneName, KeyCode.E);
            portalController.SetPortalEnabled(false);

            var portalFrame = FindOrCreateChild(portal.transform, "PortalFrame");
            ConfigureWorldVisual(portalFrame, new Vector3(1.2f, 1.25f, 1f), new Color(0.2f, 0.34f, 0.5f, 0.54f), -16, Vector3.zero, false);
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
                new Color(0.07f, 0.11f, 0.16f, 0.9f));

            var rightPanel = CreatePanel(
                canvasObject.transform,
                "RightInfoPanel",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-28f, 28f),
                new Vector2(500f, 430f),
                new Color(0.07f, 0.11f, 0.16f, 0.9f));

            var bottomPanel = CreatePanel(
                canvasObject.transform,
                "BottomPanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 20f),
                new Vector2(920f, 180f),
                new Color(0.07f, 0.11f, 0.16f, 0.88f));

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
            topHint.text = "角色选择房间";
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
            statusText.text = "左键选择角色 → 点击确认选择 → 使用 WASD 控制角色 → 在传送门处按 E 进入战斗。";

            infoPanelController = GetOrAddComponent<CharacterInfoPanelController>(canvasObject);
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
            targetCamera.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
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
            var all = Object.FindObjectsOfType<T>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    Object.Destroy(all[i].gameObject);
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

                Object.Destroy(go);
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
