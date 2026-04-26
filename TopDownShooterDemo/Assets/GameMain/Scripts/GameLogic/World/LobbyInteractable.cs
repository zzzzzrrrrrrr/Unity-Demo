using System;
using GameMain.GameLogic.CharacterSelect;
using GameMain.GameLogic.Meta;
using GameMain.GameLogic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// CharacterSelectScene meta-layer bootstrap: HUD, settings/equipment buttons, and display-only NPCs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyInteractable : MonoBehaviour
    {
        private const string CharacterSelectSceneName = "CharacterSelectScene";
        private const string LobbyCanvasName = "LobbyMetaCanvas";
        private const string LobbyRootName = "LobbyMetaLayer";

        private static bool sceneCallbacksRegistered;
        private SettingsPanel settingsPanel;
        private EquipmentPanel equipmentPanel;
        private DialogPanel dialogPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            sceneCallbacksRegistered = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallbacks()
        {
            if (sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneCallbacksRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            EnsureLobby(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureLobby(scene);
        }

        private static void EnsureLobby(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, CharacterSelectSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (FindObjectOfType<LobbyInteractable>() != null)
            {
                return;
            }

            var root = new GameObject(LobbyRootName);
            root.AddComponent<LobbyInteractable>();
        }

        private void Awake()
        {
            BuildLobbyMetaLayer();
        }

        private void BuildLobbyMetaLayer()
        {
            PlayerProfileService.Load();
            SettingsPanel.ApplySavedSettings();
            MetaUiFactory.EnsureEventSystem();

            var canvas = MetaUiFactory.EnsureCanvas(LobbyCanvasName, 25);
            var metaHud = MetaHudPanel.Create(canvas.transform);
            HideMetaHudBlueBlock(metaHud);
            settingsPanel = SettingsPanel.Create(canvas.transform);
            equipmentPanel = EquipmentPanel.Create(canvas.transform);
            dialogPanel = DialogPanel.Create(canvas.transform);

            var settingsButton = MetaUiFactory.CreateButton(canvas.transform, "SettingsButton", "设置", new Vector2(760f, 462f), new Vector2(140f, 42f));
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(settingsPanel.Show);

            var equipmentButton = MetaUiFactory.CreateButton(canvas.transform, "EquipmentButton", "装备/背包", new Vector2(590f, 462f), new Vector2(170f, 42f));
            equipmentButton.onClick.RemoveAllListeners();
            equipmentButton.onClick.AddListener(equipmentPanel.Show);

            CreateNpc("WeaponMerchant", "武器商人", new Vector3(-17.1f, 5.6f, 0f), "Images/Materials/WeaponCoupon", new Color(0.76f, 0.9f, 1f, 1f), new[]
            {
                "战斗中靠近武器台按 E，可以替换当前武器槽。",
                "右侧武器通常火力更猛，但节奏也更重。",
            });
            CreateNpc("TrainingOfficer", "训练官", new Vector3(17.1f, 5.3f, 0f), "Images/Materials/Battery", new Color(0.78f, 1f, 0.78f, 1f), new[]
            {
                "WASD 移动，Space 闪避，F 使用角色技能。",
                "Q 可以切换两把初始武器。",
            });
            CreateNpc("SystemsEngineer", "工程师", new Vector3(-17.2f, -8.4f, 0f), "Images/Materials/Gear", new Color(0.9f, 0.86f, 1f, 1f), new[]
            {
                "按 I 打开战斗信息面板。",
                "面板里的 Reload Lua 会按本地热更优先级重新读取配置。",
            });
        }

        private void Update()
        {
            if (equipmentPanel == null)
            {
                return;
            }

            var selection = FindObjectOfType<CharacterSelectionController>();
            if (selection != null)
            {
                equipmentPanel.BindSelectionController(selection);
            }
        }

        private void CreateNpc(string objectName, string displayName, Vector3 position, string spritePath, Color fallbackColor, string[] lines)
        {
            var npcObject = new GameObject(objectName);
            npcObject.transform.SetParent(transform, false);
            npcObject.transform.position = position;
            npcObject.transform.localScale = Vector3.one;

            var renderer = npcObject.AddComponent<SpriteRenderer>();
            renderer.sprite = MetaUiFactory.LoadSprite(spritePath);
            renderer.color = renderer.sprite != null ? Color.white : fallbackColor;
            renderer.sortingOrder = 20;

            if (renderer.sprite == null)
            {
                npcObject.transform.localScale = new Vector3(0.85f, 1.15f, 1f);
            }

            var nameTag = new GameObject("NameTag");
            nameTag.transform.SetParent(npcObject.transform, false);
            nameTag.transform.localPosition = new Vector3(0f, -1.12f, 0f);

            var collider = npcObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 1.25f;

            var body = npcObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            var prompt = new GameObject("PromptAnchor");
            prompt.transform.SetParent(npcObject.transform, false);
            prompt.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            var trigger = npcObject.AddComponent<NpcDialogueTrigger>();
            trigger.Configure(displayName, lines, dialogPanel, prompt, KeyCode.E);
        }

        private static void HideMetaHudBlueBlock(MetaHudPanel metaHud)
        {
            if (metaHud == null)
            {
                return;
            }

            var coinIcon = metaHud.transform.Find("CoinIcon");
            if (coinIcon == null)
            {
                return;
            }

            var image = coinIcon.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
                image.raycastTarget = false;
            }
        }
    }
}
