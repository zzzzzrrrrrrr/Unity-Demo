// Path: Assets/_Scripts/Audio/DemoBgmService.cs
// Simple scene-based BGM player for demo usage.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Audio
{
    [DisallowMultipleComponent]
    public class DemoBgmService : MonoBehaviour
    {
        private const string KeyMusicVolume = "SETTINGS_MUSIC_VOLUME";

        [Header("Audio Source")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip fallbackClip;

        [Header("Playback")]
        [SerializeField] private bool playOnSceneLoaded = true;
        [SerializeField] private bool loop = true;
        [SerializeField] [Range(0f, 1f)] private float globalVolumeScale = 1f;
        [SerializeField] private bool verboseLog = false;

        private readonly Dictionary<string, AudioClip> sceneTrackMap = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private float cachedMusicVolume = -1f;

        private void Awake()
        {
            EnsureSource();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            if (playOnSceneLoaded)
            {
                PlayForScene(SceneManager.GetActiveScene().name);
            }

            RefreshVolume(force: true);
        }

        private void Update()
        {
            RefreshVolume(force: false);
        }

        public void SetTrackForScene(string sceneName, AudioClip clip)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            sceneTrackMap[sceneName] = clip;
        }

        public void SetFallbackClip(AudioClip clip)
        {
            fallbackClip = clip;
        }

        public void SetGlobalVolumeScale(float scale)
        {
            globalVolumeScale = Mathf.Clamp01(scale);
            RefreshVolume(force: true);
        }

        public void SetVerboseLog(bool enabled)
        {
            verboseLog = enabled;
        }

        public void PlayForScene(string sceneName)
        {
            EnsureSource();

            AudioClip next = ResolveTrack(sceneName);
            if (next == null)
            {
                if (musicSource.isPlaying)
                {
                    musicSource.Stop();
                }

                musicSource.clip = null;
                if (verboseLog)
                {
                    Debug.Log("[DemoBgmService] No BGM clip mapped for scene: " + sceneName, this);
                }

                return;
            }

            bool shouldRestart = musicSource.clip != next || !musicSource.isPlaying;
            musicSource.loop = loop;
            musicSource.clip = next;
            RefreshVolume(force: true);

            if (shouldRestart)
            {
                musicSource.Play();
                if (verboseLog)
                {
                    Debug.Log("[DemoBgmService] Play BGM '" + next.name + "' for scene: " + sceneName, this);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!playOnSceneLoaded)
            {
                return;
            }

            PlayForScene(scene.name);
        }

        private AudioClip ResolveTrack(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName) &&
                sceneTrackMap.TryGetValue(sceneName, out AudioClip mapped) &&
                mapped != null)
            {
                return mapped;
            }

            return fallbackClip;
        }

        private void EnsureSource()
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            musicSource.playOnAwake = false;
            musicSource.loop = loop;
            musicSource.spatialBlend = 0f;
            musicSource.dopplerLevel = 0f;
            musicSource.priority = 64;
        }

        private void RefreshVolume(bool force)
        {
            if (musicSource == null)
            {
                return;
            }

            float musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, 1f));
            if (!force && Mathf.Abs(musicVolume - cachedMusicVolume) < 0.0001f)
            {
                return;
            }

            cachedMusicVolume = musicVolume;
            musicSource.volume = Mathf.Clamp01(musicVolume * globalVolumeScale);
        }
    }
}

