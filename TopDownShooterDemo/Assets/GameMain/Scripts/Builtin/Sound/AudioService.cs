using System.Collections.Generic;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using UnityEngine;

namespace GameMain.Builtin.Sound
{
    /// <summary>
    /// Runtime audio entry. Handles BGM switching and SFX playback by SoundIds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        private struct ClipEntry
        {
            public AudioClip Clip;
            public float Volume;
        }

        [SerializeField] private AudioClipBindings clipBindings;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private bool autoBindProcedure = true;

        private readonly Dictionary<int, ClipEntry> bgmLookup = new Dictionary<int, ClipEntry>();
        private readonly Dictionary<int, ClipEntry> sfxLookup = new Dictionary<int, ClipEntry>();
        private readonly HashSet<int> missingBgmIds = new HashSet<int>();
        private readonly HashSet<int> missingSfxIds = new HashSet<int>();
        private ProcedureManager boundProcedureManager;
        private int currentBgmId = -1;
        private bool warnedMissingBindings;

        public static AudioService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureAudioSources();
            RebuildLookup();
        }

        private void OnEnable()
        {
            if (autoBindProcedure && GameEntryBridge.IsReady)
            {
                BindProcedureManager(GameEntryBridge.Procedure);
            }
        }

        private void OnDisable()
        {
            UnbindProcedureManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetClipBindings(AudioClipBindings bindings)
        {
            clipBindings = bindings;
            RebuildLookup();
        }

        public void BindAudioSources(AudioSource bgm, AudioSource sfx)
        {
            bgmSource = bgm;
            sfxSource = sfx;
            EnsureAudioSources();
        }

        public void BindProcedureManager(ProcedureManager manager)
        {
            if (boundProcedureManager == manager)
            {
                return;
            }

            UnbindProcedureManager();
            boundProcedureManager = manager;
            if (boundProcedureManager != null)
            {
                boundProcedureManager.ProcedureChanged += OnProcedureChanged;
                OnProcedureChanged(boundProcedureManager.CurrentProcedureType, boundProcedureManager.CurrentProcedureType);
            }
        }

        public void PlayBgm(int soundId, bool restartIfSame = false)
        {
            if (soundId <= 0)
            {
                StopBgm();
                return;
            }

            if (!restartIfSame && currentBgmId == soundId && bgmSource != null && bgmSource.isPlaying)
            {
                return;
            }

            if (!bgmLookup.TryGetValue(soundId, out var entry) || entry.Clip == null)
            {
                WarnMissingClip(soundId, true);
                return;
            }

            EnsureAudioSources();
            currentBgmId = soundId;
            bgmSource.clip = entry.Clip;
            bgmSource.loop = true;
            bgmSource.volume = Mathf.Clamp01(entry.Volume);
            bgmSource.Play();
        }

        public void StopBgm()
        {
            currentBgmId = -1;
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        public void PlaySfx(int soundId)
        {
            if (soundId <= 0 || !sfxLookup.TryGetValue(soundId, out var entry) || entry.Clip == null)
            {
                if (soundId > 0)
                {
                    WarnMissingClip(soundId, false);
                }

                return;
            }

            EnsureAudioSources();
            sfxSource.PlayOneShot(entry.Clip, Mathf.Clamp01(entry.Volume));
        }

        public static void PlayBgmById(int soundId, bool restartIfSame = false)
        {
            Instance?.PlayBgm(soundId, restartIfSame);
        }

        public static void PlaySfxById(int soundId)
        {
            Instance?.PlaySfx(soundId);
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            switch (current)
            {
                case ProcedureType.Menu:
                    PlayBgm(SoundIds.BgmMenu);
                    break;
                case ProcedureType.Battle:
                    PlayBgm(SoundIds.BgmBattle);
                    break;
            }
        }

        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                var go = new GameObject("BgmSource");
                go.transform.SetParent(transform, false);
                bgmSource = go.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }

            if (sfxSource == null)
            {
                var go = new GameObject("SfxSource");
                go.transform.SetParent(transform, false);
                sfxSource = go.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
            }
        }

        private void RebuildLookup()
        {
            bgmLookup.Clear();
            sfxLookup.Clear();

            if (clipBindings == null)
            {
                if (!warnedMissingBindings)
                {
                    Debug.LogWarning("AudioService has no AudioClipBindings assigned. BGM/SFX requests will be ignored.", this);
                    warnedMissingBindings = true;
                }

                return;
            }

            for (var i = 0; i < clipBindings.bgm.Count; i++)
            {
                var entry = clipBindings.bgm[i];
                if (entry.soundId <= 0)
                {
                    continue;
                }

                bgmLookup[entry.soundId] = new ClipEntry
                {
                    Clip = entry.clip,
                    Volume = entry.volume <= 0f ? 1f : entry.volume,
                };
            }

            for (var i = 0; i < clipBindings.sfx.Count; i++)
            {
                var entry = clipBindings.sfx[i];
                if (entry.soundId <= 0)
                {
                    continue;
                }

                sfxLookup[entry.soundId] = new ClipEntry
                {
                    Clip = entry.clip,
                    Volume = entry.volume <= 0f ? 1f : entry.volume,
                };
            }
        }

        private void UnbindProcedureManager()
        {
            if (boundProcedureManager != null)
            {
                boundProcedureManager.ProcedureChanged -= OnProcedureChanged;
                boundProcedureManager = null;
            }
        }

        private void WarnMissingClip(int soundId, bool isBgm)
        {
            var missingSet = isBgm ? missingBgmIds : missingSfxIds;
            if (missingSet.Contains(soundId))
            {
                return;
            }

            missingSet.Add(soundId);
            Debug.LogWarning(
                string.Format("AudioService missing {0} clip for SoundId={1}.", isBgm ? "BGM" : "SFX", soundId),
                this);
        }
    }
}
