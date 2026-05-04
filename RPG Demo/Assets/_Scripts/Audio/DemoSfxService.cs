using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARPGDemo.Audio
{
    [DisallowMultipleComponent]
    public class DemoSfxService : MonoBehaviour
    {
        private const string KeyMasterVolume = "SETTINGS_MASTER_VOLUME";
        private const string KeySfxVolume = "SETTINGS_SFX_VOLUME";

        private static DemoSfxService instance;

        [Header("Clips")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip playerAttackClip;
        [SerializeField] private AudioClip enemyAttackClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip criticalHitClip;
        [SerializeField] private AudioClip chestOpenClip;
        [SerializeField] private AudioClip weaponSwitchClip;
        [SerializeField] private AudioClip playerHitClip;
        [SerializeField] private AudioClip armorBreakClip;
        [SerializeField] private AudioClip enemyDeathClip;
        [SerializeField] private AudioClip bossAttackClip;
        [SerializeField] private AudioClip bossDeathClip;

        [Header("Routing")]
        [SerializeField] private string playerActorId = "Player";
        [SerializeField] private string[] bossActorIds = { "Enemy_04", "Boss" };

        [Header("Playback")]
        [SerializeField] [Range(0f, 1f)] private float globalVolumeScale = 1f;
        [SerializeField] private bool autoBindUiButtons = true;
        [SerializeField] private int worldSourcePoolSize = 8;
        [SerializeField] private bool verboseLog;

        [Header("Audio Source")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioSource[] worldSourcePool = new AudioSource[0];

        private int nextWorldSourceIndex;

        public static DemoSfxService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<DemoSfxService>(true);
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            EnsureUiAudioSource();
            EnsureWorldSourcePool();
        }

        private void OnEnable()
        {
            EventCenter.AddListener<ComboStageChangedEvent>(OnComboStageChanged);
            EventCenter.AddListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.AddListener<WeaponSwitchedEvent>(OnWeaponSwitched);
            EventCenter.AddListener<ArmorBrokenEvent>(OnArmorBroken);
            EventCenter.AddListener<AttackPerformedEvent>(OnAttackPerformed);
            EventCenter.AddListener<ActorDiedEvent>(OnActorDied);
            EventCenter.AddListener<ChestOpenedEvent>(OnChestOpened);
            EventCenter.AddListener<AudioPlayEvent>(OnAudioPlayEvent);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (autoBindUiButtons)
            {
                BindUiButtonsInScene(SceneManager.GetActiveScene());
            }
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<ComboStageChangedEvent>(OnComboStageChanged);
            EventCenter.RemoveListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.RemoveListener<WeaponSwitchedEvent>(OnWeaponSwitched);
            EventCenter.RemoveListener<ArmorBrokenEvent>(OnArmorBroken);
            EventCenter.RemoveListener<AttackPerformedEvent>(OnAttackPerformed);
            EventCenter.RemoveListener<ActorDiedEvent>(OnActorDied);
            EventCenter.RemoveListener<ChestOpenedEvent>(OnChestOpened);
            EventCenter.RemoveListener<AudioPlayEvent>(OnAudioPlayEvent);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public static void TryPlayUiClick()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PlayUiClickInternal();
        }

        public static void NotifyWeaponSwitched(string actorId, Vector3 worldPosition)
        {
            EventCenter.Broadcast(new WeaponSwitchedEvent(actorId, worldPosition));
        }

        public void SetGlobalVolumeScale(float scale)
        {
            globalVolumeScale = Mathf.Clamp01(scale);
        }

        public void SetVerboseLog(bool enabled)
        {
            verboseLog = enabled;
        }

        public void SetClips(
            AudioClip uiClick,
            AudioClip playerAttack,
            AudioClip enemyAttack,
            AudioClip hit,
            AudioClip critHit,
            AudioClip chestOpen)
        {
            uiClickClip = uiClick;
            playerAttackClip = playerAttack;
            enemyAttackClip = enemyAttack;
            hitClip = hit;
            criticalHitClip = critHit;
            chestOpenClip = chestOpen;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!autoBindUiButtons)
            {
                return;
            }

            BindUiButtonsInScene(scene);
        }

        private void OnComboStageChanged(ComboStageChangedEvent evt)
        {
            if (!evt.IsStarting)
            {
                return;
            }

            if (!IsPlayerActor(evt.ActorId))
            {
                return;
            }

            AudioClip clip = playerAttackClip;
            Vector3 worldPos = ResolveActorPosition(evt.ActorId);
            PlayWorld(clip, worldPos, 1f);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.FinalDamage <= 0)
            {
                return;
            }

            if (!IsPlayerActor(evt.TargetId))
            {
                return;
            }

            AudioClip baseHit = playerHitClip != null ? playerHitClip : hitClip;
            AudioClip clip = evt.IsCritical && criticalHitClip != null ? criticalHitClip : baseHit;
            PlayWorld(clip, evt.HitPosition, 1f);
        }

        private void OnWeaponSwitched(WeaponSwitchedEvent evt)
        {
            if (!IsPlayerActor(evt.ActorId))
            {
                return;
            }

            PlayWorld(weaponSwitchClip, evt.WorldPosition, 1f);
        }

        private void OnArmorBroken(ArmorBrokenEvent evt)
        {
            if (evt.Team != ActorTeam.Player || !IsPlayerActor(evt.ActorId))
            {
                return;
            }

            PlayWorld(armorBreakClip, evt.WorldPosition, 1f);
        }

        private void OnAttackPerformed(AttackPerformedEvent evt)
        {
            if (evt.Team != ActorTeam.Enemy || !evt.IsBoss)
            {
                return;
            }

            AudioClip clip = bossAttackClip != null ? bossAttackClip : enemyAttackClip;
            PlayWorld(clip, evt.WorldPosition, 1f);
        }

        private void OnActorDied(ActorDiedEvent evt)
        {
            if (evt.Team != ActorTeam.Enemy)
            {
                return;
            }

            bool isBoss = IsBossActor(evt.ActorId);
            AudioClip clip = isBoss
                ? (bossDeathClip != null ? bossDeathClip : enemyDeathClip)
                : enemyDeathClip;

            PlayWorld(clip, evt.WorldPosition, 1f);
        }

        private void OnChestOpened(ChestOpenedEvent evt)
        {
            PlayWorld(chestOpenClip, evt.WorldPosition, 1f);
        }

        private void OnAudioPlayEvent(AudioPlayEvent evt)
        {
            if (evt.Clip == null)
            {
                return;
            }

            if (evt.IsUISound)
            {
                PlayUi(evt.Clip, evt.VolumeScale);
                return;
            }

            PlayWorld(evt.Clip, evt.WorldPosition, evt.VolumeScale);
        }

        private void PlayUiClickInternal()
        {
            PlayUi(uiClickClip, 1f);
        }

        private void PlayUi(AudioClip clip, float volumeScale)
        {
            if (clip == null)
            {
                return;
            }

            EnsureUiAudioSource();
            if (uiAudioSource == null)
            {
                return;
            }

            float volume = ResolveVolume(volumeScale);
            uiAudioSource.PlayOneShot(clip, volume);
        }

        private void PlayWorld(AudioClip clip, Vector3 worldPosition, float volumeScale)
        {
            if (clip == null)
            {
                return;
            }

            EnsureWorldSourcePool();
            if (worldSourcePool == null || worldSourcePool.Length == 0)
            {
                return;
            }

            AudioSource source = worldSourcePool[nextWorldSourceIndex];
            nextWorldSourceIndex++;
            if (nextWorldSourceIndex >= worldSourcePool.Length)
            {
                nextWorldSourceIndex = 0;
            }

            if (source == null)
            {
                return;
            }

            float volume = ResolveVolume(volumeScale);
            source.transform.position = worldPosition;
            source.PlayOneShot(clip, volume);
        }

        private float ResolveVolume(float volumeScale)
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, 1f));
            float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 1f));
            return Mathf.Clamp01(master * sfx * Mathf.Clamp01(volumeScale) * globalVolumeScale);
        }

        private void EnsureUiAudioSource()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
            }

            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            }

            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.dopplerLevel = 0f;
            uiAudioSource.priority = 96;
        }

        private void EnsureWorldSourcePool()
        {
            int safeSize = Mathf.Clamp(worldSourcePoolSize, 1, 24);
            if (worldSourcePool != null && worldSourcePool.Length == safeSize)
            {
                bool allValid = true;
                for (int i = 0; i < worldSourcePool.Length; i++)
                {
                    if (worldSourcePool[i] == null)
                    {
                        allValid = false;
                        break;
                    }
                }

                if (allValid)
                {
                    return;
                }
            }

            worldSourcePool = new AudioSource[safeSize];
            for (int i = 0; i < safeSize; i++)
            {
                GameObject go = new GameObject($"SfxWorldSource_{i + 1:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.priority = 96;
                worldSourcePool[i] = source;
            }

            nextWorldSourceIndex = 0;
        }

        private void BindUiButtonsInScene(Scene scene)
        {
            Button[] buttons = FindObjectsOfType<Button>(true);
            int boundCount = 0;

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.gameObject.scene != scene)
                {
                    continue;
                }

                if (button.GetComponent<UIButtonSfxHook>() != null)
                {
                    continue;
                }

                button.gameObject.AddComponent<UIButtonSfxHook>();
                boundCount++;
            }

            if (verboseLog && boundCount > 0)
            {
                Debug.Log($"[DemoSfxService] Bound UI click SFX hooks: {boundCount}", this);
            }
        }

        private static Vector3 ResolveActorPosition(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return Vector3.zero;
            }

            ActorStats[] all = FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < all.Length; i++)
            {
                ActorStats stats = all[i];
                if (stats != null && stats.ActorId == actorId)
                {
                    return stats.transform.position;
                }
            }

            return Vector3.zero;
        }

        private bool IsPlayerActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(playerActorId) && actorId == playerActorId)
            {
                return true;
            }

            ActorStats stats = ResolveActorStats(actorId);
            return stats != null && stats.Team == ActorTeam.Player;
        }

        private bool IsBossActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return false;
            }

            if (bossActorIds != null)
            {
                for (int i = 0; i < bossActorIds.Length; i++)
                {
                    string id = bossActorIds[i];
                    if (!string.IsNullOrEmpty(id) && actorId == id)
                    {
                        return true;
                    }
                }
            }

            return actorId.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ActorStats ResolveActorStats(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return null;
            }

            ActorStats[] all = FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < all.Length; i++)
            {
                ActorStats stats = all[i];
                if (stats != null && stats.ActorId == actorId)
                {
                    return stats;
                }
            }

            return null;
        }
    }
}
