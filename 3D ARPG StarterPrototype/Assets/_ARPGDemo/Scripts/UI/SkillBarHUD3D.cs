using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class SkillBarHUD3D : MonoBehaviour
    {
        private const int SlotCount = 3;

        [SerializeField] private PlayerSkillCaster3D caster;
        [SerializeField] private RectTransform skillBarRoot;
        [SerializeField] private Vector2 slotSize = new Vector2(76f, 76f);
        [SerializeField] private float slotSpacing = 8f;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-34f, 34f);

        private readonly SlotView[] slots = new SlotView[SlotCount];
        private Font defaultFont;
        private bool hasWarnedMissingCaster;

        private void Awake()
        {
            ResolveCaster();
            EnsureUI();
        }

        private void Reset()
        {
            ResolveCaster();
        }

        private void Update()
        {
            if (caster == null)
            {
                ResolveCaster();
                WarnMissingCasterOnce();
                return;
            }

            EnsureUI();
            UpdateSlots();
        }

        private void ResolveCaster()
        {
            if (caster == null)
            {
                caster = FindObjectOfType<PlayerSkillCaster3D>();
            }
        }

        private void EnsureUI()
        {
            defaultFont = defaultFont != null ? defaultFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (skillBarRoot == null)
            {
                GameObject bar = new GameObject("SkillBar", typeof(RectTransform));
                bar.transform.SetParent(canvas.transform, false);
                skillBarRoot = bar.GetComponent<RectTransform>();
            }

            ConfigureBarRoot();
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null || slots[i].Root == null)
                {
                    slots[i] = CreateSlot(i);
                }
            }
        }

        private void ConfigureBarRoot()
        {
            skillBarRoot.anchorMin = new Vector2(1f, 0f);
            skillBarRoot.anchorMax = new Vector2(1f, 0f);
            skillBarRoot.pivot = new Vector2(1f, 0f);
            skillBarRoot.anchoredPosition = anchoredPosition;
            skillBarRoot.sizeDelta = new Vector2(slotSize.x * SlotCount + slotSpacing * (SlotCount - 1), slotSize.y);
        }

        private SlotView CreateSlot(int index)
        {
            GameObject root = new GameObject($"SkillSlot_{index + 1}", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(skillBarRoot, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = slotSize;
            rootRect.anchoredPosition = new Vector2(slotSize.x * 0.5f + index * (slotSize.x + slotSpacing), slotSize.y * 0.5f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.12f, 0.82f);

            Image overlay = CreateImage("CooldownOverlay", rootRect, new Color(0f, 0f, 0f, 0.58f));
            RectTransform overlayRect = overlay.rectTransform;
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Text keyText = CreateText("Key", rootRect, 18, TextAnchor.UpperLeft);
            keyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            keyText.rectTransform.anchorMax = new Vector2(0f, 1f);
            keyText.rectTransform.pivot = new Vector2(0f, 1f);
            keyText.rectTransform.anchoredPosition = new Vector2(7f, -5f);
            keyText.rectTransform.sizeDelta = new Vector2(28f, 24f);
            keyText.text = (index + 1).ToString();

            Text nameText = CreateText("Name", rootRect, 11, TextAnchor.LowerCenter);
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
            nameText.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            nameText.rectTransform.sizeDelta = new Vector2(-8f, 24f);

            Text cooldownText = CreateText("Cooldown", rootRect, 18, TextAnchor.MiddleCenter);
            cooldownText.rectTransform.anchorMin = Vector2.zero;
            cooldownText.rectTransform.anchorMax = Vector2.one;
            cooldownText.rectTransform.offsetMin = Vector2.zero;
            cooldownText.rectTransform.offsetMax = Vector2.zero;

            return new SlotView(rootRect, background, overlay, keyText, nameText, cooldownText);
        }

        private void UpdateSlots()
        {
            SkillRuntimeState3D[] states = caster.RuntimeStates;
            for (int i = 0; i < SlotCount; i++)
            {
                SlotView slot = slots[i];
                SkillRuntimeState3D state = i < states.Length ? states[i] : null;
                SkillDefinition3D definition = state?.Definition;

                Color baseColor = definition != null ? definition.VfxColor : Color.gray;
                baseColor.a = state != null && state.IsReady ? 0.82f : 0.42f;
                slot.Background.color = baseColor;
                slot.KeyText.text = (i + 1).ToString();
                slot.NameText.text = definition != null ? ShortName(definition.SkillName) : "EMPTY";

                bool onCooldown = state != null && state.CooldownRemaining > 0f;
                slot.Overlay.enabled = onCooldown;
                slot.CooldownText.enabled = onCooldown;
                slot.CooldownText.text = onCooldown ? state.CooldownRemaining.ToString("0.0") : string.Empty;

                RectTransform overlayRect = slot.Overlay.rectTransform;
                overlayRect.anchorMin = new Vector2(0f, 0f);
                overlayRect.anchorMax = new Vector2(1f, state != null ? state.Cooldown01 : 0f);
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }
        }

        private Image CreateImage(string objectName, RectTransform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string objectName, RectTransform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = defaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void WarnMissingCasterOnce()
        {
            if (hasWarnedMissingCaster)
            {
                return;
            }

            hasWarnedMissingCaster = true;
            Debug.LogWarning("SkillBarHUD3D could not find PlayerSkillCaster3D.", this);
        }

        private static string ShortName(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
            {
                return "SKILL";
            }

            return skillName.Length <= 11 ? skillName : skillName.Substring(0, 11);
        }

        private sealed class SlotView
        {
            public SlotView(RectTransform root, Image background, Image overlay, Text keyText, Text nameText, Text cooldownText)
            {
                Root = root;
                Background = background;
                Overlay = overlay;
                KeyText = keyText;
                NameText = nameText;
                CooldownText = cooldownText;
            }

            public RectTransform Root { get; }
            public Image Background { get; }
            public Image Overlay { get; }
            public Text KeyText { get; }
            public Text NameText { get; }
            public Text CooldownText { get; }
        }
    }
}
