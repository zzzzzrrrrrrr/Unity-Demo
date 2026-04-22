using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameMain.Builtin.Sound
{
    [Serializable]
    public struct AudioClipBindingEntry
    {
        public int soundId;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    /// <summary>
    /// Inspector-driven clip mapping by SoundIds.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioClipBindings", menuName = "GameMain/Audio/Clip Bindings")]
    public sealed class AudioClipBindings : ScriptableObject
    {
        public List<AudioClipBindingEntry> bgm = new List<AudioClipBindingEntry>();
        public List<AudioClipBindingEntry> sfx = new List<AudioClipBindingEntry>();
    }
}
