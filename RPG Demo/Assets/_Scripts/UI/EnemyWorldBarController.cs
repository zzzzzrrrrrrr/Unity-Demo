// Path: Assets/_Scripts/UI/EnemyWorldBarController.cs
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    public class EnemyWorldBarController : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private ActorStats targetStats;
        [SerializeField] private string targetActorId = string.Empty;
        [SerializeField] private bool autoBindFromParent = true;

        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider hpSmoothSlider;
        [SerializeField] private Text hpText;
        [SerializeField] private UIFollowTarget2D followTarget;

        [Header("Follow")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Smoothing")]
        [SerializeField] private float smoothSharpness = 12f;
        [SerializeField] private bool hideWhenDead = true;

        [Header("Auto Build")]
        [SerializeField] private bool autoCreateMinimalBar = true;
        [SerializeField] private Vector2 autoBarSize = new Vector2(180f, 18f);

        [Header("Debug")]
        [SerializeField] private bool debugHudLog = false;
        [SerializeField] private bool runtimeSyncFromStats = true;

        private float targetHp;
        private float targetMaxHp = 1f;
        private float visualHpSmooth;
        private string lastBindingLogActorId = string.Empty;
        private bool loggedInitialBinding;
        private bool warnedBindingFailure;
        private bool warnedUnsafeRoot;
        private bool warnedMissingRectTransform;
        private bool warnedUnsafeSerializedReference;
        private bool warnedNonEnemyTarget;
        private CanvasGroup visibilityGroup;

        public void BindToTarget(ActorStats stats)
        {
            if (stats != null && stats.Team != ActorTeam.Enemy)
            {
                if (!warnedNonEnemyTarget)
                {
                    warnedNonEnemyTarget = true;
                    Debug.LogWarning("[HUD][Enemy] Reject non-enemy target for world bar: " + GetPath(stats.transform), this);
                }

                return;
            }

            targetStats = stats;
            targetActorId = stats != null ? stats.ActorId : string.Empty;
            HardSyncDisplayedBar(true, "BindToTarget");
        }

        private void Awake()
        {
            ResolveSafeRootTransform();
            visibilityGroup = GetOrAddVisibilityGroup();

            if (followTarget == null)
            {
                followTarget = GetComponent<UIFollowTarget2D>();
            }

            EnsureMinimalBar();
            RebindUiReferences();
            EnsureSliderVisualReady(hpSlider, new Color(0.90f, 0.22f, 0.22f, 0.95f));
            EnsureSliderVisualReady(hpSmoothSlider, new Color(0.45f, 0.10f, 0.10f, 0.90f));

            EnsureTargetStatsBinding(true, false);
            visualHpSmooth = targetHp;
            HardSyncDisplayedBar(true, "Awake");
        }

        private void OnEnable()
        {
            EventCenter.AddListener<ActorHealthChangedEvent>(OnActorHealthChanged);
            EventCenter.AddListener<ActorDiedEvent>(OnActorDied);
            EventCenter.AddListener<ActorRevivedEvent>(OnActorRevived);
            HardSyncDisplayedBar(true, "OnEnable");
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<ActorHealthChangedEvent>(OnActorHealthChanged);
            EventCenter.RemoveListener<ActorDiedEvent>(OnActorDied);
            EventCenter.RemoveListener<ActorRevivedEvent>(OnActorRevived);
        }

        private void LateUpdate()
        {
            HardSyncDisplayedBar(false, "LateUpdate");
        }

        private void OnActorHealthChanged(ActorHealthChangedEvent evt)
        {
            Log("Recv ActorHealthChangedEvent ActorId=" + evt.ActorId + ", Team=" + evt.Team + ", HP=" + evt.CurrentHp + "/" + evt.MaxHp);

            if (evt.Team != ActorTeam.Enemy)
            {
                return;
            }

            if (!ShouldProcessEnemyEvent(evt.ActorId))
            {
                return;
            }

            EnsureTargetStatsBinding();
            HardSyncDisplayedBar(true, "OnActorHealthChanged");
        }

        private void OnActorDied(ActorDiedEvent evt)
        {
            if (evt.Team != ActorTeam.Enemy)
            {
                return;
            }

            if (!ShouldProcessEnemyEvent(evt.ActorId))
            {
                return;
            }

            EnsureTargetStatsBinding();
            HardSyncDisplayedBar(true, "OnActorDied");
        }

        private void OnActorRevived(ActorRevivedEvent evt)
        {
            if (evt.Team != ActorTeam.Enemy)
            {
                return;
            }

            if (!ShouldProcessEnemyEvent(evt.ActorId))
            {
                return;
            }

            EnsureTargetStatsBinding();
            HardSyncDisplayedBar(true, "OnActorRevived");
        }

        private void HardSyncDisplayedBar(bool logNow, string source)
        {
            RebindUiReferences();
            EnsureTargetStatsBinding(false, logNow);
            SyncFromTargetStats();

            Slider displayedMain;
            Slider displayedSmooth;
            Text displayedText;
            ResolveDisplayedBarComponents(out displayedMain, out displayedSmooth, out displayedText);

            if (displayedMain != null)
            {
                displayedMain.minValue = 0f;
                displayedMain.maxValue = targetMaxHp;
                displayedMain.SetValueWithoutNotify(targetHp);
                hpSlider = displayedMain;
            }

            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            visualHpSmooth = MathUtility2D.ExpSmooth(visualHpSmooth <= 0f ? targetHp : visualHpSmooth, targetHp, smoothSharpness * 0.5f, dt);
            if (displayedSmooth != null && displayedSmooth != displayedMain)
            {
                displayedSmooth.minValue = 0f;
                displayedSmooth.maxValue = targetMaxHp;
                displayedSmooth.SetValueWithoutNotify(visualHpSmooth);
                hpSmoothSlider = displayedSmooth;
            }

            if (displayedText != null)
            {
                displayedText.text = Mathf.CeilToInt(targetHp) + " / " + Mathf.CeilToInt(targetMaxHp);
                hpText = displayedText;
            }

            if (hideWhenDead)
            {
                SetVisible(targetHp > 0f);
            }

            if (!logNow)
            {
                return;
            }

            float mainValue = displayedMain != null ? displayedMain.value : -1f;
            float smoothValue = displayedSmooth != null ? displayedSmooth.value : -1f;
            string textValue = displayedText != null ? displayedText.text : "(null)";
            Log("EventWrite Source=" + source
                + ", ActorId=" + (targetStats != null ? targetStats.ActorId : targetActorId)
                + ", RealHP=" + targetHp + "/" + targetMaxHp
                + ", HP_Main.value=" + mainValue
                + ", HP_Smooth.value=" + smoothValue
                + ", HP_Text.text=" + textValue
                + ", Paths(Main/Smooth/Text)="
                + GetPath(displayedMain != null ? displayedMain.transform : null) + " | "
                + GetPath(displayedSmooth != null ? displayedSmooth.transform : null) + " | "
                + GetPath(displayedText != null ? displayedText.transform : null));
        }

        private bool EnsureTargetStatsBinding(bool forceRebind = false, bool logResult = false)
        {
            string expectedActorId = GetExpectedActorIdForBinding();
            ActorStats boundFromFollow;
            bool hasFollowBinding = TryBindFromFollowTarget(out boundFromFollow);
            bool followAccepted = false;
            if (hasFollowBinding)
            {
                if (string.IsNullOrEmpty(expectedActorId) || boundFromFollow.ActorId == expectedActorId)
                {
                    targetStats = boundFromFollow;
                    followAccepted = true;
                }
            }

            bool hasMismatch = targetStats != null
                && !string.IsNullOrEmpty(expectedActorId)
                && targetStats.ActorId != expectedActorId;

            if (forceRebind || targetStats == null || hasMismatch)
            {
                if (!string.IsNullOrEmpty(expectedActorId))
                {
                    targetStats = FindStatsByActorId(expectedActorId);
                }

                if (targetStats == null && autoBindFromParent)
                {
                    ActorStats parentStats = GetComponentInParent<ActorStats>();
                    if (parentStats != null && (string.IsNullOrEmpty(expectedActorId) || parentStats.ActorId == expectedActorId))
                    {
                        targetStats = parentStats;
                    }
                }
            }

            bool hasValidTarget = targetStats != null && targetStats.Team == ActorTeam.Enemy;
            if (targetStats != null && !hasValidTarget)
            {
                targetStats = null;
            }

            if (targetStats != null)
            {
                bool targetChanged = targetActorId != targetStats.ActorId;
                targetActorId = targetStats.ActorId;

                if (followTarget != null)
                {
                    Transform preferredWorldTarget = ResolvePreferredFollowWorldTarget(targetStats.transform, followTarget.WorldTarget);
                    if (followTarget.WorldTarget != preferredWorldTarget)
                    {
                        followTarget.WorldTarget = preferredWorldTarget;
                    }

                    followTarget.WorldOffset = preferredWorldTarget == targetStats.transform ? followOffset : Vector3.zero;
                }

                if (logResult || !loggedInitialBinding || targetChanged)
                {
                    loggedInitialBinding = true;
                    lastBindingLogActorId = targetActorId;
                    LogBindingResult(hasFollowBinding, followAccepted, expectedActorId);
                }
            }
            else if (logResult && !warnedBindingFailure)
            {
                warnedBindingFailure = true;
                Debug.LogWarning("[HUD][Enemy] Bind failed. ExpectedActorId=" + expectedActorId, this);
            }

            return targetStats != null;
        }

        private void LogBindingResult(bool hasFollowBinding, bool followAccepted, string expectedActorId)
        {
            Log("BindResult FollowBound=" + hasFollowBinding
                + ", FollowAccepted=" + followAccepted
                + ", ExpectedActorId=" + expectedActorId
                + ", TargetActorId=" + targetActorId
                + ", TargetPath=" + GetPath(targetStats != null ? targetStats.transform : null)
                + ", TargetHP=" + (targetStats != null ? targetStats.CurrentHealth : -1f)
                + "/" + (targetStats != null ? targetStats.MaxHealth : -1f));
        }

        private bool TryBindFromFollowTarget(out ActorStats boundStats)
        {
            boundStats = null;

            if (followTarget == null)
            {
                followTarget = GetComponent<UIFollowTarget2D>();
            }

            if (followTarget == null || followTarget.WorldTarget == null)
            {
                return false;
            }

            Transform worldTarget = followTarget.WorldTarget;
            boundStats = worldTarget.GetComponent<ActorStats>();
            if (boundStats == null)
            {
                boundStats = worldTarget.GetComponentInParent<ActorStats>();
            }

            if (boundStats == null)
            {
                return false;
            }

            return true;
        }

        private void SyncFromTargetStats()
        {
            if (!runtimeSyncFromStats)
            {
                return;
            }

            if (targetStats == null)
            {
                EnsureTargetStatsBinding();
            }

            if (targetStats == null)
            {
                targetMaxHp = Mathf.Max(1f, targetMaxHp);
                targetHp = Mathf.Clamp(targetHp, 0f, targetMaxHp);
                return;
            }

            targetMaxHp = Mathf.Max(1f, targetStats.MaxHealth);
            targetHp = Mathf.Clamp(targetStats.CurrentHealth, 0f, targetMaxHp);
        }

        private void ResolveDisplayedBarComponents(out Slider displayedMain, out Slider displayedSmooth, out Text displayedText)
        {
            Transform rootTr = root != null ? root.transform : transform;

            displayedMain = FindNamedComponentInChildren<Slider>(rootTr, "HP_Main");
            displayedSmooth = FindNamedComponentInChildren<Slider>(rootTr, "HP_Smooth");
            displayedText = FindNamedComponentInChildren<Text>(rootTr, "HP_Text");

            if (displayedMain == null)
            {
                displayedMain = hpSlider;
            }

            if (displayedSmooth == null)
            {
                displayedSmooth = hpSmoothSlider;
            }

            if (displayedText == null)
            {
                displayedText = hpText;
            }
        }

        private ActorStats FindStatsByActorId(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return null;
            }

            ActorStats[] all = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].ActorId == actorId)
                {
                    return all[i];
                }
            }

            return null;
        }

        private bool ShouldProcessEnemyEvent(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return false;
            }

            string expected = GetExpectedActorIdForBinding();
            if (string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return actorId == expected;
        }

        private string GetExpectedActorIdForBinding()
        {
            if (!string.IsNullOrEmpty(targetActorId))
            {
                return targetActorId;
            }

            string fromBarName = ResolveActorIdFromBarName();
            if (!string.IsNullOrEmpty(fromBarName))
            {
                return fromBarName;
            }

            if (targetStats != null && !string.IsNullOrEmpty(targetStats.ActorId))
            {
                return targetStats.ActorId;
            }

            return string.Empty;
        }

        private string ResolveActorIdFromBarName()
        {
            string barName = !string.IsNullOrEmpty(name) ? name : transform.name;
            const string suffix = "_WorldBar";

            if (string.IsNullOrEmpty(barName) || !barName.EndsWith(suffix))
            {
                return string.Empty;
            }

            return barName.Substring(0, barName.Length - suffix.Length);
        }

        private static Transform ResolvePreferredFollowWorldTarget(Transform actorRoot, Transform currentWorldTarget)
        {
            if (actorRoot == null)
            {
                return null;
            }

            if (IsUsableBarAnchor(actorRoot, currentWorldTarget))
            {
                return currentWorldTarget;
            }

            Transform barAnchor = actorRoot.Find("BarAnchor");
            if (barAnchor == null)
            {
                Transform legacyHeadAnchor = actorRoot.Find("HeadAnchor");
                if (legacyHeadAnchor != null)
                {
                    legacyHeadAnchor.name = "BarAnchor";
                    barAnchor = legacyHeadAnchor;
                }
            }

            return barAnchor != null ? barAnchor : actorRoot;
        }

        private static bool IsUsableBarAnchor(Transform actorRoot, Transform candidate)
        {
            if (actorRoot == null || candidate == null)
            {
                return false;
            }

            if (candidate == actorRoot)
            {
                return false;
            }

            return candidate.IsChildOf(actorRoot);
        }

        private Transform ResolveSafeRootTransform()
        {
            Transform fallback = transform;
            Transform candidate = root != null ? root.transform : null;

            if (candidate == null)
            {
                root = gameObject;
                return fallback;
            }

            if (candidate == fallback)
            {
                return fallback;
            }

            bool unsafeRoot = candidate.GetComponent<Canvas>() != null
                || candidate.GetComponent<PlayerHUDController>() != null
                || candidate.name == "Canvas"
                || candidate.name == "HUD"
                || candidate.name == "PlayerHUD";

            if (unsafeRoot)
            {
                if (!warnedUnsafeRoot)
                {
                    warnedUnsafeRoot = true;
                    Debug.LogWarning("[HUD][Enemy] Unsafe root ignored for world bar: " + GetPath(candidate), this);
                }

                root = gameObject;
                return fallback;
            }

            return candidate;
        }

        private CanvasGroup GetOrAddVisibilityGroup()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void SetVisible(bool visible)
        {
            if (visibilityGroup != null)
            {
                visibilityGroup.alpha = visible ? 1f : 0f;
                visibilityGroup.interactable = visible;
                visibilityGroup.blocksRaycasts = visible;
                return;
            }

            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private void EnsureMinimalBar()
        {
            if (!autoCreateMinimalBar)
            {
                return;
            }

            if (hpSlider != null && hpText != null)
            {
                return;
            }

            RectTransform rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                Debug.LogWarning("[HUD][Enemy] Root must be RectTransform.", this);
                return;
            }

            if (followTarget == null)
            {
                followTarget = GetComponent<UIFollowTarget2D>();
                if (followTarget == null)
                {
                    followTarget = gameObject.AddComponent<UIFollowTarget2D>();
                }
            }

            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = autoBarSize;

            CreateAutoBar(rootRect);
        }

        private void CreateAutoBar(RectTransform rootRect)
        {
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }

            bg.color = new Color(0f, 0f, 0f, 0.55f);

            if (hpSmoothSlider == null)
            {
                hpSmoothSlider = CreateSlider("HP_Smooth", rootRect, new Color(0.45f, 0.10f, 0.10f, 0.90f));
            }

            if (hpSlider == null)
            {
                hpSlider = CreateSlider("HP_Main", rootRect, new Color(0.90f, 0.22f, 0.22f, 0.95f));
            }

            if (hpText == null)
            {
                GameObject textGo = new GameObject("HP_Text", typeof(RectTransform), typeof(Text));
                RectTransform textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(rootRect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                hpText = textGo.GetComponent<Text>();
                hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                hpText.fontSize = 12;
                hpText.alignment = TextAnchor.MiddleCenter;
                hpText.color = Color.white;
                hpText.raycastTarget = false;
            }
        }

        private static Slider CreateSlider(string name, RectTransform parent, Color fillColor)
        {
            GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.SetParent(parent, false);
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = new Vector2(2f, 2f);
            sliderRect.offsetMax = new Vector2(-2f, -2f);

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;

            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(sliderRect, false);
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

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

            return slider;
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

        private void RebindUiReferences()
        {
            Transform rootTr = root != null ? root.transform : transform;
            Slider main = FindNamedComponentInChildren<Slider>(rootTr, "HP_Main");
            if (main != null)
            {
                hpSlider = main;
            }

            Slider smooth = FindNamedComponentInChildren<Slider>(rootTr, "HP_Smooth");
            if (smooth != null)
            {
                hpSmoothSlider = smooth;
            }

            Text text = FindNamedComponentInChildren<Text>(rootTr, "HP_Text");
            if (text != null)
            {
                hpText = text;
            }
        }

        private static T FindNamedComponentInChildren<T>(Transform root, string name) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            T[] all = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private void Log(string msg)
        {
            if (!debugHudLog)
            {
                return;
            }

            Debug.Log("[HUD][Enemy] " + msg, this);
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
