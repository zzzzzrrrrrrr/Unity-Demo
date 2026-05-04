// Path: Assets/_Scripts/UI/PlayerHUDController.cs
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    public class PlayerHUDController : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private string playerActorId = "Player";
        [SerializeField] private bool autoBindFirstPlayer = true;

        [Header("HP")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider hpSmoothSlider;
        [SerializeField] private Text hpText;

        [Header("MP")]
        [SerializeField] private Slider mpSlider;
        [SerializeField] private Slider mpSmoothSlider;
        [SerializeField] private Text mpText;

        [Header("Fill Bars (Optional)")]
        [SerializeField] private Image hpBarFill;
        [SerializeField] private Image armorBarFill;
        [SerializeField] private Image energyBarFill;

        [Header("Follow")]
        [SerializeField] private bool followPlayer = false;
        [SerializeField] private UIFollowTarget2D followTarget;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.8f, 0f);

        [Header("Smoothing")]
        [SerializeField] private float smoothSharpness = 12f;

        [Header("Auto Build")]
        [SerializeField] private bool autoCreateMinimalHud = true;
        [SerializeField] private Vector2 autoHudAnchor = new Vector2(20f, -20f);
        [SerializeField] private Vector2 autoHudSize = new Vector2(320f, 24f);

        [Header("Debug")]
        [SerializeField] private bool debugHudLog = false;
        [SerializeField] private bool runtimeSyncFromStats = true;

        private float targetHp;
        private float targetMaxHp = 1f;
        private float targetMp;
        private float targetMaxMp = 1f;
        private float targetArmor;
        private float targetMaxArmor = 1f;
        private float visualHp;
        private float visualHpSmooth;
        private float visualMp;
        private float visualMpSmooth;
        private float visualArmor;
        private ActorStats boundPlayerStats;
        private bool warnedMissingCanvas;

        private void Awake()
        {
            EnsureMinimalHud();
            EnsureSliderVisualReady(hpSlider, new Color(0.9f, 0.22f, 0.22f, 1f));
            EnsureSliderVisualReady(hpSmoothSlider, new Color(0.6f, 0.10f, 0.10f, 0.7f));
            EnsureSliderVisualReady(mpSlider, new Color(0.2f, 0.55f, 0.95f, 1f));
            EnsureSliderVisualReady(mpSmoothSlider, new Color(0.12f, 0.30f, 0.70f, 0.7f));
        }

        private void OnEnable()
        {
            EventCenter.AddListener<ActorHealthChangedEvent>(OnActorHealthChanged);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<ActorHealthChangedEvent>(OnActorHealthChanged);
        }

        private void Start()
        {
            TryBindFromScene();
            ForceRefresh();
            Log("Start bind ActorId=" + playerActorId + ", BoundStats=" + (boundPlayerStats != null));
            Log("UI refs: HP=" + GetPath(hpSlider != null ? hpSlider.transform : null)
                + ", HP_Smooth=" + GetPath(hpSmoothSlider != null ? hpSmoothSlider.transform : null)
                + ", MP=" + GetPath(mpSlider != null ? mpSlider.transform : null)
                + ", MP_Smooth=" + GetPath(mpSmoothSlider != null ? mpSmoothSlider.transform : null)
                + ", HP_Fill=" + GetPath(hpBarFill != null ? hpBarFill.transform : null)
                + ", Armor_Fill=" + GetPath(armorBarFill != null ? armorBarFill.transform : null)
                + ", Energy_Fill=" + GetPath(energyBarFill != null ? energyBarFill.transform : null));
        }

        private void Update()
        {
            if (runtimeSyncFromStats && boundPlayerStats != null)
            {
                targetMaxHp = Mathf.Max(1f, boundPlayerStats.MaxHealth);
                targetHp = Mathf.Clamp(boundPlayerStats.CurrentHealth, 0f, targetMaxHp);
                targetMaxMp = Mathf.Max(1f, boundPlayerStats.MaxMana);
                targetMp = Mathf.Clamp(boundPlayerStats.CurrentMana, 0f, targetMaxMp);
                targetMaxArmor = Mathf.Max(0f, boundPlayerStats.MaxArmor);
                targetArmor = Mathf.Clamp(boundPlayerStats.CurrentArmor, 0f, targetMaxArmor);
            }

            float dt = Time.unscaledDeltaTime;
            visualHp = targetHp;
            visualHpSmooth = MathUtility2D.ExpSmooth(visualHpSmooth, targetHp, smoothSharpness * 0.55f, dt);
            visualMp = targetMp;
            visualMpSmooth = MathUtility2D.ExpSmooth(visualMpSmooth, targetMp, smoothSharpness * 0.55f, dt);
            visualArmor = targetArmor;

            ApplyUI(false);

            if (followPlayer)
            {
                TryBindFollowTarget();
            }
        }

        private void OnActorHealthChanged(ActorHealthChangedEvent evt)
        {
            Log("Recv ActorHealthChangedEvent ActorId=" + evt.ActorId + ", Team=" + evt.Team + ", HP=" + evt.CurrentHp + "/" + evt.MaxHp);

            bool pass = IsPlayerEvent(evt);
            if (!pass)
            {
                Log("Filter reject event ActorId=" + evt.ActorId);
                return;
            }

            if ((boundPlayerStats == null || boundPlayerStats.ActorId != evt.ActorId) && evt.Team == ActorTeam.Player)
            {
                BindPlayerByActorId(evt.ActorId);
            }

            if (string.IsNullOrEmpty(playerActorId) && autoBindFirstPlayer)
            {
                playerActorId = evt.ActorId;
            }

            targetMaxHp = Mathf.Max(1f, evt.MaxHp);
            targetHp = Mathf.Clamp(evt.CurrentHp, 0f, targetMaxHp);
            targetMaxMp = Mathf.Max(1f, evt.MaxMp);
            targetMp = Mathf.Clamp(evt.CurrentMp, 0f, targetMaxMp);
            if (boundPlayerStats != null)
            {
                targetMaxArmor = Mathf.Max(0f, boundPlayerStats.MaxArmor);
                targetArmor = Mathf.Clamp(boundPlayerStats.CurrentArmor, 0f, targetMaxArmor);
            }
            visualHp = targetHp;
            visualMp = targetMp;
            visualArmor = targetArmor;

            ApplyUI(true);
            float hpMain = hpSlider != null ? hpSlider.value : -1f;
            float hpSmooth = hpSmoothSlider != null ? hpSmoothSlider.value : -1f;
            string hpValueText = hpText != null ? hpText.text : "(null)";
            Log("EventWrite ActorId=" + evt.ActorId
                + ", HP_Main.value=" + hpMain
                + ", HP_Smooth.value=" + hpSmooth
                + ", HP_Text.text=" + hpValueText
                + ", TargetPath=" + GetPath(transform));
        }

        private bool IsPlayerEvent(ActorHealthChangedEvent evt)
        {
            if (evt.Team != ActorTeam.Player)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(playerActorId))
            {
                return evt.ActorId == playerActorId;
            }

            return autoBindFirstPlayer;
        }

        private void TryBindFromScene()
        {
            ActorStats[] all = FindObjectsOfType<ActorStats>();
            ActorStats firstPlayer = null;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].Team != ActorTeam.Player)
                {
                    continue;
                }

                if (firstPlayer == null)
                {
                    firstPlayer = all[i];
                }

                if (!string.IsNullOrEmpty(playerActorId) && all[i].ActorId == playerActorId)
                {
                    firstPlayer = all[i];
                    break;
                }
            }

            if (firstPlayer == null)
            {
                return;
            }

            boundPlayerStats = firstPlayer;
            if (string.IsNullOrEmpty(playerActorId) || autoBindFirstPlayer)
            {
                playerActorId = firstPlayer.ActorId;
            }

            targetMaxHp = Mathf.Max(1f, firstPlayer.MaxHealth);
            targetHp = Mathf.Clamp(firstPlayer.CurrentHealth, 0f, targetMaxHp);
            targetMaxMp = Mathf.Max(1f, firstPlayer.MaxMana);
            targetMp = Mathf.Clamp(firstPlayer.CurrentMana, 0f, targetMaxMp);
            targetMaxArmor = Mathf.Max(0f, firstPlayer.MaxArmor);
            targetArmor = Mathf.Clamp(firstPlayer.CurrentArmor, 0f, targetMaxArmor);
        }

        private void BindPlayerByActorId(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return;
            }

            ActorStats[] all = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Team == ActorTeam.Player && all[i].ActorId == actorId)
                {
                    boundPlayerStats = all[i];
                    playerActorId = actorId;
                    Log("Rebind player stats -> " + actorId);
                    return;
                }
            }
        }

        private void TryBindFollowTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            if (boundPlayerStats == null)
            {
                TryBindFromScene();
            }

            if (boundPlayerStats != null)
            {
                followTarget.WorldTarget = boundPlayerStats.transform;
                followTarget.WorldOffset = followOffset;
            }
        }

        private void ForceRefresh()
        {
            visualHp = targetHp;
            visualHpSmooth = targetHp;
            visualMp = targetMp;
            visualMpSmooth = targetMp;
            visualArmor = targetArmor;
            ApplyUI(true);
        }

        private void ApplyUI(bool logNow)
        {
            bool updated = false;

            if (hpSlider != null)
            {
                hpSlider.minValue = 0f;
                hpSlider.maxValue = targetMaxHp;
                hpSlider.value = targetHp;
                updated = true;
            }

            if (hpSmoothSlider != null)
            {
                if (hpSmoothSlider == hpSlider)
                {
                    // A minimal HUD may intentionally use only one HP slider.
                }
                else
                {
                    hpSmoothSlider.maxValue = targetMaxHp;
                    hpSmoothSlider.value = visualHpSmooth;
                    updated = true;
                }
            }

            if (mpSlider != null)
            {
                mpSlider.minValue = 0f;
                mpSlider.maxValue = targetMaxMp;
                mpSlider.value = targetMp;
                updated = true;
            }

            if (mpSmoothSlider != null)
            {
                if (mpSmoothSlider == mpSlider)
                {
                    // A minimal HUD may intentionally use only one MP slider.
                }
                else
                {
                    mpSmoothSlider.maxValue = targetMaxMp;
                    mpSmoothSlider.value = visualMpSmooth;
                    updated = true;
                }
            }

            if (hpText != null)
            {
                hpText.text = "HP " + Mathf.CeilToInt(targetHp) + " / " + Mathf.CeilToInt(targetMaxHp);
                updated = true;
            }

            if (mpText != null)
            {
                mpText.text = "MP " + Mathf.CeilToInt(targetMp) + " / " + Mathf.CeilToInt(targetMaxMp);
                updated = true;
            }

            if (hpBarFill != null)
            {
                hpBarFill.fillAmount = ResolveFillAmount(targetHp, targetMaxHp);
                updated = true;
            }

            if (armorBarFill != null)
            {
                armorBarFill.fillAmount = ResolveFillAmount(visualArmor, targetMaxArmor);
                updated = true;
            }

            if (energyBarFill != null)
            {
                energyBarFill.fillAmount = ResolveFillAmount(targetMp, targetMaxMp);
                updated = true;
            }

            if (logNow)
            {
                float hpMain = hpSlider != null ? hpSlider.value : -1f;
                float hpSmooth = hpSmoothSlider != null ? hpSmoothSlider.value : -1f;
                string hpTextValue = hpText != null ? hpText.text : "(null)";
                string hpMainPath = GetPath(hpSlider != null ? hpSlider.transform : null);
                string hpSmoothPath = GetPath(hpSmoothSlider != null ? hpSmoothSlider.transform : null);
                string hpTextPath = GetPath(hpText != null ? hpText.transform : null);
                float hpFillValue = hpBarFill != null ? hpBarFill.fillAmount : -1f;
                float armorFillValue = armorBarFill != null ? armorBarFill.fillAmount : -1f;
                float energyFillValue = energyBarFill != null ? energyBarFill.fillAmount : -1f;
                Log("UI updated=" + updated + ", TargetHP=" + targetHp + "/" + targetMaxHp
                    + ", HP.value=" + hpMain + ", HP_Smooth.value=" + hpSmooth
                    + ", Armor=" + targetArmor + "/" + targetMaxArmor
                    + ", Fill(HP/Armor/Energy)=" + hpFillValue + "/" + armorFillValue + "/" + energyFillValue
                    + ", HP_Text.text=" + hpTextValue
                    + ", Paths(HP/HP_Smooth/HP_Text)=" + hpMainPath + " | " + hpSmoothPath + " | " + hpTextPath);
            }
        }

        private static float ResolveFillAmount(float value, float max)
        {
            if (max <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(value / max);
        }

        private void EnsureMinimalHud()
        {
            if (!autoCreateMinimalHud)
            {
                return;
            }

            if (hpSlider != null && hpText != null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                if (!warnedMissingCanvas)
                {
                    warnedMissingCanvas = true;
                    Debug.LogWarning("[HUD][Player] Missing Canvas, skip auto HUD build.", this);
                }
                return;
            }

            GameObject staleAutoRoot = GameObject.Find("BattleHUD_Auto");
            if (staleAutoRoot != null && staleAutoRoot != gameObject)
            {
                staleAutoRoot.SetActive(false);
                Log("Disable stale root: " + GetPath(staleAutoRoot.transform));
            }

            GameObject rootGo;
            RectTransform rootRect;

            RectTransform selfRect = transform as RectTransform;
            bool selfUsable = selfRect != null && transform.IsChildOf(canvas.transform);
            if (selfUsable)
            {
                rootGo = gameObject;
                rootRect = selfRect;
            }
            else
            {
                Transform hudTr = canvas.transform.Find("HUD");
                if (hudTr == null)
                {
                    rootGo = new GameObject("HUD", typeof(RectTransform));
                    rootGo.transform.SetParent(canvas.transform, false);
                }
                else
                {
                    rootGo = hudTr.gameObject;
                }

                rootRect = rootGo.GetComponent<RectTransform>();
                if (rootRect == null)
                {
                    rootRect = rootGo.AddComponent<RectTransform>();
                }
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            if (hpSlider == null || hpText == null)
            {
                CreateAutoBar(
                    "PlayerHP_Bar",
                    rootRect,
                    autoHudAnchor,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    autoHudSize,
                    new Color(0.90f, 0.22f, 0.22f, 1f),
                    out hpSlider,
                    out hpText);

                hpSmoothSlider = null;
            }
        }

        private static void CreateAutoBar(
            string rootName,
            Transform parent,
            Vector2 anchoredPos,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Color fillColor,
            out Slider slider,
            out Text valueText)
        {
            GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(Image), typeof(Slider));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = anchorMin;
            rootRect.anchorMax = anchorMax;
            rootRect.pivot = pivot;
            rootRect.anchoredPosition = anchoredPos;
            rootRect.sizeDelta = size;

            Image bgImage = root.GetComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);

            slider = root.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(rootRect, false);
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(4f, 4f);
            fillAreaRect.offsetMax = new Vector2(-4f, -4f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = fillColor;
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;

            GameObject textGo = new GameObject("ValueText", typeof(RectTransform), typeof(Text));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rootRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            valueText = textGo.GetComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 14;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.raycastTarget = false;
        }

        private void EnsureSliderVisualReady(Slider slider, Color fillColor)
        {
            if (slider == null || slider.fillRect != null)
            {
                return;
            }

            RectTransform sliderRect = slider.transform as RectTransform;
            if (sliderRect == null)
            {
                return;
            }

            if (slider.GetComponent<Image>() == null)
            {
                Image bg = slider.gameObject.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.5f);
            }

            GameObject fillArea = new GameObject("FillArea_Auto", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(sliderRect, false);
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            GameObject fill = new GameObject("Fill_Auto", typeof(RectTransform), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = fillColor;
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
        }

        private void Log(string msg)
        {
            if (!debugHudLog)
            {
                return;
            }

            Debug.Log("[HUD][Player] " + msg, this);
        }

        private static string GetPath(Transform tr)
        {
            if (tr == null)
            {
                return "(null)";
            }

            string path = tr.name;
            Transform cur = tr.parent;
            while (cur != null)
            {
                path = cur.name + "/" + path;
                cur = cur.parent;
            }

            return path;
        }
    }
}
